using System;
using System.ComponentModel;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class MainWindow : Window
{
    private WindowState _preFullscreenState = WindowState.Normal;
    private MainWindowViewModel? _trackedVm;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
        KeyDown += OnKeyDown;
        TextInput += OnTextInput;

        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
        DragDrop.SetAllowDrop(this, true);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var s = vm.Settings;
        if (s.WindowWidth > 0) Width = s.WindowWidth;
        if (s.WindowHeight > 0) Height = s.WindowHeight;
        if (!double.IsNaN(s.WindowX) && !double.IsNaN(s.WindowY))
            Position = new PixelPoint((int)s.WindowX, (int)s.WindowY);
        if (s.WindowMaximized) WindowState = WindowState.Maximized;

        _trackedVm = vm;
        vm.ViewerVM.PropertyChanged += OnViewerVmPropertyChanged;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        vm.ViewerVM.StopSlideshow();
        if (_trackedVm is not null)
            _trackedVm.ViewerVM.PropertyChanged -= OnViewerVmPropertyChanged;

        var s = vm.Settings;
        s.WindowMaximized = WindowState == WindowState.Maximized;
        if (WindowState == WindowState.Normal)
        {
            s.WindowX = Position.X;
            s.WindowY = Position.Y;
            s.WindowWidth = Bounds.Width;
            s.WindowHeight = Bounds.Height;
        }
    }

    private void OnViewerVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ViewerViewModel.IsFullscreen)) return;
        if (DataContext is not MainWindowViewModel vm) return;

        if (vm.ViewerVM.IsFullscreen)
        {
            _preFullscreenState = WindowState == WindowState.FullScreen ? WindowState.Normal : WindowState;
            WindowState = WindowState.FullScreen;
        }
        else
        {
            WindowState = _preFullscreenState;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var viewer = vm.ViewerVM;
        var browser = vm.BrowserVM;

        if (vm.IsViewerMode && viewer.IsSlideshowRunning && e.Key != Key.Space && e.Key != Key.F5)
            viewer.StopSlideshow();

        switch (e.Key)
        {
            case Key.Escape:
                if (viewer.IsFullscreen)
                    viewer.ToggleFullscreenCommand.Execute(null);
                else if (!vm.IsViewerMode && !string.IsNullOrEmpty(browser.FilterText))
                    browser.FilterText = "";
                else
                    Close();
                e.Handled = true;
                return;

            case Key.Enter:
                vm.ToggleModeCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.O when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _ = OpenFolderDialogAsync();
                e.Handled = true;
                return;
        }

        if (vm.IsViewerMode)
        {
            switch (e.Key)
            {
                case Key.Left:
                    viewer.PreviousCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.Right:
                    viewer.NextCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.R:
                    viewer.RotateRightCommand.Execute(null);
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
                case Key.Space:
                case Key.F5:
                    viewer.ToggleSlideshowCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
            return;
        }

        switch (e.Key)
        {
            case Key.Delete:
                browser.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Back:
                if (!string.IsNullOrEmpty(browser.FilterText))
                {
                    browser.FilterText = browser.FilterText[..^1];
                    e.Handled = true;
                }
                break;
            case Key.D1 when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                browser.SortByNameCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D2 when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                browser.SortByDateCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.D3 when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                browser.SortBySizeCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    private void OnTextInput(object? sender, TextInputEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.IsViewerMode) return;
        if (string.IsNullOrEmpty(e.Text)) return;
        if (e.Text.Length == 1 && char.IsControl(e.Text[0])) return;

        vm.BrowserVM.FilterText += e.Text;
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.Data.Contains(DataFormats.Files)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var files = e.Data.GetFiles();
        if (files is null) return;
        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                vm.Open(path);
                return;
            }
        }
    }

    private async Task OpenFolderDialogAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open folder",
            AllowMultiple = false
        });
        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;
        if (DataContext is MainWindowViewModel vm)
            vm.Open(path);
    }
}
