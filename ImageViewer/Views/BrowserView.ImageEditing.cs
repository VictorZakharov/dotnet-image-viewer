using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private async void OnEditImageClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm is not { } viewModel || GetOwnerWindow() is not { } owner) return;
        if (sender is not MenuItem
            {
                DataContext: ThumbnailItem { IsImage: true } item,
                Tag: string command
            }) return;
        if (!Enum.TryParse<SingleImageEditKind>(command, out var kind)) return;

        viewModel.SelectForContextMenu(item);
        if (kind is SingleImageEditKind.RotateLeft
            or SingleImageEditKind.RotateRight
            or SingleImageEditKind.Crop)
        {
            if (owner is MainWindow mainWindow
                && await mainWindow.OpenCanvasImageToolAsync(item.Path, kind))
                return;

            viewModel.ReportFileOperation("The image could not be opened for editing.");
            FocusThumbnailGrid();
            return;
        }

        try
        {
            var result = await new SingleImageEditDialog(item.Path, kind)
                .ShowDialog<SingleImageEditResult?>(owner);
            if (result is null) return;

            viewModel.ApplyImageEditResult(result);
            viewModel.ReportFileOperation(
                $"{result.Kind.DisplayName()} complete | {result.OutputName}");
            if (viewModel.SelectedIndex >= 0)
                ScrollIndexIntoView(viewModel.SelectedIndex);
        }
        catch (Exception ex)
        {
            viewModel.ReportFileOperation($"Image edit could not open: {ex.Message}");
        }
        finally
        {
            FocusThumbnailGrid();
        }
    }
}
