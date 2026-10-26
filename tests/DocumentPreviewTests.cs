using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Exercise the shipped assembly, including asynchronous document loading and layout.
internal static class DocumentPreviewTests
{
    private const BindingFlags HiddenInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int passed, failed;

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 2 && args.Length != 4) return 2;
        try { SetProcessDpiAwarenessContext(new IntPtr(-4)); }
        catch (EntryPointNotFoundException) { }
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Assembly app = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        string directory = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(directory);
        InfoTipSuppressorTests.Run(app, Test);
        PhotoshopPreviewTests.Run(app, directory, Test);
        PowerPointPreviewTests.Run(app, directory, Test);
        Test("Photoshop window uses composite resolution with enlargement off", delegate
        {
            CheckWindow(app, PhotoshopPreviewTests.CreateFixture(directory), directory,
                "photoshop-window.png", "document_composite", new Size(540, 540));
        });
        Test("PowerPoint window preserves a styled preview with enlargement off", delegate
        {
            CheckWindow(app, PowerPointPreviewTests.CreateWindowFixture(directory), directory,
                "powerpoint-window.png", "document_embedded", new Size(720, 405));
        });
        if (args.Length == 4)
        {
            NativePowerPointPreviewTests.Run(app, args[2], args[3], directory, Test);
            Test("Native PowerPoint window preserves styles at the configured size", delegate
            {
                CheckWindow(app, args[2], directory, "powerpoint-native-window.png", "document_powerpoint", new Size(720, 405));
            });
        }
        Console.WriteLine("{0} passed; {1} failed.", passed, failed);
        return failed == 0 ? 0 : 1;
    }

    private static void Test(string name, Action action)
    {
        try { action(); passed++; Console.WriteLine("PASS: " + name); }
        catch (Exception error)
        {
            failed++;
            while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
            Console.WriteLine("FAIL: " + name + " — " + error.Message);
        }
    }

    private static void CheckWindow(Assembly app, string path, string directory, string screenshot,
        string expectedState, Size expectedSize)
    {
        Type windowType = app.GetType("DesktopHover.PreviewWindow", true);
        Type settingsType = app.GetType("DesktopHover.AppSettings", true);
        object settings = Activator.CreateInstance(settingsType, true);
        settingsType.GetField("MaxWidth").SetValue(settings, 720);
        settingsType.GetField("MaxHeight").SetValue(settings, 540);
        settingsType.GetField("Upscale").SetValue(settings, false);
        using (Form window = (Form)Activator.CreateInstance(windowType))
        {
            windowType.GetMethod("Configure", HiddenInstance).Invoke(window, new[] { settings });
            Rectangle workArea = Screen.PrimaryScreen.WorkingArea;
            Point cursor = new Point(workArea.Left + workArea.Width / 2, workArea.Top + workArea.Height / 2);
            bool shown = (bool)windowType.GetMethod("ShowFile", HiddenInstance).Invoke(window,
                new object[] { path, cursor, new Rectangle(cursor, new Size(48, 48)) });
            if (!shown) throw new InvalidOperationException("Preview did not open.");
            window.Hide();
            PropertyInfo state = windowType.GetProperty("MediaState", HiddenInstance);
            Stopwatch elapsed = Stopwatch.StartNew();
            while ((string)state.GetValue(window, null) == "document_loading" && elapsed.ElapsedMilliseconds < 10000)
            {
                Application.DoEvents();
                Thread.Sleep(10);
            }
            window.Hide();
            if ((string)state.GetValue(window, null) != expectedState)
                throw new InvalidOperationException("Unexpected preview source: " + state.GetValue(window, null));
            Size actual = (Size)windowType.GetProperty("DisplayedImageSize", HiddenInstance).GetValue(window, null);
            if (actual.Width <= 160 || actual.Height <= 160)
                throw new InvalidOperationException("Window is still thumbnail-sized: " + actual);
            float scale = (float)windowType.GetField("scale", HiddenInstance).GetValue(window);
            int availableWidth = Math.Min(720, workArea.Width - 2 * Px(3, scale) - Px(32, scale));
            int availableHeight = Math.Min(540, workArea.Height - Px(3, scale) - Px(12, scale) - Px(48, scale) - Px(32, scale));
            double fit = Math.Min(1, Math.Min((double)availableWidth / expectedSize.Width,
                (double)availableHeight / expectedSize.Height));
            Size fitted = new Size(Math.Max(1, (int)Math.Round(expectedSize.Width * fit)),
                Math.Max(1, (int)Math.Round(expectedSize.Height * fit)));
            if (actual != fitted) throw new InvalidOperationException("Expected " + fitted + ", got " + actual);
            if (!workArea.Contains(window.Bounds)) throw new InvalidOperationException("Window exceeds monitor work area.");
            using (var bitmap = new Bitmap(window.Width, window.Height))
            {
                window.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                bitmap.Save(Path.Combine(directory, screenshot), ImageFormat.Png);
            }
            windowType.GetMethod("ClearPreview").Invoke(window, null);
            using (new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
        }
    }

    private static int Px(int value, float scale) { return (int)Math.Round(value * scale); }

    [DllImport("user32.dll")]
    private static extern bool SetProcessDpiAwarenessContext(IntPtr context);
}
