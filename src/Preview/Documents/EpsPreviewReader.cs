using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace DesktopHover
{
    // Adobe EPSF 3.0, section 5.2: the DOS header points to a TIFF screen preview.
    // Decode that saved raster only; PostScript and WMF content are never executed.
    internal static class EpsPreviewReader
    {
        private const int MaximumPreviewBytes = 8 * 1024 * 1024;
        private const long MaximumPixels = 40000000;

        internal static Bitmap TryLoad(Stream stream, Size maximum, CancellationToken cancellation)
        {
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (stream.Length < 30) return null;
                stream.Position = 0;
                byte[] header = new byte[30];
                ReadExactly(stream, header, cancellation);
                if (header[0] != 0xc5 || header[1] != 0xd0 || header[2] != 0xd3 || header[3] != 0xc6) return null;
                long offset = UInt32(header, 20, true), length = UInt32(header, 24, true);
                if (offset < header.Length || length < 8 || length > MaximumPreviewBytes || offset > stream.Length ||
                    length > stream.Length - offset) return null;
                stream.Position = offset;
                byte[] tiff = new byte[(int)length];
                ReadExactly(stream, tiff, cancellation);
                if (!SafeTiffDimensions(tiff)) return null;
                using (var input = new MemoryStream(tiff, false))
                using (Image source = Image.FromStream(input, false, false))
                {
                    if (source.Width < 1 || source.Height < 1 || (long)source.Width * source.Height > MaximumPixels) return null;
                    double fit = Math.Min(1, Math.Min((double)Math.Max(1, Math.Min(1600, maximum.Width)) / source.Width,
                        (double)Math.Max(1, Math.Min(1200, maximum.Height)) / source.Height));
                    Size size = new Size(Math.Max(1, (int)(source.Width * fit)), Math.Max(1, (int)(source.Height * fit)));
                    Bitmap result = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
                    try
                    {
                        using (Graphics graphics = Graphics.FromImage(result))
                        {
                            graphics.CompositingMode = CompositingMode.SourceCopy;
                            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            graphics.DrawImage(source, new Rectangle(Point.Empty, size));
                        }
                        cancellation.ThrowIfCancellationRequested();
                        return result;
                    }
                    catch { result.Dispose(); throw; }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (InvalidDataException) { return null; }
            catch (IOException) { return null; }
            catch (ArgumentException) { return null; }
            catch (ExternalException) { return null; }
        }

        private static bool SafeTiffDimensions(byte[] bytes)
        {
            if (bytes.Length < 8) return false;
            bool little = bytes[0] == 'I' && bytes[1] == 'I';
            if (!little && !(bytes[0] == 'M' && bytes[1] == 'M')) return false;
            if (UInt16(bytes, 2, little) != 42) return false; // Classic TIFF, not BigTIFF.
            uint directory = UInt32(bytes, 4, little);
            if (directory < 8 || directory > bytes.Length - 2) return false;
            int count = UInt16(bytes, (int)directory, little);
            long end = (long)directory + 2 + 12L * count;
            if (count < 2 || count > 256 || end > bytes.Length - 4) return false;
            // EPS screen previews are single images. Do not let the decoder walk an
            // unchecked chain of additional frames or subordinate image directories.
            if (UInt32(bytes, (int)end, little) != 0) return false;
            long width = 0, height = 0;
            for (int index = 0; index < count; index++)
            {
                int entry = (int)directory + 2 + index * 12;
                int tag = UInt16(bytes, entry, little);
                if (tag == 330) return false;
                if (tag != 256 && tag != 257) continue;
                int type = UInt16(bytes, entry + 2, little);
                if (UInt32(bytes, entry + 4, little) != 1 || (type != 3 && type != 4)) return false;
                long value = type == 3 ? (long)UInt16(bytes, entry + 8, little) : UInt32(bytes, entry + 8, little);
                if (value < 1 || value > 300000) return false;
                if (tag == 256) { if (width != 0) return false; width = value; }
                else { if (height != 0) return false; height = value; }
            }
            return width > 0 && height > 0 && width * height <= MaximumPixels;
        }

        private static int UInt16(byte[] bytes, int offset, bool little)
        {
            return little ? bytes[offset] | bytes[offset + 1] << 8 : bytes[offset] << 8 | bytes[offset + 1];
        }

        private static uint UInt32(byte[] bytes, int offset, bool little)
        {
            return little ? (uint)UInt16(bytes, offset, true) | (uint)UInt16(bytes, offset + 2, true) << 16
                : (uint)UInt16(bytes, offset, false) << 16 | (uint)UInt16(bytes, offset + 2, false);
        }

        private static void ReadExactly(Stream stream, byte[] bytes, CancellationToken cancellation)
        {
            int offset = 0;
            while (offset < bytes.Length)
            {
                cancellation.ThrowIfCancellationRequested();
                int count = stream.Read(bytes, offset, Math.Min(65536, bytes.Length - offset));
                if (count <= 0) throw new EndOfStreamException();
                offset += count;
            }
        }
    }
}
