using System;
using System.IO;
using System.Threading;

namespace DesktopHover
{
    // Illustrator's PDF-compatible save option stores an actual PDF representation.
    // Render it with the existing isolated Windows PDF worker, never execute PostScript.
    internal static class AdobePdfPreviewReader
    {
        internal static bool HasPdfContent(string path, CancellationToken cancellation)
        {
            string extension = Path.GetExtension(path);
            if (!String.Equals(extension, ".ai", StringComparison.OrdinalIgnoreCase) &&
                !String.Equals(extension, ".ait", StringComparison.OrdinalIgnoreCase)) return false;
            cancellation.ThrowIfCancellationRequested();
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    byte[] header = new byte[1024];
                    int count = 0;
                    while (count < header.Length)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        int read = stream.Read(header, count, header.Length - count);
                        if (read == 0) break;
                        count += read;
                    }
                    for (int i = 0; i + 7 < count; i++)
                        if (header[i] == '%' && header[i + 1] == 'P' && header[i + 2] == 'D' && header[i + 3] == 'F' &&
                            header[i + 4] == '-' && header[i + 5] >= '1' && header[i + 5] <= '2' &&
                            header[i + 6] == '.' && header[i + 7] >= '0' && header[i + 7] <= '9') return true;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            return false;
        }
    }
}
