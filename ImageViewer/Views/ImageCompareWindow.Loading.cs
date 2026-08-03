using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;
using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class ImageCompareWindow
{
    private readonly CancellationTokenSource _windowCancellation = new();
    private readonly Dictionary<CompareCandidateViewModel, CancellationTokenSource>
        _candidateLoads = [];
    private readonly SemaphoreSlim _fullResolutionGate = new(2);
    private readonly ThumbnailCache _compareThumbnailCache = new();
    private bool _candidateLoadingStarted;
    private bool _candidateLoadingFinished;
    private bool _loadingInfrastructureDisposed;

    private async void StartCandidateLoading()
    {
        _candidateLoadingStarted = true;
        try
        {
            await Task.WhenAll(_viewModel.Candidates.Select(LoadCandidateAsync));
            if (!_resourcesReleased)
                _viewModel.StatusText =
                    "Full-resolution images loaded. Different metadata values are highlighted.";
        }
        catch (OperationCanceledException) { }
        finally
        {
            _candidateLoadingFinished = true;
            DisposeLoadingInfrastructureWhenSafe();
        }
    }

    private async Task LoadCandidateAsync(CompareCandidateViewModel candidate)
    {
        var cancellation = CancellationTokenSource.CreateLinkedTokenSource(
            _windowCancellation.Token);
        _candidateLoads[candidate] = cancellation;
        var token = cancellation.Token;
        try
        {
            var metadata = await Task.Run(() => ExifReader.Read(candidate.Path), token);
            if (!IsCandidateCurrent(candidate, cancellation)) return;
            candidate.SetMetadata(metadata);
            candidate.Rotation = metadata.OrientationRotation;
            _viewModel.RefreshDifferences();

            var preview = await _compareThumbnailCache.GetOrCreateAsync(
                candidate.Path, 384, token);
            if (preview is not null)
            {
                if (IsCandidateCurrent(candidate, cancellation))
                    candidate.ReplaceBitmap(preview, isFullResolution: false);
                else
                    preview.Dispose();
            }

            await _fullResolutionGate.WaitAsync(token);
            try
            {
                var loaded = await ImageLoader.LoadAsync(candidate.Path, token);
                if (!IsCandidateCurrent(candidate, cancellation))
                {
                    loaded.Bitmap.Dispose();
                    return;
                }
                candidate.ReplaceBitmap(loaded.Bitmap, isFullResolution: true);
                candidate.Rotation = loaded.OrientationBaked
                    ? 0
                    : candidate.Metadata.OrientationRotation;
                if (candidate.Metadata.Width is null || candidate.Metadata.Height is null)
                {
                    candidate.SetMetadata(WithDimensions(
                        candidate.Metadata,
                        loaded.Bitmap.PixelSize.Width,
                        loaded.Bitmap.PixelSize.Height));
                    _viewModel.RefreshDifferences();
                }
                SynchronizeLoadedCandidate(candidate);
                RefreshBlinkImage();
            }
            finally
            {
                _fullResolutionGate.Release();
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (IsCandidateCurrent(candidate, cancellation))
                candidate.LoadError = ex.Message;
        }
        finally
        {
            if (IsCandidateCurrent(candidate, cancellation))
            {
                candidate.IsLoading = false;
                _candidateLoads.Remove(candidate);
            }
            cancellation.Dispose();
        }
    }

    private bool IsCandidateCurrent(
        CompareCandidateViewModel candidate,
        CancellationTokenSource cancellation) =>
        !_resourcesReleased
        && _viewModel.Candidates.Contains(candidate)
        && _candidateLoads.TryGetValue(candidate, out var current)
        && ReferenceEquals(current, cancellation);

    private void CancelCandidateLoad(CompareCandidateViewModel candidate)
    {
        if (_candidateLoads.Remove(candidate, out var cancellation))
            cancellation.Cancel();
    }

    private void CancelCandidateLoading()
    {
        _windowCancellation.Cancel();
        foreach (var cancellation in _candidateLoads.Values) cancellation.Cancel();
        _candidateLoads.Clear();
    }

    private void DisposeLoadingInfrastructureWhenSafe()
    {
        if (!_resourcesReleased || (_candidateLoadingStarted && !_candidateLoadingFinished)
            || _loadingInfrastructureDisposed) return;
        _loadingInfrastructureDisposed = true;
        _windowCancellation.Dispose();
        _fullResolutionGate.Dispose();
    }

    private static ImageMetadata WithDimensions(ImageMetadata source, int width, int height) => new()
    {
        OrientationRotation = source.OrientationRotation,
        CameraMake = source.CameraMake,
        CameraModel = source.CameraModel,
        Lens = source.Lens,
        ExposureTimeSeconds = source.ExposureTimeSeconds,
        FNumber = source.FNumber,
        Iso = source.Iso,
        TakenAt = source.TakenAt,
        Width = width,
        Height = height,
        FileSizeBytes = source.FileSizeBytes,
        FileCreatedAt = source.FileCreatedAt,
        FileModifiedAt = source.FileModifiedAt,
        FileAccessedAt = source.FileAccessedAt
    };
}
