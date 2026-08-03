using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class BatchProcessTests
{
    [Fact]
    public void PreviewShowsOrderedResizeAndConversionResult()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("source.png", 100, 50);
        var output = folder.Folder("output");
        var resize = Operation(BatchProcessOperationKind.Resize);
        resize.ResizeWidth = 20;
        resize.ResizeHeight = 20;
        var convert = Operation(BatchProcessOperationKind.Convert);
        convert.OutputFormat = BatchOutputFormat.Jpeg;

        var preview = new BatchProcessPlanner().BuildPreview(
            [source],
            Options(output, [resize, convert]));

        var item = Assert.Single(preview);
        Assert.Equal(BatchPreviewStatus.Ready, item.Status);
        Assert.EndsWith(".jpg", item.TargetPath, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("20 × 10", item.Message);
    }

    [Fact]
    public void PreviewFlagsOutputsThatConvergeAfterConversion()
    {
        using var folder = new BatchTestFolder();
        var jpeg = folder.Image("same.jpg");
        var png = folder.Image("same.png");
        var output = folder.Folder("output");
        var convert = Operation(BatchProcessOperationKind.Convert);
        convert.OutputFormat = BatchOutputFormat.Jpeg;
        var options = Options(output, [convert]) with
        {
            OverwritePolicy = BatchOverwritePolicy.Replace
        };

        var preview = new BatchProcessPlanner().BuildPreview([jpeg, png], options);

        Assert.All(preview, item => Assert.Equal(BatchPreviewStatus.Collision, item.Status));
    }

    [Fact]
    public void CropValidationUsesTheConfiguredOperationOrder()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("source.png", 100, 50);
        var output = folder.Folder("output");
        var resize = Operation(BatchProcessOperationKind.Resize);
        resize.ResizeWidth = 20;
        resize.ResizeHeight = 10;
        resize.ResizeMode = BatchResizeMode.Exact;
        var crop = Operation(BatchProcessOperationKind.Crop);
        crop.CropWidth = 80;
        crop.CropHeight = 40;

        var invalid = new BatchProcessPlanner().BuildPreview(
            [source], Options(output, [resize, crop]));
        var valid = new BatchProcessPlanner().BuildPreview(
            [source], Options(output, [crop, resize]));

        Assert.Equal(BatchPreviewStatus.Unsupported, Assert.Single(invalid).Status);
        Assert.Equal(BatchPreviewStatus.Ready, Assert.Single(valid).Status);
    }

    [Fact]
    public void PreviewRejectsFolderCollisionsAndPathSeparatorsInSuffixes()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("source.png");
        var output = folder.Folder("output");
        folder.Folder(Path.Combine("output", "source.png"));
        var resize = Operation(BatchProcessOperationKind.Resize);
        var planner = new BatchProcessPlanner();

        var folderCollision = planner.BuildPreview([source], Options(output, [resize]));
        var invalidSuffixOptions = Options(output, [resize]) with
        {
            OutputMode = BatchOutputMode.BesideOriginal,
            Suffix = Path.DirectorySeparatorChar + "nested"
        };
        var invalidSuffix = planner.BuildPreview([source], invalidSuffixOptions);

        Assert.Equal(BatchPreviewStatus.Collision, Assert.Single(folderCollision).Status);
        Assert.Equal(BatchPreviewStatus.Invalid, Assert.Single(invalidSuffix).Status);
    }

    [Fact]
    public void LosslessJpegRotationRejectsUnnormalizedExifOrientation()
    {
        using var folder = new BatchTestFolder();
        var source = folder.ImageWithMetadata("oriented.jpg", orientation: 6);
        var output = folder.Folder("output");
        var rotate = Operation(BatchProcessOperationKind.Rotate);
        rotate.LosslessJpeg = true;

        var preview = new BatchProcessPlanner().BuildPreview(
            [source], Options(output, [rotate]));

        var item = Assert.Single(preview);
        Assert.Equal(BatchPreviewStatus.Unsupported, item.Status);
        Assert.Contains("already-normalized EXIF orientation", item.Message);
    }

    [Fact]
    public async Task ProcessingRetainsSourceAndCommitsCompleteOutput()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("source.png", 100, 50);
        var expectedModified = new DateTime(2020, 4, 5, 6, 7, 8, DateTimeKind.Local);
        File.SetLastWriteTime(source, expectedModified);
        var output = folder.Folder("output");
        var resize = Operation(BatchProcessOperationKind.Resize);
        resize.ResizeWidth = 24;
        resize.ResizeHeight = 12;
        resize.ResizeMode = BatchResizeMode.Exact;
        var options = Options(output, [resize]);
        var preview = new BatchProcessPlanner().BuildPreview([source], options);

        var result = await new BatchImageProcessor().ExecuteAsync(
            preview,
            options,
            progress: null,
            CancellationToken.None);

        var success = Assert.Single(result.Successful);
        Assert.True(File.Exists(source));
        Assert.True(File.Exists(success.TargetPath));
        Assert.Equal((24u, 12u), folder.Dimensions(success.TargetPath));
        Assert.InRange(
            File.GetLastWriteTime(success.TargetPath),
            expectedModified.AddSeconds(-2),
            expectedModified.AddSeconds(2));
        Assert.Empty(Directory.GetFiles(output, ".imageviewer-process-*"));
    }

    [Fact]
    public async Task CropRotateAndWatermarkRunInOrder()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("source.png", 100, 50);
        var output = folder.Folder("output");
        var crop = Operation(BatchProcessOperationKind.Crop);
        crop.CropX = 5;
        crop.CropY = 5;
        crop.CropWidth = 40;
        crop.CropHeight = 20;
        var rotate = Operation(BatchProcessOperationKind.Rotate);
        rotate.RotationDegrees = 90;
        var watermark = Operation(BatchProcessOperationKind.Watermark);
        watermark.WatermarkText = "QA";
        watermark.WatermarkPointSize = 10;
        var options = Options(output, [crop, rotate, watermark]);
        var preview = new BatchProcessPlanner().BuildPreview([source], options);

        var result = await new BatchImageProcessor().ExecuteAsync(
            preview,
            options,
            progress: null,
            CancellationToken.None);

        var target = Assert.Single(result.Successful).TargetPath;
        Assert.Equal((20u, 40u), folder.Dimensions(target));
    }

    [Fact]
    public void RawExtensionsWithoutConversionAreRejectedBeforeDecode()
    {
        using var folder = new BatchTestFolder();
        var raw = Path.Combine(folder.Root, "source.dng");
        File.WriteAllText(raw, "fixture is intentionally not decoded");
        var output = folder.Folder("output");
        var resize = Operation(BatchProcessOperationKind.Resize);

        var withoutConversion = new BatchProcessPlanner().BuildPreview(
            [raw], Options(output, [resize]));

        var item = Assert.Single(withoutConversion);
        Assert.Equal(BatchPreviewStatus.Unsupported, item.Status);
        Assert.Contains("must include a format-conversion operation", item.Message);
    }

    [Fact]
    public async Task CancellationReportsReadyImagesAsNotStarted()
    {
        using var folder = new BatchTestFolder();
        var first = folder.Image("first.png");
        var second = folder.Image("second.png");
        var output = folder.Folder("output");
        var resize = Operation(BatchProcessOperationKind.Resize);
        var options = Options(output, [resize]);
        var preview = new BatchProcessPlanner().BuildPreview([first, second], options);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await new BatchImageProcessor().ExecuteAsync(
            preview,
            options,
            progress: null,
            cancellation.Token);

        Assert.True(result.IsCanceled);
        Assert.Empty(result.Successful);
        Assert.Equal(2, result.UnprocessedPaths.Count);
        Assert.Empty(Directory.GetFiles(output));
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
