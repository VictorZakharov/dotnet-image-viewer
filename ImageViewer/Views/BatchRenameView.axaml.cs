using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class BatchRenameView : UserControl
{
    private readonly BatchRenameService _service = new();
    private readonly BatchPresetStore _presetStore = new();
    private IReadOnlyList<string> _sources = Array.Empty<string>();
    private IReadOnlyList<BatchPreviewItem> _preview = Array.Empty<BatchPreviewItem>();
    private CancellationTokenSource? _previewCancellation;
    private bool _initialized;

    public event Action? Completed;

    public BatchRenameView()
    {
        InitializeComponent();
    }

    public void Initialize(IReadOnlyList<string> sources)
    {
        _sources = sources;
        _initialized = true;
        RefreshPresetNames();
        SchedulePreview();
    }

    private BatchRenameOptions ReadOptions() => new(
        TemplateBox.Text ?? "",
        SearchBox.Text ?? "",
        ReplaceBox.Text ?? "",
        MatchCaseCheck.IsChecked == true,
        (BatchNameCase)Math.Clamp(CaseCombo.SelectedIndex, 0, 3),
        Decimal.ToInt32(CounterStartBox.Value ?? 1),
        Decimal.ToInt32(CounterPaddingBox.Value ?? 3));

    private void ApplyOptions(BatchRenameOptions options)
    {
        _initialized = false;
        TemplateBox.Text = options.Template;
        SearchBox.Text = options.SearchText;
        ReplaceBox.Text = options.ReplaceText;
        MatchCaseCheck.IsChecked = options.MatchCase;
        CaseCombo.SelectedIndex = (int)options.CaseMode;
        CounterStartBox.Value = options.CounterStart;
        CounterPaddingBox.Value = options.CounterPadding;
        _initialized = true;
        SchedulePreview();
    }

    private void OnOptionsChanged(object? sender, TextChangedEventArgs e) => SchedulePreview();
    private void OnOptionsChanged(object? sender, SelectionChangedEventArgs e) => SchedulePreview();
    private void OnOptionsChanged(object? sender, NumericUpDownValueChangedEventArgs e) =>
        SchedulePreview();
    private void OnOptionsChanged(object? sender, RoutedEventArgs e) => SchedulePreview();

    private void OnInsertToken(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string token }) return;
        var text = TemplateBox.Text ?? "";
        var start = Math.Clamp(TemplateBox.SelectionStart, 0, text.Length);
        var end = Math.Clamp(TemplateBox.SelectionEnd, start, text.Length);
        TemplateBox.Text = text[..start] + token + text[end..];
        TemplateBox.CaretIndex = start + token.Length;
        TemplateBox.Focus();
    }

    private async void SchedulePreview()
    {
        if (!_initialized) return;
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        PreviewProgress.IsVisible = true;
        PreviewStatusText.Text = "Validating generated paths and metadata...";
        ApplyRenameButton.IsEnabled = false;
        try
        {
            await Task.Delay(120, cancellation.Token);
            var result = await _service.BuildPreviewAsync(
                _sources,
                ReadOptions(),
                cancellation.Token);
            if (!ReferenceEquals(_previewCancellation, cancellation)) return;
            _preview = result;
            PreviewList.ItemsSource = result;
            UpdatePreviewSummary();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            PreviewStatusText.Text = $"Preview failed: {ex.Message}";
        }
        finally
        {
            if (ReferenceEquals(_previewCancellation, cancellation))
                PreviewProgress.IsVisible = false;
        }
    }

    private void UpdatePreviewSummary()
    {
        var ready = _preview.Count(item => item.Status == BatchPreviewStatus.Ready);
        var unchanged = _preview.Count(item => item.Status == BatchPreviewStatus.Unchanged);
        var blocking = _preview.Count(item => item.IsBlocking);
        PreviewStatusText.Text = $"{ready} ready · {unchanged} unchanged · {blocking} need attention";
        ApplyRenameButton.IsEnabled = ready > 0 && blocking == 0;
    }
}
