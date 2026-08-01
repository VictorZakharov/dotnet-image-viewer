using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ImageViewer.Services;

/// <summary>
/// Uses the Windows thumbnail provider registered for a media type. This keeps
/// LibVLC out of folder scans and lets Explorer's codec providers do the work.
/// Call only from a background thread: shell thumbnail extraction can block.
/// </summary>
internal static class ShellThumbnailProvider
{
    private const uint BiRgb = 0;

    public static Bitmap? TryGet(string path, int requestedSize)
    {
        if (!OperatingSystem.IsWindows()) return null;

        IShellItemImageFactory? factory = null;
        IntPtr bitmapHandle = IntPtr.Zero;
        try
        {
            var iid = typeof(IShellItemImageFactory).GUID;
            var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
            if (hr < 0 || factory is null) return null;

            var size = new NativeSize
            {
                Width = Math.Max(64, requestedSize),
                Height = Math.Max(64, requestedSize)
            };
            hr = factory.GetImage(
                size,
                ShellItemImageFactoryFlags.ThumbnailOnly |
                ShellItemImageFactoryFlags.BiggerSizeOk,
                out bitmapHandle);
            return hr < 0 || bitmapHandle == IntPtr.Zero
                ? null
                : CopyBitmap(bitmapHandle);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (bitmapHandle != IntPtr.Zero) DeleteObject(bitmapHandle);
            if (factory is not null && Marshal.IsComObject(factory))
                Marshal.FinalReleaseComObject(factory);
        }
    }

    private static Bitmap? CopyBitmap(IntPtr bitmapHandle)
    {
        if (GetObject(bitmapHandle, Marshal.SizeOf<NativeBitmap>(), out var nativeBitmap) == 0)
            return null;

        var width = nativeBitmap.Width;
        var height = Math.Abs(nativeBitmap.Height);
        if (width <= 0 || height <= 0) return null;

        var info = new BitmapInfo
        {
            Header = new BitmapInfoHeader
            {
                Size = (uint)Marshal.SizeOf<BitmapInfoHeader>(),
                Width = width,
                Height = -height, // top-down pixels
                Planes = 1,
                BitCount = 32,
                Compression = BiRgb
            }
        };

        var pixels = new byte[checked(width * height * 4)];
        var screen = GetDC(IntPtr.Zero);
        try
        {
            if (screen == IntPtr.Zero || GetDIBits(
                    screen,
                    bitmapHandle,
                    0,
                    (uint)height,
                    pixels,
                    ref info,
                    0) == 0)
                return null;
        }
        finally
        {
            if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
        }

        var result = new WriteableBitmap(
            new PixelSize(width, height),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Premul);

        using var framebuffer = result.Lock();
        var sourceStride = width * 4;
        if (framebuffer.RowBytes == sourceStride)
        {
            Marshal.Copy(pixels, 0, framebuffer.Address, pixels.Length);
        }
        else
        {
            for (var row = 0; row < height; row++)
            {
                Marshal.Copy(
                    pixels,
                    row * sourceStride,
                    IntPtr.Add(framebuffer.Address, row * framebuffer.RowBytes),
                    sourceStride);
            }
        }

        return result;
    }

    [Flags]
    private enum ShellItemImageFactoryFlags : uint
    {
        BiggerSizeOk = 0x1,
        ThumbnailOnly = 0x8
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSize
    {
        public int Width;
        public int Height;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeBitmap
    {
        public int Type;
        public int Width;
        public int Height;
        public int WidthBytes;
        public ushort Planes;
        public ushort BitsPixel;
        public IntPtr Bits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public uint Colors;
    }

    [ComImport]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B")]
    private interface IShellItemImageFactory
    {
        [PreserveSig]
        int GetImage(
            NativeSize size,
            ShellItemImageFactoryFlags flags,
            out IntPtr bitmapHandle);
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
    private static extern int SHCreateItemFromParsingName(
        [MarshalAs(UnmanagedType.LPWStr)] string path,
        IntPtr bindContext,
        ref Guid requestedInterface,
        [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory? shellItem);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetObject(
        IntPtr handle,
        int objectSize,
        out NativeBitmap bitmap);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(
        IntPtr deviceContext,
        IntPtr bitmap,
        uint startScan,
        uint scanLines,
        [Out] byte[] bits,
        ref BitmapInfo bitmapInfo,
        uint usage);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr window);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);
}
