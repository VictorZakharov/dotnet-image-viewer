using ImageMagick;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class ImageConversionPreviewTests
{
    [Fact]
    public async Task PreviewContainsDecodedTargetFormatAndExactEncodedSize()
    {
        using var folder = new BatchTestFolder();
        var source = folder.Image("source.png", 120, 80);

        var preview = await ImageConversionPreviewService.CreateAsync(
            source,
            BatchOutputFormat.Jpeg,
            quality: 82);

        using var converted = new MagickImage(preview.EncodedBytes);
        Assert.Equal(MagickFormat.Jpeg, converted.Format);
        Assert.Equal((120u, 80u), (converted.Width, converted.Height));
        Assert.Equal(preview.EncodedBytes.LongLength, preview.ConvertedSizeBytes);
        Assert.Equal(new FileInfo(source).Length, preview.SourceSizeBytes);
    }

    [Theory]
    [InlineData(512, "512 B")]
    [InlineData(1536, "1.5 KB")]
    [InlineData(1572864, "1.5 MB")]
    public void FileSizesUseCompactReadableUnits(long bytes, string expected) =>
        Assert.Equal(expected, FileSizeDisplay.Format(bytes));
}
