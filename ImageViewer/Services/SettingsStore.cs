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

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings)
                       ?? new AppSettings();
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
            var json = JsonSerializer.Serialize(settings, SettingsJsonContext.Default.AppSettings);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Best-effort persistence.
        }
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNameCaseInsensitive = true,
    // WindowX/Y use double.NaN as "unset" sentinels.
    NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext
{
}
