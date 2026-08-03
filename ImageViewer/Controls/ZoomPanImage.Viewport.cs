using System;
using Avalonia;

namespace ImageViewer.Controls;

public partial class ZoomPanImage
{
    public event EventHandler? ViewportChanged;

    public bool IsFitView => _fitMode;

    public NormalizedImageViewport CurrentViewport
    {
        get
        {
            var source = Source;
            if (source is null)
                return new NormalizedImageViewport(0.5, 0.5, 1, true);
            return ImageViewportMath.Capture(
                Bounds.Size,
                GetRotatedSize(source.Size, Rotation),
                _zoom,
                _offset,
                _fitMode);
        }
    }

    public void ApplyViewport(NormalizedImageViewport state, bool notify = false)
    {
        var source = Source;
        if (source is null) return;
        if (state.IsFit)
        {
            ResetViewCore(notify);
            return;
        }

        var applied = ImageViewportMath.Apply(
            Bounds.Size,
            GetRotatedSize(source.Size, Rotation),
            state);
        _zoom = applied.Zoom;
        _offset = applied.Offset;
        _fitMode = false;
        InvalidateVisual();
        if (notify) RaiseViewportChanged();
    }

    public void SetActualSize(bool notify)
    {
        var source = Source;
        if (source is null) return;
        var displaySize = GetRotatedSize(source.Size, Rotation);
        _zoom = ImageViewportMath.ClampZoom(Bounds.Size, displaySize, 1);
        _offset = new Vector(
            (Bounds.Width - displaySize.Width * _zoom) / 2,
            (Bounds.Height - displaySize.Height * _zoom) / 2);
        _fitMode = false;
        InvalidateVisual();
        if (notify) RaiseViewportChanged();
    }

    private void ResetViewCore(bool notify)
    {
        _zoom = 1;
        _offset = default;
        _fitMode = true;
        InvalidateVisual();
        if (notify) RaiseViewportChanged();
    }

    private void ConstrainManualViewport()
    {
        if (_fitMode || Source is not { } source) return;
        var displaySize = GetRotatedSize(source.Size, Rotation);
        _zoom = ImageViewportMath.ClampZoom(Bounds.Size, displaySize, _zoom);
        _offset = ImageViewportMath.ConstrainOffset(
            Bounds.Size,
            displaySize,
            _zoom,
            _offset);
    }

    private void RaiseViewportChanged() => ViewportChanged?.Invoke(this, EventArgs.Empty);
}
