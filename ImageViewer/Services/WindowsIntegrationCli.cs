using System;

namespace ImageViewer.Services;

internal static class WindowsIntegrationCli
{
    public static int Execute(StartupCommand command)
    {
        if (!OperatingSystem.IsWindows()) return 2;

        try
        {
            switch (command)
            {
                case StartupCommand.Register:
                    WindowsFileRegistration.RegisterCurrentExecutable();
                    break;
                case StartupCommand.Unregister:
                    WindowsFileRegistration.UnregisterCurrentUser();
                    break;
                case StartupCommand.DefaultApps:
                    return WindowsIntegrationLauncher.OpenDefaultApps() ? 0 : 1;
                default:
                    return 2;
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
