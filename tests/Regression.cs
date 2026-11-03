using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
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
            if (InfoTipRestoreGuard.TryRunWorker(args)) return 0;
            if (DocumentPreviewWorker.TryRunWorker(args)) return Environment.ExitCode;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            output = Path.GetFullPath(args.Length > 0 ? args[0] : ".verification/regression");
            Directory.CreateDirectory(output);
            try
            {
                if (args.Length > 1 && args[1] == "settings")
                {
                    SettingsLayout(); SettingsSaving(); FormatPreferences(); ThemePreferences(); GlassCorners();
                    Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                if (args.Length > 1 && args[1] == "themes")
                {
                    ThemePreferences(); GlassCorners();
                    Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                if (args.Length > 1 && args[1] == "corners")
                {
                    GlassCorners();
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
                if (args.Length > 1 && args[1] == "pinned")
                {
                    Images(); PinnedViewer(); AnimatedImages(); Playback(); VideoLoop(null);
                    Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                if (args.Length > 1 && args[1] == "office")
                {
                    OfficeThemes(); OfficeNavigation(); Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                if (args.Length > 1 && args[1] == "fixes")
                {
                    RefreshAndDetail(); AnimatedImages(); SettingsLayout(); OfficeNavigation(); Playback(); Shutdown();
                    Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                if (args.Length > 3 && args[1] == "powerpoint-priority")
                {
                    PowerPointPriority(args[2], args[3]);
                    Console.WriteLine("PASS: " + assertions + " assertions"); return 0;
                }
                Images();
                AnimatedImages();
                SettingsLayout();
                SettingsSaving();
                Shutdown();
                TrayLayoutAndMotion();
                FolderLookup();
                Preferences();
                FormatPreferences();
                ThemePreferences();
                GlassCorners();
                PinnedViewer();
                OfficeThemes();
                OfficeNavigation();
                RefreshAndDetail();
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
        private static void PowerPointPriority(string large, string next)
        {
            PowerPointPreviewReader.Invalidate(large); PowerPointPreviewReader.Invalidate(next);
            int count = 0;
            var old = System.Threading.Tasks.Task.Factory.StartNew(delegate
            {
                using (var result = PowerPointPreviewReader.Load(large, new Size(2800, 2100), CancellationToken.None,
                    value => { if (value.Slides != null) Interlocked.Exchange(ref count, value.Slides.Length); value.Dispose(); })) { }
            });
            Until(() => Volatile.Read(ref count) > 0 || old.IsCompleted, 65000);
            Check(Volatile.Read(ref count) > 0 && !old.IsCompleted, "long native export delivers slides progressively");
            object work = typeof(PowerPointPreviewReader).GetField("pendingExport", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            var timer = Stopwatch.StartNew();
            var newer = System.Threading.Tasks.Task.Factory.StartNew(() => PowerPointPreviewReader.Load(next, new Size(1600, 1200), CancellationToken.None));
            Until(() => newer.IsCompleted, 65000);
            using (var result = newer.GetAwaiter().GetResult())
                Check(result != null && result.Slides != null && result.Slides.Length > 0, "latest presentation receives a native export: " + (result == null ? "no result" : result.DetailKey));
            Until(() => old.IsCompleted, 3000);
            Check(old.IsFaulted && old.Exception != null && old.Exception.GetBaseException() is OperationCanceledException, "superseded request is cancelled");
            Check(Field<byte[][]>(work, "Slides").Length < 200 && Field<CancellationTokenSource>(work, "Cancellation").IsCancellationRequested,
                "old native export stops before all 200 slides");
            Console.WriteLine("Latest presentation ready in " + timer.ElapsedMilliseconds + " ms");
            timer.Restart();
            using (var cached = PowerPointPreviewReader.Load(next, new Size(640, 480), CancellationToken.None))
                Check(cached.Slides != null && cached.Image.Width <= 640, "larger cached export supplies a smaller preview");
            Check(timer.ElapsedMilliseconds < 1000, "cached presentation does not start another native export");
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
            byte[] looping = File.ReadAllBytes(path);
            byte[] finite = new byte[looping.Length - 19];
            Array.Copy(looping, 0, finite, 0, 19); Array.Copy(looping, 38, finite, 19, looping.Length - 38);
            File.WriteAllBytes(path, finite);
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { AnimationsEnabled = false });
                window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                Until(() => Field<bool>(window, "playbackPaused"), 5000);
                var button = Field<ToolStripButton>(window, "playbackButton");
                Check(button.Text == "▶", "finished GIF displays play");
                button.PerformClick();
                Check(!Field<bool>(window, "playbackPaused") && Field<System.Windows.Forms.Timer>(window, "animationTimer").Enabled,
                    "finished GIF restarts on the first click");
                Check(Field<Bitmap>(window, "preview").GetPixel(0, 0).R > 200, "GIF restart selects its first frame");
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
                // Windows' own minimize/maximize animation is independent of UiMotion.
                // Let it settle before issuing the next native state transition.
                maximize.PerformClick(); Pump(400);
                window.ShowCentered(new Point(300, 300));
                Check(window.WindowState == FormWindowState.Maximized, "settings support maximization and preserve it on reopen");
                Check(window.RectangleToScreen(window.ClientRectangle) == Screen.FromControl(window).WorkingArea,
                    "maximized custom caption leaves taskbar available");
                Field<WindowCaptionButton>(window, "minimizeButton").PerformClick(); Pump(400);
                window.ShowCentered(new Point(300, 300)); Pump(400);
                Check(window.WindowState == FormWindowState.Maximized, "minimized maximized settings restore correctly");
                maximize.PerformClick();
                Check(window.WindowState == FormWindowState.Normal, "up-chevron restores normal window size");
                Point edge = window.PointToScreen(new Point(1, 120));
                object[] message = { Message.Create(window.Handle, 0x84, IntPtr.Zero, new IntPtr((long)(ushort)edge.X | ((long)(ushort)edge.Y << 16))) };
                typeof(SettingsWindow).GetMethod("WndProc", Private).Invoke(window, message);
                Check(((Message)message[0]).Result.ToInt32() == 10, "custom window retains native edge resizing");
                Pump(400);
                Check(window.WindowState == FormWindowState.Normal && window.Size == chosen,
                    "restored settings retain their exact original size: " + window.Size + " expected " + chosen);
                for (int cycle = 0; cycle < 3; cycle++)
                {
                    maximize.PerformClick(); Pump(400); maximize.PerformClick(); Pump(400);
                    Check(window.Size == chosen, "settings do not grow after restore cycle " + (cycle + 1));
                }
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
        private static void Shutdown()
        {
            int saved = 0;
            using (var context = new HoverContext())
            {
                typeof(HoverContext).GetField("paused", Private).SetValue(context, true);
                var settings = new SettingsWindow(new AppSettings { AnimationsEnabled = false }, false,
                    value => { if (value.VolumePercent == 42) saved++; }, null);
                typeof(HoverContext).GetField("settingsWindow", Private).SetValue(context, settings);
                settings.ShowCentered(new Point(300, 300));
                Field<HoverSlider>(settings, "volumeSlider").Value = 42;
                Check(saved == 0, "shutdown begins with a pending settings save");
                Program.ExitEvent.Set();
                Call(context, "Tick", null, EventArgs.Empty);
                Check(saved == 1 && settings.IsDisposed && Field<bool>(context, "stopping"), "exit request flushes settings before shutting down");
            }
            int worker;
            using (var guard = new InfoTipRestoreGuard())
            {
                Check(guard.Remember(new InfoTipRestoreGuard.Lease { Window = new IntPtr(1) }), "Explorer guard starts with a harmless invalid lease");
                worker = Field<Process>(guard, "worker").Id;
            }
            bool alive = false;
            foreach (Process process in Process.GetProcesses())
                using (process) if (process.Id == worker) alive = true;
            Check(!alive, "guard consumes pipe EOF and exits before disposal returns");
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
        private static void FormatPreferences()
        {
            string path = Path.Combine(output, "format-preferences.ini");
            var settings = new AppSettings { Language = "lv", AnimationsEnabled = false, DisabledExtensions = " JPG, .jpg, .PNG, ../invalid " };
            Check(settings.DisabledExtensions == ".jpg,.png", "format preferences normalize case, duplicates and invalid values");
            settings.Save(path); settings = AppSettings.Load(path);
            Check(!settings.Allows("photo.JPG") && settings.Allows("photo.jpeg") && settings.Allows("photo.jpg.txt"), "extension filters match exactly and ignore letter case");
            var clone = settings.Clone(); clone.SetExtensionDisabled(".jpg", false);
            Check(settings.IsExtensionDisabled(".jpg") && !clone.IsExtensionDisabled(".jpg"), "settings copies own independent format selections");
            Check(AppSettings.Load(Path.Combine(output, "no-format-preferences.ini")).DisabledExtensions == "", "older preferences allow all formats by default");
            var catalog = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string extension in PreviewFormats.Extensions)
                if (!catalog.Add(extension) || MediaFiles.Kind("sample" + extension) == MediaKind.None)
                    throw new InvalidOperationException("Invalid format catalog entry: " + extension);
            Check(catalog.Contains(".jpg") && catalog.Contains(".jpeg") && catalog.Contains(".psd") && catalog.Contains(".pptx") && catalog.Contains(".mp4"), "format catalog covers images, Office, Adobe and media");
            string jpg = Path.Combine(output, "format-filter.jpg"), jpeg = Path.Combine(output, "format-filter.jpeg");
            WriteImage(jpg, Color.Tomato); WriteImage(jpeg, Color.CornflowerBlue);
            using (var window = new PreviewWindow(true))
            {
                window.Configure(settings);
                Check(!window.ShowFile(jpg, Point.Empty, Rectangle.Empty) && !window.ShowImage(jpg, Point.Empty, Rectangle.Empty), "blocked JPEG cannot start a hover or direct image load");
                window.ShowFile(jpeg, new Point(300, 300), Rectangle.Empty);
                Until(() => window.HasImage, 5000);
                settings.SetExtensionDisabled(".jpeg", true); window.Configure(settings);
                Check(!window.HasImage && window.CurrentPath == null && !window.Visible, "disabling a format immediately closes its existing pinned preview");
            }
            settings.DisabledExtensions = "";
            using (var owner = new SettingsWindow(settings, false, value => { settings = value; value.Save(path); }, null))
            {
                owner.ShowCentered(new Point(300, 300)); Pump(40);
                var button = Field<HoverButton>(owner, "advancedButton");
                Check(button.Text == "Papildu iestatījumi" && button.Visible, "settings expose the localized advanced button");
                button.PerformClick(); Pump(40);
                var advanced = Field<AdvancedSettingsWindow>(owner, "advancedWindow");
                Check(advanced.Visible && advanced.Owner == owner, "advanced formats open in a separate owned window");
                var checks = Field<System.Collections.Generic.Dictionary<string, CheckBox>>(advanced, "checks");
                Check(checks.Count == catalog.Count && checks[".jpg"].Visible && checks[".jpg"] is HoverCheckBox, "every supported extension has a themed checkbox");
                Check(checks[".jpg"].Checked && checks[".jpeg"].Checked, "allowed formats are checked by default");
                Call(checks[".jpg"], "OnClick", EventArgs.Empty);
                Check(!checks[".jpg"].Checked && !settings.Allows(jpg) && settings.Allows(jpeg) && AppSettings.Load(path).IsExtensionDisabled(".jpg"), "unchecking JPG blocks only JPG and saves immediately");
                var search = Field<TextBox>(advanced, "search"); search.Text = "JPG"; Pump(30);
                Check(checks[".jpg"].Visible && !checks[".png"].Visible && !checks[".jpg"].Checked, "format search preserves selections and ignores case");
                var sections = Field<System.Collections.Generic.Dictionary<MediaKind, HoverToggle>>(advanced, "sectionToggles");
                var types = Field<HoverToggle[]>(owner, "typeToggles");
                sections[MediaKind.Image].PerformClick();
                Check(!settings.PreviewImages && !types[0].Checked && !checks[".jpg"].Enabled && checks[".png"].Checked &&
                    !AppSettings.Load(path).PreviewImages, "section switch updates main settings and preserves individual choices even while searching");
                types[0].PerformClick();
                Check(sections[MediaKind.Image].Checked && checks[".jpg"].Enabled && !checks[".jpg"].Checked && settings.Allows("picture.png"),
                    "main settings switch updates the advanced section without resetting exclusions");
                Snapshot(advanced, "advanced-formats-search.png");
                FormatNavigation(advanced, checks);
                Snapshot(advanced, "advanced-formats-lv.png");
                advanced.Close(); button.PerformClick(); Pump(30);
                advanced = Field<AdvancedSettingsWindow>(owner, "advancedWindow");
                checks = Field<System.Collections.Generic.Dictionary<string, CheckBox>>(advanced, "checks");
                Check(!checks[".jpg"].Checked, "reopening advanced settings keeps previously disabled formats unchecked");
                checks[".jpg"].AccessibilityObject.DoDefaultAction();
                Check(checks[".jpg"].Checked && settings.Allows(jpg) &&
                    (checks[".jpg"].AccessibilityObject.State & AccessibleStates.Checked) != 0, "checking restores the format through the accessible checkbox action");
                sections = Field<System.Collections.Generic.Dictionary<MediaKind, HoverToggle>>(advanced, "sectionToggles");
                var categoryKinds = new[] { MediaKind.Image, MediaKind.Document, MediaKind.Text, MediaKind.Audio, MediaKind.Video };
                for (int i = 0; i < categoryKinds.Length; i++)
                {
                    MediaKind kind = categoryKinds[i];
                    sections[kind].PerformClick();
                    Check(!settings.Allows(kind) && !types[i].Checked, "advanced section disables its matching category: " + kind);
                    sections[kind].PerformClick();
                    Check(settings.Allows(kind) && types[i].Checked, "advanced section restores its matching category: " + kind);
                }
                checks[".mp4"].Checked = false;
                types[0].PerformClick();
                Field<HoverButton>(advanced, "allowAll").PerformClick();
                Check(settings.DisabledExtensions == "" && !settings.Allows(jpg) && settings.Allows("video.mp4"), "allow all clears individual exclusions without overriding category switches");
                settings.Theme = "light"; settings.Language = "de"; Brand.SetTheme("light"); owner.UpdateSettings(settings);
                Check(advanced.BackColor == Brand.Background && advanced.Text == "Erweiterte Einstellungen", "advanced window follows live theme and language changes");
                Snapshot(advanced, "advanced-formats-light.png");
                owner.Close(); Check(advanced.IsDisposed, "closing settings also closes the advanced window");
            }
            Brand.SetTheme("dark");
            settings.Theme = "dark"; settings.Language = "lv"; settings.PreviewImages = true;
            using (var owner = new Form { Bounds = new Rectangle(150, 100, 900, 700) })
            using (var advanced = new AdvancedSettingsWindow(settings, delegate { }, delegate { }, delegate { }, 1.5f))
            {
                owner.Show(); advanced.ShowFor(owner); Pump(40);
                var checks = Field<System.Collections.Generic.Dictionary<string, CheckBox>>(advanced, "checks");
                var list = Field<Panel>(advanced, "formats");
                var sections = Field<System.Collections.Generic.Dictionary<MediaKind, HoverToggle>>(advanced, "sectionToggles");
                Check(checks[".jpg"].Height == 54 && sections[MediaKind.Image].Height == 66 &&
                    !checks[".jpg"].Bounds.IntersectsWith(checks[".jpeg"].Bounds) && !list.HorizontalScroll.Visible,
                    "themed format cards and section switches fit at 150 percent DPI");
                Snapshot(advanced, "advanced-formats-150-percent.png");
            }
        }

        private static void FormatNavigation(AdvancedSettingsWindow window, System.Collections.Generic.Dictionary<string, CheckBox> checks)
        {
            var searchField = Field<HoverSearchBox>(window, "searchField");
            var host = Field<HoverScrollPanel>(window, "formatHost");
            var bar = host.ScrollBar;
            Check(!bar.Visible && bar.Maximum == 0, "short search results hide the scrollbar");
            searchField.ClearButton.PerformClick(); Pump(30);
            Check(searchField.Editor.Text == "" && searchField.Editor.Focused && !searchField.ClearButton.Visible &&
                checks[".png"].Visible && !checks[".jpg"].Checked, "clear search restores all formats and input focus without changing choices");
            Check(bar.Visible && bar.Maximum > 0 && !host.Content.HorizontalScroll.Visible, "long format lists use the themed vertical scrollbar");
            bar.AccessibilityObject.GetChild(1).DoDefaultAction();
            Check(bar.Value > 0 && -host.Content.AutoScrollPosition.Y == bar.Value, "accessible down arrow scrolls the format list");
            bar.AccessibilityObject.GetChild(0).DoDefaultAction();
            Check(bar.Value == 0 && host.Content.AutoScrollPosition.Y == 0, "accessible up arrow returns to the top");
            Call(bar, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, bar.Width / 2, bar.Height - 8, 0));
            Call(bar, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, bar.Width / 2, bar.Height - 8, 0));
            Check(bar.Value > 0 && !bar.Capture, "mouse arrow click scrolls and releases capture");
            Call(bar, "OnKeyDown", new KeyEventArgs(Keys.End));
            Check(bar.Value == bar.Maximum && -host.Content.AutoScrollPosition.Y == bar.Maximum, "End reaches the bottom of the list");
            Call(bar, "OnKeyDown", new KeyEventArgs(Keys.Home));
            Call(host.Content, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 20, 20, -120));
            Check(bar.Value > 0 && bar.Value == -host.Content.AutoScrollPosition.Y, "mouse wheel over the list updates the themed thumb");
            Call(bar, "OnKeyDown", new KeyEventArgs(Keys.Home));
            Call(bar, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 10, 60, -60));
            Call(bar, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, 10, 60, -60));
            Check(bar.Value > 0 && bar.Value == -host.Content.AutoScrollPosition.Y, "scrollbar accumulates small wheel deltas");
            Call(bar, "OnKeyDown", new KeyEventArgs(Keys.Home));
            Rectangle thumb = (Rectangle)bar.GetType().GetProperty("Thumb", Private).GetValue(bar, null);
            int middle = thumb.Top + thumb.Height / 2;
            Call(bar, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, bar.Width / 2, middle, 0));
            Call(bar, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, bar.Width / 2, middle + 80, 0));
            Call(bar, "OnMouseUp", new MouseEventArgs(MouseButtons.Left, 1, bar.Width / 2, middle + 80, 0));
            Check(bar.Value > 0 && bar.Value == -host.Content.AutoScrollPosition.Y, "dragging the thumb moves the format list");
            Check(checks[".wmv"].Focus(), "last format accepts keyboard focus"); Pump(30);
            Rectangle last = checks[".wmv"].RectangleToScreen(checks[".wmv"].ClientRectangle);
            Check(host.ViewportBounds.Contains(last) && bar.Value == -host.Content.AutoScrollPosition.Y,
                "focusing a lower format reveals it and synchronizes the scrollbar");
            searchField.Editor.Focus(); searchField.Editor.Text = "jpg"; Pump(30);
            Check(bar.Maximum == 0 && !bar.Visible && host.Content.AutoScrollPosition.Y == 0 && checks[".jpg"].Visible,
                "filtering a scrolled list resets the position and hides the scrollbar");
            searchField.ClearButton.PerformClick(); Pump(30);
            // Capture the actual clipped windows too: DrawToBitmap paints native
            // child scrollbars beyond their parent's clip when processing WM_PRINT.
            using (var image = new Bitmap(window.Width, window.Height))
            using (var graphics = Graphics.FromImage(image))
            {
                graphics.CopyFromScreen(window.Location, Point.Empty, image.Size);
                image.Save(Path.Combine(output, "advanced-formats-screen.png"));
            }
        }

        private static void ThemePreferences()
        {
            string path = Path.Combine(output, "theme-preferences.ini");
            var settings = new AppSettings { Theme = "GLASS", Language = "lv", AnimationsEnabled = false };
            settings.Save(path);
            Check(settings.Theme == "glass" && AppSettings.Load(path).Theme == "glass", "translucent theme survives normalization and restart");
            Check(AppSettings.NormalizeTheme("missing") == "dark", "unknown themes retain the solid dark fallback");
            using (var tray = new TrayMenu(() => settings, () => false, value => settings = value, delegate { }, delegate { }, delegate { }))
            {
                var choices = (ToolStripMenuItem)tray.Items["theme"];
                tray.RefreshState();
                Check(choices.DropDownItems.Count == 3 && ((ToolStripMenuItem)choices.DropDownItems["theme-glass"]).Checked &&
                    !((ToolStripMenuItem)choices.DropDownItems["theme-dark"]).Checked, "tray theme menu marks only the translucent choice");
                choices.DropDownItems["theme-light"].PerformClick();
                Check(settings.Theme == "light" && !((ToolStripMenuItem)choices.DropDownItems["theme-glass"]).Checked,
                    "tray theme choices share the same settings");
            }
            settings.Theme = "dark";
            using (var background = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(60, 40, 1180, 900), ShowInTaskbar = false, TopMost = true })
            using (var window = new SettingsWindow(settings, false, value => { settings = value; value.Save(path); }, null))
            {
                background.Paint += delegate(object sender, PaintEventArgs e)
                {
                    e.Graphics.Clear(Color.FromArgb(190, 35, 65));
                    using (var blue = new SolidBrush(Color.FromArgb(35, 90, 190))) e.Graphics.FillRectangle(blue, 590, 0, 590, 900);
                    for (int x = 0; x < 1180; x += 40) e.Graphics.FillRectangle(Brushes.White, x, 0, 2, 900);
                };
                background.Show(); window.Owner = background; window.TopMost = true; window.ShowCentered(new Point(650, 460));
                window.Location = new Point(100, 80); Pump(100);
                var button = Field<HoverButton>(window, "themeToggle");
                var menu = Field<ContextMenuStrip>(window, "themeMenu");
                button.PerformClick(); Pump(50);
                Check(menu.Visible && menu.Items.Count == 3 && menu.Items[2].Text == "Caurspīdīga", "theme button opens three localized choices");
                CaptureScreen(window, "theme-menu.png");
                menu.Items[1].PerformClick(); menu.Close(); Pump(100);
                Check(settings.Theme == "light" && Brand.IsLight && !WindowBackdrop.IsActive(window), "light theme stays solid");
                menu.Items[2].PerformClick(); Pump(300);
                Check(settings.Theme == "glass" && Brand.IsGlass && AppSettings.Load(path).Theme == "glass" &&
                    ((ToolStripMenuItem)menu.Items[2]).Checked, "selecting translucent applies and saves the third theme");
                Check(WindowBackdrop.IsRequested(window) && window.Opacity == 1 && window.TransparencyKey.IsEmpty,
                    "glass uses a native backdrop without making text translucent or click-through");
                Console.WriteLine("Native backdrop active: " + WindowBackdrop.IsActive(window));
                bool nativeGlass = WindowBackdrop.IsActive(window);
                CaptureScreen(window, "settings-glass.png");
                if (WindowBackdrop.IsActive(window))
                    using (var screen = new Bitmap(Path.Combine(output, "settings-glass.png")))
                    {
                        Color left = screen.GetPixel(12, 360), right = screen.GetPixel(screen.Width - 12, 360);
                        Check(left.R > left.B && right.B > right.R && left.GetBrightness() < .5 && right.GetBrightness() < .5,
                            "native blur reveals tinted background colors without becoming fully transparent");
                    }
                Field<HoverButton>(window, "advancedButton").PerformClick(); Pump(150);
                var advanced = Field<AdvancedSettingsWindow>(window, "advancedWindow");
                Check(WindowBackdrop.IsRequested(advanced) && advanced.Opacity == 1, "advanced settings inherit the translucent theme");
                Field<TextBox>(advanced, "search").Text = "jpg"; Pump(80);
                CaptureScreen(advanced, "advanced-glass-search.png");
                Field<HoverSearchBox>(advanced, "searchField").ClearButton.PerformClick(); Pump(40);
                CaptureScreen(advanced, "advanced-glass.png");
                menu.Items[0].PerformClick(); Pump(80);
                Check(settings.Theme == "dark" && !WindowBackdrop.IsActive(window) && !WindowBackdrop.IsActive(advanced),
                    "returning to dark removes blur from both open settings windows");
                menu.Items[2].PerformClick(); Pump(80);
                advanced.Close(); Pump(40);
                Call(window, "RecreateHandle"); Pump(100);
                Check(WindowBackdrop.IsRequested(window) && WindowBackdrop.IsActive(window) == nativeGlass,
                    "glass is reapplied when Windows recreates the window handle");
                Size before = window.Size;
                window.WindowState = FormWindowState.Maximized; Pump(400);
                window.WindowState = FormWindowState.Normal; Pump(400);
                Check(window.Size == before && WindowBackdrop.IsActive(window) == nativeGlass,
                    "glass survives maximize and restore without growing the window");
                settings.AnimationsEnabled = true; window.UpdateSettings(settings);
                window.Hide(); window.ShowCentered(window.Location); Pump(40);
                Check(window.Opacity == 1, "glass opening animation keeps the native material and text opaque");
                Call(window, "SetUiScale", 1.5f); window.Size = new Size(1420, 1020);
                window.SettingsViewport.AutoScrollPosition = Point.Empty; Call(window, "ArrangeSettings"); Pump(100);
                CaptureScreen(window, "settings-glass-150-percent.png");
                settings.AnimationsEnabled = false; window.UpdateSettings(settings); window.Close();
            }
            settings.Theme = "glass";
            string imagePath = Path.Combine(output, "glass-image.png"); WriteImage(imagePath, Color.CornflowerBlue);
            using (var mediaBackground = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
                Bounds = Screen.PrimaryScreen.WorkingArea, BackColor = Color.Black, TopMost = true, ShowInTaskbar = false })
            using (var preview = new PreviewWindow(true))
            {
                // Keep screenshot checks independent of other apps and video behind the preview.
                mediaBackground.Show(); preview.Owner = mediaBackground; preview.TopMost = true;
                preview.Configure(settings); preview.ShowFile(imagePath, new Point(300, 300), Rectangle.Empty);
                Until(() => preview.HasImage, 5000); Pump(200);
                Check(WindowBackdrop.IsRequested(preview) && preview.Opacity == 1, "pinned previews use glass without a layered video window");
                CaptureScreen(preview, "preview-glass.png");
                using (var screen = new Bitmap(Path.Combine(output, "preview-glass.png")))
                    Check(screen.GetPixel(screen.Width / 2, screen.Height / 2).ToArgb() == Color.CornflowerBlue.ToArgb(),
                        "glass leaves preview image colors fully opaque");
                settings.Theme = "light"; preview.Configure(settings);
                Check(!WindowBackdrop.IsActive(preview) && preview.HasImage, "switching away from glass preserves the loaded preview");
                string videoPath = Path.Combine(output, "glass-video.avi"); WriteVideo(videoPath);
                settings.Theme = "glass"; settings.VolumePercent = 0; preview.Configure(settings);
                preview.ShowFile(videoPath, new Point(300, 300), Rectangle.Empty);
                Until(() => preview.MediaState != "media_loading", 16000); Pump(300);
                var media = Field<MediaPlayback>(preview, "media");
                Check(media != null && media.IsOpen && media.HasVideo && preview.MediaState == "media_playing" && preview.Opacity == 1,
                    "native video playback remains active inside the translucent preview");
                Call(preview, "TogglePlayback"); Pump(100);
                CaptureScreen(preview, "video-glass.png");
                using (var screen = new Bitmap(Path.Combine(output, "video-glass.png")))
                {
                    Color pixel = screen.GetPixel(screen.Width / 2, screen.Height / 2);
                    Check(Math.Max(pixel.R, pixel.B) > 220 && Math.Abs(pixel.G - 50) < 20,
                        "video frames remain visible above the glass backdrop");
                }
            }
            Brand.SetTheme("dark");
        }
        private static void CaptureScreen(Form window, string name)
        {
            using (var image = new Bitmap(window.Width, window.Height))
            using (var graphics = Graphics.FromImage(image))
            { graphics.CopyFromScreen(window.Location, Point.Empty, image.Size); image.Save(Path.Combine(output, name)); }
        }

        private static void GlassCorners()
        {
            var settings = new AppSettings { Theme = "glass", AnimationsEnabled = false };
            Brand.SetTheme("glass");
            using (var background = new Form { FormBorderStyle = FormBorderStyle.None, StartPosition = FormStartPosition.Manual,
                Bounds = new Rectangle(60, 40, 1200, 900), BackColor = Color.Lime, TopMost = true, ShowInTaskbar = false })
            {
                background.Show();
                using (var window = new SettingsWindow(settings, false, delegate { }, null))
                {
                    window.Owner = background; window.TopMost = true;
                    window.ShowCentered(new Point(600, 450)); window.Bounds = new Rectangle(100, 80, 1000, 780);
                    Pump(200); CheckGlassCorners(window, "settings-glass-corners.png");
                    window.WindowState = FormWindowState.Maximized; Pump(400);
                    Check(window.Region == null, "maximized glass settings keep the full rectangular work area");
                    window.WindowState = FormWindowState.Normal; Pump(400);
                    CheckGlassCorners(window, "settings-glass-restored-corners.png");
                    settings.Theme = "dark"; window.UpdateSettings(settings); Pump(50);
                    settings.Theme = "glass"; window.UpdateSettings(settings); Pump(150);
                    CheckGlassCorners(window, "settings-glass-switched-corners.png");
                    Call(window, "RecreateHandle"); Pump(150);
                    CheckGlassCorners(window, "settings-glass-recreated-corners.png");
                }
                using (var advanced = new AdvancedSettingsWindow(settings, delegate { }, delegate { }, delegate { }, 1))
                { advanced.TopMost = true; advanced.ShowFor(background); Pump(150); CheckGlassCorners(advanced, "advanced-glass-corners.png"); }
                string path = Path.Combine(output, "corners-image.png"); WriteImage(path, Color.CornflowerBlue);
                foreach (bool pinned in new[] { false, true })
                    using (var preview = new PreviewWindow(pinned))
                    {
                        preview.Owner = background; preview.TopMost = true; preview.Configure(settings);
                        preview.ShowFile(path, new Point(300, 250), Rectangle.Empty); Until(() => preview.HasImage, 5000);
                        preview.Location = new Point(150, 120); Pump(180);
                        CheckGlassCorners(preview, pinned ? "pinned-glass-corners.png" : "hover-glass-corners.png");
                    }
            }
            Brand.SetTheme("dark");
        }
        private static void CheckGlassCorners(Form window, string name)
        {
            CaptureScreen(window, name);
            using (var screenshot = new Bitmap(Path.Combine(output, name)))
            {
                Point[] points = { Point.Empty, new Point(screenshot.Width - 1, 0),
                    new Point(0, screenshot.Height - 1), new Point(screenshot.Width - 1, screenshot.Height - 1) };
                bool rounded = true;
                foreach (Point point in points)
                {
                    Color pixel = screenshot.GetPixel(point.X, point.Y);
                    rounded &= pixel.G > 160 && pixel.R < 30 && pixel.B < 30;
                }
                Check(rounded, "glass leaves the background visible outside all four rounded corners in " + name);
                Color inside = screenshot.GetPixel(screenshot.Width / 2, 5);
                Check(inside.G < 140 || inside.R > 30 || inside.B > 30, "rounded glass retains visible content inside its outline in " + name);
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
                window.Configure(new AppSettings { Language = "lv", Theme = "dark", AnimationsEnabled = false });
                string image = Path.Combine(output, "image.png");
                window.ShowFile(image, new Point(300, 300), Rectangle.Empty);
                Rectangle loadingBounds = window.Bounds;
                Until(() => Field<Bitmap>(window, "preview") != null, 5000);
                Check(window.IsInteractive && window.ShowInTaskbar, "pinned preview is an interactive taskbar window");
                Check(window.Bounds == loadingBounds, "image completion preserves pinned window bounds");
                Check(window.FormBorderStyle == FormBorderStyle.None && window.Region != null, "pinned window has custom rounded chrome");
                Check(!Field<ToolStripButton>(window, "previousButton").Available && !Field<ToolStripButton>(window, "playbackButton").Available,
                    "static image hides irrelevant page and playback controls");
                Field<ToolStripButton>(window, "zoomInButton").PerformClick();
                Check(Field<float>(window, "zoom") > 1, "pinned preview supports zoom");
                Rectangle view = Field<Rectangle>(window, "viewport");
                Point anchor = new Point(view.Left + view.Width / 2 + 30, view.Top + view.Height / 2 + 20);
                Rectangle beforeZoom = (Rectangle)Call(window, "ZoomedImageArea");
                Call(window, "ZoomAt", 1.25f, anchor);
                Rectangle afterZoom = (Rectangle)Call(window, "ZoomedImageArea");
                double relativeX = (anchor.X - beforeZoom.X) / (double)beforeZoom.Width;
                double relativeY = (anchor.Y - beforeZoom.Y) / (double)beforeZoom.Height;
                Check(Math.Abs(afterZoom.X + relativeX * afterZoom.Width - anchor.X) < 2 &&
                    Math.Abs(afterZoom.Y + relativeY * afterZoom.Height - anchor.Y) < 2, "zoom keeps the image point beneath the cursor");
                Bitmap loaded = Field<Bitmap>(window, "preview");
                float savedZoom = Field<float>(window, "zoom");
                window.ShowFile(image, new Point(50, 50), Rectangle.Empty);
                Check(ReferenceEquals(loaded, Field<Bitmap>(window, "preview")) && Field<float>(window, "zoom") == savedZoom,
                    "pinning the same file again preserves image and zoom");
                window.Configure(new AppSettings { Language = "lv", Theme = "dark", AnimationsEnabled = false, MaxWidth = 920, MaxHeight = 690 });
                Check(ReferenceEquals(loaded, Field<Bitmap>(window, "preview")) && Field<float>(window, "zoom") == savedZoom,
                    "hover size settings do not reload the pinned file");
                int renderedWidth = afterZoom.Width;
                window.ClientSize = new Size(760, 560); window.Location = new Point(120, 80); Pump(30);
                Check(Math.Abs(((Rectangle)Call(window, "ZoomedImageArea")).Width - renderedWidth) <= 1,
                    "resizing retains manual image scale");
                Field<ToolStripButton>(window, "fitButton").PerformClick();
                Check(Field<float>(window, "zoom") == 1, "fit restores image scale");
                Check(Field<ToolStripLabel>(window, "zoomLabel").Text == Math.Round(window.DisplayedImageSize.Width * 100.0 / 640) + "%",
                    "zoom percentage uses original image pixels");
                Field<ToolStripButton>(window, "zoomOutButton").PerformClick();
                Check(Field<float>(window, "zoom") < 1, "zoom out can go below fit size");
                Call(window, "OnMouseDoubleClick", new MouseEventArgs(MouseButtons.Left, 2, anchor.X, anchor.Y, 0));
                Check(Field<bool>(window, "fitMode") && Field<Point>(window, "pan") == Point.Empty, "double click fits and centers the image");
                Snapshot(window, "pinned-image.png");
                Rectangle retainedBounds = window.Bounds;
                string portrait = Path.Combine(output, "portrait.png");
                using (var bitmap = new Bitmap(350, 900))
                using (var g = Graphics.FromImage(bitmap))
                {
                    g.Clear(Color.Coral);
                    using (var pen = new Pen(Color.White, 4))
                        for (int y = 50; y < 900; y += 100) g.DrawRectangle(pen, 30, y, 290, 60);
                    bitmap.Save(portrait);
                }
                window.ShowFile(portrait, new Point(800, 600), Rectangle.Empty);
                Until(() => window.HasImage, 5000);
                Check(window.Bounds == retainedBounds, "switching aspect ratios retains window size and position");
                Call(window, "ChangeZoom", 2f);
                view = Field<Rectangle>(window, "viewport");
                Rectangle fitted = Field<Rectangle>(window, "imageArea");
                using (var capture = new Bitmap(window.Width, window.Height))
                {
                    window.DrawToBitmap(capture, new Rectangle(Point.Empty, capture.Size));
                    Color pixel = capture.GetPixel(fitted.Left - 20, view.Top + view.Height / 2);
                    Check(pixel.R > 240 && pixel.G > 70, "zoomed portrait paints beyond its original fitted rectangle");
                }
                Snapshot(window, "pinned-portrait-zoom.png");
                Call(window, "ChangeZoom", 4f);
                Point center = new Point(view.Left + view.Width / 2, view.Top + view.Height / 2);
                Call(window, "OnMouseDown", new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
                Call(window, "OnMouseMove", new MouseEventArgs(MouseButtons.Left, 0, center.X + 5000, center.Y + 5000, 0));
                Rectangle dragged = (Rectangle)Call(window, "ZoomedImageArea");
                Check(dragged.Left <= view.Left + 1 && dragged.Top <= view.Top + 1 && dragged.Right >= view.Right - 1 && dragged.Bottom >= view.Bottom - 1,
                    "dragging clamps against the viewport without exposing empty edges");
                window.Capture = false;
                Check(!Field<bool>(window, "panning") && window.Cursor == Cursors.Default, "lost mouse capture ends image dragging");
                Field<WindowCaptionButton>(window, "pinnedMaximize").PerformClick(); Pump(60);
                Check(window.WindowState == FormWindowState.Maximized && window.Region == null &&
                    Screen.FromControl(window).WorkingArea.Contains(window.RectangleToScreen(window.ClientRectangle)), "maximize fills the work area and leaves the taskbar available");
                Field<WindowCaptionButton>(window, "pinnedMinimize").PerformClick(); Pump(30);
                window.ActivatePinned(); Pump(30);
                Check(window.WindowState == FormWindowState.Maximized, "reopening a minimized preview restores its maximized state");
                Field<WindowCaptionButton>(window, "pinnedMaximize").PerformClick(); Pump(30);
                Check(window.Bounds == retainedBounds && window.Region != null, "restore returns to the user chosen rounded window bounds: " + window.Bounds + " expected " + retainedBounds);
                string pdf = Path.Combine(output, "two-pages.pdf"); WritePdf(pdf);
                window.ShowFile(pdf, new Point(300, 300), Rectangle.Empty);
                Until(() => !Field<bool>(window, "documentLoading"), 12000);
                Check(Field<int>(window, "pdfPageCount") == 2, "PDF reports page count");
                Check(Field<Bitmap>(window, "preview").GetPixel(100, 100).R > 200, "PDF first page is red");
                Field<ToolStripButton>(window, "nextButton").PerformClick();
                Until(() => !Field<bool>(window, "documentLoading"), 12000);
                Check(Field<int>(window, "pdfPageIndex") == 1, "PDF navigation selects second page");
                Check(Field<Bitmap>(window, "preview").GetPixel(100, 100).B > 200, "PDF second page is blue");
                Check(window.Bounds == retainedBounds, "PDF loading and page changes preserve window bounds");
                Snapshot(window, "pinned-pdf.png");
                Field<ToolStripButton>(window, "previousButton").PerformClick();
                window.ClearPreview(); Pump(150);
                Check(!window.Visible && window.CurrentPath == null, "cancelled PDF page cannot reopen window");
                string text = Path.Combine(output, "scroll.txt");
                var lines = new StringBuilder();
                for (int i = 1; i <= 200; i++) lines.AppendLine("Line " + i);
                File.WriteAllText(text, lines.ToString());
                window.ShowFile(text, new Point(800, 600), Rectangle.Empty);
                Check(window.Bounds == retainedBounds && !Field<ToolStripButton>(window, "zoomInButton").Available,
                    "text uses the same window bounds and hides image zoom controls");
                Call(window, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, center.X, center.Y, -60));
                Check(Field<int>(window, "textOffset") == 0, "partial wheel input does not jump a full text step");
                Call(window, "OnMouseWheel", new MouseEventArgs(MouseButtons.None, 0, center.X, center.Y, -60));
                Check(Field<int>(window, "textOffset") > 0, "accumulated wheel input scrolls text");
                Snapshot(window, "pinned-text.png");
                Field<WindowCaptionButton>(window, "pinnedMinimize").PerformClick(); Pump(30);
                window.ActivatePinned(); Pump(30);
                Check(window.Bounds == retainedBounds, "minimize and restore preserves normal window size");
            }
        }
        private static void OfficeThemes()
        {
            foreach (bool sheet in new[] { false, true })
            {
                string path = Path.Combine(output, sheet ? "Theme workbook.xlsx" : "Theme document.docx");
                WriteOfficeFixture(path, sheet);
                byte[] original = File.ReadAllBytes(path);
                using (var window = new PreviewWindow(true))
                {
                    var settings = new AppSettings { Theme = "dark", AnimationsEnabled = false };
                    window.Configure(settings); window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                    Until(() => !Field<bool>(window, "documentLoading"), 5000);
                    Bitmap image = Field<Bitmap>(window, "preview");
                    Check(window.MediaState == (sheet ? "document_sheet" : "document_word"), "Office content renders instead of the saved white thumbnail");
                    Check(image.GetPixel(image.Width - 10, image.Height - 10).GetBrightness() < .15f, "Office page follows the dark palette");
                    Check(CountColor(image, Brand.Text) > 100, "dark Office preview contains readable light text");
                    if (!sheet) Check(CountColor(image, Color.FromArgb(255, 96, 0)) > 500, "Word picture retains its original color in dark mode");
                    Snapshot(window, sheet ? "excel-pinned-dark.png" : "word-pinned-dark.png");
                    Call(window, "ChangeZoom", 1.25f);
                    float zoom = Field<float>(window, "zoom"); Rectangle bounds = window.Bounds;
                    settings.Theme = "light"; window.Configure(settings);
                    Until(() => !Field<bool>(window, "documentLoading"), 5000);
                    image = Field<Bitmap>(window, "preview");
                    Check(image.GetPixel(image.Width - 10, image.Height - 10).GetBrightness() > .95f, "open Office preview updates when switching to light theme");
                    Check(window.Bounds == bounds && Field<float>(window, "zoom") == zoom, "Office theme switch retains window bounds and zoom");
                    if (!sheet) Check(CountColor(image, Color.FromArgb(255, 96, 0)) > 500, "Word picture retains its original color in light mode");
                    Call(window, "FitImage"); Snapshot(window, sheet ? "excel-pinned-light.png" : "word-pinned-light.png");
                    settings.Theme = "dark"; window.Configure(settings);
                    settings.Theme = "light"; window.Configure(settings);
                    settings.Theme = "dark"; window.Configure(settings);
                    Until(() => !Field<bool>(window, "documentLoading"), 5000); Pump(150);
                    image = Field<Bitmap>(window, "preview");
                    Check(image.GetPixel(image.Width - 10, image.Height - 10).GetBrightness() < .15f, "rapid theme changes cannot apply a stale Office render");
                    settings.Theme = "light"; window.Configure(settings); window.ClearPreview(); Pump(150);
                    Check(!window.Visible && window.CurrentPath == null, "closing during Office theme rendering cancels the preview");
                }
                using (var hover = new PreviewWindow())
                {
                    hover.Configure(new AppSettings { Theme = "dark", AnimationsEnabled = false });
                    hover.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                    Until(() => !Field<bool>(hover, "documentLoading"), 5000);
                    Bitmap image = Field<Bitmap>(hover, "preview");
                    Check(image.GetPixel(image.Width - 10, image.Height - 10).GetBrightness() < .15f, "hover Office preview also uses the dark palette");
                    Snapshot(hover, sheet ? "excel-hover-dark.png" : "word-hover-dark.png");
                }
                Check(Convert.ToBase64String(original) == Convert.ToBase64String(File.ReadAllBytes(path)), "Office theme rendering leaves the source file unchanged");
            }
        }
        private static int CountColor(Bitmap image, Color target)
        {
            int count = 0;
            for (int y = 0; y < image.Height; y += 2)
                for (int x = 0; x < image.Width; x += 2)
                    if (image.GetPixel(x, y).ToArgb() == target.ToArgb()) count++;
            return count;
        }
        private static void RefreshAndDetail()
        {
            string path = Path.Combine(output, "large-refresh.bmp");
            WriteLargeImage(path, Color.Tomato);
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { AnimationsEnabled = false });
                window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                Until(() => window.HasImage && !window.IsImageLoading, 5000);
                Check(Field<Bitmap>(window, "preview").Width <= 1600, "initial image stays bounded");
                Call(window, "ChangeZoom", 8f);
                Until(() => Field<Bitmap>(window, "preview").Width == 3000, 10000);
                float zoom = Field<float>(window, "zoom");
                Check(zoom == 8f, "detail replacement preserves zoom");
                WriteLargeImage(path, Color.CornflowerBlue);
                File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(2));
                Until(() => window.HasImage && Field<Bitmap>(window, "preview").GetPixel(20, 20).B > 200, 5000);
                Check(window.Visible && window.CurrentPath == path, "pinned content refreshes automatically after file changes");
                DateTime stamp = File.GetLastWriteTimeUtc(path);
                WriteLargeImage(path, Color.Tomato); File.SetLastWriteTimeUtc(path, stamp);
                Field<ToolStripButton>(window, "refreshButton").PerformClick();
                Until(() => window.HasImage && !window.IsImageLoading, 5000);
                Check(Field<Bitmap>(window, "preview").GetPixel(20, 20).R > 240, "manual refresh bypasses cache even with unchanged timestamp and size");
                WriteLargeImage(path, Color.CornflowerBlue); File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(4));
                window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                Until(() => window.HasImage && !window.IsImageLoading, 5000);
                Check(Field<Bitmap>(window, "preview").GetPixel(20, 20).B > 200, "repinning a changed file reloads its content");
            }
            string pdf = Path.Combine(output, "detail.pdf"); WritePdf(pdf);
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { AnimationsEnabled = false });
                window.ShowFile(pdf, new Point(300, 300), Rectangle.Empty);
                Until(() => !Field<bool>(window, "documentLoading"), 12000);
                Call(window, "ChangeZoom", 8f);
                Until(() => Field<Bitmap>(window, "preview").Width == 4096, 12000);
                Check(Field<Bitmap>(window, "preview").GetPixel(50, 50).R > 200, "PDF zoom renders additional source detail");
                Call(window, "Navigate", 1);
                Until(() => !Field<bool>(window, "documentLoading") && Field<Bitmap>(window, "preview").Width == 4096, 12000);
                Check(Field<Bitmap>(window, "preview").GetPixel(50, 50).B > 200, "high resolution PDF page change cannot show the old page");
            }
            string project = Path.Combine(output, "detail.aep"), sidecar = project + ".preview.bmp";
            File.WriteAllText(project, "Preview-sidecar test project"); WriteLargeImage(sidecar, Color.Tomato);
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { AnimationsEnabled = false });
                window.ShowFile(project, new Point(300, 300), Rectangle.Empty);
                Until(() => !Field<bool>(window, "documentLoading"), 5000);
                Check(window.HasImage, "Adobe image sidecar loads");
                Call(window, "ChangeZoom", 8f);
                Until(() => Field<Bitmap>(window, "preview").Width == 3000, 10000);
                Check(Field<float>(window, "zoom") == 8f, "Adobe sidecar zoom loads source pixels without changing scale");
                WriteLargeImage(sidecar, Color.CornflowerBlue); File.SetLastWriteTimeUtc(sidecar, DateTime.UtcNow.AddSeconds(2));
                Until(() => window.HasImage && Field<Bitmap>(window, "preview").GetPixel(20, 20).B > 200, 7000);
                Check(window.CurrentPath == project, "Adobe sidecar changes refresh the pinned project");
            }
        }
        private static void WriteLargeImage(string path, Color color)
        {
            using (var bitmap = new Bitmap(3000, 3000))
            {
                using (Graphics g = Graphics.FromImage(bitmap)) g.Clear(color);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Bmp);
            }
        }

        private static void OfficeNavigation()
        {
            string sheetPath = Path.Combine(output, "many-sheets.xlsx"), wordPath = Path.Combine(output, "long-word.docx");
            WriteOfficeFixture(sheetPath, true); WriteOfficeFixture(wordPath, false);
            const string office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            using (var file = File.Open(sheetPath, FileMode.Open, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Update))
            {
                zip.GetEntry("xl/workbook.xml").Delete(); zip.GetEntry("xl/_rels/workbook.xml.rels").Delete();
                OfficeEntry(zip, "xl/workbook.xml", "<workbook xmlns:r='" + office + "'><sheets><sheet name='First' r:id='sheet'/><sheet name='Second' r:id='second'/><sheet name='Hidden' state='hidden' r:id='second'/></sheets></workbook>");
                OfficeEntry(zip, "xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id='sheet' Type='" + office + "/worksheet' Target='worksheets/sheet1.xml'/><Relationship Id='second' Type='" + office + "/worksheet' Target='worksheets/sheet2.xml'/></Relationships>");
                var rows = new StringBuilder("<worksheet><cols><col min='1' max='1' width='30'/><col min='2' max='2' width='10'/></cols><sheetData>");
                for (int i = 1; i <= 120; i++) rows.Append("<row r='" + i + "'><c r='A" + i + "' t='inlineStr'><is><t>Second sheet row " + i + "</t></is></c><c r='B" + i + "'><v>100</v></c></row>");
                rows.Append("</sheetData></worksheet>"); OfficeEntry(zip, "xl/worksheets/sheet2.xml", rows.ToString());
            }
            using (var file = File.Open(wordPath, FileMode.Open, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Update))
            {
                zip.GetEntry("word/document.xml").Delete();
                var paragraphs = new StringBuilder("<document><body>");
                for (int i = 0; i < 140; i++) paragraphs.Append("<p><r><t>Paragraph " + i + " of the long document. This content must remain reachable by scrolling.</t></r></p>");
                paragraphs.Append("</body></document>"); OfficeEntry(zip, "word/document.xml", paragraphs.ToString());
            }
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { AnimationsEnabled = false, Language = "lv" });
                window.ShowFile(sheetPath, new Point(300, 300), Rectangle.Empty);
                Until(() => !Field<bool>(window, "documentLoading"), 5000);
                var picker = Field<ToolStripComboBox>(window, "sheetPicker");
                Check(picker.Visible && picker.Items.Count == 2, "worksheet picker includes visible sheets");
                picker.SelectedIndex = 1; Pump(160);
                Until(() => !Field<bool>(window, "documentLoading"), 5000);
                Check(picker.SelectedIndex == 1 && Field<OfficeViewOptions>(window, "officeView").SheetIndex == 1, "worksheet selection survives asynchronous render");
                var scroll = Field<VScrollBar>(window, "officeScroll");
                Check(scroll.Visible && scroll.Maximum > scroll.LargeChange * 3, "long worksheet exposes scrolling");
                // First column uses 30 / (30 + 10 + 8.43) of the available width.
                Bitmap rendered = Field<Bitmap>(window, "preview");
                int boundary = (int)Math.Round(80 + (1600 - 80) * 30 / 48.43);
                bool found = false;
                for (int x = boundary - 2; x <= boundary + 2; x++)
                    found |= rendered.GetPixel(x, 170).ToArgb() != rendered.GetPixel(boundary - 15, 170).ToArgb();
                Check(found, "worksheet column widths follow the saved proportions");
                scroll.Value = 2000; Pump(160); Until(() => !Field<bool>(window, "documentLoading"), 5000);
                Check(Field<OfficeViewOptions>(window, "officeView").ScrollY == 2000, "worksheet scroll position is retained");
                Snapshot(window, "worksheet-navigation.png");
                window.ShowFile(wordPath, new Point(300, 300), Rectangle.Empty);
                Until(() => !Field<bool>(window, "documentLoading"), 5000);
                Check(scroll.Visible && scroll.Maximum > 5000, "long Word document reports content beyond the first viewport");
                scroll.Value = scroll.Maximum - scroll.LargeChange + 1; Pump(160);
                Until(() => !Field<bool>(window, "documentLoading"), 5000);
                Check(Field<OfficeViewOptions>(window, "officeView").ScrollY > 5000 && window.HasImage, "Word document can render its final section");
                Snapshot(window, "word-scrolled.png");
            }
        }

        private static void WriteOfficeFixture(string path, bool sheet)
        {
            const string relationships = "http://schemas.openxmlformats.org/package/2006/relationships";
            const string office = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            string main = sheet ? "xl/workbook.xml" : "word/document.xml";
            using (var file = File.Create(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                OfficeEntry(zip, "_rels/.rels", "<Relationships xmlns='" + relationships + "'><Relationship Id='doc' Type='" + office + "/officeDocument' Target='" + main + "'/><Relationship Id='thumb' Type='" + office + "/thumbnail' Target='docProps/thumbnail.png'/></Relationships>");
                using (var bitmap = new Bitmap(120, 60))
                {
                    using (Graphics g = Graphics.FromImage(bitmap)) { g.Clear(Color.White); g.FillRectangle(Brushes.Black, 5, 5, 30, 12); }
                    using (Stream stream = zip.CreateEntry("docProps/thumbnail.png").Open()) bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    using (Graphics g = Graphics.FromImage(bitmap)) g.Clear(Color.FromArgb(255, 96, 0));
                    using (Stream stream = zip.CreateEntry("word/media/picture.png").Open()) bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                }
                if (sheet)
                {
                    OfficeEntry(zip, main, "<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='" + office + "'><sheets><sheet name='Launch budget' sheetId='1' r:id='sheet'/></sheets></workbook>");
                    OfficeEntry(zip, "xl/_rels/workbook.xml.rels", "<Relationships xmlns='" + relationships + "'><Relationship Id='sheet' Type='" + office + "/worksheet' Target='worksheets/sheet1.xml'/></Relationships>");
                    OfficeEntry(zip, "xl/worksheets/sheet1.xml", "<worksheet xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>Workstream</t></is></c><c r='B1' t='inlineStr'><is><t>Budget</t></is></c></row><row r='2'><c r='A2' t='inlineStr'><is><t>Interface design</t></is></c><c r='B2'><v>640</v></c></row></sheetData></worksheet>");
                }
                else
                {
                    OfficeEntry(zip, main, "<w:document xmlns:w='http://schemas.openxmlformats.org/wordprocessingml/2006/main' xmlns:r='" + office + "' xmlns:a='http://schemas.openxmlformats.org/drawingml/2006/main'><w:body><w:p><w:pPr><w:pStyle w:val='Title'/></w:pPr><w:r><w:t>Desktop Hover / Project brief</w:t></w:r></w:p><w:p><w:r><w:t>Readable documents in dark and light themes. Original pictures keep their colors.</w:t></w:r></w:p><w:tbl><w:tr><w:tc><w:p><w:r><w:t>Interface design</w:t></w:r></w:p></w:tc><w:tc><w:p><w:r><w:t>Ready</w:t></w:r></w:p></w:tc></w:tr></w:tbl><w:p><w:r><w:drawing><a:blip r:embed='photo'/></w:drawing></w:r></w:p></w:body></w:document>");
                    OfficeEntry(zip, "word/_rels/document.xml.rels", "<Relationships xmlns='" + relationships + "'><Relationship Id='photo' Type='" + office + "/image' Target='media/picture.png'/></Relationships>");
                }
            }
        }
        private static void OfficeEntry(ZipArchive zip, string name, string xml)
        { using (var writer = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) writer.Write(xml); }

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
                var seek = Field<HoverSlider>(window, "mediaSeek");
                Check(seek.Visible && seek.Enabled, "pinned audio exposes a seekable timeline");
                seek.Value = 700; Pump(180);
                Check(window.PlaybackPosition.TotalSeconds > 1.8 && window.PlaybackPosition.TotalSeconds < 2.4 && Field<bool>(window, "playbackPaused"), "seeking preserves pause and changes position");
                Field<HoverSlider>(window, "mediaVolume").Value = 35;
                var player = Field<System.Windows.Media.MediaPlayer>(Field<MediaPlayback>(window, "media"), "player");
                Check(Math.Abs(player.Volume - .35) < .001 && !player.IsMuted, "pinned volume reaches the decoder");
                Field<CheckBox>(window, "mediaMute").Checked = true;
                Check(player.IsMuted, "mute silences the decoder");
                Field<CheckBox>(window, "mediaMute").Checked = false;
                Check(!player.IsMuted && Math.Abs(player.Volume - .35) < .001, "unmute restores the selected volume");
                Snapshot(window, "audio-timeline.png");
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
                window.ClientSize = new Size(760, 560);
                Check(ReferenceEquals(session, Field<MediaPlayback>(window, "media")) &&
                    Field<Rectangle>(window, "viewport").Contains(Field<Rectangle>(session, "videoBounds")),
                    "resizing fits video inside the viewer without replacing the playback session");
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
        private static object Call(object target, string name, params object[] args) { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
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
