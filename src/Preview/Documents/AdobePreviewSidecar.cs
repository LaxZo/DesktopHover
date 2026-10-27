using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PixelFormat = System.Drawing.Imaging.PixelFormat;

namespace DesktopHover
{
    // A deliberately named export is a project preview; arbitrary linked footage is not.
    // Prefer "Design.aep.preview.mp4" over "Design.preview.mp4" when both are present.
    internal static class AdobePreviewSidecar
    {
        private const long MaximumVideoBytes = 512L * 1024L * 1024L;
        private const long MaximumImageBytes = 64L * 1024L * 1024L;
        private const long MaximumImagePixels = 40000000;
        private const FileAttributes UnavailableAttributes = (FileAttributes)(0x1000 | 0x40000 | 0x400000) |
            FileAttributes.ReparsePoint | FileAttributes.Directory;
        private static readonly string[] Extensions = {
            ".mp4", ".mov", ".webm", ".m4v", ".avi", ".wmv",
            ".png", ".jpg", ".jpeg", ".webp", ".bmp"
        };

        internal static DocumentPreviewResult TryLoad(string path, Size maxSize, CancellationToken cancellation)
        {
            return LoadCore(path, maxSize, cancellation, true);
        }

        internal static DocumentPreviewResult TryLoadStill(string path, Size maxSize, CancellationToken cancellation)
        {
            return LoadCore(path, maxSize, cancellation, false);
        }

        private static DocumentPreviewResult LoadCore(string path, Size maxSize, CancellationToken cancellation, bool allowVideo)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!AdobeProjectFiles.IsSupported(path)) return null;
            string directory, name;
            try
            {
                path = Path.GetFullPath(path);
                directory = Path.GetDirectoryName(path);
                if (!IsLocalDirectory(directory)) return null;
                name = Path.GetFileName(path);
            }
            catch (ArgumentException) { return null; }
            catch (IOException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
            string[] stems = { name + ".preview", Path.GetFileNameWithoutExtension(name) + ".preview" };
            foreach (string stem in stems)
            {
                foreach (string extension in Extensions)
                {
                    cancellation.ThrowIfCancellationRequested();
                    string candidate = Path.Combine(directory, stem + extension);
                    try
                    {
                        bool video = IsVideoExtension(extension);
                        if (video && !allowVideo) continue;
                        if (!IsAvailable(candidate, video ? MaximumVideoBytes : MaximumImageBytes)) continue;
                        if (video)
                        {
                            using (var stream = new FileStream(candidate, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                if (stream.Length <= 0 || stream.Length > MaximumVideoBytes || !HasVideoHeader(stream, extension)) continue;
                            }
                            cancellation.ThrowIfCancellationRequested();
                            // Windows validates the remaining container/codec and presence of video.
                            return new DocumentPreviewResult(null, "document_sidecar") { MediaPath = candidate };
                        }
                        Bitmap image = ReadImage(candidate, maxSize, cancellation);
                        if (image != null) return new DocumentPreviewResult(image, "document_sidecar");
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    catch (ArgumentException) { }
                    catch (NotSupportedException) { }
                    catch (ExternalException) { }
                    catch (InvalidOperationException) { }
                    catch (FormatException) { }
                }
            }
            return null;
        }

        private static bool IsLocalDirectory(string path)
        {
            if (String.IsNullOrEmpty(path) || path.StartsWith(@"\\", StringComparison.Ordinal) ||
                new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network) return false;
            // A directory junction can redirect a local-looking path onto a network share.
            for (DirectoryInfo directory = new DirectoryInfo(path); directory != null; directory = directory.Parent)
            {
                if (!directory.Exists || (directory.Attributes & (FileAttributes.ReparsePoint |
                    (FileAttributes)(0x1000 | 0x40000 | 0x400000))) != 0) return false;
            }
            return true;
        }

        private static bool IsAvailable(string path, long maximumBytes)
        {
            var info = new FileInfo(path);
            return info.Exists && (info.Attributes & UnavailableAttributes) == 0 &&
                info.Length > 0 && info.Length <= maximumBytes;
        }

        private static bool IsVideoExtension(string extension)
        {
            return extension == ".mp4" || extension == ".mov" || extension == ".webm" ||
                extension == ".m4v" || extension == ".avi" || extension == ".wmv";
        }

        private static bool HasVideoHeader(Stream stream, string extension)
        {
            byte[] header = new byte[32];
            int length = stream.Read(header, 0, header.Length);
            if (extension == ".avi")
                return length >= 12 && Match(header, 0, "RIFF") && Match(header, 8, "AVI ");
            if (extension == ".webm")
                return length >= 8 && header[0] == 0x1a && header[1] == 0x45 && header[2] == 0xdf && header[3] == 0xa3;
            if (extension == ".wmv")
            {
                byte[] signature = { 0x30, 0x26, 0xb2, 0x75, 0x8e, 0x66, 0xcf, 0x11,
                    0xa6, 0xd9, 0x00, 0xaa, 0x00, 0x62, 0xce, 0x6c };
                if (length < signature.Length) return false;
                for (int i = 0; i < signature.Length; i++) if (header[i] != signature[i]) return false;
                return true;
            }
            if (length < 16) return false;
            long atomSize = ((long)header[0] << 24) | ((long)header[1] << 16) | ((long)header[2] << 8) | header[3];
            if (atomSize == 1)
            {
                // The byte limit excludes 64-bit lengths with a nonzero high word.
                if (header[8] != 0 || header[9] != 0 || header[10] != 0 || header[11] != 0) return false;
                atomSize = ((long)header[12] << 24) | ((long)header[13] << 16) | ((long)header[14] << 8) | header[15];
                if (atomSize < 16) return false;
            }
            else if (atomSize != 0 && atomSize < 8) return false;
            if (atomSize > stream.Length) return false;
            return Match(header, 4, "ftyp") || Match(header, 4, "moov") || Match(header, 4, "mdat") ||
                Match(header, 4, "wide") || Match(header, 4, "free") || Match(header, 4, "skip");
        }

        private static bool Match(byte[] bytes, int offset, string text)
        {
            if (offset + text.Length > bytes.Length) return false;
            for (int i = 0; i < text.Length; i++) if (bytes[offset + i] != text[i]) return false;
            return true;
        }

        private static Bitmap ReadImage(string path, Size maxSize, CancellationToken cancellation)
        {
            BitmapImage decoded;
            int orientation = 1;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length <= 0 || stream.Length > MaximumImageBytes) return null;
                // Limit the accepted container types before invoking an installed WIC codec.
                byte[] header = new byte[16];
                int count = stream.Read(header, 0, header.Length);
                bool png = count >= 8 && header[0] == 0x89 && Match(header, 1, "PNG\r\n\u001a\n");
                bool jpeg = count >= 3 && header[0] == 0xff && header[1] == 0xd8 && header[2] == 0xff;
                bool bmp = count >= 14 && Match(header, 0, "BM");
                bool webp = count >= 12 && Match(header, 0, "RIFF") && Match(header, 8, "WEBP");
                if (!png && !jpeg && !bmp && !webp) return null;
                stream.Position = 0;
                BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (decoder.Frames.Count < 1) return null;
                BitmapFrame frame = decoder.Frames[0];
                int width = frame.PixelWidth, height = frame.PixelHeight;
                if (width <= 0 || height <= 0 || (long)width * height > MaximumImagePixels) return null;
                try
                {
                    BitmapMetadata metadata = frame.Metadata as BitmapMetadata;
                    if (metadata != null)
                    {
                        object value = metadata.GetQuery("/app1/ifd/{ushort=274}") ?? metadata.GetQuery("/ifd/{ushort=274}");
                        if (value != null) orientation = Convert.ToInt32(value);
                    }
                }
                catch { orientation = 1; }
                int maximumWidth = Math.Max(1, Math.Min(1600, maxSize.Width));
                int maximumHeight = Math.Max(1, Math.Min(1200, maxSize.Height));
                bool swapped = orientation >= 5 && orientation <= 8;
                int rawMaximumWidth = swapped ? maximumHeight : maximumWidth;
                int rawMaximumHeight = swapped ? maximumWidth : maximumHeight;
                double widthRatio = (double)rawMaximumWidth / width;
                double heightRatio = (double)rawMaximumHeight / height;
                cancellation.ThrowIfCancellationRequested();
                stream.Position = 0;
                decoded = new BitmapImage();
                decoded.BeginInit();
                decoded.CacheOption = BitmapCacheOption.OnLoad;
                decoded.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                decoded.StreamSource = stream;
                if (widthRatio < 1.0 || heightRatio < 1.0)
                {
                    // Decode by the limiting dimension, including very thin/tall images.
                    if (widthRatio <= heightRatio) decoded.DecodePixelWidth = rawMaximumWidth;
                    else decoded.DecodePixelHeight = rawMaximumHeight;
                }
                decoded.EndInit();
                decoded.Freeze();
                if (decoded.PixelWidth <= 0 || decoded.PixelHeight <= 0 ||
                    decoded.PixelWidth > rawMaximumWidth || decoded.PixelHeight > rawMaximumHeight) return null;
            }
            cancellation.ThrowIfCancellationRequested();
            var converted = new FormatConvertedBitmap(decoded, PixelFormats.Pbgra32, null, 0);
            int stride = checked(converted.PixelWidth * 4);
            byte[] pixels = new byte[checked(stride * converted.PixelHeight)];
            converted.CopyPixels(pixels, stride, 0);
            var image = new Bitmap(converted.PixelWidth, converted.PixelHeight, PixelFormat.Format32bppPArgb);
            try
            {
                BitmapData data = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppPArgb);
                try
                {
                    for (int y = 0; y < image.Height; y++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        Marshal.Copy(pixels, y * stride, IntPtr.Add(data.Scan0, y * data.Stride), stride);
                    }
                }
                finally { image.UnlockBits(data); }
                switch (orientation)
                {
                    case 2: image.RotateFlip(RotateFlipType.RotateNoneFlipX); break;
                    case 3: image.RotateFlip(RotateFlipType.Rotate180FlipNone); break;
                    case 4: image.RotateFlip(RotateFlipType.Rotate180FlipX); break;
                    case 5: image.RotateFlip(RotateFlipType.Rotate90FlipX); break;
                    case 6: image.RotateFlip(RotateFlipType.Rotate90FlipNone); break;
                    case 7: image.RotateFlip(RotateFlipType.Rotate270FlipX); break;
                    case 8: image.RotateFlip(RotateFlipType.Rotate270FlipNone); break;
                }
                cancellation.ThrowIfCancellationRequested();
                return image;
            }
            catch { image.Dispose(); throw; }
        }
    }
}
