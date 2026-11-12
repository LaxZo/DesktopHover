using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace DesktopHover
{
    internal static class ArchiveListing
    {
        internal static bool Supports(string path)
        {
            string ext = Path.GetExtension(path ?? "").ToLowerInvariant();
            return ext == ".zip" || ext == ".edoc" || ext == ".asice";
        }

        internal static TextPreviewContent Load(FileInfo file, CancellationToken cancellation)
        {
            bool signed = !file.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase);
            var rows = new List<string[]>();
            int files = 0, entries = 0;
            bool shortened = false;
            using (var stream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                if (!DocumentArchiveGuard.IsSafe(stream, cancellation)) throw new InvalidDataException("Unsupported archive directory.");
                stream.Position = 0;
                using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                foreach (ZipArchiveEntry entry in zip.Entries)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string name = entry.FullName.Replace('\\', '/');
                    bool folder = name.EndsWith("/", StringComparison.Ordinal);
                    if (signed && (folder || name.Equals("mimetype", StringComparison.OrdinalIgnoreCase) ||
                        name.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))) continue;
                    if (!folder) files++;
                    entries++;
                    if (rows.Count < 300)
                    {
                        if (name.Length > 500) shortened = true;
                        string display = name.Length <= 500 ? name : name.Substring(0, 499) + "…";
                        display = TextPreviewContent.NormalizeLine(display, 0, display.Length);
                        rows.Add(new[] { display, folder ? "—" : entry.Length.ToString(CultureInfo.InvariantCulture) + " B" });
                    }
                }
            }
            return new TextPreviewContent { LanguageName = file.Extension.TrimStart('.').ToUpperInvariant(), TableRows = rows.ToArray(),
                TableColumns = 2, FileBytes = file.Length, ArchiveFileCount = files, Truncated = shortened || entries > rows.Count };
        }
    }
}
