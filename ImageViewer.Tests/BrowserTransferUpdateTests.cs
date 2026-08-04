using System.Collections.Specialized;
using ImageViewer.Models;
using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class BrowserTransferUpdateTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"ImageViewer.TransferUpdateTests.{Guid.NewGuid():N}");

    public BrowserTransferUpdateTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void PasteInsertsInSortOrderWithoutResettingGridOrFocusedItem()
    {
        using var viewModel = CreateViewModel();
        var middle = AddFile(viewModel, "middle.jpg");
        var last = AddFile(viewModel, "z-last.jpg");
        viewModel.SelectItem(last);
        var destination = CreateFile("a-first.jpg");
        var actions = new List<NotifyCollectionChangedAction>();
        viewModel.FilteredItems.CollectionChanged += (_, args) => actions.Add(args.Action);

        viewModel.ApplyFileOperationChanges(CopyResult(destination));

        Assert.Equal(
            new[] { "a-first.jpg", "middle.jpg", "z-last.jpg" },
            viewModel.FilteredItems.Select(item => item.FileName));
        Assert.Same(last, viewModel.SelectedItem);
        Assert.Same(middle, viewModel.Items[1]);
        Assert.Equal([NotifyCollectionChangedAction.Add], actions);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
    }

    [Fact]
    public void PastedFolderAddsOnlyItsGridTileAndLoadedTreeNode()
    {
        using var viewModel = CreateViewModel();
        var treeRoot = new FolderTreeItem(_root, "root", addPlaceholder: false);
        viewModel.DriveTree.Add(treeRoot);
        viewModel.SelectedTreeItem = treeRoot;
        var destination = Path.Combine(_root, "album");
        Directory.CreateDirectory(destination);

        viewModel.ApplyFileOperationChanges(CopyResult(destination));

        var tile = Assert.Single(viewModel.Items);
        Assert.True(tile.IsFolder);
        Assert.Equal(destination, tile.Path);
        var node = Assert.Single(treeRoot.Children);
        Assert.Equal(destination, node.Path);
    }

    [Fact]
    public void CutPasteRemovesSourceAndAddsDestinationWithoutGridReset()
    {
        using var viewModel = CreateViewModel();
        var survivor = AddFile(viewModel, "keep.jpg");
        var source = AddFile(viewModel, "move-me.jpg");
        viewModel.SelectItem(survivor);
        var destination = Path.Combine(_root, "moved.jpg");
        File.Move(source.Path, destination);
        var actions = new List<NotifyCollectionChangedAction>();
        viewModel.FilteredItems.CollectionChanged += (_, args) => actions.Add(args.Action);
        var result = new FileOperationResult(
            FileOperationKind.Move,
            [new FileOperationSuccess(source.Path, destination)],
            [],
            [],
            IsCanceled: false);

        viewModel.ApplyFileOperationChanges(result);

        Assert.Equal(new[] { "keep.jpg", "moved.jpg" },
            viewModel.Items.Select(item => item.FileName));
        Assert.Same(survivor, viewModel.SelectedItem);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.Equal(
            [NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add],
            actions);
    }

    [Fact]
    public void ImageEditCopyIsInsertedAndSelectedWithoutResettingGrid()
    {
        using var viewModel = CreateViewModel();
        var source = AddFile(viewModel, "photo.png");
        viewModel.SelectItem(source);
        var output = CreateFile("photo_resized.png");
        var actions = new List<NotifyCollectionChangedAction>();
        viewModel.FilteredItems.CollectionChanged += (_, args) => actions.Add(args.Action);

        viewModel.ApplyImageEditResult(new SingleImageEditResult(
            source.Path, output, ReplacedOriginal: false, SingleImageEditKind.Resize));

        Assert.Equal(new[] { "photo.png", "photo_resized.png" },
            viewModel.Items.Select(item => item.FileName));
        Assert.Equal(output, viewModel.SelectedPath);
        Assert.Equal([NotifyCollectionChangedAction.Add], actions);
    }

    [Fact]
    public void ImageEditConversionReplacesSourceWithoutResettingGrid()
    {
        using var viewModel = CreateViewModel();
        var source = AddFile(viewModel, "photo.png");
        viewModel.SelectItem(source);
        File.Delete(source.Path);
        var output = CreateFile("photo.jpg");
        var actions = new List<NotifyCollectionChangedAction>();
        viewModel.FilteredItems.CollectionChanged += (_, args) => actions.Add(args.Action);

        viewModel.ApplyImageEditResult(new SingleImageEditResult(
            source.Path, output, ReplacedOriginal: true, SingleImageEditKind.Convert));

        Assert.Equal(["photo.jpg"], viewModel.Items.Select(item => item.FileName));
        Assert.Equal(output, viewModel.SelectedPath);
        Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
        Assert.Equal(
            [NotifyCollectionChangedAction.Remove, NotifyCollectionChangedAction.Add],
            actions);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private BrowserViewModel CreateViewModel() => new(new AppSettings())
    {
        CurrentFolder = _root
    };

    private ThumbnailItem AddFile(BrowserViewModel viewModel, string name)
    {
        var item = new ThumbnailItem(CreateFile(name));
        viewModel.Items.Add(item);
        viewModel.FilteredItems.Add(item);
        return item;
    }

    private string CreateFile(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "test");
        return path;
    }

    private static FileOperationResult CopyResult(string destination) => new(
        FileOperationKind.Copy,
        [new FileOperationSuccess("source", destination)],
        [],
        [],
        IsCanceled: false);
}
