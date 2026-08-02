using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ImageViewer.Controls;

internal sealed class SmoothScrollController : IDisposable
{
    private readonly ScrollViewer _scrollViewer;
    private readonly InertialScrollMotion _motion = new();
    private readonly DispatcherTimer _timer;
    private long _lastTickTimestamp;
    private long _lastWheelTimestamp;

    public SmoothScrollController(ScrollViewer scrollViewer)
    {
        _scrollViewer = scrollViewer;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTick;
    }

    public bool TryHandleWheel(double delta, bool animationEnabled)
    {
        if (!animationEnabled || IsPrecisionDelta(delta))
        {
            Cancel();
            return false;
        }

        var maximumOffset = MaximumOffset;
        var now = Stopwatch.GetTimestamp();
        var elapsed = _lastWheelTimestamp == 0
            ? double.PositiveInfinity
            : Stopwatch.GetElapsedTime(_lastWheelTimestamp, now).TotalSeconds;
        _lastWheelTimestamp = now;

        var handled = _motion.AddWheelInput(
            _scrollViewer.Offset.Y,
            maximumOffset,
            delta,
            elapsed);
        if (!handled) return false;

        if (!_timer.IsEnabled)
        {
            _lastTickTimestamp = now;
            _timer.Start();
        }
        return true;
    }

    public void Cancel()
    {
        _timer.Stop();
        _motion.Reset(_scrollViewer.Offset.Y);
        _lastTickTimestamp = 0;
        _lastWheelTimestamp = 0;
    }

    public void Dispose()
    {
        Cancel();
        _timer.Tick -= OnTick;
    }

    internal static bool IsPrecisionDelta(double delta)
    {
        var magnitude = Math.Abs(delta);
        if (magnitude < 0.001) return true;
        return magnitude < 0.95 || Math.Abs(magnitude - Math.Round(magnitude)) > 0.01;
    }

    private double MaximumOffset => Math.Max(
        0,
        _scrollViewer.Extent.Height - _scrollViewer.Viewport.Height);

    private void OnTick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = _lastTickTimestamp == 0
            ? 1d / 60
            : Stopwatch.GetElapsedTime(_lastTickTimestamp, now).TotalSeconds;
        _lastTickTimestamp = now;

        var nextOffset = _motion.Advance(
            _scrollViewer.Offset.Y,
            MaximumOffset,
            _scrollViewer.Viewport.Height,
            elapsed);
        if (Math.Abs(nextOffset - _scrollViewer.Offset.Y) > 0.001)
            _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, nextOffset);

        if (!_motion.IsActive)
        {
            _timer.Stop();
            _lastTickTimestamp = 0;
        }
    }
}
