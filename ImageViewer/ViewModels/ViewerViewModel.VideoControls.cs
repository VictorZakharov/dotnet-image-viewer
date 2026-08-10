using System;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel
{
    private static readonly TimeSpan VideoControlsIdleDelay = TimeSpan.FromSeconds(3);

    [ObservableProperty] private bool _areVideoControlsVisible = true;

    private DispatcherTimer? _videoControlsIdleTimer;

    public void NotifyVideoControlActivity()
    {
        if (_disposed || !IsVideo) return;

        AreVideoControlsVisible = true;
        if (IsFullscreen)
            RestartVideoControlsIdleTimer();
        else
            StopVideoControlsIdleTimer();
    }

    partial void OnIsFullscreenChanged(bool value)
    {
        AreVideoControlsVisible = true;
        if (value && IsVideo)
            RestartVideoControlsIdleTimer();
        else
            StopVideoControlsIdleTimer();
    }

    partial void OnIsVideoChanged(bool value)
    {
        ResetVideoControls();
    }

    private void RestartVideoControlsIdleTimer()
    {
        if (_videoControlsIdleTimer is null)
        {
            _videoControlsIdleTimer = new DispatcherTimer
            {
                Interval = VideoControlsIdleDelay
            };
            _videoControlsIdleTimer.Tick += OnVideoControlsIdleTimerTick;
        }

        _videoControlsIdleTimer.Stop();
        _videoControlsIdleTimer.Start();
    }

    private void OnVideoControlsIdleTimerTick(object? sender, EventArgs e)
    {
        StopVideoControlsIdleTimer();
        if (_disposed || !IsVideo || !IsFullscreen)
        {
            AreVideoControlsVisible = true;
            return;
        }

        HideTimelinePreview();
        AreVideoControlsVisible = false;
    }

    private void ResetVideoControls()
    {
        StopVideoControlsIdleTimer();
        AreVideoControlsVisible = true;
    }

    private void StopVideoControlsIdleTimer()
    {
        _videoControlsIdleTimer?.Stop();
    }

    private void DisposeVideoControls()
    {
        if (_videoControlsIdleTimer is null) return;
        _videoControlsIdleTimer.Stop();
        _videoControlsIdleTimer.Tick -= OnVideoControlsIdleTimerTick;
        _videoControlsIdleTimer = null;
    }
}
