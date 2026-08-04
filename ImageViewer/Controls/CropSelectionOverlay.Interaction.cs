using System;
using Avalonia;
using Avalonia.Input;

namespace ImageViewer.Controls;

public partial class CropSelectionOverlay
{
    private CropDragMode _dragMode;
    private CropResizeEdges _resizeEdges;
    private Point _dragStart;
    private Rect _dragStartSelection;
    private StandardCursorType? _currentCursorType;

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (!HasImage || !point.Properties.IsLeftButtonPressed) return;
        Focus();
        var imageRect = CropSelectionMath.FitRect(Bounds, _pixelSize);
        var viewportSelection = CropSelectionMath.ToViewportRect(
            _selection, imageRect, _pixelSize);
        var viewportPoint = e.GetPosition(this);
        _dragStart = CropSelectionMath.ToImagePoint(viewportPoint, imageRect, _pixelSize);
        _dragStartSelection = _selection;
        _resizeEdges = HitEdges(viewportPoint, viewportSelection);
        _dragMode = _resizeEdges != CropResizeEdges.None
            ? CropDragMode.Resize
            : viewportSelection.Contains(viewportPoint)
                ? CropDragMode.Move
                : CropDragMode.New;
        SetCursor(_dragMode switch
        {
            CropDragMode.Resize => CropCursorResolver.Resolve(
                _resizeEdges, insideSelection: true),
            CropDragMode.Move => StandardCursorType.SizeAll,
            _ => StandardCursorType.Cross
        });
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!HasImage) return;
        var imageRect = CropSelectionMath.FitRect(Bounds, _pixelSize);
        var viewportPoint = e.GetPosition(this);
        if (_dragMode == CropDragMode.None)
        {
            UpdateHoverCursor(viewportPoint, imageRect);
            return;
        }

        var imagePoint = CropSelectionMath.ToImagePoint(viewportPoint, imageRect, _pixelSize);
        var next = _dragMode switch
        {
            CropDragMode.New => CropSelectionMath.FromPoints(_dragStart, imagePoint, _pixelSize),
            CropDragMode.Move => CropSelectionMath.Move(
                _dragStartSelection, imagePoint - _dragStart, _pixelSize),
            _ => CropSelectionMath.Resize(
                _dragStartSelection, imagePoint, _resizeEdges, _pixelSize)
        };
        SetSelection(next);
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (_dragMode == CropDragMode.None) return;
        _dragMode = CropDragMode.None;
        _resizeEdges = CropResizeEdges.None;
        e.Pointer.Capture(null);
        UpdateHoverCursor(e.GetPosition(this));
        SelectionCommitted?.Invoke(this, EventArgs.Empty);
        e.Handled = true;
    }

    private static CropResizeEdges HitEdges(Point point, Rect rect)
    {
        const double tolerance = 11;
        if (!rect.Inflate(tolerance).Contains(point)) return CropResizeEdges.None;
        var edges = CropResizeEdges.None;
        if (Math.Abs(point.X - rect.Left) <= tolerance) edges |= CropResizeEdges.Left;
        else if (Math.Abs(point.X - rect.Right) <= tolerance) edges |= CropResizeEdges.Right;
        if (Math.Abs(point.Y - rect.Top) <= tolerance) edges |= CropResizeEdges.Top;
        else if (Math.Abs(point.Y - rect.Bottom) <= tolerance) edges |= CropResizeEdges.Bottom;
        return edges;
    }

    private void UpdateHoverCursor(Point point, Rect? imageRect = null)
    {
        var image = imageRect ?? CropSelectionMath.FitRect(Bounds, _pixelSize);
        var selection = CropSelectionMath.ToViewportRect(_selection, image, _pixelSize);
        var edges = HitEdges(point, selection);
        SetCursor(CropCursorResolver.Resolve(edges, selection.Contains(point)));
    }

    private void SetCursor(StandardCursorType type)
    {
        if (_currentCursorType == type) return;
        _currentCursorType = type;
        Cursor = new Cursor(type);
    }

    private enum CropDragMode
    {
        None,
        New,
        Move,
        Resize
    }
}
