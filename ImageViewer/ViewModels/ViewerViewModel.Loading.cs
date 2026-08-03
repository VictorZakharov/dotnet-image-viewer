using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel
{
    private readonly Func<string, CancellationToken, Task<LoadedImage>> _imageLoader;
    private List<MediaScanEntry> _folderMedia = new();
    private string? _currentFolder;
    private int _currentIndex = -1;
    private CancellationTokenSource? _loadCts;

    public ViewerViewModel(AppSettings settings)
        : this(settings, ImageLoader.LoadAsync)
    {
    }

    internal ViewerViewModel(
        AppSettings settings,
        Func<string, CancellationToken, Task<LoadedImage>> imageLoader)
    {
        Settings = settings;
        _imageLoader = imageLoader;
        ShowExifOverlay = settings.ShowExifOverlay;
    }

    public async Task LoadAsync(string path)
    {
        var loadCts = new CancellationTokenSource();
        var previousLoad = _loadCts;
        _loadCts = loadCts;
        previousLoad?.Cancel();
        var ct = loadCts.Token;

        FilePath = path;
        PlaybackError = null;
        Metadata = null;
        Rotation = 0;

        var isVideo = MediaFileTypes.IsVideo(path);
        IsVideo = isVideo;
        IsImageLoading = !isVideo;
        Bitmap = null;

        if (isVideo)
            StopSlideshow();
        else
            StopVideo();

        try
        {
            var folder = Path.GetDirectoryName(path);
            if (folder is not null &&
                (!string.Equals(folder, _currentFolder, StringComparison.OrdinalIgnoreCase)
                 || _folderMedia.Count == 0))
            {
                var folderMedia = await FolderScanner.ScanAsync(folder, ct);
                if (!IsActiveLoad(loadCts)) return;

                _currentFolder = folder;
                _folderMedia = folderMedia;
            }

            if (!IsActiveLoad(loadCts)) return;
            _currentIndex = _folderMedia.FindIndex(entry =>
                string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
            UpdateStatus();

            var metadata = await Task.Run(() => ExifReader.Read(path), ct);
            if (!IsActiveLoad(loadCts)) return;
            Metadata = metadata;

            if (isVideo)
            {
                StartVideo(path);
            }
            else
            {
                var loaded = await _imageLoader(path, ct);
                if (!IsActiveLoad(loadCts))
                {
                    loaded.Bitmap.Dispose();
                    return;
                }

                Bitmap = loaded.Bitmap;
                Rotation = loaded.OrientationBaked ? 0 : (Metadata?.OrientationRotation ?? 0);
            }

            UpdateStatus();
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer load supersedes this one.
        }
        catch (Exception ex)
        {
            if (IsActiveLoad(loadCts) && isVideo)
                PlaybackError = $"Could not play this video: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_loadCts, loadCts))
            {
                _loadCts = null;
                if (!_disposed && !isVideo)
                    IsImageLoading = false;
            }

            loadCts.Dispose();
        }
    }

    private bool IsActiveLoad(CancellationTokenSource loadCts) =>
        !_disposed &&
        !loadCts.IsCancellationRequested &&
        ReferenceEquals(_loadCts, loadCts);
}
