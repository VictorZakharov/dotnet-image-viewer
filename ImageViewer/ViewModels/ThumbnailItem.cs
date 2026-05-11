using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageViewer.ViewModels;

public partial class ThumbnailItem : ObservableObject
{
    public string Path { get; }
    public string FileName { get; }

    [ObservableProperty]
    private Bitmap? _thumbnail;

    public ThumbnailItem(string path)
    {
        Path = path;
        FileName = System.IO.Path.GetFileName(path);
    }
}
