using Avalonia.Input;

namespace ImageViewer.Controls;

public static class CropCursorResolver
{
    public static StandardCursorType Resolve(
        CropResizeEdges edges,
        bool insideSelection) => edges switch
    {
        CropResizeEdges.Left | CropResizeEdges.Top => StandardCursorType.TopLeftCorner,
        CropResizeEdges.Right | CropResizeEdges.Top => StandardCursorType.TopRightCorner,
        CropResizeEdges.Left | CropResizeEdges.Bottom => StandardCursorType.BottomLeftCorner,
        CropResizeEdges.Right | CropResizeEdges.Bottom => StandardCursorType.BottomRightCorner,
        CropResizeEdges.Left => StandardCursorType.LeftSide,
        CropResizeEdges.Right => StandardCursorType.RightSide,
        CropResizeEdges.Top => StandardCursorType.TopSide,
        CropResizeEdges.Bottom => StandardCursorType.BottomSide,
        _ when insideSelection => StandardCursorType.SizeAll,
        _ => StandardCursorType.Cross
    };
}
