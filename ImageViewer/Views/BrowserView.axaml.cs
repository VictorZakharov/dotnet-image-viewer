using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ImageViewer.ViewModels;

namespace ImageViewer.Views;

public partial class BrowserView : UserControl
{
    public BrowserView()
    {
        InitializeComponent();
        Loaded += (_, _) => ThumbList.Focus();
    }

    private void OnThumbDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is BrowserViewModel vm)
            vm.OpenSelected();
    }

    private async void OnOpenFolderClicked(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open folder",
            AllowMultiple = false
        });

        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        if (string.IsNullOrEmpty(path)) return;

        if (topLevel is Window window && window.DataContext is MainWindowViewModel mwvm)
            mwvm.Open(path);
        else if (DataContext is BrowserViewModel bvm)
            await bvm.LoadFolderAsync(path);
    }
}
