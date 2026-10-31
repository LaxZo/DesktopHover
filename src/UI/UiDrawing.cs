using System.Drawing;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static class UiDrawing
    {
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
