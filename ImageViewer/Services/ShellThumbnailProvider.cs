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
internal static partial class ShellThumbnailProvider
{
    private const uint BiRgb = 0;
    private const int IUnknownReleaseSlot = 2;
    private const int GetImageSlot = 3;

    public static unsafe Bitmap? TryGet(string path, int requestedSize)
    {
        if (!OperatingSystem.IsWindows()) return null;

        IntPtr factory = IntPtr.Zero;
        IntPtr bitmapHandle = IntPtr.Zero;
        try
        {
            var iid = ShellItemImageFactoryId;
            var hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory);
            if (hr < 0 || factory == IntPtr.Zero) return null;

            var size = new NativeSize
            {
                Width = Math.Max(64, requestedSize),
                Height = Math.Max(64, requestedSize)
            };
            var vtable = *(IntPtr**)factory;
            // Source-generated P/Invoke gives us the raw interface pointer.
            // Calling its fixed IUnknown vtable keeps this bridge compatible
            // with Native AOT, which cannot emit built-in COM wrappers.
            var getImage = (delegate* unmanaged[Stdcall]<
                IntPtr,
                NativeSize,
                ShellItemImageFactoryFlags,
                IntPtr*,
                int>)vtable[GetImageSlot];
            hr = getImage(
                factory,
                size,
                ShellItemImageFactoryFlags.ThumbnailOnly |
                ShellItemImageFactoryFlags.BiggerSizeOk,
                &bitmapHandle);
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
            if (factory != IntPtr.Zero)
            {
                var vtable = *(IntPtr**)factory;
                var release = (delegate* unmanaged[Stdcall]<IntPtr, uint>)vtable[IUnknownReleaseSlot];
                _ = release(factory);
            }
        }
    }

    private static readonly Guid ShellItemImageFactoryId =
        new("BCC18B79-BA16-442F-80C4-8A59C30C463B");

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

    [LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int SHCreateItemFromParsingName(
        string path,
        IntPtr bindContext,
        ref Guid requestedInterface,
        out IntPtr shellItem);

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
