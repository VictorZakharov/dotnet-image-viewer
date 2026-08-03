using System;
using System.Linq;
using ImageMagick;

namespace ImageViewer.Services;

internal static class WatermarkFontResolver
{
    private static readonly string[] PreferredFonts =
    [
        "Inter",
        "DejaVu-Sans",
        "Liberation-Sans",
        "Noto-Sans",
        "Arial",
        "Helvetica"
    ];

    private static readonly Lazy<string?> SelectedFont = new(FindFont);

    public static string? FontName => SelectedFont.Value;

    private static string? FindFont()
    {
        var available = MagickNET.FontNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var preferred in PreferredFonts)
        {
            var match = available.FirstOrDefault(name =>
                string.Equals(name, preferred, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        return available.FirstOrDefault();
    }
}
