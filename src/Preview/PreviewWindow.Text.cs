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
            bool archive = ArchiveListing.Supports(CurrentPath);
            try { textPreview = task.GetAwaiter().GetResult(); }
            catch (Exception error)
            {
                textPreview = null;
                if (archive) AppLog.PreviewError("Reading archive directory", CurrentPath, error);
            }
            if (textPreview == null)
            {
                mediaState = archive ? "archive_unavailable" : "text_unavailable";
                LayoutImage(); ShowPreview(); Invalidate();
            }
            else { mediaState = null; LayoutText(); RestoreLoadedView(); if (findOpen) RebuildMatches(); }
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
