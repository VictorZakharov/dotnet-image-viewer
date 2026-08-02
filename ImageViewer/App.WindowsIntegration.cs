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
            || _mainWindow is not { IsVisible: true } owner
            || _vm?.Settings.SuppressAssociationPrompt == true)
        {
            return;
        }

        var status = WindowsFileRegistration.GetStatus();
        var allFormatsRegistered =
            status.State == WindowsIntegrationState.RegisteredHere
            && status.Extensions.Count == WindowsFileRegistration.TotalAssociationCount;
        if (allFormatsRegistered || !owner.IsVisible) return;

        var dialog = new WindowsIntegrationDialog(
            isStartupPrompt: true,
            settings: _vm?.Settings);
        await dialog.ShowDialog(owner);
    }
}
