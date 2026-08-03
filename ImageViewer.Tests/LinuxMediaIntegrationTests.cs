using System.Diagnostics;
using ImageViewer.Services;
using ImageMagick;
using LibVLCSharp.Shared;

namespace ImageViewer.Tests;

public sealed class LinuxMediaIntegrationTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "ImageViewer-LinuxMediaTests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task FfmpegCreatesVideoThumbnail()
    {
        if (!OperatingSystem.IsLinux()) return;
        var video = await CreateVideoAsync();

        var encoded = await VideoThumbnailProvider.TryGetLinuxPngAsync(
            video,
            128,
            CancellationToken.None);

        Assert.NotNull(encoded);
        using var thumbnail = new MagickImage(encoded);
        Assert.InRange(thumbnail.Width, 1u, 128u);
        Assert.InRange(thumbnail.Height, 1u, 128u);
    }

    [Fact]
    public async Task SystemLibVlcCanDecodeVideo()
    {
        if (!OperatingSystem.IsLinux()) return;
        var video = await CreateVideoAsync();
        Core.Initialize();
        using var libVlc = new LibVLC("--quiet", "--vout=dummy", "--aout=dummy");
        using var media = new Media(libVlc, new Uri(video));
        using var player = new MediaPlayer(libVlc);

        Assert.True(player.Play(media));
        var decoded = SpinWait.SpinUntil(
            () => player.State is VLCState.Playing or VLCState.Ended or VLCState.Error,
            TimeSpan.FromSeconds(8));
        Assert.True(decoded);
        Assert.NotEqual(VLCState.Error, player.State);
        player.Stop();
    }

    private async Task<string> CreateVideoAsync()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "sample.mp4");
        if (File.Exists(path)) return path;

        var startInfo = new ProcessStartInfo
        {
            FileName = "ffmpeg",
            UseShellExecute = false,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[]
                 {
                     "-hide_banner", "-loglevel", "error", "-nostdin",
                     "-f", "lavfi", "-i", "color=c=blue:s=320x180:d=2",
                     "-c:v", "mpeg4", "-y", path
                 })
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Could not start ffmpeg.");
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, error);
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); }
        catch { /* best-effort test cleanup */ }
    }
}
