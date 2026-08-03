using ImageViewer.Services;
using ImageViewer.ViewModels;

namespace ImageViewer.Tests;

public sealed class ViewerImageLoadingTests
{
    [Fact]
    public async Task SupersededLoad_DoesNotRevealImageWhileReplacementIsPending()
    {
        var firstStarted = NewSignal();
        var secondStarted = NewSignal();
        var loadNumber = 0;

        async Task<LoadedImage> WaitForCancellationAsync(string _, CancellationToken ct)
        {
            var currentLoad = Interlocked.Increment(ref loadNumber);
            (currentLoad == 1 ? firstStarted : secondStarted).TrySetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("A cancelled test load unexpectedly resumed.");
        }

        var folder = Directory.CreateTempSubdirectory("image-viewer-loading-");
        var viewModel = new ViewerViewModel(new AppSettings(), WaitForCancellationAsync);

        try
        {
            var firstLoad = viewModel.LoadAsync(Path.Combine(folder.FullName, "first.jpg"));
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(viewModel.IsImageLoading);
            Assert.Null(viewModel.Bitmap);

            var secondLoad = viewModel.LoadAsync(Path.Combine(folder.FullName, "second.jpg"));
            await secondStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await firstLoad.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(viewModel.IsImageLoading);
            Assert.Equal("second.jpg", Path.GetFileName(viewModel.FilePath));
            Assert.Null(viewModel.Bitmap);

            viewModel.Dispose();
            await secondLoad.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            viewModel.Dispose();
            folder.Delete(recursive: true);
        }
    }

    private static TaskCompletionSource NewSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
