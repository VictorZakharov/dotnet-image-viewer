using System.Linq;
using System.Threading.Tasks;
using Avalonia.Interactivity;
using ImageViewer.Models;

namespace ImageViewer.Views;

public partial class SingleImageEditDialog
{
    private async void OnCompareConversion(object? sender, RoutedEventArgs e)
    {
        if (_isBusy || _conversionPreview is not { } conversion) return;
        var item = _preview.SingleOrDefault();
        if (item?.Status != BatchPreviewStatus.Ready) return;

        var saveFromComparison = await new ConversionCompareWindow(
                _sourcePath,
                conversion.EncodedBytes,
                item.TargetName,
                ApplyButton.Content?.ToString() ?? "Use conversion")
            .ShowDialog<bool>(this);
        if (saveFromComparison)
            await ApplyAsync();
    }
}
