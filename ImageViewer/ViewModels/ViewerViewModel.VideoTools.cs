using System;
using System.IO;
using Avalonia.Threading;
using ImageViewer.Services;
using LibVLCSharp.Shared;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel
{
    private const int VideoTrackRefreshAttempts = 6;
    private DispatcherTimer? _videoTrackRefreshTimer;
    private int _remainingVideoTrackRefreshes;

    public VideoPlaybackTools VideoTools { get; } = new();
    public bool ShowVideoTools => IsVideo && !IsFullscreen;

    private void PrepareVideoTools(MediaPlayer player, Media media)
    {
        StopVideoTrackRefreshTimer();
        VideoTools.Attach(new LibVlcVideoTrackBackend(player, media));
    }

    private void ActivateVideoTools()
    {
        VideoTools.Activate();
        StopVideoTrackRefreshTimer();
        _remainingVideoTrackRefreshes = VideoTrackRefreshAttempts;
        _videoTrackRefreshTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _videoTrackRefreshTimer.Tick += OnVideoTrackRefreshTick;
        _videoTrackRefreshTimer.Start();
    }

    private void RefreshVideoTools()
    {
        if (VideoTools.IsReady)
            VideoTools.Refresh();
    }

    private void ResetVideoTools()
    {
        StopVideoTrackRefreshTimer();
        VideoTools.Detach();
    }

    private void OnVideoTrackRefreshTick(object? sender, EventArgs e)
    {
        if (_disposed || !IsVideo || !VideoTools.IsReady)
        {
            StopVideoTrackRefreshTimer();
            return;
        }

        VideoTools.Refresh();
        _remainingVideoTrackRefreshes--;
        if (_remainingVideoTrackRefreshes <= 0)
            StopVideoTrackRefreshTimer();
    }

    private void StopVideoTrackRefreshTimer()
    {
        if (_videoTrackRefreshTimer is null) return;
        _videoTrackRefreshTimer.Stop();
        _videoTrackRefreshTimer.Tick -= OnVideoTrackRefreshTick;
        _videoTrackRefreshTimer = null;
    }

    private void OnPlayerElementaryStreamChanged(object? sender, EventArgs e) =>
        PostVideoEventToUi(RefreshVideoTools);

    public bool LoadExternalSubtitle(string path)
    {
        var loaded = VideoTools.LoadExternalSubtitle(path);
        StatusText = loaded
            ? $"Loaded subtitles: {Path.GetFileName(path)}"
            : $"Could not load subtitles: {Path.GetFileName(path)}";
        return loaded;
    }
}
