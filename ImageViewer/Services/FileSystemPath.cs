using System;
using System.IO;

namespace ImageViewer.Services;

/// <summary>
/// Centralizes filesystem path identity. Windows paths are case-insensitive;
/// Linux paths are case-sensitive even when the UI sorts names without case.
/// </summary>
public static class FileSystemPath
{
    public static StringComparer Comparer { get; } = OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static StringComparison Comparison { get; } = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    public static bool Equals(string? left, string? right) =>
        string.Equals(left, right, Comparison);

    public static bool IsSameOrChild(string path, string possibleParent)
    {
        if (Equals(path, possibleParent)) return true;
        var prefix = Path.EndsInDirectorySeparator(possibleParent)
            ? possibleParent
            : possibleParent + Path.DirectorySeparatorChar;
        return path.StartsWith(prefix, Comparison);
    }

    public static string NormalizeForCache(string path)
    {
        var normalized = Path.GetFullPath(path);
        return OperatingSystem.IsWindows() ? normalized.ToUpperInvariant() : normalized;
    }
}
