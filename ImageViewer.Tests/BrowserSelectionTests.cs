using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class BrowserSelectionTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"ImageViewer.SelectionTests.{Guid.NewGuid():N}");
    private readonly List<BrowserViewModel> _viewModels = new();

    public BrowserSelectionTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void CheckmarksAppearOnlyWhenMultipleItemsAreSelected()
    {
        var first = CreateFileItem("first.jpg");
        var second = CreateFileItem("second.jpg");
        var vm = CreateViewModel(first, second);

        vm.SelectItem(first);

        Assert.True(first.IsSelected);
        Assert.False(first.ShowSelectionCheckmark);

        vm.SelectItem(second, toggle: true);

        Assert.True(first.ShowSelectionCheckmark);
        Assert.True(second.ShowSelectionCheckmark);
    }

    [Fact]
    public void BulkFileSelectionExcludesFolderPreviewTiles()
    {
        var folder = ThumbnailItem.CreateFolder(CreateFolder("folder"));
        var file = CreateFileItem("photo.jpg");
        var vm = CreateViewModel(folder, file);

        vm.SelectAll();

        Assert.Equal(2, vm.SelectedCount);
        Assert.Equal(1, vm.SelectedFileCount);
        Assert.Equal(new[] { file.Path }, vm.SelectedFilePaths);
    }

    public void Dispose()
    {
        foreach (var vm in _viewModels) vm.Dispose();
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
    }

    private BrowserViewModel CreateViewModel(params ThumbnailItem[] items)
    {
        var vm = new BrowserViewModel(new AppSettings());
        foreach (var item in items)
        {
            vm.Items.Add(item);
            vm.FilteredItems.Add(item);
        }
        _viewModels.Add(vm);
        return vm;
    }

    private ThumbnailItem CreateFileItem(string name)
    {
        var path = Path.Combine(_root, name);
        File.WriteAllText(path, name);
        return new ThumbnailItem(path);
    }

    private string CreateFolder(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }
}
