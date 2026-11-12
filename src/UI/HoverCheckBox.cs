using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopHover
{
    // Retain the native checkbox's input and accessibility behavior, with app-themed painting.
    internal class HoverCheckBox : CheckBox
    {
        internal float UiScale = 1;
        private readonly UiTween checkMotion, hoverMotion;

        internal HoverCheckBox()
        {
            checkMotion = new UiTween(delegate(double value) { Invalidate(); });
            hoverMotion = new UiTween(delegate(double value) { Invalidate(); });
            AutoSize = false; FlatStyle = FlatStyle.Flat; UseVisualStyleBackColor = false;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            SystemCursors.UseHand(this);
        }

        private void Animate(UiTween motion, double value)
        {
            if (motion == null) return;
            if (!IsHandleCreated || !Visible || !Enabled) motion.Snap(value);
            else motion.AnimateTo(value, 140);
        }
        protected override void OnCheckedChanged(EventArgs e)
        { Animate(checkMotion, Checked ? 1 : 0); base.OnCheckedChanged(e); }
        protected override void OnMouseEnter(EventArgs e) { Animate(hoverMotion, 1); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { Animate(hoverMotion, 0); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (hoverMotion != null) hoverMotion.Snap(0);
            if (checkMotion != null) checkMotion.Snap(Checked ? 1 : 0);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; float s = UiScale;
            WindowBackdrop.Clear(g, this); g.SmoothingMode = SmoothingMode.AntiAlias;
            double selected = checkMotion.Value;
            double emphasis = Enabled ? (Focused && ShowFocusCues ? 1 : hoverMotion.Value) : 0;
            Color surface = Enabled ? UiMotion.Blend(Brand.Surface, Brand.SelectedSurface, selected * .45 + emphasis * .25)
                : UiMotion.Blend(Brand.Surface, Brand.Background, .6);
            Color border = Enabled ? UiMotion.Blend(Brand.Border, Brand.Accent, emphasis * .75 + selected * .2) : Brand.Border;
            var bounds = new RectangleF(s, s, Math.Max(1, Width - 2 * s), Math.Max(1, Height - 2 * s));
            using (GraphicsPath tile = Brand.RoundRect(bounds, 8 * s))
            using (var fill = new SolidBrush(surface))
            using (var pen = new Pen(border, s)) { g.FillPath(fill, tile); g.DrawPath(pen, tile); }

            var box = new RectangleF(12 * s, (Height - 18 * s) / 2, 18 * s, 18 * s);
            DrawIndicator(g, box, selected, surface);
            TextRenderer.DrawText(g, Text, Font, new Rectangle((int)(40 * s), 0, Math.Max(1, Width - (int)(48 * s)), Height),
                Enabled ? Brand.Text : Brand.Muted, TextFormatFlags.NoPadding | TextFormatFlags.SingleLine |
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected virtual void DrawIndicator(Graphics g, RectangleF box, double selected, Color surface)
        {
            float s = UiScale;
            Color ink = Enabled ? Brand.Accent : Brand.Muted;
            using (GraphicsPath outline = Brand.RoundRect(box, 5 * s))
            using (var fill = new SolidBrush(UiMotion.Blend(surface, Enabled ? Brand.AccentFill : Brand.ToggleOff, selected)))
            using (var pen = new Pen(UiMotion.Blend(Brand.Muted, ink, selected), 1.2f * s))
            { g.FillPath(fill, outline); g.DrawPath(pen, outline); }
            if (selected > .01)
                using (var tick = new Pen(Color.FromArgb((int)(255 * selected), Enabled ? Brand.AccentMark(Color.White) : Brand.Muted), 1.8f * s)
                    { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    g.DrawLines(tick, new[] { new PointF(box.Left + 4 * s, box.Top + 9 * s),
                        new PointF(box.Left + 7.5f * s, box.Top + 12.5f * s), new PointF(box.Left + 14 * s, box.Top + 5.5f * s) });

        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) { checkMotion.Dispose(); hoverMotion.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
