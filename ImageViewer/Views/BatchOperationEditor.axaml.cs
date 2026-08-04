using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class BatchOperationEditor : UserControl
{
    private List<BatchProcessOperation> _operations = BatchProcessOperation.CreateDefaults();
    private bool _updating = true;

    public event Action? OptionsChanged;

    public IReadOnlyList<BatchProcessOperation> Operations
    {
        get
        {
            ReadControls();
            return _operations.Select(operation => operation.Clone()).ToList();
        }
    }

    public BatchOperationEditor()
    {
        InitializeComponent();
        OperationList.ItemsSource = _operations;
        OperationList.SelectedIndex = 0;
        _updating = false;
    }

    public void SetOperations(IEnumerable<BatchProcessOperation> operations)
    {
        _updating = true;
        _operations = operations.Select(operation => operation.Clone()).ToList();
        foreach (var fallback in BatchProcessOperation.CreateDefaults())
            if (_operations.All(operation => operation.Kind != fallback.Kind))
                _operations.Add(fallback);
        PopulateControls();
        RefreshOperationList(0);
        _updating = false;
        OptionsChanged?.Invoke();
    }

    private void OnOperationEnabledChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox { DataContext: BatchProcessOperation operation } checkBox)
            operation.IsEnabled = checkBox.IsChecked == true;
        RaiseChanged();
    }

    private void OnSettingsChanged(object? sender, TextChangedEventArgs e) => RaiseChanged();
    private void OnSettingsChanged(object? sender, SelectionChangedEventArgs e) => RaiseChanged();
    private void OnSettingsChanged(object? sender, NumericUpDownValueChangedEventArgs e) =>
        RaiseChanged();
    private void OnSettingsChanged(object? sender, RoutedEventArgs e) => RaiseChanged();

    private void RaiseChanged()
    {
        if (_updating) return;
        ReadControls();
        OptionsChanged?.Invoke();
    }

    private void OnMoveUp(object? sender, RoutedEventArgs e) => MoveSelected(-1);
    private void OnMoveDown(object? sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int delta)
    {
        var index = OperationList.SelectedIndex;
        var target = index + delta;
        if (index < 0 || target < 0 || target >= _operations.Count) return;
        (_operations[index], _operations[target]) = (_operations[target], _operations[index]);
        RefreshOperationList(target);
        OptionsChanged?.Invoke();
    }

    private void RefreshOperationList(int selectedIndex)
    {
        OperationList.ItemsSource = null;
        OperationList.ItemsSource = _operations;
        OperationList.SelectedIndex = selectedIndex;
    }

    private void PopulateControls()
    {
        var resize = Find(BatchProcessOperationKind.Resize);
        ResizeWidthBox.Value = resize.ResizeWidth;
        ResizeHeightBox.Value = resize.ResizeHeight;
        ResizeModeCombo.SelectedIndex = (int)resize.ResizeMode;
        AllowUpscaleCheck.IsChecked = resize.AllowUpscale;

        FormatCombo.SelectedIndex = (int)Find(BatchProcessOperationKind.Convert).OutputFormat;
        var rotate = Find(BatchProcessOperationKind.Rotate);
        RotationCombo.SelectedIndex = rotate.RotationDegrees switch { 180 => 1, 270 => 2, _ => 0 };
        LosslessJpegCheck.IsChecked = rotate.LosslessJpeg;

        var crop = Find(BatchProcessOperationKind.Crop);
        CropXBox.Value = crop.CropX;
        CropYBox.Value = crop.CropY;
        CropWidthBox.Value = crop.CropWidth;
        CropHeightBox.Value = crop.CropHeight;

        var watermark = Find(BatchProcessOperationKind.Watermark);
        WatermarkTextBox.Text = watermark.WatermarkText;
        WatermarkPositionCombo.SelectedIndex = (int)watermark.WatermarkPosition;
        WatermarkSizeBox.Value = watermark.WatermarkPointSize;
        WatermarkOpacityBox.Value = watermark.WatermarkOpacity;
        MetadataModeCombo.SelectedIndex =
            (int)Find(BatchProcessOperationKind.MetadataCleanup).MetadataCleanupMode;
    }

    private void ReadControls()
    {
        var resize = Find(BatchProcessOperationKind.Resize);
        resize.ResizeWidth = ToInt(ResizeWidthBox, 1920);
        resize.ResizeHeight = ToInt(ResizeHeightBox, 1080);
        resize.ResizeMode = (BatchResizeMode)Math.Clamp(ResizeModeCombo.SelectedIndex, 0, 1);
        resize.AllowUpscale = AllowUpscaleCheck.IsChecked == true;

        Find(BatchProcessOperationKind.Convert).OutputFormat =
            (BatchOutputFormat)Math.Clamp(FormatCombo.SelectedIndex, 0, 4);
        var rotate = Find(BatchProcessOperationKind.Rotate);
        rotate.RotationDegrees = RotationCombo.SelectedIndex switch { 1 => 180, 2 => 270, _ => 90 };
        rotate.LosslessJpeg = LosslessJpegCheck.IsChecked == true;

        var crop = Find(BatchProcessOperationKind.Crop);
        crop.CropX = ToInt(CropXBox, 0);
        crop.CropY = ToInt(CropYBox, 0);
        crop.CropWidth = ToInt(CropWidthBox, 1000);
        crop.CropHeight = ToInt(CropHeightBox, 1000);

        var watermark = Find(BatchProcessOperationKind.Watermark);
        watermark.WatermarkText = WatermarkTextBox.Text ?? "";
        watermark.WatermarkPosition = (BatchWatermarkPosition)Math.Clamp(
            WatermarkPositionCombo.SelectedIndex, 0, 4);
        watermark.WatermarkPointSize = ToInt(WatermarkSizeBox, 32);
        watermark.WatermarkOpacity = ToInt(WatermarkOpacityBox, 70);
        Find(BatchProcessOperationKind.MetadataCleanup).MetadataCleanupMode =
            (BatchMetadataCleanupMode)Math.Clamp(MetadataModeCombo.SelectedIndex, 0, 1);
    }

    private BatchProcessOperation Find(BatchProcessOperationKind kind) =>
        _operations.First(operation => operation.Kind == kind);

    private static int ToInt(NumericUpDown input, int fallback) =>
        input.Value is { } value ? Decimal.ToInt32(value) : fallback;
}
