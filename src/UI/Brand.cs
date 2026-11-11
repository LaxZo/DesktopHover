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
        public static bool IsGlass { get; private set; }
        internal static Color WindowsAccentColor { get; private set; }
        private static Color readableAccent;
        public static void SetTheme(string theme, bool useWindowsAccent = false)
        {
            SetPalette(theme, useWindowsAccent ? WindowsAccent.Read() : Color.Empty);
        }

        internal static void SetPalette(string theme, Color accent)
        {
            bool light = String.Equals(theme, "light", StringComparison.OrdinalIgnoreCase);
            bool glass = String.Equals(theme, "glass", StringComparison.OrdinalIgnoreCase);
            if (IsLight == light && IsGlass == glass && WindowsAccentColor == accent) return;
            IsLight = light; IsGlass = glass; WindowsAccentColor = accent;
            if (accent.IsEmpty) return;
            // Use the original Windows color on fills and a legible shade on text.
            Color target = IsLight ? Color.Black : Color.White;
            double surface = Luminance(SelectedSurface);
            readableAccent = accent;
            for (int step = 1; step <= 12; step++)
            {
                double ink = Luminance(readableAccent);
                if ((Math.Max(ink, surface) + .05) / (Math.Min(ink, surface) + .05) >= 4.5) break;
                readableAccent = UiMotion.Blend(accent, target, step / 12.0);
            }
            accentMark = Luminance(accent) > .179 ? Color.Black : Color.White;
        }

        private static double Linear(byte channel)
        {
            double value = channel / 255.0;
            return value <= .04045 ? value / 12.92 : Math.Pow((value + .055) / 1.055, 2.4);
        }
        private static double Luminance(Color c) { return .2126 * Linear(c.R) + .7152 * Linear(c.G) + .0722 * Linear(c.B); }
        private static Color Tint(int neutral, double amount) { return UiMotion.Blend(Color.FromArgb(neutral, neutral, neutral), WindowsAccentColor, amount); }
        private static Color accentMark;
        internal static Color AccentFill { get { return WindowsAccentColor.IsEmpty ? Accent : WindowsAccentColor; } }
        internal static Color AccentMark(Color original) { return WindowsAccentColor.IsEmpty ? original : accentMark; }
        internal static Color AcrylicTint { get { return WindowsAccentColor.IsEmpty ? Color.FromArgb(16, 11, 16) : Tint(12, .12); } }
        public static Color Background { get { return IsGlass ? Color.Black : !WindowsAccentColor.IsEmpty ? Tint(IsLight ? 249 : 11, .025) : IsLight ? Color.FromArgb(250, 246, 248) : Color.FromArgb(12, 10, 12); } }
        public static Color Surface { get { return IsLight ? Color.White : WindowsAccentColor.IsEmpty ? Color.FromArgb(21, 15, 19) : Tint(17, .04); } }
        public static Color Border { get { return !WindowsAccentColor.IsEmpty ? Tint(IsLight ? 218 : 35, .14) : IsLight ? Color.FromArgb(226, 208, 216) : Color.FromArgb(48, 29, 38); } }
        public static Color Text { get { return IsLight ? Color.FromArgb(43, 28, 36) : Color.FromArgb(233, 229, 231); } }
        public static Color Muted { get { return IsLight ? Color.FromArgb(118, 96, 106) : Color.FromArgb(166, 155, 161); } }
        public static Color Accent { get { return !WindowsAccentColor.IsEmpty ? readableAccent : IsLight ? Color.FromArgb(182, 22, 75) : Color.FromArgb(225, 31, 94); } }
        public static Color AccentHover { get { return !WindowsAccentColor.IsEmpty ? UiMotion.Blend(Accent, IsLight ? Color.Black : Color.White, .16) : IsLight ? Color.FromArgb(149, 15, 60) : Color.FromArgb(244, 72, 126); } }
        public static Color HoverSurface { get { return !WindowsAccentColor.IsEmpty ? Tint(IsLight ? 250 : 17, .10) : IsLight ? Color.FromArgb(250, 232, 239) : Color.FromArgb(36, 18, 27); } }
        public static Color SelectedSurface { get { return !WindowsAccentColor.IsEmpty ? Tint(IsLight ? 248 : 18, .20) : IsLight ? Color.FromArgb(248, 219, 230) : Color.FromArgb(49, 19, 31); } }
        public static Color ToggleOff { get { return !WindowsAccentColor.IsEmpty ? Tint(IsLight ? 195 : 32, .09) : IsLight ? Color.FromArgb(207, 183, 194) : Color.FromArgb(44, 26, 35); } }
        public static Color ToggleThumb { get { return IsLight ? Color.White : Color.FromArgb(12, 10, 12); } }
        public static Color Paused { get { return IsLight ? Color.FromArgb(157, 105, 32) : Color.FromArgb(224, 176, 99); } }
        public static Color EditorBackground { get { return IsLight ? Color.White : Color.FromArgb(16, 13, 16); } }
        public static Color EditorGutter { get { return IsLight ? Background : WindowsAccentColor.IsEmpty ? Color.FromArgb(25, 19, 24) : Tint(21, .045); } }
        public static Color EditorLineNumber { get { return Muted; } }
        public static Color CheckerFirst { get { return IsLight ? Color.FromArgb(241, 235, 238) : Color.FromArgb(38, 32, 36); } }
        public static Color CheckerSecond { get { return IsLight ? Color.FromArgb(225, 216, 221) : Color.FromArgb(49, 42, 47); } }

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
                    SelectedSurface, Background))
                using (var edge = new Pen(Border, 1.2f))
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
