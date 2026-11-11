using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed partial class SettingsWindow
    {
        private const int CaptionHeight = 48;
        private SettingsSurface content, captionHeader;
        private WindowCaptionButton minimizeButton, maximizeButton, closeButton;
        private Size roundedSize;
        private float roundedScale;
        private Rectangle normalBounds;
        private FormWindowState lastWindowState = FormWindowState.Normal;
        private bool restoringBounds;
        internal ScrollableControl SettingsViewport { get { return content; } }

        private void InitializeChrome()
        {
            content = new SettingsSurface { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Brand.Background };
            foreach (Control control in designBounds.Keys) content.Controls.Add(control);
            content.Paint += PaintSettings;
            content.Scroll += delegate { content.Invalidate(); };
            Controls.Add(content);
            captionHeader = new SettingsSurface { Dock = DockStyle.Top, Height = CaptionHeight, BackColor = Brand.Background };
            captionHeader.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.ScaleTransform(uiScale, uiScale);
                Brand.DrawLogo(e.Graphics, new Rectangle(24, 12, 24, 24));
                DrawText(e.Graphics, "Desktop Hover", titleFont, Brand.Text, new Rectangle(58, 10, 246, 28));
                DrawText(e.Graphics, "v" + AppInfo.Version, smallFont, Brand.Muted, new Rectangle(206, 14, 100, 22));
            };
            captionHeader.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                windowMotion.Finish();
                if (e.Clicks == 2) ToggleMaximize();
                else { ReleaseCapture(); SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero); }
            };
            minimizeButton = new WindowCaptionButton(0) { Name = "minimize-settings" };
            maximizeButton = new WindowCaptionButton(1) { Name = "maximize-settings" };
            closeButton = new WindowCaptionButton(2) { Name = "close-settings" };
            minimizeButton.Click += delegate { WindowState = FormWindowState.Minimized; };
            maximizeButton.Click += delegate { ToggleMaximize(); };
            closeButton.Click += delegate { Close(); };
            captionHeader.Controls.AddRange(new Control[] { minimizeButton, maximizeButton, closeButton });
            Controls.Add(captionHeader);
            captionHeader.SendToBack();
            UpdateChrome(); PositionChrome();
        }

        private void ToggleMaximize()
        {
            windowMotion.Finish();
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { WindowBackdrop.Clear(e.Graphics, this); }

        protected override void OnResize(EventArgs e)
        {
            if (IsHandleCreated && WindowState == FormWindowState.Normal && lastWindowState != FormWindowState.Normal &&
                !normalBounds.IsEmpty && !restoringBounds)
            {
                Rectangle saved = normalBounds;
                restoringBounds = true;
                BeginInvoke((MethodInvoker)delegate
                {
                    // Do not overwrite an explicit resize made before this callback.
                    if (!IsDisposed && WindowState == FormWindowState.Normal &&
                        Math.Abs(Width - saved.Width) <= 64 && Math.Abs(Height - saved.Height) <= 64) Bounds = saved;
                    restoringBounds = false;
                    if (!IsDisposed && WindowState == FormWindowState.Normal) normalBounds = Bounds;
                });
            }
            lastWindowState = WindowState;
            base.OnResize(e);
            if (WindowState == FormWindowState.Normal && !restoringBounds) normalBounds = Bounds;
        }

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (WindowState == FormWindowState.Normal && lastWindowState == FormWindowState.Normal && !restoringBounds)
                normalBounds = Bounds;
        }

        private void UpdateChrome()
        {
            if (captionHeader == null) return;
            content.BackColor = captionHeader.BackColor = Brand.Background;
            minimizeButton.AccessibleName = T("minimize_settings");
            maximizeButton.AccessibleName = T(WindowState == FormWindowState.Maximized ? "restore_settings" : "maximize_settings");
            closeButton.AccessibleName = T("close_settings");
            foreach (Control button in captionHeader.Controls)
            {
                button.BackColor = Brand.Background; button.ForeColor = Brand.Text;
                windowToolTip.SetToolTip(button, button.AccessibleName); button.Invalidate();
            }
            captionHeader.Invalidate();
        }

        private void PositionChrome()
        {
            if (captionHeader == null) return;
            captionHeader.Height = (int)Math.Round(CaptionHeight * uiScale);
            int width = (int)Math.Round(42 * uiScale), height = (int)Math.Round(32 * uiScale);
            int right = ClientSize.Width - (int)Math.Round(12 * uiScale);
            WindowCaptionButton[] buttons = { minimizeButton, maximizeButton, closeButton };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].UiScale = uiScale;
                buttons[i].SetBounds(right - (3 - i) * width, (int)Math.Round(8 * uiScale), width, height);
            }
            UpdateChrome();
            ApplyWindowShape();
        }

        private void ApplyWindowShape()
        {
            if (!layoutReady || !IsHandleCreated || WindowState == FormWindowState.Minimized) return;
            if (WindowState == FormWindowState.Maximized)
            {
                Region previous = Region;
                Region = null;
                if (previous != null) previous.Dispose();
                roundedSize = Size.Empty;
                return;
            }
            if (roundedSize == Size && roundedScale == uiScale && Region != null) return;
            // A custom, fading frame cannot rely on Windows' automatic corner rounding.
            // Clip the entire window so its child panels share the same rounded outline.
            using (GraphicsPath path = Brand.RoundRect(new RectangleF(0, 0, Width, Height), 12 * uiScale))
            {
                Region previous = Region;
                Region = new Region(path);
                if (previous != null) previous.Dispose();
            }
            roundedSize = Size; roundedScale = uiScale;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                parameters.Style |= 0x000F0000; // THICKFRAME, SYSMENU, MINIMIZEBOX, MAXIMIZEBOX
                parameters.Style &= ~0x00C00000; // WS_CAPTION: custom buttons replace the native caption.
                return parameters;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x02E0 && layoutReady && m.LParam != IntPtr.Zero) // WM_DPICHANGED
            {
                var rect = (WindowRectangle)Marshal.PtrToStructure(m.LParam, typeof(WindowRectangle));
                Rectangle suggested = Rectangle.FromLTRB(rect.Left, rect.Top, rect.Right, rect.Bottom);
                Rectangle work = Screen.FromRectangle(suggested).WorkingArea;
                ApplyMonitorScale((m.WParam.ToInt64() & 0xffff) / 96f, work);
                if (WindowState == FormWindowState.Normal)
                {
                    suggested.Width = Math.Min(work.Width, Math.Max(MinimumSize.Width, suggested.Width));
                    suggested.Height = Math.Min(work.Height, Math.Max(MinimumSize.Height, suggested.Height));
                    suggested.X = Math.Max(work.Left, Math.Min(work.Right - suggested.Width, suggested.X));
                    suggested.Y = Math.Max(work.Top, Math.Min(work.Bottom - suggested.Height, suggested.Y));
                    Bounds = suggested;
                }
                ArrangeSettings();
                m.Result = IntPtr.Zero;
                return;
            }
            if (m.Msg == 0x0085) { m.Result = IntPtr.Zero; return; } // No legacy WM_NCPAINT over the custom header.
            if (m.Msg == 0x0086) { m.Result = new IntPtr(1); return; } // WM_NCACTIVATE
            // Keep the native resizable/minimize/maximize window styles, but draw our own caption.
            if (m.Msg == 0x0083 && m.WParam != IntPtr.Zero) // WM_NCCALCSIZE
            {
                // Maximized sizable windows retain an invisible resize frame outside
                // the work area. Keep client controls inside the visible work area.
                if (WindowState == FormWindowState.Maximized)
                {
                    Rectangle work = Screen.FromHandle(m.HWnd).WorkingArea;
                    Marshal.StructureToPtr(new WindowRectangle { Left = work.Left, Top = work.Top, Right = work.Right, Bottom = work.Bottom }, m.LParam, false);
                }
                m.Result = IntPtr.Zero; return;
            }
            base.WndProc(ref m);
            if (m.Msg == 0x0024) // WM_GETMINMAXINFO: maximization must leave the taskbar available.
            {
                Screen screen = Screen.FromHandle(m.HWnd);
                var limits = (WindowLimits)Marshal.PtrToStructure(m.LParam, typeof(WindowLimits));
                limits.MaxPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
                limits.MaxSize = screen.WorkingArea.Size;
                Marshal.StructureToPtr(limits, m.LParam, false);
            }
            else if (m.Msg == 0x0084 && WindowState == FormWindowState.Normal) // WM_NCHITTEST
            {
                long coordinates = m.LParam.ToInt64();
                Point p = PointToClient(new Point((short)(coordinates & 0xffff), (short)((coordinates >> 16) & 0xffff)));
                int edge = Math.Max(5, (int)Math.Round(6 * uiScale));
                bool left = p.X < edge, right = p.X >= ClientSize.Width - edge;
                bool top = p.Y < edge, bottom = p.Y >= ClientSize.Height - edge;
                int hit = top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 1;
                m.Result = new IntPtr(hit);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowRectangle { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct WindowLimits
        {
            public Point Reserved;
            public Size MaxSize;
            public Point MaxPosition;
            public Size MinTrackSize, MaxTrackSize;
        }

        private sealed class SettingsSurface : Panel
        {
            internal SettingsSurface() { DoubleBuffered = true; ResizeRedraw = true; }
            protected override void OnPaintBackground(PaintEventArgs e) { WindowBackdrop.Clear(e.Graphics, this); }
        }
    }

    internal sealed class WindowCaptionButton : Button
    {
        private readonly int glyph;
        private readonly UiTween hover;
        internal float UiScale = 1;
        internal WindowCaptionButton(int glyph)
        {
            this.glyph = glyph;
            hover = new UiTween(delegate { Invalidate(); });
            FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            SystemCursors.UseHand(this); TabStop = true;
        }
        protected override void OnMouseEnter(EventArgs e) { hover.AnimateTo(1, 120); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover.AnimateTo(0, 160); base.OnMouseLeave(e); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; WindowBackdrop.Clear(g, this);
            g.ScaleTransform(UiScale, UiScale); g.SmoothingMode = SmoothingMode.AntiAlias;
            float width = Width / UiScale, height = Height / UiScale;
            double amount = Focused && ShowFocusCues ? 1 : hover.Value;
            float x = width / 2, y = height / 2;
            using (var pen = new Pen(UiMotion.Blend(Brand.Text, Brand.Accent, amount), 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
            {
                if (glyph == 2)
                { g.DrawLine(pen, x - 6, y - 6, x + 6, y + 6); g.DrawLine(pen, x + 6, y - 6, x - 6, y + 6); }
                else
                {
                    float direction = glyph == 0 ? 1 : -1;
                    g.DrawLines(pen, new[] { new PointF(x - 6, y - 3 * direction), new PointF(x, y + 3 * direction), new PointF(x + 6, y - 3 * direction) });
                }
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) hover.Dispose(); base.Dispose(disposing); }
    }
}
