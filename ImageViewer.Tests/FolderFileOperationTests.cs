using ImageViewer.Models;
using ImageViewer.Services;

namespace ImageViewer.Tests;

public sealed class FolderFileOperationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"ImageViewer.FolderTests.{Guid.NewGuid():N}");
    private readonly BulkFileOperationService _service = new();

    public FolderFileOperationTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task CopyFolderIncludesNestedFilesAndEmptyFolders()
    {
        CreateFolder("source");
        var destination = CreateFolder("destination");
        var album = CreateFolder(Path.Combine("source", "album"));
        CreateFolder(Path.Combine("source", "album", "empty"));
        CreateFile(album, "cover.jpg", "image");

        var result = await ExecuteAsync(
            FileOperationKind.Copy,
            album,
            destination,
            FileCollisionChoice.Skip);

        Assert.Single(result.Successful);
        Assert.Equal("image", File.ReadAllText(
            Path.Combine(destination, "album", "cover.jpg")));
        Assert.True(Directory.Exists(Path.Combine(destination, "album", "empty")));
    }

    [Fact]
    public async Task CopySelectionCanMixFilesAndFolders()
    {
        var source = CreateFolder("source");
        var destination = CreateFolder("destination");
        var album = CreateFolder(Path.Combine("source", "album"));
        var looseFile = Path.Combine(source, "loose.jpg");
        CreateFile(source, "loose.jpg", "loose");
        CreateFile(album, "nested.jpg", "nested");

        var result = await _service.ExecuteAsync(
            new FileOperationRequest(
                FileOperationKind.Copy,
                new[] { looseFile, album },
                destination),
            (_, _) => Task.FromResult(FileCollisionChoice.Skip),
            progress: null,
            CancellationToken.None);

        Assert.Equal(2, result.Successful.Count);
        Assert.Equal("loose", File.ReadAllText(Path.Combine(destination, "loose.jpg")));
        Assert.Equal("nested", File.ReadAllText(
            Path.Combine(destination, "album", "nested.jpg")));
    }

    [Fact]
    public async Task ReplaceFolderRemovesOldContentsAndPublishesNewContents()
    {
        var source = CreateFolder(Path.Combine("source", "album"));
        var destination = CreateFolder("destination");
        var existing = CreateFolder(Path.Combine("destination", "album"));
        CreateFile(source, "new.jpg", "new");
        CreateFile(existing, "old.jpg", "old");

        var result = await ExecuteAsync(
            FileOperationKind.Copy,
            source,
            destination,
            FileCollisionChoice.Replace);

        Assert.Single(result.Successful);
        Assert.False(File.Exists(Path.Combine(existing, "old.jpg")));
        Assert.Equal("new", File.ReadAllText(Path.Combine(existing, "new.jpg")));
    }

    [Fact]
    public async Task RenameFolderCollisionKeepsDotsInTheFolderName()
    {
        var source = CreateFolder(Path.Combine("source", "album.2024"));
        var destination = CreateFolder("destination");
        CreateFolder(Path.Combine("destination", "album.2024"));
        CreateFile(source, "photo.jpg", "data");

        var result = await ExecuteAsync(
            FileOperationKind.Copy,
            source,
            destination,
            FileCollisionChoice.Rename);

        Assert.Single(result.Successful);
        Assert.True(File.Exists(Path.Combine(
            destination,
            "album.2024 (2)",
            "photo.jpg")));
    }

    [Fact]
    public async Task MoveFolderCanBeUndone()
    {
        var source = CreateFolder(Path.Combine("source", "album"));
        var destination = CreateFolder("destination");
        CreateFile(source, "photo.jpg", "data");
        var moved = await ExecuteAsync(
            FileOperationKind.Move,
            source,
            destination,
            FileCollisionChoice.Skip);
        var success = Assert.Single(moved.Successful);

        var undone = await _service.UndoMovesAsync(
            new[] { new FileTransferPair(success.DestinationPath!, success.SourcePath) },
            (_, _) => Task.FromResult(FileCollisionChoice.Skip),
            progress: null,
            CancellationToken.None);

        Assert.Single(undone.Successful);
        Assert.True(File.Exists(Path.Combine(source, "photo.jpg")));
        Assert.False(Directory.Exists(success.DestinationPath));
    }

    [Fact]
    public async Task CopyFolderIntoItselfFailsWithoutCreatingAPartialCopy()
    {
        var source = CreateFolder("album");
        var child = CreateFolder(Path.Combine("album", "child"));

        var result = await ExecuteAsync(
            FileOperationKind.Copy,
            source,
            child,
            FileCollisionChoice.Skip);

        Assert.Single(result.Failures);
        Assert.False(Directory.Exists(Path.Combine(child, "album")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private Task<FileOperationResult> ExecuteAsync(
        FileOperationKind kind,
        string source,
        string destination,
        FileCollisionChoice choice) => _service.ExecuteAsync(
            new FileOperationRequest(kind, new[] { source }, destination),
            (_, _) => Task.FromResult(choice),
            progress: null,
            CancellationToken.None);

    private string CreateFolder(string relativePath)
    {
        var path = Path.Combine(_root, relativePath);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CreateFile(string folder, string name, string content) =>
        File.WriteAllText(Path.Combine(folder, name), content);
}
