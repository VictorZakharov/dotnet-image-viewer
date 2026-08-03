using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

internal static partial class SafeFileSystemTransfer
{
    public static async Task CopyAsync(
        string source,
        string destination,
        bool replace,
        CancellationToken cancellationToken)
    {
        var sourceIsDirectory = Directory.Exists(source);
        ValidateDestination(source, destination, sourceIsDirectory);
        var staged = CreateSiblingTemporaryPath(destination, "stage");
        try
        {
            if (sourceIsDirectory)
                await Task.Run(
                    () => SafeDirectoryTransfer.CopyAsync(source, staged, cancellationToken),
                    cancellationToken).ConfigureAwait(false);
            else
                await CopyFileToNewPathAsync(source, staged, cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            PublishStagedPath(staged, destination, replace);
        }
        finally
        {
            if (Exists(staged)) DeletePermanently(staged);
        }
    }

    public static async Task MoveAsync(
        string source,
        string destination,
        bool replace,
        CancellationToken cancellationToken)
    {
        var sourceIsDirectory = Directory.Exists(source);
        ValidateDestination(source, destination, sourceIsDirectory);
        cancellationToken.ThrowIfCancellationRequested();

        if (string.Equals(
            Path.GetPathRoot(source),
            Path.GetPathRoot(destination),
            StringComparison.OrdinalIgnoreCase))
        {
            await Task.Run(
                () => MoveWithinVolume(source, destination, replace),
                cancellationToken).ConfigureAwait(false);
            return;
        }

        await CopyAsync(source, destination, replace, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        DeletePermanently(source);
    }

    internal static async Task CopyFileToNewPathAsync(
        string source,
        string destination,
        CancellationToken cancellationToken)
    {
        if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("Links and reparse points cannot be copied safely.");

        await using (var input = new FileStream(
            source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var output = new FileStream(
            destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await input.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
        }

        TryCopyFileMetadata(source, destination);
    }

    private static void ValidateDestination(
        string source,
        string destination,
        bool sourceIsDirectory)
    {
        if (!sourceIsDirectory) return;
        var sourcePath = Normalize(source);
        var destinationPath = Normalize(destination);
        if (destinationPath.StartsWith(
            sourcePath + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase))
        {
            throw new IOException("A folder cannot be copied or moved into itself.");
        }
    }

    private static string Normalize(string path) =>
        Path.GetFullPath(path).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);

    private static void TryCopyFileMetadata(string source, string destination)
    {
        try
        {
            File.SetCreationTimeUtc(destination, File.GetCreationTimeUtc(source));
            File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
            File.SetAttributes(destination, File.GetAttributes(source));
        }
        catch { /* content is more important than best-effort metadata */ }
    }
}
