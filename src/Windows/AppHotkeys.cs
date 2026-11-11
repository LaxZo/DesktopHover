using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed class AppHotkeys : NativeWindow, IDisposable
    {
        private readonly Action toggle, pin;
        private Keys toggleChord, pinChord;
        private int toggleId = 1, pinId = 2;
        internal bool ToggleRegistered { get; private set; }
        internal bool PinRegistered { get; private set; }
        internal AppHotkeys(Action toggle, Action pin, AppSettings settings = null)
        {
            this.toggle = toggle; this.pin = pin;
            CreateHandle(new CreateParams { Caption = "DesktopHover hotkeys", Parent = new IntPtr(-3) });
            toggleChord = settings == null ? Shortcut.ToggleDefault : settings.ToggleShortcut;
            pinChord = settings == null ? Shortcut.PinDefault : settings.PinShortcut;
            ToggleRegistered = Register(toggleId, toggleChord);
            PinRegistered = Register(pinId, pinChord);
        }

        private bool Register(int id, Keys chord)
        {
            uint modifiers = 0x4000; // MOD_NOREPEAT
            if ((chord & Keys.Alt) != 0) modifiers |= 1;
            if ((chord & Keys.Control) != 0) modifiers |= 2;
            if ((chord & Keys.Shift) != 0) modifiers |= 4;
            return RegisterHotKey(Handle, id, modifiers, (uint)(chord & Keys.KeyCode));
        }

        // Reserve replacement chords before saving. Keep the active shortcuts if
        // Windows rejects a chord or saving the settings fails.
        internal void Apply(AppSettings settings, Action save)
        {
            bool changeToggle = settings.ToggleShortcut != toggleChord, changePin = settings.PinShortcut != pinChord;
            if (settings.ToggleShortcut == settings.PinShortcut || !Shortcut.Supported(settings.ToggleShortcut) || !Shortcut.Supported(settings.PinShortcut))
                throw new InvalidOperationException(L10n.Text(settings.Language, "shortcut_conflict"));
            int nextToggle = toggleId == 1 ? 3 : 1, nextPin = pinId == 2 ? 4 : 2;
            bool reservedToggle = false, reservedPin = false;
            try
            {
                if (changeToggle && !(reservedToggle = Register(nextToggle, settings.ToggleShortcut)) ||
                    changePin && !(reservedPin = Register(nextPin, settings.PinShortcut)))
                    throw new InvalidOperationException(L10n.Text(settings.Language, "shortcut_conflict"));
                save();
            }
            catch
            {
                if (reservedToggle) UnregisterHotKey(Handle, nextToggle);
                if (reservedPin) UnregisterHotKey(Handle, nextPin);
                throw;
            }
            if (changeToggle)
            {
                if (ToggleRegistered) UnregisterHotKey(Handle, toggleId);
                toggleId = nextToggle; toggleChord = settings.ToggleShortcut; ToggleRegistered = true;
            }
            if (changePin)
            {
                if (PinRegistered) UnregisterHotKey(Handle, pinId);
                pinId = nextPin; pinChord = settings.PinShortcut; PinRegistered = true;
            }
            // Retry unchanged shortcuts independently. A startup conflict must not
            // block saving unrelated settings or recovering the other shortcut.
            if (!ToggleRegistered) ToggleRegistered = Register(toggleId, toggleChord);
            if (!PinRegistered) PinRegistered = Register(pinId, pinChord);
        }
        protected override void WndProc(ref Message message)
        {
            if (message.Msg == 0x0312)
            {
                if (message.WParam.ToInt32() == toggleId) toggle();
                else if (message.WParam.ToInt32() == pinId) pin();
                return;
            }
            base.WndProc(ref message);
        }
        public void Dispose()
        {
            if (Handle == IntPtr.Zero) return;
            if (ToggleRegistered) UnregisterHotKey(Handle, toggleId);
            if (PinRegistered) UnregisterHotKey(Handle, pinId);
            DestroyHandle();
        }
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);
    }
}
