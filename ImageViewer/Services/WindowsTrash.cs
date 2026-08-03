using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace ImageViewer.Services;

internal static class WindowsTrash
{
    [SupportedOSPlatform("windows")]
    public static bool TryMove(string path)
    {
        var operation = new ShellFileOperation
        {
            Function = Delete,
            From = path + "\0",
            Flags = AllowUndo | NoConfirmation | Silent | NoErrorUi
        };
        var result = SHFileOperation(ref operation);
        return result == 0 && !operation.AnyOperationsAborted;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileOperation
    {
        public IntPtr Window;
        public uint Function;
        [MarshalAs(UnmanagedType.LPWStr)] public string From;
        [MarshalAs(UnmanagedType.LPWStr)] public string? To;
        public ushort Flags;
        [MarshalAs(UnmanagedType.Bool)] public bool AnyOperationsAborted;
        public IntPtr NameMappings;
        [MarshalAs(UnmanagedType.LPWStr)] public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, EntryPoint = "SHFileOperationW")]
    private static extern int SHFileOperation(ref ShellFileOperation operation);

    private const uint Delete = 0x0003;
    private const ushort AllowUndo = 0x0040;
    private const ushort NoConfirmation = 0x0010;
    private const ushort Silent = 0x0004;
    private const ushort NoErrorUi = 0x0400;
}
