using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel
{
    private readonly Func<string, CancellationToken, Task<LoadedImage>> _imageLoader;
    private readonly Func<string, CancellationToken, Task<List<MediaScanEntry>>> _folderScanner;
    private readonly Func<string, CancellationToken, Task<ImageMetadata>> _metadataLoader;
    private List<MediaScanEntry> _folderMedia = new();
    private string? _currentFolder;
    private int _currentIndex = -1;
    private CancellationTokenSource? _loadCts;
    private Task _navigationReady = Task.CompletedTask;

    public ViewerViewModel(AppSettings settings)
        : this(settings, ImageLoader.LoadAsync)
    {
    }

    internal ViewerViewModel(
        AppSettings settings,
        Func<string, CancellationToken, Task<LoadedImage>> imageLoader,
        Func<string, CancellationToken, Task<List<MediaScanEntry>>>? folderScanner = null,
        Func<string, CancellationToken, Task<ImageMetadata>>? metadataLoader = null)
    {
        Settings = settings;
        _imageLoader = imageLoader;
        _folderScanner = folderScanner ?? FolderScanner.ScanAsync;
        _metadataLoader = metadataLoader
                          ?? ((path, ct) => Task.Run(() => ExifReader.Read(path), ct));
        ShowExifOverlay = settings.ShowExifOverlay;
    }

    public async Task ReloadFolderAndImageAsync(string path)
    {
        InvalidateFolderMedia();
        await LoadAsync(path);
    }

    public void InvalidateFolderMedia()
    {
        _currentFolder = null;
        _folderMedia.Clear();
        _currentIndex = -1;
    }

    public async Task LoadAsync(string path)
    {
        ResetEditSession();
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
        ReplaceBitmap(null);

        if (isVideo)
            StopSlideshow();
        else
            StopVideo();

        Task<LoadedImage>? imageTask = null;
        TaskCompletionSource? navigationReady = null;
        var imageWasAdopted = false;
        try
        {
            var folder = Path.GetDirectoryName(path);
            var hasFolderNavigation = folder is not null
                                      && FileSystemPath.Equals(folder, _currentFolder)
                                      && _folderMedia.Count > 0;
            if (!hasFolderNavigation)
            {
                _currentFolder = null;
                _folderMedia.Clear();
                _currentIndex = -1;
                if (folder is not null)
                {
                    navigationReady = new TaskCompletionSource(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    _navigationReady = navigationReady.Task;
                }
                else
                {
                    _navigationReady = Task.CompletedTask;
                }
            }
            else
            {
                _navigationReady = Task.CompletedTask;
                _currentIndex = _folderMedia.FindIndex(entry =>
                    FileSystemPath.Equals(entry.Path, path));
            }

            if (isVideo)
            {
                var metadata = await _metadataLoader(path, ct);
                if (!IsActiveLoad(loadCts)) return;
                Metadata = metadata;
                StartVideo(path);
            }
            else
            {
                var metadataTask = _metadataLoader(path, ct);
                imageTask = _imageLoader(path, ct);
                await Task.WhenAll(metadataTask, imageTask);
                if (!IsActiveLoad(loadCts))
                    return;

                Metadata = metadataTask.Result;
                var loaded = imageTask.Result;
                ReplaceBitmap(loaded.Bitmap);
                imageWasAdopted = true;
                Rotation = loaded.OrientationBaked ? 0 : (Metadata?.OrientationRotation ?? 0);
                IsImageLoading = false;
            }

            UpdateStatus();

            // Folder navigation is useful for Previous/Next, but it must never
            // delay the image explicitly requested by the user. Populate it
            // only after that image is visible.
            if (!hasFolderNavigation && folder is not null)
            {
                var folderMedia = await _folderScanner(folder, ct);
                if (!IsActiveLoad(loadCts)) return;

                _currentFolder = folder;
                _folderMedia = folderMedia;
                _currentIndex = _folderMedia.FindIndex(entry =>
                    FileSystemPath.Equals(entry.Path, path));
                UpdateStatus();
            }
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
            if (!imageWasAdopted && imageTask?.Status == TaskStatus.RanToCompletion)
                imageTask.Result.Bitmap.Dispose();

            if (ReferenceEquals(_loadCts, loadCts))
            {
                _loadCts = null;
                if (!_disposed && !isVideo)
                    IsImageLoading = false;
            }

            navigationReady?.TrySetResult();
            loadCts.Dispose();
        }
    }

    private bool IsActiveLoad(CancellationTokenSource loadCts) =>
        !_disposed &&
        !loadCts.IsCancellationRequested &&
        ReferenceEquals(_loadCts, loadCts);
}
