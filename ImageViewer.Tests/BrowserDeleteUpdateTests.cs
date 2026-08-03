using System.Collections.Specialized;
using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class BrowserDeleteUpdateTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"ImageViewer.DeleteUpdateTests.{Guid.NewGuid():N}");

    public BrowserDeleteUpdateTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void DeleteRemovesOnlyCompletedItemsAndFocusesNearestSurvivor()
    {
        using var viewModel = new BrowserViewModel(new AppSettings());
        var first = CreateFileItem("first.jpg");
        var removed = CreateFileItem("removed.jpg");
        var last = CreateFileItem("last.jpg");
        AddItems(viewModel, first, removed, last);
        viewModel.SelectItem(removed);
        var changes = new List<NotifyCollectionChangedAction>();
        var observedCounts = new List<int>();
        viewModel.FilteredItems.CollectionChanged += (_, args) =>
        {
            changes.Add(args.Action);
            observedCounts.Add(viewModel.FilteredItems.Count);
        };

        viewModel.ApplyDeletedPaths([removed.Path]);

        Assert.Equal(new[] { first, last }, viewModel.Items);
        Assert.Equal(new[] { first, last }, viewModel.FilteredItems);
        Assert.Same(last, viewModel.SelectedItem);
        Assert.False(viewModel.IsLoading);
        Assert.Equal([NotifyCollectionChangedAction.Remove], changes);
        Assert.DoesNotContain(0, observedCounts);
    }

    [Fact]
    public void FolderDeleteAlsoRemovesLoadedTreeNodeWithoutRebuildingTree()
    {
        using var viewModel = new BrowserViewModel(new AppSettings());
        var drive = new FolderTreeItem(_root, "root", addPlaceholder: false);
        var deletedPath = Path.Combine(_root, "album");
        var sibling = new FolderTreeItem(
            Path.Combine(_root, "sibling"), "sibling", addPlaceholder: false);
        drive.Children.Add(new FolderTreeItem(deletedPath, "album", addPlaceholder: false));
        drive.Children.Add(sibling);
        viewModel.DriveTree.Add(drive);

        viewModel.ApplyDeletedPaths([deletedPath]);

        Assert.Single(viewModel.DriveTree);
        Assert.Equal(new[] { sibling }, drive.Children);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private ThumbnailItem CreateFileItem(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, "test");
        return new ThumbnailItem(path);
    }

    private static void AddItems(BrowserViewModel viewModel, params ThumbnailItem[] items)
    {
        foreach (var item in items)
        {
            viewModel.Items.Add(item);
            viewModel.FilteredItems.Add(item);
        }
    }
}
