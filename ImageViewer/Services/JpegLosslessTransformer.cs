using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

public static class JpegLosslessTransformer
{
    private static readonly Lazy<string?> Executable = new(FindExecutable);

    public static bool IsAvailable => Executable.Value is not null;

    public static async Task RotateAsync(
        string sourcePath,
        string targetPath,
        int degrees,
        CancellationToken cancellationToken)
    {
        var executable = Executable.Value
            ?? throw new NotSupportedException("jpegtran is not available on PATH.");
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        process.StartInfo.ArgumentList.Add("-copy");
        process.StartInfo.ArgumentList.Add("all");
        process.StartInfo.ArgumentList.Add("-perfect");
        process.StartInfo.ArgumentList.Add("-rotate");
        process.StartInfo.ArgumentList.Add(degrees.ToString(System.Globalization.CultureInfo.InvariantCulture));
        process.StartInfo.ArgumentList.Add("-outfile");
        process.StartInfo.ArgumentList.Add(targetPath);
        process.StartInfo.ArgumentList.Add(sourcePath);

        if (!process.Start()) throw new IOException("Could not start jpegtran.");
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        var error = await process.StandardError.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        if (process.ExitCode != 0)
            throw new IOException(string.IsNullOrWhiteSpace(error)
                ? $"jpegtran exited with code {process.ExitCode}."
                : error.Trim());
    }

    private static string? FindExecutable()
    {
        var name = OperatingSystem.IsWindows() ? "jpegtran.exe" : "jpegtran";
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "")
                     .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(folder.Trim('"'), name);
                if (File.Exists(candidate)) return candidate;
            }
            catch { }
        }
        return null;
    }
}
