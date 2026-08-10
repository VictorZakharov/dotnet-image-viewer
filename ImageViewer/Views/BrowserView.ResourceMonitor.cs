using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ImageViewer.Views;

public partial class BrowserView
{
    private ResourceMonitorWindow? _resourceMonitorWindow;

    private void OnTaskManagerClicked(object? sender, RoutedEventArgs e)
    {
        if (_resourceMonitorWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized)
                existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        if (GetOwnerWindow() is not { } owner) return;
        if (_vm is not { } viewModel) return;
        var window = new ResourceMonitorWindow(viewModel.ResourceMonitor);
        _resourceMonitorWindow = window;
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_resourceMonitorWindow, window))
                _resourceMonitorWindow = null;
        };
        window.Show(owner);
    }
}
