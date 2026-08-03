using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ImageViewer.Models;

namespace ImageViewer.Services;

public static class ImageConversionPreviewService
{
    public static Task<ConversionPreview> CreateAsync(
        string sourcePath,
        BatchOutputFormat format,
        int quality,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(sourcePath);
        image.AutoOrient();
        var magickFormat = ResolveFormat(format);
        if (magickFormat == MagickFormat.Jpeg)
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }

        image.Quality = (uint)Math.Clamp(quality, 1, 100);
        using var output = new MemoryStream();
        image.Write(output, magickFormat);
        cancellationToken.ThrowIfCancellationRequested();
        return new ConversionPreview(
            output.ToArray(),
            new FileInfo(sourcePath).Length,
            format,
            quality);
    }, cancellationToken);

    private static MagickFormat ResolveFormat(BatchOutputFormat format) => format switch
    {
        BatchOutputFormat.Jpeg => MagickFormat.Jpeg,
        BatchOutputFormat.Png => MagickFormat.Png,
        BatchOutputFormat.WebP => MagickFormat.WebP,
        BatchOutputFormat.Tiff => MagickFormat.Tiff,
        _ => throw new ArgumentOutOfRangeException(nameof(format))
    };
}
