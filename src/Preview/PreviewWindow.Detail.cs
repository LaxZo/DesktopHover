using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private System.Windows.Forms.Timer detailTimer;
        private CancellationTokenSource detailCancellation;
        private Task<ImagePreviewResult> detailTask;
        private DateTime detailDue;
        private Size detailRequested;
        private int detailSlide, detailPage;

        private void QueueImageDetail()
        {
            if (!interactive || preview == null || animatedGif != null || IsOfficeView || media != null) return;
            if (mediaKind != MediaKind.Image && mediaKind != MediaKind.Document) return;
            if (detailTimer == null)
            {
                detailTimer = new System.Windows.Forms.Timer { Interval = 100 };
                detailTimer.Tick += delegate { CompleteImageDetail(); };
            }
            detailDue = DateTime.UtcNow.AddMilliseconds(180); detailTimer.Start();
        }

        private void CompleteImageDetail()
        {
            if (detailTask != null)
            {
                if (!detailTask.IsCompleted) return;
                var task = detailTask; detailTask = null;
                detailCancellation.Dispose(); detailCancellation = null;
                try
                {
                    using (var result = task.GetAwaiter().GetResult())
                        if (preview != null && result != null && result.Image != null &&
                            detailPage == pdfPageIndex && detailSlide == documentSlideIndex &&
                            (long)result.Image.Width * result.Image.Height > (long)preview.Width * preview.Height)
                        {
                            Bitmap old = preview; preview = result.Image; result.Image = null; old.Dispose();
                            // Keep the exact visible rectangle and pan when replacing its pixels.
                            UpdateToolbar(); Invalidate(viewport);
                        }
                }
                catch { }
            }
            if (DateTime.UtcNow < detailDue) return;
            detailTimer.Stop();
            if (preview == null || IsPreviewLoading || CurrentPath == null || animatedGif != null) return;
            double width = imageArea.Width * zoom, height = imageArea.Height * zoom;
            if (mediaKind == MediaKind.Image && !SvgPreview.Supports(CurrentPath))
            { width = Math.Min(sourceWidth, width); height = Math.Min(sourceHeight, height); }
            double cap = Math.Min(1, Math.Min(4096 / Math.Max(1, width), 4096 / Math.Max(1, height)));
            Size size = new Size(Math.Max(1, (int)Math.Ceiling(width * cap)), Math.Max(1, (int)Math.Ceiling(height * cap)));
            if (size.Width <= preview.Width * 1.1 && size.Height <= preview.Height * 1.1) return;
            if (size.Width <= detailRequested.Width && size.Height <= detailRequested.Height) return;
            detailRequested = size; detailSlide = documentSlideIndex; detailPage = pdfPageIndex;
            string path = CurrentPath;
            int page = pdfPageIndex, slide = documentSlideIndex;
            bool image = mediaKind == MediaKind.Image;
            var palette = OfficePreviewPalette.Capture();
            detailCancellation = new CancellationTokenSource(); var token = detailCancellation.Token;
            detailTask = Task.Factory.StartNew(delegate
            {
                if (image) return ImagePreviewLoader.Load(path, size, token);
                if (String.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    int count;
                    return new ImagePreviewResult { Image = DocumentPreviewWorker.TryLoadPdfPage(path, size, token, page, out count) };
                }
                using (var document = DocumentPreviewContent.LoadStill(path, size, token, palette, null, true))
                {
                    if (document == null) return null;
                    if (document.Slides != null && slide < document.Slides.Length)
                        using (var stream = new MemoryStream(document.Slides[slide], false))
                        using (var bitmap = System.Drawing.Image.FromStream(stream))
                            return new ImagePreviewResult { Image = DocumentPreviewWorker.CopyImage(bitmap, size) };
                    var result = new ImagePreviewResult { Image = document.Image }; document.Image = null; return result;
                }
            }, CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
            detailTimer.Start();
        }

        private void CancelImageDetail()
        {
            if (detailTimer != null) detailTimer.Stop();
            detailRequested = Size.Empty;
            var task = detailTask; var cancellation = detailCancellation;
            detailTask = null; detailCancellation = null;
            if (cancellation == null) return;
            cancellation.Cancel();
            task.ContinueWith(completed =>
            {
                try { using (var result = completed.GetAwaiter().GetResult()) { } }
                catch { }
                finally { cancellation.Dispose(); }
            }, TaskScheduler.Default);
        }
    }
}
