using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class SingleImageEditProcessingTests
{
    [Fact]
    public async Task CopyBesideOriginalAutoRenamesAndKeepsSource()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("photo.png", 100, 50);
        folder.Image("photo_resized.png", 10, 10);
        var resize = new BatchProcessOperation
        {
            Kind = BatchProcessOperationKind.Resize,
            IsEnabled = true,
            ResizeWidth = 40,
            ResizeHeight = 20,
            ResizeMode = BatchResizeMode.Exact
        };
        var options = Options(
            BatchOutputMode.BesideOriginal,
            "_resized",
            BatchOverwritePolicy.AutoRename,
            resize);
        var preview = new BatchProcessPlanner().BuildPreview([source], options);

        var item = Assert.Single(preview);
        Assert.Equal(BatchPreviewStatus.Ready, item.Status);
        Assert.EndsWith("photo_resized (1).png", item.TargetPath);

        var result = await new BatchImageProcessor().ExecuteAsync(
            preview, options, progress: null, CancellationToken.None);

        Assert.Empty(result.Failures);
        Assert.True(File.Exists(source));
        Assert.Equal((40u, 20u), folder.Dimensions(Assert.Single(result.Successful).TargetPath));
    }

    [Fact]
    public async Task ReplaceOriginalConversionCommitsNewFormatThenRemovesSource()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("photo.png", 80, 60);
        var convert = new BatchProcessOperation
        {
            Kind = BatchProcessOperationKind.Convert,
            IsEnabled = true,
            OutputFormat = BatchOutputFormat.Jpeg
        };
        var options = Options(
            BatchOutputMode.ReplaceOriginal,
            "",
            BatchOverwritePolicy.Replace,
            convert);
        var preview = new BatchProcessPlanner().BuildPreview([source], options);

        var result = await new BatchImageProcessor().ExecuteAsync(
            preview, options, progress: null, CancellationToken.None);

        var target = Assert.Single(result.Successful).TargetPath;
        Assert.Empty(result.Failures);
        Assert.False(File.Exists(source));
        Assert.True(File.Exists(target));
        Assert.EndsWith("photo.jpg", target, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((80u, 60u), folder.Dimensions(target));
    }

    [Theory]
    [InlineData(SingleImageEditKind.RotateLeft, "_rotated")]
    [InlineData(SingleImageEditKind.Resize, "_resized")]
    [InlineData(SingleImageEditKind.RemoveMetadata, "_clean")]
    public void EditCommandsUseReadableCopySuffixes(
        SingleImageEditKind kind,
        string expected) => Assert.Equal(expected, kind.DefaultSuffix());

    private static BatchProcessOptions Options(
        BatchOutputMode mode,
        string suffix,
        BatchOverwritePolicy overwrite,
        BatchProcessOperation operation) => new(
        mode,
        DestinationFolder: "",
        suffix,
        overwrite,
        Quality: 90,
        PreserveFileDates: false,
        PreserveIccProfile: true,
        MaxConcurrency: 1,
        [operation]);
}
