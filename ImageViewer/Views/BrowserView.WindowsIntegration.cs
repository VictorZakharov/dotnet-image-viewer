using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private async void OnWindowsIntegrationClicked(object? sender, RoutedEventArgs e)
    {
        var dialog = new WindowsIntegrationDialog();
        if (TopLevel.GetTopLevel(this) is Window owner)
            await dialog.ShowDialog(owner);
        else
            dialog.Show();
    }
}
