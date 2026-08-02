using System;
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
        var request = CommandLineRequest.Parse(args);
        switch (request.Command)
        {
            case StartupCommand.Register:
            case StartupCommand.Unregister:
            case StartupCommand.DefaultApps:
                return WindowsIntegrationCli.Execute(request);
            case StartupCommand.Invalid:
                return 2;
        }

        InitialPath = request.InitialPath;

        var mutex = new Mutex(initiallyOwned: false, MutexName);
        bool acquired = false;
        try
        {
            try { acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { acquired = true; }

            if (!acquired)
            {
                return SingleInstanceServer.TryHandoff(
                    PipeName,
                    InitialPath,
                    timeoutMs: 5000)
                    ? 0
                    : 3;
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
