using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

public sealed class DuplicateHashCache
{
    private readonly object _gate = new();
    private readonly string _path;
    private Dictionary<string, DuplicateHashCacheEntry> _entries =
        new(FileSystemPath.Comparer);

    public DuplicateHashCache(string? customPath = null)
    {
        _path = customPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImageViewer",
            "duplicate-hashes.json");
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path)) return;
            await using var stream = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.Read,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var document = await JsonSerializer.DeserializeAsync(
                stream,
                DuplicateHashCacheJsonContext.Default.DuplicateHashCacheDocument,
                cancellationToken).ConfigureAwait(false);
            if (document is null) return;

            lock (_gate)
            {
                _entries = new Dictionary<string, DuplicateHashCacheEntry>(
                    FileSystemPath.Comparer);
                foreach (var entry in document.Entries)
                    _entries[Normalize(entry.Path)] = entry;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // An unreadable cache only costs scan time; source files remain untouched.
        }
    }

    public bool TryGet(
        string path,
        FileIdentity identity,
        long size,
        DateTime modifiedUtc,
        bool requirePerceptualHash,
        out DuplicateHashCacheEntry entry)
    {
        lock (_gate)
        {
            if (_entries.TryGetValue(Normalize(path), out var cached)
                && cached.Identity == identity.Value
                && cached.SizeBytes == size
                && cached.ModifiedUtcTicks == modifiedUtc.Ticks
                && !string.IsNullOrEmpty(cached.ContentHash)
                && (!requirePerceptualHash || cached.PerceptualHash is not null))
            {
                entry = cached;
                return true;
            }
        }

        entry = null!;
        return false;
    }

    public void Upsert(DuplicateHashCacheEntry entry)
    {
        lock (_gate) _entries[Normalize(entry.Path)] = entry;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        DuplicateHashCacheDocument snapshot;
        lock (_gate)
            snapshot = new DuplicateHashCacheDocument { Entries = [.. _entries.Values] };

        var directory = Path.GetDirectoryName(_path);
        if (string.IsNullOrEmpty(directory)) return;
        Directory.CreateDirectory(directory);
        var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var stream = new FileStream(
                tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshot,
                    DuplicateHashCacheJsonContext.Default.DuplicateHashCacheDocument,
                    cancellationToken).ConfigureAwait(false);
            }
            File.Move(tempPath, _path, overwrite: true);
        }
        finally
        {
            try { File.Delete(tempPath); } catch { /* best effort */ }
        }
    }

    private static string Normalize(string path) => Path.GetFullPath(path);
}
