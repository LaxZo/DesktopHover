using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Windows.UI.ViewManagement;

namespace DesktopHover
{
    internal static class WindowsAccent
    {
        internal static readonly UISettings Source = CreateSource();

        private static UISettings CreateSource()
        {
            try { return new UISettings(); }
            catch (COMException) { return null; }
            catch (UnauthorizedAccessException) { return null; }
        }

        internal static Color Read()
        {
            if (Source != null)
            {
                try
                {
                    var color = Source.GetColorValue(UIColorType.Accent);
                    return Color.FromArgb(color.R, color.G, color.B);
                }
                catch (COMException) { }
                catch (UnauthorizedAccessException) { }
            }
            uint argb; bool opaque;
            return DwmGetColorizationColor(out argb, out opaque) == 0
                ? Color.FromArgb(255, Color.FromArgb(unchecked((int)argb))) : SystemColors.Highlight;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmGetColorizationColor(out uint color, [MarshalAs(UnmanagedType.Bool)] out bool opaque);
    }
}
