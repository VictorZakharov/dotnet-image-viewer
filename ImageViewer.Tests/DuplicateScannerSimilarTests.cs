using ImageMagick;
using ImageViewer.Models;

namespace ImageViewer.Tests;

public sealed class DuplicateScannerSimilarTests : IDisposable
{
    private readonly DuplicateScannerTestFolder _folder = new();

    [Fact]
    public async Task ResizedAndRecompressedImageIsReportedAsVisuallySimilar()
    {
        var original = _folder.CreateImage("original.png");
        var recompressed = Path.Combine(_folder.Root, "smaller.jpg");
        using (var image = new MagickImage(original))
        {
            image.Resize(48, 32);
            image.Quality = 45;
            image.Write(recompressed);
        }

        var result = await _folder.ScanAsync(DuplicateScanMode.Similar, threshold: 4);

        var group = Assert.Single(result.Groups);
        Assert.Equal(DuplicateGroupKind.Similar, group.Kind);
        Assert.Equal(2, group.Files.Count);
        Assert.Equal(4, group.SimilarityThreshold);
    }

    [Fact]
    public async Task ByteIdenticalImagesRemainClearlyMarkedExactInSimilarScan()
    {
        var original = _folder.CreateImage("first.png");
        File.Copy(original, Path.Combine(_folder.Root, "copy.png"));

        var result = await _folder.ScanAsync(DuplicateScanMode.Similar);

        Assert.Equal(DuplicateGroupKind.Exact, Assert.Single(result.Groups).Kind);
    }

    public void Dispose() => _folder.Dispose();
}
