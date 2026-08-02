using System;
using System.Diagnostics;
using System.IO;

namespace ImageViewer.Services;

public static class WindowsIntegrationLauncher
{
    private const string DefaultAppsUri =
        "ms-settings:defaultapps?registeredAppUser=ImageViewer";

    public static string GetExecutablePath() =>
        TryGetExecutablePath()
        ?? throw new InvalidOperationException("ImageViewer.exe could not be located.");

    public static string? TryGetExecutablePath()
    {
        var appHost = Path.Combine(AppContext.BaseDirectory, "ImageViewer.exe");
        if (File.Exists(appHost)) return Path.GetFullPath(appHost);

        var processPath = Environment.ProcessPath;
        return processPath is not null
            && processPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            && !Path.GetFileName(processPath).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase)
                ? Path.GetFullPath(processPath)
                : null;
    }

    public static bool OpenDefaultApps()
    {
        if (!OperatingSystem.IsWindows()) return false;
        try
        {
            Process.Start(new ProcessStartInfo(DefaultAppsUri)
            {
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }
}
