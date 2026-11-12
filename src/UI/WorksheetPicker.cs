using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed class WorksheetPicker : ComboBox
    {
        private float uiScale = 1;
        private bool hovered;
        [StructLayout(LayoutKind.Sequential)]
        private struct ComboInfo
        {
            internal int Size;
            internal Rectangle Item, Button;
            internal int ButtonState;
            internal IntPtr Combo, Edit, List;
        }
        [DllImport("user32.dll")]
        private static extern bool GetComboBoxInfo(IntPtr combo, ref ComboInfo info);
        private const TextFormatFlags LabelFlags = TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix |
            TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding;

        internal WorksheetPicker()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            SetStyle(ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            SystemCursors.UseHand(this);
            UiDrawing.BindNativeTheme(this);
            RefreshTheme();
        }

        private int Px(int value) { return Math.Max(1, (int)Math.Round(value * uiScale)); }

        internal void SetMetrics(float scale, Font font)
        {
            uiScale = scale;
            Font = font;
            int height = Px(25);
            if (ItemHeight != height) ItemHeight = height;
        }

        internal void RefreshTheme()
        {
            BackColor = SystemInformation.HighContrast ? SystemColors.Window : Brand.Surface;
            ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : Brand.Text;
            UiDrawing.RefreshNativeTheme(this);
            if (IsHandleCreated) StyleListWindow();
            Invalidate();
        }

        private void StyleListWindow()
        {
            var info = new ComboInfo { Size = Marshal.SizeOf(typeof(ComboInfo)) };
            if (GetComboBoxInfo(Handle, ref info) && info.List != IntPtr.Zero)
                WindowBackdrop.ApplyPopupFrame(info.List);
        }

        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnSelectedIndexChanged(EventArgs e) { base.OnSelectedIndexChanged(e); Invalidate(); }
        protected override void OnDropDownClosed(EventArgs e) { base.OnDropDownClosed(e); Invalidate(); }

        protected override void OnDropDown(EventArgs e)
        {
            int width = Width;
            using (Graphics g = CreateGraphics())
                foreach (object item in Items)
                    width = Math.Max(width, TextRenderer.MeasureText(g, GetItemText(item), Font, Size.Empty, LabelFlags).Width +
                        Px(40) + (Items.Count > MaxDropDownItems ? SystemInformation.VerticalScrollBarWidth : 0));
            DropDownWidth = Math.Min(width, Screen.FromControl(this).WorkingArea.Width);
            StyleListWindow();
            base.OnDropDown(e);
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool contrast = SystemInformation.HighContrast;
            bool active = DroppedDown || hovered || (Focused && ShowFocusCues);
            Color surface = contrast ? SystemColors.Window : DroppedDown ? Brand.SelectedSurface : Brand.Surface;
            Color ink = contrast ? SystemColors.WindowText : active ? Brand.Accent : Brand.Text;
            Color edge = contrast ? SystemColors.WindowText : active ? Brand.Accent : Brand.Border;
            Graphics g = e.Graphics;
            if (contrast) g.Clear(SystemColors.Control);
            else WindowBackdrop.Clear(g, this);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var bounds = new RectangleF(uiScale, uiScale, Width - 2 * uiScale, Height - 2 * uiScale);
            using (var path = Brand.RoundRect(bounds, Px(8)))
            using (var brush = new SolidBrush(surface))
            using (var pen = new Pen(edge, uiScale)) { g.FillPath(brush, path); g.DrawPath(pen, path); }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Px(10), 0, Math.Max(1, Width - Px(37)), Height), ink, LabelFlags);
            float x = Width - Px(14), y = Height / 2f, dy = (DroppedDown ? -2 : 2) * uiScale;
            using (var pen = new Pen(ink, 1.6f * uiScale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                g.DrawLines(pen, new[] { new PointF(x - Px(4), y - dy), new PointF(x, y + dy), new PointF(x + Px(4), y - dy) });
            if (contrast && Focused && ShowFocusCues) ControlPaint.DrawFocusRectangle(g, Rectangle.Inflate(ClientRectangle, -Px(3), -Px(3)));
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            bool current = e.Index == SelectedIndex;
            bool selected = (e.State & DrawItemState.Selected) != 0;
            bool contrast = SystemInformation.HighContrast;
            Color ink = contrast ? (selected ? SystemColors.HighlightText : SystemColors.WindowText) :
                (current || selected) ? Brand.Accent : Brand.Text;
            using (var brush = new SolidBrush(BackColor)) e.Graphics.FillRectangle(brush, e.Bounds);
            if (current || selected)
            {
                var row = Rectangle.Inflate(e.Bounds, -Px(3), -Px(1));
                Color fill = contrast ? (selected ? SystemColors.Highlight : SystemColors.Window) :
                    current ? Brand.SelectedSurface : Brand.HoverSurface;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                SettingsWindow.FillRound(e.Graphics, row, Px(5), fill);
            }
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font,
                new Rectangle(e.Bounds.Left + Px(10), e.Bounds.Top, Math.Max(1, e.Bounds.Width - Px(36)), e.Bounds.Height), ink, LabelFlags);
            if (current)
            {
                float x = e.Bounds.Right - Px(15), y = e.Bounds.Top + e.Bounds.Height / 2f;
                using (var pen = new Pen(ink, 1.7f * uiScale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    e.Graphics.DrawLines(pen, new[] { new PointF(x - Px(5), y), new PointF(x - Px(2), y + Px(3)), new PointF(x + Px(5), y - Px(4)) });
            }
            base.OnDrawItem(e);
        }
    }
}
