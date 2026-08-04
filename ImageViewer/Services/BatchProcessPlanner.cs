using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class BatchProcessPlanner
{
    private static readonly HashSet<string> WritableOriginalExtensions = new(
        [".jpg", ".jpeg", ".png", ".bmp", ".webp", ".tif", ".tiff"],
        StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<BatchPreviewItem>> BuildPreviewAsync(
        IReadOnlyList<string> sourcePaths,
        BatchProcessOptions options,
        CancellationToken cancellationToken) => Task.Run<IReadOnlyList<BatchPreviewItem>>(
            () => BuildPreview(sourcePaths, options, cancellationToken),
            cancellationToken);

    public IReadOnlyList<BatchPreviewItem> BuildPreview(
        IReadOnlyList<string> sourcePaths,
        BatchProcessOptions options,
        CancellationToken cancellationToken = default)
    {
        var sources = sourcePaths
            .Where(MediaFileTypes.IsImage)
            .Select(Path.GetFullPath)
            .Distinct(FileSystemPath.Comparer)
            .ToList();
        var previews = new List<BatchPreviewItem>(sources.Count);
        var reservedTargets = new HashSet<string>(FileSystemPath.Comparer);
        foreach (var source in sources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var item = BuildItem(source, options, reservedTargets);
            previews.Add(item);
            if (!item.IsBlocking && item.Status != BatchPreviewStatus.WillSkip)
                reservedTargets.Add(item.TargetPath);
        }

        MarkDuplicateTargets(previews, options.OverwritePolicy);
        return previews;
    }

    private static BatchPreviewItem BuildItem(
        string source,
        BatchProcessOptions options,
        HashSet<string> reservedTargets)
    {
        if (!File.Exists(source)) return Unsupported(source, "The source image no longer exists.");
        var enabled = options.Operations.Where(operation => operation.IsEnabled).ToList();
        if (enabled.Count == 0) return Unsupported(source, "Enable at least one processing operation.");
        var extension = Path.GetExtension(source);
        if (extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            return Unsupported(source, "Animated and multi-frame image processing is not supported yet.");

        var format = enabled.LastOrDefault(operation =>
            operation.Kind == BatchProcessOperationKind.Convert)?.OutputFormat
            ?? BatchOutputFormat.Keep;
        if (format == BatchOutputFormat.Keep && !WritableOriginalExtensions.Contains(extension))
            return Unsupported(source, "RAW images must include a format-conversion operation.");

        string target;
        try
        {
            target = BuildTargetPath(source, options, format);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return new BatchPreviewItem(source, source, BatchPreviewStatus.Invalid, ex.Message);
        }

        var nameError = BatchPathValidator.GetFileNameError(Path.GetFileName(target));
        if (nameError is not null)
            return new BatchPreviewItem(source, target, BatchPreviewStatus.Invalid, nameError);
        if (Directory.Exists(target))
        {
            return new BatchPreviewItem(
                source,
                target,
                BatchPreviewStatus.Collision,
                "A folder already exists at the output path.");
        }
        var validation = ValidateOperations(source, enabled);
        if (validation is not null)
            return new BatchPreviewItem(source, target, BatchPreviewStatus.Unsupported, validation);

        if (options.OutputMode != BatchOutputMode.ReplaceOriginal
            && FileSystemPath.Equals(source, target))
        {
            return new BatchPreviewItem(
                source,
                target,
                BatchPreviewStatus.Collision,
                "The retained-original output path is the source path; add a suffix or destination folder.");
        }

        var targetExists = File.Exists(target) && !FileSystemPath.Equals(source, target);
        if (options.OverwritePolicy == BatchOverwritePolicy.AutoRename
            && (targetExists || reservedTargets.Contains(target)))
        {
            target = FindAvailableTarget(target, reservedTargets);
            targetExists = false;
        }
        if (targetExists && options.OverwritePolicy == BatchOverwritePolicy.Skip)
            return new BatchPreviewItem(source, target, BatchPreviewStatus.WillSkip, "Output exists; policy is Skip.");

        var dimensions = GetResultDimensions(source, enabled);
        if (dimensions.Error is not null)
            return new BatchPreviewItem(source, target, BatchPreviewStatus.Unsupported, dimensions.Error);
        return new BatchPreviewItem(
            source,
            target,
            BatchPreviewStatus.Ready,
            $"Ready · {dimensions.Width} × {dimensions.Height}");
    }

}
