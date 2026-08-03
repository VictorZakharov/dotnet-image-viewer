using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ImageViewer.Models;

namespace ImageViewer.Services;

public sealed partial class DuplicateScanner
{
    private async Task<EnumerationResult> EnumerateAsync(
        IReadOnlyList<string> requestedRoots,
        DuplicateScanPause pause,
        IProgress<DuplicateScanProgress>? progress,
        ConcurrentBag<DuplicateScanError> errors,
        List<HardLinkAlias> hardLinks,
        CancellationToken cancellationToken)
    {
        var files = new List<CandidateFile>();
        var identities = new Dictionary<string, string>(StringComparer.Ordinal);
        var roots = NormalizeRoots(requestedRoots, errors);
        var pending = new Stack<string>(roots.Reverse());

        while (pending.Count > 0)
        {
            if (cancellationToken.IsCancellationRequested)
                return new EnumerationResult(files, true);
            await pause.WaitIfPausedAsync(cancellationToken).ConfigureAwait(false);
            var folder = pending.Pop();
            progress?.Report(new DuplicateScanProgress(
                DuplicateScanStage.Enumerating, files.Count, 0, folder));

            foreach (var child in ReadDirectories(folder, errors))
                if (!IsReparsePoint(child, errors)) pending.Push(child);

            foreach (var path in ReadFiles(folder, errors))
            {
                if (!MediaFileTypes.IsImage(path)) continue;
                try
                {
                    var info = new FileInfo(path);
                    var identity = _identityProvider.Get(path);
                    if (identities.TryGetValue(identity.Value, out var canonicalPath))
                    {
                        hardLinks.Add(new HardLinkAlias(canonicalPath, path));
                        continue;
                    }
                    identities[identity.Value] = path;
                    files.Add(new CandidateFile(
                        path, identity, info.Length,
                        info.CreationTimeUtc, info.LastWriteTimeUtc, info.LastAccessTimeUtc));
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    errors.Add(new DuplicateScanError(path, ex.Message));
                }
            }
        }

        return new EnumerationResult(files, false);
    }

    private static IReadOnlyList<string> NormalizeRoots(
        IReadOnlyList<string> requestedRoots,
        ConcurrentBag<DuplicateScanError> errors)
    {
        var valid = new List<string>();
        foreach (var requested in requestedRoots)
        {
            try
            {
                var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(requested));
                if (!Directory.Exists(root))
                {
                    errors.Add(new DuplicateScanError(root, "Folder does not exist."));
                    continue;
                }
                if (valid.Any(existing => IsSameOrChild(root, existing))) continue;
                valid.RemoveAll(existing => IsSameOrChild(existing, root));
                valid.Add(root);
            }
            catch (Exception ex)
            {
                errors.Add(new DuplicateScanError(requested, ex.Message));
            }
        }
        return valid;
    }

    private static bool IsSameOrChild(string path, string possibleParent)
    {
        return FileSystemPath.IsSameOrChild(path, possibleParent);
    }

    private static string[] ReadDirectories(
        string folder,
        ConcurrentBag<DuplicateScanError> errors)
    {
        try { return Directory.GetDirectories(folder); }
        catch (Exception ex)
        {
            errors.Add(new DuplicateScanError(folder, ex.Message));
            return [];
        }
    }

    private static string[] ReadFiles(
        string folder,
        ConcurrentBag<DuplicateScanError> errors)
    {
        try { return Directory.GetFiles(folder); }
        catch (Exception ex)
        {
            errors.Add(new DuplicateScanError(folder, ex.Message));
            return [];
        }
    }

    private static bool IsReparsePoint(
        string folder,
        ConcurrentBag<DuplicateScanError> errors)
    {
        try { return File.GetAttributes(folder).HasFlag(FileAttributes.ReparsePoint); }
        catch (Exception ex)
        {
            errors.Add(new DuplicateScanError(folder, ex.Message));
            return true;
        }
    }

    private sealed record EnumerationResult(List<CandidateFile> Files, bool IsCanceled);
}
