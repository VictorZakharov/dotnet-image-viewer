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

public sealed partial class FileIdentityProvider : IFileIdentityProvider
{
    public FileIdentity Get(string path)
    {
        if (OperatingSystem.IsLinux())
            return GetLinuxIdentity(path);
        if (!OperatingSystem.IsWindows())
            return new FileIdentity(FileSystemPath.NormalizeForCache(path));

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

    private static FileIdentity GetLinuxIdentity(string path)
    {
        if (LinuxNativeMethods.Stat(path, out var info) != 0)
        {
            throw new IOException(
                $"Could not read the file identity (Linux error {Marshal.GetLastPInvokeError()}).");
        }

        return new FileIdentity($"{info.Device:X16}:{info.Inode:X16}");
    }

    private static class NativeMethods
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileInformationByHandle(
            SafeFileHandle fileHandle,
            out ByHandleFileInformation fileInformation);
    }

    private static partial class LinuxNativeMethods
    {
        [LibraryImport(
            "libc",
            EntryPoint = "stat",
            StringMarshalling = StringMarshalling.Utf8,
            SetLastError = true)]
        internal static partial int Stat(string path, out LinuxStatBuffer info);
    }

    // glibc's x64 and ARM64 stat layouts begin with st_dev and st_ino.
    // Reserve the full native structure so libc can safely populate it while
    // ImageViewer reads only the stable identity fields.
    [StructLayout(LayoutKind.Explicit, Size = 256)]
    private struct LinuxStatBuffer
    {
        [FieldOffset(0)] public ulong Device;
        [FieldOffset(8)] public ulong Inode;
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
