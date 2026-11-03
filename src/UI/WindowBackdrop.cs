using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    // Apply the material to our own windows only. No desktop capture, injection,
    // color-key transparency or whole-window opacity is needed for the backdrop.
    internal sealed class WindowBackdrop : NativeWindow
    {
        private static readonly ConditionalWeakTable<Form, WindowBackdrop> windows = new ConditionalWeakTable<Form, WindowBackdrop>();
        private readonly Form window;
        private bool requested, active, applying, configured;

        private WindowBackdrop(Form window)
        {
            this.window = window;
            window.HandleCreated += Created;
            window.HandleDestroyed += Destroyed;
            window.Disposed += Disposed;
            if (window.IsHandleCreated) AssignHandle(window.Handle);
        }
        internal static void Apply(Form window, string theme)
        {
            WindowBackdrop backdrop = windows.GetValue(window, form => new WindowBackdrop(form));
            bool glass = AppSettings.NormalizeTheme(theme) == "glass";
            if (backdrop.configured && backdrop.requested == glass) return;
            backdrop.configured = true; backdrop.requested = glass;
            // Layered-window fades obscure the DWM material; contents stay opaque.
            if (glass) window.Opacity = 1;
            backdrop.Refresh();
        }
        internal static bool IsRequested(Form window)
        { WindowBackdrop backdrop; return windows.TryGetValue(window, out backdrop) && backdrop.requested; }
        internal static bool IsActive(Control control)
        {
            Form form = control as Form ?? control.FindForm();
            WindowBackdrop backdrop;
            return form != null && windows.TryGetValue(form, out backdrop) && backdrop.active;
        }
        internal static void Clear(Graphics graphics, Control control)
        {
            // GDI+ double buffers must preserve zero alpha in the background.
            // Painting an opaque black rectangle would cover the native material.
            graphics.Clear(IsActive(control) ? Color.FromArgb(0, 0, 0, 0) : Brand.Background);
        }
        private void Created(object sender, EventArgs e) { AssignHandle(window.Handle); Refresh(); }
        private void Destroyed(object sender, EventArgs e) { active = false; ReleaseHandle(); }
        private void Disposed(object sender, EventArgs e)
        {
            window.HandleCreated -= Created; window.HandleDestroyed -= Destroyed; window.Disposed -= Disposed;
            windows.Remove(window); ReleaseHandle();
        }
        private void Refresh()
        {
            if (applying || Handle == IntPtr.Zero || window.IsDisposed) return;
            applying = true;
            try
            {
                bool composition;
                bool enable = requested && !SystemInformation.HighContrast &&
                    DwmIsCompositionEnabled(out composition) == 0 && composition;
                active = false;
                if (enable)
                {
                    int dark = 1;
                    DwmSetWindowAttribute(Handle, 20, ref dark, 4);
                    // WCA_ACCENT_POLICY: Acrylic with a dark plum tint (~72% opacity).
                    // This is the same native accent mechanism used by translucent
                    // window customizers, with a solid fallback if it is unavailable.
                    var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
                    if (DwmExtendFrameIntoClientArea(Handle, ref margins) == 0)
                        active = SetAccent(4, unchecked((int)0xB8100B10));
                }
                if (!active)
                {
                    SetAccent(0, 0);
                    var margins = new Margins(); DwmExtendFrameIntoClientArea(Handle, ref margins);
                }
                // A WinForms Region clips the app's pixels, but the acrylic layer
                // still fills a rectangle. Ask DWM to round the composited window
                // too; it automatically keeps maximized windows square.
                int corners = active ? 2 : 0; // DWMWCP_ROUND / DWMWCP_DEFAULT
                DwmSetWindowAttribute(Handle, 33, ref corners, 4); // WINDOW_CORNER_PREFERENCE
            }
            catch (DllNotFoundException) { active = false; }
            catch (EntryPointNotFoundException) { active = false; }
            finally { applying = false; window.Invalidate(true); }
        }
        private bool SetAccent(int state, int tint)
        {
            var accent = new AccentPolicy { State = state, Flags = 2, GradientColor = tint };
            IntPtr pointer = Marshal.AllocHGlobal(Marshal.SizeOf(accent));
            try
            {
                Marshal.StructureToPtr(accent, pointer, false);
                var data = new CompositionData { Attribute = 19, Data = pointer, Size = new IntPtr(Marshal.SizeOf(accent)) };
                return SetWindowCompositionAttribute(Handle, ref data);
            }
            catch (EntryPointNotFoundException) { return false; }
            catch (DllNotFoundException) { return false; }
            finally { Marshal.FreeHGlobal(pointer); }
        }
        protected override void WndProc(ref Message message)
        {
            base.WndProc(ref message);
            if (requested && (message.Msg == 0x031E || message.Msg == 0x031A || message.Msg == 0x001A)) Refresh();
        }
        [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct AccentPolicy { public int State, Flags, GradientColor, Animation; }
        [StructLayout(LayoutKind.Sequential)] private struct CompositionData { public int Attribute; public IntPtr Data, Size; }
        [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowCompositionAttribute(IntPtr window, ref CompositionData data);
        [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
        [DllImport("dwmapi.dll")] private static extern int DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);
    }
}
