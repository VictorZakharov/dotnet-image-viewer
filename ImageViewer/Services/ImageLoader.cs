using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ImageMagick;

namespace ImageViewer.Services;

public sealed record LoadedImage(Bitmap Bitmap, bool OrientationBaked);

public static class ImageLoader
{
    private static readonly string[] RawExtensions =
    {
        ".nef", ".cr2", ".cr3", ".arw", ".dng", ".raf", ".rw2", ".orf", ".pef", ".srw"
    };

    private static readonly string[] CommonExtensions =
    {
        ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".tiff", ".tif", ".ico"
    };

    public static async Task<LoadedImage> LoadAsync(string path, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return IsRawExtension(ext)
                ? LoadRaw(path, ct)
                : LoadCommon(path, ct);
        }, ct).ConfigureAwait(false);
    }

    private static LoadedImage LoadCommon(string path, CancellationToken ct)
    {
        using var fs = File.OpenRead(path);
        ct.ThrowIfCancellationRequested();
        return new LoadedImage(new Bitmap(fs), OrientationBaked: false);
    }

    private static LoadedImage LoadRaw(string path, CancellationToken ct)
    {
        using var img = new MagickImage(path);
        ct.ThrowIfCancellationRequested();
        img.AutoOrient();
        using var ms = new MemoryStream();
        img.Write(ms, MagickFormat.Png);
        ct.ThrowIfCancellationRequested();
        ms.Position = 0;
        return new LoadedImage(new Bitmap(ms), OrientationBaked: true);
    }

    public static bool IsSupportedExtension(string ext)
    {
        ext = ext.ToLowerInvariant();
        return IsRawExtension(ext) || Array.IndexOf(CommonExtensions, ext) >= 0;
    }

    private static bool IsRawExtension(string lowerExt) =>
        Array.IndexOf(RawExtensions, lowerExt) >= 0;
}
