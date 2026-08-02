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
    string? InitialPath = null)
{
    public static CommandLineRequest Parse(string[] args)
    {
        if (args.Length == 0)
            return new CommandLineRequest(StartupCommand.Launch);

        if (args[0].Equals("--register", StringComparison.OrdinalIgnoreCase))
            return ExactCommand(args, StartupCommand.Register);
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
