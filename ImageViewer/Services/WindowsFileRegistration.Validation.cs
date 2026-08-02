using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ImageViewer.Services;

public static partial class WindowsFileRegistration
{
    [SupportedOSPlatform("windows")]
    private static bool IsRegistrationComplete(
        string executablePath,
        IReadOnlyList<(string Extension, bool IsVideo)> fileTypes)
    {
        using var capabilities = Registry.CurrentUser.OpenSubKey(CapabilitiesPath);
        using var associations = capabilities?.OpenSubKey("FileAssociations");
        if (capabilities is null
            || !IsOwned(capabilities)
            || associations is null
            || !ValueEquals(capabilities, "ApplicationName", ApplicationName)
            || associations.GetValueNames().Length != fileTypes.Count)
        {
            return false;
        }

        using var registeredApps = Registry.CurrentUser.OpenSubKey(RegisteredApplicationsPath);
        if (registeredApps is null
            || !ValueEquals(registeredApps, ApplicationName, CapabilitiesPath))
        {
            return false;
        }

        if (!IsApplicationEntryComplete(executablePath, fileTypes)
            || !IsAppPathComplete(executablePath))
        {
            return false;
        }

        foreach (var fileType in fileTypes)
        {
            var progId = ProgIdFor(fileType.Extension);
            if (!ValueEquals(associations, fileType.Extension, progId)
                || !IsFileTypeComplete(fileType.Extension, executablePath))
            {
                return false;
            }
        }

        return true;
    }

    [SupportedOSPlatform("windows")]
    private static bool IsApplicationEntryComplete(
        string executablePath,
        IReadOnlyList<(string Extension, bool IsVideo)> fileTypes)
    {
        using var key = Registry.CurrentUser.OpenSubKey(ApplicationEntryPath);
        using var command = key?.OpenSubKey(@"shell\open\command");
        using var supported = key?.OpenSubKey("SupportedTypes");
        return key is not null
            && IsOwned(key)
            && ValueEquals(key, "FriendlyAppName", ApplicationName)
            && command is not null
            && ValueEquals(command, "", OpenCommand(executablePath))
            && supported is not null
            && supported.GetValueNames().Length == fileTypes.Count
            && fileTypes.All(type => HasValue(supported, type.Extension));
    }

    [SupportedOSPlatform("windows")]
    private static bool IsAppPathComplete(string executablePath)
    {
        using var key = Registry.CurrentUser.OpenSubKey(AppPathPath);
        return key is not null
            && IsOwned(key)
            && ValueEquals(key, "", executablePath);
    }

    [SupportedOSPlatform("windows")]
    private static bool IsFileTypeComplete(string extension, string executablePath)
    {
        var progId = ProgIdFor(extension);
        using var progIdKey = Registry.CurrentUser.OpenSubKey(ProgIdPath(extension));
        using var openCommand = progIdKey?.OpenSubKey(@"shell\open\command");
        if (progIdKey is null
            || !IsOwned(progIdKey)
            || openCommand is null
            || !ValueEquals(openCommand, "", OpenCommand(executablePath)))
        {
            return false;
        }

        using var openWith = Registry.CurrentUser.OpenSubKey(OpenWithPath(extension));
        if (openWith is null || !HasValue(openWith, progId)) return false;

        using var verb = Registry.CurrentUser.OpenSubKey(ContextVerbPath(extension));
        using var browseCommand = verb?.OpenSubKey("command");
        return verb is not null
            && IsOwned(verb)
            && browseCommand is not null
            && ValueEquals(browseCommand, "", BrowseCommand(executablePath));
    }

    [SupportedOSPlatform("windows")]
    private static bool ValueEquals(RegistryKey key, string name, string expected) =>
        (key.GetValue(name) as string)?.Equals(expected, StringComparison.OrdinalIgnoreCase) == true;

    [SupportedOSPlatform("windows")]
    private static bool HasValue(RegistryKey key, string name) =>
        key.GetValueNames().Contains(name, StringComparer.OrdinalIgnoreCase);

    [SupportedOSPlatform("windows")]
    private static bool RegisteredExtensionsMatch(
        RegistryKey appKey,
        IReadOnlyList<(string Extension, bool IsVideo)> fileTypes)
    {
        var registered = new HashSet<string>(
            ReadRegisteredExtensions(appKey),
            StringComparer.OrdinalIgnoreCase);
        return registered.SetEquals(fileTypes.Select(type => type.Extension));
    }
}
