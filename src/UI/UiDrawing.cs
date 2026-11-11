using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static class UiDrawing
    {
        internal static ToolTip CreateToolTip()
        {
            var tip = new ToolTip { OwnerDraw = true, ShowAlways = true };
            tip.Draw += delegate(object sender, DrawToolTipEventArgs e)
            {
                bool contrast = SystemInformation.HighContrast;
                e.Graphics.Clear(contrast ? SystemColors.Info : Brand.Surface);
                ControlPaint.DrawBorder(e.Graphics, e.Bounds, contrast ? SystemColors.InfoText : Brand.Border, ButtonBorderStyle.Solid);
                Rectangle text = Rectangle.Inflate(e.Bounds, -2, -1);
                TextRenderer.DrawText(e.Graphics, e.ToolTipText, e.Font, text, contrast ? SystemColors.InfoText : Brand.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
            };
            return tip;
        }

        internal static void StylePicker(ToolStripComboBox picker)
        {
            ComboBox box = picker.ComboBox;
            box.FlatStyle = FlatStyle.Flat;
            box.DrawMode = DrawMode.OwnerDrawFixed;
            box.DrawItem += delegate(object sender, DrawItemEventArgs e)
            {
                bool selected = (e.State & DrawItemState.Selected) != 0;
                bool contrast = SystemInformation.HighContrast;
                Color background = contrast ? (selected ? SystemColors.Highlight : SystemColors.Window) :
                    (selected ? Brand.SelectedSurface : Brand.Surface);
                Color ink = contrast ? (selected ? SystemColors.HighlightText : SystemColors.WindowText) : Brand.Text;
                using (var brush = new SolidBrush(background)) e.Graphics.FillRectangle(brush, e.Bounds);
                if (e.Index >= 0 && e.Index < box.Items.Count)
                    TextRenderer.DrawText(e.Graphics, box.GetItemText(box.Items[e.Index]), e.Font, e.Bounds, ink,
                        TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if ((e.State & (DrawItemState.Focus | DrawItemState.NoFocusRect)) == DrawItemState.Focus) e.DrawFocusRectangle();
            };
            BindNativeTheme(box);
            RefreshPicker(picker);
        }

        internal static void RefreshPicker(ToolStripComboBox picker)
        {
            picker.BackColor = SystemInformation.HighContrast ? SystemColors.Window : Brand.Surface;
            picker.ForeColor = SystemInformation.HighContrast ? SystemColors.WindowText : Brand.Text;
            RefreshNativeTheme(picker.ComboBox);
        }

        internal static void BindNativeTheme(Control control)
        {
            control.HandleCreated += delegate { RefreshNativeTheme(control); };
            RefreshNativeTheme(control);
        }

        internal static void RefreshNativeTheme(Control control)
        {
            if (control != null && control.IsHandleCreated)
                SetWindowTheme(control.Handle, SystemInformation.HighContrast ? null : Brand.IsLight ? "Explorer" : "DarkMode_Explorer", null);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hwnd, string application, string classes);

        // GDI+ honors the DPI and scroll transforms used by the custom settings canvas.
        internal static void Text(Graphics graphics, string text, Font font, Rectangle bounds, Color color, TextFormatFlags flags)
        {
            using (var format = new StringFormat(StringFormat.GenericTypographic))
            using (var brush = new SolidBrush(color))
            {
                format.FormatFlags = StringFormatFlags.NoWrap;
                format.Trimming = StringTrimming.EllipsisCharacter;
                format.Alignment = (flags & TextFormatFlags.Right) != 0 ? StringAlignment.Far :
                    (flags & TextFormatFlags.HorizontalCenter) != 0 ? StringAlignment.Center : StringAlignment.Near;
                format.LineAlignment = (flags & TextFormatFlags.VerticalCenter) != 0 ? StringAlignment.Center : StringAlignment.Near;
                graphics.DrawString(text ?? "", font, brush, bounds, format);
            }
        }
    }
}
