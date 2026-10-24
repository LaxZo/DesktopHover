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

        internal DocumentPreviewResult(Bitmap image, string noteKey)
        {
            Image = image;
            NoteKey = noteKey;
        }

        public void Dispose()
        {
            if (Image != null) Image.Dispose();
            Image = null;
        }
    }

    // Readers only inspect local content; they do not evaluate formulas or external links.
    internal static class DocumentPreviewContent
    {
        internal const long MaximumFileBytes = 512L * 1024L * 1024L;

        internal static bool IsSupported(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            string extension;
            try { extension = Path.GetExtension(path).ToLowerInvariant(); }
            catch (ArgumentException) { return false; }
            switch (extension)
            {
                case ".psd": case ".psb": case ".ai": case ".eps":
                case ".indd": case ".indt": case ".idml": case ".xd":
                case ".aep": case ".aepx": case ".prproj": case ".fla":
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
            try
            {
                cancellation.ThrowIfCancellationRequested();
                path = Path.GetFullPath(path);
                if (!IsSupported(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
                    new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network)
                    return new DocumentPreviewResult(null, "document_unavailable");
                FileInfo info = new FileInfo(path);
                if (!info.Exists || (info.Attributes & (FileAttributes)(0x1000 | 0x40000 | 0x400000)) != 0)
                    return new DocumentPreviewResult(null, "document_unavailable");
                if (info.Length > MaximumFileBytes)
                    return new DocumentPreviewResult(null, "document_limit");
                maxSize = new Size(Math.Max(1, Math.Min(1600, maxSize.Width)), Math.Max(1, Math.Min(1200, maxSize.Height)));
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
                return new DocumentPreviewResult(thumbnail, thumbnail == null ? "document_unavailable" : "document_shell");
            }
            catch (OperationCanceledException) { return null; }
            catch { return new DocumentPreviewResult(null, "document_unavailable"); }
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
