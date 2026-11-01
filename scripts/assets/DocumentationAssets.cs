using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace DesktopHover
{
    // Render the actual application controls with public demonstration files.
    internal static class DocumentationAssets
    {
        private static string root, fixtures, output;
        [STAThread] private static int Main(string[] args)
        {
            if (DocumentPreviewWorker.TryRunWorker(args)) return Environment.ExitCode;
            try
            {
                root = Path.GetFullPath(args[0]);
                fixtures = Path.Combine(root, ".verification", "readme-assets", "fixtures");
                output = Path.Combine(root, "docs", "assets");
                Directory.CreateDirectory(fixtures); Directory.CreateDirectory(output);
                Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
                UiMotion.Enabled = false; Brand.SetTheme("dark");
                if (args[1] == "artwork") { IconAssets(); SampleArtwork(); }
                else if (args[1] == "tray") TrayScreenshot();
                else Screenshots();
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }

        private static Bitmap Logo(int size)
        {
            using (Bitmap original = Brand.CreateLogoBitmap(size * 4))
            {
                var result = new Bitmap(size, size, PixelFormat.Format32bppArgb);
                using (Graphics g = Graphics.FromImage(result))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(original, new Rectangle(0, 0, size, size));
                }
                return result;
            }
        }

        private static void IconAssets()
        {
            using (Bitmap logo = Logo(512)) logo.Save(Path.Combine(root, "assets", "icon-preview.png"), ImageFormat.Png);
            int[] sizes = { 16, 20, 24, 32, 48, 64, 128, 256 };
            var frames = new List<byte[]>();
            foreach (int size in sizes)
                using (Bitmap bitmap = Logo(size))
                using (var stream = new MemoryStream())
                { bitmap.Save(stream, ImageFormat.Png); frames.Add(stream.ToArray()); }
            using (var writer = new BinaryWriter(File.Create(Path.Combine(root, "assets", "DesktopHover.ico"))))
            {
                writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
                int offset = 6 + sizes.Length * 16;
                for (int i = 0; i < sizes.Length; i++)
                {
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)(sizes[i] == 256 ? 0 : sizes[i]));
                    writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((ushort)1); writer.Write((ushort)32);
                    writer.Write(frames[i].Length); writer.Write(offset); offset += frames[i].Length;
                }
                foreach (byte[] frame in frames) writer.Write(frame);
            }
            Console.WriteLine("Rendered logo and eight icon sizes.");
        }

        private static void SampleArtwork()
        {
            using (var landscape = new Bitmap(1280, 800))
            using (Graphics g = Graphics.FromImage(landscape))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var sky = new LinearGradientBrush(new Rectangle(0, 0, 1280, 800), Color.FromArgb(27, 11, 26), Color.FromArgb(125, 37, 72), 90))
                    g.FillRectangle(sky, 0, 0, 1280, 800);
                using (var sun = new SolidBrush(Color.FromArgb(246, 112, 150))) g.FillEllipse(sun, 840, 120, 200, 200);
                Polygon(g, Color.FromArgb(110, 36, 66), new[] { new Point(0, 470), new Point(200, 220), new Point(360, 370), new Point(540, 180), new Point(800, 480), new Point(1050, 300), new Point(1280, 470), new Point(1280, 800), new Point(0, 800) });
                Polygon(g, Color.FromArgb(60, 25, 44), new[] { new Point(0, 480), new Point(160, 370), new Point(380, 530), new Point(670, 320), new Point(940, 490), new Point(1130, 400), new Point(1280, 490), new Point(1280, 800), new Point(0, 800) });
                using (var water = new LinearGradientBrush(new Rectangle(0, 520, 1280, 280), Color.FromArgb(77, 29, 53), Color.FromArgb(18, 13, 23), 90))
                    g.FillRectangle(water, 0, 520, 1280, 280);
                for (int i = 0; i < 12; i++)
                    using (var pen = new Pen(Color.FromArgb(115 - i * 8, 240, 93, 140), 2))
                        g.DrawLine(pen, 825 - i * 10, 548 + i * 15, 1055 + i * 10, 548 + i * 15);
                Polygon(g, Color.FromArgb(15, 11, 19), new[] { new Point(0, 660), new Point(140, 610), new Point(390, 710), new Point(690, 750), new Point(1280, 670), new Point(1280, 800), new Point(0, 800) });
                landscape.Save(Path.Combine(fixtures, "Rose landscape.png"), ImageFormat.Png);
            }
            using (var board = new Bitmap(720, 480))
            using (Graphics g = Graphics.FromImage(board))
            {
                g.Clear(Brand.Background); g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
                g.ScaleTransform(.6f, .6f);
                Brand.DrawLogo(g, new Rectangle(64, 48, 48, 48));
                Text(g, "DESKTOP HOVER", 18, FontStyle.Bold, Brand.Muted, new RectangleF(130, 60, 500, 32));
                Text(g, "A closer look.", 68, FontStyle.Bold, Brand.Text, new RectangleF(64, 154, 1000, 96));
                Text(g, "Just a hover away.", 58, FontStyle.Bold, Brand.Accent, new RectangleF(64, 240, 1050, 90));
                Text(g, "Images, documents, media and code. Instantly within reach.", 24, FontStyle.Regular, Brand.Muted, new RectangleF(68, 358, 1050, 48));
                Color[] colors = { Brand.Accent, Brand.SelectedSurface, Brand.Surface, Brand.Text };
                string[] names = { "ROSE", "BURGUNDY", "INK", "PEARL" };
                for (int i = 0; i < colors.Length; i++)
                {
                    SettingsWindow.FillRound(g, new RectangleF(64 + i * 274, 472, 246, 160), 18, colors[i]);
                    Text(g, names[i], 16, FontStyle.Bold, Brand.Muted, new RectangleF(64 + i * 274, 653, 246, 30));
                }
                Text(g, "DESIGN STUDY  /  01", 16, FontStyle.Regular, Brand.Muted, new RectangleF(64, 728, 700, 30));
                board.Save(Path.Combine(fixtures, "Brand board.png"), ImageFormat.Png);
                WritePsd(board, Path.Combine(fixtures, "Brand board.psd"));
            }
            File.WriteAllText(Path.Combine(fixtures, "hello.cpp"),
                "#include <iostream>\n#include <string>\n#include <vector>\n\n// Preview code without opening an editor.\nstruct Preview {\n    std::string filename;\n    int delay_ms = 450;\n};\n\nint main() {\n    const std::vector<Preview> files = {\n        {\"Rose landscape.png\", 450},\n        {\"Project brief.docx\", 450}\n    };\n\n    for (const auto& file : files) {\n        std::cout << \"Ready: \" << file.filename << '\\n';\n    }\n    return 0;\n}\n");
            using (var writer = new BinaryWriter(File.Create(Path.Combine(fixtures, "Ambient loop.wav"))))
            {
                int samples = 22050 * 30;
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(22050); writer.Write(44100);
                writer.Write((short)2); writer.Write((short)16); writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
                writer.Write(new byte[samples * 2]);
            }
            Console.WriteLine("Created original demonstration artwork, PSD, code and silent audio.");
        }

        private static void Polygon(Graphics g, Color color, Point[] points)
        { using (var brush = new SolidBrush(color)) g.FillPolygon(brush, points); }
        private static void Text(Graphics g, string text, float size, FontStyle style, Color color, RectangleF rectangle)
        { using (var font = new Font("Segoe UI", size, style, GraphicsUnit.Pixel)) using (var brush = new SolidBrush(color)) g.DrawString(text, font, brush, rectangle); }
        private static void BigEndian(BinaryWriter writer, int value, int bytes)
        { for (int i = bytes - 1; i >= 0; i--) writer.Write((byte)(value >> (i * 8))); }
        private static void WritePsd(Bitmap bitmap, string path)
        {
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("8BPS")); BigEndian(writer, 1, 2); writer.Write(new byte[6]);
                BigEndian(writer, 3, 2); BigEndian(writer, bitmap.Height, 4); BigEndian(writer, bitmap.Width, 4);
                BigEndian(writer, 8, 2); BigEndian(writer, 3, 2);
                BigEndian(writer, 0, 4); BigEndian(writer, 0, 4); BigEndian(writer, 0, 4); BigEndian(writer, 0, 2);
                for (int channel = 0; channel < 3; channel++)
                    for (int y = 0; y < bitmap.Height; y++)
                        for (int x = 0; x < bitmap.Width; x++)
                        { Color color = bitmap.GetPixel(x, y); writer.Write(channel == 0 ? color.R : channel == 1 ? color.G : color.B); }
            }
        }

        private static AppSettings Settings(string theme)
        { return new AppSettings { Theme = theme, Language = "en", MaxWidth = 720, MaxHeight = 540, DelayMs = 450, VolumePercent = 65, AnimationsEnabled = false }; }
        private static void Screenshots()
        {
            foreach (string theme in new[] { "dark", "light" })
            {
                using (var settings = new SettingsWindow(Settings(theme), false, null, null))
                {
                    settings.ShowCentered(new Point(100, 100)); settings.FitToArea(new Rectangle(0, 0, 1920, 1080), 1);
                    settings.ClientSize = new Size(1000, 780); settings.ActiveControl = null;
                    Pump(100); Snapshot(settings, "settings-" + theme + ".png");
                }
                Preview("hello.cpp", "code-" + theme + ".png", theme);
            }
            Preview("Rose landscape.png", "image-preview.png", "dark");
            Preview("Brand board.psd", "photoshop-preview.png", "dark");
            Preview("Project brief.docx", "word-preview.png", "dark");
            Preview("Launch budget.xlsx", "excel-preview.png", "dark");
            Preview("Desktop Hover.pptx", "powerpoint-preview.png", "dark");
            Preview("Ambient loop.wav", "audio-preview.png", "dark");
            TrayScreenshot();
        }
        private static void TrayScreenshot()
        {
            AppSettings current = Settings("dark");
            using (var menu = new TrayMenu(() => current, () => false, value => current = value, value => { }, () => { }, () => { }))
            {
                menu.SetHotkeyState(true, true);
                menu.Show(new Point(100, 100)); Pump(100); Snapshot(menu, "tray-menu.png"); menu.Close();
            }
        }
        private static T Field<T>(object value, string name)
        { return (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value); }
        private static void Preview(string filename, string screenshot, string theme)
        {
            using (var preview = new PreviewWindow())
            {
                preview.Configure(Settings(theme));
                if (!preview.ShowFile(Path.Combine(fixtures, filename), new Point(100, 100), Rectangle.Empty)) throw new InvalidOperationException(filename);
                var clock = Stopwatch.StartNew();
                while (Field<bool>(preview, "documentLoading") || Field<object>(preview, "imageTask") != null || preview.MediaState == "media_loading")
                { if (clock.ElapsedMilliseconds > 75000) throw new TimeoutException(filename); Pump(30); }
                if (filename.EndsWith(".wav"))
                { if (preview.MediaState != "media_playing") throw new InvalidOperationException(filename + ": " + preview.MediaState); }
                else if (filename.EndsWith(".pptx") && preview.MediaState != "document_powerpoint")
                    throw new InvalidOperationException(filename + ": native slide rendering did not complete (" + preview.MediaState + ").");
                else if (!filename.EndsWith(".cpp") && Field<Bitmap>(preview, "preview") == null)
                    throw new InvalidOperationException(filename + ": " + preview.MediaState);
                Pump(100); Snapshot(preview, screenshot); preview.ClearPreview();
            }
        }
        private static void Snapshot(Control control, string filename)
        {
            using (var bitmap = new Bitmap(control.Width, control.Height, PixelFormat.Format32bppArgb))
            { control.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, filename), ImageFormat.Png); }
            Console.WriteLine("Rendered " + filename);
        }
        private static void Pump(int milliseconds)
        { var watch = Stopwatch.StartNew(); while (watch.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); } }
    }
}
