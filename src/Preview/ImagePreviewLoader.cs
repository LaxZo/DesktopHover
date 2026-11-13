using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;

namespace DesktopHover
{
    internal sealed class ImagePreviewResult : IDisposable
    {
        internal Bitmap Image;
        internal AnimatedGif Animation;
        internal int Width, Height;
        internal int FrameCount = 1;
        internal long Bytes;
        internal string ErrorKey;
        public void Dispose()
        {
            if (Animation != null) Animation.Dispose();
            else if (Image != null) Image.Dispose();
            Animation = null; Image = null;
        }
    }

    internal static class ImagePreviewLoader
    {
        // One decode at a time bounds transient codec memory during fast pointer movement.
        private static readonly SemaphoreSlim decoder = new SemaphoreSlim(1, 1);
        private static readonly LinkedList<CachedImage> cache = new LinkedList<CachedImage>();
        private const long MaximumCacheBytes = 48L * 1024 * 1024;
        private static long cacheBytes;
        private sealed class CachedImage
        {
            internal string Path;
            internal long Length, Written, Cost;
            internal Size Size;
            internal Bitmap Image;
            internal int Width, Height;
            internal int Frame, FrameCount;
        }

        internal static ImagePreviewResult Load(string path, Size size, CancellationToken cancellation, bool refresh = false, int frame = 0)
        {
            var result = new ImagePreviewResult();
            bool entered = false;
            try
            {
                decoder.Wait(cancellation); entered = true;
                path = Path.GetFullPath(path);
                // Evict all sizes for this file under the same semaphore used by decodes.
                if (refresh)
                    for (var node = cache.Last; node != null; )
                    {
                        var previous = node.Previous;
                        if (String.Equals(node.Value.Path, path, StringComparison.OrdinalIgnoreCase))
                            RemoveCachedImage(node);
                        node = previous;
                    }
                var info = new FileInfo(path);
                if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
                    new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network ||
                    !info.Exists || ((int)info.Attributes & (0x1000 | 0x40000 | 0x400000)) != 0)
                    throw new IOException();
                if (info.Length > 100L * 1024 * 1024) { result.ErrorKey = "image_limit"; return result; }
                bool gif = String.Equals(info.Extension, ".gif", StringComparison.OrdinalIgnoreCase);
                if (!gif)
                {
                    for (var node = cache.First; node != null; node = node.Next)
                    {
                        CachedImage entry = node.Value;
                        if (!String.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase) ||
                            entry.Frame != Math.Min(frame, entry.FrameCount - 1) ||
                            entry.Size.Width < size.Width || entry.Size.Height < size.Height ||
                            entry.Length != info.Length || entry.Written != info.LastWriteTimeUtc.Ticks) continue;
                        result.Image = (Bitmap)entry.Image.Clone();
                        result.Width = entry.Width; result.Height = entry.Height; result.Bytes = entry.Length;
                        result.FrameCount = entry.FrameCount;
                        cache.Remove(node); cache.AddFirst(node);
                        return result;
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                if (gif)
                {
                    result.Animation = AnimatedGif.Load(path);
                    result.Image = result.Animation.Image;
                    result.Width = result.Image.Width; result.Height = result.Image.Height;
                    result.Bytes = result.Animation.FileBytes;
                }
                else
                {
                    if (SvgPreview.Supports(path))
                    {
                        result.Image = SvgPreview.Load(path, size, cancellation, out result.Width, out result.Height);
                        result.Bytes = info.Length;
                    }
                    else result.Image = PreviewWindow.LoadImagePage(path, size.Width, size.Height, frame,
                        out result.FrameCount, out result.Width, out result.Height, out result.Bytes);
                    cancellation.ThrowIfCancellationRequested();
                    long cost = (long)result.Image.Width * result.Image.Height * 4;
                    if (cost <= MaximumCacheBytes)
                    {
                        while (cache.Count >= 8 || cacheBytes + cost > MaximumCacheBytes)
                            RemoveCachedImage(cache.Last);
                        cache.AddFirst(new CachedImage { Path = path, Size = size, Length = info.Length,
                            Written = info.LastWriteTimeUtc.Ticks, Cost = cost, Width = result.Width,
                            Frame = Math.Min(frame, result.FrameCount - 1), FrameCount = result.FrameCount,
                            Height = result.Height, Image = (Bitmap)result.Image.Clone() });
                        cacheBytes += cost;
                    }
                }
                cancellation.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException) { result.Dispose(); throw; }
            catch (Exception error)
            {
                result.Dispose();
                AppLog.PreviewError("Image preview", path, error);
                result.ErrorKey = "image_unavailable";
                return result;
            }
            finally { if (entered) decoder.Release(); }
        }

        private static void RemoveCachedImage(LinkedListNode<CachedImage> node)
        {
            cacheBytes -= node.Value.Cost;
            node.Value.Image.Dispose();
            cache.Remove(node);
        }
    }
}
