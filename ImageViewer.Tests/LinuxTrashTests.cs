using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class LinuxTrashTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ImageViewer-LinuxTrashTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task MoveCreatesRecoverableTrashEntry()
    {
        if (!OperatingSystem.IsLinux()) return;
        var sourceDirectory = Directory.CreateDirectory(Path.Combine(_root, "source"));
        var source = Path.Combine(sourceDirectory.FullName, "photo name.jpg");
        await File.WriteAllTextAsync(source, "image data");
        var trashRoot = Path.Combine(_root, "Trash");

        Assert.True(await LinuxTrash.MoveAsync(source, trashRoot));

        Assert.False(File.Exists(source));
        Assert.Equal("image data", await File.ReadAllTextAsync(
            Path.Combine(trashRoot, "files", "photo name.jpg")));
        var info = await File.ReadAllTextAsync(
            Path.Combine(trashRoot, "info", "photo name.jpg.trashinfo"));
        Assert.StartsWith("[Trash Info]\n", info);
        Assert.Contains("Path=", info);
        Assert.Contains("photo%20name.jpg", info);
        Assert.Contains("\nDeletionDate=", info);
    }

    [Fact]
    public async Task ExistingTrashNameGetsUniqueSuffix()
    {
        if (!OperatingSystem.IsLinux()) return;
        var firstDirectory = Directory.CreateDirectory(Path.Combine(_root, "one"));
        var secondDirectory = Directory.CreateDirectory(Path.Combine(_root, "two"));
        var first = Path.Combine(firstDirectory.FullName, "photo.jpg");
        var second = Path.Combine(secondDirectory.FullName, "photo.jpg");
        await File.WriteAllTextAsync(first, "first");
        await File.WriteAllTextAsync(second, "second");
        var trashRoot = Path.Combine(_root, "Trash");

        Assert.True(await LinuxTrash.MoveAsync(first, trashRoot));
        Assert.True(await LinuxTrash.MoveAsync(second, trashRoot));

        Assert.True(File.Exists(Path.Combine(trashRoot, "files", "photo.jpg")));
        Assert.True(File.Exists(Path.Combine(trashRoot, "files", "photo (2).jpg")));
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* best-effort test cleanup */ }
    }
}
