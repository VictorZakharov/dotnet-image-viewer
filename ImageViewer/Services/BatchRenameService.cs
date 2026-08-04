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
    public Task<IReadOnlyList<BatchPreviewItem>> BuildPreviewAsync(
        IReadOnlyList<string> sourcePaths,
        BatchRenameOptions options,
        CancellationToken cancellationToken) => Task.Run<IReadOnlyList<BatchPreviewItem>>(
            () => BuildPreview(sourcePaths, options, cancellationToken),
            cancellationToken);

    public IReadOnlyList<BatchPreviewItem> BuildPreview(
        IReadOnlyList<string> sourcePaths,
        BatchRenameOptions options,
        CancellationToken cancellationToken = default)
    {
        var sources = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(FileSystemPath.Comparer)
            .ToList();
        var previews = new List<BatchPreviewItem>(sources.Count);
        for (var index = 0; index < sources.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            previews.Add(BuildItem(sources[index], options, index));
        }

        MarkNestedSelections(previews);
        MarkDuplicateTargets(previews);
        MarkExistingTargets(previews);
        return previews;
    }

    private static BatchPreviewItem BuildItem(
        string source,
        BatchRenameOptions options,
        int index)
    {
        if (!BatchPathValidator.Exists(source))
            return Invalid(source, "The source item no longer exists.");

        try
        {
            var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(source));
            if (string.IsNullOrEmpty(parent))
                return Invalid(source, "File-system roots cannot be renamed in a batch.");

            var stem = BatchRenameTemplate.Expand(
                source,
                checked(options.CounterStart + index),
                options);
            var extension = File.Exists(source) ? Path.GetExtension(source) : "";
            var outputName = stem + extension;
            var nameError = BatchPathValidator.GetFileNameError(outputName);
            if (nameError is not null) return Invalid(source, nameError, outputName);

            var target = Path.GetFullPath(Path.Combine(parent, outputName));
            var unchanged = string.Equals(source, target, StringComparison.Ordinal);
            return new BatchPreviewItem(
                source,
                target,
                unchanged ? BatchPreviewStatus.Unchanged : BatchPreviewStatus.Ready,
                unchanged ? "The generated name is unchanged." : "Ready to rename.");
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException
                                   or PathTooLongException or NotSupportedException)
        {
            return Invalid(source, ex.Message);
        }
    }

    private static void MarkNestedSelections(List<BatchPreviewItem> previews)
    {
        var directories = previews
            .Where(item => Directory.Exists(item.SourcePath))
            .Select(item => item.SourcePath)
            .ToList();
        if (directories.Count == 0) return;

        for (var index = 0; index < previews.Count; index++)
        {
            var item = previews[index];
            var parent = directories.FirstOrDefault(directory =>
                !FileSystemPath.Equals(directory, item.SourcePath)
                && FileSystemPath.IsSameOrChild(item.SourcePath, directory));
            if (parent is null) continue;
            previews[index] = item with
            {
                Status = BatchPreviewStatus.Unsupported,
                Message = "A selected item is inside another selected folder; rename them in separate batches."
            };
            var parentIndex = previews.FindIndex(candidate =>
                FileSystemPath.Equals(candidate.SourcePath, parent));
            if (parentIndex >= 0)
            {
                previews[parentIndex] = previews[parentIndex] with
                {
                    Status = BatchPreviewStatus.Unsupported,
                    Message = "This folder contains another selected item; rename them in separate batches."
                };
            }
        }
    }

    private static void MarkDuplicateTargets(List<BatchPreviewItem> previews)
    {
        var duplicateTargets = previews
            .Where(item => !item.IsBlocking)
            .GroupBy(item => item.TargetPath, FileSystemPath.Comparer)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(FileSystemPath.Comparer);
        if (duplicateTargets.Count == 0) return;

        for (var index = 0; index < previews.Count; index++)
        {
            var item = previews[index];
            if (!duplicateTargets.Contains(item.TargetPath)) continue;
            previews[index] = item with
            {
                Status = BatchPreviewStatus.Collision,
                Message = "More than one selected item generates this output path."
            };
        }
    }

    private static void MarkExistingTargets(List<BatchPreviewItem> previews)
    {
        var sources = previews
            .Select(item => item.SourcePath)
            .ToHashSet(FileSystemPath.Comparer);
        for (var index = 0; index < previews.Count; index++)
        {
            var item = previews[index];
            if (item.Status != BatchPreviewStatus.Ready) continue;
            if (!BatchPathValidator.Exists(item.TargetPath) || sources.Contains(item.TargetPath)) continue;
            previews[index] = item with
            {
                Status = BatchPreviewStatus.Collision,
                Message = "An item already exists at the generated output path."
            };
        }
    }

    private static BatchPreviewItem Invalid(string source, string message, string? outputName = null)
    {
        var parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(source)) ?? "";
        var target = string.IsNullOrEmpty(outputName) ? source : Path.Combine(parent, outputName);
        return new BatchPreviewItem(source, target, BatchPreviewStatus.Invalid, message);
    }
}
