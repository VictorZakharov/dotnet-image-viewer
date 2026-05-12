using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ImageViewer.ViewModels;

public partial class ThumbnailItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExtensionLabel), nameof(ExtensionBrush))]
    private string _path;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ExtensionLabel), nameof(ExtensionBrush))]
    private string _fileName;

    [ObservableProperty] private Bitmap? _thumbnail;
    [ObservableProperty] private bool _isRenaming;
    [ObservableProperty] private string _renameText = "";

    public string ExtensionLabel
    {
        get
        {
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
    {
        _path = path;
        _fileName = System.IO.Path.GetFileName(path);
    }

    public void BeginRename()
    {
        RenameText = System.IO.Path.GetFileNameWithoutExtension(FileName);
        IsRenaming = true;
    }

    public void CancelRename() => IsRenaming = false;

    public bool TryCommitRename(out string? newPath)
    {
        newPath = null;
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
