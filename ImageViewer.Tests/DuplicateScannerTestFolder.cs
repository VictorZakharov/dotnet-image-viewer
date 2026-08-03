using ImageMagick;
using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Tests;

internal sealed class DuplicateScannerTestFolder : IDisposable
{
    public string Root { get; } = Path.Combine(
        Path.GetTempPath(), $"ImageViewer.Duplicates.{Guid.NewGuid():N}");

    public string CachePath => Path.Combine(Root, "hash-cache.json");

    public DuplicateScannerTestFolder() => Directory.CreateDirectory(Root);

    public string CreateFile(string name, string content)
    {
        var path = Path.Combine(Root, name);
        File.WriteAllText(path, content);
        return path;
    }

    public string CreateImage(string name, IMagickColor<byte>? color = null)
    {
        var path = Path.Combine(Root, name);
        using var image = new MagickImage(color ?? MagickColors.CornflowerBlue, 96, 64);
        image.Write(path);
        return path;
    }

    public Task<DuplicateScanResult> ScanAsync(
        DuplicateScanMode mode,
        int threshold = 8)
    {
        var scanner = new DuplicateScanner(new DuplicateHashCache(CachePath));
        return scanner.ScanAsync(
            new DuplicateScanOptions([Root], mode, threshold),
            new DuplicateScanPause(),
            progress: null,
            CancellationToken.None);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
