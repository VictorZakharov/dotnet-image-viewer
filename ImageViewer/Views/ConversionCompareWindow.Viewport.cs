using Avalonia.Interactivity;
using ImageViewer.Controls;

namespace ImageViewer.Views;

public partial class ConversionCompareWindow
{
    private bool _applyingViewport;

    private void OnOriginalViewportChanged(object? sender, System.EventArgs e) =>
        SynchronizeViewport(OriginalImage, ConvertedImage);

    private void OnConvertedViewportChanged(object? sender, System.EventArgs e) =>
        SynchronizeViewport(ConvertedImage, OriginalImage);

    private void SynchronizeViewport(ZoomPanImage source, ZoomPanImage target)
    {
        if (_applyingViewport || source.Source is null || target.Source is null) return;
        _applyingViewport = true;
        try { target.ApplyViewport(source.CurrentViewport); }
        finally { _applyingViewport = false; }
    }

    private void OnFit(object? sender, RoutedEventArgs e) => ApplyFit();
    private void OnActualSize(object? sender, RoutedEventArgs e) => ApplyActualSize();

    private void ApplyFit()
    {
        _applyingViewport = true;
        try
        {
            var fit = new NormalizedImageViewport(0.5, 0.5, 1, true);
            OriginalImage.ApplyViewport(fit);
            ConvertedImage.ApplyViewport(fit);
        }
        finally { _applyingViewport = false; }
    }

    private void ApplyActualSize()
    {
        _applyingViewport = true;
        try
        {
            OriginalImage.SetActualSize(notify: false);
            ConvertedImage.SetActualSize(notify: false);
        }
        finally { _applyingViewport = false; }
    }
}
