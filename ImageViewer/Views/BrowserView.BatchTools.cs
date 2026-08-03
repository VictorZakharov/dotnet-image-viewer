using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private async void OnBatchToolsClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm is not { } viewModel || GetOwnerWindow() is not { } owner) return;
        if (sender is MenuItem { DataContext: ThumbnailItem item })
            viewModel.SelectForContextMenu(item);
        var paths = viewModel.SelectedPaths.ToList();
        if (paths.Count == 0) return;

        var changed = await new BatchToolsWindow(paths, viewModel.CurrentFolder)
            .ShowDialog<bool>(owner);
        if (changed) await viewModel.ReloadAfterFileOperationAsync();
        FocusThumbnailGrid();
    }
}
