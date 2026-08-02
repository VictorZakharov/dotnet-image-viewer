using System;

namespace ImageViewer.Services;

internal static class WindowsIntegrationCli
{
    public static int Execute(CommandLineRequest request)
    {
        if (!OperatingSystem.IsWindows()) return 2;

        try
        {
            switch (request.Command)
            {
                case StartupCommand.Register:
                    WindowsFileRegistration.RegisterCurrentExecutable(request.AssociationGroups);
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
