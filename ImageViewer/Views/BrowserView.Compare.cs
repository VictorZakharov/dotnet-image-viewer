using System;
using System.Linq;
using Avalonia.Interactivity;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private async void OnCompareSelectedClicked(object? sender, RoutedEventArgs e)
    {
        if (_vm is not { } viewModel || GetOwnerWindow() is not { } owner) return;
        var selectedPaths = viewModel.SelectedPaths.ToList();
        if (selectedPaths.Count is < 2 or > 4)
        {
            viewModel.ReportFileOperation(
                $"Compare needs 2–4 selected images; {selectedPaths.Count} selected.");
            return;
        }
        var unsupported = selectedPaths.Where(path => !MediaFileTypes.IsImage(path)).ToList();
        if (unsupported.Count > 0)
        {
            viewModel.ReportFileOperation(
                "Compare supports images only; remove folders or videos from the selection.");
            return;
        }

        var focusedPath = viewModel.SelectedPath;
        var compare = new ImageCompareWindow(selectedPaths);
        await compare.ShowDialog(owner);
        if (compare.Result is not { } result) return;

        var remainingSelection = selectedPaths
            .Except(result.DeletedPaths, FileSystemPath.Comparer)
            .ToList();
        if (result.DeletedPaths.Count > 0)
            await viewModel.ReloadAfterFileOperationAsync();
        viewModel.ApplyCompareDecisions(result.Decisions);
        viewModel.RestoreSelectionByPaths(remainingSelection, focusedPath);
        viewModel.ReportFileOperation(result.DeletedPaths.Count > 0
            ? $"Compare closed · {result.DeletedPaths.Count} moved to the {FileOperations.TrashDisplayName}"
            : "Compare closed · selection restored");
    }
}
