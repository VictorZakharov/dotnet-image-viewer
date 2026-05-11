using System;
using System.IO;
using System.Linq;
using ImageViewer.Models;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;

namespace ImageViewer.Services;

public static class ExifReader
{
    public static ImageMetadata Read(string path)
    {
        long fileSize = 0;
        try { fileSize = new FileInfo(path).Length; } catch { /* ignored */ }

        try
        {
            var directories = ImageMetadataReader.ReadMetadata(path);
            var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
            var sub = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

            int orientationRot = 0;
            if (ifd0 is not null && ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out int o))
                orientationRot = o switch { 3 => 180, 6 => 90, 8 => 270, _ => 0 };

            double? exposure = null;
            if (sub is not null && sub.TryGetRational(ExifDirectoryBase.TagExposureTime, out var expRat))
                exposure = expRat.ToDouble();

            double? fNumber = null;
            if (sub is not null && sub.TryGetRational(ExifDirectoryBase.TagFNumber, out var fRat))
                fNumber = fRat.ToDouble();

            int? iso = null;
            if (sub is not null && sub.TryGetInt32(ExifDirectoryBase.TagIsoEquivalent, out int isoVal))
                iso = isoVal;

            DateTime? takenAt = null;
            if (sub is not null && sub.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var dt))
                takenAt = dt;

            int? width = null;
            int? height = null;
            if (sub is not null)
            {
                if (sub.TryGetInt32(ExifDirectoryBase.TagExifImageWidth, out int w)) width = w;
                if (sub.TryGetInt32(ExifDirectoryBase.TagExifImageHeight, out int h)) height = h;
            }

            return new ImageMetadata
            {
                OrientationRotation = orientationRot,
                CameraMake = ifd0?.GetString(ExifDirectoryBase.TagMake),
                CameraModel = ifd0?.GetString(ExifDirectoryBase.TagModel),
                Lens = sub?.GetString(ExifDirectoryBase.TagLensModel),
                ExposureTimeSeconds = exposure,
                FNumber = fNumber,
                Iso = iso,
                TakenAt = takenAt,
                Width = width,
                Height = height,
                FileSizeBytes = fileSize
            };
        }
        catch
        {
            return new ImageMetadata { FileSizeBytes = fileSize };
        }
    }
}
