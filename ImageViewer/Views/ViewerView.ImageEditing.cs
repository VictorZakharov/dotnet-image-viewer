using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class ViewerView
{
    private async void OnEditImageClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is not { IsImage: true, FilePath: { } path }) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        if (sender is not MenuItem { Tag: string command }
            || !Enum.TryParse<SingleImageEditKind>(command, out var kind)) return;

        var result = await new SingleImageEditDialog(path, kind)
            .ShowDialog<SingleImageEditResult?>(owner);
        if (result is null) return;

        if (owner.DataContext is MainWindowViewModel mainWindow)
            mainWindow.OpenEditedImage(result.OutputPath);
        else
            await _viewModel.ReloadFolderAndImageAsync(result.OutputPath);
    }
}
