using System.Runtime.InteropServices;

namespace ImageViewer.Services;

public static class MotionPreferences
{
    private const uint GetClientAreaAnimation = 0x1042;

    public static bool AreAnimationsEnabled()
    {
        if (!OperatingSystem.IsWindows()) return true;

        try
        {
            return SystemParametersInfo(
                GetClientAreaAnimation,
                0,
                out var animationsEnabled,
                0)
                && animationsEnabled;
        }
        catch
        {
            // Preserve motion when a platform cannot report a preference.
            return true;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(
        uint action,
        uint parameter,
        [MarshalAs(UnmanagedType.Bool)] out bool value,
        uint updateFlags);
}
