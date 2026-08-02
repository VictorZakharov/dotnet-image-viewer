using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ImageViewer.Services;

public static partial class WindowsFileRegistration
{
    private const int AssociationChanged = 0x08000000;

    [SupportedOSPlatform("windows")]
    private static void WriteApplicationRoot(
        string executablePath,
        MediaAssociationGroups groups,
        IReadOnlyList<(string Extension, bool IsVideo)> fileTypes)
    {
        using var key = OpenOwnedOrNew(AppRoot);
        key.SetValue(RegisteredExecutableName, executablePath, RegistryValueKind.String);
        key.SetValue(
            RegisteredExtensionsName,
            fileTypes.Select(type => type.Extension).ToArray(),
            RegistryValueKind.MultiString);
        key.SetValue(RegisteredGroupsName, (int)groups, RegistryValueKind.DWord);
        key.SetValue("RegistrationVersion", 3, RegistryValueKind.DWord);
    }

    [SupportedOSPlatform("windows")]
    private static void WriteCapabilities(
        string executablePath,
        IReadOnlyList<(string Extension, bool IsVideo)> fileTypes)
    {
        using var key = RecreateOwned(CapabilitiesPath);
        key.SetValue("ApplicationName", ApplicationName, RegistryValueKind.String);
        key.SetValue(
            "ApplicationDescription",
            "A lightweight image and video viewer for Windows",
            RegistryValueKind.String);
        key.SetValue("ApplicationIcon", IconReference(executablePath), RegistryValueKind.String);

        using var associations = key.CreateSubKey("FileAssociations", writable: true);
        foreach (var fileType in fileTypes)
            associations.SetValue(fileType.Extension, ProgIdFor(fileType.Extension), RegistryValueKind.String);
    }

    [SupportedOSPlatform("windows")]
    private static void WriteFileType(string extension, bool isVideo, string executablePath)
    {
        var progId = ProgIdFor(extension);
        using (var key = RecreateOwned(ProgIdPath(extension)))
        {
            var kind = isVideo ? "video" : "image";
            key.SetValue("", $"{extension.TrimStart('.').ToUpperInvariant()} {kind} - ImageViewer");
            using var icon = key.CreateSubKey("DefaultIcon", writable: true);
            icon.SetValue("", IconReference(executablePath));
            using var command = key.CreateSubKey(@"shell\open\command", writable: true);
            command.SetValue("", OpenCommand(executablePath));
        }

        using (var openWith = Registry.CurrentUser.CreateSubKey(OpenWithPath(extension), writable: true))
            openWith.SetValue(progId, "", RegistryValueKind.String);

        using var verb = RecreateOwned(ContextVerbPath(extension));
        verb.SetValue("", "Browse containing folder in ImageViewer");
        verb.SetValue("Icon", IconReference(executablePath));
        verb.SetValue("MultiSelectModel", "Single");
        using var browseCommand = verb.CreateSubKey("command", writable: true);
        browseCommand.SetValue("", BrowseCommand(executablePath));
    }

    [SupportedOSPlatform("windows")]
    private static void WriteApplicationEntry(
        string executablePath,
        IReadOnlyList<(string Extension, bool IsVideo)> fileTypes)
    {
        using var key = RecreateOwned(ApplicationEntryPath);
        key.SetValue("FriendlyAppName", ApplicationName);
        using var icon = key.CreateSubKey("DefaultIcon", writable: true);
        icon.SetValue("", IconReference(executablePath));
        using var command = key.CreateSubKey(@"shell\open\command", writable: true);
        command.SetValue("", OpenCommand(executablePath));
        using var supported = key.CreateSubKey("SupportedTypes", writable: true);
        foreach (var fileType in fileTypes)
            supported.SetValue(fileType.Extension, "", RegistryValueKind.String);
    }

    [SupportedOSPlatform("windows")]
    private static void WriteAppPath(string executablePath)
    {
        using var key = RecreateOwned(AppPathPath);
        key.SetValue("", executablePath);
        var directory = Path.GetDirectoryName(executablePath);
        if (!string.IsNullOrEmpty(directory)) key.SetValue("Path", directory);
    }

    [SupportedOSPlatform("windows")]
    private static void WriteRegisteredApplicationsValue()
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegisteredApplicationsPath, writable: true);
        var existing = key.GetValue(ApplicationName) as string;
        if (existing is not null
            && !existing.Equals(CapabilitiesPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Another application owns the ImageViewer Default Apps registration.");
        }
        key.SetValue(ApplicationName, CapabilitiesPath, RegistryValueKind.String);
    }

    [SupportedOSPlatform("windows")]
    private static void RemoveFileType(string extension)
    {
        using (var openWith = Registry.CurrentUser.OpenSubKey(OpenWithPath(extension), writable: true))
            openWith?.DeleteValue(ProgIdFor(extension), throwOnMissingValue: false);
        DeleteOwnedSubtree(ProgIdPath(extension));
        DeleteOwnedSubtree(ContextVerbPath(extension));
    }

    [SupportedOSPlatform("windows")]
    private static void DeleteRegisteredApplicationsValue()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegisteredApplicationsPath, writable: true);
        if ((key?.GetValue(ApplicationName) as string)?.Equals(
                CapabilitiesPath,
                StringComparison.OrdinalIgnoreCase) == true)
        {
            key!.DeleteValue(ApplicationName, throwOnMissingValue: false);
        }
    }

    [SupportedOSPlatform("windows")]
    private static RegistryKey OpenOwnedOrNew(string path)
    {
        using var existing = Registry.CurrentUser.OpenSubKey(path);
        if (existing is not null && !IsOwned(existing) && HasContent(existing))
            throw new InvalidOperationException($"Registry key is not owned by ImageViewer: {path}");

        var key = Registry.CurrentUser.CreateSubKey(path, writable: true);
        key.SetValue(OwnerName, OwnerId, RegistryValueKind.String);
        return key;
    }

    [SupportedOSPlatform("windows")]
    private static RegistryKey RecreateOwned(string path)
    {
        using (var existing = Registry.CurrentUser.OpenSubKey(path))
        {
            if (existing is not null && !IsOwned(existing) && HasContent(existing))
                throw new InvalidOperationException($"Registry key is not owned by ImageViewer: {path}");
        }

        Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
        return OpenOwnedOrNew(path);
    }

    [SupportedOSPlatform("windows")]
    private static void DeleteOwnedSubtree(string path)
    {
        using var key = Registry.CurrentUser.OpenSubKey(path);
        if (key is not null && IsOwned(key))
            Registry.CurrentUser.DeleteSubKeyTree(path, throwOnMissingSubKey: false);
    }

    [SupportedOSPlatform("windows")]
    private static bool IsOwned(RegistryKey key) =>
        (key.GetValue(OwnerName) as string)?.Equals(OwnerId, StringComparison.Ordinal) == true;

    [SupportedOSPlatform("windows")]
    private static bool HasContent(RegistryKey key) =>
        key.ValueCount > 0 || key.SubKeyCount > 0;

    private static bool PathsEqual(string left, string right) =>
        Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar)
            .Equals(
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);

    [SupportedOSPlatform("windows")]
    private static void NotifyAssociationsChanged() =>
        SHChangeNotify(AssociationChanged, 0, IntPtr.Zero, IntPtr.Zero);

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(
        int eventId,
        uint flags,
        IntPtr item1,
        IntPtr item2);
}
