using System;
using System.Drawing;
using System.IO;
using System.Threading;

namespace DesktopHover
{
    internal sealed class DocumentPreviewResult : IDisposable
    {
        internal Bitmap Image;
        internal string NoteKey;
        internal string MediaPath;
        internal byte[][] Slides;

        internal DocumentPreviewResult(Bitmap image, string noteKey)
        {
            Image = image;
            NoteKey = noteKey;
        }

        public void Dispose()
        {
            if (Image != null) Image.Dispose();
            Image = null;
            MediaPath = null;
            Slides = null;
        }
    }

    // Readers only inspect local content; they do not evaluate formulas or external links.
    internal static class DocumentPreviewContent
    {
        internal const long MaximumFileBytes = 512L * 1024L * 1024L;

        internal static bool IsSupported(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            if (AdobeProjectFiles.IsSupported(path)) return true;
            string extension;
            try { extension = Path.GetExtension(path).ToLowerInvariant(); }
            catch (ArgumentException) { return false; }
            switch (extension)
            {
                case ".pdf":
                case ".docx": case ".docm": case ".dotx": case ".dotm":
                case ".xlsx": case ".xlsm": case ".xltx": case ".xltm":
                case ".pptx": case ".pptm": case ".potx": case ".potm":
                case ".ppsx": case ".ppsm":
                case ".doc": case ".dot": case ".xls": case ".xlt": case ".xlsb":
                case ".ppt": case ".pot": case ".pps":
                case ".odt": case ".ods": case ".odp":
                    return true;
                default: return false;
            }
        }

        internal static DocumentPreviewResult Load(string path, Size maxSize, CancellationToken cancellation)
        {
            return LoadCore(path, maxSize, cancellation, true);
        }

        internal static DocumentPreviewResult LoadStill(string path, Size maxSize, CancellationToken cancellation)
        {
            return LoadCore(path, maxSize, cancellation, false);
        }

        private static DocumentPreviewResult LoadCore(string path, Size maxSize, CancellationToken cancellation, bool allowVideo)
        {
            string unavailableKey = "document_unavailable";
            try
            {
                cancellation.ThrowIfCancellationRequested();
                path = Path.GetFullPath(path);
                bool pdfFile = String.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
                if (pdfFile) unavailableKey = "document_pdf_unavailable";
                else if (AdobeProjectFiles.IsSupported(path)) unavailableKey = "document_adobe_unavailable";
                if (!IsSupported(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
                    new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network)
                    return new DocumentPreviewResult(null, unavailableKey);
                FileInfo info = new FileInfo(path);
                if (!info.Exists || (info.Attributes & (FileAttributes)(0x1000 | 0x40000 | 0x400000)) != 0)
                    return new DocumentPreviewResult(null, unavailableKey);
                maxSize = new Size(Math.Max(1, Math.Min(1600, maxSize.Width)), Math.Max(1, Math.Min(1200, maxSize.Height)));
                if (AdobeProjectFiles.IsSupported(path))
                {
                    // An explicitly named export can preview even a large project without parsing it.
                    DocumentPreviewResult saved = allowVideo ? AdobePreviewSidecar.TryLoad(path, maxSize, cancellation)
                        : AdobePreviewSidecar.TryLoadStill(path, maxSize, cancellation);
                    if (saved != null)
                    {
                        if (cancellation.IsCancellationRequested) { saved.Dispose(); return null; }
                        return saved;
                    }
                }
                if (info.Length > MaximumFileBytes)
                    return new DocumentPreviewResult(null, "document_limit");
                if (pdfFile)
                {
                    Bitmap pdf = DocumentPreviewWorker.TryLoad(path, maxSize, cancellation, true);
                    if (cancellation.IsCancellationRequested)
                    {
                        if (pdf != null) pdf.Dispose();
                        return null;
                    }
                    return new DocumentPreviewResult(pdf, pdf == null ? "document_pdf_unavailable" : "document_pdf");
                }
                if (PowerPointPreviewReader.IsSupported(path))
                {
                    DocumentPreviewResult powerpoint = PowerPointPreviewReader.Load(path, maxSize, cancellation);
                    if (cancellation.IsCancellationRequested) { if (powerpoint != null) powerpoint.Dispose(); return null; }
                    return powerpoint;
                }
                if (AdobePdfPreviewReader.HasPdfContent(path, cancellation))
                {
                    Bitmap artwork = DocumentPreviewWorker.TryLoad(path, maxSize, cancellation, true);
                    if (cancellation.IsCancellationRequested) { if (artwork != null) artwork.Dispose(); return null; }
                    if (artwork != null) return new DocumentPreviewResult(artwork, "document_pdf");
                }
                DocumentPreviewResult result = TryReader(path, maxSize, cancellation, true);
                if (result == null) result = TryReader(path, maxSize, cancellation, false);
                if (result != null)
                {
                    if (cancellation.IsCancellationRequested) { result.Dispose(); return null; }
                    return result;
                }
                cancellation.ThrowIfCancellationRequested();
                Bitmap thumbnail = ShellThumbnailProvider.TryLoad(path, maxSize, cancellation);
                if (cancellation.IsCancellationRequested)
                {
                    if (thumbnail != null) thumbnail.Dispose();
                    return null;
                }
                return new DocumentPreviewResult(thumbnail, thumbnail == null ? unavailableKey : "document_shell");
            }
            catch (OperationCanceledException) { return null; }
            catch { return new DocumentPreviewResult(null, unavailableKey); }
        }

        private static DocumentPreviewResult TryReader(string path, Size maxSize, CancellationToken cancellation, bool office)
        {
            cancellation.ThrowIfCancellationRequested();
            try
            {
                return office ? OfficePreviewReader.TryLoad(path, maxSize, cancellation)
                    : AdobePreviewReader.TryLoad(path, maxSize, cancellation);
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; }
        }
    }
}
