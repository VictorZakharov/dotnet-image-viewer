using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed class SingleImageEditSessionService
{
    private readonly BatchProcessPlanner _planner = new();
    private readonly BatchImageProcessor _processor = new();

    public async Task SaveAsync(
        string sourcePath,
        IReadOnlyList<BatchProcessOperation> operations,
        CancellationToken cancellationToken = default)
    {
        var editable = operations.Select(operation => operation.Clone()).ToList();
        EnableLosslessRotationWhenSafe(sourcePath, editable);
        var options = CreateOptions(editable);
        var preview = await _planner.BuildPreviewAsync(
            [sourcePath], options, cancellationToken);
        var item = preview.SingleOrDefault();
        if (item?.Status != BatchPreviewStatus.Ready)
            throw new InvalidOperationException(item?.Message ?? "The edit could not be validated.");

        var result = await _processor.ExecuteAsync(
            preview, options, progress: null, cancellationToken);
        if (result.Successful.Count == 1) return;
        throw new IOException(result.Failures.FirstOrDefault()?.Error
                              ?? "The edited image could not be saved.");
    }

    private static BatchProcessOptions CreateOptions(
        IReadOnlyList<BatchProcessOperation> operations) => new(
        BatchOutputMode.ReplaceOriginal,
        DestinationFolder: "",
        Suffix: "",
        BatchOverwritePolicy.Replace,
        Quality: 95,
        PreserveFileDates: false,
        PreserveIccProfile: true,
        MaxConcurrency: 1,
        operations);

    private static void EnableLosslessRotationWhenSafe(
        string sourcePath,
        List<BatchProcessOperation> operations)
    {
        if (operations.Count != 1
            || operations[0].Kind != BatchProcessOperationKind.Rotate
            || !JpegLosslessTransformer.IsAvailable
            || ExifReader.Read(sourcePath).OrientationRotation != 0) return;
        var extension = Path.GetExtension(sourcePath);
        if (extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            operations[0].LosslessJpeg = true;
        }
    }
}
