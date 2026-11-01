using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static class SystemCursors
    {
        private static Cursor hand;

        internal static void UseHand(Control control)
        {
            RefreshHand(control, EventArgs.Empty);
            control.MouseEnter += RefreshHand;
        }

        private static void RefreshHand(object sender, EventArgs e)
        {
            // .NET Framework's Cursors.Hand embeds an old bitmap. IDC_HAND uses
            // the user's Windows cursor scheme, including animated cursors.
            IntPtr handle = LoadCursorW(IntPtr.Zero, new IntPtr(32649));
            if (handle == IntPtr.Zero) return;
            // Windows owns this shared handle. Cursor(IntPtr) does not destroy it.
            if (hand == null || hand.Handle != handle) hand = new Cursor(handle);
            ((Control)sender).Cursor = hand;
        }

        [DllImport("user32.dll", ExactSpelling = true)]
        private static extern IntPtr LoadCursorW(IntPtr instance, IntPtr resource);
    }
}
