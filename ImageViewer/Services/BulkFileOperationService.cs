using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed class BulkFileOperationService
{
    public Task<FileOperationResult> ExecuteAsync(
        FileOperationRequest request,
        Func<FileCollision, CancellationToken, Task<FileCollisionChoice>> resolveCollision,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var sources = request.SourcePaths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (request.Kind == FileOperationKind.Delete)
            return DeleteAsync(sources, progress, cancellationToken);
        if (string.IsNullOrEmpty(request.DestinationFolder))
            throw new ArgumentException("A destination folder is required.", nameof(request));

        var transfers = sources.Select(source => new FileTransferPair(
            source,
            Path.Combine(request.DestinationFolder, Path.GetFileName(source)))).ToList();
        return TransferAsync(
            request.Kind,
            transfers,
            resolveCollision,
            progress,
            cancellationToken);
    }

    public Task<FileOperationResult> UndoMovesAsync(
        IReadOnlyList<FileTransferPair> transfers,
        Func<FileCollision, CancellationToken, Task<FileCollisionChoice>> resolveCollision,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken) =>
        TransferAsync(
            FileOperationKind.Move,
            transfers,
            resolveCollision,
            progress,
            cancellationToken);

    private static async Task<FileOperationResult> TransferAsync(
        FileOperationKind kind,
        IReadOnlyList<FileTransferPair> transfers,
        Func<FileCollision, CancellationToken, Task<FileCollisionChoice>> resolveCollision,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var successful = new List<FileOperationSuccess>();
        var skipped = new List<string>();
        var failures = new List<FileOperationFailure>();
        var canceled = false;

        for (var index = 0; index < transfers.Count; index++)
        {
            var transfer = transfers[index];
            progress?.Report(new FileOperationProgress(index, transfers.Count, transfer.SourcePath));
            if (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                break;
            }

            var destination = transfer.DestinationPath;
            try
            {
                if (!File.Exists(transfer.SourcePath))
                    throw new FileNotFoundException("The source file no longer exists.", transfer.SourcePath);
                var destinationFolder = Path.GetDirectoryName(destination);
                if (string.IsNullOrEmpty(destinationFolder) || !Directory.Exists(destinationFolder))
                    throw new DirectoryNotFoundException("The destination folder does not exist.");

                var samePath = PathsEqual(transfer.SourcePath, destination);
                var replace = false;
                if (samePath || File.Exists(destination) || Directory.Exists(destination))
                {
                    var choice = await resolveCollision(
                        new FileCollision(transfer.SourcePath, destination, samePath),
                        cancellationToken);
                    if (choice == FileCollisionChoice.Cancel)
                    {
                        canceled = true;
                        break;
                    }
                    if (choice == FileCollisionChoice.Skip ||
                        (samePath && choice == FileCollisionChoice.Replace))
                    {
                        skipped.Add(transfer.SourcePath);
                        continue;
                    }
                    if (choice == FileCollisionChoice.Rename)
                        destination = FileNameCollisionResolver.CreateUniquePath(destination);
                    else
                        replace = true;
                }

                if (kind == FileOperationKind.Copy)
                    await CopyFileSafelyAsync(transfer.SourcePath, destination, replace, cancellationToken);
                else
                    await MoveFileSafelyAsync(transfer.SourcePath, destination, replace, cancellationToken);
                successful.Add(new FileOperationSuccess(transfer.SourcePath, destination));
            }
            catch (OperationCanceledException)
            {
                canceled = true;
                break;
            }
            catch (Exception ex)
            {
                failures.Add(new FileOperationFailure(
                    transfer.SourcePath,
                    destination,
                    ex.Message));
            }
        }

        progress?.Report(new FileOperationProgress(
            successful.Count + skipped.Count + failures.Count,
            transfers.Count,
            ""));
        return new FileOperationResult(kind, successful, skipped, failures, canceled);
    }

    private static Task<FileOperationResult> DeleteAsync(
        IReadOnlyList<string> sources,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(() =>
    {
        var successful = new List<FileOperationSuccess>();
        var failures = new List<FileOperationFailure>();
        var canceled = false;
        for (var index = 0; index < sources.Count; index++)
        {
            var source = sources[index];
            progress?.Report(new FileOperationProgress(index, sources.Count, source));
            if (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                break;
            }
            try
            {
                if (!FileOperations.DeleteToRecycleBin(source))
                    throw new IOException("Windows could not move the file to the Recycle Bin.");
                successful.Add(new FileOperationSuccess(source, null));
            }
            catch (Exception ex)
            {
                failures.Add(new FileOperationFailure(source, null, ex.Message));
            }
        }

        progress?.Report(new FileOperationProgress(
            successful.Count + failures.Count,
            sources.Count,
            ""));
        return new FileOperationResult(
            FileOperationKind.Delete,
            successful,
            Array.Empty<string>(),
            failures,
            canceled);
    }, CancellationToken.None);

    private static async Task CopyFileSafelyAsync(
        string source,
        string destination,
        bool replace,
        CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(destination)!;
        var temp = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var input = new FileStream(
                source, FileMode.Open, FileAccess.Read, FileShare.Read, 1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var output = new FileStream(
                temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1024 * 128,
                FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await input.CopyToAsync(output, cancellationToken);
            }

            File.SetLastWriteTime(temp, File.GetLastWriteTime(source));
            File.Move(temp, destination, replace);
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); }
            catch { /* a later cleanup can remove an abandoned temp file */ }
        }
    }

    private static async Task MoveFileSafelyAsync(
        string source,
        string destination,
        bool replace,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
            Path.GetPathRoot(source),
            Path.GetPathRoot(destination),
            StringComparison.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(source, destination, replace);
            return;
        }

        await CopyFileSafelyAsync(source, destination, replace, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(source);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);
}
