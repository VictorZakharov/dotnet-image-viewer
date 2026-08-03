using System;
using System.Collections.Generic;
using System.IO;

namespace ImageViewer.Services;

internal static class FileSystemRoots
{
    public static IReadOnlyList<(string Path, string Label)> Get()
    {
        if (OperatingSystem.IsLinux())
            return [(Path.DirectorySeparatorChar.ToString(), "File System")];

        var roots = new List<(string Path, string Label)>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            var path = drive.RootDirectory.FullName;
            try
            {
                var shortName = drive.Name.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                if (string.IsNullOrEmpty(shortName)) shortName = drive.Name;
                var label = string.IsNullOrEmpty(drive.VolumeLabel)
                    ? shortName
                    : $"{shortName} ({drive.VolumeLabel})";
                roots.Add((path, label));
            }
            catch
            {
                roots.Add((path, drive.Name));
            }
        }
        return roots;
    }
}
