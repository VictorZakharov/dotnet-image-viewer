using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ImageViewer.Controls;

public partial class ZoomPanImage : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<ZoomPanImage, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<int> RotationProperty =
        AvaloniaProperty.Register<ZoomPanImage, int>(nameof(Rotation));

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public int Rotation
    {
        get => GetValue(RotationProperty);
        set => SetValue(RotationProperty, value);
    }

    private double _zoom = 1.0;
    private Vector _offset;
    private bool _fitMode = true;

    private bool _panning;
    private Point _panStart;
    private Vector _panStartOffset;

    static ZoomPanImage()
    {
        AffectsRender<ZoomPanImage>(SourceProperty, RotationProperty);
        SourceProperty.Changed.AddClassHandler<ZoomPanImage>((c, _) => c.ResetViewCore(false));
        RotationProperty.Changed.AddClassHandler<ZoomPanImage>((c, _) => c.ResetViewCore(false));
    }

    public ZoomPanImage()
    {
        Focusable = true;
        ClipToBounds = true;
        SizeChanged += (_, _) =>
        {
            ConstrainManualViewport();
            InvalidateVisual();
        };
        DoubleTapped += OnDoubleTappedHandler;
    }

    public void ResetView() => ResetViewCore(true);

    public void SetActualSize() => SetActualSize(notify: true);

    public override void Render(DrawingContext context)
    {
        var src = Source;
        if (src is null) return;

        var bounds = Bounds;
        if (bounds.Width < 1 || bounds.Height < 1) return;

        var bmpSize = src.Size;
        var displaySize = GetRotatedSize(bmpSize, Rotation);

        double drawW;
        double drawH;
        Vector offset;

        if (_fitMode)
        {
            double scale = ImageViewportMath.FitScale(bounds.Size, displaySize);
            drawW = displaySize.Width * scale;
            drawH = displaySize.Height * scale;
            offset = new Vector((bounds.Width - drawW) / 2.0, (bounds.Height - drawH) / 2.0);
            _zoom = scale;
            _offset = offset;
        }
        else
        {
            drawW = displaySize.Width * _zoom;
            drawH = displaySize.Height * _zoom;
            offset = _offset;
        }

        var center = new Point(offset.X + drawW / 2.0, offset.Y + drawH / 2.0);

        double bmpDrawW = bmpSize.Width * _zoom;
        double bmpDrawH = bmpSize.Height * _zoom;
        var bmpRect = new Rect(
            center.X - bmpDrawW / 2.0,
            center.Y - bmpDrawH / 2.0,
            bmpDrawW,
            bmpDrawH);

        if (Rotation != 0)
        {
            double radians = Rotation * Math.PI / 180.0;
            var transform = Matrix.CreateTranslation(-center.X, -center.Y)
                          * Matrix.CreateRotation(radians)
                          * Matrix.CreateTranslation(center.X, center.Y);
            using (context.PushTransform(transform))
            {
                context.DrawImage(src, bmpRect);
            }
        }
        else
        {
            context.DrawImage(src, bmpRect);
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var src = Source;
        if (src is null) return;
        if (_fitMode && e.Delta.Y <= 0)
        {
            e.Handled = true;
            return;
        }

        EnsureManualMode();

        const double wheelStep = 1.15;
        double factor = e.Delta.Y > 0 ? wheelStep : 1.0 / wheelStep;

        var displaySize = GetRotatedSize(src.Size, Rotation);
        double newZoom = ImageViewportMath.ClampZoom(
            Bounds.Size,
            displaySize,
            _zoom * factor);
        if (Math.Abs(newZoom - _zoom) < 0.0001)
        {
            e.Handled = true;
            return;
        }

        var cursor = e.GetPosition(this);
        double ratio = newZoom / _zoom;
        _offset = new Vector(
            cursor.X - (cursor.X - _offset.X) * ratio,
            cursor.Y - (cursor.Y - _offset.Y) * ratio);
        _zoom = newZoom;
        ConstrainManualViewport();

        InvalidateVisual();
        RaiseViewportChanged();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        if (Source is not null && pt.Properties.IsLeftButtonPressed)
        {
            Focus();
            EnsureManualMode();
            _panning = true;
            _panStart = e.GetPosition(this);
            _panStartOffset = _offset;
            Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Pointer.Capture(this);
            e.Handled = true;
            return;
        }
        base.OnPointerPressed(e);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_panning) return;
        var cur = e.GetPosition(this);
        _offset = _panStartOffset + (cur - _panStart);
        ConstrainManualViewport();
        InvalidateVisual();
        RaiseViewportChanged();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_panning) return;
        _panning = false;
        Cursor = new Cursor(StandardCursorType.Arrow);
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnDoubleTappedHandler(object? sender, TappedEventArgs e)
    {
        if (Source is not { } source) return;
        var displaySize = GetRotatedSize(source.Size, Rotation);
        var actualZoom = ImageViewportMath.ClampZoom(Bounds.Size, displaySize, 1);
        if (_fitMode || Math.Abs(_zoom - actualZoom) > 0.01)
            SetActualSize();
        else
            ResetView();
        e.Handled = true;
    }

    private void EnsureManualMode()
    {
        if (!_fitMode) return;
        var src = Source;
        if (src is null) return;
        var displaySize = GetRotatedSize(src.Size, Rotation);
        double fitScale = ImageViewportMath.FitScale(Bounds.Size, displaySize);
        double dispW = displaySize.Width * fitScale;
        double dispH = displaySize.Height * fitScale;
        _zoom = fitScale;
        _offset = new Vector((Bounds.Width - dispW) / 2.0, (Bounds.Height - dispH) / 2.0);
        _fitMode = false;
    }

    private static Size GetRotatedSize(Size original, int rotation)
    {
        return (Math.Abs(rotation) % 180 == 0)
            ? original
            : new Size(original.Height, original.Width);
    }
}
