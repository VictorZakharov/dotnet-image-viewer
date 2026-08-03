using System;
using System.IO;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

public static class FileOperations
{
    public static string TrashDisplayName => OperatingSystem.IsWindows()
        ? "Recycle Bin"
        : "Trash";

    public static async Task<bool> MoveToTrashAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return false;

        cancellationToken.ThrowIfCancellationRequested();
        if (OperatingSystem.IsWindows())
            return await MoveToWindowsTrashAsync(path).ConfigureAwait(false);

        if (OperatingSystem.IsLinux())
            return await LinuxTrash.MoveAsync(path, cancellationToken: cancellationToken)
                .ConfigureAwait(false);

        return false;
    }

    [SupportedOSPlatform("windows")]
    private static Task<bool> MoveToWindowsTrashAsync(string path) =>
        Task.Run(() => WindowsTrash.TryMove(path));
}
