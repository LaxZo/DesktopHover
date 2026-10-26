using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;

internal static class NativePowerPointPreviewTests
{
    private const BindingFlags HiddenStatic = BindingFlags.NonPublic | BindingFlags.Static;
    private const BindingFlags HiddenInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    internal static void Run(Assembly app, string fixture, string reference, string directory, Action<string, Action> test)
    {
        if (Running()) throw new InvalidOperationException("Close PowerPoint before running the optional native tests.");
        byte[] sourceHash = Hash(fixture);
        test("Native PowerPoint export preserves the original style at 1440x810", delegate
        {
            object result = Load(app, fixture, CancellationToken.None);
            if (result == null) throw new Exception("Native preview unavailable.");
            using ((IDisposable)result)
            {
                Type type = result.GetType();
                string note = (string)type.GetField("NoteKey", HiddenInstance).GetValue(result);
                Bitmap image = (Bitmap)type.GetField("Image", HiddenInstance).GetValue(result);
                if (note != "document_powerpoint" || image == null || image.Size != new Size(1440, 810))
                    throw new Exception("Expected native 1440x810 image; got " + note);
                using (var expected = new Bitmap(reference)) ComparePixels(image, expected);
                image.Save(Path.Combine(directory, "powerpoint-native.png"), ImageFormat.Png);
            }
            if (Convert.ToBase64String(sourceHash) != Convert.ToBase64String(Hash(fixture)))
                throw new Exception("Source presentation was modified.");
            WaitForExit();
        });
        test("Cached native preview retains original style", delegate
        {
            object result = Load(app, fixture, CancellationToken.None);
            using ((IDisposable)result)
            using (var expected = new Bitmap(reference))
                ComparePixels((Bitmap)result.GetType().GetField("Image", HiddenInstance).GetValue(result), expected);
        });
        test("Native renderer does not attach to an existing PowerPoint session", delegate
        {
            object application = null, presentations = null, presentation = null;
            try
            {
                application = Activator.CreateInstance(Type.GetTypeFromProgID("PowerPoint.Application", true));
                presentations = ((dynamic)application).Presentations;
                if ((int)((dynamic)presentations).Count != 0) throw new Exception("PowerPoint became busy; test stopped.");
                presentation = ((dynamic)presentations).Add(0);
                using (Bitmap result = Worker(app, fixture, CancellationToken.None))
                    if (result != null) throw new Exception("Renderer attached to a running PowerPoint session.");
                if ((int)((dynamic)presentations).Count != 1) throw new Exception("Existing presentation was closed.");
            }
            finally
            {
                if (presentation != null) { try { ((dynamic)presentation).Close(); } catch { } }
                Release(presentation); Release(presentations); Release(application);
            }
            WaitForExit();
        });
        test("Canceling native export returns promptly and releases the automation server", delegate
        {
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.CancelAfter(350);
                Stopwatch elapsed = Stopwatch.StartNew();
                using (Bitmap result = Worker(app, fixture, cancellation.Token))
                    if (result != null) throw new Exception("Canceled render returned an image.");
                if (elapsed.ElapsedMilliseconds > 3000) throw new Exception("Cancellation blocked the caller.");
            }
            WaitForExit();
        });
        test("Native renderer rejects linked external images", delegate
        {
            string linked = Path.Combine(directory, "linked-media.pptx");
            File.Copy(fixture, linked, true);
            using (var file = new FileStream(linked, FileMode.Open, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Update))
            using (Stream stream = zip.CreateEntry("ppt/_rels/linked-test.xml.rels").Open())
            using (var writer = new StreamWriter(stream))
                writer.Write("<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"external\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\" TargetMode=\"External\" Target=\"https://example.invalid/image.png\"/></Relationships>");
            using (Bitmap result = Worker(app, linked, CancellationToken.None))
                if (result != null) throw new Exception("Linked media must not be loaded on hover.");
            if (Running()) throw new Exception("PowerPoint started for a rejected package.");
        });
    }

    private static object Load(Assembly app, string path, CancellationToken cancellation)
    {
        return app.GetType("DesktopHover.DocumentPreviewContent", true).GetMethod("Load", HiddenStatic)
            .Invoke(null, new object[] { path, new Size(1440, 1080), cancellation });
    }

    private static Bitmap Worker(Assembly app, string path, CancellationToken cancellation)
    {
        return (Bitmap)app.GetType("DesktopHover.DocumentPreviewWorker", true).GetMethod("TryLoadPowerPoint", HiddenStatic)
            .Invoke(null, new object[] { path, new Size(1440, 1080), cancellation });
    }

    private static void ComparePixels(Bitmap actual, Bitmap expected)
    {
        if (actual.Size != expected.Size) throw new Exception("Reference size differs.");
        long error = 0;
        for (int y = 0; y < actual.Height; y++)
            for (int x = 0; x < actual.Width; x++)
            {
                Color a = actual.GetPixel(x, y), b = expected.GetPixel(x, y);
                error += Math.Abs(a.R - b.R) + Math.Abs(a.G - b.G) + Math.Abs(a.B - b.B);
            }
        double mean = (double)error / (actual.Width * actual.Height * 3);
        if (mean > 1.0) throw new Exception("Slide styling differs from PowerPoint export: mean channel error " + mean);
    }

    private static byte[] Hash(string path) { using (var stream = File.OpenRead(path)) using (var sha = SHA256.Create()) return sha.ComputeHash(stream); }
    private static bool Running()
    {
        Process[] processes = Process.GetProcessesByName("POWERPNT");
        foreach (Process process in processes) process.Dispose();
        return processes.Length != 0;
    }
    private static void WaitForExit()
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        while (Running() && elapsed.ElapsedMilliseconds < 10000) Thread.Sleep(100);
        if (Running()) throw new Exception("PowerPoint automation server did not exit.");
    }
    private static void Release(object value)
    {
        if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
    }
}
