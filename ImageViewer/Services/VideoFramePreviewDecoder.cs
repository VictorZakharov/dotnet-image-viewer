using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using LibVLCSharp.Shared;

namespace ImageViewer.Services;

internal sealed class VideoPreviewFrameReadyEventArgs(
    long requestId,
    double position,
    int width,
    int height,
    int pitch,
    byte[] pixels) : EventArgs
{
    public long RequestId { get; } = requestId;
    public double Position { get; } = position;
    public int Width { get; } = width;
    public int Height { get; } = height;
    public int Pitch { get; } = pitch;
    public byte[] Pixels { get; } = pixels;
}

/// <summary>
/// Decodes small timeline-preview frames without moving the visible player.
/// </summary>
internal sealed class VideoFramePreviewDecoder : IDisposable
{
    private static readonly long FrameSettleTicks =
        (long)(Stopwatch.Frequency * TimeSpan.FromMilliseconds(100).TotalSeconds);
    private const long MaximumFrameTimeErrorMilliseconds = 500;

    private readonly Media _media;
    private readonly MediaPlayer _player;
    private readonly int _width;
    private readonly int _height;
    private readonly int _pitch;
    private readonly int _bufferLength;
    private readonly IntPtr _buffer;
    private readonly object _bufferGate = new();
    private readonly object _stateGate = new();
    private readonly MediaPlayer.LibVLCVideoLockCb _lockCallback;
    private readonly MediaPlayer.LibVLCVideoUnlockCb _unlockCallback;
    private readonly MediaPlayer.LibVLCVideoDisplayCb _displayCallback;

    private bool _started;
    private bool _busy;
    private bool _disposed;
    private long _nextRequestId;
    private long _activeRequestId;
    private double _activePosition;
    private long _captureNotBefore = long.MaxValue;

    public VideoFramePreviewDecoder(LibVLC libVlc, string path, uint width, uint height)
    {
        ArgumentNullException.ThrowIfNull(libVlc);
        ArgumentException.ThrowIfNullOrEmpty(path);
        if (width == 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height == 0) throw new ArgumentOutOfRangeException(nameof(height));

        _width = checked((int)width);
        _height = checked((int)height);
        _pitch = checked(_width * 4);
        _bufferLength = checked(_pitch * _height);
        _buffer = Marshal.AllocHGlobal(_bufferLength);

        _lockCallback = OnVideoLock;
        _unlockCallback = OnVideoUnlock;
        _displayCallback = OnVideoDisplay;

        Media? media = null;
        MediaPlayer? player = null;
        try
        {
            media = new Media(libVlc, new Uri(path));
            media.AddOption(":no-audio");
            media.AddOption(":no-sub-autodetect-file");

            player = new MediaPlayer(libVlc)
            {
                Mute = true,
                Volume = 0,
                EnableKeyInput = false,
                EnableMouseInput = false
            };
            player.Playing += OnPlayerPlaying;
            player.SetVideoFormat("RV32", width, height, checked((uint)_pitch));
            player.SetVideoCallbacks(_lockCallback, _unlockCallback, _displayCallback);

            _media = media;
            _player = player;
        }
        catch
        {
            if (player is not null)
            {
                player.Playing -= OnPlayerPlaying;
                player.Dispose();
            }
            media?.Dispose();
            Marshal.FreeHGlobal(_buffer);
            throw;
        }
    }

    public event EventHandler<VideoPreviewFrameReadyEventArgs>? FrameReady;

    public bool IsBusy
    {
        get
        {
            lock (_stateGate) return _busy;
        }
    }

    public bool RequestFrame(double position)
    {
        long requestId;
        bool start;
        lock (_stateGate)
        {
            if (_disposed || _busy) return false;

            _busy = true;
            _activeRequestId = requestId = ++_nextRequestId;
            _activePosition = Math.Clamp(position, 0, 1);
            _captureNotBefore = long.MaxValue;
            start = !_started;
            _started = true;
        }

        try
        {
            if (start)
            {
                if (_player.Play(_media)) return true;
                FailRequest(requestId);
                return false;
            }

            if (!_player.IsPlaying && !_player.Play())
            {
                FailRequest(requestId);
                return false;
            }

            ApplySeek(requestId);
            return true;
        }
        catch
        {
            FailRequest(requestId);
            return false;
        }
    }

    public void PauseAfterCapture(long requestId)
    {
        lock (_stateGate)
        {
            if (_disposed || _busy || requestId != _activeRequestId) return;
        }

        TryPause();
    }

    public void Cancel()
    {
        lock (_stateGate)
        {
            if (_disposed) return;
            _activeRequestId = ++_nextRequestId;
            _busy = false;
            _captureNotBefore = long.MaxValue;
        }

        TryPause();
    }

    private void OnPlayerPlaying(object? sender, EventArgs e)
    {
        long requestId;
        lock (_stateGate)
        {
            if (_disposed) return;
            if (!_busy)
            {
                ThreadPool.QueueUserWorkItem(static decoder =>
                    ((VideoFramePreviewDecoder)decoder!).PauseIfIdle(), this);
                return;
            }

            requestId = _activeRequestId;
        }

        ApplySeek(requestId);
    }

    private void ApplySeek(long requestId)
    {
        double position;
        lock (_stateGate)
        {
            if (_disposed || !_busy || requestId != _activeRequestId) return;
            position = _activePosition;
        }

        try
        {
            _player.Position = (float)position;
            lock (_stateGate)
            {
                if (!_disposed && _busy && requestId == _activeRequestId)
                    _captureNotBefore = Stopwatch.GetTimestamp() + FrameSettleTicks;
            }
        }
        catch
        {
            FailRequest(requestId);
        }
    }

    private IntPtr OnVideoLock(IntPtr opaque, IntPtr planes)
    {
        Monitor.Enter(_bufferGate);
        Marshal.WriteIntPtr(planes, _buffer);
        return _buffer;
    }

    private void OnVideoUnlock(IntPtr opaque, IntPtr picture, IntPtr planes)
    {
        byte[]? pixels = null;
        long requestId = 0;
        double position = 0;
        try
        {
            long candidateRequestId = 0;
            double candidatePosition = 0;
            lock (_stateGate)
            {
                if (!_disposed && _busy && Stopwatch.GetTimestamp() >= _captureNotBefore)
                {
                    candidateRequestId = _activeRequestId;
                    candidatePosition = _activePosition;
                }
            }

            if (candidateRequestId != 0 && IsPlaybackClockNearTarget(candidatePosition))
            {
                lock (_stateGate)
                {
                    if (!_disposed && _busy && candidateRequestId == _activeRequestId)
                    {
                        requestId = candidateRequestId;
                        position = candidatePosition;
                        _busy = false;
                        _captureNotBefore = long.MaxValue;
                        pixels = new byte[_bufferLength];
                    }
                }
            }

            if (pixels is not null)
                Marshal.Copy(_buffer, pixels, 0, pixels.Length);
        }
        finally
        {
            Monitor.Exit(_bufferGate);
        }

        if (pixels is not null)
        {
            try
            {
                FrameReady?.Invoke(this, new VideoPreviewFrameReadyEventArgs(
                    requestId,
                    position,
                    _width,
                    _height,
                    _pitch,
                    pixels));
            }
            catch
            {
                // Managed listeners must never unwind through VLC's native callback.
            }
        }
    }

    private bool IsPlaybackClockNearTarget(double position)
    {
        try
        {
            var length = _player.Length;
            var time = _player.Time;
            if (length <= 0 || time < 0) return true;

            var targetTime = length * position;
            return Math.Abs(time - targetTime) <= MaximumFrameTimeErrorMilliseconds;
        }
        catch
        {
            return false;
        }
    }

    private static void OnVideoDisplay(IntPtr opaque, IntPtr picture)
    {
    }

    private void FailRequest(long requestId)
    {
        lock (_stateGate)
        {
            if (_disposed || requestId != _activeRequestId) return;
            _busy = false;
            _captureNotBefore = long.MaxValue;
        }
    }

    private void PauseIfIdle()
    {
        lock (_stateGate)
        {
            if (_disposed || _busy) return;
        }

        TryPause();
    }

    private void TryPause()
    {
        try
        {
            if (_player.IsPlaying)
                _player.SetPause(true);
        }
        catch
        {
            // Preview playback is best-effort and never affects the main player.
        }
    }

    public void Dispose()
    {
        lock (_stateGate)
        {
            if (_disposed) return;
            _disposed = true;
            _busy = false;
            _captureNotBefore = long.MaxValue;
        }

        _player.Playing -= OnPlayerPlaying;
        try { _player.Stop(); } catch { }
        _player.Dispose();
        _media.Dispose();

        lock (_bufferGate)
            Marshal.FreeHGlobal(_buffer);
    }
}
