using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

public static class FolderScanner
{
    public static async Task<List<string>> ScanAsync(string folder, CancellationToken ct = default)
    {
        return await Task.Run(() =>
        {
            var result = new List<string>();
            try
            {
                foreach (var file in Directory.EnumerateFiles(folder))
                {
                    ct.ThrowIfCancellationRequested();
                    if (ImageLoader.IsSupportedExtension(Path.GetExtension(file)))
                        result.Add(file);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch
            {
                // Folder inaccessible — return what we have.
            }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }, ct).ConfigureAwait(false);
    }
}
