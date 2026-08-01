using System;
using System.Collections.Generic;
using System.IO;

namespace ImageViewer.Services;

public static class MediaFileTypes
{
    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".m4v", ".mov", ".avi", ".mkv", ".webm", ".wmv",
        ".mpg", ".mpeg", ".m2v", ".mts", ".m2ts", ".ts", ".3gp",
        ".3g2", ".ogv", ".vob"
    };

    public static bool IsImage(string path) =>
        ImageLoader.IsSupportedExtension(Path.GetExtension(path));

    public static bool IsVideo(string path) =>
        VideoExtensions.Contains(Path.GetExtension(path));

    public static bool IsSupported(string path) => IsImage(path) || IsVideo(path);
}
