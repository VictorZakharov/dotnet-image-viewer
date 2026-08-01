using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

public sealed record FolderScanEntry(string Path, IReadOnlyList<string> PreviewImages);

public sealed record BrowserScanResult(
    IReadOnlyList<FolderScanEntry> Folders,
    IReadOnlyList<string> Images);

public static class FolderScanner
{
    public static async Task<List<string>> ScanAsync(string folder, CancellationToken ct = default)
    {
        return await Task.Run(() => ScanImages(folder, ct), ct).ConfigureAwait(false);
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

                    var previewImages = ScanImages(directory, ct, maxCount: 4);
                    folders.Add(new FolderScanEntry(directory, previewImages));
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

            var images = ScanImages(folder, ct);
            return new BrowserScanResult(folders, images);
        }, ct).ConfigureAwait(false);
    }

    private static List<string> ScanImages(
        string folder,
        CancellationToken ct,
        int maxCount = int.MaxValue)
    {
        var result = new List<string>();
        try
        {
            foreach (var file in Directory.EnumerateFiles(folder))
            {
                ct.ThrowIfCancellationRequested();
                if (ImageLoader.IsSupportedExtension(Path.GetExtension(file)))
                {
                    result.Add(file);
                    if (result.Count >= maxCount) break;
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            // Folder inaccessible - return what we have.
        }

        result.Sort(StringComparer.OrdinalIgnoreCase);
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
