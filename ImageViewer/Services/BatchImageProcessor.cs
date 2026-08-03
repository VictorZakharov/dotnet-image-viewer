using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class BatchImageProcessor
{
    public async Task<BatchOperationResult> ExecuteAsync(
        IReadOnlyList<BatchPreviewItem> preview,
        BatchProcessOptions options,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var ready = new ConcurrentQueue<BatchPreviewItem>(
            preview.Where(item => item.Status == BatchPreviewStatus.Ready));
        var skipped = preview
            .Where(item => item.Status is BatchPreviewStatus.WillSkip or BatchPreviewStatus.Unchanged)
            .Select(item => item.SourcePath)
            .ToList();
        var successful = new ConcurrentBag<BatchItemSuccess>();
        var failures = new ConcurrentBag<BatchItemFailure>();
        var completed = skipped.Count;
        var workerCount = Math.Clamp(options.MaxConcurrency, 1, 8);

        async Task WorkerAsync()
        {
            while (!cancellationToken.IsCancellationRequested && ready.TryDequeue(out var item))
            {
                progress?.Report(new FileOperationProgress(
                    Volatile.Read(ref completed),
                    preview.Count,
                    item.SourcePath));
                try
                {
                    await ProcessOneAsync(item, options, cancellationToken).ConfigureAwait(false);
                    successful.Add(new BatchItemSuccess(item.SourcePath, item.TargetPath));
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    failures.Add(new BatchItemFailure(item.SourcePath, item.TargetPath, ex.Message));
                }
                finally
                {
                    Interlocked.Increment(ref completed);
                }
            }
        }

        var workers = Enumerable.Range(0, Math.Min(workerCount, Math.Max(1, ready.Count)))
            .Select(_ => WorkerAsync())
            .ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);

        var handled = successful.Select(item => item.SourcePath)
            .Concat(skipped)
            .Concat(failures.Select(item => item.SourcePath))
            .ToHashSet(FileSystemPath.Comparer);
        var unprocessed = preview
            .Where(item => item.Status == BatchPreviewStatus.Ready && !handled.Contains(item.SourcePath))
            .Select(item => item.SourcePath)
            .ToList();
        progress?.Report(new FileOperationProgress(
            successful.Count + skipped.Count + failures.Count,
            preview.Count,
            ""));
        return new BatchOperationResult(
            successful.OrderBy(item => item.SourcePath, FileSystemPath.Comparer).ToList(),
            skipped,
            failures.OrderBy(item => item.SourcePath, FileSystemPath.Comparer).ToList(),
            unprocessed,
            cancellationToken.IsCancellationRequested);
    }

    private static async Task ProcessOneAsync(
        BatchPreviewItem item,
        BatchProcessOptions options,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var targetFolder = Path.GetDirectoryName(item.TargetPath)!;
        Directory.CreateDirectory(targetFolder);
        var samePath = FileSystemPath.Equals(item.SourcePath, item.TargetPath);
        if (!samePath && File.Exists(item.TargetPath)
            && options.OverwritePolicy != BatchOverwritePolicy.Replace)
        {
            throw new IOException($"Output appeared after preview: {item.TargetPath}");
        }

        var timestamps = FileTimestamps.Read(item.SourcePath);
        var temporary = CreateTemporaryOutput(item.TargetPath);
        try
        {
            var operations = options.Operations.Where(operation => operation.IsEnabled).ToList();
            var losslessRotate = operations.Count == 1
                && operations[0].Kind == BatchProcessOperationKind.Rotate
                && operations[0].LosslessJpeg
                && IsJpeg(item.SourcePath);
            if (losslessRotate)
            {
                await JpegLosslessTransformer.RotateAsync(
                    item.SourcePath,
                    temporary,
                    operations[0].RotationDegrees,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await Task.Run(
                    () => ProcessWithMagick(item.SourcePath, temporary, options, operations),
                    CancellationToken.None).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(
                temporary,
                item.TargetPath,
                overwrite: samePath || options.OverwritePolicy == BatchOverwritePolicy.Replace);
            if (options.OutputMode == BatchOutputMode.ReplaceOriginal && !samePath)
                File.Delete(item.SourcePath);
            if (options.PreserveFileDates) timestamps.Apply(item.TargetPath);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }

    private static string CreateTemporaryOutput(string target)
    {
        var folder = Path.GetDirectoryName(target)!;
        var extension = Path.GetExtension(target);
        string path;
        do
        {
            path = Path.Combine(folder, $".imageviewer-process-{Guid.NewGuid():N}{extension}");
        } while (File.Exists(path));
        return path;
    }

    private static bool IsJpeg(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record FileTimestamps(DateTime Creation, DateTime Modified, DateTime Accessed)
    {
        public static FileTimestamps Read(string path) => new(
            File.GetCreationTime(path),
            File.GetLastWriteTime(path),
            File.GetLastAccessTime(path));

        public void Apply(string path)
        {
            TrySet(() => File.SetCreationTime(path, Creation));
            TrySet(() => File.SetLastWriteTime(path, Modified));
            TrySet(() => File.SetLastAccessTime(path, Accessed));
        }

        private static void TrySet(Action action)
        {
            try { action(); } catch { }
        }
    }
}
