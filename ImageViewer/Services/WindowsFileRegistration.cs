using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Win32;

namespace ImageViewer.Services;

public static partial class WindowsFileRegistration
{
    public const string ApplicationName = "ImageViewer";

    private const string OwnerName = "ImageViewerRegistrationId";
    private const string OwnerId = "{404C93A5-1BF7-4BE1-9306-8910D3671A81}";
    private const string AppRoot = @"Software\ImageViewer";
    private const string CapabilitiesPath = @"Software\ImageViewer\Capabilities";
    private const string RegisteredApplicationsPath = @"Software\RegisteredApplications";
    private const string ClassesPath = @"Software\Classes";
    private const string RegisteredExecutableName = "RegisteredExecutable";
    private const string RegisteredExtensionsName = "RegisteredExtensions";
    private const string RegisteredGroupsName = "RegisteredGroups";
    private const string BrowseVerb = "ImageViewer.BrowseContaining";

    private static readonly (string Extension, bool IsVideo)[] FileTypes =
    [
        .. MediaFileTypes.ImageExtensions.Select(extension => (extension, false)),
        .. MediaFileTypes.SupportedVideoExtensions.Select(extension => (extension, true))
    ];

    public static int ImageAssociationCount => MediaFileTypes.ImageExtensions.Count;
    public static int VideoAssociationCount => MediaFileTypes.SupportedVideoExtensions.Count;
    public static int TotalAssociationCount => FileTypes.Length;
    public static IReadOnlyList<string> ImageExtensions => MediaFileTypes.ImageExtensions;
    public static IReadOnlyList<string> VideoExtensions => MediaFileTypes.SupportedVideoExtensions;

    public static WindowsIntegrationStatus GetStatus()
    {
        if (!OperatingSystem.IsWindows())
            return new WindowsIntegrationStatus(WindowsIntegrationState.Unsupported);

        try { return GetStatusWindows(); }
        catch { return new WindowsIntegrationStatus(WindowsIntegrationState.NeedsRepair); }
    }

    public static void RegisterCurrentExecutable(
        MediaAssociationGroups groups = MediaAssociationGroups.All)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows integration is available only on Windows.");
        if (groups == MediaAssociationGroups.None
            || (groups & ~MediaAssociationGroups.All) != MediaAssociationGroups.None)
        {
            throw new ArgumentOutOfRangeException(
                nameof(groups),
                "Select image associations, video associations, or both.");
        }

        RegisterWindows(
            WindowsIntegrationLauncher.GetExecutablePath(),
            SelectFileTypes(groups));
    }

    public static void RegisterCurrentExecutable(IReadOnlyCollection<string> extensions)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows integration is available only on Windows.");
        ArgumentNullException.ThrowIfNull(extensions);

        RegisterWindows(
            WindowsIntegrationLauncher.GetExecutablePath(),
            SelectFileTypes(extensions));
    }

    public static void UnregisterCurrentUser()
    {
        if (!OperatingSystem.IsWindows()) return;
        UnregisterWindows();
    }

    [SupportedOSPlatform("windows")]
    private static WindowsIntegrationStatus GetStatusWindows()
    {
        using var appKey = Registry.CurrentUser.OpenSubKey(AppRoot);
        if (appKey is null)
            return new WindowsIntegrationStatus(WindowsIntegrationState.NotRegistered);
        if (!IsOwned(appKey))
            return new WindowsIntegrationStatus(WindowsIntegrationState.NeedsRepair);

        var registeredPath = appKey.GetValue(RegisteredExecutableName) as string;
        var registeredExtensions = ReadRegisteredExtensions(appKey);
        var selectedFileTypes = SelectKnownFileTypes(registeredExtensions);
        var selectedExtensions = selectedFileTypes
            .Select(type => type.Extension)
            .ToArray();
        var groups = GetGroups(selectedFileTypes);
        if (string.IsNullOrWhiteSpace(registeredPath)
            || selectedFileTypes.Length == 0
            || !RegisteredExtensionsMatch(appKey, selectedFileTypes)
            || !IsRegistrationComplete(registeredPath, selectedFileTypes))
        {
            return new WindowsIntegrationStatus(
                WindowsIntegrationState.NeedsRepair,
                registeredPath,
                groups,
                selectedExtensions);
        }

        var currentPath = WindowsIntegrationLauncher.TryGetExecutablePath();
        var state = currentPath is not null && PathsEqual(currentPath, registeredPath)
            ? WindowsIntegrationState.RegisteredHere
            : WindowsIntegrationState.RegisteredElsewhere;
        return new WindowsIntegrationStatus(
            state,
            registeredPath,
            groups,
            selectedExtensions);
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(
        string executablePath,
        IReadOnlyList<(string Extension, bool IsVideo)> selectedFileTypes)
    {
        executablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("The executable to register was not found.", executablePath);

        var groups = GetGroups(selectedFileTypes);
        var selectedExtensions = selectedFileTypes
            .Select(type => type.Extension)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var extensionsToRemove = ReadRegisteredExtensions()
            .Concat(FileTypes.Select(type => type.Extension))
            .Where(extension => !selectedExtensions.Contains(extension))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in extensionsToRemove)
        {
            RemoveFileType(extension);
        }

        WriteApplicationRoot(executablePath, groups, selectedFileTypes);
        WriteCapabilities(executablePath, selectedFileTypes);
        foreach (var fileType in selectedFileTypes)
            WriteFileType(fileType.Extension, fileType.IsVideo, executablePath);
        WriteApplicationEntry(executablePath, selectedFileTypes);
        WriteAppPath(executablePath);
        WriteRegisteredApplicationsValue();
        NotifyAssociationsChanged();
    }

    [SupportedOSPlatform("windows")]
    private static void UnregisterWindows()
    {
        var extensions = ReadRegisteredExtensions()
            .Concat(FileTypes.Select(type => type.Extension))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var extension in extensions)
            RemoveFileType(extension);

        DeleteOwnedSubtree(ApplicationEntryPath);
        DeleteOwnedSubtree(AppPathPath);
        DeleteRegisteredApplicationsValue();
        DeleteOwnedSubtree(AppRoot);
        NotifyAssociationsChanged();
    }

    private static string ProgIdFor(string extension) =>
        $"ImageViewer.AssocFile.{extension.TrimStart('.').ToUpperInvariant()}";

    private static string ProgIdPath(string extension) =>
        $@"{ClassesPath}\{ProgIdFor(extension)}";

    private static string OpenWithPath(string extension) =>
        $@"{ClassesPath}\{extension}\OpenWithProgids";

    private static string ContextVerbPath(string extension) =>
        $@"{ClassesPath}\SystemFileAssociations\{extension}\shell\{BrowseVerb}";

    private static string ApplicationEntryPath =>
        $@"{ClassesPath}\Applications\ImageViewer.exe";

    private static string AppPathPath =>
        @"Software\Microsoft\Windows\CurrentVersion\App Paths\ImageViewer.exe";

    private static string OpenCommand(string executablePath) =>
        $"\"{executablePath}\" \"%1\"";

    private static string BrowseCommand(string executablePath) =>
        $"\"{executablePath}\" --browse \"%1\"";

    private static string IconReference(string executablePath) =>
        $"\"{executablePath}\",0";
}
