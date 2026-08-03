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
    private readonly DuplicateHashCache _cache;
    private readonly IFileIdentityProvider _identityProvider;
    private readonly DuplicateImageHasher _hasher;

    public DuplicateScanner(
        DuplicateHashCache? cache = null,
        IFileIdentityProvider? identityProvider = null,
        DuplicateImageHasher? hasher = null)
    {
        _cache = cache ?? new DuplicateHashCache();
        _identityProvider = identityProvider ?? new FileIdentityProvider();
        _hasher = hasher ?? new DuplicateImageHasher();
    }

    public Task<DuplicateScanResult> ScanAsync(
        DuplicateScanOptions options,
        DuplicateScanPause pause,
        IProgress<DuplicateScanProgress>? progress,
        CancellationToken cancellationToken) => Task.Run(
            () => ScanCoreAsync(options, pause, progress, cancellationToken),
            CancellationToken.None);

    private async Task<DuplicateScanResult> ScanCoreAsync(
        DuplicateScanOptions options,
        DuplicateScanPause pause,
        IProgress<DuplicateScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        var errors = new ConcurrentBag<DuplicateScanError>();
        var hardLinks = new List<HardLinkAlias>();
        var fileCount = 0;
        try
        {
            await _cache.LoadAsync(cancellationToken).ConfigureAwait(false);
            var enumeration = await EnumerateAsync(
                options.RootFolders, pause, progress, errors, hardLinks,
                cancellationToken).ConfigureAwait(false);
            fileCount = enumeration.Files.Count + hardLinks.Count;
            if (enumeration.IsCanceled)
                return Result([], hardLinks, errors, true, fileCount);

            var filesToHash = options.Mode == DuplicateScanMode.Exact
                ? enumeration.Files
                    .GroupBy(file => file.SizeBytes)
                    .Where(group => group.Count() > 1)
                    .SelectMany(group => group)
                    .ToList()
                : enumeration.Files;

            var hashing = await HashAsync(
                filesToHash, options.Mode == DuplicateScanMode.Similar,
                pause, progress, errors, cancellationToken).ConfigureAwait(false);
            if (hashing.IsCanceled)
                return Result([], hardLinks, errors, true, fileCount);

            var threshold = Math.Clamp(options.SimilarityThreshold, 0, 20);
            var candidates = options.Mode == DuplicateScanMode.Exact
                ? await GroupExactAsync(
                    hashing.Files, pause, progress, errors, cancellationToken)
                    .ConfigureAwait(false)
                : await GroupSimilarAsync(
                    hashing.Files, threshold, pause, progress, errors, cancellationToken)
                    .ConfigureAwait(false);

            var groups = await ReadMetadataAsync(
                candidates, threshold, pause, progress, errors, cancellationToken)
                .ConfigureAwait(false);
            progress?.Report(new DuplicateScanProgress(
                DuplicateScanStage.Complete, groups.Count, groups.Count, ""));
            return Result(groups, hardLinks, errors, false, fileCount);
        }
        catch (OperationCanceledException)
        {
            return Result([], hardLinks, errors, true, fileCount);
        }
    }

    private static DuplicateScanResult Result(
        IReadOnlyList<DuplicateGroup> groups,
        IReadOnlyList<HardLinkAlias> hardLinks,
        IEnumerable<DuplicateScanError> errors,
        bool canceled,
        int fileCount) => new(
            groups,
            hardLinks,
            errors.OrderBy(error => error.Path, StringComparer.OrdinalIgnoreCase).ToList(),
            canceled,
            fileCount);

    private sealed record CandidateFile(
        string Path,
        FileIdentity Identity,
        long SizeBytes,
        DateTime CreatedUtc,
        DateTime ModifiedUtc,
        DateTime AccessedUtc);

    private sealed record HashedFile(
        CandidateFile File,
        string ContentHash,
        ulong PerceptualHash,
        int Width,
        int Height);

    private sealed record GroupCandidate(
        DuplicateGroupKind Kind,
        IReadOnlyList<HashedFile> Files,
        int MaximumDistance);
}
