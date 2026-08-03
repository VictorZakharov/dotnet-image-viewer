using Avalonia;
using ImageViewer.Controls;

namespace ImageViewer.Tests;

public sealed class ImageViewportMathTests
{
    [Fact]
    public void NormalizedCenterSurvivesDifferentImageDimensions()
    {
        var state = new NormalizedImageViewport(
            CenterX: 0.72,
            CenterY: 0.31,
            ZoomRatio: 3.4,
            IsFit: false);
        var viewport = new Size(800, 500);
        var targetImage = new Size(6000, 4000);

        var applied = ImageViewportMath.Apply(viewport, targetImage, state);
        var captured = ImageViewportMath.Capture(
            viewport, targetImage, applied.Zoom, applied.Offset, isFit: false);

        Assert.Equal(state.CenterX, captured.CenterX, precision: 10);
        Assert.Equal(state.CenterY, captured.CenterY, precision: 10);
        Assert.Equal(state.ZoomRatio, captured.ZoomRatio, precision: 10);
    }

    [Fact]
    public void FitStateAlwaysRepresentsCenteredWholeImage()
    {
        var state = ImageViewportMath.Capture(
            new Size(1200, 700),
            new Size(3000, 2000),
            zoom: 0.2,
            offset: new Vector(17, 29),
            isFit: true);

        Assert.True(state.IsFit);
        Assert.Equal(0.5, state.CenterX);
        Assert.Equal(0.5, state.CenterY);
        Assert.Equal(1, state.ZoomRatio);
    }

    [Fact]
    public void FitScaleUpscalesSmallImageUntilOneAxisTouchesViewport()
    {
        var viewport = new Size(1000, 800);
        var image = new Size(500, 250);

        var scale = ImageViewportMath.FitScale(viewport, image);

        Assert.Equal(2, scale);
        Assert.Equal(viewport.Width, image.Width * scale);
        Assert.True(image.Height * scale < viewport.Height);
    }

    [Fact]
    public void ZoomAndOffsetCannotExposeBackgroundOnBothAxes()
    {
        var viewport = new Size(1000, 800);
        var image = new Size(2000, 1000);
        var zoom = ImageViewportMath.ClampZoom(viewport, image, 0.1);
        var offset = ImageViewportMath.ConstrainOffset(
            viewport,
            image,
            zoom,
            new Vector(200, -400));

        Assert.Equal(0.5, zoom);
        Assert.Equal(0, offset.X);
        Assert.Equal(150, offset.Y);
    }

    [Fact]
    public void PanningCannotPullAZoomedImageInsideViewportEdges()
    {
        var offset = ImageViewportMath.ConstrainOffset(
            new Size(1000, 800),
            new Size(2000, 1000),
            zoom: 1,
            new Vector(200, -400));

        Assert.Equal(0, offset.X);
        Assert.Equal(-200, offset.Y);
    }

    [Fact]
    public void SynchronizedViewportCannotApplyZoomBelowFit()
    {
        var viewport = new Size(1000, 800);
        var image = new Size(2000, 1000);
        var state = new NormalizedImageViewport(0.2, 0.8, 0.25, IsFit: false);

        var applied = ImageViewportMath.Apply(viewport, image, state);

        Assert.Equal(0.5, applied.Zoom);
        Assert.Equal(new Vector(0, 150), applied.Offset);
    }
}
