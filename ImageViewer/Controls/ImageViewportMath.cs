using System;
using Avalonia;

namespace ImageViewer.Controls;

public readonly record struct NormalizedImageViewport(
    double CenterX,
    double CenterY,
    double ZoomRatio,
    bool IsFit);

public static class ImageViewportMath
{
    private const double MaximumZoom = 32;

    public static double FitScale(Size viewport, Size image)
    {
        if (viewport.Width <= 0 || viewport.Height <= 0
            || image.Width <= 0 || image.Height <= 0) return 1;
        return Math.Min(
            viewport.Width / image.Width,
            viewport.Height / image.Height);
    }

    public static double ClampZoom(Size viewport, Size image, double zoom)
    {
        var fitScale = FitScale(viewport, image);
        return Math.Clamp(zoom, fitScale, Math.Max(MaximumZoom, fitScale));
    }

    public static Vector ConstrainOffset(
        Size viewport,
        Size image,
        double zoom,
        Vector offset) => new(
            ConstrainAxis(viewport.Width, image.Width * zoom, offset.X),
            ConstrainAxis(viewport.Height, image.Height * zoom, offset.Y));

    private static double ConstrainAxis(double viewport, double image, double offset)
    {
        if (image <= viewport) return (viewport - image) / 2;
        return Math.Clamp(offset, viewport - image, 0);
    }

    public static NormalizedImageViewport Capture(
        Size viewport,
        Size image,
        double zoom,
        Vector offset,
        bool isFit)
    {
        if (isFit) return new NormalizedImageViewport(0.5, 0.5, 1, true);
        var drawWidth = Math.Max(1, image.Width * zoom);
        var drawHeight = Math.Max(1, image.Height * zoom);
        return new NormalizedImageViewport(
            Math.Clamp((viewport.Width / 2 - offset.X) / drawWidth, 0, 1),
            Math.Clamp((viewport.Height / 2 - offset.Y) / drawHeight, 0, 1),
            Math.Max(1, zoom / FitScale(viewport, image)),
            false);
    }

    public static (double Zoom, Vector Offset) Apply(
        Size viewport,
        Size image,
        NormalizedImageViewport state)
    {
        var zoom = ClampZoom(
            viewport,
            image,
            FitScale(viewport, image) * Math.Max(1, state.ZoomRatio));
        var offset = new Vector(
            viewport.Width / 2 - Math.Clamp(state.CenterX, 0, 1) * image.Width * zoom,
            viewport.Height / 2 - Math.Clamp(state.CenterY, 0, 1) * image.Height * zoom);
        return (zoom, ConstrainOffset(viewport, image, zoom, offset));
    }
}
