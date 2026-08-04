using System;
using Avalonia;

namespace ImageViewer.Controls;

[Flags]
public enum CropResizeEdges
{
    None = 0,
    Left = 1,
    Top = 2,
    Right = 4,
    Bottom = 8
}

public static class CropSelectionMath
{
    public static Rect FitRect(Rect viewport, Size image, double padding = 0)
    {
        var available = viewport.Deflate(Math.Max(0, padding));
        if (available.Width <= 0 || available.Height <= 0
            || image.Width <= 0 || image.Height <= 0) return default;
        var scale = Math.Min(available.Width / image.Width, available.Height / image.Height);
        var width = image.Width * scale;
        var height = image.Height * scale;
        return new Rect(
            available.X + (available.Width - width) / 2,
            available.Y + (available.Height - height) / 2,
            width,
            height);
    }

    public static Point ToImagePoint(Point point, Rect imageRect, Size pixelSize)
    {
        if (imageRect.Width <= 0 || imageRect.Height <= 0) return default;
        return new Point(
            Math.Clamp((point.X - imageRect.X) / imageRect.Width * pixelSize.Width,
                0, pixelSize.Width),
            Math.Clamp((point.Y - imageRect.Y) / imageRect.Height * pixelSize.Height,
                0, pixelSize.Height));
    }

    public static Rect ToViewportRect(Rect selection, Rect imageRect, Size pixelSize) => new(
        imageRect.X + selection.X / pixelSize.Width * imageRect.Width,
        imageRect.Y + selection.Y / pixelSize.Height * imageRect.Height,
        selection.Width / pixelSize.Width * imageRect.Width,
        selection.Height / pixelSize.Height * imageRect.Height);

    public static Rect FromPoints(Point first, Point second, Size bounds) => Clamp(
        new Rect(
            Math.Min(first.X, second.X),
            Math.Min(first.Y, second.Y),
            Math.Abs(first.X - second.X),
            Math.Abs(first.Y - second.Y)),
        bounds);

    public static Rect Move(Rect original, Vector delta, Size bounds)
    {
        var x = Math.Clamp(original.X + delta.X, 0, Math.Max(0, bounds.Width - original.Width));
        var y = Math.Clamp(original.Y + delta.Y, 0, Math.Max(0, bounds.Height - original.Height));
        return new Rect(x, y, original.Width, original.Height);
    }

    public static Rect Resize(
        Rect original,
        Point pointer,
        CropResizeEdges edges,
        Size bounds,
        double minimum = 1)
    {
        var left = edges.HasFlag(CropResizeEdges.Left)
            ? Math.Clamp(pointer.X, 0, original.Right - minimum)
            : original.Left;
        var right = edges.HasFlag(CropResizeEdges.Right)
            ? Math.Clamp(pointer.X, original.Left + minimum, bounds.Width)
            : original.Right;
        var top = edges.HasFlag(CropResizeEdges.Top)
            ? Math.Clamp(pointer.Y, 0, original.Bottom - minimum)
            : original.Top;
        var bottom = edges.HasFlag(CropResizeEdges.Bottom)
            ? Math.Clamp(pointer.Y, original.Top + minimum, bounds.Height)
            : original.Bottom;
        return new Rect(left, top, right - left, bottom - top);
    }

    public static Rect Clamp(Rect selection, Size bounds, double minimum = 1)
    {
        var left = Math.Clamp(selection.Left, 0, Math.Max(0, bounds.Width - minimum));
        var top = Math.Clamp(selection.Top, 0, Math.Max(0, bounds.Height - minimum));
        var right = Math.Clamp(selection.Right, left + minimum, bounds.Width);
        var bottom = Math.Clamp(selection.Bottom, top + minimum, bounds.Height);
        return new Rect(left, top, right - left, bottom - top);
    }

    public static Rect Round(Rect selection, Size bounds)
    {
        var left = Math.Round(selection.Left);
        var top = Math.Round(selection.Top);
        var right = Math.Round(selection.Right);
        var bottom = Math.Round(selection.Bottom);
        return Clamp(new Rect(left, top, right - left, bottom - top), bounds);
    }
}
