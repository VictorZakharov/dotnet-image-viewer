using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    private const int MaxConcurrentThumbnailLoads = 4;

    private readonly PriorityQueue<QueuedThumbnailRequest, int> _thumbnailQueue = new();
    private readonly Dictionary<ThumbnailItem, int> _queuedThumbnailPriorities = new();
    private readonly Dictionary<ThumbnailItem, ActiveThumbnailRequest> _activeThumbnailRequests = new();
    private CancellationTokenSource _thumbnailSessionCts = new();
    private int _thumbnailGeneration;
    private int _thumbnailInFlight;
    private bool _viewportValid;
    private int _firstVisibleIndex;
    private int _lastVisibleIndex;
    private int _firstOverscanIndex;
    private int _lastOverscanIndex;
    private bool _disposed;

    private sealed record QueuedThumbnailRequest(
        ThumbnailItem Item,
        int Index,
        int Priority,
        int Generation);

    private sealed record ActiveThumbnailRequest(CancellationTokenSource Cancellation);

    public void UpdateThumbnailViewport(
        int firstVisibleIndex,
        int lastVisibleIndex,
        int firstOverscanIndex,
        int lastOverscanIndex)
    {
        if (_disposed) return;

        if (FilteredItems.Count == 0 || lastOverscanIndex < firstOverscanIndex)
        {
            _viewportValid = false;
            ReconcileThumbnailRequests();
            return;
        }

        var lastIndex = FilteredItems.Count - 1;
        _firstVisibleIndex = Math.Clamp(firstVisibleIndex, 0, lastIndex);
        _lastVisibleIndex = Math.Clamp(lastVisibleIndex, _firstVisibleIndex, lastIndex);
        _firstOverscanIndex = Math.Clamp(firstOverscanIndex, 0, _firstVisibleIndex);
        _lastOverscanIndex = Math.Clamp(lastOverscanIndex, _lastVisibleIndex, lastIndex);
        _viewportValid = true;
        ReconcileThumbnailRequests();
    }

    public void ReportRealizedItems(int count) =>
        RealizedItemCount = Math.Max(0, count);

    private int FolderPreviewTier =>
        RoundToCacheTier(Math.Max(64, ThumbnailWidth / 2));

    private bool NeedsCurrentThumbnail(ThumbnailItem item) => item.IsFolder
        ? item.NeedsFolderThumbnails(FolderPreviewTier)
        : item.NeedsThumbnail(_activeCacheTier);

    private void ReconcileThumbnailRequests()
    {
        var desired = BuildDesiredRequests();

        foreach (var pair in _activeThumbnailRequests.ToList())
        {
            if (!desired.ContainsKey(pair.Key))
                pair.Value.Cancellation.Cancel();
        }

        // Rebuild from the small overscan set. This bounds the physical heap as
        // well as the observable queue during repeated high-velocity scrolling.
        _thumbnailQueue.Clear();
        _queuedThumbnailPriorities.Clear();
        foreach (var pair in desired)
        {
            if (_activeThumbnailRequests.ContainsKey(pair.Key)) continue;
            QueueThumbnailRequest(pair.Key, pair.Value.Index, pair.Value.Priority);
        }

        PumpThumbnailQueue();
    }

    private Dictionary<ThumbnailItem, (int Index, int Priority)> BuildDesiredRequests()
    {
        var desired = new Dictionary<ThumbnailItem, (int, int)>();
        if (!_viewportValid || FilteredItems.Count == 0) return desired;

        var center = (_firstVisibleIndex + _lastVisibleIndex) / 2;
        for (var index = _firstOverscanIndex; index <= _lastOverscanIndex; index++)
        {
            var item = FilteredItems[index];
            if (!NeedsCurrentThumbnail(item)) continue;

            var priority = index >= _firstVisibleIndex && index <= _lastVisibleIndex
                ? Math.Abs(index - center)
                : 100_000 + Math.Abs(index - center);
            desired[item] = (index, priority);
        }

        return desired;
    }

    private void QueueThumbnailRequest(ThumbnailItem item, int index, int priority)
    {
        _queuedThumbnailPriorities[item] = priority;
        var request = new QueuedThumbnailRequest(item, index, priority, _thumbnailGeneration);
        _thumbnailQueue.Enqueue(request, priority);
    }

    private void PumpThumbnailQueue()
    {
        while (!_disposed
               && _thumbnailInFlight < MaxConcurrentThumbnailLoads
               && _thumbnailQueue.TryDequeue(out var request, out _))
        {
            if (IsStaleQueuedRequest(request)) continue;

            _queuedThumbnailPriorities.Remove(request.Item);
            if (!IsRequestStillDesired(request) || !NeedsCurrentThumbnail(request.Item))
                continue;

            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
                _thumbnailSessionCts.Token);
            var active = new ActiveThumbnailRequest(cancellation);
            _activeThumbnailRequests[request.Item] = active;
            _thumbnailInFlight++;
            _ = ProcessThumbnailRequestAsync(request, active);
        }

        UpdateSchedulerStatus();
    }

    private bool IsStaleQueuedRequest(QueuedThumbnailRequest request) =>
        request.Generation != _thumbnailGeneration
        || !_queuedThumbnailPriorities.TryGetValue(request.Item, out var priority)
        || priority != request.Priority;

    private bool IsRequestStillDesired(QueuedThumbnailRequest request) =>
        _viewportValid
        && request.Index >= _firstOverscanIndex
        && request.Index <= _lastOverscanIndex
        && request.Index < FilteredItems.Count
        && ReferenceEquals(FilteredItems[request.Index], request.Item);

    private bool IsActiveRequestCurrent(
        QueuedThumbnailRequest request,
        ActiveThumbnailRequest active) =>
        !_disposed
        && !active.Cancellation.IsCancellationRequested
        && request.Generation == _thumbnailGeneration
        && _activeThumbnailRequests.TryGetValue(request.Item, out var current)
        && ReferenceEquals(current, active);

    private async Task ProcessThumbnailRequestAsync(
        QueuedThumbnailRequest request,
        ActiveThumbnailRequest active)
    {
        try
        {
            if (request.Item.IsFolder)
                await LoadFolderPreviewAsync(request, active).ConfigureAwait(false);
            else
                await LoadFileThumbnailAsync(request, active).ConfigureAwait(false);
        }
        catch (OperationCanceledException) { /* expected */ }
        catch { /* one unreadable item must not stop the queue */ }
        finally
        {
            await CompleteThumbnailRequestAsync(request, active).ConfigureAwait(false);
        }
    }

    private async Task CompleteThumbnailRequestAsync(
        QueuedThumbnailRequest request,
        ActiveThumbnailRequest active)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _thumbnailInFlight = Math.Max(0, _thumbnailInFlight - 1);
                if (_activeThumbnailRequests.TryGetValue(request.Item, out var current)
                    && ReferenceEquals(current, active))
                {
                    _activeThumbnailRequests.Remove(request.Item);
                }
                active.Cancellation.Dispose();

                if (_disposed) UpdateSchedulerStatus();
                else ReconcileThumbnailRequests();
            });
        }
        catch
        {
            active.Cancellation.Dispose();
        }
    }

    private void ResetThumbnailRequests()
    {
        _thumbnailGeneration++;
        _viewportValid = false;

        _thumbnailSessionCts.Cancel();
        _thumbnailSessionCts.Dispose();
        _thumbnailSessionCts = new CancellationTokenSource();

        foreach (var active in _activeThumbnailRequests.Values)
            active.Cancellation.Cancel();
        _activeThumbnailRequests.Clear();
        _thumbnailQueue.Clear();
        _queuedThumbnailPriorities.Clear();
        UpdateSchedulerStatus();
    }

    private void UpdateSchedulerStatus()
    {
        QueuedThumbnailCount = _queuedThumbnailPriorities.Count;
        ActiveThumbnailCount = _activeThumbnailRequests.Count;
        IsThumbnailLoading = QueuedThumbnailCount > 0 || ActiveThumbnailCount > 0;
    }

}
