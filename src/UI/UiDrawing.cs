using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static class UiDrawing
    {
        internal static void FitMenuRows(ToolStripDropDownMenu menu, Control anchor, float scale)
        {
            // WinForms includes the check column in Padding.Left, then subtracts it
            // when positioning each menu item. It is already inside the row width.
            int inset = Math.Max(1, (int)Math.Round(4 * scale));
            Size available = Screen.FromControl(anchor).WorkingArea.Size;
            int rowWidth = 0;
            foreach (ToolStripItem item in menu.Items)
                if (item.Available && item is ToolStripMenuItem) rowWidth = Math.Max(rowWidth, item.Width);
            rowWidth = Math.Max(1, Math.Min(rowWidth, available.Width - 2 * inset));
            menu.SuspendLayout();
            try
            {
                menu.AutoSize = false;
                int height = menu.Padding.Vertical;
                foreach (ToolStripItem item in menu.Items)
                {
                    if (!item.Available) continue;
                    if (item is ToolStripMenuItem)
                    {
                        item.Margin = new Padding(inset, item.Margin.Top, inset, item.Margin.Bottom);
                        item.Width = rowWidth;
                    }
                    height += item.Height + item.Margin.Vertical;
                }
                menu.Size = new Size(rowWidth + 2 * inset, Math.Min(height, available.Height));
            }
            finally { menu.ResumeLayout(true); }
        }

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
