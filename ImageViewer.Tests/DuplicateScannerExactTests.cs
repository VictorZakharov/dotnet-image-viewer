using ImageViewer.Models;
using System.Runtime.InteropServices;

namespace ImageViewer.Tests;

public sealed class DuplicateScannerExactTests : IDisposable
{
    private readonly DuplicateScannerTestFolder _folder = new();

    [Fact]
    public async Task IdenticalBytesWithDifferentNamesAndDatesAreGrouped()
    {
        var first = _folder.CreateFile("first.jpg", "identical bytes");
        var second = _folder.CreateFile("renamed.jpg", "identical bytes");
        File.SetLastWriteTimeUtc(first, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        File.SetLastWriteTimeUtc(second, new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        var result = await _folder.ScanAsync(DuplicateScanMode.Exact);

        var group = Assert.Single(result.Groups);
        Assert.Equal(DuplicateGroupKind.Exact, group.Kind);
        Assert.Equal(2, group.Files.Count);
        Assert.Equal(new FileInfo(first).Length, group.ReclaimableBytes);
    }

    [Fact]
    public async Task SameSizeDifferentBytesAreNeverReportedAsExact()
    {
        _folder.CreateFile("left.jpg", "abcdefgh");
        _folder.CreateFile("right.jpg", "ABCDEFGH");

        var result = await _folder.ScanAsync(DuplicateScanMode.Exact);

        Assert.Empty(result.Groups);
    }

    [Fact]
    public async Task HardLinkIsExcludedFromReclaimableCopies()
    {
        if (!OperatingSystem.IsWindows()) return;
        var original = _folder.CreateFile("original.jpg", "same data");
        var alias = Path.Combine(_folder.Root, "hard-link.jpg");
        Assert.True(CreateHardLink(alias, original, IntPtr.Zero));
        _folder.CreateFile("copy.jpg", "same data");

        var result = await _folder.ScanAsync(DuplicateScanMode.Exact);

        Assert.Single(result.HardLinks);
        var group = Assert.Single(result.Groups);
        Assert.Equal(2, group.Files.Count);
        Assert.Equal(1, group.Files.Count(file =>
            string.Equals(file.Path, alias, StringComparison.OrdinalIgnoreCase)
            || string.Equals(file.Path, original, StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task UnreadableFileIsReportedWithoutAbortingScan()
    {
        _folder.CreateFile("first.jpg", "good duplicate");
        _folder.CreateFile("second.jpg", "good duplicate");
        var lockedPath = _folder.CreateFile("locked.jpg", "locked content");
        await using var locked = new FileStream(
            lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = await _folder.ScanAsync(DuplicateScanMode.Exact);

        Assert.Single(result.Groups);
        Assert.Contains(result.Errors, error => error.Path == lockedPath);
    }

    public void Dispose() => _folder.Dispose();

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(
        string fileName,
        string existingFileName,
        IntPtr securityAttributes);
}
