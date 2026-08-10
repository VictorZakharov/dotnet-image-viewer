using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class BrowserPreviewTests
{
    [Fact]
    public async Task OpeningPreviewLoadsFocusedImageWithExifOrientation()
    {
        using var folder = new BatchTestFolder();
        var path = folder.File("portrait.jpg", "test image");
        string? loadedPath = null;

        Task<LoadedImage> LoadPreviewAsync(string requestedPath, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            loadedPath = requestedPath;
            // Bitmap rendering is covered by the Avalonia application build. The
            // unit-test host has no platform renderer, so use the same loader seam
            // as the viewer-loading tests to exercise preview orchestration.
            return Task.FromResult(new LoadedImage(null!, OrientationBaked: false));
        }

        static Task<int> LoadRotationAsync(string _, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(90);
        }

        var settings = new AppSettings();
        using var viewModel = new BrowserViewModel(
            settings,
            LoadPreviewAsync,
            LoadRotationAsync);
        var item = new ThumbnailItem(path);
        viewModel.Items.Add(item);
        viewModel.FilteredItems.Add(item);
        viewModel.SelectItem(item);

        Assert.Null(viewModel.PreviewBitmap);

        viewModel.ShowPreviewPane = true;
        await viewModel.PreviewLoadTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(settings.ShowPreviewPane);
        Assert.Equal(path, loadedPath);
        Assert.Equal(90, viewModel.PreviewRotation);
        Assert.Equal("portrait.jpg", viewModel.PreviewFileName);
        Assert.False(viewModel.IsPreviewLoading);
        Assert.Equal("", viewModel.PreviewStatus);

        viewModel.ShowPreviewPane = false;

        Assert.False(settings.ShowPreviewPane);
        Assert.Null(viewModel.PreviewBitmap);
    }

    [Fact]
    public async Task RemovingFocusedImageRefreshesPreviewAtSameGridIndex()
    {
        using var folder = new BatchTestFolder();
        var firstPath = folder.File("first.jpg", "first");
        var secondPath = folder.File("second.jpg", "second");
        var loadedPaths = new List<string>();

        Task<LoadedImage> LoadPreviewAsync(string path, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            loadedPaths.Add(path);
            return Task.FromResult(new LoadedImage(null!, OrientationBaked: true));
        }

        using var viewModel = new BrowserViewModel(
            new AppSettings { ShowPreviewPane = true },
            LoadPreviewAsync);
        var first = new ThumbnailItem(firstPath);
        var second = new ThumbnailItem(secondPath);
        viewModel.Items.Add(first);
        viewModel.Items.Add(second);
        viewModel.FilteredItems.Add(first);
        viewModel.FilteredItems.Add(second);
        viewModel.SelectItem(first);
        await viewModel.PreviewLoadTask;

        viewModel.ApplyDeletedPaths([firstPath]);
        await viewModel.PreviewLoadTask;

        Assert.Equal([firstPath, secondPath], loadedPaths);
        Assert.Same(second, viewModel.SelectedItem);
        Assert.Equal("second.jpg", viewModel.PreviewFileName);
    }
}
