using System;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static class Shortcut
    {
        internal const Keys ToggleDefault = Keys.Control | Keys.Alt | Keys.H;
        internal const Keys PinDefault = Keys.Control | Keys.Alt | Keys.P;
        internal static readonly Keys[] Modifiers = { Keys.Control | Keys.Alt, Keys.Control | Keys.Shift,
            Keys.Alt | Keys.Shift, Keys.Control | Keys.Alt | Keys.Shift, Keys.Control, Keys.Alt };

        internal static bool Supported(Keys chord)
        {
            int key = (int)(chord & Keys.KeyCode);
            return Array.IndexOf(Modifiers, chord & Keys.Modifiers) >= 0 &&
                ((key >= (int)Keys.A && key <= (int)Keys.Z) || (key >= (int)Keys.D0 && key <= (int)Keys.D9) ||
                (key >= (int)Keys.F1 && key <= (int)Keys.F11));
        }
        internal static string Format(Keys chord)
        {
            string modifiers = ((chord & Keys.Control) != 0 ? "Ctrl+" : "") +
                ((chord & Keys.Alt) != 0 ? "Alt+" : "") + ((chord & Keys.Shift) != 0 ? "Shift+" : "");
            Keys key = chord & Keys.KeyCode;
            if (key == Keys.None) return modifiers.TrimEnd('+');
            int code = (int)key;
            return modifiers + (code >= (int)Keys.D0 && code <= (int)Keys.D9 ?
                (code - (int)Keys.D0).ToString(System.Globalization.CultureInfo.InvariantCulture) : key.ToString());
        }
        internal static Keys Parse(string text, Keys fallback)
        {
            Keys result = Keys.None, key;
            string[] parts = text.Split('+');
            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].Trim().ToLowerInvariant())
                {
                    case "ctrl": result |= Keys.Control; break;
                    case "alt": result |= Keys.Alt; break;
                    case "shift": result |= Keys.Shift; break;
                    default: return fallback;
                }
            }
            string last = parts[parts.Length - 1].Trim();
            if (last.Length == 1 && last[0] >= '0' && last[0] <= '9') last = "D" + last;
            if (!Enum.TryParse(last, true, out key) || key != (key & Keys.KeyCode)) return fallback;
            return Supported(result | key) ? result | key : fallback;
        }
    }
}
