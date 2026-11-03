using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace DesktopHover
{
    internal static partial class OfficePreviewReader
    {
        private static readonly object cacheGate = new object();
        private static readonly List<PackageData> packages = new List<PackageData>();

        // At most two saved documents; no file handles or GDI objects are retained.
        // XML is charged conservatively for its managed node/string overhead.
        private sealed class PackageData
        {
            internal string Path, Stamp;
            internal long Bytes;
            internal readonly Dictionary<string, byte[]> Parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            internal readonly Dictionary<string, XmlDocument> Documents = new Dictionary<string, XmlDocument>(StringComparer.Ordinal);
            internal readonly Dictionary<string, Dictionary<string, Relation>> Relationships = new Dictionary<string, Dictionary<string, Relation>>(StringComparer.Ordinal);
            internal readonly Dictionary<string, SheetContent> Sheets = new Dictionary<string, SheetContent>(StringComparer.Ordinal);
            internal bool Reserve(long bytes)
            {
                if (Bytes + bytes > 48L * 1024 * 1024) return false;
                Bytes += bytes; return true;
            }
        }

        private sealed class SheetContent
        {
            internal Dictionary<int, string> Values;
            internal int HighestColumn, HighestRow;
            internal bool Truncated;
        }

        private static PackageData CachedPackage(string path)
        {
            path = System.IO.Path.GetFullPath(path);
            string stamp = PreviewFileStamp.Read(path);
            lock (cacheGate)
            {
                for (int i = packages.Count - 1; i >= 0; i--)
                    if (String.Equals(packages[i].Path, path, StringComparison.OrdinalIgnoreCase))
                    {
                        PackageData data = packages[i]; packages.RemoveAt(i);
                        if (data.Stamp == stamp) { packages.Add(data); return data; }
                    }
                var next = new PackageData { Path = path, Stamp = stamp };
                if (packages.Count >= 2) packages.RemoveAt(0);
                packages.Add(next); return next;
            }
        }

        internal static void Invalidate(string path)
        {
            lock (cacheGate)
                packages.RemoveAll(data => String.Equals(data.Path, path, StringComparison.OrdinalIgnoreCase));
        }
    }
}
