using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class BatchProcessView : UserControl
{
    private readonly BatchProcessPlanner _planner = new();
    private readonly BatchImageProcessor _processor = new();
    private readonly BatchPresetStore _presetStore = new();
    private IReadOnlyList<string> _sources = Array.Empty<string>();
    private IReadOnlyList<BatchPreviewItem> _preview = Array.Empty<BatchPreviewItem>();
    private CancellationTokenSource? _previewCancellation;
    private bool _initialized;

    public event Action? Completed;

    public BatchProcessView()
    {
        InitializeComponent();
        ConcurrencyBox.Value = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
        OperationEditor.OptionsChanged += SchedulePreview;
    }

    public void Initialize(IReadOnlyList<string> sources, string? currentFolder)
    {
        _sources = sources.Where(MediaFileTypes.IsImage).ToList();
        var baseFolder = !string.IsNullOrEmpty(currentFolder)
            ? currentFolder
            : _sources.Select(Path.GetDirectoryName).FirstOrDefault(path => !string.IsNullOrEmpty(path));
        DestinationBox.Text = string.IsNullOrEmpty(baseFolder)
            ? ""
            : Path.Combine(baseFolder, "Processed");
        _initialized = true;
        RefreshPresetNames();
        UpdateOutputControls();
        SchedulePreview();
    }

    private BatchProcessOptions ReadOptions() => new(
        (BatchOutputMode)Math.Clamp(OutputModeCombo.SelectedIndex, 0, 2),
        DestinationBox.Text ?? "",
        SuffixBox.Text ?? "",
        (BatchOverwritePolicy)Math.Clamp(OverwriteCombo.SelectedIndex, 0, 2),
        ToInt(QualityBox, 90),
        PreserveDatesCheck.IsChecked == true,
        PreserveIccCheck.IsChecked == true,
        ToInt(ConcurrencyBox, 2),
        OperationEditor.Operations);

    private void ApplyOptions(BatchProcessOptions options)
    {
        _initialized = false;
        OutputModeCombo.SelectedIndex = (int)options.OutputMode;
        DestinationBox.Text = options.DestinationFolder;
        SuffixBox.Text = options.Suffix;
        OverwriteCombo.SelectedIndex = (int)options.OverwritePolicy;
        QualityBox.Value = options.Quality;
        PreserveDatesCheck.IsChecked = options.PreserveFileDates;
        PreserveIccCheck.IsChecked = options.PreserveIccProfile;
        ConcurrencyBox.Value = Math.Clamp(options.MaxConcurrency, 1, 8);
        OperationEditor.SetOperations(options.Operations);
        _initialized = true;
        UpdateOutputControls();
        SchedulePreview();
    }

    private void OnOutputModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateOutputControls();
        SchedulePreview();
    }

    private void UpdateOutputControls()
    {
        var mode = (BatchOutputMode)Math.Clamp(OutputModeCombo.SelectedIndex, 0, 2);
        DestinationPanel.IsVisible = mode == BatchOutputMode.NewFolder;
        SuffixPanel.IsVisible = mode == BatchOutputMode.BesideOriginal;
        var overwrite = (BatchOverwritePolicy)Math.Clamp(OverwriteCombo.SelectedIndex, 0, 2);
        DestructiveWarning.IsVisible = mode == BatchOutputMode.ReplaceOriginal
                                       || overwrite == BatchOverwritePolicy.Replace;
        DestructiveWarningText.Text = mode == BatchOutputMode.ReplaceOriginal
            ? "Replace originals is destructive. A successfully written and closed output is committed before the original is replaced or removed."
            : "Replace overwrites existing output files. A final confirmation is required.";
    }

    private void OnOptionsChanged(object? sender, TextChangedEventArgs e) => OptionsChanged();
    private void OnOptionsChanged(object? sender, SelectionChangedEventArgs e) => OptionsChanged();
    private void OnOptionsChanged(object? sender, NumericUpDownValueChangedEventArgs e) =>
        OptionsChanged();
    private void OnOptionsChanged(object? sender, RoutedEventArgs e) => OptionsChanged();

    private void OptionsChanged()
    {
        UpdateOutputControls();
        SchedulePreview();
    }

    private async void OnBrowseDestination(object? sender, RoutedEventArgs e)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner) return;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Choose batch output folder",
            AllowMultiple = false
        });
        var path = folders.FirstOrDefault()?.TryGetLocalPath();
        if (!string.IsNullOrEmpty(path)) DestinationBox.Text = path;
    }

    private async void SchedulePreview()
    {
        if (!_initialized) return;
        _previewCancellation?.Cancel();
        _previewCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _previewCancellation = cancellation;
        PreviewProgress.IsVisible = true;
        PreviewStatusText.Text = "Inspecting images, dimensions, outputs, and operation support...";
        ProcessButton.IsEnabled = false;
        try
        {
            await Task.Delay(140, cancellation.Token);
            var result = await _planner.BuildPreviewAsync(_sources, ReadOptions(), cancellation.Token);
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
        var skipped = _preview.Count(item => item.Status == BatchPreviewStatus.WillSkip);
        var blocking = _preview.Count(item => item.IsBlocking);
        PreviewStatusText.Text = _sources.Count == 0
            ? "The selection contains no supported images. Rename is still available on the first tab."
            : $"{ready} ready · {skipped} will skip · {blocking} need attention";
        ProcessButton.IsEnabled = ready > 0 && blocking == 0;
    }

    private static int ToInt(NumericUpDown input, int fallback) =>
        input.Value is { } value ? Decimal.ToInt32(value) : fallback;
}
