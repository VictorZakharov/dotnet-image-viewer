using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel : ObservableObject
{
    public AppSettings Settings { get; }

    [ObservableProperty] private Bitmap? _bitmap;
    [ObservableProperty] private string? _filePath;
    [ObservableProperty] private int _rotation;
    [ObservableProperty] private bool _isFullscreen;
    [ObservableProperty] private bool _showExifOverlay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SlideshowStatus))]
    private bool _isSlideshowRunning;

    [ObservableProperty] private string? _statusText;
    [ObservableProperty] private ImageMetadata? _metadata;

    public string? SlideshowStatus => IsSlideshowRunning ? "▶ Slideshow" : null;

    private List<string> _folderImages = new();
    private string? _currentFolder;
    private int _currentIndex = -1;

    private CancellationTokenSource? _loadCts;
    private DispatcherTimer? _slideshowTimer;

    public ViewerViewModel(AppSettings settings)
    {
        Settings = settings;
        ShowExifOverlay = settings.ShowExifOverlay;
    }

    public async Task LoadAsync(string path)
    {
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        FilePath = path;

        try
        {
            var folder = Path.GetDirectoryName(path);
            if (folder is not null && (!string.Equals(folder, _currentFolder, StringComparison.OrdinalIgnoreCase) || _folderImages.Count == 0))
            {
                _currentFolder = folder;
                _folderImages = await FolderScanner.ScanAsync(folder, ct);
            }
            _currentIndex = _folderImages.IndexOf(path);
            UpdateStatus();

            Metadata = await Task.Run(() => ExifReader.Read(path), ct);

            var loaded = await ImageLoader.LoadAsync(path, ct);
            if (ct.IsCancellationRequested) return;

            Bitmap = loaded.Bitmap;
            Rotation = loaded.OrientationBaked ? 0 : (Metadata?.OrientationRotation ?? 0);
            UpdateStatus();
        }
        catch (OperationCanceledException)
        {
            // Expected when a newer load supersedes this one.
        }
        catch (Exception)
        {
            // TODO: surface a load error overlay (pass 4).
        }
    }

    [RelayCommand]
    private async Task Next()
    {
        if (_folderImages.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _folderImages.Count;
        await LoadAsync(_folderImages[_currentIndex]);
    }

    [RelayCommand]
    private async Task Previous()
    {
        if (_folderImages.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _folderImages.Count) % _folderImages.Count;
        await LoadAsync(_folderImages[_currentIndex]);
    }

    [RelayCommand]
    private void RotateRight()
    {
        Rotation = (Rotation + 90) % 360;
    }

    [RelayCommand]
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
    }

    [RelayCommand]
    private void ToggleExifOverlay()
    {
        ShowExifOverlay = !ShowExifOverlay;
        Settings.ShowExifOverlay = ShowExifOverlay;
    }

    [RelayCommand]
    private void ToggleSlideshow()
    {
        if (IsSlideshowRunning) StopSlideshow();
        else StartSlideshow();
    }

    public void StopSlideshow()
    {
        _slideshowTimer?.Stop();
        _slideshowTimer = null;
        IsSlideshowRunning = false;
    }

    private void StartSlideshow()
    {
        int delay = Math.Max(1, Settings.SlideshowDelaySeconds);
        _slideshowTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(delay) };
        _slideshowTimer.Tick += async (_, _) =>
        {
            try { await Next(); }
            catch { /* keep slideshow running */ }
        };
        _slideshowTimer.Start();
        IsSlideshowRunning = true;
    }

    private void UpdateStatus()
    {
        var name = FilePath is null ? null : Path.GetFileName(FilePath);
        if (_folderImages.Count > 0 && _currentIndex >= 0)
        {
            string dims = (Metadata?.DimensionsSummary is { } d) ? $"  —  {d}" : "";
            StatusText = $"{name}  —  {_currentIndex + 1}/{_folderImages.Count}{dims}";
        }
        else
        {
            StatusText = name;
        }
    }
}
