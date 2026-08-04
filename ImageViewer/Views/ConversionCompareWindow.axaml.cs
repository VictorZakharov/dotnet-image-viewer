using System;
using System.IO;
using System.Threading;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class ConversionCompareWindow : Window
{
    private readonly CancellationTokenSource _loadingCancellation = new();
    private string _sourcePath = "";
    private byte[] _convertedBytes = [];
    private Bitmap? _originalBitmap;
    private Bitmap? _convertedBitmap;
    private bool _released;

    public ConversionCompareWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closed += (_, _) => ReleaseResources();
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
    }

    public ConversionCompareWindow(
        string sourcePath,
        byte[] convertedBytes,
        string convertedName,
        string applyLabel) : this()
    {
        _sourcePath = sourcePath;
        _convertedBytes = convertedBytes;
        OriginalNameText.Text = Path.GetFileName(sourcePath);
        ConvertedNameText.Text = convertedName;
        OriginalSizeText.Text = FileSizeDisplay.Format(new FileInfo(sourcePath).Length);
        ConvertedSizeText.Text = FileSizeDisplay.Format(convertedBytes.LongLength);
        UseConversionButton.Content = applyLabel;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        try
        {
            using var convertedStream = new MemoryStream(_convertedBytes, writable: false);
            _convertedBitmap = new Bitmap(convertedStream);
            ConvertedImage.Source = _convertedBitmap;

            var loaded = await ImageLoader.LoadAsync(
                _sourcePath, _loadingCancellation.Token);
            if (_loadingCancellation.IsCancellationRequested)
            {
                loaded.Bitmap.Dispose();
                return;
            }

            _originalBitmap = loaded.Bitmap;
            OriginalImage.Source = _originalBitmap;
            OriginalImage.Rotation = loaded.OrientationBaked
                ? 0
                : ExifReader.Read(_sourcePath).OrientationRotation;
            LoadingPanel.IsVisible = false;
            ApplyFit();
            OriginalImage.Focus();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            LoadingText.Text = $"Could not load comparison: {ex.Message}";
        }
    }

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F: ApplyFit(); break;
            case Key.A: ApplyActualSize(); break;
            case Key.Escape: Close(false); break;
            default: return;
        }
        e.Handled = true;
    }

    private void OnBack(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
    private void OnUseConversion(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(true);

    private void ReleaseResources()
    {
        if (_released) return;
        _released = true;
        _loadingCancellation.Cancel();
        _loadingCancellation.Dispose();
        OriginalImage.Source = null;
        ConvertedImage.Source = null;
        _originalBitmap?.Dispose();
        _convertedBitmap?.Dispose();
    }
}
