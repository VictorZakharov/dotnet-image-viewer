using System.Threading.Tasks;
using Avalonia.Controls;
using ImageViewer.Models;
using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class MainWindow
{
    private bool _allowPendingEditClose;
    private bool _pendingEditPromptOpen;

    internal async Task<bool> ResolvePendingImageEditsAsync(bool reloadCurrentImage)
    {
        if (DataContext is not MainWindowViewModel { IsViewerMode: true } main
            || !main.ViewerVM.HasPendingEdits)
            return true;
        if (_pendingEditPromptOpen) return false;

        var viewer = main.ViewerVM;
        var path = viewer.FilePath;
        if (path is null) return true;

        viewer.StopSlideshow();
        _pendingEditPromptOpen = true;
        try
        {
            var choice = await PendingImageEditsDialog.ShowAsync(this, path);
            switch (choice)
            {
                case PendingImageEditChoice.Save:
                    if (!await viewer.SavePendingEditsAsync()) return false;
                    break;
                case PendingImageEditChoice.Discard:
                    viewer.DiscardPendingEdits();
                    break;
                default:
                    return false;
            }

            if (reloadCurrentImage && FileSystemPath.Equals(viewer.FilePath, path))
                await viewer.ReloadFolderAndImageAsync(path);
            return true;
        }
        finally
        {
            _pendingEditPromptOpen = false;
        }
    }

    internal async Task<bool> OpenCanvasImageToolAsync(
        string path,
        SingleImageEditKind kind)
    {
        if (DataContext is not MainWindowViewModel main) return false;
        if (!await main.OpenImageForCanvasEditAsync(path)) return false;

        switch (kind)
        {
            case SingleImageEditKind.RotateLeft:
                main.ViewerVM.RotateLeftCommand.Execute(null);
                return true;
            case SingleImageEditKind.RotateRight:
                main.ViewerVM.RotateRightCommand.Execute(null);
                return true;
            case SingleImageEditKind.Crop:
                return _viewerView?.BeginCrop() == true;
            default:
                return false;
        }
    }

    private async Task LeaveViewerAsync()
    {
        if (DataContext is not MainWindowViewModel { IsViewerMode: true } main) return;
        if (!await ResolvePendingImageEditsAsync(reloadCurrentImage: false)) return;
        main.ToggleModeCommand.Execute(null);
    }

    private async Task NavigateViewerAsync(bool next)
    {
        if (DataContext is not MainWindowViewModel { IsViewerMode: true } main) return;
        if (!await ResolvePendingImageEditsAsync(reloadCurrentImage: false)) return;
        if (next) await main.ViewerVM.NextCommand.ExecuteAsync(null);
        else await main.ViewerVM.PreviousCommand.ExecuteAsync(null);
    }

    private bool DeferCloseForPendingEdits(WindowClosingEventArgs e)
    {
        if (_allowPendingEditClose
            || DataContext is not MainWindowViewModel { IsViewerMode: true } main)
            return false;

        if (main.ViewerVM.IsCropping)
            main.ViewerVM.CancelCrop();
        if (!main.ViewerVM.HasPendingEdits) return false;

        e.Cancel = true;
        if (!_pendingEditPromptOpen)
            _ = ResolvePendingEditsAndCloseAsync();
        return true;
    }

    private async Task ResolvePendingEditsAndCloseAsync()
    {
        if (!await ResolvePendingImageEditsAsync(reloadCurrentImage: false)) return;
        _allowPendingEditClose = true;
        Close();
    }
}
