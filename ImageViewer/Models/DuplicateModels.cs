using System;
using System.Collections.Generic;
using ImageViewer.Services;

namespace ImageViewer.Models;

public enum DuplicateScanMode
{
    Exact,
    Similar
}

public enum DuplicateGroupKind
{
    Exact,
    Similar
}

public enum DuplicateSortMode
{
    ReclaimableSpace,
    GroupSize,
    Date
}

public enum DuplicateScanStage
{
    Enumerating,
    Hashing,
    Comparing,
    ReadingMetadata,
    Complete
}

public sealed record DuplicateScanOptions(
    IReadOnlyList<string> RootFolders,
    DuplicateScanMode Mode,
    int SimilarityThreshold = 8);

public sealed record DuplicateScanProgress(
    DuplicateScanStage Stage,
    int Completed,
    int Total,
    string CurrentPath)
{
    public bool IsIndeterminate => Total <= 0;
    public double Percentage => Total <= 0 ? 0 : Completed * 100d / Total;
}

public sealed record DuplicateScanError(string Path, string Error);

public sealed record HardLinkAlias(string CanonicalPath, string AliasPath);

public sealed class DuplicateFileEntry
{
    public required string Path { get; init; }
    public required string ContentHash { get; init; }
    public required ulong PerceptualHash { get; init; }
    public required long SizeBytes { get; init; }
    public required DateTime CreatedUtc { get; init; }
    public required DateTime ModifiedUtc { get; init; }
    public required DateTime AccessedUtc { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }
    public DateTime? TakenAt { get; init; }
    public string? Camera { get; init; }
    public string? Lens { get; init; }
    public string? Exposure { get; init; }
}

public sealed class DuplicateGroup
{
    public required DuplicateGroupKind Kind { get; init; }
    public required IReadOnlyList<DuplicateFileEntry> Files { get; init; }
    public required string SuggestedKeeperPath { get; init; }
    public required string KeeperReason { get; init; }
    public required int SimilarityThreshold { get; init; }
    public required int MaximumDistance { get; init; }

    public long ReclaimableBytes => Files.SumSizesExcept(SuggestedKeeperPath);
    public DateTime NewestDateUtc => Files.MaxDate();
}

public sealed record DuplicateScanResult(
    IReadOnlyList<DuplicateGroup> Groups,
    IReadOnlyList<HardLinkAlias> HardLinks,
    IReadOnlyList<DuplicateScanError> Errors,
    bool IsCanceled,
    int ScannedFileCount);

internal static class DuplicateModelExtensions
{
    public static long SumSizesExcept(
        this IReadOnlyList<DuplicateFileEntry> files,
        string keeperPath)
    {
        long total = 0;
        foreach (var file in files)
            if (!FileSystemPath.Equals(file.Path, keeperPath))
                total += file.SizeBytes;
        return total;
    }

    public static DateTime MaxDate(this IReadOnlyList<DuplicateFileEntry> files)
    {
        var newest = DateTime.MinValue;
        foreach (var file in files)
        {
            var date = file.TakenAt?.ToUniversalTime() ?? file.ModifiedUtc;
            if (date > newest) newest = date;
        }
        return newest;
    }
}
