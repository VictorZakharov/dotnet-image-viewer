using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class BatchRenameView
{
    private async void OnApplyRename(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        ApplyRenameButton.IsEnabled = false;
        try
        {
            _preview = await _service.BuildPreviewAsync(
                _sources,
                ReadOptions(),
                default);
            PreviewList.ItemsSource = _preview;
            UpdatePreviewSummary();
            var ready = _preview.Where(item => item.Status == BatchPreviewStatus.Ready).ToList();
            if (ready.Count == 0 || _preview.Any(item => item.IsBlocking)) return;

            var confirmed = await BatchConfirmationDialog.ConfirmAsync(
                owner,
                $"Rename {ready.Count} item{(ready.Count == 1 ? "" : "s")}?",
                "This changes the original paths in place. The full preview has been validated, " +
                "and each folder is committed as one rollback-safe transaction.",
                "Rename originals",
                ready.Select(item => item.SourcePath).ToList());
            if (!confirmed) return;

            var progressDialog = new FileOperationProgressDialog("Renaming items");
            progressDialog.Show(owner);
            BatchOperationResult result;
            try
            {
                result = await _service.ExecuteAsync(
                    _preview,
                    new Progress<FileOperationProgress>(progressDialog.Report),
                    progressDialog.CancellationToken);
            }
            finally
            {
                progressDialog.Finish();
            }

            await new FileOperationResultDialog("Batch rename complete", result).ShowDialog(owner);
            if (result.Successful.Count > 0) Completed?.Invoke();
        }
        catch (Exception ex)
        {
            PreviewStatusText.Text = $"Rename could not start: {ex.Message}";
        }
        finally
        {
            UpdatePreviewSummary();
        }
    }
}
