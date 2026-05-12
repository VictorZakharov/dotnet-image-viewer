using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ImageViewer.Services;

public sealed class AppSettings
{
    public double WindowX { get; set; } = double.NaN;
    public double WindowY { get; set; } = double.NaN;
    public double WindowWidth { get; set; }
    public double WindowHeight { get; set; }
    public bool WindowMaximized { get; set; }
    public string? LastFolder { get; set; }
    public int ThumbnailSize { get; set; } = 192;
    public string SortMode { get; set; } = "Name";
    public bool SortDescending { get; set; }
    public int SlideshowDelaySeconds { get; set; } = 5;
    public bool ShowExifOverlay { get; set; }
    public bool ShowExifPane { get; set; }
}

public static class SettingsStore
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ImageViewer");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        // WindowX/Y default to double.NaN as "unset" sentinels; without this
        // System.Text.Json throws on NaN and the whole save silently fails.
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals
    };

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt or unreadable — fall through to defaults.
        }
        return new AppSettings();
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence.
        }
    }
}
