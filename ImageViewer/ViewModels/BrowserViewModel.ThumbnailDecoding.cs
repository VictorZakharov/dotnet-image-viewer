using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    private readonly ThumbnailCache _cache = new();

    private async Task LoadFileThumbnailAsync(
        QueuedThumbnailRequest request,
        ActiveThumbnailRequest active)
    {
        var ct = active.Cancellation.Token;
        Bitmap? thumbnail = null;
        try
        {
            thumbnail = await _cache.GetOrCreateAsync(
                request.Item.Path,
                _activeCacheTier,
                request.Item.IsVideo,
                ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsActiveRequestCurrent(request, active)) return;
                request.Item.ApplyThumbnail(thumbnail, _activeCacheTier);
                thumbnail = null;
            });
        }
        finally
        {
            thumbnail?.Dispose();
        }
    }

    private async Task LoadFolderPreviewAsync(
        QueuedThumbnailRequest request,
        ActiveThumbnailRequest active)
    {
        var item = request.Item;
        var ct = active.Cancellation.Token;
        IReadOnlyList<MediaScanEntry>? previewMedia = null;
        var beganLoading = false;

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!IsActiveRequestCurrent(request, active)) return;
            item.BeginFolderPreviewLoading();
            beganLoading = true;
            if (item.FolderPreviewMediaLoaded)
                previewMedia = item.FolderPreviewMedia;
        });

        if (!beganLoading) return;
        try
        {
            if (previewMedia is null)
                previewMedia = await ScanFolderPreviewAsync(item, request, active, ct)
                    .ConfigureAwait(false);
            if (previewMedia is null) return;

            var tier = FolderPreviewTier;
            var previewCount = Math.Min(ThumbnailItem.FolderPreviewSlotCount, previewMedia.Count);
            for (var index = 0; index < previewCount; index++)
                await LoadFolderPreviewSlotAsync(
                    item, previewMedia[index], index, tier, request, active, ct)
                    .ConfigureAwait(false);

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (IsActiveRequestCurrent(request, active))
                    item.MarkFolderThumbnailsAttempted(tier);
            });
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(item.EndFolderPreviewLoading);
        }
    }

    private async Task<IReadOnlyList<MediaScanEntry>?> ScanFolderPreviewAsync(
        ThumbnailItem item,
        QueuedThumbnailRequest request,
        ActiveThumbnailRequest active,
        CancellationToken ct)
    {
        var scannedMedia = await FolderScanner.ScanPreviewAsync(item.Path, ct)
            .ConfigureAwait(false);
        ct.ThrowIfCancellationRequested();

        IReadOnlyList<MediaScanEntry>? acceptedMedia = null;
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (!IsActiveRequestCurrent(request, active)) return;
            item.SetFolderPreviewMedia(scannedMedia);
            acceptedMedia = item.FolderPreviewMedia;
        });
        return acceptedMedia;
    }

    private async Task LoadFolderPreviewSlotAsync(
        ThumbnailItem item,
        MediaScanEntry media,
        int index,
        int tier,
        QueuedThumbnailRequest request,
        ActiveThumbnailRequest active,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Bitmap? preview = null;
        try
        {
            preview = await _cache.GetOrCreateAsync(
                media.Path,
                tier,
                media.IsVideo,
                ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                if (!IsActiveRequestCurrent(request, active)) return;
                item.ApplyFolderThumbnail(index, preview);
                preview = null;
            });
        }
        finally
        {
            preview?.Dispose();
        }
    }

    private static void DisposeItems(IEnumerable<ThumbnailItem> items)
    {
        foreach (var item in items)
            item.Dispose();
    }
}
