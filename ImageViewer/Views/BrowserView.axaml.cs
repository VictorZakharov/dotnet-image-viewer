using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class BrowserView : UserControl
{
    private BrowserViewModel? _vm;

    public BrowserView()
    {
        InitializeComponent();
        Loaded += (_, _) => ThumbList.Focus();
        ThumbList.AddHandler(InputElement.PointerWheelChangedEvent, OnThumbWheel, RoutingStrategies.Tunnel);
        ThumbList.AddHandler(InputElement.KeyDownEvent, OnThumbListKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.KeyDownEvent, OnRootPreviewKeyDown, RoutingStrategies.Tunnel);
        AddHandler(InputElement.PointerPressedEvent, OnRootPointerPressed, RoutingStrategies.Tunnel);
        FolderTree.SizeChanged += OnFolderTreeSizeChanged;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnRootPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not BrowserViewModel vm) return;
        if (!vm.FilteredItems.Any(i => i.IsRenaming)) return;

        // If the rename TextBox is the event source, let Left/Right/Home/End
        // reach it so the caret moves. Up/Down/PageUp/Down are single-line
        // TextBox no-ops, so we still swallow them to keep the ListBox bubble
        // class handler from navigating selection.
        if (e.Source is TextBox)
        {
            switch (e.Key)
            {
                case Key.Up:
                case Key.Down:
                case Key.PageUp:
                case Key.PageDown:
                    e.Handled = true;
                    break;
            }
            return;
        }

        // Focus didn't land on the rename TextBox — swallow all nav keys so the
        // grid doesn't shift behind the still-open rename.
        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
            case Key.PageUp:
            case Key.PageDown:
                e.Handled = true;
                break;
        }
    }

    private void OnRootPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not BrowserViewModel vm) return;
        if (!vm.IsEditingPath) return;
        if (e.Source is Visual v && IsInsideVisual(v, PathEditBox)) return;
        vm.CancelPathEdit();
    }

    private static bool IsInsideVisual(Visual? element, Visual? ancestor)
    {
        if (ancestor is null) return false;
        for (var cur = element; cur is not null; cur = cur.GetVisualParent())
            if (ReferenceEquals(cur, ancestor)) return true;
        return false;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.TreeNodeFocused -= OnTreeNodeFocused;
            _vm.RenameRequested -= OnRenameRequested;
        }
        _vm = DataContext as BrowserViewModel;
        if (_vm is not null)
        {
            _vm.TreeNodeFocused += OnTreeNodeFocused;
            _vm.RenameRequested += OnRenameRequested;
        }
    }

    private void OnRenameRequested(ThumbnailItem item)
    {
        // AttachedToVisualTree on the rename TextBox fires once at ListBoxItem
        // creation (when IsRenaming is still false), not on later visibility
        // toggles — so we drive focus from this VM-raised event instead, after
        // the layout pass has made the TextBox actually visible.
        Dispatcher.UIThread.Post(() =>
        {
            var container = ThumbList.ContainerFromItem(item) as Control;
            if (container is null) return;
            var tb = container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (tb is null) return;
            tb.Focus();
            tb.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    private void OnFolderTreeSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedTreeItem is FolderTreeItem item)
            Dispatcher.UIThread.Post(() => CenterTreeContainer(item), DispatcherPriority.Loaded);
    }

    private void OnThumbDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is BrowserViewModel vm)
            vm.OpenSelected();
    }

    private async void OnOpenFolderClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        if (topLevel is Window window && window.DataContext is MainWindowViewModel mwvm)
            mwvm.Open(path);
        else if (DataContext is BrowserViewModel bvm)
            await bvm.LoadFolderAsync(path);
    }

    private void OnThumbWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (DataContext is not BrowserViewModel vm) return;

        const int step = 24;
        var oldWidth = vm.ThumbnailWidth;
        vm.ResizeThumbnailsBy(e.Delta.Y > 0 ? step : -step);
        if (vm.ThumbnailWidth != oldWidth && vm.SelectedIndex >= 0)
        {
            var idx = vm.SelectedIndex;
            Dispatcher.UIThread.Post(() => ThumbList.ScrollIntoView(idx), DispatcherPriority.Loaded);
        }
        e.Handled = true;
    }

    private void OnThumbListKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox) return;
        if (DataContext is not BrowserViewModel vm) return;
        if (vm.FilteredItems.Any(i => i.IsRenaming)) return;
        var total = vm.FilteredItems.Count;
        if (total == 0) return;

        int perRow = ItemsPerRow(vm);
        if (perRow <= 0) return;

        int idx = vm.SelectedIndex;
        if (idx < 0) idx = 0;
        int newIdx = idx;

        switch (e.Key)
        {
            case Key.Down:
                if (idx + perRow < total) newIdx = idx + perRow;
                break;
            case Key.Up:
                if (idx >= perRow) newIdx = idx - perRow;
                break;
            case Key.Right:
                if (idx < total - 1) newIdx = idx + 1;
                break;
            case Key.Left:
                if (idx > 0) newIdx = idx - 1;
                break;
            case Key.PageDown:
                {
                    int step = perRow * Math.Max(1, RowsPerPage(vm) - 1);
                    newIdx = Math.Min(idx + step, total - 1);
                }
                break;
            case Key.PageUp:
                {
                    int step = perRow * Math.Max(1, RowsPerPage(vm) - 1);
                    newIdx = Math.Max(idx - step, 0);
                }
                break;
            case Key.Home:
                newIdx = 0;
                break;
            case Key.End:
                newIdx = total - 1;
                break;
            default:
                return;
        }

        if (newIdx != idx)
        {
            vm.SelectedIndex = newIdx;
            ThumbList.ScrollIntoView(newIdx);
        }
        e.Handled = true;
    }

    private int ItemsPerRow(BrowserViewModel vm)
    {
        var w = ThumbList.Bounds.Width - 8; // listbox padding
        if (w <= 0) return 0;
        var cellW = vm.ThumbnailWidth + 4; // margin 2 each side
        return Math.Max(1, (int)Math.Floor(w / cellW));
    }

    private int RowsPerPage(BrowserViewModel vm)
    {
        var h = ThumbList.Bounds.Height - 8;
        if (h <= 0) return 1;
        var cellH = vm.ThumbnailHeight + 4;
        return Math.Max(1, (int)Math.Floor(h / cellH));
    }

    private void OnRenameTextBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.Tag is "wired") return;
        tb.AddHandler(InputElement.KeyDownEvent, OnRenameTunnelKeyDown, RoutingStrategies.Tunnel);
        tb.AddHandler(InputElement.KeyDownEvent, OnRenameBubbleKeyDown, RoutingStrategies.Bubble, handledEventsToo: true);
        tb.Tag = "wired";
        // Focus is driven by BrowserViewModel.RenameRequested → OnRenameRequested
        // because AttachedToVisualTree only fires once (at item creation, when
        // IsRenaming is still false).
    }

    private void OnRenameTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Up:
            case Key.Down:
            case Key.PageUp:
            case Key.PageDown:
                e.Handled = true;
                break;
        }
    }

    private void OnRenameBubbleKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ThumbnailItem item) return;
        if (DataContext is not BrowserViewModel vm) return;

        switch (e.Key)
        {
            case Key.Enter:
                vm.CommitRename(item);
                e.Handled = true;
                break;
            case Key.Escape:
                vm.CancelRename(item);
                e.Handled = true;
                break;
            case Key.Left:
            case Key.Right:
            case Key.Home:
            case Key.End:
                e.Handled = true;
                break;
        }
    }

    private void OnRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb) return;
        if (tb.DataContext is not ThumbnailItem item) return;
        if (!item.IsRenaming) return;
        if (DataContext is not BrowserViewModel vm) return;
        vm.CommitRename(item);
    }

    private void OnPathClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is BrowserViewModel vm) vm.BeginEditPath();
        e.Handled = true;
    }

    private void OnPathEditAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox tb) return;
        tb.Focus();
        tb.SelectAll();
    }

    private void OnPathEditKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not BrowserViewModel vm) return;
        if (e.Key == Key.Enter)
        {
            vm.TryCommitPathEdit();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.CancelPathEdit();
            e.Handled = true;
        }
    }

    private void OnPathEditLostFocus(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BrowserViewModel vm && vm.IsEditingPath)
            vm.CancelPathEdit();
    }

    private void OnTreeNodeFocused(FolderTreeItem item)
    {
        Dispatcher.UIThread.Post(() => CenterTreeContainer(item), DispatcherPriority.Loaded);
    }

    private void CenterTreeContainer(FolderTreeItem item)
    {
        var container = FolderTree.TreeContainerFromItem(item);
        if (container is null) return;

        var scroll = FolderTree.FindDescendantOfType<ScrollViewer>();
        if (scroll is null) { container.BringIntoView(); return; }

        var viewportH = scroll.Viewport.Height;
        var itemH = container.Bounds.Height;
        if (itemH <= 0 || viewportH <= itemH)
        {
            container.BringIntoView();
            return;
        }

        var pad = (viewportH - itemH) / 2;
        var rect = new Rect(0, -pad, container.Bounds.Width, itemH + 2 * pad);
        container.BringIntoView(rect);
    }

    private void OnThumbPropertiesClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not BrowserViewModel vm) return;
        if (sender is MenuItem mi && mi.DataContext is ThumbnailItem item)
        {
            var idx = vm.FilteredItems.IndexOf(item);
            if (idx >= 0) vm.SelectedIndex = idx;
        }
        vm.ShowExifPane = true;
    }

    private void OnCloseExifPaneClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BrowserViewModel vm) vm.ShowExifPane = false;
    }
}
