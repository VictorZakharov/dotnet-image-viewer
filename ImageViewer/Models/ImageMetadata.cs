using System;
using System.Collections.Generic;

namespace ImageViewer.Models;

public sealed class ImageMetadata
{
    public int OrientationRotation { get; init; }
    public string? CameraMake { get; init; }
    public string? CameraModel { get; init; }
    public string? Lens { get; init; }
    public double? ExposureTimeSeconds { get; init; }
    public double? FNumber { get; init; }
    public int? Iso { get; init; }
    public DateTime? TakenAt { get; init; }
    public int? Width { get; init; }
    public int? Height { get; init; }
    public long FileSizeBytes { get; init; }
    public DateTime? FileCreatedAt { get; init; }
    public DateTime? FileModifiedAt { get; init; }
    public DateTime? FileAccessedAt { get; init; }

    public string? ExposureSummary => BuildExposure();
    public string? DimensionsSummary =>
        Width is int w && Height is int h ? $"{w} × {h}" : null;
    public string? TakenAtSummary =>
        FormatDate(TakenAt);
    public string? FileCreatedAtSummary => FormatDate(FileCreatedAt);
    public string? FileModifiedAtSummary => FormatDate(FileModifiedAt);
    public string? FileAccessedAtSummary => FormatDate(FileAccessedAt);
    public string? FileSizeSummary => FileSizeBytes > 0 ? FormatBytes(FileSizeBytes) : null;

    public string? CameraSummary
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(CameraMake)) parts.Add(CameraMake!);
            if (!string.IsNullOrEmpty(CameraModel)) parts.Add(CameraModel!);
            return parts.Count == 0 ? null : string.Join(" ", parts);
        }
    }

    public bool HasAnyExif =>
        CameraSummary is not null
        || !string.IsNullOrEmpty(Lens)
        || ExposureSummary is not null
        || TakenAtSummary is not null;

    private string? BuildExposure()
    {
        var parts = new List<string>();
        if (ExposureTimeSeconds is { } t && t > 0)
        {
            parts.Add(t >= 1
                ? $"{t:0.0}s"
                : $"1/{Math.Round(1.0 / t)}s");
        }
        if (FNumber is { } f) parts.Add($"f/{f:0.0}");
        if (Iso is { } iso) parts.Add($"ISO {iso}");
        return parts.Count == 0 ? null : string.Join("  ", parts);
    }

    private static string? FormatDate(DateTime? value) =>
        value is { } date ? date.ToString("yyyy-MM-dd HH:mm:ss") : null;

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        double kb = bytes / 1024.0;
        if (kb < 1024) return $"{kb:0.#} KB";
        double mb = kb / 1024.0;
        if (mb < 1024) return $"{mb:0.#} MB";
        return $"{mb / 1024.0:0.##} GB";
    }
}
