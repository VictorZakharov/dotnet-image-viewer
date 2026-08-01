using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

public sealed record MediaScanEntry(string Path, bool IsVideo);

public sealed record FolderScanEntry(string Path, IReadOnlyList<MediaScanEntry> PreviewMedia);

public sealed record BrowserScanResult(
    IReadOnlyList<FolderScanEntry> Folders,
    IReadOnlyList<MediaScanEntry> Media);

public static class FolderScanner
{
    public static async Task<List<MediaScanEntry>> ScanAsync(string folder, CancellationToken ct = default)
    {
        return await Task.Run(() => ScanMedia(folder, ct), ct).ConfigureAwait(false);
    }

    public static async Task<BrowserScanResult> ScanBrowserAsync(
        string folder,
        CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var folders = new List<FolderScanEntry>();
            try
            {
                foreach (var directory in Directory.EnumerateDirectories(folder))
                {
                    ct.ThrowIfCancellationRequested();
                    if (IsHiddenOrSystem(directory)) continue;

                    var previewMedia = ScanMedia(directory, ct, maxCount: 4);
                    folders.Add(new FolderScanEntry(directory, previewMedia));
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Folder inaccessible - return what we have.
            }

            folders.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(
                Path.GetFileName(left.Path),
                Path.GetFileName(right.Path)));

            var media = ScanMedia(folder, ct);
            return new BrowserScanResult(folders, media);
        }, ct).ConfigureAwait(false);
    }

    private static List<MediaScanEntry> ScanMedia(
        string folder,
        CancellationToken ct,
        int maxCount = int.MaxValue)
    {
        var result = new List<MediaScanEntry>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                ct.ThrowIfCancellationRequested();
                if (MediaFileTypes.IsSupported(file))
                {
                    result.Add(new MediaScanEntry(file, MediaFileTypes.IsVideo(file)));
                    if (result.Count >= maxCount) break;
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Folder inaccessible - return what we have.
        }

        result.Sort((left, right) =>
            StringComparer.OrdinalIgnoreCase.Compare(left.Path, right.Path));
        return result;
    }

    private static bool IsHiddenOrSystem(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0;
        }
        catch
        {
            return true;
        }
    }
}
