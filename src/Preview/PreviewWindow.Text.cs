using System;
using System.Threading;
using System.Threading.Tasks;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private Task<TextPreviewContent> textTask;
        private CancellationTokenSource textCancellation;

        private void CompleteTextLoad()
        {
            if (textTask == null || !textTask.IsCompleted) return;
            var task = textTask; textTask = null;
            textCancellation.Dispose(); textCancellation = null;
            mediaTimer.Stop(); mediaTimer.Interval = 200;
            try { textPreview = task.GetAwaiter().GetResult(); } catch { textPreview = null; }
            if (textPreview == null)
            {
                mediaState = "text_unavailable";
                LayoutImage(); ShowPreview(); Invalidate();
            }
            else { mediaState = null; LayoutText(); RestoreLoadedView(); }
        }

        private void CancelTextLoad()
        {
            var cancellation = textCancellation; var task = textTask;
            textCancellation = null; textTask = null;
            if (cancellation == null) return;
            cancellation.Cancel();
            task.ContinueWith(completed =>
            {
                try { completed.GetAwaiter().GetResult(); } catch { }
                finally { cancellation.Dispose(); }
            }, TaskScheduler.Default);
        }
    }
}
