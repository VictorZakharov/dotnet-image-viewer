using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using ImageMagick;

namespace ImageViewer.Services;

public sealed class ThumbnailCache
{
    private readonly string _cacheDir;
    private readonly SemaphoreSlim _semaphore;

    public ThumbnailCache(string? customDir = null)
    {
        _cacheDir = customDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ImageViewer", "thumbs");
        _semaphore = new SemaphoreSlim(Math.Max(2, Environment.ProcessorCount / 2));
    }

    public async Task<Bitmap?> GetOrCreateAsync(string imagePath, int requestedDim, CancellationToken ct = default)
        => await GetOrCreateAsync(imagePath, requestedDim, isVideo: false, ct).ConfigureAwait(false);

    public async Task<Bitmap?> GetOrCreateAsync(
        string imagePath,
        int requestedDim,
        bool isVideo,
        CancellationToken ct = default)
    {
        FileInfo fi;
        try { fi = new FileInfo(imagePath); }
        catch { return null; }
        if (!fi.Exists) return null;

        var dim = Math.Max(64, requestedDim);
        string key = ComputeKey(imagePath, fi.LastWriteTimeUtc, fi.Length, dim);
        string thumbPath = Path.Combine(_cacheDir, key + (isVideo ? ".png" : ".jpg"));

        if (File.Exists(thumbPath))
        {
            try
            {
                await using var fs = File.OpenRead(thumbPath);
                return new Bitmap(fs);
            }
            catch
            {
                try { File.Delete(thumbPath); } catch { /* ignored */ }
            }
        }

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    ct.ThrowIfCancellationRequested();
                    if (isVideo)
                    {
                        var thumbnail = ShellThumbnailProvider.TryGet(imagePath, dim);
                        if (thumbnail is null) return null;

                        try
                        {
                            Directory.CreateDirectory(_cacheDir);
                            using var output = File.Create(thumbPath);
                            thumbnail.Save(output);
                        }
                        catch { /* cache write failure is non-fatal */ }

                        return thumbnail;
                    }

                    using var img = new MagickImage(imagePath);
                    ct.ThrowIfCancellationRequested();
                    img.AutoOrient();
                    img.Thumbnail((uint)dim, (uint)dim);
                    img.Quality = 85;

                    using var ms = new MemoryStream();
                    img.Write(ms, MagickFormat.Jpg);
                    ct.ThrowIfCancellationRequested();

                    try
                    {
                        Directory.CreateDirectory(_cacheDir);
                        File.WriteAllBytes(thumbPath, ms.ToArray());
                    }
                    catch { /* cache write failure is non-fatal */ }

                    ms.Position = 0;
                    return new Bitmap(ms);
                }
                catch (OperationCanceledException) { throw; }
                catch { return (Bitmap?)null; }
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void Cleanup(long maxBytes = 200L * 1024 * 1024)
    {
        try
        {
            var files = Directory.EnumerateFiles(_cacheDir)
                .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                            || f.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .Select(f => new FileInfo(f))
                .OrderBy(f => f.LastAccessTimeUtc)
                .ToList();
            long total = files.Sum(f => f.Length);
            foreach (var f in files)
            {
                if (total <= maxBytes) break;
                try
                {
                    long len = f.Length;
                    f.Delete();
                    total -= len;
                }
                catch { /* skip locked file */ }
            }
        }
        catch { /* cleanup is best-effort */ }
    }

    private static string ComputeKey(string path, DateTime mtime, long size, int dim)
    {
        var input = $"{path.ToLowerInvariant()}|{mtime.Ticks}|{size}|{dim}";
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA1.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
