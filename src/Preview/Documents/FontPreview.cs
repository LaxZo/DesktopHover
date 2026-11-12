using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Packaging;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Color = System.Windows.Media.Color;
using Point = System.Windows.Point;
using Size = System.Drawing.Size;

namespace DesktopHover
{
    internal static class FontPreview
    {
        internal static bool Supports(string path)
        {
            string ext = Path.GetExtension(path);
            return String.Equals(ext, ".ttf", StringComparison.OrdinalIgnoreCase) || String.Equals(ext, ".otf", StringComparison.OrdinalIgnoreCase);
        }

        internal static DocumentPreviewResult Load(string path, Size size, OfficePreviewPalette palette, CancellationToken token)
        {
            // A unique package URI avoids WPF's directory font cache, including on F5 after replacing a font.
            var key = new Uri("memory://desktophover/" + Guid.NewGuid().ToString("N"));
            var partUri = new Uri("/sample" + Path.GetExtension(path).ToLowerInvariant(), UriKind.Relative);
            using (var memory = new MemoryStream())
            using (var package = Package.Open(memory, FileMode.Create, FileAccess.ReadWrite))
            {
                var part = package.CreatePart(partUri, "application/vnd.ms-opentype", CompressionOption.NotCompressed);
                using (var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                using (var destination = part.GetStream())
                {
                    const int limit = 64 * 1024 * 1024;
                    if (source.Length > limit) return new DocumentPreviewResult(null, "document_limit");
                    byte[] buffer = new byte[81920];
                    int read, total = 0;
                    while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        token.ThrowIfCancellationRequested();
                        total += read;
                        if (total > limit) return new DocumentPreviewResult(null, "document_limit");
                        destination.Write(buffer, 0, read);
                    }
                }
                package.Flush();
                PackageStore.AddPackage(key, package);
                try { return Draw(new GlyphTypeface(PackUriHelper.Create(key, partUri)), path, size, palette, token); }
                finally { PackageStore.RemovePackage(key); }
            }
        }

        private static DocumentPreviewResult Draw(GlyphTypeface font, string path, Size size, OfficePreviewPalette palette, CancellationToken token)
        {
            string name;
            if (!font.FamilyNames.TryGetValue(CultureInfo.CurrentUICulture, out name) &&
                !font.FamilyNames.TryGetValue(CultureInfo.GetCultureInfo("en-US"), out name))
            {
                name = Path.GetFileNameWithoutExtension(path);
                foreach (var pair in font.FamilyNames) { name = pair.Value; break; }
            }
            var visual = new DrawingVisual();
            using (DrawingContext dc = visual.RenderOpen())
            {
                double s = Math.Min(size.Width / 650.0, size.Height / 320.0);
                dc.DrawRectangle(Brush(palette.Paper), null, new Rect(0, 0, size.Width, size.Height));
                dc.PushTransform(new ScaleTransform(s, s));
                var title = new FormattedText(name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"), 25, Brush(palette.Accent), 1) { MaxTextWidth = 610, MaxLineCount = 1 };
                dc.DrawText(title, new Point(20, 12));
                DrawLine(dc, font, "Aa Bb Cc 0123", 48, 106, Brush(palette.Ink), token);
                DrawLine(dc, font, "ABCDEFGHIJKLMNOPQRSTUVWXYZ", 22, 159, Brush(palette.Ink), token);
                DrawLine(dc, font, "abcdefghijklmnopqrstuvwxyz 0123456789", 18, 197, Brush(palette.Ink), token);
                DrawLine(dc, font, "ĀČĒĢĪĶĻŅŠŪŽ", 34, 251, Brush(palette.Ink), token);
                DrawLine(dc, font, "āčēģīķļņšūž", 22, 290, Brush(palette.Ink), token);
                dc.Pop();
            }
            return new DocumentPreviewResult(VectorBitmap.Render(visual, size.Width, size.Height), "font_sample");
        }

        private static SolidColorBrush Brush(System.Drawing.Color color)
        { return new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)); }

        private static void DrawLine(DrawingContext dc, GlyphTypeface font, string text, double size, double baseline, System.Windows.Media.Brush brush, CancellationToken token)
        {
            var glyphs = new List<ushort>();
            double width = 0;
            foreach (char c in text)
            {
                ushort index; font.CharacterToGlyphMap.TryGetValue(c, out index);
                glyphs.Add(index); width += font.AdvanceWidths[index];
            }
            size = Math.Min(size, 608 / Math.Max(1, width));
            double x = 20;
            foreach (ushort index in glyphs)
            {
                token.ThrowIfCancellationRequested();
                dc.PushTransform(new TranslateTransform(x, baseline));
                dc.DrawGeometry(brush, null, font.GetGlyphOutline(index, size, size)); dc.Pop();
                x += font.AdvanceWidths[index] * size;
            }
        }

    }
}
