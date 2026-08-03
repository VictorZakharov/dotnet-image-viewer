using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class ImageCompareWindow
{
    private readonly BulkFileOperationService _compareFileOperations = new();
    private bool _deleteRunning;

    private async void OnDeleteRejected(object? sender, RoutedEventArgs e) =>
        await DeleteRejectedAsync();

    private async Task DeleteRejectedAsync()
    {
        if (_deleteRunning) return;
        var paths = _viewModel.Candidates
            .Where(candidate => candidate.Mark == CompareMark.Reject)
            .Select(candidate => candidate.Path)
            .ToList();
        if (paths.Count == 0)
        {
            _viewModel.StatusText = "Mark one or more candidates Reject before deleting.";
            return;
        }
        if (paths.Count == _viewModel.Candidates.Count)
        {
            _viewModel.StatusText = "Keep at least one comparison candidate before deleting.";
            return;
        }
        if (!await FileDeleteConfirmationDialog.ConfirmAsync(this, paths)) return;

        _deleteRunning = true;
        var progressDialog = new FileOperationProgressDialog(
            "Moving rejected images to the Recycle Bin");
        progressDialog.Show(this);
        FileOperationResult result;
        try
        {
            result = await _compareFileOperations.ExecuteAsync(
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
                [new FileOperationFailure("Rejected images", null, ex.Message)],
                IsCanceled: false);
        }
        finally
        {
            progressDialog.Finish();
            _deleteRunning = false;
        }

        var deleted = result.Successful.Select(success => success.SourcePath).ToList();
        foreach (var candidate in _viewModel.Candidates
                     .Where(candidate => deleted.Contains(
                         candidate.Path, StringComparer.OrdinalIgnoreCase)).ToList())
            CancelCandidateLoad(candidate);
        if (deleted.Count > 0)
        {
            _viewModel.RemoveDeleted(deleted);
            _viewModel.StatusText =
                $"Moved {deleted.Count} rejected image{(deleted.Count == 1 ? "" : "s")} " +
                "to the Recycle Bin.";
            if (!_viewModel.CanBlink) ExitBlink();
        }
        if (result.Failures.Count > 0 || result.IsCanceled)
            await new FileOperationResultDialog(result).ShowDialog(this);
    }
}
