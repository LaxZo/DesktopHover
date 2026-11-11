using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace DesktopHover
{
    /// <summary>One local-media hover session. All calls belong on the STA UI thread.</summary>
    internal sealed class MediaPlayback : IDisposable
    {
        private const int WsPopup = unchecked((int)0x80000000);
        private const int WsDisabled = 0x08000000;
        private const int WsClipSiblings = 0x04000000;
        private const int WsExNoActivate = 0x08000000;
        private const int SwpNoActivate = 0x0010;
        private const int SwpNoZOrder = 0x0004;
        private const int SwpShowWindow = 0x0040;

        private readonly IntPtr parentHwnd;
        private readonly System.Windows.Forms.Form owner;
        private readonly MediaPlayer player;
        private HwndSource videoSource;
        private DrawingVisual videoVisual;
        private Rectangle videoBounds;
        private bool requested;
        private string mediaPath;
        private bool failed;
        private bool disposed;
        private bool ownerUpdateQueued;

        internal event EventHandler Opened;
        internal event EventHandler Ended;
        internal event EventHandler Failed;

        internal bool IsOpen { get; private set; }
        internal bool HasEnded { get; private set; }
        internal bool HasVideo { get; private set; }
        internal int NaturalWidth { get; private set; }
        internal int NaturalHeight { get; private set; }
        internal TimeSpan Duration { get; private set; }
        internal TimeSpan Position
        {
            get
            {
                if (disposed || !IsOpen) return TimeSpan.Zero;
                try { return player.Position; }
                catch { return TimeSpan.Zero; }
            }
        }

        internal MediaPlayback(IntPtr parentHwnd, int volumePercent = 100)
        {
            this.parentHwnd = parentHwnd;
            owner = System.Windows.Forms.Control.FromHandle(parentHwnd) as System.Windows.Forms.Form;
            if (owner != null)
            {
                owner.LocationChanged += OwnerChanged;
                owner.SizeChanged += OwnerChanged;
                owner.VisibleChanged += OwnerChanged;
            }
            player = new MediaPlayer();
            volumePercent = Math.Max(0, Math.Min(100, volumePercent));
            player.Volume = volumePercent / 100.0;
            player.IsMuted = volumePercent == 0;
            player.MediaOpened += OnOpened;
            player.MediaEnded += OnEnded;
            player.MediaFailed += OnFailed;
        }

        internal void Open(string localPath)
        {
            if (disposed) throw new ObjectDisposedException("MediaPlayback");
            mediaPath = localPath;
            if (requested) throw new InvalidOperationException("A media session can only be opened once.");
            requested = true;
            try
            {
                string fullPath = Path.GetFullPath(localPath);
                if (!File.Exists(fullPath)) throw new FileNotFoundException("Media file not found.", fullPath);
                // The file is intentionally opened by Windows' decoder, never as a remote URL.
                player.Open(new Uri(fullPath, UriKind.Absolute));
            }
            catch (Exception error) { Fail(error); }
        }

        internal void SetVolume(int percent)
        {
            if (disposed) return;
            player.Volume = Math.Max(0, Math.Min(100, percent)) / 100.0;
            player.IsMuted = percent == 0;
        }

        internal void Pause() { if (!disposed && IsOpen) player.Pause(); }

        internal void Seek(TimeSpan position)
        {
            if (disposed || failed || !IsOpen || Duration <= TimeSpan.Zero) return;
            try
            {
                player.Position = TimeSpan.FromTicks(Math.Max(0, Math.Min(Duration.Ticks, position.Ticks)));
                HasEnded = false;
            }
            catch (Exception error) { Fail(error); }
        }

        internal void Play()
        {
            if (disposed || failed || !IsOpen) return;
            try
            {
                if (HasEnded) { player.Position = TimeSpan.Zero; HasEnded = false; }
                player.Play();
            }
            catch (Exception error) { Fail(error); }
        }

        internal void SetVideoBounds(Rectangle bounds)
        {
            if (disposed || failed) return;
            videoBounds = bounds;
            if (!HasVideo || bounds.Width <= 0 || bounds.Height <= 0)
            {
                if (videoSource != null) ShowWindow(videoSource.Handle, 0);
                return;
            }
            try
            {
                if (videoSource == null)
                {
                    var parameters = new HwndSourceParameters("Desktop Hover video");
                    parameters.ParentWindow = parentHwnd;
                    // An owned popup has its own opaque DWM surface. A WPF child
                    // inherits the parent's acrylic composition and lifts video colors.
                    // Ownership keeps it above the preview, without a taskbar entry.
                    parameters.WindowStyle = WsPopup | WsDisabled | WsClipSiblings;
                    parameters.ExtendedWindowStyle = WsExNoActivate | 0x80; // WS_EX_TOOLWINDOW
                    parameters.PositionX = bounds.X;
                    parameters.PositionY = bounds.Y;
                    parameters.Width = bounds.Width;
                    parameters.Height = bounds.Height;
                    parameters.HwndSourceHook = VideoWindowProc;
                    parameters.AcquireHwndFocusInMenuMode = false;
                    parameters.TreatAsInputRoot = false;
                    videoSource = new HwndSource(parameters);
                    videoSource.CompositionTarget.BackgroundColor = Colors.Black;
                    videoVisual = new DrawingVisual();
                    videoSource.RootVisual = videoVisual;
                }
                PositionVideo();
                DrawVideo();
            }
            catch (Exception error) { Fail(error); }
        }

        private void OwnerChanged(object sender, EventArgs e)
        {
            PositionVideo();
            if (disposed || ownerUpdateQueued) return;
            ownerUpdateQueued = true;
            // Native restore/move processing can reposition owned windows after
            // WinForms' SizeChanged notification. Align once that has finished.
            player.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
            {
                ownerUpdateQueued = false;
                if (disposed || failed) return;
                try { PositionVideo(); DrawVideo(); }
                catch (Exception error) { Fail(error); }
            }));
        }

        private void PositionVideo()
        {
            if (disposed || failed || videoSource == null || videoSource.IsDisposed) return;
            if (owner == null || !owner.Visible || owner.WindowState == System.Windows.Forms.FormWindowState.Minimized)
            { ShowWindow(videoSource.Handle, 0); return; }
            System.Drawing.Point origin = owner.PointToScreen(videoBounds.Location);
            SetWindowPos(videoSource.Handle, IntPtr.Zero, origin.X, origin.Y,
                videoBounds.Width, videoBounds.Height, SwpNoActivate | SwpNoZOrder | SwpShowWindow);
        }

        private void DrawVideo()
        {
            if (disposed || failed || videoSource == null || videoVisual == null ||
                videoSource.IsDisposed || videoBounds.Width <= 0 || videoBounds.Height <= 0) return;

            // Native child-window bounds are pixels; WPF drawings use device-independent units.
            Matrix pixelsToDips = videoSource.CompositionTarget.TransformFromDevice;
            System.Windows.Point extent = pixelsToDips.Transform(
                new System.Windows.Point(videoBounds.Width, videoBounds.Height));
            Rect target = new Rect(0, 0, Math.Max(1.0, extent.X), Math.Max(1.0, extent.Y));
            using (DrawingContext drawing = videoVisual.RenderOpen())
            {
                drawing.DrawRectangle(System.Windows.Media.Brushes.Black, null, target);
                drawing.DrawVideo(player, target);
            }
        }

        private IntPtr VideoWindowProc(IntPtr hwnd, int message, IntPtr wParam,
            IntPtr lParam, ref bool handled)
        {
            if (message == 0x0084) // WM_NCHITTEST: keep Explorer's hover and mouse input.
            {
                handled = true;
                return new IntPtr(-1);
            }
            if (message == 0x0021) // WM_MOUSEACTIVATE
            {
                handled = true;
                return new IntPtr(3);
            }
            if ((message == 0x02E0 || message == 0x02E3) && !disposed)
            {
                // Let WPF process the monitor DPI change before reading its new transform.
                player.Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(delegate
                {
                    if (disposed || failed) return;
                    try { DrawVideo(); }
                    catch (Exception error) { Fail(error); }
                }));
            }
            return IntPtr.Zero;
        }

        private void OnOpened(object sender, EventArgs args)
        {
            if (disposed || failed) return;
            try
            {
                NaturalWidth = player.NaturalVideoWidth;
                NaturalHeight = player.NaturalVideoHeight;
                HasVideo = player.HasVideo && NaturalWidth > 0 && NaturalHeight > 0;
                Duration = player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan : TimeSpan.Zero;
                IsOpen = true;
            }
            catch (Exception error) { Fail(error); return; }
            EventHandler handler = Opened;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnEnded(object sender, EventArgs args)
        {
            if (disposed || failed) return;
            HasEnded = true;
            EventHandler handler = Ended;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void OnFailed(object sender, ExceptionEventArgs args)
        {
            Fail(args.ErrorException);
        }

        private void Fail(Exception error)
        {
            if (disposed || failed) return;
            failed = true;
            AppLog.PreviewError("Media playback", mediaPath, error);
            IsOpen = false;
            StopAndClose();
            if (videoSource != null && !videoSource.IsDisposed) ShowWindow(videoSource.Handle, 0);
            EventHandler handler = Failed;
            if (handler != null) handler(this, EventArgs.Empty);
        }

        private void StopAndClose()
        {
            // Mute immediately while Windows tears down its asynchronous decoder graph.
            try { player.IsMuted = true; } catch { }
            try { player.Stop(); } catch { }
            try { player.Close(); } catch { }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (owner != null)
            {
                owner.LocationChanged -= OwnerChanged;
                owner.SizeChanged -= OwnerChanged;
                owner.VisibleChanged -= OwnerChanged;
            }
            IsOpen = false;
            player.MediaOpened -= OnOpened;
            player.MediaEnded -= OnEnded;
            player.MediaFailed -= OnFailed;
            Opened = null;
            Ended = null;
            Failed = null;
            StopAndClose();
            if (videoSource != null)
            {
                videoSource.RootVisual = null;
                videoSource.Dispose();
                videoSource = null;
            }
            videoVisual = null;
        }

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr insertAfter,
            int x, int y, int width, int height, int flags);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hwnd, int command);
    }
}
