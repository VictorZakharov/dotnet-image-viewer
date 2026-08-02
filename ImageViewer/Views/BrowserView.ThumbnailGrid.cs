using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private readonly HashSet<Control> _realizedThumbnailElements = new();
    private bool _viewportRefreshScheduled;

    private void InitializeThumbnailGrid()
    {
        Loaded += (_, _) =>
        {
            FocusThumbnailGrid();
            ScheduleViewportRefresh();
        };
        ThumbRepeater.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnThumbWheel,
            RoutingStrategies.Tunnel);
        ThumbRepeater.AddHandler(
            InputElement.KeyDownEvent,
            OnThumbnailGridKeyDown,
            RoutingStrategies.Tunnel);
        ThumbRepeater.ElementPrepared += OnThumbnailElementPrepared;
        ThumbRepeater.ElementClearing += OnThumbnailElementClearing;
        ThumbRepeater.ElementIndexChanged += OnThumbnailElementIndexChanged;
        ThumbScroll.ScrollChanged += OnThumbScrollChanged;
        ThumbScroll.SizeChanged += OnThumbScrollSizeChanged;
    }

    private void AttachThumbnailGrid(BrowserViewModel vm)
    {
        vm.RenameRequested += OnRenameRequested;
        vm.ThumbnailRequestsInvalidated += ScheduleViewportRefresh;
        vm.FilteredItems.CollectionChanged += OnFilteredItemsChanged;
        vm.ReportRealizedItems(_realizedThumbnailElements.Count);
        ScheduleViewportRefresh();
    }

    private void DetachThumbnailGrid(BrowserViewModel vm)
    {
        vm.RenameRequested -= OnRenameRequested;
        vm.ThumbnailRequestsInvalidated -= ScheduleViewportRefresh;
        vm.FilteredItems.CollectionChanged -= OnFilteredItemsChanged;
    }

    public void FocusThumbnailGrid() => ThumbRepeater.Focus();

    private void OnFilteredItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        ScheduleViewportRefresh();

    private void OnThumbScrollChanged(object? sender, ScrollChangedEventArgs e) =>
        ScheduleViewportRefresh();

    private void OnThumbScrollSizeChanged(object? sender, SizeChangedEventArgs e) =>
        ScheduleViewportRefresh();

    private void OnThumbnailElementPrepared(
        object? sender,
        ItemsRepeaterElementPreparedEventArgs e)
    {
        _realizedThumbnailElements.Add(e.Element);
        _vm?.ReportRealizedItems(_realizedThumbnailElements.Count);
        ScheduleViewportRefresh();
    }

    private void OnThumbnailElementClearing(
        object? sender,
        ItemsRepeaterElementClearingEventArgs e)
    {
        _realizedThumbnailElements.Remove(e.Element);
        _vm?.ReportRealizedItems(_realizedThumbnailElements.Count);
    }

    private void OnThumbnailElementIndexChanged(
        object? sender,
        ItemsRepeaterElementIndexChangedEventArgs e) =>
        ScheduleViewportRefresh();

    private void ScheduleViewportRefresh()
    {
        if (_viewportRefreshScheduled) return;
        _viewportRefreshScheduled = true;
        Dispatcher.UIThread.Post(() =>
        {
            _viewportRefreshScheduled = false;
            RefreshThumbnailViewport();
        }, DispatcherPriority.Loaded);
    }

    private void RefreshThumbnailViewport()
    {
        if (_vm is not { } vm) return;
        var total = vm.FilteredItems.Count;
        if (total == 0)
        {
            vm.UpdateThumbnailViewport(0, -1, 0, -1);
            return;
        }

        var viewportWidth = ThumbScroll.Viewport.Width;
        var viewportHeight = ThumbScroll.Viewport.Height;
        if (viewportWidth <= 0 || viewportHeight <= 0) return;
        ConstrainRepeaterToViewport(viewportWidth);

        var itemsPerRow = Math.Max(1, (int)Math.Floor(
            viewportWidth / vm.ThumbnailCellWidth));
        vm.ReportGridLayoutMetrics(
            itemsPerRow,
            viewportWidth,
            ThumbScroll.Bounds.Width,
            ThumbRepeater.Bounds.Width);
        var cellHeight = vm.ThumbnailCellHeight;
        var firstVisibleRow = Math.Max(0, (int)Math.Floor(
            ThumbScroll.Offset.Y / cellHeight));
        var lastVisibleRow = Math.Max(firstVisibleRow, (int)Math.Floor(
            (ThumbScroll.Offset.Y + viewportHeight - 0.01) / cellHeight));
        var overscanRows = Math.Max(1, (int)Math.Ceiling(viewportHeight / cellHeight));

        var firstVisible = Math.Min(total - 1, firstVisibleRow * itemsPerRow);
        var lastVisible = Math.Min(total - 1, ((lastVisibleRow + 1) * itemsPerRow) - 1);
        var firstOverscan = Math.Max(0, (firstVisibleRow - overscanRows) * itemsPerRow);
        var lastOverscan = Math.Min(
            total - 1,
            ((lastVisibleRow + overscanRows + 1) * itemsPerRow) - 1);

        vm.UpdateThumbnailViewport(
            firstVisible,
            lastVisible,
            firstOverscan,
            lastOverscan);
    }

    private void ConstrainRepeaterToViewport(double viewportWidth)
    {
        if (double.IsNaN(ThumbRepeater.Width)
            || Math.Abs(ThumbRepeater.Width - viewportWidth) > 0.5)
        {
            ThumbRepeater.Width = viewportWidth;
        }
    }

    private void OnThumbnailPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: ThumbnailItem item }) return;
        if (DataContext is not BrowserViewModel vm) return;
        if (IsInsideTextBox(e.Source as Visual)) return;

        var properties = e.GetCurrentPoint(this).Properties;
        if (!properties.IsLeftButtonPressed && !properties.IsRightButtonPressed) return;
        vm.SelectItem(item);
        FocusThumbnailGrid();
    }

    private static bool IsInsideTextBox(Visual? visual)
    {
        for (var current = visual; current is not null; current = current.GetVisualParent())
            if (current is TextBox) return true;
        return false;
    }

    private void OnThumbDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not BrowserViewModel vm) return;
        if (vm.FilteredItems.Any(item => item.IsRenaming)) return;
        vm.OpenSelected();
    }

    private void OnThumbWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (DataContext is not BrowserViewModel vm) return;

        const int step = 24;
        var oldWidth = vm.ThumbnailWidth;
        vm.ResizeThumbnailsBy(e.Delta.Y > 0 ? step : -step);
        if (vm.ThumbnailWidth != oldWidth && vm.SelectedIndex >= 0)
            ScrollIndexIntoView(vm.SelectedIndex);
        e.Handled = true;
    }

    private void OnThumbnailGridKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        if (DataContext is not BrowserViewModel vm) return;
        if (vm.FilteredItems.Any(item => item.IsRenaming)) return;
        var total = vm.FilteredItems.Count;
        if (total == 0) return;

        var perRow = ItemsPerRow(vm);
        var index = Math.Max(0, vm.SelectedIndex);
        var newIndex = GetNavigationTarget(e.Key, index, total, perRow, RowsPerPage(vm));
        if (newIndex < 0) return;

        if (newIndex != index)
        {
            vm.SelectIndex(newIndex);
            ScrollIndexIntoView(newIndex);
        }
        e.Handled = true;
    }

    private static int GetNavigationTarget(
        Key key,
        int index,
        int total,
        int perRow,
        int rowsPerPage) => key switch
    {
        Key.Down => index + perRow < total ? index + perRow : index,
        Key.Up => index >= perRow ? index - perRow : index,
        Key.Right => Math.Min(index + 1, total - 1),
        Key.Left => Math.Max(index - 1, 0),
        Key.PageDown => Math.Min(
            index + perRow * Math.Max(1, rowsPerPage - 1),
            total - 1),
        Key.PageUp => Math.Max(
            index - perRow * Math.Max(1, rowsPerPage - 1),
            0),
        Key.Home => 0,
        Key.End => total - 1,
        _ => -1
    };

    private int ItemsPerRow(BrowserViewModel vm) => Math.Max(
        1,
        (int)Math.Floor(ThumbScroll.Viewport.Width / vm.ThumbnailCellWidth));

    private int RowsPerPage(BrowserViewModel vm) => Math.Max(
        1,
        (int)Math.Floor(ThumbScroll.Viewport.Height / vm.ThumbnailCellHeight));

    private void ScrollIndexIntoView(int index)
    {
        Dispatcher.UIThread.Post(() =>
        {
            try
            {
                var element = ThumbRepeater.TryGetElement(index)
                    ?? ThumbRepeater.GetOrCreateElement(index);
                element.BringIntoView();
                ScheduleViewportRefresh();
            }
            catch
            {
                // The collection may have changed before this layout pass.
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnThumbPropertiesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BrowserViewModel vm) return;
        if (sender is MenuItem { DataContext: ThumbnailItem item })
            vm.SelectItem(item);
        vm.ShowExifPane = true;
    }
}
