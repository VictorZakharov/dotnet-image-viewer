using System;
using System.IO;

namespace ImageViewer.Services;

public static class FileNameCollisionResolver
{
    public static string CreateUniquePath(string desiredPath, bool isDirectory = false)
    {
        if (!Exists(desiredPath)) return desiredPath;

        var directory = Path.GetDirectoryName(desiredPath)
            ?? throw new ArgumentException("A destination directory is required.", nameof(desiredPath));
        var stem = isDirectory
            ? Path.GetFileName(desiredPath)
            : Path.GetFileNameWithoutExtension(desiredPath);
        var extension = isDirectory ? "" : Path.GetExtension(desiredPath);
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = Path.Combine(directory, $"{stem} ({suffix}){extension}");
            if (!Exists(candidate)) return candidate;
        }

        throw new IOException($"Could not create a unique name for {Path.GetFileName(desiredPath)}.");
    }

    private static bool Exists(string path) => File.Exists(path) || Directory.Exists(path);
}
