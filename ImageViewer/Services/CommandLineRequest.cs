using System;
using System.IO;

namespace ImageViewer.Services;

internal enum StartupCommand
{
    Launch,
    Register,
    Unregister,
    DefaultApps,
    Invalid
}

internal sealed record CommandLineRequest(
    StartupCommand Command,
    string? InitialPath = null,
    MediaAssociationGroups AssociationGroups = MediaAssociationGroups.None)
{
    public static CommandLineRequest Parse(string[] args)
    {
        if (args.Length == 0)
            return new CommandLineRequest(StartupCommand.Launch);

        if (args[0].Equals("--register", StringComparison.OrdinalIgnoreCase))
            return ParseRegister(args);
        if (args[0].Equals("--unregister", StringComparison.OrdinalIgnoreCase))
            return ExactCommand(args, StartupCommand.Unregister);
        if (args[0].Equals("--default-apps", StringComparison.OrdinalIgnoreCase))
            return ExactCommand(args, StartupCommand.DefaultApps);
        if (args[0].Equals("--browse", StringComparison.OrdinalIgnoreCase))
            return ParseBrowse(args);
        if (args[0].StartsWith("--", StringComparison.Ordinal))
            return new CommandLineRequest(StartupCommand.Invalid);

        return new CommandLineRequest(StartupCommand.Launch, ResolvePath(args[0]));
    }

    private static CommandLineRequest ExactCommand(string[] args, StartupCommand command) =>
        new(args.Length == 1 ? command : StartupCommand.Invalid);

    private static CommandLineRequest ParseRegister(string[] args)
    {
        if (args.Length == 1)
            return new CommandLineRequest(
                StartupCommand.Register,
                AssociationGroups: MediaAssociationGroups.All);

        var groups = MediaAssociationGroups.None;
        for (var index = 1; index < args.Length; index++)
        {
            if (args[index].Equals("images", StringComparison.OrdinalIgnoreCase))
                groups |= MediaAssociationGroups.Images;
            else if (args[index].Equals("videos", StringComparison.OrdinalIgnoreCase))
                groups |= MediaAssociationGroups.Videos;
            else if (args[index].Equals("all", StringComparison.OrdinalIgnoreCase))
                groups |= MediaAssociationGroups.All;
            else
                return new CommandLineRequest(StartupCommand.Invalid);
        }

        return new CommandLineRequest(StartupCommand.Register, AssociationGroups: groups);
    }

    private static CommandLineRequest ParseBrowse(string[] args)
    {
        if (args.Length != 2) return new CommandLineRequest(StartupCommand.Invalid);
        try
        {
            var target = Path.GetFullPath(args[1]);
            var folder = Path.GetDirectoryName(target);
            return string.IsNullOrEmpty(folder)
                ? new CommandLineRequest(StartupCommand.Invalid)
                : new CommandLineRequest(StartupCommand.Launch, folder);
        }
        catch
        {
            return new CommandLineRequest(StartupCommand.Invalid);
        }
    }

    private static string? ResolvePath(string path)
    {
        try { return Path.GetFullPath(path); }
        catch { return null; }
    }
}
