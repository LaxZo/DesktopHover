using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using System.Xml;

namespace DesktopHover
{
    // Preserve the slide's design: native export, saved image or Windows thumbnail.
    // Never reconstruct a slide from a subset of its XML shapes and text.
    internal static class PowerPointPreviewReader
    {
        private static readonly object CacheGate = new object();
        private static string cachedPath;
        private static long cachedLength, cachedWriteTime;
        private static Size cachedSize;
        private static Bitmap cachedImage;

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
            lock (CacheGate)
            {
                if (cachedImage != null && String.Equals(path, cachedPath, StringComparison.OrdinalIgnoreCase) &&
                    cachedLength == length && cachedWriteTime == writeTime && cachedSize == maximum)
                    return new DocumentPreviewResult(new Bitmap(cachedImage), "document_powerpoint");
            }
            DocumentPreviewResult saved = OfficePreviewReader.TryLoad(path, maximum, cancellation);
            try
            {
                if (saved != null && Fills(saved.Image, maximum)) { var ready = saved; saved = null; return ready; }
                if (SupportsNative(path))
                {
                    Bitmap native = DocumentPreviewWorker.TryLoadPowerPoint(path, maximum, cancellation);
                    if (native != null)
                    {
                        if (cancellation.IsCancellationRequested) { native.Dispose(); cancellation.ThrowIfCancellationRequested(); }
                        lock (CacheGate)
                        {
                            if (cachedImage != null) cachedImage.Dispose();
                            cachedImage = new Bitmap(native);
                            cachedPath = path; cachedLength = length; cachedWriteTime = writeTime; cachedSize = maximum;
                        }
                        return new DocumentPreviewResult(native, "document_powerpoint");
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                Bitmap shell = ShellThumbnailProvider.TryLoad(path, maximum, cancellation);
                if (shell != null)
                {
                    if (cancellation.IsCancellationRequested) { shell.Dispose(); cancellation.ThrowIfCancellationRequested(); }
                    if (saved == null || (long)shell.Width * shell.Height > (long)saved.Image.Width * saved.Image.Height)
                        return new DocumentPreviewResult(shell, "document_shell");
                    shell.Dispose();
                }
                cancellation.ThrowIfCancellationRequested();
                var fallback = saved;
                saved = null;
                return fallback ?? new DocumentPreviewResult(null, "document_unavailable");
            }
            finally { if (saved != null) saved.Dispose(); }
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
        internal static Bitmap Render(string path, Size maximum, string output)
        {
            if (!SupportsNative(path) || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) return null;
            bool acquired = false;
            using (var gate = new Mutex(false, @"Local\DesktopHover_PowerPointPreview"))
            {
                try
                {
                    try { acquired = gate.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired || PowerPointIsRunning()) return null;
                    Type applicationType = Type.GetTypeFromProgID("PowerPoint.Application", false);
                    if (applicationType == null) return null;
                    string copy = SourceCopy(output, path), rendered = output + ".slide.png";
                    try
                    {
                        // A unique copy prevents Open from returning an existing user document.
                        using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var target = new FileStream(copy, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        {
                            byte[] buffer = new byte[65536];
                            int count;
                            while ((count = input.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                if (target.Length + count > DocumentPreviewContent.MaximumFileBytes) return null;
                                target.Write(buffer, 0, count);
                            }
                        }
                        if (!IsStaticPackage(copy) || PowerPointIsRunning()) return null;
                        Export(applicationType, copy, rendered, maximum);
                        var info = new FileInfo(rendered);
                        if (!info.Exists || info.Length > 16L * 1024L * 1024L) return null;
                        using (Image image = Image.FromFile(rendered))
                        {
                            if (image.Width > 1600 || image.Height > 1200) return null;
                            return DocumentPreviewWorker.CopyImage(image, maximum);
                        }
                    }
                    finally { DeleteTemporaryFiles(output, path); }
                }
                finally { if (acquired) gate.ReleaseMutex(); }
            }
        }

        private static void Export(Type applicationType, string copy, string rendered, Size maximum)
        {
            object application = null, presentations = null, windows = null, presentation = null,
                pageSetup = null, slides = null, slide = null;
            try
            {
                application = Activator.CreateInstance(applicationType);
                dynamic app = application;
                presentations = app.Presentations;
                windows = app.Windows;
                // Recheck after activation in case the user started PowerPoint meanwhile.
                if ((int)((dynamic)presentations).Count != 0 || (int)((dynamic)windows).Count != 0 || (int)app.Visible != 0) return;
                int security = (int)app.AutomationSecurity;
                int alerts = (int)app.DisplayAlerts;
                try
                {
                    // https://learn.microsoft.com/office/vba/api/powerpoint.application.automationsecurity
                    app.AutomationSecurity = 3; // msoAutomationSecurityForceDisable
                    app.DisplayAlerts = 1; // ppAlertsNone: a hover must not open repair/password prompts.
                    presentation = ((dynamic)presentations).Open(copy, -1, 0, 0); // ReadOnly, no window.
                }
                finally
                {
                    try { app.AutomationSecurity = security; }
                    finally { app.DisplayAlerts = alerts; }
                }
                pageSetup = ((dynamic)presentation).PageSetup;
                double width = (double)((dynamic)pageSetup).SlideWidth, height = (double)((dynamic)pageSetup).SlideHeight;
                if (width <= 0 || height <= 0 || Double.IsNaN(width) || Double.IsNaN(height) ||
                    Double.IsInfinity(width) || Double.IsInfinity(height)) return;
                double fit = Math.Min(maximum.Width / width, maximum.Height / height);
                int pixelsWide = Math.Max(1, (int)Math.Round(width * fit));
                int pixelsHigh = Math.Max(1, (int)Math.Round(height * fit));
                slides = ((dynamic)presentation).Slides;
                if ((int)((dynamic)slides).Count < 1) return;
                slide = ((dynamic)slides)[1];
                // Native export preserves themes, masters, fonts, charts and effects.
                // https://learn.microsoft.com/office/vba/api/powerpoint.slide.export
                ((dynamic)slide).Export(rendered, "PNG", pixelsWide, pixelsHigh);
            }
            finally
            {
                Release(slide); Release(slides); Release(pageSetup);
                if (presentation != null) { try { ((dynamic)presentation).Close(); } catch { } }
                Release(presentation); Release(windows); Release(presentations); Release(application);
                // Releasing the invisible automation server lets it exit on its own.
                // Never call Quit: the user could have opened a presentation during export.
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
            try { File.Delete(output + ".slide.png"); } catch { }
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) { try { Marshal.FinalReleaseComObject(value); } catch { } }
        }
    }
}
