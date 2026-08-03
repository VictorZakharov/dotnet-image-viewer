using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ImageMagick;
using ImageViewer.Models;

namespace ImageViewer.Services;

public static class ImageEditPreviewRenderer
{
    public static Task<Bitmap> RenderAsync(
        string sourcePath,
        IReadOnlyList<BatchProcessOperation> operations,
        CancellationToken cancellationToken = default) => Task.Run(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var image = new MagickImage(sourcePath);
        image.AutoOrient();
        foreach (var operation in operations)
        {
            cancellationToken.ThrowIfCancellationRequested();
            switch (operation.Kind)
            {
                case BatchProcessOperationKind.Rotate:
                    image.Rotate(operation.RotationDegrees);
                    image.ResetPage();
                    break;
                case BatchProcessOperationKind.Crop:
                    image.Crop(new MagickGeometry(
                        operation.CropX,
                        operation.CropY,
                        (uint)operation.CropWidth,
                        (uint)operation.CropHeight));
                    image.ResetPage();
                    break;
            }
        }

        using var stream = new MemoryStream();
        image.Write(stream, MagickFormat.Png);
        cancellationToken.ThrowIfCancellationRequested();
        stream.Position = 0;
        return new Bitmap(stream);
    }, cancellationToken);
}
