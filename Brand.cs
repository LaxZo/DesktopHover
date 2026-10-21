using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace DesktopHover
{
    internal static class Brand
    {
        public static readonly Color Background = Color.FromArgb(16, 20, 24);
        public static readonly Color Surface = Color.FromArgb(27, 33, 40);
        public static readonly Color Border = Color.FromArgb(48, 57, 67);
        public static readonly Color Text = Color.FromArgb(238, 243, 247);
        public static readonly Color Muted = Color.FromArgb(145, 160, 174);
        public static readonly Color Accent = Color.FromArgb(99, 219, 194);

        public static GraphicsPath RoundRect(RectangleF rect, float radius)
        {
            var path = new GraphicsPath();
            float diameter = Math.Min(radius * 2, Math.Min(rect.Width, rect.Height));
            if (diameter <= 0)
            {
                path.AddRectangle(rect);
                return path;
            }
            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawLogo(Graphics graphics, Rectangle rect)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                float side = Math.Min(rect.Width, rect.Height);
                graphics.TranslateTransform(rect.X + (rect.Width - side) / 2, rect.Y + (rect.Height - side) / 2);
                graphics.ScaleTransform(side / 64f, side / 64f);
                using (var tile = RoundRect(new RectangleF(1, 1, 62, 62), 15))
                using (var fill = new LinearGradientBrush(new PointF(0, 0), new PointF(64, 64),
                    Color.FromArgb(33, 46, 52), Background))
                using (var edge = new Pen(Color.FromArgb(54, 70, 77), 1.2f))
                {
                    graphics.FillPath(fill, tile);
                    graphics.DrawPath(edge, tile);
                }
                using (var frame = RoundRect(new RectangleF(12, 14, 37, 32), 5))
                using (var stroke = new Pen(Accent, 3.5f))
                using (var sun = new SolidBrush(Accent))
                {
                    stroke.StartCap = LineCap.Round;
                    stroke.EndCap = LineCap.Round;
                    stroke.LineJoin = LineJoin.Round;
                    graphics.DrawPath(stroke, frame);
                    graphics.FillEllipse(sun, 35, 20, 6.5f, 6.5f);
                    graphics.DrawLines(stroke, new PointF[] {
                        new PointF(15, 39), new PointF(25, 29),
                        new PointF(32, 36), new PointF(37, 31),
                        new PointF(46, 40)
                    });
                }
                // A pointer entering the image communicates the hover action even at tray size.
                using (var pointer = new GraphicsPath())
                using (var fill = new SolidBrush(Text))
                using (var outline = new Pen(Background, 4.5f))
                {
                    pointer.AddPolygon(new PointF[] {
                        new PointF(38, 29), new PointF(38, 54),
                        new PointF(44.5f, 48.5f), new PointF(49.5f, 57),
                        new PointF(55, 53.5f), new PointF(50, 45.5f),
                        new PointF(58, 43.5f)
                    });
                    outline.LineJoin = LineJoin.Round;
                    graphics.DrawPath(outline, pointer);
                    graphics.FillPath(fill, pointer);
                }
            }
            finally { graphics.Restore(state); }
        }

        public static Bitmap CreateLogoBitmap(int size)
        {
            var bitmap = new Bitmap(size, size, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Transparent);
                DrawLogo(graphics, new Rectangle(0, 0, size, size));
            }
            return bitmap;
        }

        public static Icon CreateIcon()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("DesktopHover.ico"))
            {
                if (stream != null)
                    using (var icon = new Icon(stream, new Size(32, 32))) return (Icon)icon.Clone();
            }
            using (Bitmap bitmap = CreateLogoBitmap(32))
            {
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon icon = Icon.FromHandle(handle)) return (Icon)icon.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr icon);
    }
}
