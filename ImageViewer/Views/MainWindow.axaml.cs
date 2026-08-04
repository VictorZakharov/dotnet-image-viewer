using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class MainWindow : Window
{
    private WindowState _preFullscreenState = WindowState.Normal;
    private MainWindowViewModel? _trackedVm;
    private ViewerViewModel? _trackedViewerVm;
    private BrowserView? _browserView;
    private ViewerView? _viewerView;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnOpened;
        Closing += OnClosing;
        AddHandler(
            InputElement.KeyDownEvent,
            OnKeyDown,
            RoutingStrategies.Tunnel,
            handledEventsToo: true);
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
        vm.PropertyChanged += OnMainVmPropertyChanged;
        ShowActiveView();

        // Let the browser render before scanning the remembered folder. This
        // avoids disk and thumbnail work competing with first-frame layout.
        if (!vm.IsViewerMode
            && !string.IsNullOrEmpty(vm.CurrentFolder)
            && string.IsNullOrEmpty(vm.BrowserVM.CurrentFolder))
        {
            var initialFolder = vm.CurrentFolder;
            Dispatcher.UIThread.Post(
                () => _ = vm.BrowserVM.LoadFolderAsync(initialFolder),
                DispatcherPriority.Background);
        }
    }

    private void OnMainVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MainWindowViewModel.IsViewerMode)) return;
        if (DataContext is MainWindowViewModel vm && !vm.IsViewerMode)
            vm.DeactivateViewer();
        ShowActiveView();
        // After a mode switch (double-click thumbnail, Enter, etc.) keyboard
        // focus stays on whichever element had it before — typically the hidden
        // ListBox after entering viewer mode, which then swallows arrow keys via
        // its selection class handler so the viewer never sees them. Move focus
        // to the appropriate live element.
        Dispatcher.UIThread.Post(FocusActiveView, DispatcherPriority.Loaded);
    }

    private void ShowActiveView()
    {
        if (DataContext is not MainWindowViewModel vm) return;

        if (vm.IsViewerMode)
        {
            var viewerVm = vm.ViewerVM;
            TrackViewerVm(viewerVm);
            _viewerView ??= new ViewerView { DataContext = viewerVm };
            ModeHost.Content = _viewerView;
        }
        else
        {
            _browserView ??= new BrowserView { DataContext = vm.BrowserVM };
            ModeHost.Content = _browserView;
            Dispatcher.UIThread.Post(
                () => _ = vm.BrowserVM.EnsureDrivesLoadedAsync(),
                DispatcherPriority.Background);
        }
    }

    private void TrackViewerVm(ViewerViewModel viewerVm)
    {
        if (ReferenceEquals(_trackedViewerVm, viewerVm)) return;
        if (_trackedViewerVm is not null)
            _trackedViewerVm.PropertyChanged -= OnViewerVmPropertyChanged;
        _trackedViewerVm = viewerVm;
        _trackedViewerVm.PropertyChanged += OnViewerVmPropertyChanged;
    }

    private void FocusActiveView()
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.IsViewerMode)
        {
            if (vm.ViewerVM.IsVideo && _viewerView is not null)
            {
                _viewerView.Focus();
                return;
            }

            // Window itself isn't Focusable in Avalonia, so Focus() on `this`
            // is a no-op. ZoomPanImage sets Focusable = true and is the natural
            // recipient for arrow keys in viewer mode.
            var zoomPan = this.GetVisualDescendants()
                .OfType<Controls.ZoomPanImage>()
                .FirstOrDefault();
            if (zoomPan is not null) zoomPan.Focus();
        }
        else
        {
            _browserView?.FocusThumbnailGrid();
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (DeferCloseForPendingEdits(e)) return;

        vm.StopViewerSlideshow();
        if (_trackedVm is not null)
        {
            _trackedVm.PropertyChanged -= OnMainVmPropertyChanged;
        }
        if (_trackedViewerVm is not null)
            _trackedViewerVm.PropertyChanged -= OnViewerVmPropertyChanged;

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

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        var browser = vm.BrowserVM;
        var viewer = vm.IsViewerMode ? vm.ViewerVM : null;

        if (viewer is not null && viewer.IsSlideshowRunning && e.Key != Key.Space && e.Key != Key.F5)
            viewer.StopSlideshow();

        switch (e.Key)
        {
            case Key.Escape:
                if (viewer?.IsFullscreen == true)
                    viewer.ToggleFullscreenCommand.Execute(null);
                else if (viewer?.IsCropping == true)
                    _viewerView?.CancelCrop();
                else if (viewer is not null && vm.CloseViewerOnEscape)
                    Close();
                else if (viewer is not null)
                    await LeaveViewerAsync();
                else if (!string.IsNullOrEmpty(browser.FilterText))
                    browser.FilterText = "";
                e.Handled = true;
                return;

            case Key.Enter:
                if (viewer?.IsCropping == true)
                    await (_viewerView?.ApplyCropAsync() ?? Task.CompletedTask);
                else if (viewer is not null)
                    await LeaveViewerAsync();
                else
                    vm.ToggleModeCommand.Execute(null);
                e.Handled = true;
                return;

            case Key.O when e.KeyModifiers.HasFlag(KeyModifiers.Control):
                _ = OpenFolderDialogAsync();
                e.Handled = true;
                return;
        }

        if (viewer is not null)
        {
            switch (e.Key)
            {
                case Key.Left:
                case Key.Up:
                    e.Handled = true;
                    if (viewer.IsCropping)
                        break;
                    await NavigateViewerAsync(next: false);
                    break;
                case Key.Right:
                case Key.Down:
                    e.Handled = true;
                    if (viewer.IsCropping)
                        break;
                    await NavigateViewerAsync(next: true);
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
                    if (viewer.IsVideo) viewer.TogglePlaybackCommand.Execute(null);
                    else viewer.ToggleSlideshowCommand.Execute(null);
                    e.Handled = true;
                    break;
                case Key.F5:
                    if (!viewer.IsVideo) viewer.ToggleSlideshowCommand.Execute(null);
                    e.Handled = true;
                    break;
            }
            return;
        }

        if (e.Source is not TextBox && e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.A:
                    browser.SelectAll();
                    e.Handled = true;
                    return;
                case Key.C:
                    browser.CopyFilesCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.X:
                    browser.CutFilesCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.V:
                    browser.PasteFilesCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Z:
                    browser.UndoFileOperationCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }

        if (e.Source is TextBox) return;

        switch (e.Key)
        {
            case Key.Delete:
                browser.DeleteSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F2:
                browser.BeginRenameSelected();
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
        // If a thumbnail is being renamed, the TextBox owns text input — don't
        // siphon characters into the filter when focus didn't land cleanly.
        foreach (var item in vm.BrowserVM.FilteredItems)
            if (item.IsRenaming) return;

        vm.BrowserVM.FilterText += e.Text;
        e.Handled = true;
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        if (_browserView?.IsInternalItemDragActive == true)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.DragEffects = e.DataTransfer.Formats.Contains(DataFormat.File)
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (_browserView?.IsInternalItemDragActive == true)
        {
            e.Handled = true;
            return;
        }

        if (DataContext is not MainWindowViewModel vm) return;
        var files = e.DataTransfer.TryGetFiles();
        if (files is null) return;
        foreach (var f in files)
        {
            var path = f.TryGetLocalPath();
            if (!string.IsNullOrEmpty(path))
            {
                if (!await ResolvePendingImageEditsAsync(reloadCurrentImage: false))
                    return;
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
        if (DataContext is MainWindowViewModel vm
            && await ResolvePendingImageEditsAsync(reloadCurrentImage: false))
            vm.Open(path);
    }
}
