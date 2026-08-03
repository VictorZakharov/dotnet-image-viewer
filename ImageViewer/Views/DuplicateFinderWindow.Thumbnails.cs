using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Services;

namespace ImageViewer.Views;

public partial class DuplicateFinderWindow
{
    private readonly ThumbnailCache _thumbnailCache = new();

    private async void StartThumbnailLoading()
    {
        _thumbnailCancellation?.Cancel();
        _thumbnailCancellation?.Dispose();
        var cancellation = new CancellationTokenSource();
        _thumbnailCancellation = cancellation;
        var token = cancellation.Token;
        var files = _viewModel.Groups.SelectMany(group => group.Files).ToList();
        try
        {
            await Task.WhenAll(files.Select(async file =>
            {
                var thumbnail = await _thumbnailCache.GetOrCreateAsync(
                    file.Path, 128, token);
                if (token.IsCancellationRequested || _isClosing)
                    thumbnail?.Dispose();
                else
                    file.Thumbnail = thumbnail;
            }));
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_thumbnailCancellation, cancellation))
                _thumbnailCancellation = null;
            cancellation.Dispose();
        }
    }
}
