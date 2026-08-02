using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ImageViewer.Controls;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private SmoothScrollController _gridScrollController = null!;
    private SmoothScrollController? _treeScrollController;

    private void InitializeSmoothScrolling()
    {
        _gridScrollController = new SmoothScrollController(ThumbScroll);

        ThumbScroll.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnGridWheel,
            RoutingStrategies.Tunnel);
        FolderTree.AddHandler(
            InputElement.PointerWheelChangedEvent,
            OnTreeWheel,
            RoutingStrategies.Tunnel);
        ThumbScroll.AddHandler(
            InputElement.PointerPressedEvent,
            OnGridPointerPressed,
            RoutingStrategies.Tunnel);
        FolderTree.AddHandler(
            InputElement.PointerPressedEvent,
            OnTreePointerPressed,
            RoutingStrategies.Tunnel);
        ThumbScroll.AddHandler(
            InputElement.ScrollGestureEvent,
            OnGridScrollGesture,
            RoutingStrategies.Tunnel);
        FolderTree.AddHandler(
            InputElement.ScrollGestureEvent,
            OnTreeScrollGesture,
            RoutingStrategies.Tunnel);
        FolderTree.AddHandler(
            InputElement.KeyDownEvent,
            OnTreeKeyDown,
            RoutingStrategies.Tunnel);

        Loaded += (_, _) => EnsureTreeScrollController();
        DetachedFromVisualTree += (_, _) => CancelSmoothScrolling();
    }

    private void AttachSmoothScrolling(BrowserViewModel vm) =>
        vm.SmoothScrollingChanged += OnSmoothScrollingChanged;

    private void DetachSmoothScrolling(BrowserViewModel vm)
    {
        vm.SmoothScrollingChanged -= OnSmoothScrollingChanged;
        CancelSmoothScrolling();
    }

    private void OnSmoothScrollingChanged() => CancelSmoothScrolling();

    private void OnGridWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not BrowserViewModel vm) return;

        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            CancelGridSmoothScrolling();
            const int step = 24;
            var oldWidth = vm.ThumbnailWidth;
            vm.ResizeThumbnailsBy(e.Delta.Y > 0 ? step : -step);
            if (vm.ThumbnailWidth != oldWidth && vm.SelectedIndex >= 0)
                ScrollIndexIntoView(vm.SelectedIndex);
            e.Handled = true;
            return;
        }

        if (_gridScrollController.TryHandleWheel(
                e.Delta.Y,
                vm.IsSmoothScrollingActive))
        {
            e.Handled = true;
        }
    }

    private void OnTreeWheel(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not BrowserViewModel vm) return;
        var controller = EnsureTreeScrollController();
        if (controller?.TryHandleWheel(e.Delta.Y, vm.IsSmoothScrollingActive) == true)
            e.Handled = true;
    }

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e) =>
        CancelGridSmoothScrolling();

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e) =>
        CancelTreeSmoothScrolling();

    private void OnGridScrollGesture(object? sender, ScrollGestureEventArgs e) =>
        CancelGridSmoothScrolling();

    private void OnTreeScrollGesture(object? sender, ScrollGestureEventArgs e) =>
        CancelTreeSmoothScrolling();

    private void OnTreeKeyDown(object? sender, KeyEventArgs e) =>
        CancelTreeSmoothScrolling();

    private SmoothScrollController? EnsureTreeScrollController()
    {
        if (_treeScrollController is not null) return _treeScrollController;
        var scrollViewer = FolderTree.FindDescendantOfType<ScrollViewer>();
        if (scrollViewer is not null)
            _treeScrollController = new SmoothScrollController(scrollViewer);
        return _treeScrollController;
    }

    private void CancelSmoothScrolling()
    {
        CancelGridSmoothScrolling();
        CancelTreeSmoothScrolling();
    }

    private void CancelGridSmoothScrolling() => _gridScrollController.Cancel();

    private void CancelTreeSmoothScrolling() => _treeScrollController?.Cancel();
}
