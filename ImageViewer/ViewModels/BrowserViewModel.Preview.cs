using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ImageViewer.Services;

namespace ImageViewer.ViewModels;

public partial class BrowserViewModel
{
    public const double DefaultPreviewPaneWidth = 420;

    private readonly Func<string, CancellationToken, Task<LoadedImage>> _previewImageLoader;
    private readonly Func<string, CancellationToken, Task<int>> _previewRotationLoader;
    private CancellationTokenSource? _previewLoadCts;
    private Task _previewLoadTask = Task.CompletedTask;
    private int _previewLoadVersion;

    [ObservableProperty] private bool _showPreviewPane;
    [ObservableProperty] private GridLength _previewPaneColumnWidth;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasPreviewImage))]
    private Bitmap? _previewBitmap;

    [ObservableProperty] private int _previewRotation;
    [ObservableProperty] private bool _isPreviewLoading;
    [ObservableProperty] private string? _previewFileName;
    [ObservableProperty] private string _previewStatus = "Select an image to preview.";

    public bool HasPreviewImage => PreviewBitmap is not null;

    internal Task PreviewLoadTask => _previewLoadTask;

    partial void OnShowPreviewPaneChanged(bool value)
    {
        Settings.ShowPreviewPane = value;
        PreviewPaneColumnWidth = new GridLength(value ? DefaultPreviewPaneWidth : 0);
        RefreshPreview();
    }

    [RelayCommand]
    private void TogglePreviewPane() => ShowPreviewPane = !ShowPreviewPane;

    private void RefreshPreview()
    {
        var version = ++_previewLoadVersion;
        CancelPreviewLoad();
        ReplacePreviewBitmap(null);
        PreviewRotation = 0;
        PreviewFileName = null;

        if (!ShowPreviewPane)
        {
            PreviewStatus = "";
            return;
        }

        if (SelectedItem is not { IsImage: true } item)
        {
            PreviewStatus = SelectedItem?.IsVideo == true
                ? "Preview is available for images."
                : "Select an image to preview.";
            return;
        }

        PreviewFileName = item.FileName;
        PreviewStatus = "Loading preview...";
        IsPreviewLoading = true;
        var cancellation = new CancellationTokenSource();
        _previewLoadCts = cancellation;
        _previewLoadTask = LoadPreviewAsync(item.Path, version, cancellation);
    }

    private async Task LoadPreviewAsync(
        string path,
        int version,
        CancellationTokenSource cancellation)
    {
        LoadedImage? loaded = null;
        var adopted = false;
        try
        {
            loaded = await _previewImageLoader(path, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            var rotation = loaded.OrientationBaked
                ? 0
                : await _previewRotationLoader(path, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (!IsCurrentPreviewLoad(version, cancellation)) return;

            PreviewRotation = rotation;
            ReplacePreviewBitmap(loaded.Bitmap);
            adopted = true;
            PreviewStatus = "";
        }
        catch (OperationCanceledException)
        {
            // Expected when focus changes, the pane closes, or the view model is disposed.
        }
        catch
        {
            if (IsCurrentPreviewLoad(version, cancellation))
                PreviewStatus = "Could not preview this image.";
        }
        finally
        {
            if (!adopted) loaded?.Bitmap.Dispose();
            if (IsCurrentPreviewLoad(version, cancellation))
            {
                _previewLoadCts = null;
                IsPreviewLoading = false;
            }
            cancellation.Dispose();
        }
    }

    private bool IsCurrentPreviewLoad(
        int version,
        CancellationTokenSource cancellation) =>
        !_disposed
        && version == _previewLoadVersion
        && ReferenceEquals(_previewLoadCts, cancellation)
        && !cancellation.IsCancellationRequested;

    private void CancelPreviewLoad()
    {
        var previous = _previewLoadCts;
        _previewLoadCts = null;
        previous?.Cancel();
        IsPreviewLoading = false;
    }

    private void ReplacePreviewBitmap(Bitmap? bitmap)
    {
        if (ReferenceEquals(PreviewBitmap, bitmap)) return;
        var previous = PreviewBitmap;
        PreviewBitmap = bitmap;
        previous?.Dispose();
    }

    private void DisposePreview()
    {
        _previewLoadVersion++;
        CancelPreviewLoad();
        ReplacePreviewBitmap(null);
    }

    private static Task<int> LoadPreviewRotationAsync(string path, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try { return ExifReader.Read(path).OrientationRotation; }
            catch { return 0; }
        }, cancellationToken);
}
