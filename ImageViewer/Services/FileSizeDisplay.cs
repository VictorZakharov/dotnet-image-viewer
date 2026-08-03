using System;

namespace ImageViewer.Services;

public static class FileSizeDisplay
{
    public static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        var kibibytes = bytes / 1024d;
        if (kibibytes < 1024) return $"{kibibytes:0.#} KB";
        var mebibytes = kibibytes / 1024d;
        if (mebibytes < 1024) return $"{mebibytes:0.#} MB";
        return $"{mebibytes / 1024d:0.##} GB";
    }

    public static string DescribeChange(long originalBytes, long convertedBytes)
    {
        if (originalBytes <= 0) return Format(convertedBytes);
        var percent = (convertedBytes - originalBytes) * 100d / originalBytes;
        if (Math.Abs(percent) < 0.5) return $"{Format(convertedBytes)} · about the same size";
        return percent < 0
            ? $"{Format(convertedBytes)} · {Math.Abs(percent):0}% smaller"
            : $"{Format(convertedBytes)} · {percent:0}% larger";
    }
}
