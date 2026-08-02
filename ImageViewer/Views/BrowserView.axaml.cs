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
        InitializeThumbnailGrid();
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
            DetachThumbnailGrid(_vm);
        }
        _vm = DataContext as BrowserViewModel;
        if (_vm is not null)
        {
            _vm.TreeNodeFocused += OnTreeNodeFocused;
            AttachThumbnailGrid(_vm);
        }
    }

    private void OnFolderTreeSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        if (_vm?.SelectedTreeItem is FolderTreeItem item)
            Dispatcher.UIThread.Post(() => CenterTreeContainer(item), DispatcherPriority.Loaded);
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

    private void OnCloseExifPaneClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is BrowserViewModel vm) vm.ShowExifPane = false;
    }
}
