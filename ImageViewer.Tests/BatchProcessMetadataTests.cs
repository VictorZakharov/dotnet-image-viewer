using ImageMagick;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class BatchProcessMetadataTests
{
    [Fact]
    public async Task RegularProcessingNormalizesOrientationAndPreservesMetadata()
    {
        using var folder = new BatchTestFolder();
        var source = folder.ImageWithMetadata("source.jpg", orientation: 6);
        var output = folder.Folder("output");
        var resize = Operation(BatchProcessOperationKind.Resize);
        var options = Options(output, [resize]);
        var preview = new BatchProcessPlanner().BuildPreview([source], options);

        var result = await new BatchImageProcessor().ExecuteAsync(
            preview,
            options,
            progress: null,
            CancellationToken.None);

        var target = Assert.Single(result.Successful).TargetPath;
        Assert.Equal((50u, 100u), folder.Dimensions(target));
        var metadata = ExifReader.Read(target);
        Assert.Equal(0, metadata.OrientationRotation);
        Assert.Equal("Test Camera", metadata.CameraMake);
        using var processed = new MagickImage(target);
        Assert.NotNull(processed.GetColorProfile());
    }

    [Fact]
    public async Task MetadataCleanupRemovesExifButRetainsIccWhenRequested()
    {
        using var folder = new BatchTestFolder();
        var source = folder.ImageWithMetadata("source.jpg");
        var output = folder.Folder("output");
        var cleanup = Operation(BatchProcessOperationKind.MetadataCleanup);
        cleanup.MetadataCleanupMode = BatchMetadataCleanupMode.RemoveAll;
        var options = Options(output, [cleanup]);
        var preview = new BatchProcessPlanner().BuildPreview([source], options);

        var result = await new BatchImageProcessor().ExecuteAsync(
            preview,
            options,
            progress: null,
            CancellationToken.None);

        using var processed = new MagickImage(Assert.Single(result.Successful).TargetPath);
        Assert.Null(processed.GetExifProfile());
        Assert.NotNull(processed.GetColorProfile());
    }

    private static BatchProcessOperation Operation(BatchProcessOperationKind kind) =>
        new() { Kind = kind, IsEnabled = true };

    private static BatchProcessOptions Options(
        string output,
        IReadOnlyList<BatchProcessOperation> operations) => new(
        BatchOutputMode.NewFolder,
        output,
        "_processed",
        BatchOverwritePolicy.Skip,
        Quality: 90,
        PreserveFileDates: true,
        PreserveIccProfile: true,
        MaxConcurrency: 2,
        operations);
}
