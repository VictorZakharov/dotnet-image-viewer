using System;
using System.IO;

namespace ImageViewer.Services;

internal static partial class SafeFileSystemTransfer
{
    internal static bool Exists(string path) =>
        File.Exists(path) || Directory.Exists(path);

    internal static void DeletePermanently(string path)
    {
        if (Directory.Exists(path))
        {
            DeleteDirectoryPermanently(path);
        }
        else if (File.Exists(path))
        {
            File.SetAttributes(path, FileAttributes.Normal);
            File.Delete(path);
        }
    }

    private static void MoveWithinVolume(string source, string destination, bool replace)
    {
        string? backup = null;
        if (Exists(destination))
        {
            if (!replace) throw new IOException("The destination item already exists.");
            backup = CreateSiblingTemporaryPath(destination, "backup");
            MovePath(destination, backup);
        }

        try
        {
            MovePath(source, destination);
        }
        catch
        {
            if (backup is not null && !Exists(destination)) MovePath(backup, destination);
            throw;
        }

        if (backup is not null) DeletePermanently(backup);
    }

    private static void PublishStagedPath(string staged, string destination, bool replace)
    {
        string? backup = null;
        if (Exists(destination))
        {
            if (!replace) throw new IOException("The destination item already exists.");
            backup = CreateSiblingTemporaryPath(destination, "backup");
            MovePath(destination, backup);
        }

        try
        {
            MovePath(staged, destination);
        }
        catch
        {
            if (backup is not null && !Exists(destination)) MovePath(backup, destination);
            throw;
        }

        if (backup is not null) DeletePermanently(backup);
    }

    private static void MovePath(string source, string destination)
    {
        if (Directory.Exists(source)) Directory.Move(source, destination);
        else File.Move(source, destination);
    }

    private static string CreateSiblingTemporaryPath(string destination, string purpose)
    {
        var parent = Path.GetDirectoryName(destination)
            ?? throw new IOException("The destination has no parent folder.");
        string candidate;
        do
        {
            candidate = Path.Combine(
                parent,
                $".{Path.GetFileName(destination)}.ImageViewer-{Guid.NewGuid():N}.{purpose}");
        } while (Exists(candidate));
        return candidate;
    }

    private static void DeleteDirectoryPermanently(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            File.SetAttributes(path, FileAttributes.Normal);
            Directory.Delete(path, recursive: false);
            return;
        }

        foreach (var child in Directory.EnumerateFileSystemEntries(path))
        {
            DeletePermanently(child);
        }

        File.SetAttributes(path, FileAttributes.Normal);
        Directory.Delete(path, recursive: false);
    }
}
