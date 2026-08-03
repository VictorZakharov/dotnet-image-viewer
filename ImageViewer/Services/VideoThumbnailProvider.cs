using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace ImageViewer.Services;

internal static class VideoThumbnailProvider
{
    public static async Task<Bitmap?> TryGetAsync(
        string path,
        int dimension,
        CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            return await Task.Run(
                () => ShellThumbnailProvider.TryGet(path, dimension),
                cancellationToken).ConfigureAwait(false);
        }

        if (!OperatingSystem.IsLinux()) return null;

        var encoded = await TryGetLinuxPngAsync(path, dimension, cancellationToken)
            .ConfigureAwait(false);
        if (encoded is null) return null;
        try
        {
            using var input = new MemoryStream(encoded, writable: false);
            return new Bitmap(input);
        }
        catch
        {
            return null;
        }
    }

    internal static async Task<byte[]?> TryGetLinuxPngAsync(
        string path,
        int dimension,
        CancellationToken cancellationToken) =>
        await TryExtractWithFfmpegAsync(
            path,
            dimension,
            seekSeconds: 1,
            cancellationToken).ConfigureAwait(false)
        ?? await TryExtractWithFfmpegAsync(
            path,
            dimension,
            seekSeconds: 0,
            cancellationToken).ConfigureAwait(false);

    private static async Task<byte[]?> TryExtractWithFfmpegAsync(
        string path,
        int dimension,
        int seekSeconds,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = CreateStartInfo(path, dimension, seekSeconds)
        };

        try
        {
            if (!process.Start()) return null;
        }
        catch
        {
            return null;
        }

        await using var output = new MemoryStream();
        try
        {
            var copyOutput = process.StandardOutput.BaseStream.CopyToAsync(
                output,
                cancellationToken);
            var readError = process.StandardError.ReadToEndAsync(cancellationToken);
            await Task.WhenAll(
                copyOutput,
                readError,
                process.WaitForExitAsync(cancellationToken)).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }
        catch
        {
            TryKill(process);
            return null;
        }

        return process.ExitCode == 0 && output.Length > 0
            ? output.ToArray()
            : null;
    }

    private static ProcessStartInfo CreateStartInfo(
        string path,
        int dimension,
        int seekSeconds)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("-hide_banner");
        startInfo.ArgumentList.Add("-loglevel");
        startInfo.ArgumentList.Add("error");
        startInfo.ArgumentList.Add("-nostdin");
        if (seekSeconds > 0)
        {
            startInfo.ArgumentList.Add("-ss");
            startInfo.ArgumentList.Add(seekSeconds.ToString());
        }
        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(path);
        startInfo.ArgumentList.Add("-frames:v");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-an");
        startInfo.ArgumentList.Add("-sn");
        startInfo.ArgumentList.Add("-dn");
        startInfo.ArgumentList.Add("-vf");
        startInfo.ArgumentList.Add(
            $"scale={dimension}:{dimension}:force_original_aspect_ratio=decrease");
        startInfo.ArgumentList.Add("-f");
        startInfo.ArgumentList.Add("image2pipe");
        // Folder mosaics can launch several extractions at once. The PNG
        // encoder otherwise creates its own worker pool for every process and
        // can fail under container/user thread limits.
        startInfo.ArgumentList.Add("-threads:v");
        startInfo.ArgumentList.Add("1");
        startInfo.ArgumentList.Add("-vcodec");
        startInfo.ArgumentList.Add("png");
        startInfo.ArgumentList.Add("pipe:1");
        return startInfo;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch
        {
            // The process may have exited between the checks.
        }
    }
}
