using System;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    public AppSettings Settings { get; }
    public ViewerViewModel ViewerVM { get; }
    public BrowserViewModel BrowserVM { get; }

    public MainWindowViewModel(AppSettings settings)
    {
        Settings = settings;
        ViewerVM = new ViewerViewModel(settings);
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

    private void OnBrowserOpenRequested(string path) => Open(path);

    public void Open(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                CurrentFolder = path;
                Settings.LastFolder = path;
                CurrentImagePath = null;
                _ = BrowserVM.LoadFolderAsync(path);
                IsViewerMode = false;
            }
            else if (File.Exists(path))
            {
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
            _ = BrowserVM.LoadFolderAsync(CurrentFolder);
            IsViewerMode = false;
        }
        else if (!IsViewerMode && BrowserVM.SelectedPath is { } sel)
        {
            Open(sel);
        }
        else if (!IsViewerMode && !string.IsNullOrEmpty(CurrentImagePath))
        {
            IsViewerMode = true;
        }
    }
}
