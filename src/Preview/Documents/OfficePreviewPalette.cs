using System.Drawing;

namespace DesktopHover
{
    // Capture on the UI thread so a background render cannot mix two themes.
    internal sealed class OfficePreviewPalette
    {
        internal readonly Color Paper, Ink, Accent, Line, Muted, Header, Tab, TabText;

        private OfficePreviewPalette(Color paper, Color ink, Color accent, Color line,
            Color muted, Color header, Color tab, Color tabText)
        {
            Paper = paper; Ink = ink; Accent = accent; Line = line;
            Muted = muted; Header = header; Tab = tab; TabText = tabText;
        }

        internal static OfficePreviewPalette Capture()
        {
            return new OfficePreviewPalette(Brand.EditorBackground, Brand.Text, Brand.Accent,
                Brand.Border, Brand.Muted, Brand.EditorGutter, Brand.SelectedSurface, Brand.Text);
        }

        internal static readonly OfficePreviewPalette Default = new OfficePreviewPalette(
            Color.White, Color.FromArgb(39, 47, 60), Color.FromArgb(39, 103, 168),
            Color.FromArgb(220, 225, 232), Color.FromArgb(105, 118, 132),
            Color.FromArgb(239, 243, 248), Color.FromArgb(28, 119, 80), Color.White);
    }
}
