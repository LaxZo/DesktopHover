using System;
using System.IO;
using System.Threading;

namespace DesktopHover
{
    // ZipArchive materializes the whole central directory on first access to Entries.
    // Bound that work before constructing it, including when the EOCD count is forged.
    internal static class DocumentArchiveGuard
    {
        private const int MaximumEntries = 4096;
        private const long MaximumDirectoryBytes = 4L * 1024L * 1024L;

        internal static bool IsSafe(Stream stream, CancellationToken cancellation)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek) return false;
            long originalPosition = stream.Position;
            try
            {
                cancellation.ThrowIfCancellationRequested();
                long length = stream.Length;
                if (length < 22) return false;
                int tailLength = (int)Math.Min(length, 65535L + 22L);
                byte[] tail = new byte[tailLength];
                stream.Position = length - tailLength;
                if (!ReadExactly(stream, tail, tail.Length, cancellation)) return false;
                int endRecord = -1;
                for (int index = tail.Length - 22; index >= 0; index--)
                {
                    if ((index & 1023) == 0) cancellation.ThrowIfCancellationRequested();
                    if (UInt32(tail, index) == 0x06054b50 && index + 22 + UInt16(tail, index + 20) == tail.Length)
                    { endRecord = index; break; }
                }
                if (endRecord < 0) return false;
                int entries = UInt16(tail, endRecord + 10);
                uint directorySize = UInt32(tail, endRecord + 12);
                uint directoryOffset = UInt32(tail, endRecord + 16);
                // Multi-volume and ZIP64 containers are outside this bounded preview reader.
                if (UInt16(tail, endRecord + 4) != 0 || UInt16(tail, endRecord + 6) != 0 ||
                    UInt16(tail, endRecord + 8) != entries || entries > MaximumEntries ||
                    directorySize == uint.MaxValue || directoryOffset == uint.MaxValue ||
                    directorySize > MaximumDirectoryBytes)
                    return false;
                long endRecordOffset = length - tailLength + endRecord;
                long directoryEnd = (long)directoryOffset + directorySize;
                if (directoryEnd != endRecordOffset || directoryOffset > endRecordOffset) return false;
                if (entries == 0) return directorySize == 0;
                if (directorySize < 46L * entries) return false;

                stream.Position = directoryOffset;
                byte[] header = new byte[46];
                byte[] extraHeader = new byte[4];
                for (int entry = 0; entry < entries; entry++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (stream.Position + header.Length > directoryEnd || !ReadExactly(stream, header, header.Length, cancellation) ||
                        UInt32(header, 0) != 0x02014b50) return false;
                    int nameLength = UInt16(header, 28), extraLength = UInt16(header, 30), commentLength = UInt16(header, 32);
                    uint localOffset = UInt32(header, 42);
                    if (nameLength == 0 || UInt16(header, 34) != 0 || localOffset >= directoryOffset ||
                        UInt32(header, 20) == uint.MaxValue || UInt32(header, 24) == uint.MaxValue)
                        return false;
                    long next = stream.Position + nameLength + extraLength + commentLength;
                    if (next > directoryEnd) return false;
                    stream.Position += nameLength;
                    long extraEnd = stream.Position + extraLength;
                    while (stream.Position < extraEnd)
                    {
                        if (stream.Position + 4 > extraEnd || !ReadExactly(stream, extraHeader, 4, cancellation)) return false;
                        int fieldLength = UInt16(extraHeader, 2);
                        if (UInt16(extraHeader, 0) == 0x0001 || stream.Position + fieldLength > extraEnd) return false;
                        stream.Position += fieldLength;
                    }
                    stream.Position = next;
                }
                // Do not trust the advertised count: every central header must be accounted for.
                return stream.Position == directoryEnd;
            }
            catch (IOException) { return false; }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
            finally
            {
                try { stream.Position = originalPosition; }
                catch (IOException) { }
                catch (ObjectDisposedException) { }
            }
        }

        private static bool ReadExactly(Stream stream, byte[] buffer, int count, CancellationToken cancellation)
        {
            int offset = 0;
            while (offset < count)
            {
                cancellation.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, offset, count - offset);
                if (read <= 0) return false;
                offset += read;
            }
            return true;
        }

        private static int UInt16(byte[] bytes, int offset)
        {
            return bytes[offset] | (bytes[offset + 1] << 8);
        }

        private static uint UInt32(byte[] bytes, int offset)
        {
            return (uint)(bytes[offset] | (bytes[offset + 1] << 8) | (bytes[offset + 2] << 16) | (bytes[offset + 3] << 24));
        }
    }
}
