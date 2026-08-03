using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ImageViewer.Services;

public readonly record struct FileIdentity(string Value);

public interface IFileIdentityProvider
{
    FileIdentity Get(string path);
}

public sealed class FileIdentityProvider : IFileIdentityProvider
{
    public FileIdentity Get(string path)
    {
        if (!OperatingSystem.IsWindows())
            return new FileIdentity(Path.GetFullPath(path));

        using var handle = File.OpenHandle(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            FileOptions.SequentialScan);
        if (!NativeMethods.GetFileInformationByHandle(handle, out var info))
            throw new IOException(
                $"Could not read the file identity (Windows error {Marshal.GetLastWin32Error()}).");

        return new FileIdentity(
            $"{info.VolumeSerialNumber:X8}:{info.FileIndexHigh:X8}{info.FileIndexLow:X8}");
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(
            SafeFileHandle fileHandle,
            out ByHandleFileInformation fileInformation);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }
}
