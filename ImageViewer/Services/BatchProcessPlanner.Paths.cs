using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class BatchProcessPlanner
{
    private static string BuildTargetPath(
        string source,
        BatchProcessOptions options,
        BatchOutputFormat format)
    {
        var sourceFolder = Path.GetDirectoryName(source)!;
        if (options.OutputMode == BatchOutputMode.NewFolder
            && string.IsNullOrWhiteSpace(options.DestinationFolder))
            throw new ArgumentException("Choose an output folder.");
        var folder = options.OutputMode == BatchOutputMode.NewFolder
            ? Path.GetFullPath(options.DestinationFolder)
            : sourceFolder;
        if (File.Exists(folder))
            throw new ArgumentException("The output folder path points to an existing file.");

        var stem = Path.GetFileNameWithoutExtension(source);
        if (options.OutputMode == BatchOutputMode.BesideOriginal)
            stem += options.Suffix;
        var outputName = stem + GetExtension(source, format);
        var nameError = BatchPathValidator.GetFileNameError(outputName);
        if (nameError is not null) throw new ArgumentException(nameError);
        return Path.Combine(folder, outputName);
    }

    private static string GetExtension(string source, BatchOutputFormat format) => format switch
    {
        BatchOutputFormat.Jpeg => ".jpg",
        BatchOutputFormat.Png => ".png",
        BatchOutputFormat.WebP => ".webp",
        BatchOutputFormat.Tiff => ".tif",
        _ => Path.GetExtension(source)
    };

    private static string FindAvailableTarget(string target, HashSet<string> reserved)
    {
        var folder = Path.GetDirectoryName(target)!;
        var stem = Path.GetFileNameWithoutExtension(target);
        var extension = Path.GetExtension(target);
        for (var counter = 1; ; counter++)
        {
            var candidate = Path.Combine(folder, $"{stem} ({counter}){extension}");
            if (!BatchPathValidator.Exists(candidate) && !reserved.Contains(candidate)) return candidate;
        }
    }

    private static void MarkDuplicateTargets(
        List<BatchPreviewItem> preview,
        BatchOverwritePolicy overwritePolicy)
    {
        if (overwritePolicy == BatchOverwritePolicy.AutoRename) return;
        var duplicates = preview
            .Where(item => !item.IsBlocking && item.Status != BatchPreviewStatus.WillSkip)
            .GroupBy(item => item.TargetPath, FileSystemPath.Comparer)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(FileSystemPath.Comparer);
        for (var index = 0; index < preview.Count; index++)
        {
            if (!duplicates.Contains(preview[index].TargetPath)) continue;
            preview[index] = preview[index] with
            {
                Status = BatchPreviewStatus.Collision,
                Message = "More than one source maps to this output path."
            };
        }
    }

    private static BatchPreviewItem Unsupported(string source, string message) =>
        new(source, source, BatchPreviewStatus.Unsupported, message);
}
