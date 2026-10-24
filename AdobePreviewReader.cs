using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;

namespace DesktopHover
{
    // Photoshop layout: https://www.adobe.com/devnet-apps/photoshop/fileformatashtml/
    // XMP thumbnails: https://developer.adobe.com/xmp/docs/xmp-namespaces/xmp-data-types/thumbnails/
    internal static class AdobePreviewReader
    {
        private const int MaximumEncodedImageBytes = 8 * 1024 * 1024;
        private const int MaximumScanBytes = 16 * 1024 * 1024;
        private const long MaximumImagePixels = 40000000;
        private const string ThumbnailNamespace = "http://ns.adobe.com/xap/1.0/g/img/";

        internal static DocumentPreviewResult TryLoad(string path, Size maxSize, CancellationToken cancellation)
        {
            try { return Load(path, maxSize, cancellation); }
            catch (OperationCanceledException) { throw; }
            catch (InvalidDataException) { return null; }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }
            catch (XmlException) { return null; }
            catch (ExternalException) { return null; }
        }

        private static DocumentPreviewResult Load(string path, Size maxSize, CancellationToken cancellation)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            switch (extension)
            {
                case ".psd": case ".psb":
                    using (var stream = Open(path)) return ReadPhotoshop(stream, maxSize, cancellation);
                case ".ai": case ".eps": case ".indd": case ".indt": case ".idml":
                case ".xd": case ".aep": case ".aepx": case ".prproj": case ".fla":
                case ".odt": case ".ods": case ".odp":
                    break;
                default: return null;
            }
            cancellation.ThrowIfCancellationRequested();
            using (var stream = Open(path))
            {
                if (stream.Length >= 4 && ReadU32(stream) == 0x504b0304)
                {
                    stream.Position = 0;
                    DocumentPreviewResult zipped = ReadZip(stream, maxSize, cancellation);
                    if (zipped != null) return zipped;
                }
                stream.Position = 0;
                Bitmap image = ReadXmp(stream, maxSize, cancellation);
                return image == null ? null : new DocumentPreviewResult(image, "document_embedded");
            }
        }

        private static FileStream Open(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        }

        private static DocumentPreviewResult ReadPhotoshop(Stream stream, Size maxSize, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (ReadU32(stream) != 0x38425053) return null;
            int version = ReadU16(stream);
            if (version != 1 && version != 2) return null;
            Skip(stream, 6);
            int channels = ReadU16(stream);
            uint heightValue = ReadU32(stream), widthValue = ReadU32(stream);
            int depth = ReadU16(stream), mode = ReadU16(stream);
            if (channels < 1 || channels > 56 || widthValue < 1 || heightValue < 1 ||
                widthValue > (version == 1 ? 30000 : 300000) || heightValue > (version == 1 ? 30000 : 300000)) return null;
            int width = (int)widthValue, height = (int)heightValue;
            Skip(stream, ReadU32(stream)); // Color-mode data.
            long resourcesEnd = End(stream, ReadU32(stream));
            int resourceCount = 0;
            while (stream.Position < resourcesEnd)
            {
                cancellation.ThrowIfCancellationRequested();
                if (++resourceCount > 100000 || resourcesEnd - stream.Position < 12) return null;
                if (ReadU32(stream) != 0x3842494d) return null; // 8BIM
                int id = ReadU16(stream);
                int nameLength = ReadByte(stream);
                Skip(stream, nameLength + ((nameLength + 1) % 2));
                long dataLength = ReadU32(stream);
                long dataEnd = End(stream, dataLength);
                long paddedEnd = dataEnd + (dataLength % 2);
                if (paddedEnd > resourcesEnd) return null;
                if ((id == 1033 || id == 1036) && dataLength >= 28 && dataLength <= MaximumEncodedImageBytes + 28)
                {
                    Bitmap thumbnail = null;
                    try { thumbnail = ReadThumbnail(stream, dataEnd, id == 1033, maxSize, cancellation); }
                    catch (OperationCanceledException) { throw; }
                    catch (ArgumentException) { }
                    catch (ExternalException) { }
                    catch (InvalidDataException) { }
                    catch (EndOfStreamException) { }
                    if (thumbnail != null) return new DocumentPreviewResult(thumbnail, "document_embedded");
                }
                stream.Position = paddedEnd;
            }
            if (stream.Position != resourcesEnd) return null;
            long layerLength = version == 1 ? ReadU32(stream) : ReadU64(stream);
            long layerEnd = End(stream, layerLength);
            bool hasMergedAlpha = false;
            // A negative layer count means the first extra composite channel is transparency.
            int layerLengthBytes = version == 1 ? 4 : 8;
            if (layerLength >= layerLengthBytes + 2)
            {
                long layerInfoLength = version == 1 ? ReadU32(stream) : ReadU64(stream);
                if (layerInfoLength >= 2 && layerInfoLength <= layerEnd - stream.Position)
                    hasMergedAlpha = unchecked((short)ReadU16(stream)) < 0;
            }
            stream.Position = layerEnd;
            if (depth != 8 || (mode != 1 && mode != 3) || (long)width * height > MaximumImagePixels) return null;
            int colorChannels = mode == 1 ? 1 : 3;
            if (channels < colorChannels) return null;
            int decodeChannels = colorChannels + (hasMergedAlpha && channels > colorChannels ? 1 : 0);
            int compression = ReadU16(stream);
            if (compression != 0 && compression != 1) return null;
            Size target = Fit(width, height, maxSize);
            byte[] pixels = new byte[target.Width * target.Height * 4];
            for (int i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            int[] lengths = null;
            if (compression == 1)
            {
                long countBytes = (long)channels * height * (version == 1 ? 2 : 4);
                long rowsStart = End(stream, countBytes);
                if ((long)decodeChannels * height > 2000000) return null;
                lengths = new int[decodeChannels * height];
                for (int i = 0; i < lengths.Length; i++)
                {
                    if ((i & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                    uint rowBytes = version == 1 ? (uint)ReadU16(stream) : ReadU32(stream);
                    if (rowBytes > width * 2L + 4096) return null;
                    lengths[i] = (int)rowBytes;
                }
                stream.Position = rowsStart;
            }
            else if ((long)decodeChannels * width * height > stream.Length - stream.Position) return null;
            byte[] row = new byte[width];
            for (int channel = 0; channel < decodeChannels; channel++)
            {
                int targetY = 0;
                for (int y = 0; y < height; y++)
                {
                    if ((y & 63) == 0) cancellation.ThrowIfCancellationRequested();
                    int count = compression == 0 ? width : lengths[channel * height + y];
                    bool sample = targetY < target.Height && y == (long)targetY * height / target.Height;
                    if (!sample) { Skip(stream, count); continue; }
                    if (compression == 0) ReadExactly(stream, row, 0, width, cancellation);
                    else DecodePackBits(ReadBytes(stream, count, cancellation), row);
                    int offset = targetY * target.Width * 4;
                    for (int x = 0; x < target.Width; x++)
                    {
                        byte value = row[(int)((long)x * width / target.Width)];
                        if (channel == colorChannels) pixels[offset + x * 4 + 3] = value;
                        else if (mode == 1)
                        {
                            pixels[offset + x * 4] = value;
                            pixels[offset + x * 4 + 1] = value;
                            pixels[offset + x * 4 + 2] = value;
                        }
                        else pixels[offset + x * 4 + 2 - channel] = value;
                    }
                    targetY++;
                }
            }
            cancellation.ThrowIfCancellationRequested();
            return new DocumentPreviewResult(FromPixels(pixels, target.Width, target.Height), "document_composite");
        }

        private static Bitmap ReadThumbnail(Stream stream, long end, bool bgr, Size maxSize, CancellationToken cancellation)
        {
            uint format = ReadU32(stream), w = ReadU32(stream), h = ReadU32(stream);
            uint widthBytes = ReadU32(stream), totalBytes = ReadU32(stream), compressedBytes = ReadU32(stream);
            int bits = ReadU16(stream), planes = ReadU16(stream);
            if (w < 1 || h < 1 || w > 10000 || h > 10000 || (long)w * h > MaximumImagePixels ||
                bits != 24 || planes != 1 || compressedBytes > end - stream.Position ||
                compressedBytes > MaximumEncodedImageBytes) return null;
            if (format == 1)
            {
                Bitmap image = DecodeImage(ReadBytes(stream, (int)compressedBytes, cancellation), maxSize, cancellation);
                if (image != null && bgr) SwapRedBlue(image);
                return image;
            }
            if (format != 0 || widthBytes != ((w * 24 + 31) / 32) * 4 ||
                (long)widthBytes * h != totalBytes || totalBytes > end - stream.Position || totalBytes > MaximumEncodedImageBytes) return null;
            Size target = Fit((int)w, (int)h, maxSize);
            byte[] pixels = new byte[target.Width * target.Height * 4];
            byte[] row = new byte[widthBytes];
            int targetY = 0;
            for (int y = 0; y < h; y++)
            {
                cancellation.ThrowIfCancellationRequested();
                if (targetY >= target.Height || y != (long)targetY * h / target.Height) { Skip(stream, widthBytes); continue; }
                ReadExactly(stream, row, 0, row.Length, cancellation);
                for (int x = 0; x < target.Width; x++)
                {
                    int source = (int)((long)x * w / target.Width) * 3;
                    int destination = (targetY * target.Width + x) * 4;
                    pixels[destination] = row[source + (bgr ? 0 : 2)];
                    pixels[destination + 1] = row[source + 1];
                    pixels[destination + 2] = row[source + (bgr ? 2 : 0)];
                    pixels[destination + 3] = 255;
                }
                targetY++;
            }
            return FromPixels(pixels, target.Width, target.Height);
        }

        private static void DecodePackBits(byte[] encoded, byte[] row)
        {
            int input = 0, output = 0;
            while (input < encoded.Length)
            {
                int command = unchecked((sbyte)encoded[input++]);
                if (command == -128) continue;
                int count = command >= 0 ? command + 1 : 1 - command;
                if (count > row.Length - output) throw new InvalidDataException("Invalid Photoshop row length.");
                if (command >= 0)
                {
                    if (count > encoded.Length - input) throw new InvalidDataException("Truncated Photoshop row.");
                    Buffer.BlockCopy(encoded, input, row, output, count);
                    input += count;
                    output += count;
                }
                else
                {
                    if (input >= encoded.Length) throw new InvalidDataException("Truncated Photoshop run.");
                    byte value = encoded[input++];
                    for (int i = 0; i < count; i++) row[output++] = value;
                }
            }
            if (output != row.Length) throw new InvalidDataException("Incomplete Photoshop row.");
        }

        private static DocumentPreviewResult ReadZip(Stream stream, Size maxSize, CancellationToken cancellation)
        {
            if (!DocumentArchiveGuard.IsSafe(stream, cancellation)) return null;
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, true))
            {
                if (archive.Entries.Count > 4096) return null;
                // Try document previews before any other archive assets; never show an arbitrary media item.
                string[] names = { "Thumbnails/thumbnail.png", "preview.png", "preview.jpg", "thumbnail.png", "thumbnail.jpg", "META-INF/thumbnail.png", "designmap.xml" };
                foreach (string name in names)
                {
                    cancellation.ThrowIfCancellationRequested();
                    foreach (ZipArchiveEntry entry in archive.Entries)
                    {
                        if (!String.Equals(entry.FullName, name, StringComparison.OrdinalIgnoreCase)) continue;
                        if (entry.Length < 1 || entry.Length > MaximumEncodedImageBytes) continue;
                        byte[] bytes;
                        using (Stream content = entry.Open()) bytes = ReadBounded(content, MaximumEncodedImageBytes, cancellation);
                        Bitmap image;
                        if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                            image = ExtractXmp(Encoding.UTF8.GetString(bytes), maxSize, cancellation);
                        else image = DecodeImage(bytes, maxSize, cancellation);
                        if (image != null) return new DocumentPreviewResult(image, "document_embedded");
                    }
                }
            }
            return null;
        }

        private static Bitmap ReadXmp(Stream stream, Size maxSize, CancellationToken cancellation)
        {
            // XMP can appear near either end of a project; do not scan a whole large file on hover.
            int head = (int)Math.Min(MaximumScanBytes, stream.Length);
            byte[] bytes = ReadBytes(stream, head, cancellation);
            Bitmap image = ExtractXmp(Encoding.UTF8.GetString(bytes), maxSize, cancellation);
            if (image != null || stream.Length <= head) return image;
            stream.Position = Math.Max(head, stream.Length - MaximumScanBytes);
            bytes = ReadBytes(stream, (int)(stream.Length - stream.Position), cancellation);
            return ExtractXmp(Encoding.UTF8.GetString(bytes), maxSize, cancellation);
        }

        private static Bitmap ExtractXmp(string data, Size maxSize, CancellationToken cancellation)
        {
            int search = 0;
            for (int attempt = 0; attempt < 16; attempt++)
            {
                cancellation.ThrowIfCancellationRequested();
                int start = data.IndexOf("<x:xmpmeta", search, StringComparison.Ordinal);
                string closing = "</x:xmpmeta>";
                if (start < 0)
                {
                    start = data.IndexOf("<xmpmeta", search, StringComparison.Ordinal);
                    closing = "</xmpmeta>";
                }
                if (start < 0) return null;
                int openEnd = data.IndexOf('>', start);
                if (openEnd < 0) return null;
                int end;
                if (data[openEnd - 1] == '/') end = openEnd + 1;
                else
                {
                    end = data.IndexOf(closing, openEnd, StringComparison.Ordinal);
                    if (end < 0) return null;
                    end += closing.Length;
                }
                search = end;
                if (end - start > MaximumEncodedImageBytes * 2) continue;
                try
                {
                    var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumEncodedImageBytes * 2L };
                    using (var text = new StringReader(data.Substring(start, end - start)))
                    using (var reader = XmlReader.Create(text, settings))
                    {
                        while (reader.Read())
                        {
                            cancellation.ThrowIfCancellationRequested();
                            if (reader.NodeType != XmlNodeType.Element) continue;
                            string encoded = reader.GetAttribute("image", ThumbnailNamespace);
                            if (encoded == null && reader.LocalName == "image" && reader.NamespaceURI == ThumbnailNamespace)
                                encoded = reader.ReadElementContentAsString();
                            if (encoded == null || encoded.Length > MaximumEncodedImageBytes * 2) continue;
                            byte[] bytes = Convert.FromBase64String(encoded);
                            if (bytes.Length > MaximumEncodedImageBytes) continue;
                            Bitmap image = DecodeImage(bytes, maxSize, cancellation);
                            if (image != null) return image;
                        }
                    }
                }
                catch (XmlException) { }
                catch (FormatException) { }
                catch (ArgumentException) { }
            }
            return null;
        }

        private static Bitmap DecodeImage(byte[] bytes, Size maxSize, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            if (bytes.Length < 4 || bytes.Length > MaximumEncodedImageBytes) return null;
            // Validate raster dimensions before the decoder can allocate a pixel buffer.
            // Metafiles and other embedded executable drawing formats are not accepted.
            if (!SafeRasterHeader(bytes)) return null;
            using (var stream = new MemoryStream(bytes, false))
            using (Image source = Image.FromStream(stream, false, false))
            {
                if (source.Width < 1 || source.Height < 1 || (long)source.Width * source.Height > MaximumImagePixels) return null;
                Size target = Fit(source.Width, source.Height, maxSize);
                Bitmap result = new Bitmap(target.Width, target.Height, PixelFormat.Format32bppArgb);
                try
                {
                    using (Graphics graphics = Graphics.FromImage(result))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.DrawImage(source, new Rectangle(Point.Empty, target));
                    }
                    cancellation.ThrowIfCancellationRequested();
                    return result;
                }
                catch { result.Dispose(); throw; }
            }
        }

        private static bool SafeRasterHeader(byte[] bytes)
        {
            if (bytes.Length >= 24 && bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4e && bytes[3] == 0x47)
            {
                if (bytes[4] != 13 || bytes[5] != 10 || bytes[6] != 26 || bytes[7] != 10 ||
                    bytes[12] != 73 || bytes[13] != 72 || bytes[14] != 68 || bytes[15] != 82) return false;
                long width = ((long)bytes[16] << 24) | ((long)bytes[17] << 16) | ((long)bytes[18] << 8) | bytes[19];
                long height = ((long)bytes[20] << 24) | ((long)bytes[21] << 16) | ((long)bytes[22] << 8) | bytes[23];
                return width > 0 && height > 0 && width <= 300000 && height <= 300000 && width * height <= MaximumImagePixels;
            }
            if (bytes.Length < 4 || bytes[0] != 0xff || bytes[1] != 0xd8) return false;
            int position = 2;
            while (position < bytes.Length)
            {
                if (bytes[position++] != 0xff) return false;
                while (position < bytes.Length && bytes[position] == 0xff) position++;
                if (position >= bytes.Length) return false;
                int marker = bytes[position++];
                if (marker == 0xd9 || marker == 0xda) return false;
                if (marker == 0x01 || (marker >= 0xd0 && marker <= 0xd7)) continue;
                if (bytes.Length - position < 2) return false;
                int length = (bytes[position] << 8) | bytes[position + 1];
                if (length < 2 || length > bytes.Length - position) return false;
                bool frame = marker >= 0xc0 && marker <= 0xcf && marker != 0xc4 && marker != 0xc8 && marker != 0xcc;
                if (frame)
                {
                    if (length < 8) return false;
                    int height = (bytes[position + 3] << 8) | bytes[position + 4];
                    int width = (bytes[position + 5] << 8) | bytes[position + 6];
                    return width > 0 && height > 0 && (long)width * height <= MaximumImagePixels;
                }
                position += length;
            }
            return false;
        }

        private static Size Fit(int width, int height, Size maximum)
        {
            double scale = Math.Min(1, Math.Min((double)Math.Max(1, Math.Min(1600, maximum.Width)) / width,
                (double)Math.Max(1, Math.Min(1200, maximum.Height)) / height));
            return new Size(Math.Max(1, (int)(width * scale)), Math.Max(1, (int)(height * scale)));
        }

        private static Bitmap FromPixels(byte[] pixels, int width, int height)
        {
            Bitmap image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            try
            {
                BitmapData data = image.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                try { for (int y = 0; y < height; y++) Marshal.Copy(pixels, y * width * 4, IntPtr.Add(data.Scan0, y * data.Stride), width * 4); }
                finally { image.UnlockBits(data); }
                return image;
            }
            catch { image.Dispose(); throw; }
        }

        private static void SwapRedBlue(Bitmap image)
        {
            BitmapData data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
            try
            {
                byte[] row = new byte[image.Width * 4];
                for (int y = 0; y < image.Height; y++)
                {
                    IntPtr address = IntPtr.Add(data.Scan0, y * data.Stride);
                    Marshal.Copy(address, row, 0, row.Length);
                    for (int x = 0; x < row.Length; x += 4) { byte red = row[x + 2]; row[x + 2] = row[x]; row[x] = red; }
                    Marshal.Copy(row, 0, address, row.Length);
                }
            }
            finally { image.UnlockBits(data); }
        }

        private static int ReadByte(Stream stream)
        {
            int value = stream.ReadByte();
            if (value < 0) throw new EndOfStreamException();
            return value;
        }

        private static int ReadU16(Stream stream) { return (ReadByte(stream) << 8) | ReadByte(stream); }
        private static uint ReadU32(Stream stream) { return ((uint)ReadU16(stream) << 16) | (uint)ReadU16(stream); }
        private static long ReadU64(Stream stream)
        {
            uint high = ReadU32(stream), low = ReadU32(stream);
            if (high > 0x7fffffff) throw new InvalidDataException("Invalid section length.");
            return ((long)high << 32) | low;
        }

        private static long End(Stream stream, long count)
        {
            if (count < 0 || count > stream.Length - stream.Position) throw new EndOfStreamException();
            return stream.Position + count;
        }

        private static void Skip(Stream stream, long count) { stream.Position = End(stream, count); }

        private static byte[] ReadBytes(Stream stream, int count, CancellationToken cancellation)
        {
            if (count < 0 || count > MaximumScanBytes) throw new InvalidDataException("Preview data is too large.");
            byte[] bytes = new byte[count];
            ReadExactly(stream, bytes, 0, count, cancellation);
            return bytes;
        }

        private static void ReadExactly(Stream stream, byte[] buffer, int offset, int count, CancellationToken cancellation)
        {
            while (count > 0)
            {
                cancellation.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, offset, Math.Min(count, 65536));
                if (read == 0) throw new EndOfStreamException();
                offset += read;
                count -= read;
            }
        }

        private static byte[] ReadBounded(Stream stream, int maximum, CancellationToken cancellation)
        {
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[65536];
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (output.Length + read > maximum) throw new InvalidDataException("Preview data is too large.");
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
        }
    }
}
