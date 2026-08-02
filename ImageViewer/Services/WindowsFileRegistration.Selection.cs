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

    [SupportedOSPlatform("windows")]
    private static MediaAssociationGroups ReadRegisteredGroups(RegistryKey key)
    {
        if (key.GetValue(RegisteredGroupsName) is int stored)
            return (MediaAssociationGroups)stored & MediaAssociationGroups.All;

        var extensions = ReadRegisteredExtensions(key);
        var groups = MediaAssociationGroups.None;
        if (ContainsAny(extensions, MediaFileTypes.ImageExtensions))
            groups |= MediaAssociationGroups.Images;
        if (ContainsAny(extensions, MediaFileTypes.SupportedVideoExtensions))
            groups |= MediaAssociationGroups.Videos;
        return groups;
    }

    private static bool ContainsAny(
        IReadOnlyList<string> registered,
        IReadOnlyList<string> supported) =>
        registered.Any(extension => supported.Contains(
            extension,
            StringComparer.OrdinalIgnoreCase));
}
