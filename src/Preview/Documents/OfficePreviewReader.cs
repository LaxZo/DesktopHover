using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Xml;

namespace DesktopHover
{
    // A bounded, read-only OOXML preview, not an Office layout engine.
    // Package relationships: https://learn.microsoft.com/en-us/office/open-xml/spreadsheet/structure-of-a-spreadsheetml-document
    internal static partial class OfficePreviewReader
    {
        private const int MaximumEntries = 4096;
        private const int MaximumXmlBytes = 4 * 1024 * 1024;
        private const int MaximumImageBytes = 12 * 1024 * 1024;
        private const long MaximumImagePixels = 24000000;
        internal static bool SupportsTheming(string path)
        {
            switch (Path.GetExtension(path ?? "").ToLowerInvariant())
            {
                case ".docx": case ".docm": case ".dotx": case ".dotm":
                case ".xlsx": case ".xlsm": case ".xltx": case ".xltm": return true;
                default: return false;
            }
        }

        internal static DocumentPreviewResult TryLoad(string path, Size maxSize, CancellationToken cancellation, OfficePreviewPalette palette = null, OfficeViewOptions view = null)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            int kind;
            switch (ext)
            {
                case ".docx": case ".docm": case ".dotx": case ".dotm": kind = 1; break;
                case ".xlsx": case ".xlsm": case ".xltx": case ".xltm": kind = 2; break;
                case ".pptx": case ".pptm": case ".potx": case ".potm": case ".ppsx": case ".ppsm": kind = 3; break;
                default: return null;
            }
            cancellation.ThrowIfCancellationRequested();
            maxSize = new Size(Math.Max(1, Math.Min(1600, maxSize.Width)), Math.Max(1, Math.Min(1200, maxSize.Height)));
            try
            {
                PackageData data = CachedPackage(path);
                lock (data)
                using (var package = new Package(data, cancellation))
                {
                    Dictionary<string, Relation> root = package.Relations("");
                    string main = null;
                    var thumbnails = new List<string>();
                    foreach (Relation relation in root.Values)
                    {
                        if (relation.Type.EndsWith("/officeDocument", StringComparison.Ordinal)) main = relation.Target;
                        if (relation.Type.EndsWith("/thumbnail", StringComparison.Ordinal) && !thumbnails.Contains(relation.Target))
                            thumbnails.Add(relation.Target);
                    }
                    foreach (string name in new[] { "docProps/thumbnail.jpeg", "docProps/thumbnail.jpg", "docProps/thumbnail.png" })
                        if (!thumbnails.Contains(name)) thumbnails.Add(name);
                    Bitmap fallback = null;
                    try
                    {
                        // Re-render document content with the selected palette. Saved Office
                        // thumbnails are flattened white pages and cannot preserve photo colors
                        // if inverted. They remain a fallback when content cannot be read.
                        if (kind != 3 && palette != null)
                        {
                            if (main == null) main = kind == 1 ? "word/document.xml" : "xl/workbook.xml";
                            XmlDocument document = package.Xml(main);
                            if (document != null)
                            {
                                DocumentPreviewResult themed = kind == 1 ? RenderWord(package, main, document, maxSize, palette, view)
                                    : RenderSheet(package, main, document, maxSize, palette, view);
                                if (themed != null) return themed;
                            }
                        }
                        foreach (string name in thumbnails)
                        {
                            // PowerPoint styles, masters and graphics must come from a real
                            // rendered preview. Even a uniform saved slide is valid content.
                            Bitmap embedded = kind == 3 ? package.Image(name, maxSize) : Thumbnail(package, name, maxSize);
                            if (embedded == null) continue;
                            if (kind != 3) return new DocumentPreviewResult(embedded, "document_embedded");
                            if (fallback == null || (long)embedded.Width * embedded.Height > (long)fallback.Width * fallback.Height)
                            {
                                if (fallback != null) fallback.Dispose();
                                fallback = embedded;
                            }
                            else embedded.Dispose();
                        }
                        if (kind != 3 && palette == null)
                        {
                            if (main == null) main = kind == 1 ? "word/document.xml" : "xl/workbook.xml";
                            XmlDocument document = package.Xml(main);
                            if (document != null)
                            {
                                if (kind == 1) return RenderWord(package, main, document, maxSize, OfficePreviewPalette.Default);
                                return RenderSheet(package, main, document, maxSize, OfficePreviewPalette.Default);
                            }
                        }
                        // Never replace a styled slide with a partial reconstruction.
                        // A native renderer or Windows thumbnail is tried by the caller.
                        cancellation.ThrowIfCancellationRequested();
                        if (fallback == null) return null;
                        var result = new DocumentPreviewResult(fallback, "document_embedded");
                        fallback = null;
                        return result;
                    }
                    finally { if (fallback != null) fallback.Dispose(); }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch { return null; }
        }

        private sealed class Relation
        {
            internal string Type, Target;
        }

        private static ZipArchive OpenArchive(Stream stream, CancellationToken cancellation)
        {
            if (!DocumentArchiveGuard.IsSafe(stream, cancellation)) throw new InvalidDataException();
            return new ZipArchive(stream, ZipArchiveMode.Read, false);
        }

        private static Bitmap Thumbnail(Package package, string name, Size maximum)
        {
            Bitmap image = package.Image(name, maximum);
            if (image == null) return null;
            // Some document generators carry an empty template thumbnail. Render the content instead.
            Color first = image.GetPixel(0, 0);
            for (int y = 0; y < image.Height; y += Math.Max(1, image.Height / 32))
                for (int x = 0; x < image.Width; x += Math.Max(1, image.Width / 32))
                {
                    Color pixel = image.GetPixel(x, y);
                    if (Math.Abs(pixel.R - first.R) + Math.Abs(pixel.G - first.G) + Math.Abs(pixel.B - first.B) + Math.Abs(pixel.A - first.A) > 12) return image;
                }
            image.Dispose();
            return null;
        }

        private sealed class Package : IDisposable
        {
            private readonly Dictionary<string, ZipArchiveEntry> entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
            internal readonly CancellationToken Cancellation;
            private readonly PackageData data;
            private ZipArchive archive;
            private long remaining = 48L * 1024L * 1024L;

            internal Package(PackageData data, CancellationToken cancellation)
            {
                this.data = data;
                Cancellation = cancellation;
            }

            private void Open()
            {
                if (archive != null) return;
                Cancellation.ThrowIfCancellationRequested();
                var stream = new FileStream(data.Path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                try { archive = OpenArchive(stream, Cancellation); }
                catch { stream.Dispose(); throw; }
                if (archive.Entries.Count > MaximumEntries) throw new InvalidDataException();
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    Cancellation.ThrowIfCancellationRequested();
                    if (entry.FullName.Length > 1024 || entries.ContainsKey(entry.FullName)) throw new InvalidDataException();
                    entries.Add(entry.FullName, entry);
                }
            }

            public void Dispose() { if (archive != null) archive.Dispose(); }

            internal SheetContent CachedSheet(string key)
            { SheetContent sheet; return data.Sheets.TryGetValue(key, out sheet) ? sheet : null; }

            internal void CacheSheet(string key, SheetContent sheet)
            {
                long bytes = sheet.Values.Count * 64L;
                foreach (string value in sheet.Values.Values) bytes += value.Length * 2L;
                if (data.Reserve(bytes)) data.Sheets[key] = sheet;
            }

            internal byte[] Read(string name, int limit)
            {
                Cancellation.ThrowIfCancellationRequested();
                if (String.IsNullOrEmpty(name)) return null;
                byte[] cached;
                if (data.Parts.TryGetValue(name, out cached)) return cached;
                Open();
                ZipArchiveEntry entry;
                if (!entries.TryGetValue(name, out entry)) { data.Parts[name] = null; return null; }
                if (entry.Length < 0 || entry.Length > limit || entry.Length > remaining) throw new InvalidDataException();
                remaining -= entry.Length;
                using (Stream input = entry.Open())
                using (var output = new MemoryStream((int)entry.Length))
                {
                    byte[] buffer = new byte[16384];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        Cancellation.ThrowIfCancellationRequested();
                        if (output.Length + read > entry.Length) throw new InvalidDataException();
                        output.Write(buffer, 0, read);
                    }
                    if (output.Length != entry.Length) throw new InvalidDataException();
                    byte[] result = output.ToArray();
                    if (data.Reserve(result.Length)) data.Parts[name] = result;
                    return result;
                }
            }

            internal XmlDocument Xml(string name)
            {
                Cancellation.ThrowIfCancellationRequested();
                XmlDocument cached;
                if (data.Documents.TryGetValue(name, out cached)) return cached;
                byte[] bytes = Read(name, MaximumXmlBytes);
                if (bytes == null) return null;
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                    MaxCharactersInDocument = MaximumXmlBytes, MaxCharactersFromEntities = 1024, IgnoreComments = true };
                var result = new XmlDocument { XmlResolver = null };
                using (var input = new MemoryStream(bytes, false))
                using (XmlReader reader = XmlReader.Create(input, settings))
                {
                    int nodes = 0;
                    while (reader.Read())
                    {
                        if (++nodes > 100000 || reader.Depth > 128) throw new InvalidDataException();
                        if ((nodes & 255) == 0) Cancellation.ThrowIfCancellationRequested();
                    }
                }
                using (var input = new MemoryStream(bytes, false))
                using (XmlReader reader = XmlReader.Create(input, settings)) result.Load(reader);
                Cancellation.ThrowIfCancellationRequested();
                if (data.Reserve(bytes.Length * 8L)) data.Documents[name] = result;
                return result;
            }

            internal Dictionary<string, Relation> Relations(string source)
            {
                Cancellation.ThrowIfCancellationRequested();
                Dictionary<string, Relation> cached;
                if (data.Relationships.TryGetValue(source, out cached)) return cached;
                int slash = source.LastIndexOf('/');
                string folder = slash < 0 ? "" : source.Substring(0, slash + 1);
                string leaf = slash < 0 ? source : source.Substring(slash + 1);
                XmlDocument document = Xml(folder + "_rels/" + leaf + ".rels");
                var result = new Dictionary<string, Relation>(StringComparer.Ordinal);
                if (document == null) return result;
                foreach (XmlNode node in document.DocumentElement.ChildNodes)
                {
                    if (node.LocalName != "Relationship" || Attr(node, "TargetMode").Equals("External", StringComparison.OrdinalIgnoreCase)) continue;
                    string target = Resolve(source, Attr(node, "Target"));
                    if (target != null) result[Attr(node, "Id")] = new Relation { Type = Attr(node, "Type"), Target = target };
                }
                if (data.Reserve(result.Count * 2048L)) data.Relationships[source] = result;
                return result;
            }

            internal Bitmap Image(string name, Size maximum)
            {
                if (String.IsNullOrEmpty(name)) return null;
                string extension = Path.GetExtension(name).ToLowerInvariant();
                if (extension != ".png" && extension != ".jpg" && extension != ".jpeg" && extension != ".gif" && extension != ".bmp" && extension != ".tif" && extension != ".tiff") return null;
                try
                {
                    byte[] bytes = Read(name, MaximumImageBytes);
                    if (bytes == null || !RasterSignature(bytes)) return null;
                    using (var input = new MemoryStream(bytes, false))
                    using (System.Drawing.Image image = System.Drawing.Image.FromStream(input, false, false))
                    {
                        if (image.Width < 1 || image.Height < 1 || (long)image.Width * image.Height > MaximumImagePixels || image is Metafile) return null;
                        Size size = Fit(image.Width, image.Height, maximum, false);
                        var result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
                        try
                        {
                            using (Graphics graphics = Graphics.FromImage(result))
                            {
                                graphics.Clear(Color.Transparent);
                                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                                graphics.DrawImage(image, new Rectangle(Point.Empty, size));
                            }
                            Cancellation.ThrowIfCancellationRequested();
                            return result;
                        }
                        catch { result.Dispose(); throw; }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch { return null; }
            }
        }

        private static string Resolve(string source, string target)
        {
            if (String.IsNullOrEmpty(target) || target.Length > 1024 || target.IndexOfAny(new[] { '\\', ':', '#', '?' }) >= 0) return null;
            try { target = Uri.UnescapeDataString(target); }
            catch { return null; }
            if (target.IndexOfAny(new[] { '\\', ':', '#', '?', '\0' }) >= 0) return null;
            int slash = source.LastIndexOf('/');
            string joined = target.StartsWith("/", StringComparison.Ordinal) ? target.Substring(1)
                : (slash < 0 ? "" : source.Substring(0, slash + 1)) + target;
            var parts = new List<string>();
            foreach (string part in joined.Split('/'))
            {
                if (part == "." || part == "") continue;
                if (part == "..") { if (parts.Count == 0) return null; parts.RemoveAt(parts.Count - 1); }
                else parts.Add(part);
            }
            return String.Join("/", parts.ToArray());
        }

        private static bool RasterSignature(byte[] bytes)
        {
            if (bytes.Length < 8) return false;
            return (bytes[0] == 137 && bytes[1] == 80 && bytes[2] == 78 && bytes[3] == 71 && bytes[4] == 13 && bytes[5] == 10 && bytes[6] == 26 && bytes[7] == 10)
                || (bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255)
                || (bytes[0] == 'B' && bytes[1] == 'M')
                || (bytes[0] == 'G' && bytes[1] == 'I' && bytes[2] == 'F' && bytes[3] == '8' && (bytes[4] == '7' || bytes[4] == '9') && bytes[5] == 'a')
                || (bytes[0] == 'I' && bytes[1] == 'I' && bytes[2] == 42 && bytes[3] == 0)
                || (bytes[0] == 'M' && bytes[1] == 'M' && bytes[2] == 0 && bytes[3] == 42);
        }

        private static DocumentPreviewResult RenderWord(Package package, string part, XmlDocument document, Size maximum, OfficePreviewPalette palette, OfficeViewOptions view = null)
        {
            XmlNode body = document.SelectSingleNode("/*[local-name()='document']/*[local-name()='body']");
            if (body == null) return null;
            Dictionary<string, Relation> relations = package.Relations(part);
            Size size = new Size(maximum.Width, maximum.Height);
            int contentHeight = size.Height;
            bool truncated = false;
            var image = new Bitmap(size.Width, size.Height);
            try
            {
                using (Graphics g = Graphics.FromImage(image))
                using (var regular = new Font("Segoe UI", Math.Max(10, size.Width / 48F), FontStyle.Regular, GraphicsUnit.Pixel))
                using (var heading = new Font("Segoe UI", regular.Size * 1.55F, FontStyle.Bold, GraphicsUnit.Pixel))
                using (var ink = new SolidBrush(palette.Ink))
                using (var accent = new SolidBrush(palette.Accent))
                using (var border = new Pen(palette.Line))
                using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter })
                {
                    Prepare(g, palette);
                    if (view != null) g.TranslateTransform(0, -view.ScrollY);
                    float margin = Math.Max(12, size.Width / 24F), y = margin;
                    float width = Math.Max(1, size.Width - margin * 2);
                    int count = 0;
                    foreach (XmlNode block in body.ChildNodes)
                    {
                        package.Cancellation.ThrowIfCancellationRequested();
                        if (++count > (view == null ? 300 : 2000) || y > (view == null ? size.Height - regular.Height : 1000000)) { truncated = true; break; }
                        if (block.LocalName == "p")
                        {
                            string text = ParagraphText(block, 8000);
                            XmlNode style = block.SelectSingleNode("./*[local-name()='pPr']/*[local-name()='pStyle']");
                            string styleName = Attr(style, "val");
                            bool isHeading = styleName.IndexOf("heading", StringComparison.OrdinalIgnoreCase) >= 0 || styleName.Equals("Title", StringComparison.OrdinalIgnoreCase);
                            Font font = isHeading ? heading : regular;
                            if (block.SelectSingleNode("./*[local-name()='pPr']/*[local-name()='numPr']") != null) text = "• " + text;
                            if (text.Length > 0)
                            {
                                float height = Math.Min(view == null ? size.Height - margin - y : 1000000 - y,
                                    g.MeasureString(text, font, (int)width, format).Height + 3);
                                if (height <= 0) break;
                                g.DrawString(text, font, isHeading ? accent : ink, new RectangleF(margin, y, width, height), format);
                                y += height + regular.Height * 0.4F;
                            }
                            int pictures = 0;
                            foreach (XmlNode blip in block.SelectNodes(".//*[local-name()='blip']"))
                            {
                                Relation picture;
                                if (++pictures > 4 || (view == null && y >= size.Height - margin)) break;
                                if (!relations.TryGetValue(Attr(blip, "embed"), out picture)) continue;
                                using (Bitmap bitmap = package.Image(picture.Target, new Size((int)width, (int)Math.Max(1, view == null ? size.Height - margin - y : size.Height - margin * 2))))
                                {
                                    if (bitmap == null) continue;
                                    g.DrawImage(bitmap, margin, y, bitmap.Width, bitmap.Height);
                                    y += bitmap.Height + regular.Height * 0.4F;
                                }
                            }
                            if (text.Length == 0 && pictures == 0) y += regular.Height * 0.65F;
                        }
                        else if (block.LocalName == "tbl")
                        {
                            foreach (XmlNode row in block.SelectNodes("./*[local-name()='tr']"))
                            {
                                package.Cancellation.ThrowIfCancellationRequested();
                                XmlNodeList cells = row.SelectNodes("./*[local-name()='tc']");
                                int columns = Math.Min(8, cells.Count);
                                if (columns == 0) continue;
                                float cellWidth = width / columns, rowHeight = regular.Height * 2.2F;
                                if (y + rowHeight > (view == null ? size.Height - margin : 1000000)) break;
                                for (int i = 0; i < columns; i++)
                                {
                                    var box = new RectangleF(margin + i * cellWidth, y, cellWidth, rowHeight);
                                    g.DrawRectangle(border, box.X, box.Y, box.Width, box.Height);
                                    box.Inflate(-5, -3);
                                    g.DrawString(ParagraphText(cells[i], 1000), regular, ink, box, format);
                                }
                                y += rowHeight;
                            }
                            y += regular.Height * 0.6F;
                        }
                    }
                    contentHeight = Math.Max(size.Height, (int)Math.Ceiling(y + margin));
                }
                return new DocumentPreviewResult(image, "document_word") { ContentHeight = contentHeight, DetailKey = truncated ? "office_truncated" : null };
            }
            catch { image.Dispose(); throw; }
        }

        private static DocumentPreviewResult RenderSheet(Package package, string part, XmlDocument workbook, Size maximum, OfficePreviewPalette palette, OfficeViewOptions view = null)
        {
            Dictionary<string, Relation> relations = package.Relations(part);
            XmlNode selected = null;
            Relation sheetRelation = null;
            var visibleSheets = new List<XmlNode>();
            foreach (XmlNode sheet in workbook.SelectNodes("/*[local-name()='workbook']/*[local-name()='sheets']/*[local-name()='sheet']"))
            {
                string state = Attr(sheet, "state");
                if (state == "hidden" || state == "veryHidden") continue;
                Relation candidate;
                if (relations.TryGetValue(RelationId(sheet), out candidate) && candidate.Type.EndsWith("/worksheet", StringComparison.Ordinal))
                    visibleSheets.Add(sheet);
            }
            int selectedIndex = Math.Max(0, Math.Min(visibleSheets.Count - 1, view == null ? 0 : view.SheetIndex));
            if (view != null && !String.IsNullOrEmpty(view.SheetName))
                for (int i = 0; i < visibleSheets.Count; i++)
                    if (Attr(visibleSheets[i], "name") == view.SheetName) { selectedIndex = i; break; }
            if (visibleSheets.Count > 0) { selected = visibleSheets[selectedIndex]; sheetRelation = relations[RelationId(selected)]; }
            if (selected == null) return null;
            XmlDocument worksheet = package.Xml(sheetRelation.Target);
            if (worksheet == null) return null;
            string sheetKey = sheetRelation.Target + (view == null ? "|hover" : "|pinned");
            SheetContent sheetContent = package.CachedSheet(sheetKey);
            if (sheetContent == null)
            {
                var strings = new List<string>();
                var numberFormats = new List<int>();
                foreach (Relation relation in relations.Values)
                {
                    if (relation.Type.EndsWith("/sharedStrings", StringComparison.Ordinal))
                    {
                        XmlDocument shared = package.Xml(relation.Target);
                        if (shared != null) foreach (XmlNode item in shared.SelectNodes("/*[local-name()='sst']/*[local-name()='si']"))
                        {
                            package.Cancellation.ThrowIfCancellationRequested();
                            if (strings.Count >= 50000) break;
                            strings.Add(ParagraphText(item, 2000));
                        }
                    }
                    if (relation.Type.EndsWith("/styles", StringComparison.Ordinal))
                    {
                        XmlDocument styles = package.Xml(relation.Target);
                        if (styles != null) foreach (XmlNode xf in styles.SelectNodes("/*[local-name()='styleSheet']/*[local-name()='cellXfs']/*[local-name()='xf']"))
                        { if (numberFormats.Count >= 10000) break; numberFormats.Add(Int(Attr(xf, "numFmtId"), 0)); }
                    }
                }
                bool date1904 = Attr(workbook.SelectSingleNode("/*[local-name()='workbook']/*[local-name()='workbookPr']"), "date1904") == "1";
                var values = new Dictionary<int, string>();
                int highestColumn = 0, highestRow = 0, scanned = 0;
                bool truncated = false;
                foreach (XmlNode row in worksheet.SelectNodes("/*[local-name()='worksheet']/*[local-name()='sheetData']/*[local-name()='row']"))
                {
                    package.Cancellation.ThrowIfCancellationRequested();
                    int rowIndex = Int(Attr(row, "r"), highestRow + 1);
                    if (rowIndex > (view == null ? 30 : 5000)) { truncated = true; continue; }
                    if (rowIndex < 1 || Attr(row, "hidden") == "1") continue;
                    int nextColumn = 0;
                    foreach (XmlNode cell in row.ChildNodes)
                    {
                        if (cell.LocalName != "c") continue;
                        if (++scanned > (view == null ? 10000 : 100000)) { truncated = true; break; }
                        int column = ColumnIndex(Attr(cell, "r"));
                        if (column < 0) column = nextColumn;
                        nextColumn = column + 1;
                        if (column >= (view == null ? 8 : 16384)) { truncated = true; continue; }
                        string type = Attr(cell, "t");
                        string value = TextOf(cell.SelectSingleNode("./*[local-name()='v']"));
                        if (type == "s")
                        {
                            int index = Int(value, -1);
                            value = index >= 0 && index < strings.Count ? strings[index] : "";
                        }
                        else if (type == "inlineStr") value = ParagraphText(cell.SelectSingleNode("./*[local-name()='is']"), 2000);
                        else if (type == "b") value = value == "1" ? "TRUE" : "FALSE";
                        else if (type == "" || type == "n")
                        {
                            int style = Int(Attr(cell, "s"), 0);
                            value = FormatNumber(value, style >= 0 && style < numberFormats.Count ? numberFormats[style] : 0, date1904);
                        }
                        if (value.Length == 0)
                        {
                            string formula = TextOf(cell.SelectSingleNode("./*[local-name()='f']"));
                            if (formula.Length > 0) value = "=" + formula;
                        }
                        if (value.Length > 2000) truncated = true;
                        values[(rowIndex - 1) * 16384 + column] = Limit(value, 2000);
                        highestColumn = Math.Max(highestColumn, column + 1);
                        highestRow = Math.Max(highestRow, rowIndex);
                    }
                    if (scanned > (view == null ? 10000 : 100000)) break;
                }
                sheetContent = new SheetContent { Values = values, HighestColumn = highestColumn, HighestRow = highestRow, Truncated = truncated };
                package.CacheSheet(sheetKey, sheetContent);
            }
            var displayedValues = sheetContent.Values;
            int columnCount = sheetContent.HighestColumn, rowCount = sheetContent.HighestRow;
            bool contentTruncated = sheetContent.Truncated;
            int contentHeight = maximum.Height, contentWidth = maximum.Width;
            var image = new Bitmap(maximum.Width, maximum.Height);
            try
            {
                using (Graphics g = Graphics.FromImage(image))
                using (var font = new Font("Segoe UI", Math.Max(10, maximum.Width / 56F), GraphicsUnit.Pixel))
                using (var bold = new Font(font, FontStyle.Bold))
                using (var ink = new SolidBrush(palette.Ink))
                using (var muted = new SolidBrush(palette.Muted))
                using (var header = new SolidBrush(palette.Header))
                using (var tab = new SolidBrush(palette.Tab))
                using (var pen = new Pen(palette.Line))
                using (var format = new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap, LineAlignment = StringAlignment.Center })
                {
                    Prepare(g, palette);
                    float unit = Math.Max(1, maximum.Width / 720F);
                    float rowHeight = Math.Max(24 * unit, font.Height + 10 * unit), gutter = 36 * unit, top = rowHeight;
                    int columns = Math.Max(3, Math.Min(view == null ? 8 : 16384, columnCount));
                    float[] widths = new float[columns], positions = new float[columns + 1];
                    for (int col = 0; col < columns; col++) widths[col] = 8.43f;
                    foreach (XmlNode col in worksheet.SelectNodes("/*[local-name()='worksheet']/*[local-name()='cols']/*[local-name()='col']"))
                    {
                        double width;
                        if (!Double.TryParse(Attr(col, "width"), NumberStyles.Float, CultureInfo.InvariantCulture, out width) || Double.IsNaN(width) || Double.IsInfinity(width)) continue;
                        for (int i = Math.Max(0, Int(Attr(col, "min"), 1) - 1); i < Math.Min(columns, Int(Attr(col, "max"), columns)); i++)
                            widths[i] = Attr(col, "hidden") == "1" ? 0 : (float)Math.Max(1, Math.Min(100, width));
                    }
                    float firstWidth = 0, totalWidth = 0;
                    for (int col = 0; col < columns; col++) { totalWidth += widths[col]; if (col < 8) firstWidth += widths[col]; }
                    float widthScale = (maximum.Width - gutter) / (firstWidth > 0 ? firstWidth : 8 * 8.43f);
                    contentWidth = Math.Max(maximum.Width, (int)Math.Ceiling(gutter + totalWidth * widthScale));
                    int scrollX = view == null ? 0 : Math.Max(0, Math.Min(view.ScrollX, contentWidth - maximum.Width));
                    positions[0] = gutter - scrollX;
                    for (int col = 0; col < columns; col++) { widths[col] *= widthScale; positions[col + 1] = positions[col] + widths[col]; }
                    g.FillRectangle(tab, 0, 0, image.Width, top);
                    using (var title = new SolidBrush(palette.TabText))
                        g.DrawString(Limit(Attr(selected, "name"), 100), bold, title, new RectangleF(12, 0, image.Width - 24, top), format);
                    g.FillRectangle(header, 0, top, image.Width, rowHeight);
                    int rows = Math.Max(0, Math.Min(view == null ? 30 : 5000, (int)Math.Ceiling((image.Height - top - rowHeight) / rowHeight)));
                    int firstRow = view == null ? 0 : Math.Max(0, Math.Min(rowCount - rows, (int)(view.ScrollY / rowHeight)));
                    contentHeight = Math.Max(maximum.Height, (int)Math.Ceiling(top + (rowCount + 1) * rowHeight));
                    g.FillRectangle(header, 0, top, gutter, image.Height - top);
                    GraphicsState gridState = g.Save();
                    g.SetClip(new RectangleF(gutter, top, image.Width - gutter, image.Height - top));
                    for (int col = 0; col < columns; col++)
                    {
                        if (widths[col] < 1 || positions[col + 1] < gutter || positions[col] > image.Width) continue;
                        var box = new RectangleF(positions[col], top, widths[col], rowHeight);
                        using (var centered = (StringFormat)format.Clone())
                        { centered.Alignment = StringAlignment.Center; g.DrawString(ColumnName(col), bold, muted, box, centered); }
                    }
                    for (int row = 0; row < rows; row++)
                    {
                        package.Cancellation.ThrowIfCancellationRequested();
                        float y = top + (row + 1) * rowHeight;
                        for (int col = 0; col < columns; col++)
                        {
                            string value;
                            if (widths[col] > 12 && positions[col + 1] >= gutter && positions[col] <= image.Width &&
                                displayedValues.TryGetValue((firstRow + row) * 16384 + col, out value))
                                g.DrawString(value, font, ink, new RectangleF(positions[col] + 6, y, widths[col] - 12, rowHeight), format);
                        }
                    }
                    for (int row = 0; row <= rows + 1; row++) g.DrawLine(pen, 0, top + row * rowHeight, image.Width, top + row * rowHeight);
                    for (int col = 0; col <= columns; col++)
                        if (positions[col] >= gutter && positions[col] <= image.Width) g.DrawLine(pen, positions[col], top, positions[col], image.Height);
                    g.Restore(gridState);
                    for (int row = 0; row < rows; row++)
                        g.DrawString((firstRow + row + 1).ToString(CultureInfo.InvariantCulture), font, muted,
                            new RectangleF(5, top + (row + 1) * rowHeight, gutter - 8, rowHeight), format);
                    for (int row = 0; row <= rows + 1; row++) g.DrawLine(pen, 0, top + row * rowHeight, gutter, top + row * rowHeight);
                    g.DrawLine(pen, gutter, top, gutter, image.Height);
                }
                var names = new string[visibleSheets.Count];
                for (int i = 0; i < names.Length; i++) names[i] = Attr(visibleSheets[i], "name");
                return new DocumentPreviewResult(image, "document_sheet") { SheetNames = names, SheetIndex = selectedIndex,
                    ContentHeight = contentHeight, ContentWidth = contentWidth, DetailKey = contentTruncated ? "office_truncated" : null };
            }
            catch { image.Dispose(); throw; }
        }

        private static string FormatNumber(string value, int format, bool date1904)
        {
            double number;
            if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) || Double.IsNaN(number) || Double.IsInfinity(number)) return value;
            if (format >= 14 && format <= 22 && number >= 0 && number < 2950000)
            {
                try
                {
                    DateTime date = DateTime.FromOADate(number + (date1904 ? 1462 : number < 60 ? 1 : 0));
                    return date.ToString(format >= 18 && format <= 21 ? "HH:mm" : format == 22 ? "yyyy-MM-dd HH:mm" : "yyyy-MM-dd", CultureInfo.InvariantCulture);
                }
                catch (ArgumentException) { return value; }
            }
            if (format == 9 || format == 10) return number.ToString(format == 9 ? "P0" : "P2", CultureInfo.InvariantCulture);
            if (format == 1 || format == 2) return number.ToString(format == 1 ? "0" : "0.00", CultureInfo.InvariantCulture);
            if (format == 3 || format == 4) return number.ToString(format == 3 ? "#,##0" : "#,##0.00", CultureInfo.InvariantCulture);
            return value;
        }

        private static int ColumnIndex(string reference)
        {
            int column = 0, count = 0;
            foreach (char character in reference)
            {
                if (character < 'A' || character > 'Z') break;
                if (++count > 3) return 16384;
                column = column * 26 + character - 'A' + 1;
            }
            return column - 1;
        }

        private static string ColumnName(int index)
        {
            string result = "";
            do { result = (char)('A' + index % 26) + result; index = index / 26 - 1; } while (index >= 0);
            return result;
        }

        private static string ParagraphText(XmlNode node, int limit)
        {
            if (node == null) return "";
            var builder = new StringBuilder();
            foreach (XmlNode fragment in node.SelectNodes(".//*[local-name()='t' or local-name()='tab' or local-name()='br']"))
            {
                string text = fragment.LocalName == "tab" ? "    " : fragment.LocalName == "br" ? "\n" : fragment.InnerText;
                builder.Append(text, 0, Math.Min(text.Length, limit - builder.Length));
                if (builder.Length >= limit) break;
            }
            return builder.ToString();
        }

        private static string RelationId(XmlNode node)
        {
            if (node == null || node.Attributes == null) return "";
            foreach (XmlAttribute attribute in node.Attributes)
                if (attribute.LocalName == "id" && attribute.NamespaceURI.EndsWith("/relationships", StringComparison.Ordinal)) return attribute.Value;
            return "";
        }

        private static string Attr(XmlNode node, string name)
        {
            if (node == null || node.Attributes == null) return "";
            foreach (XmlAttribute attribute in node.Attributes) if (attribute.LocalName == name) return attribute.Value;
            return "";
        }

        private static string TextOf(XmlNode node) { return node == null ? "" : node.InnerText; }
        private static string Limit(string value, int limit) { return value.Length <= limit ? value : value.Substring(0, limit - 1) + "…"; }
        private static int Int(string text, int fallback) { int value; return Int32.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback; }
        private static Size Fit(double width, double height, Size maximum, bool upscale)
        {
            double scale = Math.Min(maximum.Width / width, maximum.Height / height);
            if (!upscale) scale = Math.Min(1, scale);
            return new Size(Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
        }
        private static void Prepare(Graphics graphics, OfficePreviewPalette palette)
        {
            graphics.Clear(palette.Paper);
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        }
    }
}
