using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class SingleImageEditDialog
{
    private async void SchedulePreview()
    {
        if (!_initialized || _isBusy) return;
        CancelPreview();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        PreviewProgress.IsVisible = true;
        PreviewStatusText.Text = "Checking the output...";
        ApplyButton.IsEnabled = false;
        try
        {
            await Task.Delay(140, cancellation.Token);
            var result = await _planner.BuildPreviewAsync(
                [_sourcePath], ReadOptions(), cancellation.Token);
            if (!ReferenceEquals(_previewCancellation, cancellation)) return;
            _preview = result;
            UpdatePreviewSummary();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_previewCancellation, cancellation))
                PreviewStatusText.Text = $"Could not validate this edit: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_previewCancellation, cancellation))
            {
                _previewCancellation = null;
                PreviewProgress.IsVisible = false;
                cancellation.Dispose();
            }
        }
    }

    private void UpdatePreviewSummary()
    {
        var item = _preview.SingleOrDefault();
        if (item is null)
        {
            PreviewStatusText.Text = "The source image is unavailable.";
            ApplyButton.IsEnabled = false;
            return;
        }

        if (item.Status == BatchPreviewStatus.Ready)
        {
            PreviewStatusText.Text = $"Output: {item.TargetName}  |  {item.Message}";
            ApplyButton.IsEnabled = true;
            return;
        }

        PreviewStatusText.Text = $"{item.StatusLabel}: {item.Message}";
        ApplyButton.IsEnabled = false;
    }

    private void CancelPreview()
    {
        var preview = _previewCancellation;
        _previewCancellation = null;
        if (preview is null) return;
        preview.Cancel();
        preview.Dispose();
    }
}
