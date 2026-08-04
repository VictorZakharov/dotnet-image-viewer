using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using ImageViewer.Controls;

namespace ImageViewer.Tests;

public sealed class CropSelectionMathTests
{
    [Fact]
    public void OverlayOwnsAFullTransparentPointerSurface()
    {
        Assert.True(typeof(Grid).IsAssignableFrom(typeof(CropSelectionOverlay)));
    }

    [Theory]
    [InlineData(CropResizeEdges.Left, StandardCursorType.LeftSide)]
    [InlineData(CropResizeEdges.Right, StandardCursorType.RightSide)]
    [InlineData(CropResizeEdges.Top, StandardCursorType.TopSide)]
    [InlineData(CropResizeEdges.Bottom, StandardCursorType.BottomSide)]
    [InlineData(CropResizeEdges.Left | CropResizeEdges.Top, StandardCursorType.TopLeftCorner)]
    [InlineData(CropResizeEdges.Right | CropResizeEdges.Top, StandardCursorType.TopRightCorner)]
    [InlineData(CropResizeEdges.Left | CropResizeEdges.Bottom, StandardCursorType.BottomLeftCorner)]
    [InlineData(CropResizeEdges.Right | CropResizeEdges.Bottom, StandardCursorType.BottomRightCorner)]
    public void CropEdgesUseDirectionalResizeCursors(
        CropResizeEdges edges,
        StandardCursorType expected) =>
        Assert.Equal(expected, CropCursorResolver.Resolve(edges, insideSelection: true));

    [Fact]
    public void CropInteriorUsesPanAndExteriorUsesCrosshair()
    {
        Assert.Equal(StandardCursorType.SizeAll,
            CropCursorResolver.Resolve(CropResizeEdges.None, insideSelection: true));
        Assert.Equal(StandardCursorType.Cross,
            CropCursorResolver.Resolve(CropResizeEdges.None, insideSelection: false));
    }

    [Fact]
    public void FitRectCentersImageWithoutChangingAspectRatio()
    {
        var result = CropSelectionMath.FitRect(
            new Rect(0, 0, 1000, 700), new Size(400, 200), padding: 0);

        Assert.Equal(new Rect(0, 100, 1000, 500), result);
    }

    [Fact]
    public void ViewportAndImageCoordinatesRoundTrip()
    {
        var imageRect = new Rect(100, 50, 800, 400);
        var pixels = new Size(4000, 2000);
        var selection = new Rect(500, 250, 2000, 1000);

        var viewport = CropSelectionMath.ToViewportRect(selection, imageRect, pixels);
        var topLeft = CropSelectionMath.ToImagePoint(viewport.TopLeft, imageRect, pixels);
        var bottomRight = CropSelectionMath.ToImagePoint(viewport.BottomRight, imageRect, pixels);

        Assert.Equal(selection.TopLeft, topLeft);
        Assert.Equal(selection.BottomRight, bottomRight);
    }

    [Fact]
    public void MovingSelectionStopsAtImageEdge()
    {
        var result = CropSelectionMath.Move(
            new Rect(20, 20, 50, 40), new Vector(100, -50), new Size(100, 80));

        Assert.Equal(new Rect(50, 0, 50, 40), result);
    }

    [Fact]
    public void ResizingCornerKeepsMinimumAndBounds()
    {
        var result = CropSelectionMath.Resize(
            new Rect(20, 20, 50, 40),
            new Point(120, -10),
            CropResizeEdges.Right | CropResizeEdges.Top,
            new Size(100, 80));

        Assert.Equal(new Rect(20, 0, 80, 60), result);
    }
}
