using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed class HoverSearchBox : Panel
    {
        internal readonly TextBox Editor;
        internal readonly SearchClearButton ClearButton;
        private readonly float scale;
        private readonly UiTween emphasis;

        internal HoverSearchBox(float scale)
        {
            this.scale = scale;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            emphasis = new UiTween(delegate { Invalidate(); });
            Editor = new TextBox { Name = "format-search", BorderStyle = BorderStyle.None };
            ClearButton = new SearchClearButton(scale) { Name = "clear-format-search", Visible = false };
            Controls.AddRange(new Control[] { Editor, ClearButton });
            Editor.TextChanged += delegate { ClearButton.Visible = Editor.TextLength > 0; Arrange(); };
            ClearButton.Click += delegate { Editor.Clear(); Editor.Focus(); };
            foreach (Control child in Controls)
            {
                child.GotFocus += delegate { UpdateEmphasis(); }; child.LostFocus += delegate { UpdateEmphasis(); };
                child.MouseEnter += delegate { UpdateEmphasis(); }; child.MouseLeave += delegate { UpdateEmphasis(); };
            }
            RefreshTheme();
        }
        internal void RefreshTheme()
        {
            BackColor = Brand.Background;
            // Native EDIT backgrounds have zero alpha on extended glass. Use
            // black there so the field shares the same material without a bright box.
            Editor.BackColor = ClearButton.BackColor = Brand.IsGlass ? Color.Black : Brand.Surface;
            Editor.ForeColor = Brand.Text; Invalidate(true);
        }
        private int Px(int value) { return (int)Math.Round(value * scale); }
        private void Arrange()
        {
            if (Editor == null) return;
            int right = Editor.TextLength == 0 ? Px(14) : Px(44);
            Editor.SetBounds(Px(40), Math.Max(0, (Height - Editor.PreferredHeight) / 2), Math.Max(1, Width - Px(40) - right), Editor.PreferredHeight);
            ClearButton.SetBounds(Width - Px(36), (Height - Px(28)) / 2, Px(28), Px(28));
            Invalidate();
        }
        private void UpdateEmphasis()
        { emphasis.AnimateTo(ContainsFocus || ClientRectangle.Contains(PointToClient(Cursor.Position)) ? 1 : 0, 140); }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); UpdateEmphasis(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); UpdateEmphasis(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) Editor.Focus(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); Arrange(); }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); Arrange(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics; WindowBackdrop.Clear(g, this); g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var shape = Brand.RoundRect(new RectangleF(scale, scale, Math.Max(1, Width - 2 * scale), Math.Max(1, Height - 2 * scale)), 10 * scale))
            using (var fill = new SolidBrush(Brand.Surface))
            using (var border = new Pen(UiMotion.Blend(Brand.Border, Brand.Accent, emphasis.Value), 1.2f * scale))
            { if (!Brand.IsGlass) g.FillPath(fill, shape); g.DrawPath(border, shape); }
            using (var pen = new Pen(UiMotion.Blend(Brand.Muted, Brand.Accent, emphasis.Value), 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
            {
                float x = 14 * scale, y = Height / 2f - 7 * scale;
                g.DrawEllipse(pen, x, y, 11 * scale, 11 * scale);
                g.DrawLine(pen, x + 9 * scale, y + 10 * scale, x + 15 * scale, y + 16 * scale);
            }
        }
        protected override void Dispose(bool disposing) { if (disposing) emphasis.Dispose(); base.Dispose(disposing); }

        internal sealed class SearchClearButton : Button
        {
            private readonly float scale;
            private bool hover;
            internal SearchClearButton(float scale)
            {
                this.scale = scale; FlatStyle = FlatStyle.Flat; FlatAppearance.BorderSize = 0;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
                SystemCursors.UseHand(this);
            }
            protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
            protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
            protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
            protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                if (Brand.IsGlass) WindowBackdrop.Clear(g, this); else g.Clear(Brand.Surface);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool active = hover || (Focused && ShowFocusCues);
                if (active) SettingsWindow.FillRound(g, new RectangleF(0, 0, Width, Height), 7 * scale, Brand.HoverSurface);
                float x = Width / 2f, y = Height / 2f, r = 4 * scale;
                using (var pen = new Pen(active ? Brand.Accent : Brand.Muted, 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round })
                { g.DrawLine(pen, x - r, y - r, x + r, y + r); g.DrawLine(pen, x + r, y - r, x - r, y + r); }
            }
        }
    }
}
