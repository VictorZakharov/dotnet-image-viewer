using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class DuplicateHashCacheTests : IDisposable
{
    private readonly DuplicateScannerTestFolder _folder = new();

    [Fact]
    public async Task EntryRequiresMatchingIdentitySizeAndModifiedTime()
    {
        var modified = new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        var cache = new DuplicateHashCache(_folder.CachePath);
        cache.Upsert(new DuplicateHashCacheEntry
        {
            Path = Path.Combine(_folder.Root, "photo.jpg"),
            Identity = "volume:file",
            SizeBytes = 123,
            ModifiedUtcTicks = modified.Ticks,
            ContentHash = "ABC",
            PerceptualHashVersion = DuplicateImageHasher.PerceptualHashVersion,
            PerceptualHash = 42,
            VerticalPerceptualHash = 24,
            ColorSignature = Enumerable.Repeat(
                (byte)17, DuplicateImageHasher.ColorSignatureLength).ToArray(),
            Width = 96,
            Height = 64
        });
        await cache.SaveAsync();
        var loaded = new DuplicateHashCache(_folder.CachePath);
        await loaded.LoadAsync();

        Assert.True(loaded.TryGet(
            Path.Combine(_folder.Root, "photo.jpg"), new FileIdentity("volume:file"),
            123, modified, true, out _));
        Assert.False(loaded.TryGet(
            Path.Combine(_folder.Root, "photo.jpg"), new FileIdentity("changed"),
            123, modified, true, out _));
        Assert.False(loaded.TryGet(
            Path.Combine(_folder.Root, "photo.jpg"), new FileIdentity("volume:file"),
            124, modified, true, out _));
        Assert.False(loaded.TryGet(
            Path.Combine(_folder.Root, "photo.jpg"), new FileIdentity("volume:file"),
            123, modified.AddSeconds(1), true, out _));
    }

    [Fact]
    public void OldPerceptualHashIsUsedForContentButNotVisualMatching()
    {
        var modified = new DateTime(2025, 2, 3, 4, 5, 6, DateTimeKind.Utc);
        var path = Path.Combine(_folder.Root, "photo.jpg");
        var cache = new DuplicateHashCache(_folder.CachePath);
        cache.Upsert(new DuplicateHashCacheEntry
        {
            Path = path,
            Identity = "volume:file",
            SizeBytes = 123,
            ModifiedUtcTicks = modified.Ticks,
            ContentHash = "ABC",
            PerceptualHash = 42,
            Width = 96,
            Height = 64
        });

        Assert.True(cache.TryGet(
            path, new FileIdentity("volume:file"), 123, modified,
            requirePerceptualHash: false, out _));
        Assert.False(cache.TryGet(
            path, new FileIdentity("volume:file"), 123, modified,
            requirePerceptualHash: true, out _));
    }

    public void Dispose() => _folder.Dispose();
}
