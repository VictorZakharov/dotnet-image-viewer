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
    private const string BrowseVerb = "ImageViewer.BrowseContaining";

    private static readonly (string Extension, bool IsVideo)[] FileTypes =
    [
        .. MediaFileTypes.ImageExtensions.Select(extension => (extension, false)),
        .. MediaFileTypes.SupportedVideoExtensions.Select(extension => (extension, true))
    ];

    public static WindowsIntegrationStatus GetStatus()
    {
        if (!OperatingSystem.IsWindows())
            return new WindowsIntegrationStatus(WindowsIntegrationState.Unsupported);

        try { return GetStatusWindows(); }
        catch { return new WindowsIntegrationStatus(WindowsIntegrationState.NeedsRepair); }
    }

    public static void RegisterCurrentExecutable()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Windows integration is available only on Windows.");

        RegisterWindows(WindowsIntegrationLauncher.GetExecutablePath());
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
        if (string.IsNullOrWhiteSpace(registeredPath)
            || !IsRegistrationComplete(registeredPath))
        {
            return new WindowsIntegrationStatus(
                WindowsIntegrationState.NeedsRepair,
                registeredPath);
        }

        var currentPath = WindowsIntegrationLauncher.TryGetExecutablePath();
        var state = currentPath is not null && PathsEqual(currentPath, registeredPath)
            ? WindowsIntegrationState.RegisteredHere
            : WindowsIntegrationState.RegisteredElsewhere;
        return new WindowsIntegrationStatus(state, registeredPath);
    }

    [SupportedOSPlatform("windows")]
    private static void RegisterWindows(string executablePath)
    {
        executablePath = Path.GetFullPath(executablePath);
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("The executable to register was not found.", executablePath);

        var previousExtensions = ReadRegisteredExtensions();
        foreach (var extension in previousExtensions.Except(
                     FileTypes.Select(type => type.Extension),
                     StringComparer.OrdinalIgnoreCase))
        {
            RemoveFileType(extension);
        }

        WriteApplicationRoot(executablePath);
        WriteCapabilities(executablePath);
        foreach (var fileType in FileTypes)
            WriteFileType(fileType.Extension, fileType.IsVideo, executablePath);
        WriteApplicationEntry(executablePath);
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
