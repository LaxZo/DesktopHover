using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Text;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static class Regression
    {
        private static int assertions;
        private static readonly BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string output;
        [STAThread] private static int Main(string[] args)
        {
            if (DocumentPreviewWorker.TryRunWorker(args)) return Environment.ExitCode;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            output = Path.GetFullPath(args.Length > 0 ? args[0] : ".verification/regression");
            Directory.CreateDirectory(output);
            try
            {
                if (args.Length > 1 && args[1] == "settings")
                {
                    SettingsLayout(); SettingsSaving();
                    Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                if (args.Length > 1 && args[1] == "video")
                {
                    VideoLoop(args.Length > 2 ? args[2] : null);
                    Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                if (args.Length > 1 && args[1] == "tray")
                {
                    TrayLayoutAndMotion();
                    Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                Images();
                AnimatedImages();
                SettingsLayout();
                SettingsSaving();
                TrayLayoutAndMotion();
                FolderLookup();
                Preferences();
                PinnedViewer();
                Playback();
                VideoLoop(null);
                Hotkeys();
                if (args.Length > 1) NativeSlides(args[1]);
                Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
            }
            catch (Exception error) { Console.WriteLine(error); return 1; }
        }
        private static void Images()
        {
            string path = Path.Combine(output, "image.png");
            WriteImage(path, Color.CornflowerBlue);
            var gate = (SemaphoreSlim)typeof(ImagePreviewLoader).GetField("decoder", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            using (var window = new PreviewWindow())
            {
                window.Configure(new AppSettings { AnimationsEnabled = false });
                gate.Wait();
                try
                {
                    var elapsed = Stopwatch.StartNew();
                    Check(window.ShowImage(path, new Point(200, 200), Rectangle.Empty), "image request accepted");
                    Check(elapsed.ElapsedMilliseconds < 1000, "UI returns while decoder is busy");
                    Pump(80); Check(window.MediaState == "preview_loading", "loading state stays responsive");
                    window.ClearPreview();
                }
                finally { gate.Release(); }
                Pump(100);
                Check(!window.Visible && window.CurrentPath == null, "cancelled image cannot reappear");
                window.ShowImage(path, new Point(200, 200), Rectangle.Empty);
                Until(() => Field<Bitmap>(window, "preview") != null, 5000);
                Check(window.DisplayedImageSize == new Size(640, 360), "background result has correct dimensions");
                Check(Field<Bitmap>(window, "preview").GetPixel(30, 30).B > 200, "background result has expected pixels");
                window.ClearPreview();
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                WriteImage(path, Color.Tomato);
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
                window.ShowImage(path, new Point(200, 200), Rectangle.Empty);
                Until(() => Field<Bitmap>(window, "preview") != null, 5000);
                Check(Field<Bitmap>(window, "preview").GetPixel(30, 30).R > 240, "changed file invalidates image cache");
                window.ClearPreview();
            }
        }
        private static void NativeSlides(string path)
        {
            using (var window = new PreviewWindow())
            {
                window.Configure(new AppSettings { AnimationsEnabled = false, MaxWidth = 920, MaxHeight = 690, SlideIntervalSeconds = 1 });
                var elapsed = Stopwatch.StartNew();
                Check(window.ShowFile(path, new Point(200, 200), Rectangle.Empty), "native presentation accepted");
                Until(() => Field<byte[][]>(window, "documentSlides") != null || !Field<bool>(window, "documentLoading"), 65000);
                long firstSlide = elapsed.ElapsedMilliseconds;
                Check(Field<byte[][]>(window, "documentSlides") != null, "first native slide is available");
                Until(() => !Field<bool>(window, "documentLoading"), 65000);
                long completed = elapsed.ElapsedMilliseconds;
                Check(Field<byte[][]>(window, "documentSlides").Length == 63, "all 63 slides complete");
                Check(firstSlide < completed, "first slide arrives before full export");
                Console.WriteLine("TIMING first native slide " + firstSlide + " ms; full export " + completed + " ms");
                int before = Field<int>(window, "documentSlideIndex");
                Until(() => Field<int>(window, "documentSlideIndex") != before, 2000);
                window.ClearPreview(); elapsed.Restart();
                window.ShowFile(path, new Point(200, 200), Rectangle.Empty);
                Until(() => !Field<bool>(window, "documentLoading"), 3000);
                Check(Field<byte[][]>(window, "documentSlides").Length == 63, "repeat hover reuses complete cache");
                Console.WriteLine("TIMING cached presentation " + elapsed.ElapsedMilliseconds + " ms");
            }
            using (var pinned = new PreviewWindow(true))
            {
                pinned.Configure(new AppSettings { AnimationsEnabled = false, MaxWidth = 920, MaxHeight = 690, SlideIntervalSeconds = 1 });
                pinned.ShowFile(path, new Point(200, 200), Rectangle.Empty);
                Until(() => !Field<bool>(pinned, "documentLoading"), 3000);
                Field<ToolStripButton>(pinned, "playbackButton").PerformClick();
                Check(!Field<System.Windows.Forms.Timer>(pinned, "slideshowTimer").Enabled, "pinned slideshow can pause");
                Field<ToolStripButton>(pinned, "nextButton").PerformClick();
                Check(Field<int>(pinned, "documentSlideIndex") == 1, "pinned slideshow can advance manually");
                Field<ToolStripButton>(pinned, "previousButton").PerformClick();
                Check(Field<int>(pinned, "documentSlideIndex") == 0, "pinned slideshow can go back");
                Snapshot(pinned, "pinned-powerpoint.png");
            }
        }
        private static void AnimatedImages()
        {
            string path = Path.Combine(output, "two-frames.gif");
            using (var writer = new BinaryWriter(File.Create(path)))
            {
                writer.Write(Encoding.ASCII.GetBytes("GIF89a"));
                writer.Write((short)1); writer.Write((short)1); writer.Write(new byte[] { 128, 0, 0, 255, 0, 0, 0, 0, 255 });
                writer.Write(new byte[] { 33, 255, 11 }); writer.Write(Encoding.ASCII.GetBytes("NETSCAPE2.0"));
                writer.Write(new byte[] { 3, 1, 0, 0, 0 });
                for (int frame = 0; frame < 2; frame++)
                {
                    writer.Write(new byte[] { 33, 249, 4, 0, 10, 0, 0, 0, 44, 0, 0, 0, 0, 1, 0, 1, 0, 0, 2, 2 });
                    writer.Write((byte)(frame == 0 ? 68 : 76)); writer.Write(new byte[] { 1, 0 });
                }
                writer.Write((byte)59);
            }
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { AnimationsEnabled = false });
                window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                Until(() => window.HasImage, 5000);
                int first = Field<Bitmap>(window, "preview").GetPixel(0, 0).ToArgb();
                Until(() => Field<Bitmap>(window, "preview").GetPixel(0, 0).ToArgb() != first, 1000);
                Check(Field<AnimatedGif>(window, "animatedGif") != null, "background GIF decoding retains animation");
                Field<ToolStripButton>(window, "playbackButton").PerformClick();
                int paused = Field<Bitmap>(window, "preview").GetPixel(0, 0).ToArgb();
                Pump(250);
                Check(Field<Bitmap>(window, "preview").GetPixel(0, 0).ToArgb() == paused, "pinned GIF can pause");
                window.ClearPreview();
                using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
        }
        private static void SettingsLayout()
        {
            using (var window = new SettingsWindow(new AppSettings { Language = "lv", AnimationsEnabled = false }, false, null, null))
            {
                window.ShowCentered(new Point(300, 300));
                window.FitToArea(new Rectangle(0, 0, 1366, 728), 1);
                Pump(50);
                Check(window.MaximizeBox && window.MinimizeBox && window.FormBorderStyle == FormBorderStyle.None,
                    "custom window controls replace the native caption");
                Check(window.Width >= 1000 && Field<bool>(window, "wideLayout"), "settings open as a wide two-column app window");
                var viewport = window.SettingsViewport;
                Check(window.Height <= 704 && viewport.VerticalScroll.Visible, "settings fit 768p screen with scrolling");
                Control last = viewport.Controls["language-picker"];
                viewport.ScrollControlIntoView(last); Pump(50);
                Check(viewport.ClientRectangle.Contains(last.Bounds), "bottom settings remain reachable");
                var maximize = Field<WindowCaptionButton>(window, "maximizeButton");
                Check(maximize.Top == 8 && maximize.Parent.Top == 0, "caption buttons stay fixed when settings scroll");
                Snapshot(window, "settings-small-screen.png");
                window.ClientSize = new Size(1000, 780); Pump(50);
                Check(!viewport.VerticalScroll.Visible && viewport.ClientRectangle.Contains(last.Bounds), "wide desktop layout shows every setting without scrolling");
                Check(viewport.Controls["autoplay-toggle"].Left > Field<HoverSlider>(window, "widthSlider").Right,
                    "playback switches occupy a separate column without overlap");
                Snapshot(window, "settings-app-window.png");
                Size chosen = window.Size;
                window.ShowCentered(new Point(300, 300));
                Check(window.Size == chosen, "opening settings again preserves the resized window");
                maximize.PerformClick(); Pump(80);
                window.ShowCentered(new Point(300, 300));
                Check(window.WindowState == FormWindowState.Maximized, "settings support maximization and preserve it on reopen");
                Check(window.RectangleToScreen(window.ClientRectangle) == Screen.FromControl(window).WorkingArea,
                    "maximized custom caption leaves taskbar available");
                Field<WindowCaptionButton>(window, "minimizeButton").PerformClick(); Pump(50);
                window.ShowCentered(new Point(300, 300)); Pump(50);
                Check(window.WindowState == FormWindowState.Maximized, "minimized maximized settings restore correctly");
                maximize.PerformClick();
                Check(window.WindowState == FormWindowState.Normal, "up-chevron restores normal window size");
                Point edge = window.PointToScreen(new Point(1, 120));
                object[] message = { Message.Create(window.Handle, 0x84, IntPtr.Zero, new IntPtr((long)(ushort)edge.X | ((long)(ushort)edge.Y << 16))) };
                typeof(SettingsWindow).GetMethod("WndProc", Private).Invoke(window, message);
                Check(((Message)message[0]).Result.ToInt32() == 10, "custom window retains native edge resizing");
                window.ClientSize = new Size(650, 600); Pump(50);
                Check(!Field<bool>(window, "wideLayout") && viewport.VerticalScroll.Visible, "narrow window reflows into one scrolling column");
                viewport.ScrollControlIntoView(last); Pump(50);
                Check(viewport.ClientRectangle.Contains(last.Bounds) && !viewport.HorizontalScroll.Visible, "narrow layout keeps footer reachable without horizontal scrolling");
                Snapshot(window, "settings-narrow.png");
                window.FitToArea(new Rectangle(0, 0, 1920, 1040), 1.5f);
                Pump(50);
                Check(window.Height <= 1016 && Field<float>(window, "uiScale") == 1.5f, "settings adapt to 150 percent DPI");
                Snapshot(window, "settings-150-percent.png");
            }
        }
        private static void SettingsSaving()
        {
            int saves = 0; AppSettings applied = null;
            using (var window = new SettingsWindow(new AppSettings { AnimationsEnabled = false }, false,
                value => { saves++; applied = value; }, null))
            {
                window.ShowCentered(new Point(300, 300));
                var slider = Field<HoverSlider>(window, "volumeSlider");
                for (int i = 99; i >= 60; i--) slider.Value = i;
                Check(saves == 0, "slider drag defers disk writes");
                Pump(400); Check(saves == 1 && applied.VolumePercent == 60, "forty slider changes become one save");
                slider.Value = 55; Field<WindowCaptionButton>(window, "closeButton").PerformClick();
                Check(saves == 2 && applied.VolumePercent == 55, "closing settings flushes latest value");
                Check(window.IsDisposed, "custom close button closes settings");
            }
        }
        private static void TrayLayoutAndMotion()
        {
            var settings = new AppSettings { Language = "lv", AnimationsEnabled = false };
            using (var menu = new TrayMenu(() => settings, () => false,
                value => { settings = value; UiMotion.Enabled = value.AnimationsEnabled; }, value => { }, () => { }, () => { }))
            {
                menu.SetHotkeyState(true, true);
                Rectangle work = Screen.PrimaryScreen.WorkingArea;
                Point origin = new Point(work.Right - 20, work.Bottom - 20);
                menu.Show(origin); Pump(30);
                Rectangle settled = menu.Bounds;
                Console.WriteLine("Compact Latvian tray menu: " + menu.Width + " x " + menu.Height);
                Check(menu.Height <= 410 && menu.Width <= 335, "Latvian tray menu stays compact with both shortcuts");
                Check(menu.Visible && menu.Opacity == 1 && Screen.FromRectangle(menu.Bounds).WorkingArea.Contains(menu.Bounds), "disabled tray motion opens immediately inside the screen");
                using (var bitmap = new Bitmap(menu.Width, menu.Height))
                { menu.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size)); bitmap.Save(Path.Combine(output, "tray-compact-lv.png")); }
                menu.Close();
                foreach (var language in L10n.Languages)
                {
                    settings.Language = language.Code; menu.RefreshState(); menu.Show(origin); Pump(10);
                    bool fits = Screen.FromRectangle(menu.Bounds).WorkingArea.Contains(menu.Bounds);
                    foreach (ToolStripItem item in menu.Items)
                        if (item.Available) fits &= item.Bounds.Top >= 0 && item.Bounds.Bottom <= menu.Height;
                    Check(fits, "tray menu keeps all rows visible: " + language.Code);
                    menu.Close();
                }
                settings.Language = "lv"; settings.AnimationsEnabled = true; menu.RefreshState();
                menu.Show(origin);
                Check(menu.Opacity < 1 && VisibleMenuArea(menu) < menu.Width * menu.Height * .85,
                    "tray entrance starts faded and reveals from a corner");
                Pump(50);
                Check(menu.Visible && Screen.FromRectangle(menu.Bounds).WorkingArea.Contains(menu.Bounds), "moving tray entrance stays inside the work area");
                Pump(300);
                Check(menu.Visible && menu.Opacity == 1 && menu.Bounds == settled,
                    "tray entrance finishes at the normal Windows menu position");
                Check(VisibleMenuArea(menu) > menu.Width * menu.Height * .98, "completed entrance restores the full rounded menu");
                menu.Close(); menu.Show(origin); menu.Close(); Pump(300);
                Check(!menu.Visible && menu.Opacity == 1, "closing during entrance cancels motion without reopening the menu");
                menu.Show(origin); UiMotion.Enabled = false;
                Check(menu.Visible && menu.Opacity == 1 && menu.Bounds == settled && VisibleMenuArea(menu) > menu.Width * menu.Height * .98,
                    "disabling motion mid-entrance restores full menu immediately");
                menu.Close(); UiMotion.Enabled = true; menu.Show(origin);
                typeof(Control).GetMethod("OnKeyDown", Private).Invoke(menu, new object[] { new KeyEventArgs(Keys.Down) });
                Check(menu.Opacity == 1 && menu.Bounds == settled, "keyboard navigation finishes the entrance before selecting items");
                var sizes = (ToolStripMenuItem)menu.Items["image-size"];
                sizes.ShowDropDown(); Pump(250);
                Check(sizes.DropDown.Visible && sizes.DropDown.Opacity == 1, "compact submenu remains usable after entrance");
                menu.Close(); Pump(30);
                Check(!sizes.DropDown.Visible, "closing the main menu closes its submenu");
            }
            UiMotion.Enabled = true;
        }

        private static float VisibleMenuArea(ToolStripDropDown menu)
        {
            if (menu.Region == null) return menu.Width * menu.Height;
            using (Graphics graphics = menu.CreateGraphics())
            { RectangleF bounds = menu.Region.GetBounds(graphics); return bounds.Width * bounds.Height; }
        }

        private static void FolderLookup()
        {
            var index = new FolderNameIndex();
            for (int i = 0; i < 25000; i++) index.Add("file" + i, "C:\\fixture\\file" + i + ".png", true);
            Check(index.Find("file24999") != null, "folder index supports more than 20000 entries");
            index.Add("same", "C:\\fixture\\a.jpg", true); index.Add("same", "C:\\fixture\\a.png", true);
            Check(index.Find("same") == null, "ambiguous display names never choose an arbitrary file");
            string directory = Path.Combine(output, "watched"); Directory.CreateDirectory(directory);
            int builds = 0;
            using (var cache = new FolderLookupCache())
            {
                Func<FolderNameIndex> build = () => { builds++; return index; };
                cache.Resolve(directory, "file3", build); cache.Resolve(directory, "file4", build);
                Check(builds == 1, "repeat hover reuses folder index");
                string created = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".txt");
                File.WriteAllText(created, "fixture"); Pump(150);
                cache.Resolve(directory, "file3", build);
                Check(builds == 2, "folder changes invalidate cached names");
                File.Delete(created);
            }
        }
        private static void Preferences()
        {
            string path = Path.Combine(output, "preferences.ini");
            var settings = new AppSettings { AutoPlayMedia = false, LoopVideo = false, PreviewAudio = false, PreviewText = false };
            settings.Save(path);
            AppSettings restored = AppSettings.Load(path);
            Check(!restored.AutoPlayMedia && !restored.PreviewAudio && !restored.PreviewText && restored.PreviewImages,
                "autoplay and file filters survive restart");
            Check(!restored.LoopVideo, "loop preference survives restart");
            Check(AppSettings.Load(Path.Combine(output, "missing.ini")).LoopVideo, "video looping defaults to enabled");
            using (var window = new PreviewWindow())
            {
                window.Configure(restored);
                Check(!window.ShowFile(Path.Combine(output, "disabled.mp3"), Point.Empty, Rectangle.Empty), "disabled media type is not opened");
            }
            using (var settingsWindow = new SettingsWindow(restored, false, value => settings = value, null))
            {
                settingsWindow.ShowCentered(new Point(300, 300));
                var autoplay = (HoverToggle)settingsWindow.SettingsViewport.Controls["autoplay-toggle"];
                settingsWindow.SettingsViewport.ScrollControlIntoView(autoplay);
                autoplay.PerformClick();
                Check(settings.AutoPlayMedia, "autoplay switch applies immediately");
                var loop = (HoverToggle)settingsWindow.SettingsViewport.Controls["loop-video-toggle"];
                loop.PerformClick(); Check(settings.LoopVideo, "video loop switch applies immediately");
            }
        }
        private static void PinnedViewer()
        {
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { Language = "lv", AnimationsEnabled = false });
                window.ShowFile(Path.Combine(output, "image.png"), new Point(300, 300), Rectangle.Empty);
                Until(() => Field<Bitmap>(window, "preview") != null, 5000);
                Check(window.IsInteractive && window.ShowInTaskbar, "pinned preview is an interactive taskbar window");
                Field<ToolStripButton>(window, "zoomInButton").PerformClick();
                Check(Field<float>(window, "zoom") > 1, "pinned preview supports zoom");
                Field<ToolStripButton>(window, "fitButton").PerformClick();
                Check(Field<float>(window, "zoom") == 1, "fit restores image scale");
                Snapshot(window, "pinned-image.png");
                string pdf = Path.Combine(output, "two-pages.pdf"); WritePdf(pdf);
                window.ShowFile(pdf, new Point(300, 300), Rectangle.Empty);
                Until(() => !Field<bool>(window, "documentLoading"), 12000);
                Check(Field<int>(window, "pdfPageCount") == 2, "PDF reports page count");
                Check(Field<Bitmap>(window, "preview").GetPixel(100, 100).R > 200, "PDF first page is red");
                Field<ToolStripButton>(window, "nextButton").PerformClick();
                Until(() => !Field<bool>(window, "documentLoading"), 12000);
                Check(Field<int>(window, "pdfPageIndex") == 1, "PDF navigation selects second page");
                Check(Field<Bitmap>(window, "preview").GetPixel(100, 100).B > 200, "PDF second page is blue");
                Snapshot(window, "pinned-pdf.png");
                Field<ToolStripButton>(window, "previousButton").PerformClick();
                window.ClearPreview(); Pump(150);
                Check(!window.Visible && window.CurrentPath == null, "cancelled PDF page cannot reopen window");
            }
        }
        private static void WritePdf(string path)
        {
            string[] objects = {
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R 5 0 R] /Count 2 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 300] /Resources << >> /Contents 4 0 R >>",
                "<< /Length 27 >>\nstream\n1 0 0 rg 0 0 400 300 re f\n\nendstream",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 300] /Resources << >> /Contents 6 0 R >>",
                "<< /Length 27 >>\nstream\n0 0 1 rg 0 0 400 300 re f\n\nendstream"
            };
            var text = new StringBuilder("%PDF-1.4\n"); var offsets = new int[objects.Length];
            for (int i = 0; i < objects.Length; i++) { offsets[i] = text.Length; text.Append((i + 1) + " 0 obj\n" + objects[i] + "\nendobj\n"); }
            int xref = text.Length;
            text.Append("xref\n0 7\n0000000000 65535 f \n");
            foreach (int offset in offsets) text.Append(offset.ToString("D10") + " 00000 n \n");
            text.Append("trailer\n<< /Size 7 /Root 1 0 R >>\nstartxref\n" + xref + "\n%%EOF\n");
            File.WriteAllText(path, text.ToString(), Encoding.ASCII);
        }
        private static void Playback()
        {
            string wav = Path.Combine(output, "silent.wav");
            using (var writer = new BinaryWriter(File.Create(wav)))
            {
                writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(96036);
                writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000);
                writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data"));
                writer.Write(96000); writer.Write(new byte[96000]);
            }
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { AutoPlayMedia = false, VolumePercent = 0, AnimationsEnabled = false });
                window.ShowFile(wav, new Point(300, 300), Rectangle.Empty);
                Until(() => window.MediaState != "media_loading", 16000);
                Check(window.MediaState == "media_paused", "autoplay off opens media paused");
                Pump(250); Check(window.PlaybackPosition.TotalMilliseconds < 100, "paused media does not advance");
                Field<ToolStripButton>(window, "playbackButton").PerformClick();
                Pump(450); Check(window.PlaybackPosition.TotalMilliseconds > 100, "play button starts media");
                Field<ToolStripButton>(window, "playbackButton").PerformClick();
                Pump(100); double paused = window.PlaybackPosition.TotalMilliseconds;
                Pump(250); Check(Math.Abs(window.PlaybackPosition.TotalMilliseconds - paused) < 100, "pause button stops media position");
            }
        }
        private static void VideoLoop(string path)
        {
            if (path == null) { path = Path.Combine(output, "short-video.avi"); WriteVideo(path); }
            using (var window = new PreviewWindow(true))
            {
                var settings = new AppSettings { AutoPlayMedia = true, LoopVideo = true, VolumePercent = 0, AnimationsEnabled = false };
                window.Configure(settings); window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                Until(() => window.MediaState != "media_loading", 16000);
                MediaPlayback session = Field<MediaPlayback>(window, "media");
                Check(session != null && session.HasVideo && session.Duration > TimeSpan.Zero, "short clip opens as actual video");
                Console.WriteLine("VIDEO duration " + session.Duration.TotalMilliseconds + " ms");
                int ended = 0; session.Ended += delegate { ended++; };
                // Real user clips may be longer than the generated 600 ms fixture.
                var player = Field<System.Windows.Media.MediaPlayer>(session, "player");
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    if (session.Duration.TotalSeconds > 3) player.Position = session.Duration - TimeSpan.FromMilliseconds(250);
                    int previous = ended;
                    Until(() => ended > previous, 6000);
                    Until(() => !session.HasEnded && window.PlaybackPosition < session.Duration, 3000);
                }
                Check(window.MediaState == "media_playing", "video keeps playing after multiple real end events");
                Field<ToolStripButton>(window, "playbackButton").PerformClick();
                Pump(120); double paused = window.PlaybackPosition.TotalMilliseconds; int pausedEnds = ended;
                Pump(750);
                Check(window.MediaState == "media_paused" && ended == pausedEnds && Math.Abs(window.PlaybackPosition.TotalMilliseconds - paused) < 80,
                    "pause prevents looping and holds the video position");
                Field<ToolStripButton>(window, "playbackButton").PerformClick();
                settings.LoopVideo = false; window.Configure(settings);
                player.Position = session.Duration - TimeSpan.FromMilliseconds(150);
                Until(() => window.MediaState == "media_ended", 5000);
                int stoppedEnds = ended; Pump(450);
                Check(ended == stoppedEnds, "disabling loop stops at the next end");
                settings.LoopVideo = true; window.Configure(settings);
                Until(() => window.MediaState == "media_playing" && !session.HasEnded, 3000);
                Check(!Field<bool>(window, "playbackPaused"), "enabling loop restarts an already finished video");
                window.ClearPreview(); Pump(800);
                Check(!window.Visible && window.CurrentPath == null, "closing a looping preview cannot reopen it");
                settings.AutoPlayMedia = false; window.Configure(settings);
                window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                Until(() => window.MediaState != "media_loading", 16000); Pump(750);
                Check(window.MediaState == "media_paused" && window.PlaybackPosition.TotalMilliseconds < 100,
                    "loop does not bypass autoplay being off");
            }
            using (var hover = new PreviewWindow())
            {
                hover.Configure(new AppSettings { LoopVideo = true, VolumePercent = 0, AnimationsEnabled = false });
                hover.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                Until(() => hover.MediaState != "media_loading", 16000);
                var session = Field<MediaPlayback>(hover, "media");
                int ended = 0; session.Ended += delegate { ended++; };
                if (session.Duration.TotalSeconds > 3)
                    Field<System.Windows.Media.MediaPlayer>(session, "player").Position = session.Duration - TimeSpan.FromMilliseconds(150);
                Until(() => ended > 0 && !session.HasEnded, 6000);
                Check(hover.MediaState == "media_playing", "passive hover previews also loop video");
                hover.ClearPreview();
            }
        }
        private static long BeginChunk(BinaryWriter writer, string name, string type)
        {
            writer.Write(Encoding.ASCII.GetBytes(name)); long size = writer.BaseStream.Position;
            writer.Write(0); if (type != null) writer.Write(Encoding.ASCII.GetBytes(type)); return size;
        }
        private static void EndChunk(BinaryWriter writer, long size)
        {
            long end = writer.BaseStream.Position; writer.BaseStream.Position = size;
            writer.Write((int)(end - size - 4)); writer.BaseStream.Position = end;
            if ((end & 1) != 0) writer.Write((byte)0);
        }
        private static void WriteVideo(string path)
        {
            const int width = 64, height = 48, frames = 6, bytes = width * height * 3;
            using (var w = new BinaryWriter(File.Create(path)))
            {
                long riff = BeginChunk(w, "RIFF", "AVI "), header = BeginChunk(w, "LIST", "hdrl");
                long chunk = BeginChunk(w, "avih", null);
                foreach (int value in new[] { 100000, bytes * 10, 0, 16, frames, 0, 1, bytes, width, height, 0, 0, 0, 0 }) w.Write(value);
                EndChunk(w, chunk); long stream = BeginChunk(w, "LIST", "strl"); chunk = BeginChunk(w, "strh", null);
                w.Write(Encoding.ASCII.GetBytes("vidsDIB ")); w.Write(0); w.Write((short)0); w.Write((short)0);
                foreach (int value in new[] { 0, 1, 10, 0, frames, bytes, -1, 0 }) w.Write(value);
                w.Write((short)0); w.Write((short)0); w.Write((short)width); w.Write((short)height); EndChunk(w, chunk);
                chunk = BeginChunk(w, "strf", null); w.Write(40); w.Write(width); w.Write(height); w.Write((short)1); w.Write((short)24);
                foreach (int value in new[] { 0, bytes, 0, 0, 0, 0 }) w.Write(value);
                EndChunk(w, chunk); EndChunk(w, stream); EndChunk(w, header);
                long movie = BeginChunk(w, "LIST", "movi"); int[] offsets = new int[frames];
                for (int i = 0; i < frames; i++)
                {
                    offsets[i] = (int)(w.BaseStream.Position - movie - 4); chunk = BeginChunk(w, "00db", null);
                    for (int pixel = 0; pixel < width * height; pixel++)
                    { w.Write((byte)(i % 2 == 0 ? 255 : 0)); w.Write((byte)50); w.Write((byte)(i % 2 == 0 ? 0 : 255)); }
                    EndChunk(w, chunk);
                }
                EndChunk(w, movie); chunk = BeginChunk(w, "idx1", null);
                foreach (int offset in offsets) { w.Write(Encoding.ASCII.GetBytes("00db")); w.Write(16); w.Write(offset); w.Write(bytes); }
                EndChunk(w, chunk); EndChunk(w, riff);
            }
        }
        private static void Hotkeys()
        {
            int toggles = 0, pins = 0;
            using (var hotkeys = new AppHotkeys(() => toggles++, () => pins++))
            {
                var handler = typeof(AppHotkeys).GetMethod("WndProc", Private);
                handler.Invoke(hotkeys, new object[] { Message.Create(hotkeys.Handle, 0x312, new IntPtr(1), IntPtr.Zero) });
                handler.Invoke(hotkeys, new object[] { Message.Create(hotkeys.Handle, 0x312, new IntPtr(2), IntPtr.Zero) });
                Check(toggles == 1 && pins == 1, "global hotkey messages dispatch distinct actions");
                using (var duplicate = new AppHotkeys(() => { }, () => { }))
                {
                    if (hotkeys.ToggleRegistered) Check(!duplicate.ToggleRegistered, "hotkey conflict is detected");
                }
            }
        }
        private static void Snapshot(Form window, string name)
        {
            using (var image = new Bitmap(window.Width, window.Height))
            { window.DrawToBitmap(image, new Rectangle(Point.Empty, image.Size)); image.Save(Path.Combine(output, name)); }
        }
        private static void WriteImage(string path, Color color)
        {
            using (var bitmap = new Bitmap(640, 360))
            {
                using (Graphics graphics = Graphics.FromImage(bitmap)) graphics.Clear(color);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
        private static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name, Private).GetValue(target); }
        private static void Until(Func<bool> ready, int timeout)
        {
            var elapsed = Stopwatch.StartNew();
            while (!ready() && elapsed.ElapsedMilliseconds < timeout) Pump(10);
            Check(ready(), "asynchronous operation completes");
        }
        private static void Pump(int milliseconds)
        {
            var elapsed = Stopwatch.StartNew();
            while (elapsed.ElapsedMilliseconds < milliseconds) { Application.DoEvents(); Thread.Sleep(5); }
            Application.DoEvents();
        }
        private static void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException(description);
            assertions++; Console.WriteLine("OK " + description);
        }
    }
}
