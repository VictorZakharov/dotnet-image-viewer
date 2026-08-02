using System;
using System.IO;
using System.Threading;
using Avalonia;
using ImageViewer.Services;

namespace ImageViewer;

internal static class Program
{
    public const string MutexName = "Local\\ImageViewer.SingleInstance.{8B2D9F35-3A14-4F8B-A19C-7E5D6C4B0A21}";
    public const string PipeName = "ImageViewer.Pipe.{8B2D9F35-3A14-4F8B-A19C-7E5D6C4B0A21}";

    public static string? InitialPath { get; private set; }

    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0)
        {
            switch (args[0])
            {
                case "--register":
                case "--unregister":
                    // Implemented in pass 3 (file association).
                    return 0;
            }
        }

        InitialPath = ResolveInitialPath(args);

        var mutex = new Mutex(initiallyOwned: false, MutexName);
        bool acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }

            if (!acquired)
            {
                SingleInstanceServer.TryHandoff(PipeName, InitialPath);
                return 0;
            }

            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (acquired)
            {
                try { mutex.ReleaseMutex(); } catch (ApplicationException) { }
            }
            mutex.Dispose();
        }
    }

    private static string? ResolveInitialPath(string[] args)
    {
        if (args.Length == 0) return null;
        try { return Path.GetFullPath(args[0]); }
        catch { return null; }
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont();

#if DEBUG
        // Keep Avalonia diagnostics available while developing without paying
        // for a trace sink in production startup.
        builder.LogToTrace();
#endif

        return builder;
    }
}
