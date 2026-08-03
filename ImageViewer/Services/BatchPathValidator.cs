using System;
using System.IO;

namespace ImageViewer.Services;

public static class BatchPathValidator
{
    private static readonly string[] WindowsReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    ];

    public static string? GetFileNameError(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return "The output name is empty.";
        if (fileName is "." or "..") return "The output name is reserved by the file system.";
        if (fileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return "The output name contains a character that is not allowed by this file system.";

        if (!OperatingSystem.IsWindows()) return null;
        if (fileName.EndsWith(' ') || fileName.EndsWith('.'))
            return "Windows names cannot end with a space or period.";
        var stem = Path.GetFileNameWithoutExtension(fileName);
        foreach (var reserved in WindowsReservedNames)
            if (string.Equals(stem, reserved, StringComparison.OrdinalIgnoreCase))
                return $"{stem} is a reserved Windows name.";
        return null;
    }

    public static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
