using System;
using System.Globalization;
using System.IO;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ImageViewer.Services;

[SupportedOSPlatform("linux")]
internal static class LinuxTrash
{
    private static readonly UTF8Encoding Utf8WithoutBom = new(false);

    public static async Task<bool> MoveAsync(
        string path,
        string? trashRoot = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(path) && !Directory.Exists(path)) return false;

        var source = Path.GetFullPath(path);
        var isDirectory = Directory.Exists(source);
        var originalName = Path.GetFileName(
            Path.TrimEndingDirectorySeparator(source));
        if (string.IsNullOrEmpty(originalName)) return false;

        var root = trashRoot ?? GetHomeTrashRoot();
        var filesDirectory = Path.Combine(root, "files");
        var infoDirectory = Path.Combine(root, "info");
        CreatePrivateDirectory(root);
        CreatePrivateDirectory(filesDirectory);
        CreatePrivateDirectory(infoDirectory);

        for (var suffix = 1; suffix < int.MaxValue; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var trashName = CandidateName(originalName, isDirectory, suffix);
            var destination = Path.Combine(filesDirectory, trashName);
            var infoPath = Path.Combine(infoDirectory, trashName + ".trashinfo");
            if (SafeFileSystemTransfer.Exists(destination)) continue;

            if (!await TryReserveInfoAsync(infoPath, source, cancellationToken)
                    .ConfigureAwait(false))
            {
                continue;
            }

            try
            {
                await SafeFileSystemTransfer.MoveAsync(
                    source,
                    destination,
                    replace: false,
                    cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch
            {
                try { File.Delete(infoPath); } catch { /* best effort */ }
                if (SafeFileSystemTransfer.Exists(source)
                    && SafeFileSystemTransfer.Exists(destination))
                {
                    try { SafeFileSystemTransfer.DeletePermanently(destination); }
                    catch { /* preserve the original error */ }
                }
                throw;
            }
        }

        throw new IOException("Could not reserve a unique name in the Trash.");
    }

    internal static string GetHomeTrashRoot()
    {
        var dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathRooted(dataHome))
        {
            dataHome = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
        }
        if (string.IsNullOrWhiteSpace(dataHome))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            dataHome = Path.Combine(home, ".local", "share");
        }
        return Path.Combine(dataHome, "Trash");
    }

    private static async Task<bool> TryReserveInfoAsync(
        string infoPath,
        string source,
        CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = new FileStream(
                infoPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous);
            var contents = "[Trash Info]\n"
                + $"Path={new Uri(source).AbsolutePath}\n"
                + $"DeletionDate={DateTime.Now.ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture)}\n";
            var bytes = Utf8WithoutBom.GetBytes(contents);
            await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (IOException) when (File.Exists(infoPath))
        {
            return false;
        }
    }

    private static string CandidateName(string originalName, bool isDirectory, int suffix)
    {
        if (suffix == 1) return originalName;
        if (isDirectory) return $"{originalName} ({suffix})";
        var stem = Path.GetFileNameWithoutExtension(originalName);
        var extension = Path.GetExtension(originalName);
        return $"{stem} ({suffix}){extension}";
    }

    private static void CreatePrivateDirectory(string path)
    {
        Directory.CreateDirectory(path);
        try
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
        catch
        {
            // Permissions are still constrained by the user's umask.
        }
    }
}
