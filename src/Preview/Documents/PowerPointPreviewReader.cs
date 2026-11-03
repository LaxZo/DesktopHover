using System;
using System.Diagnostics;
using System.Collections.Generic;
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
        private static readonly LinkedList<SlideCache> cache = new LinkedList<SlideCache>();
        private static long cacheBytes;
        private const long MaximumCacheBytes = 128L * 1024 * 1024;
        internal static string NativeFailureReason = "ppt_export_failed";
        private static ExportWork pendingExport;
        private static long latestRequest;
        internal static void CancelPending()
        { lock (CacheGate) { if (pendingExport != null) pendingExport.Cancellation.Cancel(); } }
        internal static void FinishPendingOnExit()
        {
            Task work;
            lock (CacheGate)
            {
                if (pendingExport == null) return;
                pendingExport.Cancellation.Cancel(); work = pendingExport.Task;
            }
            // Keep the parent alive long enough to deliver cancellation and remove
            // temporary exports. The worker has its own native-operation watchdog.
            try { work.Wait(65000); } catch { }
        }
        internal static void Invalidate(string path)
        {
            lock (CacheGate)
            {
                if (pendingExport != null && pendingExport.Key.StartsWith(Path.GetFullPath(path) + "|", StringComparison.OrdinalIgnoreCase))
                    pendingExport.Cancellation.Cancel();
                var node = cache.First;
                while (node != null)
                {
                    var next = node.Next;
                    if (node.Value.Key.StartsWith(Path.GetFullPath(path) + "|", StringComparison.OrdinalIgnoreCase))
                    { cacheBytes -= node.Value.Bytes; cache.Remove(node); }
                    node = next;
                }
            }
        }
        private sealed class SlideCache
        {
            internal string Key;
            internal string Identity;
            internal Size Size;
            internal byte[][] Slides;
            internal long Bytes;
        }
        private sealed class ExportWork
        {
            internal readonly CancellationTokenSource Cancellation = new CancellationTokenSource();
            internal int Readers;
            internal string Key, Reason;
            internal Task<byte[][]> Task;
            internal volatile byte[][] Slides;
            internal volatile int Total;
        }

        internal static bool IsSupported(string path)
        {
            switch (Path.GetExtension(path).ToLowerInvariant())
            {
                case ".pptx": case ".pptm": case ".potx": case ".potm":
                case ".ppsx": case ".ppsm": case ".ppt": case ".pot": case ".pps": return true;
                default: return false;
            }
        }

        internal static DocumentPreviewResult Load(string path, Size maximum, CancellationToken cancellation, Action<DocumentPreviewResult> progress = null)
        {
            cancellation.ThrowIfCancellationRequested();
            var file = new FileInfo(path);
            long length = file.Length, writeTime = file.LastWriteTimeUtc.Ticks;
            string failureReason = null;
            if (SupportsNative(path))
            {
                if (progress != null)
                {
                    DocumentPreviewResult thumbnail = OfficePreviewReader.TryLoad(path, maximum, cancellation);
                    if (thumbnail != null) progress(thumbnail);
                }
                byte[][] slides = LoadSlides(path, length, writeTime, maximum, cancellation,
                    delegate(byte[][] available, int total)
                    {
                        if (progress != null) progress(new DocumentPreviewResult(LoadSlide(available[0], maximum), "document_powerpoint")
                            { Slides = available, SlideCount = total });
                    }, out failureReason);
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
            if (saved != null) saved.DetailKey = failureReason;
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
                        return new DocumentPreviewResult(shell, "document_shell") { DetailKey = failureReason };
                    }
                    shell.Dispose();
                }
                cancellation.ThrowIfCancellationRequested();
                if (saved != null) return saved;
                return new DocumentPreviewResult(null, "document_unavailable") { DetailKey = failureReason };
            }
            catch { if (saved != null) saved.Dispose(); throw; }
        }

        private static byte[][] LoadSlides(string path, long length, long writeTime, Size maximum,
            CancellationToken cancellation, Action<byte[][], int> progress, out string reason)
        {
            string identity = Path.GetFullPath(path) + "|" + length + "|" + writeTime + "|";
            string key = identity + maximum.Width + "x" + maximum.Height;
            long request = 0;
            reason = null;
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                ExportWork work;
                lock (CacheGate)
                {
                    for (var node = cache.First; node != null; node = node.Next)
                        if (String.Equals(node.Value.Identity, identity, StringComparison.OrdinalIgnoreCase) &&
                            node.Value.Size.Width >= maximum.Width && node.Value.Size.Height >= maximum.Height)
                        {
                            byte[][] hit = node.Value.Slides; cache.Remove(node); cache.AddFirst(node); return hit;
                        }
                    if (request == 0) request = ++latestRequest;
                    else if (request != latestRequest) throw new OperationCanceledException();
                    if (pendingExport == null || pendingExport.Task.IsCompleted)
                    {
                        bool followsExport = pendingExport != null;
                        if (pendingExport != null) pendingExport.Cancellation.Dispose();
                        var created = new ExportWork { Key = key };
                        created.Task = Task.Factory.StartNew(delegate
                        {
                            // Office can outlive the helper briefly after its last COM reference
                            // is released. Let that owned export settle before testing availability.
                            if (followsExport)
                            {
                                var settled = Stopwatch.StartNew();
                                while (PowerPointIsRunning() && settled.ElapsedMilliseconds < 5000)
                                {
                                    if (created.Cancellation.IsCancellationRequested) return null;
                                    created.Cancellation.Token.WaitHandle.WaitOne(80);
                                }
                            }
                            byte[][] result = DocumentPreviewWorker.TryLoadPowerPointSlides(path, maximum, created.Cancellation.Token,
                                delegate(byte[][] slides, int total) { created.Total = total; created.Slides = slides; },
                                delegate(string failure) { created.Reason = failure; });
                            if (result != null && !created.Cancellation.IsCancellationRequested)
                            {
                                long bytes = 0; foreach (byte[] slide in result) bytes += slide.Length;
                                lock (CacheGate)
                                {
                                    while (cache.Count >= 4 || (cache.Count > 0 && cacheBytes + bytes > MaximumCacheBytes))
                                    { cacheBytes -= cache.Last.Value.Bytes; cache.RemoveLast(); }
                                    if (!created.Cancellation.IsCancellationRequested)
                                    { cache.AddFirst(new SlideCache { Key = key, Identity = identity, Size = maximum, Slides = result, Bytes = bytes }); cacheBytes += bytes; }
                                }
                            }
                            return result;
                        }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                        pendingExport = created;
                    }
                    work = pendingExport;
                    if (!String.Equals(work.Key, key, StringComparison.OrdinalIgnoreCase)) work.Cancellation.Cancel();
                    work.Readers++;
                }
                byte[][] last = null;
                bool requested = String.Equals(work.Key, key, StringComparison.OrdinalIgnoreCase) && !work.Cancellation.IsCancellationRequested;
                try
                {
                    while (true)
                    {
                        bool complete = work.Task.Wait(40, cancellation);
                        byte[][] available = work.Slides;
                        if (requested && available != null && available != last && !work.Cancellation.IsCancellationRequested)
                        { last = available; progress(available, work.Total); }
                        if (complete) break;
                    }
                    cancellation.ThrowIfCancellationRequested();
                    if (requested && !work.Cancellation.IsCancellationRequested)
                    { reason = work.Reason; return work.Task.GetAwaiter().GetResult(); }
                }
                finally
                {
                    lock (CacheGate)
                        if (--work.Readers == 0 && !work.Task.IsCompleted) work.Cancellation.Cancel();
                }
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
            if (File.Exists(outputDirectory + ".cancel")) return false;
            if (!SupportsNative(path) || Thread.CurrentThread.GetApartmentState() != ApartmentState.STA) return false;
            bool acquired = false;
            using (var gate = new Mutex(false, @"Local\DesktopHover_PowerPointPreview"))
            {
                try
                {
                    try { acquired = gate.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired || PowerPointIsRunning()) { NativeFailureReason = "ppt_busy"; return false; }
                    Type applicationType = Type.GetTypeFromProgID("PowerPoint.Application", false);
                    if (applicationType == null) { NativeFailureReason = "ppt_missing"; return false; }
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
                                if (File.Exists(outputDirectory + ".cancel")) return false;
                                if (target.Length + count > DocumentPreviewContent.MaximumFileBytes) return false;
                                target.Write(buffer, 0, count);
                            }
                        }
                        if (!PowerPointPreviewCopy.Prepare(copy) || !IsStaticPackage(copy)) { NativeFailureReason = "ppt_unsupported"; return false; }
                        if (File.Exists(outputDirectory + ".cancel")) return false;
                        if (PowerPointIsRunning()) { NativeFailureReason = "ppt_busy"; return false; }
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
                if (slideCount < 1 || slideCount > MaximumSlides) { NativeFailureReason = "ppt_slide_limit"; return false; }
                File.WriteAllText(Path.Combine(outputDirectory, "count.tmp"), slideCount.ToString(System.Globalization.CultureInfo.InvariantCulture));
                File.Move(Path.Combine(outputDirectory, "count.tmp"), Path.Combine(outputDirectory, "slides.count"));
                long totalBytes = 0;
                for (int i = 1; i <= slideCount; i++)
                {
                    if (File.Exists(outputDirectory + ".cancel")) return false;
                    slide = ((dynamic)slides)[i];
                    string rendered = Path.Combine(outputDirectory,
                        "slide-" + i.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".png");
                    ((dynamic)slide).Export(rendered + ".writing.png", "PNG", pixelsWide, pixelsHigh);
                    Release(slide); slide = null;
                    var info = new FileInfo(rendered + ".writing.png");
                    if (!info.Exists || info.Length < 8 || info.Length > MaximumSlideBytes) return false;
                    totalBytes += info.Length;
                    if (totalBytes > MaximumSlidesBytes) return false;
                    File.Move(rendered + ".writing.png", rendered);
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
            try { File.Delete(output + ".reason"); } catch { }
            try { File.Delete(output + ".cancel"); } catch { }
            try { if (Directory.Exists(output)) Directory.Delete(output, true); } catch { }
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) { try { Marshal.FinalReleaseComObject(value); } catch { } }
        }
    }
}
