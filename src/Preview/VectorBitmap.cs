using System.Drawing;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DesktopHover
{
    internal static class VectorBitmap
    {
        internal static Bitmap Render(DrawingVisual visual, int width, int height)
        {
            var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            target.Render(visual);
            var image = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
            try
            {
                var data = image.LockBits(new Rectangle(0, 0, width, height), System.Drawing.Imaging.ImageLockMode.WriteOnly, image.PixelFormat);
                try { target.CopyPixels(Int32Rect.Empty, data.Scan0, data.Stride * height, data.Stride); }
                finally { image.UnlockBits(data); }
                return image;
            }
            catch { image.Dispose(); throw; }
        }
    }
}
