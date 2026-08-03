using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class BatchProcessView
{
    private async void OnProcess(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        ProcessButton.IsEnabled = false;
        try
        {
            var options = ReadOptions();
            _preview = await _planner.BuildPreviewAsync(_sources, options, default);
            PreviewList.ItemsSource = _preview;
            UpdatePreviewSummary();
            var ready = _preview.Where(item => item.Status == BatchPreviewStatus.Ready).ToList();
            if (ready.Count == 0 || _preview.Any(item => item.IsBlocking)) return;

            var destructive = options.OutputMode == BatchOutputMode.ReplaceOriginal
                              || options.OverwritePolicy == BatchOverwritePolicy.Replace;
            var warning = destructive
                ? "This recipe can replace originals or existing outputs. Each image is written to a temporary file and closed before an atomic commit; canceling leaves completed outputs valid."
                : "Original images are retained. Each output is written to a temporary file and closed before commit; canceling leaves completed outputs valid.";
            var confirmed = await BatchConfirmationDialog.ConfirmAsync(
                owner,
                $"Process {ready.Count} image{(ready.Count == 1 ? "" : "s")}?",
                warning,
                destructive ? "Process with replacements" : "Create outputs",
                ready.Select(item => item.SourcePath).ToList());
            if (!confirmed) return;

            var progressDialog = new FileOperationProgressDialog("Processing images");
            progressDialog.Show(owner);
            BatchOperationResult result;
            try
            {
                result = await _processor.ExecuteAsync(
                    _preview,
                    options,
                    new Progress<FileOperationProgress>(progressDialog.Report),
                    progressDialog.CancellationToken);
            }
            finally
            {
                progressDialog.Finish();
            }

            await new FileOperationResultDialog("Batch processing complete", result).ShowDialog(owner);
            if (result.Successful.Count > 0) Completed?.Invoke();
        }
        catch (Exception ex)
        {
            PreviewStatusText.Text = $"Processing could not start: {ex.Message}";
        }
        finally
        {
            UpdatePreviewSummary();
        }
    }
}
