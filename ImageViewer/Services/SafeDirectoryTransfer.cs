using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

internal static class SafeDirectoryTransfer
{
    public static async Task CopyAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Links and reparse points cannot be copied safely.");

        Directory.CreateDirectory(destination);
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var target = Path.Combine(destination, Path.GetFileName(entry));
            if (Directory.Exists(entry))
                await CopyAsync(entry, target, cancellationToken).ConfigureAwait(false);
            else
                await SafeFileSystemTransfer.CopyFileToNewPathAsync(
                    entry,
                    target,
                    cancellationToken).ConfigureAwait(false);
        }

        TryCopyDirectoryMetadata(source, destination);
    }

    private static void TryCopyDirectoryMetadata(string source, string destination)
    {
        try
        {
            Directory.SetCreationTimeUtc(destination, Directory.GetCreationTimeUtc(source));
            Directory.SetLastWriteTimeUtc(destination, Directory.GetLastWriteTimeUtc(source));
            File.SetAttributes(destination, File.GetAttributes(source));
        }
        catch { /* content is more important than best-effort metadata */ }
    }
}
