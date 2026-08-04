using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageMagick;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class SingleImageEditDialog : Window
{
    private readonly BatchProcessPlanner _planner = new();
    private readonly BatchImageProcessor _processor = new();
    private IReadOnlyList<BatchPreviewItem> _preview = Array.Empty<BatchPreviewItem>();
    private ConversionPreview? _conversionPreview;
    private CancellationTokenSource? _previewCancellation;
    private string _sourcePath = "";
    private SingleImageEditKind _kind;
    private bool _initialized;
    private bool _isBusy;
    private bool _allowClose;

    public SingleImageEditDialog()
    {
        InitializeComponent();
        Closing += OnClosing;
        Closed += OnClosed;
    }

    public SingleImageEditDialog(string sourcePath, SingleImageEditKind kind) : this()
    {
        _sourcePath = Path.GetFullPath(sourcePath);
        _kind = kind;
        ConfigureEditor();
        _initialized = true;
        UpdateOutputControls();
        SchedulePreview();
    }

    private void ConfigureEditor()
    {
        Title = $"{_kind.DisplayName()} image";
        HeadingText.Text = _kind.DisplayName();
        SuffixBox.Text = _kind.DefaultSuffix();
        ConfigureSourceDetails();

        RotationPanel.IsVisible = _kind is SingleImageEditKind.RotateLeft
            or SingleImageEditKind.RotateRight;
        ResizePanel.IsVisible = _kind == SingleImageEditKind.Resize;
        CropPanel.IsVisible = _kind == SingleImageEditKind.Crop;
        ConvertPanel.IsVisible = _kind == SingleImageEditKind.Convert;
        ConversionPreviewPanel.IsVisible = _kind == SingleImageEditKind.Convert;
        WatermarkPanel.IsVisible = _kind == SingleImageEditKind.Watermark;
        MetadataPanel.IsVisible = _kind == SingleImageEditKind.RemoveMetadata;

        RotationSummaryText.Text = _kind == SingleImageEditKind.RotateLeft
            ? "Rotate 90 degrees counter-clockwise"
            : "Rotate 90 degrees clockwise";
        CommandDescriptionText.Text = GetDescription(_kind);
        ConfigureLosslessRotation();
        if (_kind == SingleImageEditKind.Convert)
            FormatCombo.SelectedIndex = IsJpeg(_sourcePath) ? 1 : 0;
    }

    private void ConfigureSourceDetails()
    {
        SourceText.Text = Path.GetFileName(_sourcePath);
        ToolTip.SetTip(SourceText, _sourcePath);
        try
        {
            var info = new MagickImageInfo(_sourcePath);
            var width = checked((int)info.Width);
            var height = checked((int)info.Height);
            var metadata = ExifReader.Read(_sourcePath);
            if (metadata.OrientationRotation is 90 or 270)
                (width, height) = (height, width);

            SourceText.Text = $"{Path.GetFileName(_sourcePath)}  |  {width} x {height}";
            CropBoundsText.Text = $"Image bounds: {width} x {height}";
            CropWidthBox.Value = width;
            CropHeightBox.Value = height;
        }
        catch
        {
            CropBoundsText.Text = "Image bounds unavailable";
        }
    }

    private void ConfigureLosslessRotation()
    {
        if (!RotationPanel.IsVisible || !IsJpeg(_sourcePath)) return;
        var orientationNormalized = ExifReader.Read(_sourcePath).OrientationRotation == 0;
        LosslessJpegCheck.IsVisible = orientationNormalized && JpegLosslessTransformer.IsAvailable;
        LosslessJpegCheck.IsChecked = false;
    }

    private BatchProcessOptions ReadOptions()
    {
        var replace = OutputModeCombo.SelectedIndex == 1;
        return new BatchProcessOptions(
            replace ? BatchOutputMode.ReplaceOriginal : BatchOutputMode.BesideOriginal,
            "",
            SuffixBox.Text ?? "",
            replace ? BatchOverwritePolicy.Replace : BatchOverwritePolicy.AutoRename,
            (int)Math.Round(QualitySlider.Value),
            PreserveDatesCheck.IsChecked == true,
            PreserveIccCheck.IsChecked == true,
            MaxConcurrency: 1,
            [ReadOperation()]);
    }

    private BatchProcessOperation ReadOperation() => new()
    {
        Kind = ToBatchKind(_kind),
        IsEnabled = true,
        ResizeWidth = ToInt(ResizeWidthBox, 1920),
        ResizeHeight = ToInt(ResizeHeightBox, 1080),
        ResizeMode = (BatchResizeMode)Math.Clamp(ResizeModeCombo.SelectedIndex, 0, 1),
        AllowUpscale = AllowUpscaleCheck.IsChecked == true,
        OutputFormat = (BatchOutputFormat)Math.Clamp(FormatCombo.SelectedIndex + 1, 1, 4),
        RotationDegrees = _kind == SingleImageEditKind.RotateLeft ? 270 : 90,
        LosslessJpeg = LosslessJpegCheck.IsVisible && LosslessJpegCheck.IsChecked == true,
        CropX = ToInt(CropXBox, 0),
        CropY = ToInt(CropYBox, 0),
        CropWidth = ToInt(CropWidthBox, 1000),
        CropHeight = ToInt(CropHeightBox, 1000),
        WatermarkText = WatermarkTextBox.Text ?? "",
        WatermarkPosition = (BatchWatermarkPosition)Math.Clamp(
            WatermarkPositionCombo.SelectedIndex, 0, 4),
        WatermarkPointSize = ToInt(WatermarkSizeBox, 32),
        WatermarkOpacity = ToInt(WatermarkOpacityBox, 70),
        MetadataCleanupMode = (BatchMetadataCleanupMode)Math.Clamp(
            MetadataModeCombo.SelectedIndex, 0, 1)
    };

    private void OnOutputModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        UpdateOutputControls();
        SchedulePreview();
    }

    private void UpdateOutputControls()
    {
        var replace = OutputModeCombo.SelectedIndex == 1;
        SuffixPanel.IsVisible = !replace;
        DestructiveWarning.IsVisible = replace;
        ApplyButton.Content = replace ? "Replace original" : "Create edited copy";
    }

    private void OnOptionsChanged(object? sender, TextChangedEventArgs e) => OptionsChanged();
    private void OnOptionsChanged(object? sender, SelectionChangedEventArgs e) => OptionsChanged();
    private void OnOptionsChanged(object? sender, NumericUpDownValueChangedEventArgs e) => OptionsChanged();
    private void OnOptionsChanged(object? sender, RoutedEventArgs e) => OptionsChanged();

    private void OnQualityChanged(object? sender, RoutedEventArgs e)
    {
        QualityValueText.Text = Math.Round(QualitySlider.Value).ToString();
        OptionsChanged();
    }

    private void OptionsChanged()
    {
        if (!_initialized || _isBusy) return;
        SchedulePreview();
    }

    private static BatchProcessOperationKind ToBatchKind(SingleImageEditKind kind) => kind switch
    {
        SingleImageEditKind.RotateLeft or SingleImageEditKind.RotateRight =>
            BatchProcessOperationKind.Rotate,
        SingleImageEditKind.Resize => BatchProcessOperationKind.Resize,
        SingleImageEditKind.Crop => BatchProcessOperationKind.Crop,
        SingleImageEditKind.Convert => BatchProcessOperationKind.Convert,
        SingleImageEditKind.Watermark => BatchProcessOperationKind.Watermark,
        _ => BatchProcessOperationKind.MetadataCleanup
    };

    private static string GetDescription(SingleImageEditKind kind) => kind switch
    {
        SingleImageEditKind.Resize => "Scale this image to fit within a box, or stretch it to exact dimensions.",
        SingleImageEditKind.Crop => "Enter a pixel rectangle within the displayed, auto-oriented image bounds.",
        SingleImageEditKind.Convert => "Choose a format and quality, then compare the encoded result with the original before saving.",
        SingleImageEditKind.Watermark => "Add a text watermark at a chosen position.",
        SingleImageEditKind.RemoveMetadata => "Remove private camera and descriptive metadata from the output.",
        _ => "Rotate the image pixels and normalize its display orientation."
    };

    private static int ToInt(NumericUpDown input, int fallback) =>
        input.Value is { } value ? Decimal.ToInt32(value) : fallback;

    private static bool IsJpeg(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }
}
