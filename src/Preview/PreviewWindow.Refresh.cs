using System;
using System.Drawing;
using System.Threading.Tasks;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private Task<string> stampTask;
        private int pathRevision, stampRevision, stampRequest, requestedStamp;
        private DateTime nextStampCheck;
        private bool refreshRequested, repinRequested, refreshing;
        private PinnedViewState reloadView;

        private sealed class PinnedViewState
        {
            internal float Zoom;
            internal bool Fit, Paused;
            internal Point Pan;
            internal int Page, Slide, TextOffset, TableColumn;
            internal OfficeViewOptions Office;
            internal TimeSpan Position;
        }

        private PinnedViewState CaptureView()
        {
            return reloadView ?? new PinnedViewState { Zoom = zoom, Fit = fitMode, Pan = pan,
                Paused = playbackPaused, Page = pdfPageIndex, Slide = documentSlideIndex,
                TextOffset = textOffset, TableColumn = tableColumn, Office = officeView.Copy(), Position = PlaybackPosition };
        }

        // Only one metadata read can be in flight per window. Results from an old
        // path never affect the current preview, even if a slow drive returns late.
        private void CheckFileChanges()
        {
            if (IsDisposed) return;
            if (stampTask != null)
            {
                if (!stampTask.IsCompleted) return;
                string stamp = null;
                try { stamp = stampTask.GetAwaiter().GetResult(); } catch { }
                stampTask = null;
                if (stampRevision == pathRevision && stampRequest == requestedStamp && CurrentPath != null && stamp != null)
                {
                    bool manual = refreshRequested;
                    refreshRequested = false;
                    bool changed = stamp != contentStamp && (stamp == observedChange || (repinRequested && contentStamp != null));
                    bool unavailable = PreviewFileStamp.IsUnavailable(stamp);
                    bool missing = unavailable && mediaState != "file_unavailable" && stamp == observedChange;
                    repinRequested = false;
                    if (manual || missing || (!IsPreviewLoading && changed))
                    {
                        if (unavailable) ShowUnavailableFile(stamp);
                        else ReloadPinned();
                    }
                    if (contentStamp == null) contentStamp = stamp;
                    observedChange = stamp;
                }
            }
            if (!interactive || CurrentPath == null || !Visible || stampTask != null ||
                (!refreshRequested && !repinRequested && DateTime.UtcNow < nextStampCheck)) return;
            string path = CurrentPath;
            stampRevision = pathRevision;
            stampRequest = requestedStamp;
            nextStampCheck = DateTime.UtcNow.AddMilliseconds(750);
            stampTask = Task.Factory.StartNew(delegate
            {
                try { return PreviewFileStamp.Read(path); }
                catch { return "unavailable|"; }
            });
        }

        private void CheckRepinnedFile() { repinRequested = true; requestedStamp++; CheckFileChanges(); }

        private void RefreshPinned()
        {
            if (!interactive || CurrentPath == null || IsDisposed) return;
            refreshRequested = true;
            requestedStamp++;
            CheckFileChanges();
        }

        private void ReloadPinned()
        {
            string path = CurrentPath; Point cursor = mediaCursor; Rectangle item = mediaItemBounds;
            PinnedViewState view = CaptureView();
            PowerPointPreviewReader.Invalidate(path);
            OfficePreviewReader.Invalidate(path);
            ImagePreviewLoader.Invalidate();
            refreshing = true; reloadView = view;
            try { ClearPreview(false); ShowFile(path, cursor, item); }
            finally { refreshing = false; }
        }

        private void ShowUnavailableFile(string stamp)
        {
            PinnedViewState view = CaptureView();
            string path = CurrentPath, name = fileName;
            ClearPreview(false);
            CurrentPath = path; fileName = name; contentStamp = stamp;
            reloadView = view;
            mediaKind = MediaKind.Document; mediaState = "file_unavailable";
            description = L10n.Text(language, mediaState);
            LayoutInteractive(); ShowPreview(); Invalidate();
        }

        private void RestoreViewAfterClear()
        {
            if (!refreshing) { reloadView = null; return; }
            if (reloadView == null) return;
            pdfPageIndex = reloadView.Page;
            playbackPaused = reloadView.Paused;
            officeView.SheetIndex = reloadView.Office.SheetIndex;
            officeView.SheetName = reloadView.Office.SheetName;
            officeView.ScrollX = reloadView.Office.ScrollX;
            officeView.ScrollY = reloadView.Office.ScrollY;
        }

        private void RestoreLoadedView()
        {
            if (reloadView == null) return;
            PinnedViewState view = reloadView; reloadView = null;
            fitMode = view.Fit; zoom = view.Zoom; pan = view.Pan; textOffset = view.TextOffset; tableColumn = view.TableColumn;
            playbackPaused = view.Paused;
            if (media != null && media.IsOpen)
            {
                media.Seek(view.Position);
                if (playbackPaused) media.Pause(); else media.Play();
                mediaState = playbackPaused ? "media_paused" : "media_playing";
            }
            LayoutInteractive(); ClampPan(); UpdateToolbar(); Invalidate(); QueueImageDetail();
        }
    }
}
