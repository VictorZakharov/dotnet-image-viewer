using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace ImageViewer.Controls;

public sealed class ResourceHistoryGraph : Control
{
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<ResourceHistoryGraph, IReadOnlyList<double>?>(nameof(Values));

    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<ResourceHistoryGraph, double>(nameof(Maximum));

    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ResourceHistoryGraph, IBrush?>(
            nameof(Stroke),
            new SolidColorBrush(Color.Parse("#67b7ff")));

    public static readonly StyledProperty<IBrush?> GridStrokeProperty =
        AvaloniaProperty.Register<ResourceHistoryGraph, IBrush?>(
            nameof(GridStroke),
            new SolidColorBrush(Color.Parse("#263755")));

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<ResourceHistoryGraph, double>(nameof(StrokeThickness), 2);

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    public IBrush? GridStroke
    {
        get => GetValue(GridStrokeProperty);
        set => SetValue(GridStrokeProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    static ResourceHistoryGraph()
    {
        AffectsRender<ResourceHistoryGraph>(
            ValuesProperty,
            MaximumProperty,
            StrokeProperty,
            GridStrokeProperty,
            StrokeThicknessProperty);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var bounds = Bounds.Deflate(1);
        if (bounds.Width < 2 || bounds.Height < 2) return;

        DrawGrid(context, bounds);
        var values = Values;
        if (values is null || values.Count == 0 || Stroke is null) return;

        var maximum = Maximum > 0
            ? Maximum
            : Math.Max(1, values.Max());
        var pen = new Pen(Stroke, Math.Max(1, StrokeThickness));
        if (values.Count == 1)
        {
            var y = ValueToY(values[0], maximum, bounds);
            context.DrawLine(pen, new Point(bounds.Left, y), new Point(bounds.Right, y));
            return;
        }

        var step = bounds.Width / (values.Count - 1);
        var previous = new Point(
            bounds.Left,
            ValueToY(values[0], maximum, bounds));
        for (var index = 1; index < values.Count; index++)
        {
            var current = new Point(
                bounds.Left + (step * index),
                ValueToY(values[index], maximum, bounds));
            context.DrawLine(pen, previous, current);
            previous = current;
        }
    }

    private void DrawGrid(DrawingContext context, Rect bounds)
    {
        if (GridStroke is null) return;
        var pen = new Pen(GridStroke, 1);
        for (var division = 1; division < 4; division++)
        {
            var y = bounds.Top + (bounds.Height * division / 4);
            context.DrawLine(pen, new Point(bounds.Left, y), new Point(bounds.Right, y));
        }
        for (var division = 1; division < 6; division++)
        {
            var x = bounds.Left + (bounds.Width * division / 6);
            context.DrawLine(pen, new Point(x, bounds.Top), new Point(x, bounds.Bottom));
        }
    }

    private static double ValueToY(double value, double maximum, Rect bounds)
    {
        var normalized = Math.Clamp(value / maximum, 0, 1);
        return bounds.Bottom - (bounds.Height * normalized);
    }
}
