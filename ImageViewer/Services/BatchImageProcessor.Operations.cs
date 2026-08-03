using System;
using System.Collections.Generic;
using System.IO;
using ImageMagick;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class BatchImageProcessor
{
    private static void ProcessWithMagick(
        string sourcePath,
        string temporaryPath,
        BatchProcessOptions options,
        IReadOnlyList<BatchProcessOperation> operations)
    {
        using var image = new MagickImage(sourcePath);
        image.AutoOrient();
        var outputFormat = BatchOutputFormat.Keep;
        foreach (var operation in operations)
        {
            switch (operation.Kind)
            {
                case BatchProcessOperationKind.Resize:
                    ApplyResize(image, operation);
                    break;
                case BatchProcessOperationKind.Convert:
                    outputFormat = operation.OutputFormat;
                    break;
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
                case BatchProcessOperationKind.Watermark:
                    ApplyWatermark(image, operation);
                    break;
                case BatchProcessOperationKind.MetadataCleanup:
                    ApplyMetadataCleanup(image, operation, options.PreserveIccProfile);
                    break;
            }
        }

        var format = ResolveFormat(sourcePath, outputFormat);
        if (format == MagickFormat.Jpeg)
        {
            image.BackgroundColor = MagickColors.White;
            image.Alpha(AlphaOption.Remove);
        }
        image.Quality = (uint)Math.Clamp(options.Quality, 1, 100);
        image.Write(temporaryPath, format);
    }

    private static void ApplyResize(MagickImage image, BatchProcessOperation operation)
    {
        var targetWidth = (uint)operation.ResizeWidth;
        var targetHeight = (uint)operation.ResizeHeight;
        if (operation.ResizeMode == BatchResizeMode.Exact)
        {
            image.Resize(new MagickGeometry(targetWidth, targetHeight)
            {
                IgnoreAspectRatio = true
            });
            return;
        }

        var scale = Math.Min(
            targetWidth / (double)image.Width,
            targetHeight / (double)image.Height);
        if (!operation.AllowUpscale) scale = Math.Min(scale, 1d);
        image.Resize(
            (uint)Math.Max(1, Math.Round(image.Width * scale)),
            (uint)Math.Max(1, Math.Round(image.Height * scale)));
    }

    private static void ApplyWatermark(MagickImage image, BatchProcessOperation operation)
    {
        var alpha = (byte)Math.Round(255 * Math.Clamp(operation.WatermarkOpacity, 1, 100) / 100d);
        image.Settings.FontPointsize = Math.Clamp(operation.WatermarkPointSize, 6, 300);
        image.Settings.FillColor = new MagickColor(255, 255, 255, alpha);
        image.Settings.StrokeColor = new MagickColor(0, 0, 0, alpha);
        image.Settings.StrokeWidth = 1;
        var margin = (int)Math.Max(8, Math.Min(image.Width, image.Height) / 50);
        var area = new MagickGeometry(
            margin,
            margin,
            Math.Max(1, image.Width - (uint)(margin * 2)),
            Math.Max(1, image.Height - (uint)(margin * 2)));
        image.Annotate(operation.WatermarkText, area, ToGravity(operation.WatermarkPosition));
    }

    private static void ApplyMetadataCleanup(
        MagickImage image,
        BatchProcessOperation operation,
        bool preserveIccProfile)
    {
        if (operation.MetadataCleanupMode == BatchMetadataCleanupMode.RemoveExif)
        {
            image.RemoveProfile("exif");
            image.RemoveProfile("xmp");
            image.RemoveProfile("iptc");
            return;
        }

        var colorProfile = preserveIccProfile ? image.GetColorProfile() : null;
        image.Strip();
        if (colorProfile is not null) image.SetProfile(colorProfile);
    }

    private static Gravity ToGravity(BatchWatermarkPosition position) => position switch
    {
        BatchWatermarkPosition.TopLeft => Gravity.Northwest,
        BatchWatermarkPosition.TopRight => Gravity.Northeast,
        BatchWatermarkPosition.Center => Gravity.Center,
        BatchWatermarkPosition.BottomLeft => Gravity.Southwest,
        _ => Gravity.Southeast
    };

    private static MagickFormat ResolveFormat(string sourcePath, BatchOutputFormat format) => format switch
    {
        BatchOutputFormat.Jpeg => MagickFormat.Jpeg,
        BatchOutputFormat.Png => MagickFormat.Png,
        BatchOutputFormat.WebP => MagickFormat.WebP,
        BatchOutputFormat.Tiff => MagickFormat.Tiff,
        _ => Path.GetExtension(sourcePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => MagickFormat.Jpeg,
            ".png" => MagickFormat.Png,
            ".bmp" => MagickFormat.Bmp,
            ".webp" => MagickFormat.WebP,
            ".tif" or ".tiff" => MagickFormat.Tiff,
            _ => throw new NotSupportedException("The original image format cannot be written safely.")
        }
    };
}
