using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Reflection;

namespace DesktopHover
{
    internal static class Brand
    {
        public static bool IsLight { get; private set; }
        public static void SetTheme(string theme)
        {
            IsLight = String.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
        }

        public static Color Background { get { return IsLight ? Color.FromArgb(245, 247, 250) : Color.FromArgb(16, 20, 24); } }
        public static Color Surface { get { return IsLight ? Color.White : Color.FromArgb(27, 33, 40); } }
        public static Color Border { get { return IsLight ? Color.FromArgb(205, 214, 222) : Color.FromArgb(48, 57, 67); } }
        public static Color Text { get { return IsLight ? Color.FromArgb(35, 47, 57) : Color.FromArgb(238, 243, 247); } }
        public static Color Muted { get { return IsLight ? Color.FromArgb(88, 105, 119) : Color.FromArgb(145, 160, 174); } }
        public static Color Accent { get { return IsLight ? Color.FromArgb(0, 116, 99) : Color.FromArgb(99, 219, 194); } }
        public static Color HoverSurface { get { return IsLight ? Color.FromArgb(232, 238, 244) : Color.FromArgb(43, 47, 57); } }
        public static Color SelectedSurface { get { return IsLight ? Color.FromArgb(218, 242, 236) : Color.FromArgb(35, 57, 62); } }
        public static Color ToggleOff { get { return IsLight ? Color.FromArgb(156, 168, 181) : Color.FromArgb(65, 70, 83); } }
        public static Color ToggleThumb { get { return Color.FromArgb(238, 243, 247); } }
        public static Color Paused { get { return IsLight ? Color.FromArgb(157, 105, 32) : Color.FromArgb(224, 176, 99); } }
        public static Color EditorBackground { get { return IsLight ? Color.White : Color.FromArgb(30, 30, 30); } }
        public static Color EditorGutter { get { return IsLight ? Color.FromArgb(243, 245, 247) : Color.FromArgb(37, 37, 38); } }
        public static Color EditorLineNumber { get { return IsLight ? Color.FromArgb(100, 110, 119) : Color.FromArgb(133, 133, 133); } }
        public static Color CheckerFirst { get { return IsLight ? Color.FromArgb(235, 239, 243) : Color.FromArgb(43, 46, 51); } }
        public static Color CheckerSecond { get { return IsLight ? Color.FromArgb(216, 223, 230) : Color.FromArgb(53, 56, 61); } }

        public static Color SyntaxColor(TextTokenKind kind)
        {
            switch (kind)
            {
                case TextTokenKind.Keyword: return IsLight ? Color.FromArgb(0, 0, 255) : Color.FromArgb(197, 134, 192);
                case TextTokenKind.String: return IsLight ? Color.FromArgb(163, 21, 21) : Color.FromArgb(206, 145, 120);
                case TextTokenKind.Comment: return IsLight ? Color.FromArgb(0, 128, 0) : Color.FromArgb(106, 153, 85);
                case TextTokenKind.Number: return IsLight ? Color.FromArgb(9, 134, 88) : Color.FromArgb(181, 206, 168);
                case TextTokenKind.Type: return IsLight ? Color.FromArgb(38, 127, 153) : Color.FromArgb(78, 201, 176);
                case TextTokenKind.Function: return IsLight ? Color.FromArgb(121, 94, 38) : Color.FromArgb(220, 220, 170);
                case TextTokenKind.Preprocessor: return IsLight ? Color.FromArgb(0, 0, 255) : Color.FromArgb(86, 156, 214);
                case TextTokenKind.Property: return IsLight ? Color.FromArgb(0, 16, 128) : Color.FromArgb(156, 220, 254);
                default: return IsLight ? Color.FromArgb(36, 41, 46) : Color.FromArgb(212, 212, 212);
            }
        }

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

        public static void DrawLanguageIcon(Graphics graphics, Rectangle rect, Color color)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TranslateTransform(rect.X, rect.Y);
                graphics.ScaleTransform(rect.Width / 24f, rect.Height / 24f);
                using (var pen = new Pen(color, 1.8f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    // Translation symbol: crossed language strokes and a Latin A.
                    graphics.DrawLine(pen, 8, 2, 8, 5);
                    graphics.DrawLine(pen, 2, 5, 15, 5);
                    graphics.DrawBezier(pen, 12, 5, 11, 11, 7, 15, 3, 18);
                    graphics.DrawBezier(pen, 5, 8, 6, 11, 9, 14, 12, 16);
                    graphics.DrawLines(pen, new[] { new PointF(13, 22), new PointF(18, 10), new PointF(23, 22) });
                    graphics.DrawLine(pen, 15, 18, 21, 18);
                }
            }
            finally { graphics.Restore(state); }
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
                    IsLight ? Color.FromArgb(227, 241, 237) : Color.FromArgb(33, 46, 52), Background))
                using (var edge = new Pen(IsLight ? Border : Color.FromArgb(54, 70, 77), 1.2f))
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
