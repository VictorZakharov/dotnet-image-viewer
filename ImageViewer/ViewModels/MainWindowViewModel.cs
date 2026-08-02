using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    public AppSettings Settings { get; }
    public BrowserViewModel BrowserVM { get; }

    private ViewerViewModel? _viewerVm;
    public ViewerViewModel ViewerVM => _viewerVm ??= new ViewerViewModel(Settings);

    public MainWindowViewModel(AppSettings settings)
    {
        Settings = settings;
        BrowserVM = new BrowserViewModel(settings);
        BrowserVM.OpenRequested += OnBrowserOpenRequested;

        if (settings.LastFolder is { } lf && Directory.Exists(lf))
        {
            CurrentFolder = lf;
        }
        else
        {
            var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            if (!string.IsNullOrEmpty(pictures) && Directory.Exists(pictures))
                CurrentFolder = pictures;
        }
    }

    [ObservableProperty]
    private bool _isViewerMode;

    [ObservableProperty]
    private string? _currentImagePath;

    [ObservableProperty]
    private string? _currentFolder;

    public bool CloseViewerOnEscape { get; private set; }

    private void OnBrowserOpenRequested(string path) => Open(path);

    public void Open(string path) => Open(path, closeViewerOnEscape: false);

    public void OpenDirect(string path) => Open(path, closeViewerOnEscape: true);

    private void Open(string path, bool closeViewerOnEscape)
    {
        try
        {
            if (Directory.Exists(path))
            {
                _viewerVm?.Deactivate();
                CloseViewerOnEscape = false;
                CurrentFolder = path;
                Settings.LastFolder = path;
                CurrentImagePath = null;
                _ = BrowserVM.LoadFolderAsync(path);
                IsViewerMode = false;
            }
            else if (File.Exists(path) && MediaFileTypes.IsSupported(path))
            {
                CloseViewerOnEscape = closeViewerOnEscape;
                CurrentImagePath = path;
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                {
                    CurrentFolder = dir;
                    Settings.LastFolder = dir;
                }
                _ = ViewerVM.LoadAsync(path);
                IsViewerMode = true;
            }
        }
        catch
        {
            // Invalid or inaccessible path.
        }
    }

    [RelayCommand]
    private void ToggleMode()
    {
        if (IsViewerMode && !string.IsNullOrEmpty(CurrentFolder))
        {
            _viewerVm?.Deactivate();
            CloseViewerOnEscape = false;
            _ = BrowserVM.LoadFolderAsync(CurrentFolder);
            IsViewerMode = false;
        }
        else if (!IsViewerMode && BrowserVM.SelectedPath is { } sel)
        {
            Open(sel);
        }
        else if (!IsViewerMode && !string.IsNullOrEmpty(CurrentImagePath))
        {
            CloseViewerOnEscape = false;
            IsViewerMode = true;
        }
    }

    public void DeactivateViewer() => _viewerVm?.Deactivate();

    public void StopViewerSlideshow() => _viewerVm?.StopSlideshow();

    public void Dispose()
    {
        BrowserVM.Dispose();
        _viewerVm?.Dispose();
    }
}
