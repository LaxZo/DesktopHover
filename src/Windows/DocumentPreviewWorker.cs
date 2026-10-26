using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopHover
{
    // Keep PDF parsing and installed shell handlers outside the UI process.
    // Both paths share cancellation, bounded output, timeout and worker cleanup.
    internal static class DocumentPreviewWorker
    {
        private const string WorkerArgument = "--shell-thumbnail";
        private const string PdfWorkerArgument = "--pdf-preview";
        private const string PowerPointWorkerArgument = "--powerpoint-preview";
        private const int ShellTimeoutMilliseconds = 4000;
        private const int PdfTimeoutMilliseconds = 8000;
        private const int PowerPointTimeoutMilliseconds = 12000;
        private const int MaximumEdge = 1600;
        private const long MaximumOutputBytes = 16L * 1024L * 1024L;
        private static readonly string TemporaryDirectory = Path.Combine(Path.GetTempPath(), "DesktopHoverThumbnails");

        internal static Bitmap TryLoad(string path, Size maxSize, CancellationToken cancellation, bool pdf)
        {
            return TryLoad(path, maxSize, cancellation, pdf ? PdfWorkerArgument : WorkerArgument,
                pdf ? PdfTimeoutMilliseconds : ShellTimeoutMilliseconds);
        }

        internal static Bitmap TryLoadPowerPoint(string path, Size maxSize, CancellationToken cancellation)
        {
            return TryLoad(path, maxSize, cancellation, PowerPointWorkerArgument, PowerPointTimeoutMilliseconds);
        }

        private static Bitmap TryLoad(string path, Size maxSize, CancellationToken cancellation, string argument, int timeoutMilliseconds)
        {
            Process worker = null;
            string output = null;
            try
            {
                if (cancellation.IsCancellationRequested || !IsLocalFile(path)) return null;
                path = Path.GetFullPath(path);
                maxSize = BoundedSize(maxSize);
                Directory.CreateDirectory(TemporaryDirectory);
                output = Path.Combine(TemporaryDirectory, Guid.NewGuid().ToString("N") + ".png");
                var start = new ProcessStartInfo(typeof(DocumentPreviewWorker).Assembly.Location)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Arguments = argument + " " + QuoteArgument(path) + " " + QuoteArgument(output) + " " +
                        maxSize.Width.ToString(CultureInfo.InvariantCulture) + " " + maxSize.Height.ToString(CultureInfo.InvariantCulture),
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory
                };
                cancellation.ThrowIfCancellationRequested();
                Stopwatch elapsed = Stopwatch.StartNew();
                worker = Process.Start(start);
                if (worker == null) return null;
                while (!worker.WaitForExit(40))
                {
                    if (cancellation.IsCancellationRequested || elapsed.ElapsedMilliseconds >= timeoutMilliseconds)
                        return null;
                }
                if (cancellation.IsCancellationRequested || worker.ExitCode != 0) return null;
                var file = new FileInfo(output);
                if (!file.Exists || file.Length < 8 || file.Length > MaximumOutputBytes) return null;
                using (var stream = new FileStream(output, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var image = Image.FromStream(stream, false, true))
                {
                    if (image.Width <= 0 || image.Height <= 0 || image.Width > MaximumEdge || image.Height > MaximumEdge)
                        return null;
                    return CopyImage(image, maxSize);
                }
            }
            catch { return null; }
            finally
            {
                if (argument == PowerPointWorkerArgument && worker != null && !worker.HasExited && cancellation.IsCancellationRequested)
                {
                    // Killing a client during COM activation can orphan PowerPoint.
                    // The hover closes immediately; let the bounded helper finish and
                    // release its own document/server before collecting its temp files.
                    Process finishing = worker;
                    string temporary = output;
                    Task.Factory.StartNew(delegate
                    {
                        try { finishing.WaitForExit(PowerPointTimeoutMilliseconds + 3000); }
                        finally { CleanupWorker(finishing, temporary, path, true); }
                    });
                }
                else CleanupWorker(worker, output, path, argument == PowerPointWorkerArgument);
            }
        }

        private static void CleanupWorker(Process worker, string output, string path, bool powerpoint)
        {
            if (worker != null)
            {
                try { if (!worker.HasExited) { worker.Kill(); worker.WaitForExit(500); } }
                catch { }
                worker.Dispose();
            }
            if (output == null) return;
            try { File.Delete(output); } catch { }
            if (powerpoint) PowerPointPreviewReader.DeleteTemporaryFiles(output, path);
        }

        // Program.Main is STA and calls this before its singleton mutex or tray setup.
        internal static bool TryRunWorker(string[] args)
        {
            if (args == null || args.Length == 0 || (args[0] != WorkerArgument && args[0] != PdfWorkerArgument &&
                args[0] != PowerPointWorkerArgument)) return false;
            bool pdf = args[0] == PdfWorkerArgument;
            bool powerpoint = args[0] == PowerPointWorkerArgument;
            int timeoutMilliseconds = powerpoint ? PowerPointTimeoutMilliseconds : pdf ? PdfTimeoutMilliseconds : ShellTimeoutMilliseconds;
            Environment.ExitCode = 1;
            int width, height;
            if (args.Length != 5 || !Int32.TryParse(args[3], NumberStyles.None, CultureInfo.InvariantCulture, out width) ||
                !Int32.TryParse(args[4], NumberStyles.None, CultureInfo.InvariantCulture, out height) ||
                width < 1 || width > MaximumEdge || height < 1 || height > MaximumEdge)
                return true;
            // A parent may exit while a native handler is blocked. This watchdog remains
            // independent of the STA and prevents an abandoned worker running indefinitely.
            using (var watchdog = new System.Threading.Timer(delegate
            {
                try { using (Process self = Process.GetCurrentProcess()) self.Kill(); }
                catch { Environment.Exit(3); }
            }, null, timeoutMilliseconds + 2000, System.Threading.Timeout.Infinite))
            {
                try
                {
                    SetErrorMode(0x0001 | 0x0002 | 0x8000);
                    if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA || !IsLocalFile(args[1]) ||
                        !IsWorkerOutput(args[2])) return true;
                    using (Bitmap image = powerpoint
                        ? PowerPointPreviewReader.Render(Path.GetFullPath(args[1]), new Size(width, height), args[2])
                        : pdf
                        ? PdfPreviewReader.Render(Path.GetFullPath(args[1]), new Size(width, height))
                        : ShellThumbnailProvider.Extract(Path.GetFullPath(args[1]), new Size(width, height)))
                    {
                        if (image == null) return true;
                        // Never overwrite a caller's existing file, even if the flag is used manually.
                        using (var stream = new FileStream(args[2], FileMode.CreateNew, FileAccess.Write, FileShare.None))
                            image.Save(stream, ImageFormat.Png);
                    }
                    Environment.ExitCode = 0;
                }
                catch { }
            }
            return true;
        }

        private static bool IsWorkerOutput(string path)
        {
            try
            {
                string full = Path.GetFullPath(path);
                if (!String.Equals(Path.GetDirectoryName(full), Path.GetFullPath(TemporaryDirectory).TrimEnd(Path.DirectorySeparatorChar),
                    StringComparison.OrdinalIgnoreCase) || !String.Equals(Path.GetExtension(full), ".png", StringComparison.OrdinalIgnoreCase))
                    return false;
                Guid value;
                return Guid.TryParseExact(Path.GetFileNameWithoutExtension(full), "N", out value) && !File.Exists(full);
            }
            catch { return false; }
        }

        private static bool IsLocalFile(string path)
        {
            try
            {
                if (String.IsNullOrWhiteSpace(path)) return false;
                string full = Path.GetFullPath(path);
                if (full.StartsWith(@"\\", StringComparison.Ordinal)) return false;
                if (new DriveInfo(Path.GetPathRoot(full)).DriveType == DriveType.Network) return false;
                var info = new FileInfo(full);
                if (!info.Exists || info.Length > DocumentPreviewContent.MaximumFileBytes) return false;
                FileAttributes attributes = info.Attributes;
                // Offline, recall-on-open, recall-on-data-access: never hydrate a cloud file.
                return (attributes & (FileAttributes)(0x1000 | 0x40000 | 0x400000 | 0x10)) == 0;
            }
            catch { return false; }
        }

        private static Size BoundedSize(Size size)
        {
            return new Size(Math.Max(1, Math.Min(MaximumEdge, size.Width)), Math.Max(1, Math.Min(MaximumEdge, size.Height)));
        }

        internal static Bitmap CopyImage(Image image, Size size)
        {
            double fit = Math.Min(1.0, Math.Min((double)size.Width / image.Width, (double)size.Height / image.Height));
            var result = new Bitmap(Math.Max(1, (int)Math.Round(image.Width * fit)), Math.Max(1, (int)Math.Round(image.Height * fit)),
                PixelFormat.Format32bppPArgb);
            try
            {
                using (Graphics graphics = Graphics.FromImage(result))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    graphics.DrawImage(image, new Rectangle(Point.Empty, result.Size));
                }
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        // Windows CommandLineToArgvW quoting, including trailing backslashes and quotes.
        private static string QuoteArgument(string argument)
        {
            var result = new StringBuilder("\"");
            int slashes = 0;
            foreach (char character in argument)
            {
                if (character == '\\') { slashes++; continue; }
                if (character == '"') result.Append('\\', slashes * 2 + 1);
                else result.Append('\\', slashes);
                result.Append(character);
                slashes = 0;
            }
            result.Append('\\', slashes * 2);
            return result.Append('"').ToString();
        }

        [DllImport("kernel32.dll")]
        private static extern uint SetErrorMode(uint mode);
    }
}
