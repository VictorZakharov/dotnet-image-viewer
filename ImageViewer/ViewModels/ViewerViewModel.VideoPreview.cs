using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel
{
    private static readonly TimeSpan ScrubPreviewInterval = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan ScrubPreviewRequestTimeout = TimeSpan.FromSeconds(2);

    [ObservableProperty] private Bitmap? _scrubPreviewBitmap;
    [ObservableProperty] private bool _isScrubPreviewVisible;
    [ObservableProperty] private string _scrubPreviewTimeLabel = "00:00";

    private DispatcherTimer? _scrubPreviewTimer;
    private VideoFramePreviewDecoder? _scrubPreviewDecoder;
    private string? _scrubPreviewDecoderPath;
    private double _requestedScrubPreviewPosition = -1;
    private double _displayedScrubPreviewPosition = -1;
    private DateTime _scrubPreviewRequestStartedAt;
    private int _scrubPreviewSessionVersion;

    public void ShowTimelinePreview(double position)
    {
        if (!IsVideo || VideoPlayer is null || _libVlc is null || string.IsNullOrEmpty(FilePath))
            return;

        IsScrubPreviewVisible = true;
        UpdateTimelinePreview(position);
        EnsureScrubPreviewTimer();
    }

    public void UpdateTimelinePreview(double position)
    {
        if (!IsScrubPreviewVisible) return;

        _requestedScrubPreviewPosition = Math.Clamp(position, 0, 1);
        var length = Math.Max(0, VideoPlayer?.Length ?? 0);
        ScrubPreviewTimeLabel = FormatDuration((long)Math.Round(
            length * _requestedScrubPreviewPosition));
    }

    public void HideTimelinePreview()
    {
        _scrubPreviewSessionVersion++;
        IsScrubPreviewVisible = false;
        _requestedScrubPreviewPosition = -1;
        _displayedScrubPreviewPosition = -1;
        _scrubPreviewRequestStartedAt = default;
        _scrubPreviewTimer?.Stop();
        _scrubPreviewDecoder?.Cancel();
        ReplaceScrubPreviewBitmap(null);
    }

    private void EnsureScrubPreviewTimer()
    {
        if (_scrubPreviewTimer is null)
        {
            _scrubPreviewTimer = new DispatcherTimer { Interval = ScrubPreviewInterval };
            _scrubPreviewTimer.Tick += OnScrubPreviewTimerTick;
        }

        _scrubPreviewTimer.Start();
    }

    private void OnScrubPreviewTimerTick(object? sender, EventArgs e)
    {
        if (!IsScrubPreviewVisible)
        {
            _scrubPreviewTimer?.Stop();
            return;
        }

        if (!EnsureScrubPreviewDecoder() || _scrubPreviewDecoder is not { } decoder)
            return;

        if (decoder.IsBusy)
        {
            if (_scrubPreviewRequestStartedAt != default &&
                DateTime.UtcNow - _scrubPreviewRequestStartedAt >= ScrubPreviewRequestTimeout)
            {
                decoder.Cancel();
            }
            return;
        }

        if (ScrubPreviewBitmap is not null &&
            Math.Abs(_requestedScrubPreviewPosition - _displayedScrubPreviewPosition) < 0.0005)
        {
            return;
        }

        if (decoder.RequestFrame(_requestedScrubPreviewPosition))
            _scrubPreviewRequestStartedAt = DateTime.UtcNow;
    }

    private bool EnsureScrubPreviewDecoder()
    {
        if (_scrubPreviewDecoder is not null &&
            FilePath is not null &&
            FileSystemPath.Equals(_scrubPreviewDecoderPath, FilePath))
        {
            return true;
        }

        DisposeScrubPreviewDecoder();
        if (_libVlc is null || string.IsNullOrEmpty(FilePath)) return false;

        try
        {
            var (width, height) = GetScrubPreviewDecoderSize();
            var decoder = new VideoFramePreviewDecoder(_libVlc, FilePath, width, height);
            decoder.FrameReady += OnScrubPreviewFrameReady;
            _scrubPreviewDecoder = decoder;
            _scrubPreviewDecoderPath = FilePath;
            return true;
        }
        catch
        {
            DisposeScrubPreviewDecoder();
            return false;
        }
    }

    private (uint Width, uint Height) GetScrubPreviewDecoderSize()
    {
        const uint maxWidth = 320;
        const uint maxHeight = 180;
        uint sourceWidth = 0;
        uint sourceHeight = 0;
        if (VideoPlayer?.Size(0, ref sourceWidth, ref sourceHeight) != true ||
            sourceWidth == 0 || sourceHeight == 0)
        {
            return (maxWidth, maxHeight);
        }

        var scale = Math.Min((double)maxWidth / sourceWidth, (double)maxHeight / sourceHeight);
        var width = (uint)Math.Max(2, Math.Round(sourceWidth * scale));
        var height = (uint)Math.Max(2, Math.Round(sourceHeight * scale));
        return (Math.Min(width, maxWidth), Math.Min(height, maxHeight));
    }

    private void OnScrubPreviewFrameReady(object? sender, VideoPreviewFrameReadyEventArgs e)
    {
        if (sender is not VideoFramePreviewDecoder decoder) return;
        var sessionVersion = _scrubPreviewSessionVersion;
        PostToUi(() =>
        {
            decoder.PauseAfterCapture(e.RequestId);
            if (!ReferenceEquals(decoder, _scrubPreviewDecoder) ||
                !IsScrubPreviewVisible || sessionVersion != _scrubPreviewSessionVersion)
            {
                return;
            }

            try
            {
                ReplaceScrubPreviewBitmap(CreateScrubPreviewBitmap(e));
                _displayedScrubPreviewPosition = e.Position;
            }
            catch
            {
                // A preview decode failure must never interrupt playback.
            }
        });
    }

    private static Bitmap CreateScrubPreviewBitmap(VideoPreviewFrameReadyEventArgs frame)
    {
        var bitmap = new WriteableBitmap(
            new PixelSize(frame.Width, frame.Height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Opaque);
        using var framebuffer = bitmap.Lock();
        var rows = Math.Min(frame.Height, framebuffer.Size.Height);
        var rowBytes = Math.Min(frame.Pitch, framebuffer.RowBytes);
        for (var row = 0; row < rows; row++)
        {
            Marshal.Copy(
                frame.Pixels,
                row * frame.Pitch,
                IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes),
                rowBytes);
        }

        return bitmap;
    }

    private void ResetScrubPreview()
    {
        HideTimelinePreview();
        DisposeScrubPreviewDecoder();
    }

    private void DisposeScrubPreviewDecoder()
    {
        if (_scrubPreviewDecoder is { } decoder)
        {
            decoder.FrameReady -= OnScrubPreviewFrameReady;
            decoder.Dispose();
        }

        _scrubPreviewDecoder = null;
        _scrubPreviewDecoderPath = null;
        _scrubPreviewRequestStartedAt = default;
    }

    private void ReplaceScrubPreviewBitmap(Bitmap? replacement)
    {
        var previous = ScrubPreviewBitmap;
        ScrubPreviewBitmap = replacement;
        previous?.Dispose();
    }
}
