using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ImageViewer.Services;

public static partial class WindowsFileRegistration
{
    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> ReadRegisteredExtensions()
    {
        using var key = Registry.CurrentUser.OpenSubKey(AppRoot);
        return key is not null && IsOwned(key) ? ReadRegisteredExtensions(key) : [];
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> ReadRegisteredExtensions(RegistryKey key) =>
        key.GetValue(RegisteredExtensionsName) as string[] ?? [];

    private static (string Extension, bool IsVideo)[] SelectFileTypes(
        MediaAssociationGroups groups) =>
        FileTypes.Where(type => type.IsVideo
            ? (groups & MediaAssociationGroups.Videos) != 0
            : (groups & MediaAssociationGroups.Images) != 0)
        .ToArray();

    private static (string Extension, bool IsVideo)[] SelectFileTypes(
        IReadOnlyCollection<string> extensions)
    {
        if (extensions.Count == 0)
            throw new ArgumentException("Select at least one media extension.", nameof(extensions));

        var requested = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        if (requested.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Media extensions cannot be blank.", nameof(extensions));

        var selected = SelectKnownFileTypes(requested);
        if (selected.Length != requested.Count)
        {
            var supported = FileTypes
                .Select(type => type.Extension)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var unsupported = requested.First(extension => !supported.Contains(extension));
            throw new ArgumentException(
                $"Unsupported media extension: {unsupported}",
                nameof(extensions));
        }

        return selected;
    }

    private static (string Extension, bool IsVideo)[] SelectKnownFileTypes(
        IReadOnlyCollection<string> extensions)
    {
        var requested = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        return FileTypes.Where(type => requested.Contains(type.Extension)).ToArray();
    }

    private static MediaAssociationGroups GetGroups(
        IReadOnlyCollection<(string Extension, bool IsVideo)> fileTypes)
    {
        var groups = MediaAssociationGroups.None;
        if (fileTypes.Any(type => !type.IsVideo))
            groups |= MediaAssociationGroups.Images;
        if (fileTypes.Any(type => type.IsVideo))
            groups |= MediaAssociationGroups.Videos;
        return groups;
    }
}
