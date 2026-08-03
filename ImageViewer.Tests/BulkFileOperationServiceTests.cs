using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class BulkFileOperationServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"ImageViewer.Tests.{Guid.NewGuid():N}");
    private readonly BulkFileOperationService _service = new();

    public BulkFileOperationServiceTests() => Directory.CreateDirectory(_root);

    [Theory]
    [InlineData(FileCollisionChoice.Skip, "old", null)]
    [InlineData(FileCollisionChoice.Replace, "new", "photo.jpg")]
    [InlineData(FileCollisionChoice.Rename, "old", "photo (2).jpg")]
    public async Task CopyHandlesEveryCollisionChoice(
        FileCollisionChoice choice,
        string originalDestinationContent,
        string? copiedName)
    {
        var sourceFolder = CreateFolder("source");
        var destinationFolder = CreateFolder("destination");
        var source = CreateFile(sourceFolder, "photo.jpg", "new");
        var destination = CreateFile(destinationFolder, "photo.jpg", "old");

        var result = await ExecuteAsync(
            FileOperationKind.Copy,
            new[] { source },
            destinationFolder,
            choice);

        Assert.Equal(originalDestinationContent, File.ReadAllText(destination));
        if (copiedName is null)
        {
            Assert.Single(result.SkippedPaths);
            Assert.Empty(result.Successful);
        }
        else
        {
            Assert.Empty(result.Failures);
            Assert.Equal("new", File.ReadAllText(Path.Combine(destinationFolder, copiedName)));
        }
    }

    [Fact]
    public async Task CopyContinuesAfterAnIndividualFailure()
    {
        var sourceFolder = CreateFolder("source");
        var destinationFolder = CreateFolder("destination");
        var first = CreateFile(sourceFolder, "first.jpg", "first");
        var missing = Path.Combine(sourceFolder, "missing.jpg");
        var last = CreateFile(sourceFolder, "last.jpg", "last");

        var result = await ExecuteAsync(
            FileOperationKind.Copy,
            new[] { first, missing, last },
            destinationFolder,
            FileCollisionChoice.Skip);

        Assert.Equal(2, result.Successful.Count);
        Assert.Single(result.Failures);
        Assert.Equal(missing, result.Failures[0].SourcePath);
        Assert.True(File.Exists(Path.Combine(destinationFolder, "last.jpg")));
    }

    [Fact]
    public async Task MoveCanBeReversedWithItsRecordedDestinations()
    {
        var sourceFolder = CreateFolder("source");
        var destinationFolder = CreateFolder("destination");
        var source = CreateFile(sourceFolder, "photo.jpg", "data");
        var moved = await ExecuteAsync(
            FileOperationKind.Move,
            new[] { source },
            destinationFolder,
            FileCollisionChoice.Skip);
        var success = Assert.Single(moved.Successful);

        var undone = await _service.UndoMovesAsync(
            new[] { new FileTransferPair(success.DestinationPath!, success.SourcePath) },
            (_, _) => Task.FromResult(FileCollisionChoice.Skip),
            progress: null,
            CancellationToken.None);

        Assert.Single(undone.Successful);
        Assert.True(File.Exists(source));
        Assert.False(File.Exists(success.DestinationPath));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private Task<FileOperationResult> ExecuteAsync(
        FileOperationKind kind,
        IReadOnlyList<string> sources,
        string destination,
        FileCollisionChoice choice) => _service.ExecuteAsync(
            new FileOperationRequest(kind, sources, destination),
            (_, _) => Task.FromResult(choice),
            progress: null,
            CancellationToken.None);

    private string CreateFolder(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static string CreateFile(string folder, string name, string content)
    {
        var path = Path.Combine(folder, name);
        File.WriteAllText(path, content);
        return path;
    }
}
