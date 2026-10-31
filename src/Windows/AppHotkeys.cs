using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed class AppHotkeys : NativeWindow, IDisposable
    {
        private readonly Action toggle, pin;
        internal bool ToggleRegistered { get; private set; }
        internal bool PinRegistered { get; private set; }
        internal AppHotkeys(Action toggle, Action pin)
        {
            this.toggle = toggle; this.pin = pin;
            CreateHandle(new CreateParams { Caption = "DesktopHover hotkeys", Parent = new IntPtr(-3) });
            ToggleRegistered = RegisterHotKey(Handle, 1, 0x4003, (uint)Keys.H);
            PinRegistered = RegisterHotKey(Handle, 2, 0x4003, (uint)Keys.P);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312)
            {
                if (message.WParam.ToInt32() == 1) toggle();
                else if (message.WParam.ToInt32() == 2) pin();
                return;
            }
            base.WndProc(ref message);
        }
        public void Dispose()
        {
            if (Handle == IntPtr.Zero) return;
            if (ToggleRegistered) UnregisterHotKey(Handle, 1);
            if (PinRegistered) UnregisterHotKey(Handle, 2);
            DestroyHandle();
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    }
}
