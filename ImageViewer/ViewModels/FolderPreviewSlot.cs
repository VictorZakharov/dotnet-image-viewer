using System;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageViewer.ViewModels;

public partial class FolderPreviewSlot : ObservableObject, IDisposable
{
    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private bool _isVideo;

    public void ApplyThumbnail(Bitmap? bitmap)
    {
        if (ReferenceEquals(Thumbnail, bitmap)) return;

        var previous = Thumbnail;
        Thumbnail = bitmap;
        previous?.Dispose();
    }

    public void Dispose() => ApplyThumbnail(null);
}
