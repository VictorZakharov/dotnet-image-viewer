using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ImageViewer.Services;
using ImageViewer.ViewModels;
using ImageViewer.Views;

namespace ImageViewer;

public partial class App : Application
{
    private MainWindow? _mainWindow;
    private MainWindowViewModel? _vm;
    private SingleInstanceServer? _instanceServer;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var settings = SettingsStore.Load();

            _vm = new MainWindowViewModel(settings);
            _mainWindow = new MainWindow { DataContext = _vm };

            if (!string.IsNullOrEmpty(Program.InitialPath))
            {
                _vm.Open(Program.InitialPath);
            }
            else if (!string.IsNullOrEmpty(_vm.CurrentFolder))
            {
                _ = _vm.BrowserVM.LoadFolderAsync(_vm.CurrentFolder);
            }

            _instanceServer = new SingleInstanceServer(Program.PipeName);
            _instanceServer.PathReceived += OnPathReceived;
            _instanceServer.FocusRequested += OnFocusRequested;
            _instanceServer.Start();

            desktop.MainWindow = _mainWindow;
            desktop.Exit += OnExit;
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnPathReceived(string path)
    {
        Dispatcher.UIThread.Post(() =>
        {
            _vm?.Open(path);
            BringToFront();
        });
    }

    private void OnFocusRequested()
    {
        Dispatcher.UIThread.Post(BringToFront);
    }

    private void BringToFront()
    {
        if (_mainWindow is null) return;
        if (_mainWindow.WindowState == WindowState.Minimized)
            _mainWindow.WindowState = WindowState.Normal;
        _mainWindow.Activate();
    }

    private void OnExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
    {
        _instanceServer?.Stop();
        if (_vm is not null)
            SettingsStore.Save(_vm.Settings);
    }
}
