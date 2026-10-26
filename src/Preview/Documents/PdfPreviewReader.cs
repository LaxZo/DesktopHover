using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace DesktopHover
{
    // Called only by the bounded PDF worker. Windows renders the page locally;
    // no PDF viewer, shell thumbnail handler, or external process is activated.
    internal static class PdfPreviewReader
    {
        private const uint MaximumEncodedBytes = 16 * 1024 * 1024;

        internal static Bitmap Render(string path, System.Drawing.Size maxSize)
        {
            int maxWidth = Math.Max(1, Math.Min(1600, maxSize.Width));
            int maxHeight = Math.Max(1, Math.Min(1200, maxSize.Height));
            StorageFile file = Wait(StorageFile.GetFileFromPathAsync(Path.GetFullPath(path)));
            PdfDocument document = Wait(PdfDocument.LoadFromFileAsync(file));
            if (document.PageCount == 0) throw new InvalidDataException("The PDF has no pages.");

            // PdfDocument has no Close/Dispose API; the short-lived worker releases
            // its remaining native state and file handles when it exits.
            using (PdfPage page = document.GetPage(0))
            using (var output = new InMemoryRandomAccessStream())
            {
                double width = page.Size.Width, height = page.Size.Height;
                if (Double.IsNaN(width) || Double.IsInfinity(width) || width <= 0 ||
                    Double.IsNaN(height) || Double.IsInfinity(height) || height <= 0)
                    throw new InvalidDataException("Invalid PDF page dimensions.");
                double scale = Math.Min(maxWidth / width, maxHeight / height);
                uint renderWidth = (uint)Math.Max(1, Math.Min(maxWidth, Math.Round(width * scale)));
                uint renderHeight = (uint)Math.Max(1, Math.Min(maxHeight, Math.Round(height * scale)));
                var options = new PdfPageRenderOptions
                {
                    DestinationWidth = renderWidth,
                    DestinationHeight = renderHeight
                };
                Wait(page.RenderToStreamAsync(output, options));
                if (output.Size == 0 || output.Size > MaximumEncodedBytes)
                    throw new InvalidDataException("Invalid PDF render size.");
                output.Seek(0);
                using (var reader = new DataReader(output))
                {
                    uint length = (uint)output.Size;
                    uint loaded = Wait(reader.LoadAsync(length));
                    if (loaded != length) throw new EndOfStreamException();
                    byte[] bytes = new byte[length];
                    reader.ReadBytes(bytes);
                    using (var stream = new MemoryStream(bytes, false))
                    using (var image = Image.FromStream(stream, false, true))
                    {
                        if (image.Width < 1 || image.Height < 1 || image.Width > maxWidth || image.Height > maxHeight)
                            throw new InvalidDataException("Unexpected PDF render dimensions.");
                        var result = new Bitmap(image.Width, image.Height, PixelFormat.Format32bppPArgb);
                        try
                        {
                            using (Graphics graphics = Graphics.FromImage(result))
                            {
                                graphics.Clear(Color.White);
                                graphics.DrawImageUnscaled(image, 0, 0);
                            }
                            return result;
                        }
                        catch { result.Dispose(); throw; }
                    }
                }
            }
        }

        // OS metadata can be referenced directly without installing the Windows SDK.
        // Avoid AsTask/AsStream extensions: their compile-time types require the SDK's
        // union Windows.winmd. The parent process/watchdog bounds these native waits.
        private static T Wait<T>(IAsyncOperation<T> operation)
        {
            try
            {
                while (operation.Status == AsyncStatus.Started) Thread.Sleep(10);
                return operation.GetResults();
            }
            finally { operation.Close(); }
        }

        private static void Wait(IAsyncAction operation)
        {
            try
            {
                while (operation.Status == AsyncStatus.Started) Thread.Sleep(10);
                operation.GetResults();
            }
            finally { operation.Close(); }
        }
    }
}
