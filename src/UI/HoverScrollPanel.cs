using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopHover
{
    // Keep WinForms' wheel, focus and ScrollControlIntoView behavior. Its native
    // scrollbar sits outside the clipped viewport; the themed bar mirrors its position.
    internal sealed class HoverScrollPanel : Panel
    {
        private readonly Panel clip = new Panel();
        internal readonly ScrollContentPanel Content = new ScrollContentPanel();
        internal readonly HoverScrollBar ScrollBar;
        private readonly float scale;
        private bool syncing;
        internal int ViewportWidth { get { return clip.ClientSize.Width; } }
        internal Rectangle ViewportBounds { get { return clip.RectangleToScreen(clip.ClientRectangle); } }

        internal HoverScrollPanel(float scale)
        {
            this.scale = scale;
            ScrollBar = new HoverScrollBar(scale);
            Controls.AddRange(new Control[] { clip, ScrollBar }); clip.Controls.Add(Content);
            clip.Paint += delegate(object sender, PaintEventArgs e) { WindowBackdrop.Clear(e.Graphics, clip); };
            Content.ViewportChanged += delegate { SyncScroll(); };
            Content.Layout += delegate { SyncScroll(); };
            ScrollBar.ValueChanged += delegate { if (!syncing) ScrollTo(ScrollBar.Value); };
        }
        private int Px(int value) { return (int)Math.Round(value * scale); }
        protected override void OnPaintBackground(PaintEventArgs e) { WindowBackdrop.Clear(e.Graphics, this); }
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e); if (ScrollBar == null) return;
            int bar = Px(24), gap = Px(8);
            clip.SetBounds(0, 0, Math.Max(1, Width - bar - gap), Height);
            // Leave enough hidden room for the native bar even on a different-DPI monitor.
            Content.SetBounds(0, 0, clip.Width + Math.Max(Px(64), SystemInformation.VerticalScrollBarWidth * 2), clip.Height);
            ScrollBar.SetBounds(Width - bar, 0, bar, Height); SyncScroll();
        }
        internal void SyncScroll()
        {
            if (syncing || ScrollBar == null) return;
            syncing = true;
            try
            {
                ScrollBar.SetMetrics(Content.DisplayRectangle.Height, Content.ClientSize.Height, -Content.AutoScrollPosition.Y);
                ScrollBar.Visible = ScrollBar.Maximum > 0;
            }
            finally { syncing = false; }
        }
        internal void ScrollTo(int value)
        { Content.AutoScrollPosition = new Point(0, Math.Max(0, Math.Min(ScrollBar.Maximum, value))); SyncScroll(); }
        internal void EnsureVisible(Control control) { Content.ScrollControlIntoView(control); SyncScroll(); }
        internal void RefreshTheme()
        {
            BackColor = clip.BackColor = Content.BackColor = Brand.Background;
            ScrollBar.BackColor = Brand.Background; Invalidate(true);
        }
        internal sealed class ScrollContentPanel : Panel
        {
            internal event EventHandler ViewportChanged;
            internal ScrollContentPanel() { AutoScroll = true; BorderStyle = BorderStyle.None; DoubleBuffered = true; }
            protected override void OnPaintBackground(PaintEventArgs e) { WindowBackdrop.Clear(e.Graphics, this); }
            private void Changed() { if (ViewportChanged != null) ViewportChanged(this, EventArgs.Empty); }
            protected override void OnScroll(ScrollEventArgs e) { base.OnScroll(e); Changed(); }
            protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Changed(); }
        }
    }

    internal sealed class HoverScrollBar : Control
    {
        private readonly float scale;
        private readonly Timer repeat = new Timer();
        private int value, viewport, content, wheelRemainder, dragStartY, dragStartValue, pressedPart;
        private bool dragging, hover;
        internal string UpName, DownName;
        internal event EventHandler ValueChanged;
        internal int Maximum { get { return Math.Max(0, content - viewport); } }
        private int Step { get { return Math.Max(1, Px(44)); } }
        internal int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(0, Math.Min(Maximum, value));
                if (this.value == next) return;
                this.value = next; Invalidate(); AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }
        internal HoverScrollBar(float scale)
        {
            this.scale = scale; TabStop = true; AccessibleRole = AccessibleRole.ScrollBar;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            SystemCursors.UseHand(this);
            repeat.Tick += delegate { repeat.Interval = 65; MovePressed(); };
        }
        private int Px(int n) { return (int)Math.Round(n * scale); }
        private Rectangle UpBox { get { return new Rectangle(0, 0, Width, Math.Min(Px(24), Height / 2)); } }
        private Rectangle DownBox { get { return new Rectangle(0, Height - UpBox.Height, Width, UpBox.Height); } }
        private Rectangle Track { get { return new Rectangle(0, UpBox.Bottom + Px(5), Width, Math.Max(1, DownBox.Top - UpBox.Bottom - Px(10))); } }
        private Rectangle Thumb
        {
            get
            {
                Rectangle track = Track;
                int height = Math.Min(track.Height, Math.Max(Px(30), (int)((long)track.Height * viewport / Math.Max(1, content))));
                int top = Maximum == 0 ? 0 : (int)((long)(track.Height - height) * Value / Maximum);
                return new Rectangle(Px(6), track.Top + top, Math.Max(1, Width - Px(12)), height);
            }
        }
        internal void SetMetrics(int contentHeight, int viewportHeight, int position)
        {
            content = Math.Max(0, contentHeight); viewport = Math.Max(0, viewportHeight);
            Value = position; if (Maximum == 0) StopInteraction(); Invalidate();
        }
        private void MovePressed()
        {
            if (pressedPart == -1 || pressedPart == 1) Value += pressedPart * Step;
            else if (pressedPart != 0)
            {
                int y = PointToClient(Cursor.Position).Y;
                if (pressedPart < 0 && y < Thumb.Top) Value -= Math.Max(Step, viewport - Step);
                if (pressedPart > 0 && y > Thumb.Bottom) Value += Math.Max(Step, viewport - Step);
            }
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e); if (e.Button != MouseButtons.Left || Maximum == 0) return;
            Focus(); Capture = true;
            if (Thumb.Contains(e.Location)) { dragging = true; dragStartY = e.Y; dragStartValue = Value; }
            else
            {
                pressedPart = UpBox.Contains(e.Location) ? -1 : DownBox.Contains(e.Location) ? 1 : e.Y < Thumb.Top ? -2 : 2;
                if (Math.Abs(pressedPart) == 2) Value += Math.Sign(pressedPart) * Math.Max(Step, viewport - Step); else MovePressed();
                repeat.Interval = 400; repeat.Start();
            }
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (dragging) Value = dragStartValue + (int)((long)(e.Y - dragStartY) * Maximum / Math.Max(1, Track.Height - Thumb.Height));
            Invalidate();
        }
        private void StopInteraction() { dragging = false; pressedPart = 0; repeat.Stop(); Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); StopInteraction(); Capture = false; }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) StopInteraction(); }
        protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if (!Visible) { StopInteraction(); Capture = false; } }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e); wheelRemainder += e.Delta;
            int steps = wheelRemainder / 120; wheelRemainder %= 120;
            int lines = SystemInformation.MouseWheelScrollLines;
            Value -= steps * (lines < 0 ? viewport : lines * Step);
        }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Up || key == Keys.Down || key == Keys.Home || key == Keys.End || key == Keys.PageUp || key == Keys.PageDown || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Up: Value -= Step; break; case Keys.Down: Value += Step; break;
                case Keys.PageUp: Value -= viewport; break; case Keys.PageDown: Value += viewport; break;
                case Keys.Home: Value = 0; break; case Keys.End: Value = Maximum; break;
                default: return;
            }
            e.Handled = e.SuppressKeyPress = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; WindowBackdrop.Clear(g, this); g.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle track = Track, thumb = Thumb;
            SettingsWindow.FillRound(g, new RectangleF(Px(5), track.Top, Math.Max(1, Width - Px(10)), track.Height), Px(7), Brand.Surface);
            SettingsWindow.FillRound(g, thumb, Px(6), dragging || hover || Focused ? Brand.Accent : UiMotion.Blend(Brand.Border, Brand.Accent, .55));
            DrawArrow(g, UpBox, -1); DrawArrow(g, DownBox, 1);
        }
        private void DrawArrow(Graphics g, Rectangle bounds, int direction)
        {
            bool active = pressedPart == direction || (hover && bounds.Contains(PointToClient(Cursor.Position)));
            SettingsWindow.FillRound(g, new RectangleF(bounds.X + scale, bounds.Y + scale, Math.Max(1, bounds.Width - 2 * scale), Math.Max(1, bounds.Height - 2 * scale)), Px(7), active ? Brand.HoverSurface : Brand.Surface);
            float x = bounds.Left + bounds.Width / 2f, y = bounds.Top + bounds.Height / 2f;
            using (var pen = new Pen(active ? Brand.Accent : Brand.Muted, 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(x - 4 * scale, y - direction * 2 * scale), new PointF(x, y + direction * 2 * scale), new PointF(x + 4 * scale, y - direction * 2 * scale) });
        }
        protected override AccessibleObject CreateAccessibilityInstance() { return new ScrollAccessibleObject(this); }
        private sealed class ScrollAccessibleObject : ControlAccessibleObject
        {
            private readonly HoverScrollBar owner;
            internal ScrollAccessibleObject(HoverScrollBar owner) : base(owner) { this.owner = owner; }
            public override string Value { get { return owner.Value.ToString(); } set { int next; if (Int32.TryParse(value, out next)) owner.Value = next; } }
            public override int GetChildCount() { return 2; }
            public override AccessibleObject GetChild(int index) { return index < 0 || index > 1 ? null : new ArrowAccessibleObject(owner, index == 0 ? -1 : 1); }
        }
        private sealed class ArrowAccessibleObject : AccessibleObject
        {
            private readonly HoverScrollBar owner; private readonly int direction;
            internal ArrowAccessibleObject(HoverScrollBar owner, int direction) { this.owner = owner; this.direction = direction; }
            public override string Name { get { return direction < 0 ? owner.UpName : owner.DownName; } }
            public override AccessibleRole Role { get { return AccessibleRole.PushButton; } }
            public override AccessibleObject Parent { get { return owner.AccessibilityObject; } }
            public override Rectangle Bounds { get { return owner.RectangleToScreen(direction < 0 ? owner.UpBox : owner.DownBox); } }
            public override string DefaultAction { get { return "Press"; } }
            public override void DoDefaultAction() { owner.Value += direction * owner.Step; }
        }
        protected override void Dispose(bool disposing) { if (disposing) repeat.Dispose(); base.Dispose(disposing); }
    }
}
