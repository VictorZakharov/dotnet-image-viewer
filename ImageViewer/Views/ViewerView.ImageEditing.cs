using System;
using System.Threading.Tasks;
using Avalonia;
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
        if (sender is not Control { Tag: string command }
            || !Enum.TryParse<SingleImageEditKind>(command, out var kind)) return;

        if (owner is MainWindow mainWindow
            && !await mainWindow.ResolvePendingImageEditsAsync(reloadCurrentImage: true))
            return;

        path = _viewModel.FilePath ?? path;

        var result = await new SingleImageEditDialog(path, kind)
            .ShowDialog<SingleImageEditResult?>(owner);
        if (result is null) return;

        if (owner.DataContext is MainWindowViewModel mainVm)
            mainVm.OpenEditedImage(result.OutputPath);
        else
            await _viewModel.ReloadFolderAndImageAsync(result.OutputPath);
    }

    private void OnCropToolClicked(object? sender, RoutedEventArgs e) => BeginCrop();

    internal bool BeginCrop()
    {
        if (_viewModel is not { Bitmap: { } bitmap } vm || !vm.BeginCrop()) return false;

        ViewerImage.ResetView();
        var width = bitmap.PixelSize.Width;
        var height = bitmap.PixelSize.Height;
        if (Math.Abs(vm.Rotation) % 180 != 0)
            (width, height) = (height, width);
        CropOverlay.Start(width, height);
        UpdateCropSelectionText();
        CropOverlay.Focus();
        return true;
    }

    internal void CancelCrop() => _viewModel?.CancelCrop();

    internal Task ApplyCropAsync() => _viewModel is { } vm
        ? vm.ApplyCropAsync(CropOverlay.Selection)
        : Task.CompletedTask;

    private void OnCropSelectionChanged(object? sender, EventArgs e) =>
        UpdateCropSelectionText();

    private void OnResetCropClicked(object? sender, RoutedEventArgs e) =>
        CropOverlay.SelectFullImage();

    private void OnCancelCropClicked(object? sender, RoutedEventArgs e) => CancelCrop();

    private async void OnApplyCropClicked(object? sender, RoutedEventArgs e) =>
        await ApplyCropAsync();

    private void UpdateCropSelectionText()
    {
        var crop = CropOverlay.Selection;
        CropSelectionText.Text =
            $"{crop.X:0}, {crop.Y:0}  ·  {crop.Width:0} × {crop.Height:0} px";
    }
}
