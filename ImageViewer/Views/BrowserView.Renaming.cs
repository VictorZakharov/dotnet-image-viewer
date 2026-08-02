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
    private void OnThumbnailTitlePressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        if (sender is not TextBlock { DataContext: ThumbnailItem item }) return;
        if (item.IsFolder || DataContext is not BrowserViewModel vm) return;

        vm.SelectItem(item);
        vm.BeginRenameSelected();
        e.Handled = true;
    }

    private void OnRenameRequested(ThumbnailItem item)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var index = _vm?.FilteredItems.IndexOf(item) ?? -1;
            if (index < 0) return;

            var container = ThumbRepeater.TryGetElement(index)
                ?? ThumbRepeater.GetOrCreateElement(index);
            container.BringIntoView();
            var textBox = container.GetVisualDescendants().OfType<TextBox>().FirstOrDefault();
            if (textBox is null) return;
            textBox.Focus();
            textBox.SelectAll();
        }, DispatcherPriority.Loaded);
    }

    private void OnRenameTextBoxAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is "wired") return;
        textBox.AddHandler(
            InputElement.KeyDownEvent,
            OnRenameTunnelKeyDown,
            RoutingStrategies.Tunnel);
        textBox.AddHandler(
            InputElement.KeyDownEvent,
            OnRenameBubbleKeyDown,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
        textBox.Tag = "wired";
    }

    private void OnRenameTunnelKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Up or Key.Down or Key.PageUp or Key.PageDown)
            e.Handled = true;
    }

    private void OnRenameBubbleKeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: ThumbnailItem item }) return;
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
        if (sender is not TextBox { DataContext: ThumbnailItem item }) return;
        if (!item.IsRenaming || DataContext is not BrowserViewModel vm) return;
        vm.CommitRename(item);
    }
}
