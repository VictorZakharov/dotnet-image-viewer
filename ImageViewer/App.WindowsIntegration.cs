using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using ImageViewer.Services;
using ImageViewer.Views;

namespace ImageViewer;

public partial class App
{
    private void OnMainWindowOpened(object? sender, EventArgs e)
    {
        if (_mainWindow is null) return;
        _mainWindow.Opened -= OnMainWindowOpened;

        // Queue behind first-frame work so checking the registry and opening
        // the prompt never delays the initial window.
        Dispatcher.UIThread.Post(
            () => _ = ShowMissingAssociationsPromptAsync(),
            DispatcherPriority.Background);
    }

    private async Task ShowMissingAssociationsPromptAsync()
    {
        // Keep startup responsive even on machines where registry or shell
        // association queries are slow. The prompt is still part of startup,
        // but it cannot contend with first-window rendering.
        await Task.Delay(TimeSpan.FromMilliseconds(300));

        if (!OperatingSystem.IsWindows()
            || _mainWindow is not { IsVisible: true } owner)
        {
            return;
        }

        var status = WindowsFileRegistration.GetStatus();
        if (status.State == WindowsIntegrationState.RegisteredHere || !owner.IsVisible) return;

        var dialog = new WindowsIntegrationDialog(isStartupPrompt: true);
        await dialog.ShowDialog(owner);
    }
}
