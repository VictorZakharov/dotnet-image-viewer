using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class SingleImageEditDialog
{
    private async void SchedulePreview()
    {
        if (!_initialized || _isBusy) return;
        CancelPreview();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        ClearConversionPreview();
        PreviewProgress.IsVisible = true;
        PreviewStatusText.Text = "Checking the output...";
        ApplyButton.IsEnabled = false;
        try
        {
            await Task.Delay(140, cancellation.Token);
            var options = ReadOptions();
            var result = await _planner.BuildPreviewAsync(
                [_sourcePath], options, cancellation.Token);
            if (!ReferenceEquals(_previewCancellation, cancellation)) return;
            _preview = result;

            if (_kind == SingleImageEditKind.Convert
                && result.SingleOrDefault()?.Status == BatchPreviewStatus.Ready)
            {
                var convert = options.Operations.Single(operation =>
                    operation.Kind == BatchProcessOperationKind.Convert);
                var encoded = await ImageConversionPreviewService.CreateAsync(
                    _sourcePath,
                    convert.OutputFormat,
                    options.Quality,
                    cancellation.Token);
                if (!ReferenceEquals(_previewCancellation, cancellation)) return;
                _conversionPreview = encoded;
            }
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
            if (_kind == SingleImageEditKind.Convert && _conversionPreview is { } conversion)
            {
                ConversionSizeText.Text = "Approx. output size: " +
                    FileSizeDisplay.DescribeChange(
                        conversion.SourceSizeBytes,
                        conversion.ConvertedSizeBytes);
                CompareConversionButton.IsEnabled = true;
            }
            return;
        }

        PreviewStatusText.Text = $"{item.StatusLabel}: {item.Message}";
        ApplyButton.IsEnabled = false;
    }

    private void ClearConversionPreview()
    {
        _conversionPreview = null;
        CompareConversionButton.IsEnabled = false;
        if (_kind == SingleImageEditKind.Convert)
            ConversionSizeText.Text = "Generating an encoded preview...";
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
