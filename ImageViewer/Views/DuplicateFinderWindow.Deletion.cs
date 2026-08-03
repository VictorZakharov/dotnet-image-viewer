using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class DuplicateFinderWindow
{
    private readonly BulkFileOperationService _fileOperations = new();
    private bool _fileOperationRunning;

    private void OnClearSelection(object? sender, RoutedEventArgs e)
    {
        foreach (var group in _viewModel.Groups) group.ClearSelection();
    }

    private async void OnDeleteSelected(object? sender, RoutedEventArgs e)
    {
        if (_fileOperationRunning) return;
        var paths = _viewModel.SelectedPaths;
        if (paths.Count == 0) return;
        if (!_viewModel.SelectionLeavesOnePerGroup)
        {
            _viewModel.StatusText =
                "Each group must keep at least one file. Clear a selection before deleting.";
            return;
        }
        if (!await FileDeleteConfirmationDialog.ConfirmAsync(this, paths)) return;

        _fileOperationRunning = true;
        var progressDialog = new FileOperationProgressDialog(
            "Moving duplicate images to the Recycle Bin");
        progressDialog.Show(this);
        FileOperationResult result;
        try
        {
            result = await _fileOperations.ExecuteAsync(
                new FileOperationRequest(FileOperationKind.Delete, paths),
                (_, _) => Task.FromResult(FileCollisionChoice.Skip),
                new Progress<FileOperationProgress>(progressDialog.Report),
                progressDialog.CancellationToken);
        }
        catch (Exception ex)
        {
            result = new FileOperationResult(
                FileOperationKind.Delete,
                Array.Empty<FileOperationSuccess>(),
                Array.Empty<string>(),
                [new FileOperationFailure("Duplicate selection", null, ex.Message)],
                IsCanceled: false);
        }
        finally
        {
            progressDialog.Finish();
            _fileOperationRunning = false;
        }

        var deleted = result.Successful.Select(item => item.SourcePath).ToList();
        if (deleted.Count > 0) _viewModel.RemoveDeletedPaths(deleted);
        if (result.Failures.Count > 0 || result.IsCanceled)
            await new FileOperationResultDialog(result).ShowDialog(this);
    }
}
