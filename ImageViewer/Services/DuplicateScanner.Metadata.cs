using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageMagick;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class DuplicateScanner
{
    private async Task<List<DuplicateGroup>> ReadMetadataAsync(
        IReadOnlyList<GroupCandidate> candidates,
        int threshold,
        DuplicateScanPause pause,
        IProgress<DuplicateScanProgress>? progress,
        ConcurrentBag<DuplicateScanError> errors,
        CancellationToken cancellationToken)
    {
        var total = candidates.Sum(group => group.Files.Count);
        var completed = 0;
        var groups = new List<DuplicateGroup>(candidates.Count);
        foreach (var candidate in candidates)
        {
            var entries = new List<DuplicateFileEntry>(candidate.Files.Count);
            foreach (var file in candidate.Files)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await pause.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
                entries.Add(ReadEntry(file, errors));
                progress?.Report(new DuplicateScanProgress(
                    DuplicateScanStage.ReadingMetadata,
                    ++completed, total, file.File.Path));
            }

            var keeper = ChooseKeeper(entries, candidate.Kind);
            groups.Add(new DuplicateGroup
            {
                Kind = candidate.Kind,
                Files = entries,
                SuggestedKeeperPath = keeper.Path,
                KeeperReason = candidate.Kind == DuplicateGroupKind.Exact
                    ? "Oldest creation date, then oldest modified date and shortest path."
                    : "Highest resolution, then largest file, oldest date and shortest path.",
                SimilarityThreshold = candidate.Kind == DuplicateGroupKind.Similar ? threshold : 0,
                MaximumDistance = candidate.MaximumDistance
            });
        }

        return groups
            .OrderByDescending(group => group.ReclaimableBytes)
            .ThenByDescending(group => group.Files.Count)
            .ToList();
    }

    private static DuplicateFileEntry ReadEntry(
        HashedFile file,
        ConcurrentBag<DuplicateScanError> errors)
    {
        var metadata = ExifReader.Read(file.File.Path);
        var width = file.Width > 0 ? file.Width : metadata.Width ?? 0;
        var height = file.Height > 0 ? file.Height : metadata.Height ?? 0;
        if (width == 0 || height == 0)
        {
            try
            {
                var info = new MagickImageInfo(file.File.Path);
                width = checked((int)info.Width);
                height = checked((int)info.Height);
            }
            catch (Exception ex)
            {
                errors.Add(new DuplicateScanError(
                    file.File.Path, $"Dimensions: {ex.Message}"));
            }
        }

        return new DuplicateFileEntry
        {
            Path = file.File.Path,
            ContentHash = file.ContentHash,
            PerceptualHash = file.PerceptualHash,
            SizeBytes = file.File.SizeBytes,
            CreatedUtc = file.File.CreatedUtc,
            ModifiedUtc = file.File.ModifiedUtc,
            AccessedUtc = file.File.AccessedUtc,
            Width = width,
            Height = height,
            TakenAt = metadata.TakenAt,
            Camera = metadata.CameraSummary,
            Lens = metadata.Lens,
            Exposure = metadata.ExposureSummary
        };
    }

    private static DuplicateFileEntry ChooseKeeper(
        IReadOnlyList<DuplicateFileEntry> files,
        DuplicateGroupKind kind)
    {
        IOrderedEnumerable<DuplicateFileEntry> ordered = kind == DuplicateGroupKind.Exact
            ? files.OrderBy(file => file.CreatedUtc)
                .ThenBy(file => file.ModifiedUtc)
            : files.OrderByDescending(file => (long)file.Width * file.Height)
                .ThenByDescending(file => file.SizeBytes)
                .ThenBy(file => file.TakenAt ?? file.CreatedUtc.ToLocalTime());
        return ordered
            .ThenBy(file => file.Path.Length)
            .ThenBy(file => file.Path, StringComparer.OrdinalIgnoreCase)
            .First();
    }
}
