using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private PinnedHeader pinnedHeader;
        private WindowCaptionButton pinnedMinimize, pinnedMaximize, pinnedClose;
        private Size pinnedRoundedSize;
        private float pinnedRoundedScale;

        protected override void SetClientSizeCore(int width, int height)
        {
            // WM_NCCALCSIZE removes the native frame. Adding its thickness again
            // would grow the window on each maximize/restore cycle.
            base.SetClientSizeCore(width, height);
            if (interactive) Size = new Size(width, height);
        }

        private void InitializePinnedChrome()
        {
            pinnedHeader = new PinnedHeader();
            pinnedHeader.Paint += delegate(object sender, PaintEventArgs e)
            {
                e.Graphics.Clear(Brand.Background);
                Brand.DrawLogo(e.Graphics, new Rectangle(Px(14), Px(10), Px(24), Px(24)));
                UiDrawing.Text(e.Graphics, fileName ?? L10n.Text(language, "pinned_preview"), titleFont,
                    new Rectangle(Px(50), 0, Math.Max(1, pinnedHeader.Width - Px(204)), pinnedHeader.Height), Brand.Text,
                    TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            };
            pinnedHeader.MouseDown += delegate(object sender, MouseEventArgs e)
            {
                if (e.Button != MouseButtons.Left) return;
                entranceMotion.Finish();
                if (e.Clicks == 2) TogglePinnedMaximize();
                else { ReleasePinnedCapture(); SendPinnedMessage(Handle, 0xA1, new IntPtr(2), IntPtr.Zero); }
            };
            pinnedMinimize = new WindowCaptionButton(0);
            pinnedMaximize = new WindowCaptionButton(1);
            pinnedClose = new WindowCaptionButton(2);
            pinnedMinimize.Click += delegate { WindowState = FormWindowState.Minimized; };
            pinnedMaximize.Click += delegate { TogglePinnedMaximize(); };
            pinnedClose.Click += delegate { Close(); };
            pinnedHeader.Controls.AddRange(new Control[] { pinnedMinimize, pinnedMaximize, pinnedClose });
            Controls.Add(pinnedHeader);
            UpdatePinnedChrome();
        }

        private void TogglePinnedMaximize()
        {
            entranceMotion.Finish();
            WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
            LayoutInteractive();
        }

        private void UpdatePinnedChrome()
        {
            if (pinnedHeader == null) return;
            pinnedHeader.SetBounds(0, 0, ClientSize.Width, Px(44));
            pinnedHeader.BackColor = Brand.Background;
            pinnedMinimize.AccessibleName = L10n.Text(language, "minimize_settings");
            pinnedMaximize.AccessibleName = L10n.Text(language, WindowState == FormWindowState.Maximized ? "restore_settings" : "maximize_preview");
            pinnedClose.AccessibleName = L10n.Text(language, "close_preview");
            WindowCaptionButton[] buttons = { pinnedMinimize, pinnedMaximize, pinnedClose };
            for (int i = 0; i < buttons.Length; i++)
            {
                buttons[i].UiScale = scale;
                buttons[i].BackColor = Brand.Background;
                buttons[i].SetBounds(ClientSize.Width - Px(10 + (3 - i) * 42), Px(6), Px(42), Px(32));
                contentTip.SetToolTip(buttons[i], buttons[i].AccessibleName); buttons[i].Invalidate();
            }
            pinnedHeader.Invalidate();
            if (!IsHandleCreated || WindowState == FormWindowState.Minimized) return;
            if (WindowState == FormWindowState.Maximized)
            {
                Region previous = Region; Region = null; if (previous != null) previous.Dispose();
                pinnedRoundedSize = Size.Empty;
            }
            else if (pinnedRoundedSize != Size || pinnedRoundedScale != scale || Region == null)
            {
                using (var path = Brand.RoundRect(new RectangleF(0, 0, Width, Height), Px(12)))
                {
                    Region previous = Region; Region = new Region(path); if (previous != null) previous.Dispose();
                }
                pinnedRoundedSize = Size; pinnedRoundedScale = scale;
            }
        }

        private bool HandlePinnedChrome(ref Message message)
        {
            if (!interactive) return false;
            if (message.Msg == 0x85) { message.Result = IntPtr.Zero; return true; }
            if (message.Msg == 0x86) { message.Result = new IntPtr(1); return true; }
            if (message.Msg == 0x83 && message.WParam != IntPtr.Zero)
            {
                if (WindowState == FormWindowState.Maximized)
                {
                    Rectangle work = Screen.FromHandle(message.HWnd).WorkingArea;
                    Marshal.StructureToPtr(new PinnedRectangle { Left = work.Left, Top = work.Top, Right = work.Right, Bottom = work.Bottom }, message.LParam, false);
                }
                message.Result = IntPtr.Zero; return true;
            }
            if (message.Msg == 0x24)
            {
                base.WndProc(ref message);
                Screen screen = Screen.FromHandle(message.HWnd);
                var limits = (PinnedWindowLimits)Marshal.PtrToStructure(message.LParam, typeof(PinnedWindowLimits));
                limits.MaxPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
                limits.MaxSize = screen.WorkingArea.Size;
                Marshal.StructureToPtr(limits, message.LParam, false);
                return true;
            }
            if (message.Msg == 0x84 && WindowState == FormWindowState.Normal)
            {
                long coordinates = message.LParam.ToInt64();
                Point point = PointToClient(new Point((short)(coordinates & 0xffff), (short)((coordinates >> 16) & 0xffff)));
                int edge = Math.Max(5, Px(6));
                bool left = point.X < edge, right = point.X >= ClientSize.Width - edge;
                bool top = point.Y < edge, bottom = point.Y >= ClientSize.Height - edge;
                message.Result = new IntPtr(top ? (left ? 13 : right ? 14 : 12) : bottom ? (left ? 16 : right ? 17 : 15) : left ? 10 : right ? 11 : 1);
                return true;
            }
            return false;
        }

        [StructLayout(LayoutKind.Sequential)] private struct PinnedRectangle { public int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)] private struct PinnedWindowLimits
        { public Point Reserved; public Size MaxSize; public Point MaxPosition; public Size MinTrackSize, MaxTrackSize; }
        private sealed class PinnedHeader : Panel
        { internal PinnedHeader() { DoubleBuffered = true; ResizeRedraw = true; } }
        [DllImport("user32.dll", EntryPoint = "ReleaseCapture")] private static extern bool ReleasePinnedCapture();
        [DllImport("user32.dll", EntryPoint = "SendMessageW")] private static extern IntPtr SendPinnedMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }
}
