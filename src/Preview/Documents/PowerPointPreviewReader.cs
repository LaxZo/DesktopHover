using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace DesktopHover
{
    // Preserve the slide's design: native export, saved image or Windows thumbnail.
    // Never reconstruct a slide from a subset of its XML shapes and text.
    internal static class PowerPointPreviewReader
    {
        private const int MaximumSlides = 200;
        private const long MaximumSlideBytes = 16L * 1024L * 1024L;
        private const long MaximumSlidesBytes = 64L * 1024L * 1024L;
        private static readonly object CacheGate = new object();
        private static string cachedPath;
        private static long cachedLength, cachedWriteTime;
        private static Size cachedSize;
        private static byte[][] cachedSlides;
        private static Task<byte[][]> pendingExport;
        private static string pendingPath;
        private static long pendingLength, pendingWriteTime;
        private static Size pendingSize;

        internal static bool IsSupported(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".pptx": case ".pptm": case ".potx": case ".potm":
                case ".ppsx": case ".ppsm": case ".ppt": case ".pot": case ".pps": return true;
                default: return false;
            }
        }

        internal static DocumentPreviewResult Load(string path, Size maximum, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            var file = new FileInfo(path);
            long length = file.Length, writeTime = file.LastWriteTimeUtc.Ticks;
            if (SupportsNative(path))
            {
                byte[][] slides = LoadSlides(path, length, writeTime, maximum, cancellation);
                if (slides != null && slides.Length > 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    var result = new DocumentPreviewResult(LoadSlide(slides[0], maximum), "document_powerpoint");
                    result.Slides = slides;
                    return result;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            DocumentPreviewResult saved = OfficePreviewReader.TryLoad(path, maximum, cancellation);
            if (saved != null && Fills(saved.Image, maximum)) return saved;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                Bitmap shell = ShellThumbnailProvider.TryLoad(path, maximum, cancellation);
                if (shell != null)
                {
                    if (cancellation.IsCancellationRequested) { shell.Dispose(); cancellation.ThrowIfCancellationRequested(); }
                    if (saved == null || (long)shell.Width * shell.Height > (long)saved.Image.Width * saved.Image.Height)
                    {
                        if (saved != null) saved.Dispose();
                        return new DocumentPreviewResult(shell, "document_shell");
                    }
                    shell.Dispose();
                }
                cancellation.ThrowIfCancellationRequested();
                if (saved != null) return saved;
                return new DocumentPreviewResult(null, "document_unavailable");
            }
            catch { if (saved != null) saved.Dispose(); throw; }
        }

        private static byte[][] LoadSlides(string path, long length, long writeTime, Size maximum, CancellationToken cancellation)
        {
            // Moving away cancels only this waiter. Keep one bounded export alive so
            // hovering the same file again can reuse it instead of falling back while
            // its PowerPoint helper still owns the native export mutex.
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                Task<byte[][]> export;
                bool requestedFile;
                lock (CacheGate)
                {
                    if (cachedSlides != null && String.Equals(path, cachedPath, StringComparison.OrdinalIgnoreCase) &&
                        cachedLength == length && cachedWriteTime == writeTime && cachedSize == maximum) return cachedSlides;
                    if (pendingExport == null || pendingExport.IsCompleted)
                    {
                        pendingPath = path; pendingLength = length; pendingWriteTime = writeTime; pendingSize = maximum;
                        pendingExport = Task.Factory.StartNew(delegate
                        {
                            byte[][] result = DocumentPreviewWorker.TryLoadPowerPointSlides(path, maximum, CancellationToken.None);
                            if (result != null && result.Length > 0)
                            {
                                lock (CacheGate)
                                {
                                    cachedSlides = result;
                                    cachedPath = path; cachedLength = length; cachedWriteTime = writeTime; cachedSize = maximum;
                                }
                            }
                            return result;
                        }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                    }
                    export = pendingExport;
                    requestedFile = String.Equals(path, pendingPath, StringComparison.OrdinalIgnoreCase) &&
                        pendingLength == length && pendingWriteTime == writeTime && pendingSize == maximum;
                }
                while (!export.Wait(40, cancellation)) { }
                cancellation.ThrowIfCancellationRequested();
                if (requestedFile) return export.GetAwaiter().GetResult();
            }
        }

        private static Bitmap LoadSlide(byte[] bytes, Size maximum)
        {
            using (var stream = new MemoryStream(bytes, false))
            using (Image image = Image.FromStream(stream, false, true))
                return DocumentPreviewWorker.CopyImage(image, maximum);
        }

        private static bool Fills(Bitmap image, Size maximum)
        {
            return image != null && Math.Min((double)maximum.Width / image.Width, (double)maximum.Height / image.Height) <= 1.001;
        }

        private static bool SupportsNative(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".pptx" || extension == ".potx" || extension == ".ppsx";
        }

        // Called only by the bounded STA helper. PowerPoint is a shared COM server:
        // do not attach to a user's running session, Quit it, or kill its process.
        // Each rendered slide stays on disk until the parent has copied the PNG set.
        internal static bool RenderSlides(string path, Size maximum, string outputDirectory)
        {
            if (!SupportsNative(path) || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) return false;
            bool acquired = false;
            using (var gate = new Mutex(false, @"Local\DesktopHover_PowerPointPreview"))
            {
                try
                {
                    try { acquired = gate.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired || PowerPointIsRunning()) return false;
                    Type applicationType = Type.GetTypeFromProgID("PowerPoint.Application", false);
                    if (applicationType == null) return false;
                    string copy = SourceCopy(outputDirectory, path);
                    try
                    {
                        using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var target = new FileStream(copy, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[65536];
                            int count;
                            while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                if (target.Length + count > DocumentPreviewContent.MaximumFileBytes) return false;
                                target.Write(buffer, 0, count);
                            }
                        }
                        if (!PowerPointPreviewCopy.Prepare(copy) || !IsStaticPackage(copy) || PowerPointIsRunning()) return false;
                        Directory.CreateDirectory(outputDirectory);
                        return ExportSlides(applicationType, copy, outputDirectory, maximum);
                    }
                    finally { try { File.Delete(SourceCopy(outputDirectory, path)); } catch { } }
                }
                finally { if (acquired) gate.ReleaseMutex(); }
            }
        }

        private static bool ExportSlides(Type applicationType, string copy, string outputDirectory, Size maximum)
        {
            object application = null, presentations = null, windows = null, presentation = null,
                pageSetup = null, slides = null, slide = null;
            try
            {
                application = Activator.CreateInstance(applicationType);
                dynamic app = application;
                presentations = app.Presentations;
                windows = app.Windows;
                if ((int)((dynamic)presentations).Count != 0 || (int)((dynamic)windows).Count != 0 || (int)app.Visible != 0) return false;
                int security = (int)app.AutomationSecurity;
                int alerts = (int)app.DisplayAlerts;
                try
                {
                    app.AutomationSecurity = 3;
                    app.DisplayAlerts = 1;
                    presentation = ((dynamic)presentations).Open(copy, -1, 0, 0);
                }
                finally
                {
                    try { app.AutomationSecurity = security; }
                    finally { app.DisplayAlerts = alerts; }
                }
                pageSetup = ((dynamic)presentation).PageSetup;
                double width = (double)((dynamic)pageSetup).SlideWidth, height = (double)((dynamic)pageSetup).SlideHeight;
                if (width <= 0 || height <= 0 || Double.IsNaN(width) || Double.IsNaN(height) ||
                    Double.IsInfinity(width) || Double.IsInfinity(height)) return false;
                double fit = Math.Min(maximum.Width / width, maximum.Height / height);
                int pixelsWide = Math.Max(1, (int)Math.Round(width * fit));
                int pixelsHigh = Math.Max(1, (int)Math.Round(height * fit));
                slides = ((dynamic)presentation).Slides;
                int slideCount = (int)((dynamic)slides).Count;
                if (slideCount < 1 || slideCount > MaximumSlides) return false;
                long totalBytes = 0;
                for (int i = 1; i <= slideCount; i++)
                {
                    slide = ((dynamic)slides)[i];
                    string rendered = Path.Combine(outputDirectory,
                        "slide-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".png");
                    ((dynamic)slide).Export(rendered, "PNG", pixelsWide, pixelsHigh);
                    Release(slide); slide = null;
                    var info = new FileInfo(rendered);
                    if (!info.Exists || info.Length < 8 || info.Length > MaximumSlideBytes) return false;
                    totalBytes += info.Length;
                    if (totalBytes > MaximumSlidesBytes) return false;
                }
                return true;
            }
            finally
            {
                Release(slide); Release(slides); Release(pageSetup);
                if (presentation != null) { try { ((dynamic)presentation).Close(); } catch { } }
                Release(presentation); Release(windows); Release(presentations); Release(application);
                // Never call Quit: the user may have opened a document during export.
            }
        }

        private static bool PowerPointIsRunning()
        {
            Process[] processes = Process.GetProcessesByName("POWERPNT");
            foreach (Process process in processes) process.Dispose();
            return processes.Length != 0;
        }

        // Do not hydrate linked media or activate embedded executable content during hover.
        private static bool IsStaticPackage(string path)
        {
            using (var input = File.OpenRead(path))
            {
                if (!DocumentArchiveGuard.IsSafe(input, CancellationToken.None)) return false;
                using (var archive = new ZipArchive(input, ZipArchiveMode.Read))
                {
                    long xmlBytes = 0;
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        string name = entry.FullName.ToLowerInvariant();
                        if (name.Contains("vbaproject") || name.StartsWith("ppt/activex/", StringComparison.Ordinal) ||
                            (name.StartsWith("ppt/embeddings/", StringComparison.Ordinal) && name.EndsWith(".bin", StringComparison.Ordinal))) return false;
                        if (!name.EndsWith(".rels", StringComparison.Ordinal)) continue;
                        xmlBytes += entry.Length;
                        if (entry.Length > 4 * 1024 * 1024 || xmlBytes > 16 * 1024 * 1024) return false;
                        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                            MaxCharactersInDocument = 4 * 1024 * 1024 };
                        using (Stream stream = entry.Open())
                        using (XmlReader reader = XmlReader.Create(stream, settings))
                        {
                            while (reader.Read())
                            {
                                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != "Relationship") continue;
                                string type = reader.GetAttribute("Type") ?? "";
                                if (String.Equals(reader.GetAttribute("TargetMode"), "External", StringComparison.OrdinalIgnoreCase) &&
                                    !type.EndsWith("/hyperlink", StringComparison.Ordinal)) return false;
                            }
                        }
                    }
                    return true;
                }
            }
        }

        private static string SourceCopy(string output, string path) { return output + ".source" + Path.GetExtension(path).ToLowerInvariant(); }

        internal static void DeleteTemporaryFiles(string output, string path)
        {
            try { File.Delete(SourceCopy(output, path)); } catch { }
            try { File.Delete(SourceCopy(output, path) + ".sanitized"); } catch { }
            try { File.Delete(output + ".slide.png"); } catch { }
            try { if (Directory.Exists(output)) Directory.Delete(output, true); } catch { }
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) { try { Marshal.FinalReleaseComObject(value); } catch { } }
        }
    }
}
