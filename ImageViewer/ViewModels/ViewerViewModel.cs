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
using LibVLCSharp.Shared;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel : ObservableObject, IDisposable
{
    public AppSettings Settings { get; }

    [ObservableProperty] private Bitmap? _bitmap;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FileNameDisplay), nameof(FolderDisplay))]
    private string? _filePath;

    [ObservableProperty] private int _rotation;
    [ObservableProperty] private bool _isFullscreen;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowImageInfoOverlay))]
    private bool _showExifOverlay;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImage), nameof(InfoPanelLabel), nameof(ShowImageInfoOverlay))]
    private bool _isVideo;

    [ObservableProperty] private MediaPlayer? _videoPlayer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayPauseLabel))]
    private bool _isPlaying;

    [ObservableProperty] private bool _isVideoLoading;
    [ObservableProperty] private string? _playbackError;
    [ObservableProperty] private double _playbackPosition;
    [ObservableProperty] private string _currentTimeLabel = "00:00";
    [ObservableProperty] private string _durationLabel = "00:00";
    [ObservableProperty] private double _volume = 100;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MuteLabel))]
    private bool _isMuted;

    public bool IsImage => !IsVideo;
    public bool ShowImageInfoOverlay => IsImage && ShowExifOverlay;
    public string PlayPauseLabel => IsPlaying ? "Pause" : "Play";
    public string MuteLabel => IsMuted ? "Unmute" : "Mute";
    public string InfoPanelLabel => IsVideo ? "INFO" : "EXIF";
    public string? FileNameDisplay => FilePath is { } p ? Path.GetFileName(p) : null;
    public string? FolderDisplay => FilePath is { } p ? Path.GetDirectoryName(p) : null;

    partial void OnShowExifOverlayChanged(bool value)
    {
        Settings.ShowExifOverlay = value;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SlideshowStatus))]
    private bool _isSlideshowRunning;

    [ObservableProperty] private string? _statusText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasAnyExifData))]
    private ImageMetadata? _metadata;

    public bool HasAnyExifData => Metadata?.HasAnyExif == true;
    public string? SlideshowStatus => IsSlideshowRunning ? "Slideshow" : null;

    private List<MediaScanEntry> _folderMedia = new();
    private string? _currentFolder;
    private int _currentIndex = -1;
    private CancellationTokenSource? _loadCts;
    private DispatcherTimer? _slideshowTimer;
    private LibVLC? _libVlc;
    private Media? _currentMedia;
    private bool _updatingPlaybackPosition;
    private bool _disposed;

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
        PlaybackError = null;
        Metadata = null;
        Rotation = 0;

        var isVideo = MediaFileTypes.IsVideo(path);
        IsVideo = isVideo;
        if (isVideo)
        {
            StopSlideshow();
            Bitmap = null;
        }
        else
        {
            StopVideo();
        }

        try
        {
            var folder = Path.GetDirectoryName(path);
            if (folder is not null &&
                (!string.Equals(folder, _currentFolder, StringComparison.OrdinalIgnoreCase)
                 || _folderMedia.Count == 0))
            {
                _currentFolder = folder;
                _folderMedia = await FolderScanner.ScanAsync(folder, ct);
            }

            _currentIndex = _folderMedia.FindIndex(entry =>
                string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase));
            UpdateStatus();

            Metadata = await Task.Run(() => ExifReader.Read(path), ct);
            if (ct.IsCancellationRequested) return;

            if (isVideo)
            {
                StartVideo(path);
            }
            else
            {
                var loaded = await ImageLoader.LoadAsync(path, ct);
                if (ct.IsCancellationRequested) return;

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
            if (isVideo)
                PlaybackError = $"Could not play this video: {ex.Message}";
        }
    }

    private void EnsureVideoPlayer()
    {
        if (VideoPlayer is not null) return;

        // Deliberately delayed until the first video opens so native VLC load
        // and plug-in discovery never affect application startup.
        Core.Initialize();
        _libVlc = new LibVLC("--no-video-title-show", "--quiet");
        var player = new MediaPlayer(_libVlc);
        player.Playing += OnPlayerPlaying;
        player.Paused += OnPlayerPaused;
        player.Stopped += OnPlayerStopped;
        player.EndReached += OnPlayerEndReached;
        player.TimeChanged += OnPlayerTimeChanged;
        player.LengthChanged += OnPlayerLengthChanged;
        player.EncounteredError += OnPlayerEncounteredError;
        player.Volume = (int)Math.Round(Volume);
        player.Mute = IsMuted;
        VideoPlayer = player;
    }

    private void StartVideo(string path)
    {
        try
        {
            EnsureVideoPlayer();
            if (VideoPlayer is null || _libVlc is null) return;

            IsVideoLoading = true;
            IsPlaying = false;
            SetPlaybackPositionFromPlayer(0);
            CurrentTimeLabel = "00:00";
            DurationLabel = "00:00";

            var previousMedia = _currentMedia;
            // Browser mode pauses playback. Stop that media before replacing it
            // so VLC creates a fresh video output instead of reusing a black one.
            if (previousMedia is not null)
                VideoPlayer.Stop();
            _currentMedia = new Media(_libVlc, new Uri(path));
            if (!VideoPlayer.Play(_currentMedia))
                PlaybackError = "Could not start video playback.";
            previousMedia?.Dispose();
        }
        catch
        {
            IsVideoLoading = false;
            throw;
        }
    }

    [RelayCommand]
    private async Task Next()
    {
        if (_folderMedia.Count == 0) return;
        _currentIndex = (_currentIndex + 1) % _folderMedia.Count;
        await LoadAsync(_folderMedia[_currentIndex].Path);
    }

    [RelayCommand]
    private async Task Previous()
    {
        if (_folderMedia.Count == 0) return;
        _currentIndex = (_currentIndex - 1 + _folderMedia.Count) % _folderMedia.Count;
        await LoadAsync(_folderMedia[_currentIndex].Path);
    }

    [RelayCommand]
    private void RotateRight()
    {
        if (!IsVideo) Rotation = (Rotation + 90) % 360;
    }

    [RelayCommand]
    private void ToggleFullscreen()
    {
        IsFullscreen = !IsFullscreen;
    }

    [RelayCommand]
    private void ToggleExifOverlay() => ShowExifOverlay = !ShowExifOverlay;

    [RelayCommand]
    private void ToggleSlideshow()
    {
        if (IsVideo) return;
        if (IsSlideshowRunning) StopSlideshow();
        else StartSlideshow();
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        if (!IsVideo || VideoPlayer is null) return;
        if (VideoPlayer.IsPlaying) VideoPlayer.Pause();
        else VideoPlayer.Play();
    }

    [RelayCommand]
    private void ToggleMute()
    {
        IsMuted = !IsMuted;
    }

    partial void OnPlaybackPositionChanged(double value)
    {
        if (_updatingPlaybackPosition || !IsVideo || VideoPlayer is null) return;
        VideoPlayer.Position = (float)Math.Clamp(value, 0, 1);
    }

    partial void OnVolumeChanged(double value)
    {
        if (VideoPlayer is not null)
            VideoPlayer.Volume = (int)Math.Round(Math.Clamp(value, 0, 100));
    }

    partial void OnIsMutedChanged(bool value)
    {
        if (VideoPlayer is not null) VideoPlayer.Mute = value;
    }

    public void StopSlideshow()
    {
        _slideshowTimer?.Stop();
        _slideshowTimer = null;
        IsSlideshowRunning = false;
    }

    public void Deactivate()
    {
        StopSlideshow();
        if (IsVideo && VideoPlayer?.IsPlaying == true)
            VideoPlayer.Pause();
    }

    private void StartSlideshow()
    {
        if (IsVideo) return;
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

    private void StopVideo()
    {
        if (VideoPlayer is null) return;
        try
        {
            if (VideoPlayer.IsPlaying) VideoPlayer.Stop();
        }
        catch { /* best effort while changing media */ }
        IsPlaying = false;
        IsVideoLoading = false;
    }

    private void OnPlayerPlaying(object? sender, EventArgs e) => PostToUi(() =>
    {
        IsPlaying = true;
        IsVideoLoading = false;
        PlaybackError = null;
    });

    private void OnPlayerPaused(object? sender, EventArgs e) => PostToUi(() => IsPlaying = false);

    private void OnPlayerStopped(object? sender, EventArgs e) => PostToUi(() =>
    {
        IsPlaying = false;
        IsVideoLoading = false;
    });

    private void OnPlayerEndReached(object? sender, EventArgs e) => PostToUi(() =>
    {
        IsPlaying = false;
        IsVideoLoading = false;
        SetPlaybackPositionFromPlayer(1);
    });

    private void OnPlayerTimeChanged(object? sender, MediaPlayerTimeChangedEventArgs e) => PostToUi(() =>
    {
        var length = VideoPlayer?.Length ?? 0;
        CurrentTimeLabel = FormatDuration(e.Time);
        if (length > 0)
            SetPlaybackPositionFromPlayer((double)e.Time / length);
    });

    private void OnPlayerLengthChanged(object? sender, MediaPlayerLengthChangedEventArgs e) => PostToUi(() =>
    {
        DurationLabel = FormatDuration(e.Length);
        UpdateStatus();
    });

    private void OnPlayerEncounteredError(object? sender, EventArgs e) => PostToUi(() =>
    {
        IsPlaying = false;
        IsVideoLoading = false;
        PlaybackError = "This video could not be decoded or played.";
    });

    private void PostToUi(Action action)
    {
        if (_disposed) return;
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed) action();
        });
    }

    private void SetPlaybackPositionFromPlayer(double value)
    {
        _updatingPlaybackPosition = true;
        try { PlaybackPosition = Math.Clamp(value, 0, 1); }
        finally { _updatingPlaybackPosition = false; }
    }

    private static string FormatDuration(long milliseconds)
    {
        if (milliseconds < 0) milliseconds = 0;
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        return duration.TotalHours >= 1
            ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
            : $"{duration.Minutes:00}:{duration.Seconds:00}";
    }

    private void UpdateStatus()
    {
        var name = FilePath is null ? null : Path.GetFileName(FilePath);
        if (_folderMedia.Count > 0 && _currentIndex >= 0)
        {
            var detail = IsVideo
                ? (DurationLabel == "00:00" ? "Video" : DurationLabel)
                : Metadata?.DimensionsSummary;
            var suffix = string.IsNullOrEmpty(detail) ? "" : $"  —  {detail}";
            StatusText = $"{name}  —  {_currentIndex + 1}/{_folderMedia.Count}{suffix}";
        }
        else
        {
            StatusText = name;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        StopSlideshow();

        if (VideoPlayer is not null)
        {
            var player = VideoPlayer;
            player.Playing -= OnPlayerPlaying;
            player.Paused -= OnPlayerPaused;
            player.Stopped -= OnPlayerStopped;
            player.EndReached -= OnPlayerEndReached;
            player.TimeChanged -= OnPlayerTimeChanged;
            player.LengthChanged -= OnPlayerLengthChanged;
            player.EncounteredError -= OnPlayerEncounteredError;

            // Notify the view first: VideoView detaches its HWND by calling the
            // still-live player. Doing this after Dispose can crash in native
            // code during application shutdown.
            VideoPlayer = null;
            try { player.Stop(); } catch { /* shutting down */ }
            player.Dispose();
        }

        _currentMedia?.Dispose();
        _currentMedia = null;
        _libVlc?.Dispose();
        _libVlc = null;
    }
}
