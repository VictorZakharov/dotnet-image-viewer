namespace ImageViewer.ViewModels;

internal static class DuplicateDisplay
{
    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var value = bytes / 1024d;
        if (value < 1024) return $"{value:0.#} KB";
        value /= 1024;
        if (value < 1024) return $"{value:0.#} MB";
        return $"{value / 1024:0.##} GB";
    }
}
