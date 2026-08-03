using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ImageMagick;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class BatchProcessPlanner
{
    private static string? ValidateOperations(
        string source,
        IReadOnlyList<BatchProcessOperation> enabled)
    {
        var rotate = enabled.FirstOrDefault(operation =>
            operation.Kind == BatchProcessOperationKind.Rotate);
        if (rotate is not null && rotate.RotationDegrees is not (90 or 180 or 270))
            return "Rotation must be 90, 180, or 270 degrees.";
        if (rotate?.LosslessJpeg == true && IsJpeg(source))
        {
            if (enabled.Count != 1)
                return "Lossless JPEG rotation must be the only enabled operation.";
            if (ExifReader.Read(source).OrientationRotation != 0)
            {
                return "Lossless JPEG rotation requires an already-normalized EXIF orientation. " +
                       "Use regular rotation for this image.";
            }
            if (!JpegLosslessTransformer.IsAvailable)
                return "Lossless JPEG rotation requires jpegtran on PATH.";
        }

        var watermark = enabled.FirstOrDefault(operation =>
            operation.Kind == BatchProcessOperationKind.Watermark);
        if (watermark is not null && string.IsNullOrWhiteSpace(watermark.WatermarkText))
            return "Watermark text is empty.";
        return null;
    }

    private static (uint Width, uint Height, string? Error) GetResultDimensions(
        string source,
        IReadOnlyList<BatchProcessOperation> operations)
    {
        try
        {
            var info = new MagickImageInfo(source);
            uint width = info.Width;
            uint height = info.Height;
            if (ExifReader.Read(source).OrientationRotation is 90 or 270)
                (width, height) = (height, width);

            foreach (var operation in operations)
            {
                switch (operation.Kind)
                {
                    case BatchProcessOperationKind.Resize:
                        if (operation.ResizeWidth <= 0 || operation.ResizeHeight <= 0)
                            return (width, height, "Resize dimensions must be positive.");
                        (width, height) = ResizeDimensions(width, height, operation);
                        break;
                    case BatchProcessOperationKind.Rotate when operation.RotationDegrees is 90 or 270:
                        (width, height) = (height, width);
                        break;
                    case BatchProcessOperationKind.Crop:
                        if (operation.CropX < 0 || operation.CropY < 0
                            || operation.CropWidth <= 0 || operation.CropHeight <= 0
                            || (long)operation.CropX + operation.CropWidth > width
                            || (long)operation.CropY + operation.CropHeight > height)
                        {
                            return (width, height, $"Crop {operation.CropWidth} × {operation.CropHeight} " +
                                $"at {operation.CropX},{operation.CropY} exceeds the current image bounds.");
                        }
                        width = (uint)operation.CropWidth;
                        height = (uint)operation.CropHeight;
                        break;
                }
            }
            return (width, height, null);
        }
        catch (Exception ex)
        {
            return (0, 0, $"Image dimensions could not be read: {ex.Message}");
        }
    }

    private static (uint Width, uint Height) ResizeDimensions(
        uint width,
        uint height,
        BatchProcessOperation operation)
    {
        if (operation.ResizeMode == BatchResizeMode.Exact)
            return ((uint)operation.ResizeWidth, (uint)operation.ResizeHeight);
        var scale = Math.Min(
            operation.ResizeWidth / (double)width,
            operation.ResizeHeight / (double)height);
        if (!operation.AllowUpscale) scale = Math.Min(scale, 1d);
        return (
            (uint)Math.Max(1, Math.Round(width * scale)),
            (uint)Math.Max(1, Math.Round(height * scale)));
    }

    private static bool IsJpeg(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);
    }
}
