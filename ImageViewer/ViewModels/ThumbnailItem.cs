using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class ThumbnailItem : ObservableObject
{
    public bool IsFolder { get; }
    public bool IsVideo { get; }
    public bool IsImage => !IsFolder && !IsVideo;
    public bool IsFile => !IsFolder;
    public IReadOnlyList<MediaScanEntry> FolderPreviewMedia { get; private set; }
    public bool FolderPreviewMediaLoaded { get; private set; }
    public bool FolderPreview1IsVideo => IsFolderPreviewVideo(0);
    public bool FolderPreview2IsVideo => IsFolderPreviewVideo(1);
    public bool FolderPreview3IsVideo => IsFolderPreviewVideo(2);
    public bool FolderPreview4IsVideo => IsFolderPreviewVideo(3);
    public DateTime? ModifiedAt { get; }
    public string DateLabel => ModifiedAt?.ToString("yyyy-MM-dd") ?? "";
    public string DateToolTip => ModifiedAt is { } date
        ? $"Modified {date:yyyy-MM-dd HH:mm:ss}"
        : "Modified date unavailable";
    public bool ShowFileTitle => IsFile && !IsRenaming;

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
    [ObservableProperty] private bool _isFolderPreviewLoading;
    private int _folderPreviewLoadCount;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowFileTitle))]
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
        ["MP4"] = new SolidColorBrush(Color.Parse("#60a5fa")),
        ["M4V"] = new SolidColorBrush(Color.Parse("#60a5fa")),
        ["MOV"] = new SolidColorBrush(Color.Parse("#5bc0de")),
        ["AVI"] = new SolidColorBrush(Color.Parse("#7dd3fc")),
        ["MKV"] = new SolidColorBrush(Color.Parse("#818cf8")),
        ["WEBM"] = new SolidColorBrush(Color.Parse("#4dc4d4")),
        ["WMV"] = new SolidColorBrush(Color.Parse("#93c5fd")),
        ["MPG"] = new SolidColorBrush(Color.Parse("#a5b4fc")),
        ["MPEG"] = new SolidColorBrush(Color.Parse("#a5b4fc")),
    };

    private static IBrush GetBrushFor(string label) =>
        ExtensionBrushes.TryGetValue(label, out var b) ? b : DefaultExtBrush;

    public ThumbnailItem(string path)
        : this(path, isFolder: false, isVideo: false, folderPreviewMedia: null)
    {
    }

    private ThumbnailItem(
        string path,
        bool isFolder,
        bool isVideo,
        IReadOnlyList<MediaScanEntry>? folderPreviewMedia)
    {
        IsFolder = isFolder;
        IsVideo = isVideo;
        FolderPreviewMedia = folderPreviewMedia ?? Array.Empty<MediaScanEntry>();
        FolderPreviewMediaLoaded = !isFolder || folderPreviewMedia is not null;
        ModifiedAt = GetModifiedAt(path);
        _path = path;
        _fileName = System.IO.Path.GetFileName(path);
    }

    public static ThumbnailItem CreateVideo(string path) =>
        new(path, isFolder: false, isVideo: true, folderPreviewMedia: null);

    public static ThumbnailItem CreateFolder(
        string path,
        IReadOnlyList<MediaScanEntry>? previewMedia = null) =>
        new(path, isFolder: true, isVideo: false, previewMedia);

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

    private bool IsFolderPreviewVideo(int index) =>
        index >= 0 && index < FolderPreviewMedia.Count && FolderPreviewMedia[index].IsVideo;

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

    public void SetFolderPreviewMedia(IReadOnlyList<MediaScanEntry> previewMedia)
    {
        FolderPreviewMedia = previewMedia;
        FolderPreviewMediaLoaded = true;
        OnPropertyChanged(nameof(FolderPreviewMedia));
        OnPropertyChanged(nameof(FolderPreview1IsVideo));
        OnPropertyChanged(nameof(FolderPreview2IsVideo));
        OnPropertyChanged(nameof(FolderPreview3IsVideo));
        OnPropertyChanged(nameof(FolderPreview4IsVideo));
    }

    public void BeginFolderPreviewLoading()
    {
        _folderPreviewLoadCount++;
        IsFolderPreviewLoading = true;
    }

    public void EndFolderPreviewLoading()
    {
        if (_folderPreviewLoadCount > 0) _folderPreviewLoadCount--;
        IsFolderPreviewLoading = _folderPreviewLoadCount > 0;
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
