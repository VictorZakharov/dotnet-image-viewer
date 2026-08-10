using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using LibVLCSharp.Shared;

namespace ImageViewer.ViewModels;

public partial class ViewerViewModel
{
    private const string ScrubSnapshotPrefix = "ImageViewer-scrub-";
    private static readonly TimeSpan ScrubSnapshotInterval = TimeSpan.FromMilliseconds(40);
    private static readonly TimeSpan ScrubFrameSettleDelay = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ScrubSnapshotTimeout = TimeSpan.FromSeconds(2);

    [ObservableProperty] private Bitmap? _scrubPreviewBitmap;
    [ObservableProperty] private bool _isScrubPreviewVisible;
    [ObservableProperty] private string _scrubPreviewTimeLabel = "00:00";

    private DispatcherTimer? _scrubPreviewTimer;
    private string? _pendingScrubSnapshotPath;
    private DateTime _pendingScrubSnapshotStartedAt;
    private double _requestedScrubPosition = -1;
    private double _preparedScrubPosition;
    private double _pendingScrubPosition;
    private double _capturedScrubPosition = -1;
    private bool _isScrubFramePreparing;
    private DateTime _scrubFrameReadyAt;
    private int _scrubPreviewVersion;
    private int _pendingScrubPreviewVersion;
    private int _pendingScrubVideoSessionVersion;
    private long _scrubSnapshotSequence;
    private long _pendingScrubSnapshotSequence;
    private long _displayedScrubSnapshotSequence;

    public void BeginScrubPreview(double position)
    {
        if (!IsVideo || VideoPlayer is null) return;

        _scrubPreviewVersion++;
        _displayedScrubSnapshotSequence = 0;
        _capturedScrubPosition = -1;
        ReplaceScrubPreviewBitmap(null);
        IsScrubPreviewVisible = true;
        UpdateScrubPreview(position);
        EnsureScrubPreviewTimer();
    }

    public void UpdateScrubPreview(double position)
    {
        if (!IsScrubPreviewVisible) return;

        _requestedScrubPosition = Math.Clamp(position, 0, 1);
        var length = Math.Max(0, VideoPlayer?.Length ?? 0);
        ScrubPreviewTimeLabel = FormatDuration((long)Math.Round(length * _requestedScrubPosition));
    }

    public void EndScrubPreview()
    {
        var finalPosition = _requestedScrubPosition;
        ResetScrubPreview();
        if (finalPosition >= 0 && VideoPlayer is { } player)
            SeekAndRenderScrubFrame(player, finalPosition);
    }

    private void EnsureScrubPreviewTimer()
    {
        if (_scrubPreviewTimer is null)
        {
            _scrubPreviewTimer = new DispatcherTimer { Interval = ScrubSnapshotInterval };
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

        if (_pendingScrubSnapshotPath is not null)
        {
            if (DateTime.UtcNow - _pendingScrubSnapshotStartedAt < ScrubSnapshotTimeout)
                return;

            var timedOutPath = _pendingScrubSnapshotPath;
            _pendingScrubSnapshotPath = null;
            TryDeleteScrubSnapshot(timedOutPath);
        }

        if (_isScrubFramePreparing)
        {
            if (DateTime.UtcNow < _scrubFrameReadyAt) return;

            _isScrubFramePreparing = false;
            RequestScrubSnapshot(_preparedScrubPosition);
            return;
        }

        if (ScrubPreviewBitmap is not null &&
            Math.Abs(_requestedScrubPosition - _capturedScrubPosition) < 0.0005)
        {
            return;
        }

        var player = VideoPlayer;
        if (player is null || _requestedScrubPosition < 0) return;

        _preparedScrubPosition = _requestedScrubPosition;
        if (!SeekAndRenderScrubFrame(player, _preparedScrubPosition)) return;
        _scrubFrameReadyAt = DateTime.UtcNow + ScrubFrameSettleDelay;
        _isScrubFramePreparing = true;
    }

    private void RequestScrubSnapshot(double position)
    {
        var player = VideoPlayer;
        if (!IsScrubPreviewVisible || player is null || _requestedScrubPosition < 0 ||
            _pendingScrubSnapshotPath is not null)
        {
            return;
        }

        var snapshotPath = Path.Combine(
            Path.GetTempPath(),
            $"{ScrubSnapshotPrefix}{Guid.NewGuid():N}.png");
        _pendingScrubSnapshotPath = snapshotPath;
        _pendingScrubSnapshotStartedAt = DateTime.UtcNow;
        _pendingScrubPosition = position;
        _pendingScrubPreviewVersion = _scrubPreviewVersion;
        _pendingScrubVideoSessionVersion = _videoSessionVersion;
        _pendingScrubSnapshotSequence = ++_scrubSnapshotSequence;

        try
        {
            // A zero height preserves the source aspect ratio.
            if (player.TakeSnapshot(0, snapshotPath, 240, 0)) return;
        }
        catch
        {
            // The vout may be between frames while the user is seeking.
        }

        _pendingScrubSnapshotPath = null;
        TryDeleteScrubSnapshot(snapshotPath);
    }

    private void OnPlayerSnapshotTaken(object? sender, MediaPlayerSnapshotTakenEventArgs e)
    {
        var snapshotPath = e.Filename;
        PostToUi(() => HandleScrubSnapshot(snapshotPath));
    }

    private void HandleScrubSnapshot(string snapshotPath)
    {
        if (_pendingScrubSnapshotPath is null ||
            !PathsEqual(snapshotPath, _pendingScrubSnapshotPath))
        {
            TryDeleteScrubSnapshot(snapshotPath);
            return;
        }

        var previewVersion = _pendingScrubPreviewVersion;
        var videoSessionVersion = _pendingScrubVideoSessionVersion;
        var position = _pendingScrubPosition;
        var sequence = _pendingScrubSnapshotSequence;
        _pendingScrubSnapshotPath = null;
        _ = LoadScrubSnapshotAsync(
            snapshotPath,
            previewVersion,
            videoSessionVersion,
            position,
            sequence);
    }

    private async Task LoadScrubSnapshotAsync(
        string snapshotPath,
        int previewVersion,
        int videoSessionVersion,
        double position,
        long sequence)
    {
        byte[]? bytes = null;
        try
        {
            bytes = await File.ReadAllBytesAsync(snapshotPath);
        }
        catch (IOException)
        {
            // A failed preview should never interrupt playback.
        }
        catch (UnauthorizedAccessException)
        {
            // A failed preview should never interrupt playback.
        }
        finally
        {
            TryDeleteScrubSnapshot(snapshotPath);
        }

        if (bytes is null || bytes.Length == 0) return;
        PostToUi(() =>
        {
            if (!IsScrubPreviewVisible || previewVersion != _scrubPreviewVersion ||
                videoSessionVersion != _videoSessionVersion ||
                sequence < _displayedScrubSnapshotSequence)
            {
                return;
            }

            try
            {
                using var stream = new MemoryStream(bytes, writable: false);
                ReplaceScrubPreviewBitmap(new Bitmap(stream));
                _displayedScrubSnapshotSequence = sequence;
                _capturedScrubPosition = position;
            }
            catch
            {
                // Ignore malformed or incomplete native snapshot output.
            }
        });
    }

    private void ResetScrubPreview()
    {
        _scrubPreviewVersion++;
        IsScrubPreviewVisible = false;
        _requestedScrubPosition = -1;
        _capturedScrubPosition = -1;
        _isScrubFramePreparing = false;
        _scrubPreviewTimer?.Stop();

        var pendingPath = _pendingScrubSnapshotPath;
        _pendingScrubSnapshotPath = null;
        TryDeleteScrubSnapshot(pendingPath);
        ReplaceScrubPreviewBitmap(null);
    }

    private static bool SeekAndRenderScrubFrame(MediaPlayer player, double position)
    {
        try
        {
            player.Position = (float)Math.Clamp(position, 0, 1);
            // Paused VLC outputs can otherwise keep returning the last frame.
            if (!player.IsPlaying)
                player.NextFrame();
            return true;
        }
        catch
        {
            // A preview failure must never interrupt normal seeking.
            return false;
        }
    }

    private void ReplaceScrubPreviewBitmap(Bitmap? replacement)
    {
        var previous = ScrubPreviewBitmap;
        ScrubPreviewBitmap = replacement;
        previous?.Dispose();
    }

    private static bool PathsEqual(string first, string second)
    {
        try
        {
            return string.Equals(
                Path.GetFullPath(first),
                Path.GetFullPath(second),
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private static void TryDeleteScrubSnapshot(string? snapshotPath)
    {
        if (string.IsNullOrEmpty(snapshotPath) ||
            !Path.GetFileName(snapshotPath).StartsWith(ScrubSnapshotPrefix, StringComparison.Ordinal))
        {
            return;
        }

        try
        {
            var fullPath = Path.GetFullPath(snapshotPath);
            var tempPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath()));
            if (!string.Equals(
                    Path.GetDirectoryName(fullPath),
                    tempPath,
                    OperatingSystem.IsWindows()
                        ? StringComparison.OrdinalIgnoreCase
                        : StringComparison.Ordinal))
            {
                return;
            }

            File.Delete(fullPath);
        }
        catch
        {
            // Best-effort cleanup of an app-owned temporary thumbnail.
        }
    }
}
