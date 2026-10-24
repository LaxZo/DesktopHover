using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;

namespace DesktopHover
{
    /// <summary>Owns a GIF and its encoded stream. Access only on the preview's UI thread.</summary>
    internal sealed class AnimatedGif : IDisposable
    {
        private const long MaximumFileBytes = 100L * 1024 * 1024;
        private const long MaximumFramePixels = 100000000L;
        private const int MaximumFrames = 2000;
        private readonly MemoryStream encoded;
        private readonly int[] delays;
        private readonly int repetitions;
        private int frameIndex;
        private int completedRepetitions;
        private bool ended;

        internal Bitmap Image { get; private set; }
        internal long FileBytes { get { return encoded.Length; } }
        internal int FrameDelay { get { return delays[frameIndex]; } }
        internal bool CanAdvance { get { return Image != null && delays.Length > 1 && !ended; } }

        private AnimatedGif(MemoryStream stream, int[] frameDelays, int repeatCount)
        {
            encoded = stream;
            delays = frameDelays;
            repetitions = repeatCount;
            // GDI+ composites GIF subframes, including transparency and disposal methods.
            // Keep the encoded stream alive; cloning into a bitmap would discard the animation.
            Image = new Bitmap(encoded);
            try
            {
                if (Image.GetFrameCount(FrameDimension.Time) != delays.Length)
                    throw new InvalidDataException("Invalid GIF frame count.");
                Image.SelectActiveFrame(FrameDimension.Time, 0);
            }
            catch { Image.Dispose(); Image = null; throw; }
        }

        internal static AnimatedGif Load(string path)
        {
            byte[] bytes;
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                if (file.Length < 13 || file.Length > MaximumFileBytes)
                    throw new InvalidDataException("GIF file size is unsupported.");
                bytes = new byte[(int)file.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int count = file.Read(bytes, read, bytes.Length - read);
                    if (count == 0) throw new EndOfStreamException();
                    read += count;
                }
            }

            var stream = new MemoryStream(bytes, false);
            try
            {
                int repeatCount;
                int[] frameDelays = ReadFrameInfo(stream, out repeatCount);
                stream.Position = 0;
                return new AnimatedGif(stream, frameDelays, repeatCount);
            }
            catch { stream.Dispose(); throw; }
        }

        internal bool Advance()
        {
            if (!CanAdvance) return false;
            int next = frameIndex + 1;
            if (next == delays.Length)
            {
                // No loop extension means play once; zero means repeat indefinitely.
                if (repetitions < 0 || (repetitions > 0 && completedRepetitions >= repetitions))
                {
                    ended = true;
                    return false;
                }
                if (repetitions > 0) completedRepetitions++;
                next = 0;
            }
            Image.SelectActiveFrame(FrameDimension.Time, next);
            frameIndex = next;
            return true;
        }

        private static int[] ReadFrameInfo(Stream stream, out int repeatCount)
        {
            // Inspect block lengths/dimensions before asking the native decoder to allocate pixels.
            var reader = new BinaryReader(stream);
            string signature = Encoding.ASCII.GetString(reader.ReadBytes(6));
            if (signature != "GIF87a" && signature != "GIF89a")
                throw new InvalidDataException("Invalid GIF signature.");
            int width = reader.ReadUInt16(), height = reader.ReadUInt16();
            if (width == 0 || height == 0 || (long)width * height > MaximumFramePixels)
                throw new InvalidDataException("GIF dimensions are too large.");
            int flags = reader.ReadByte();
            Skip(stream, 2);
            if ((flags & 128) != 0) Skip(stream, 3 << ((flags & 7) + 1));
            var frameDelays = new List<int>();
            int delay = 100;
            long pixels = 0;
            repeatCount = -1;
            while (true)
            {
                int block = reader.ReadByte();
                if (block == 0x3b) break;
                if (block == 0x2c)
                {
                    int left = reader.ReadUInt16(), top = reader.ReadUInt16();
                    int frameWidth = reader.ReadUInt16(), frameHeight = reader.ReadUInt16();
                    pixels += (long)frameWidth * frameHeight;
                    if (frameWidth == 0 || frameHeight == 0 || left + frameWidth > width ||
                        top + frameHeight > height || pixels > MaximumFramePixels || frameDelays.Count >= MaximumFrames)
                        throw new InvalidDataException("GIF frame limits exceeded.");
                    flags = reader.ReadByte();
                    if ((flags & 128) != 0) Skip(stream, 3 << ((flags & 7) + 1));
                    int codeSize = reader.ReadByte();
                    if (codeSize < 2 || codeSize > 8) throw new InvalidDataException("Invalid GIF code size.");
                    SkipBlocks(reader);
                    frameDelays.Add(delay);
                    delay = 100;
                }
                else if (block == 0x21)
                {
                    int label = reader.ReadByte();
                    if (label == 0xf9)
                    {
                        if (reader.ReadByte() != 4) throw new InvalidDataException("Invalid GIF control block.");
                        reader.ReadByte();
                        int hundredths = reader.ReadUInt16();
                        // Missing/zero/10 ms delays use the usual browser-compatible 100 ms fallback.
                        delay = hundredths < 2 ? 100 : hundredths * 10;
                        reader.ReadByte();
                        if (reader.ReadByte() != 0) throw new InvalidDataException("Invalid GIF control terminator.");
                    }
                    else if (label == 0xff)
                    {
                        int length = reader.ReadByte();
                        byte[] identifier = reader.ReadBytes(length);
                        if (identifier.Length != length) throw new EndOfStreamException();
                        string application = Encoding.ASCII.GetString(identifier);
                        while ((length = reader.ReadByte()) != 0)
                        {
                            if ((application == "NETSCAPE2.0" || application == "ANIMEXTS1.0") && length == 3)
                            {
                                int kind = reader.ReadByte(), count = reader.ReadUInt16();
                                if (kind == 1) repeatCount = count;
                            }
                            else Skip(stream, length);
                        }
                    }
                    else
                    {
                        SkipBlocks(reader);
                        if (label == 0x01) delay = 100; // Plain text consumes the preceding graphic control.
                    }
                }
                else throw new InvalidDataException("Invalid GIF block.");
            }
            if (frameDelays.Count == 0) throw new InvalidDataException("GIF contains no frames.");
            return frameDelays.ToArray();
        }

        private static void SkipBlocks(BinaryReader reader)
        {
            int length;
            while ((length = reader.ReadByte()) != 0) Skip(reader.BaseStream, length);
        }

        private static void Skip(Stream stream, int length)
        {
            if (length > stream.Length - stream.Position) throw new EndOfStreamException();
            stream.Position += length;
        }

        public void Dispose()
        {
            if (Image != null) { Image.Dispose(); Image = null; }
            encoded.Dispose();
        }
    }
}
