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
    private async Task<HashResult> HashAsync(
        IReadOnlyList<CandidateFile> files,
        bool includePerceptualHash,
        DuplicateScanPause pause,
        IProgress<DuplicateScanProgress>? progress,
        ConcurrentBag<DuplicateScanError> errors,
        CancellationToken cancellationToken)
    {
        var hashedFiles = new ConcurrentBag<HashedFile>();
        var completed = 0;
        var canceled = false;
        try
        {
            await Parallel.ForEachAsync(files, new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Clamp(Environment.ProcessorCount / 2, 2, 4)
            }, async (file, token) =>
            {
                try
                {
                    await pause.WaitIfPausedAsync(token).ConfigureAwait(false);
                    var cacheHit = _cache.TryGet(
                        file.Path, file.Identity, file.SizeBytes, file.ModifiedUtc,
                        requirePerceptualHash: false, out var cached);
                    var contentHash = cacheHit
                        ? cached.ContentHash
                        : await _hasher.ComputeContentHashAsync(file.Path, token)
                            .ConfigureAwait(false);
                    ulong perceptualHash = cached?.PerceptualHash ?? 0;
                    var width = cached?.Width ?? 0;
                    var height = cached?.Height ?? 0;

                    if (includePerceptualHash && cached?.PerceptualHash is null)
                    {
                        try
                        {
                            var visual = await _hasher.ComputePerceptualHashAsync(file.Path, token)
                                .ConfigureAwait(false);
                            perceptualHash = visual.Hash;
                            width = visual.Width;
                            height = visual.Height;
                        }
                        catch
                        {
                            Cache(file, contentHash, null, width, height);
                            throw;
                        }
                    }

                    Cache(
                        file, contentHash,
                        includePerceptualHash ? perceptualHash : cached?.PerceptualHash,
                        width, height);
                    hashedFiles.Add(new HashedFile(
                        file, contentHash, perceptualHash, width, height));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    errors.Add(new DuplicateScanError(file.Path, ex.Message));
                }
                finally
                {
                    var count = Interlocked.Increment(ref completed);
                    progress?.Report(new DuplicateScanProgress(
                        DuplicateScanStage.Hashing, count, files.Count, file.Path));
                }
            }).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            canceled = true;
        }
        finally
        {
            try { await _cache.SaveAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception ex)
            {
                errors.Add(new DuplicateScanError("Hash cache", ex.Message));
            }
        }

        return new HashResult(
            hashedFiles.OrderBy(file => file.File.Path, StringComparer.OrdinalIgnoreCase).ToList(),
            canceled);
    }

    private void Cache(
        CandidateFile file,
        string contentHash,
        ulong? perceptualHash,
        int width,
        int height) => _cache.Upsert(new DuplicateHashCacheEntry
    {
        Path = file.Path,
        Identity = file.Identity.Value,
        SizeBytes = file.SizeBytes,
        ModifiedUtcTicks = file.ModifiedUtc.Ticks,
        ContentHash = contentHash,
        PerceptualHash = perceptualHash,
        Width = width,
        Height = height
    });

    private sealed record HashResult(List<HashedFile> Files, bool IsCanceled);
}
