using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class BatchRenameService
{
    public async Task<BatchOperationResult> ExecuteAsync(
        IReadOnlyList<BatchPreviewItem> preview,
        IProgress<FileOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        var ready = preview.Where(item => item.Status == BatchPreviewStatus.Ready).ToList();
        var skipped = preview
            .Where(item => item.Status is BatchPreviewStatus.Unchanged or BatchPreviewStatus.WillSkip)
            .Select(item => item.SourcePath)
            .ToList();
        var successful = new List<BatchItemSuccess>();
        var failures = new List<BatchItemFailure>();
        var canceled = false;

        var groups = ready.GroupBy(
            item => Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(item.SourcePath)) ?? "",
            FileSystemPath.Comparer);
        foreach (var group in groups)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                canceled = true;
                break;
            }

            var items = group.ToList();
            progress?.Report(new FileOperationProgress(
                successful.Count + failures.Count + skipped.Count,
                preview.Count,
                items[0].SourcePath));
            try
            {
                await Task.Run(() => RenameDirectoryTransaction(items), CancellationToken.None)
                    .ConfigureAwait(false);
                successful.AddRange(items.Select(item =>
                    new BatchItemSuccess(item.SourcePath, item.TargetPath)));
            }
            catch (Exception ex)
            {
                failures.AddRange(items.Select(item =>
                    new BatchItemFailure(item.SourcePath, item.TargetPath, ex.Message)));
            }
        }

        var handled = successful.Select(item => item.SourcePath)
            .Concat(skipped)
            .Concat(failures.Select(item => item.SourcePath))
            .ToHashSet(FileSystemPath.Comparer);
        var unprocessed = ready
            .Where(item => !handled.Contains(item.SourcePath))
            .Select(item => item.SourcePath)
            .ToList();
        progress?.Report(new FileOperationProgress(
            successful.Count + failures.Count + skipped.Count,
            preview.Count,
            ""));
        return new BatchOperationResult(successful, skipped, failures, unprocessed, canceled);
    }

    private static void RenameDirectoryTransaction(IReadOnlyList<BatchPreviewItem> items)
    {
        var staged = new List<StagedRename>(items.Count);
        try
        {
            foreach (var item in items)
            {
                if (!BatchPathValidator.Exists(item.SourcePath))
                    throw new IOException($"Source no longer exists: {item.SourcePath}");
                var temporary = CreateTemporaryPath(item.SourcePath);
                Move(item.SourcePath, temporary);
                staged.Add(new StagedRename(item.SourcePath, temporary, item.TargetPath));
            }

            foreach (var item in staged)
            {
                if (BatchPathValidator.Exists(item.TargetPath))
                    throw new IOException($"Output appeared after preview: {item.TargetPath}");
                Move(item.TemporaryPath, item.TargetPath);
                item.Committed = true;
            }
        }
        catch
        {
            RollBack(staged);
            throw;
        }
    }

    private static void RollBack(IReadOnlyList<StagedRename> staged)
    {
        var recovery = new List<(StagedRename Item, string Path)>();
        foreach (var item in staged.Where(item => item.Committed))
        {
            var recoveryPath = CreateTemporaryPath(item.SourcePath);
            Move(item.TargetPath, recoveryPath);
            recovery.Add((item, recoveryPath));
        }

        foreach (var item in staged.Where(item => !item.Committed).Reverse())
            if (BatchPathValidator.Exists(item.TemporaryPath))
                Move(item.TemporaryPath, item.SourcePath);
        foreach (var item in recovery.AsEnumerable().Reverse())
            if (BatchPathValidator.Exists(item.Path))
                Move(item.Path, item.Item.SourcePath);
    }

    private static string CreateTemporaryPath(string source)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(source))!;
        var extension = File.Exists(source) ? Path.GetExtension(source) : "";
        string path;
        do
        {
            path = Path.Combine(parent, $".imageviewer-rename-{Guid.NewGuid():N}{extension}");
        } while (BatchPathValidator.Exists(path));
        return path;
    }

    private static void Move(string source, string target)
    {
        if (Directory.Exists(source)) Directory.Move(source, target);
        else File.Move(source, target);
    }

    private sealed class StagedRename(string sourcePath, string temporaryPath, string targetPath)
    {
        public string SourcePath { get; } = sourcePath;
        public string TemporaryPath { get; } = temporaryPath;
        public string TargetPath { get; } = targetPath;
        public bool Committed { get; set; }
    }
}
