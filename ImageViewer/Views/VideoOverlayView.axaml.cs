using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class VideoOverlayView : UserControl
{
    public VideoOverlayView()
    {
        InitializeComponent();
        AddHandler(InputElement.KeyDownEvent, OnOverlayKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not ViewerViewModel viewer) return;

        // LibVLCSharp renders this content in a small owned overlay window.
        // Once a playback control has focus, its keys no longer bubble to the
        // main window, so mirror the viewer shortcuts here.
        switch (e.Key)
        {
            case Key.Escape:
                if (viewer.IsFullscreen)
                    viewer.ToggleFullscreenCommand.Execute(null);
                else if (GetMainWindow() is
                         { DataContext: MainWindowViewModel { CloseViewerOnEscape: true } } mainWindow)
                    mainWindow.Close();
                else if (GetMainViewModel() is { } main)
                    main.ToggleModeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Enter when e.Source is not Button:
                GetMainViewModel()?.ToggleModeCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Left when e.Source is not Slider:
            case Key.Up when e.Source is not Slider:
                viewer.PreviousCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Right when e.Source is not Slider:
            case Key.Down when e.Source is not Slider:
                viewer.NextCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F:
            case Key.F11:
                viewer.ToggleFullscreenCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.I:
                viewer.ToggleExifOverlayCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Space when e.Source is not Button:
                viewer.TogglePlaybackCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private MainWindowViewModel? GetMainViewModel()
    {
        if (GetMainWindow()?.DataContext is MainWindowViewModel main)
        {
            return main;
        }

        return null;
    }

    private MainWindow? GetMainWindow() =>
        TopLevel.GetTopLevel(this) is Window overlay && overlay.Owner is MainWindow mainWindow
            ? mainWindow
            : null;

    private void OnInfoPillClicked(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ViewerViewModel vm)
            vm.ToggleExifOverlayCommand.Execute(null);
        e.Handled = true;
    }
}
