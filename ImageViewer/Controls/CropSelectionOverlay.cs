using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace ImageViewer.Controls;

public partial class CropSelectionOverlay : Grid
{
    private static readonly IBrush ShadeBrush = new SolidColorBrush(Color.FromArgb(170, 0, 0, 0));
    private static readonly IBrush HandleBrush = new SolidColorBrush(Color.FromRgb(74, 174, 255));
    private static readonly IPen SelectionPen = new Pen(HandleBrush, 2);
    private static readonly IPen GridPen = new Pen(
        new SolidColorBrush(Color.FromArgb(150, 255, 255, 255)), 1);

    private Size _pixelSize;
    private Rect _selection;
    private readonly CropSelectionAdorner _adorner;

    public event EventHandler? SelectionChanged;
    public event EventHandler? SelectionCommitted;

    public Rect Selection => _selection;
    public bool HasImage => _pixelSize.Width > 0 && _pixelSize.Height > 0;

    public CropSelectionOverlay()
    {
        ClipToBounds = true;
        Focusable = true;
        Background = Brushes.Transparent;
        Cursor = new Cursor(StandardCursorType.Cross);
        _adorner = new CropSelectionAdorner(this) { IsHitTestVisible = false };
        Children.Add(_adorner);
    }

    public void Start(int pixelWidth, int pixelHeight)
    {
        _pixelSize = new Size(Math.Max(1, pixelWidth), Math.Max(1, pixelHeight));
        var marginX = Math.Round(_pixelSize.Width * 0.1);
        var marginY = Math.Round(_pixelSize.Height * 0.1);
        SetSelection(new Rect(
            marginX,
            marginY,
            Math.Max(1, _pixelSize.Width - marginX * 2),
            Math.Max(1, _pixelSize.Height - marginY * 2)));
    }

    public void SelectFullImage() => SetSelection(new Rect(default, _pixelSize));

    public void SetSelection(Rect selection, bool notify = true)
    {
        if (!HasImage) return;
        _selection = CropSelectionMath.Round(selection, _pixelSize);
        _adorner.InvalidateVisual();
        if (notify) SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RenderAdorner(DrawingContext context, Rect bounds)
    {
        if (!HasImage) return;
        var imageRect = CropSelectionMath.FitRect(bounds, _pixelSize);
        var selected = CropSelectionMath.ToViewportRect(_selection, imageRect, _pixelSize);
        DrawShade(context, imageRect, selected);
        DrawSelection(context, selected);
    }

    private static void DrawShade(DrawingContext context, Rect image, Rect selected)
    {
        context.DrawRectangle(ShadeBrush, null,
            new Rect(image.Left, image.Top, image.Width, Math.Max(0, selected.Top - image.Top)));
        context.DrawRectangle(ShadeBrush, null,
            new Rect(image.Left, selected.Bottom, image.Width, Math.Max(0, image.Bottom - selected.Bottom)));
        context.DrawRectangle(ShadeBrush, null,
            new Rect(image.Left, selected.Top, Math.Max(0, selected.Left - image.Left), selected.Height));
        context.DrawRectangle(ShadeBrush, null,
            new Rect(selected.Right, selected.Top, Math.Max(0, image.Right - selected.Right), selected.Height));
    }

    private static void DrawSelection(DrawingContext context, Rect rect)
    {
        context.DrawRectangle(null, SelectionPen, rect);
        context.DrawLine(GridPen,
            new Point(rect.Left + rect.Width / 3, rect.Top),
            new Point(rect.Left + rect.Width / 3, rect.Bottom));
        context.DrawLine(GridPen,
            new Point(rect.Left + rect.Width * 2 / 3, rect.Top),
            new Point(rect.Left + rect.Width * 2 / 3, rect.Bottom));
        context.DrawLine(GridPen,
            new Point(rect.Left, rect.Top + rect.Height / 3),
            new Point(rect.Right, rect.Top + rect.Height / 3));
        context.DrawLine(GridPen,
            new Point(rect.Left, rect.Top + rect.Height * 2 / 3),
            new Point(rect.Right, rect.Top + rect.Height * 2 / 3));

        foreach (var point in HandlePoints(rect))
            context.DrawRectangle(HandleBrush, null,
                new Rect(point.X - 5, point.Y - 5, 10, 10));
    }

    private static Point[] HandlePoints(Rect rect) =>
    [
        rect.TopLeft,
        new Point(rect.Center.X, rect.Top),
        rect.TopRight,
        new Point(rect.Left, rect.Center.Y),
        new Point(rect.Right, rect.Center.Y),
        rect.BottomLeft,
        new Point(rect.Center.X, rect.Bottom),
        rect.BottomRight
    ];

    private sealed class CropSelectionAdorner(CropSelectionOverlay owner) : Control
    {
        public override void Render(DrawingContext context) =>
            owner.RenderAdorner(context, Bounds);
    }
}
