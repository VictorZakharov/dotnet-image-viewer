using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageViewer.ViewModels;

public partial class ThumbnailItem : ObservableObject
{
    public bool IsFolder { get; }
    public bool IsImage => !IsFolder;
    public IReadOnlyList<string> FolderPreviewPaths { get; }
    public DateTime? ModifiedAt { get; }
    public string DateLabel => ModifiedAt?.ToString("yyyy-MM-dd") ?? "";
    public string DateToolTip => ModifiedAt is { } date
        ? $"Modified {date:yyyy-MM-dd HH:mm:ss}"
        : "Modified date unavailable";
    public bool ShowImageTitle => IsImage && !IsRenaming;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExtensionLabel), nameof(ExtensionBrush))]
    private string _path;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExtensionLabel), nameof(ExtensionBrush))]
    private string _fileName;

    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private Bitmap? _folderThumbnail1;
    [ObservableProperty] private Bitmap? _folderThumbnail2;
    [ObservableProperty] private Bitmap? _folderThumbnail3;
    [ObservableProperty] private Bitmap? _folderThumbnail4;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowImageTitle))]
    private bool _isRenaming;
    [ObservableProperty] private string _renameText = "";

    public string ExtensionLabel
    {
        get
        {
            if (IsFolder) return "";
            var ext = System.IO.Path.GetExtension(FileName);
            return string.IsNullOrEmpty(ext) ? "" : ext.TrimStart('.').ToUpperInvariant();
        }
    }

    public IBrush ExtensionBrush => GetBrushFor(ExtensionLabel);

    private static readonly IBrush DefaultExtBrush = new SolidColorBrush(Color.Parse("#b3c1dd"));

    private static readonly Dictionary<string, IBrush> ExtensionBrushes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JPG"] = new SolidColorBrush(Color.Parse("#e0a060")),
        ["JPEG"] = new SolidColorBrush(Color.Parse("#e0a060")),
        ["PNG"] = new SolidColorBrush(Color.Parse("#5bc78a")),
        ["GIF"] = new SolidColorBrush(Color.Parse("#d65bbe")),
        ["BMP"] = new SolidColorBrush(Color.Parse("#e0c050")),
        ["WEBP"] = new SolidColorBrush(Color.Parse("#4dc4d4")),
        ["TIF"] = new SolidColorBrush(Color.Parse("#9a7dd4")),
        ["TIFF"] = new SolidColorBrush(Color.Parse("#9a7dd4")),
        ["ICO"] = new SolidColorBrush(Color.Parse("#8fa1c0")),
        ["NEF"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["CR2"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["CR3"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["ARW"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["DNG"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["RAF"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["RW2"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["ORF"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["PEF"] = new SolidColorBrush(Color.Parse("#e76060")),
        ["SRW"] = new SolidColorBrush(Color.Parse("#e76060")),
    };

    private static IBrush GetBrushFor(string label) =>
        ExtensionBrushes.TryGetValue(label, out var b) ? b : DefaultExtBrush;

    public ThumbnailItem(string path)
        : this(path, isFolder: false, Array.Empty<string>())
    {
    }

    private ThumbnailItem(string path, bool isFolder, IReadOnlyList<string> folderPreviewPaths)
    {
        IsFolder = isFolder;
        FolderPreviewPaths = folderPreviewPaths;
        ModifiedAt = GetModifiedAt(path);
        _path = path;
        _fileName = System.IO.Path.GetFileName(path);
    }

    public static ThumbnailItem CreateFolder(string path, IReadOnlyList<string> previewImagePaths) =>
        new(path, isFolder: true, previewImagePaths);

    private static DateTime? GetModifiedAt(string path)
    {
        try
        {
            var modifiedAt = File.GetLastWriteTime(path);
            return modifiedAt == DateTime.MinValue ? null : modifiedAt;
        }
        catch
        {
            return null;
        }
    }

    public Bitmap? GetFolderThumbnail(int index) => index switch
    {
        0 => FolderThumbnail1,
        1 => FolderThumbnail2,
        2 => FolderThumbnail3,
        3 => FolderThumbnail4,
        _ => null
    };

    public void SetFolderThumbnail(int index, Bitmap? bitmap)
    {
        switch (index)
        {
            case 0: FolderThumbnail1 = bitmap; break;
            case 1: FolderThumbnail2 = bitmap; break;
            case 2: FolderThumbnail3 = bitmap; break;
            case 3: FolderThumbnail4 = bitmap; break;
        }
    }

    public void BeginRename()
    {
        if (IsFolder) return;
        RenameText = System.IO.Path.GetFileNameWithoutExtension(FileName);
        IsRenaming = true;
    }

    public void CancelRename() => IsRenaming = false;

    public bool TryCommitRename(out string? newPath)
    {
        newPath = null;
        if (IsFolder) return false;
        var requestedStem = (RenameText ?? "").Trim();
        var oldStem = System.IO.Path.GetFileNameWithoutExtension(FileName);
        var ext = System.IO.Path.GetExtension(FileName);

        if (string.IsNullOrEmpty(requestedStem) || string.Equals(requestedStem, oldStem, StringComparison.Ordinal))
        {
            IsRenaming = false;
            return false;
        }

        if (requestedStem.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            IsRenaming = false;
            return false;
        }

        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (string.IsNullOrEmpty(dir)) { IsRenaming = false; return false; }

            var target = System.IO.Path.Combine(dir, requestedStem + ext);
            if (File.Exists(target) && !string.Equals(target, Path, StringComparison.OrdinalIgnoreCase))
            {
                IsRenaming = false;
                return false;
            }

            File.Move(Path, target);
            Path = target;
            FileName = System.IO.Path.GetFileName(target);
            IsRenaming = false;
            newPath = target;
            return true;
        }
        catch
        {
            IsRenaming = false;
            return false;
        }
    }
}
