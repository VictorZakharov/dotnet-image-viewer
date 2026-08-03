using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class DuplicateScanner
{
    private async Task<List<GroupCandidate>> GroupExactAsync(
        IReadOnlyList<HashedFile> files,
        DuplicateScanPause pause,
        IProgress<DuplicateScanProgress>? progress,
        ConcurrentBag<DuplicateScanError> errors,
        CancellationToken cancellationToken)
    {
        var hashBuckets = files
            .GroupBy(file => (file.File.SizeBytes, file.ContentHash))
            .Where(group => group.Count() > 1)
            .ToList();
        var result = new List<GroupCandidate>();

        for (var bucketIndex = 0; bucketIndex < hashBuckets.Count; bucketIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await pause.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            var partitions = new List<List<HashedFile>>();
            foreach (var file in hashBuckets[bucketIndex])
                await AddToVerifiedPartitionAsync(
                    file, partitions, errors, cancellationToken).ConfigureAwait(false);

            result.AddRange(partitions
                .Where(partition => partition.Count > 1)
                .Select(partition => new GroupCandidate(
                    DuplicateGroupKind.Exact, partition, 0)));
            progress?.Report(new DuplicateScanProgress(
                DuplicateScanStage.Comparing,
                bucketIndex + 1, hashBuckets.Count, hashBuckets[bucketIndex].First().File.Path));
        }
        return result;
    }

    private async Task AddToVerifiedPartitionAsync(
        HashedFile file,
        List<List<HashedFile>> partitions,
        ConcurrentBag<DuplicateScanError> errors,
        CancellationToken cancellationToken)
    {
        foreach (var partition in partitions)
        {
            try
            {
                if (await _hasher.FilesAreEqualAsync(
                    file.File.Path, partition[0].File.Path, cancellationToken)
                    .ConfigureAwait(false))
                {
                    partition.Add(file);
                    return;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                errors.Add(new DuplicateScanError(file.File.Path, ex.Message));
                return;
            }
        }
        partitions.Add([file]);
    }

    private async Task<List<GroupCandidate>> GroupSimilarAsync(
        IReadOnlyList<HashedFile> files,
        int threshold,
        DuplicateScanPause pause,
        IProgress<DuplicateScanProgress>? progress,
        ConcurrentBag<DuplicateScanError> errors,
        CancellationToken cancellationToken)
    {
        var index = new PerceptualHashIndex();
        var sets = new DisjointSets(files.Count);
        var matches = new List<int>();
        for (var fileIndex = 0; fileIndex < files.Count; fileIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await pause.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            matches.Clear();
            index.FindWithin(files[fileIndex].PerceptualHash, threshold, matches);
            foreach (var match in matches) sets.Union(fileIndex, match);
            index.Add(files[fileIndex].PerceptualHash, fileIndex);
            progress?.Report(new DuplicateScanProgress(
                DuplicateScanStage.Comparing,
                fileIndex + 1, files.Count, files[fileIndex].File.Path));
        }

        var clusters = Enumerable.Range(0, files.Count)
            .GroupBy(sets.Find)
            .Select(group => group.Select(indexValue => files[indexValue]).ToList())
            .Where(group => group.Count > 1)
            .ToList();
        var result = new List<GroupCandidate>(clusters.Count);
        foreach (var cluster in clusters)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var exact = await AllByteIdenticalAsync(
                cluster, errors, cancellationToken).ConfigureAwait(false);
            result.Add(new GroupCandidate(
                exact ? DuplicateGroupKind.Exact : DuplicateGroupKind.Similar,
                cluster,
                MaximumDistance(cluster)));
        }
        return result;
    }

    private async Task<bool> AllByteIdenticalAsync(
        IReadOnlyList<HashedFile> files,
        ConcurrentBag<DuplicateScanError> errors,
        CancellationToken cancellationToken)
    {
        if (files.Any(file => file.File.SizeBytes != files[0].File.SizeBytes
                              || file.ContentHash != files[0].ContentHash)) return false;
        for (var index = 1; index < files.Count; index++)
        {
            try
            {
                if (!await _hasher.FilesAreEqualAsync(
                    files[0].File.Path, files[index].File.Path, cancellationToken)
                    .ConfigureAwait(false)) return false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                errors.Add(new DuplicateScanError(files[index].File.Path, ex.Message));
                return false;
            }
        }
        return true;
    }

    private static int MaximumDistance(IReadOnlyList<HashedFile> files)
    {
        var maximum = 0;
        for (var left = 0; left < files.Count; left++)
            for (var right = left + 1; right < files.Count; right++)
                maximum = Math.Max(maximum, DuplicateImageHasher.Distance(
                    files[left].PerceptualHash, files[right].PerceptualHash));
        return maximum;
    }
}
