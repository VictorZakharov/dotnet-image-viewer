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
}
