using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;

namespace DesktopHover
{
    // Shell handlers run in the shared isolated document worker.
    internal static class ShellThumbnailProvider
    {
        internal static Bitmap TryLoad(string path, Size maxSize, CancellationToken cancellation)
        {
            return DocumentPreviewWorker.TryLoad(path, maxSize, cancellation, false);
        }

        internal static Bitmap Extract(string path, Size size)
        {
            IShellItemImageFactory factory = null;
            IntPtr handle = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                if (SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out factory) < 0 || factory == null) return null;
                // THUMBNAILONLY | BIGGERSIZEOK. Resize in managed code to preserve aspect ratio.
                // https://learn.microsoft.com/windows/win32/api/shobjidl_core/nf-shobjidl_core-ishellitemimagefactory-getimage
                if (factory.GetImage(new NativeSize(size.Width, size.Height), 0x00000008 | 0x00000001, out handle) < 0 ||
                    handle == IntPtr.Zero) return null;
                NativeBitmap bitmap;
                if (GetObject(handle, Marshal.SizeOf(typeof(NativeBitmap)), out bitmap) == 0 || bitmap.Width <= 0 ||
                    bitmap.Height <= 0 || bitmap.Width > 4096 || bitmap.Height > 4096 || (long)bitmap.Width * bitmap.Height > 8000000)
                    return null;
                using (var image = Image.FromHbitmap(handle)) return DocumentPreviewWorker.CopyImage(image, size);
            }
            finally
            {
                if (handle != IntPtr.Zero) DeleteObject(handle);
                if (factory != null) Marshal.FinalReleaseComObject(factory);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize
        {
            internal int Width, Height;
            internal NativeSize(int width, int height) { Width = width; Height = height; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeBitmap
        {
            internal int Type, Width, Height, WidthBytes;
            internal ushort Planes, BitsPixel;
            internal IntPtr Bits;
        }

        [ComImport, Guid("BCC18B79-BA16-442F-80C4-8A59C30C463B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(NativeSize size, uint flags, out IntPtr bitmap);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = true)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr bindContext, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory item);
        [DllImport("gdi32.dll", EntryPoint = "GetObjectW")]
        private static extern int GetObject(IntPtr bitmap, int size, out NativeBitmap result);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr value);
    }
}
