using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class SingleImageEditDialog
{
    private async void OnApply(object? sender, RoutedEventArgs e) => await ApplyAsync();

    private async Task ApplyAsync()
    {
        if (_isBusy) return;
        CancelPreview();
        SetBusy(true);
        try
        {
            var options = ReadOptions();
            _preview = await _planner.BuildPreviewAsync([_sourcePath], options, default);
            var item = _preview.SingleOrDefault();
            if (item is null || item.Status != BatchPreviewStatus.Ready)
            {
                UpdatePreviewSummary();
                return;
            }

            if (options.OutputMode == BatchOutputMode.ReplaceOriginal)
            {
                var confirmed = await BatchConfirmationDialog.ConfirmAsync(
                    this,
                    $"Replace {Path.GetFileName(_sourcePath)}?",
                    "The edited image is written and closed before the original is replaced. " +
                    "A format change can also replace an existing target file. " +
                    "ImageViewer cannot undo this operation.",
                    "Replace original",
                    [_sourcePath]);
                if (!confirmed) return;
            }

            PreviewProgress.IsVisible = true;
            PreviewStatusText.Text = $"Editing {Path.GetFileName(_sourcePath)}...";
            var result = await _processor.ExecuteAsync(
                _preview,
                options,
                progress: null,
                default);
            var success = result.Successful.SingleOrDefault();
            if (success is null)
            {
                PreviewStatusText.Text = result.Failures.FirstOrDefault() is { } failure
                    ? $"Edit failed: {failure.Error}"
                    : "The image was not changed.";
                return;
            }

            _allowClose = true;
            Close(new SingleImageEditResult(
                _sourcePath,
                success.TargetPath,
                options.OutputMode == BatchOutputMode.ReplaceOriginal,
                _kind));
        }
        catch (Exception ex)
        {
            PreviewStatusText.Text = $"Edit failed: {ex.Message}";
        }
        finally
        {
            PreviewProgress.IsVisible = false;
            SetBusy(false);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (_isBusy) return;
        _allowClose = true;
        Close(null);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_isBusy && !_allowClose)
        {
            e.Cancel = true;
            return;
        }

        CancelPreview();
    }

    private void OnClosed(object? sender, EventArgs e) => CancelPreview();

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        EditorScroll.IsEnabled = !busy;
        CancelButton.IsEnabled = !busy;
        ApplyButton.IsEnabled = !busy && _preview.Any(item =>
            item.Status == BatchPreviewStatus.Ready);
    }
}
