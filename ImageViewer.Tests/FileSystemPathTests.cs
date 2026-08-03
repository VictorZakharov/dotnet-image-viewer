using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class FileSystemPathTests
{
    [Fact]
    public void PathIdentityMatchesPlatformCaseRules()
    {
        var lower = Path.Combine(Path.GetTempPath(), "photo.jpg");
        var upper = Path.Combine(Path.GetTempPath(), "PHOTO.jpg");

        Assert.Equal(OperatingSystem.IsWindows(), FileSystemPath.Equals(lower, upper));
    }

    [Fact]
    public void ChildCheckRespectsDirectoryBoundary()
    {
        var parent = Path.Combine(Path.GetTempPath(), "photos");
        var child = Path.Combine(parent, "trip", "photo.jpg");
        var siblingPrefix = parent + "-backup";

        Assert.True(FileSystemPath.IsSameOrChild(child, parent));
        Assert.False(FileSystemPath.IsSameOrChild(siblingPrefix, parent));
    }

    [Fact]
    public void LinuxTreeStartsAtTheFileSystemRoot()
    {
        if (!OperatingSystem.IsLinux()) return;

        var root = Assert.Single(FileSystemRoots.Get());
        Assert.Equal("/", root.Path);
        Assert.Equal("File System", root.Label);
    }
}
