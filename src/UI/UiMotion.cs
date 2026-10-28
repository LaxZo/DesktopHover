using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace DesktopHover
{
    // Interface motion is independent of GIF, video and presentation playback.
    internal static class UiMotion
    {
        private static bool enabled = true;
        internal static event EventHandler Changed;
        internal static bool Enabled
        {
            get { return enabled; }
            set
            {
                if (enabled == value) return;
                enabled = value;
                EventHandler handler = Changed;
                if (handler != null) handler(null, EventArgs.Empty);
            }
        }

        internal static Color Blend(Color first, Color second, double amount)
        {
            amount = Math.Max(0, Math.Min(1, amount));
            return Color.FromArgb(
                (int)Math.Round(first.A + (second.A - first.A) * amount),
                (int)Math.Round(first.R + (second.R - first.R) * amount),
                (int)Math.Round(first.G + (second.G - first.G) * amount),
                (int)Math.Round(first.B + (second.B - first.B) * amount));
        }
    }

    // Timers run only while a value is changing. Reversals start at the current value.
    internal sealed class UiTween : IDisposable
    {
        private readonly Timer timer = new Timer { Interval = 15 };
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Action<double> update;
        private double start, target;
        private int duration;
        private bool disposed;
        internal double Value { get; private set; }

        internal UiTween(Action<double> update)
        {
            this.update = update;
            timer.Tick += Tick;
            UiMotion.Changed += MotionChanged;
        }

        internal void AnimateTo(double value, int milliseconds)
        {
            if (disposed) return;
            if (Value == value && !timer.Enabled) return;
            if (!UiMotion.Enabled || milliseconds <= 0) { Snap(value); return; }
            if (timer.Enabled && target == value) return;
            if (Value == value) { Snap(value); return; }
            start = Value;
            target = value;
            duration = milliseconds;
            clock.Restart();
            timer.Start();
        }

        internal void Snap(double value)
        {
            if (disposed) return;
            timer.Stop();
            clock.Reset();
            start = target = Value = value;
            update(value);
        }

        private void Tick(object sender, EventArgs e)
        {
            double progress = Math.Min(1, clock.Elapsed.TotalMilliseconds / duration);
            if (progress >= 1) { Snap(target); return; }
            double remaining = 1 - progress;
            Value = start + (target - start) * (1 - remaining * remaining * remaining);
            update(Value);
        }

        private void MotionChanged(object sender, EventArgs e)
        {
            if (!UiMotion.Enabled && timer.Enabled) Snap(target);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            UiMotion.Changed -= MotionChanged;
            timer.Stop();
            timer.Dispose();
            clock.Stop();
        }
    }

    internal sealed class WindowMotion : IDisposable
    {
        private readonly Form window;
        private readonly bool fade, animateClose;
        private readonly UiTween progress;
        private Point destination;
        private int offset;
        private bool closing, allowClose, restorePosition, disposed;

        internal WindowMotion(Form window, bool fade, bool animateClose)
        {
            this.window = window;
            this.fade = fade;
            this.animateClose = animateClose;
            progress = new UiTween(UpdateFrame);
            window.FormClosing += Closing;
            window.Resize += Resized;
            window.MouseDown += MouseDown;
        }

        internal void ShowAt(Point location)
        {
            destination = location;
            bool alreadyVisible = window.Visible && !closing;
            closing = false;
            allowClose = false;
            if (alreadyVisible || !UiMotion.Enabled)
            {
                progress.Snap(1);
                if (!window.Visible) window.Show();
                return;
            }
            Rectangle area = Screen.FromPoint(location).WorkingArea;
            offset = Math.Min(14, Math.Max(0, area.Bottom - window.Height - location.Y));
            if (offset == 0) offset = -Math.Min(14, Math.Max(0, location.Y - area.Top));
            progress.Snap(0);
            if (!window.Visible) window.Show();
            progress.AnimateTo(1, fade ? 220 : 160);
        }

        internal void Finish()
        {
            if (!disposed && progress.Value != (closing ? 0 : 1)) progress.Snap(closing ? 0 : 1);
        }

        private void UpdateFrame(double value)
        {
            if (disposed || window.IsDisposed) return;
            if (window.WindowState == FormWindowState.Normal)
                window.Location = new Point(destination.X, destination.Y + (int)Math.Round(offset * (1 - value)));
            // WPF video children must never be hosted in a layered/transparent window.
            if (fade) window.Opacity = Math.Max(0.01, value);
            if (closing && value == 0)
            {
                allowClose = true;
                // A close immediately after Show may finish synchronously inside FormClosing.
                // Leave that event before completing the close, and allow a reopen to cancel it.
                window.BeginInvoke((MethodInvoker)delegate
                {
                    if (!disposed && !window.IsDisposed && closing && allowClose) window.Close();
                });
            }
        }

        private void Closing(object sender, FormClosingEventArgs e)
        {
            if (!animateClose || allowClose || !UiMotion.Enabled || !window.Visible ||
                window.WindowState != FormWindowState.Normal || e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            if (closing) return;
            closing = true;
            destination = window.Location;
            offset = 8;
            progress.AnimateTo(0, 130);
        }

        private void Resized(object sender, EventArgs e)
        {
            if (window.WindowState == FormWindowState.Minimized)
            {
                restorePosition = progress.Value != 1 && !closing;
                Finish();
            }
            else if (restorePosition && window.WindowState == FormWindowState.Normal)
            {
                restorePosition = false;
                window.Location = destination;
            }
        }

        private void MouseDown(object sender, MouseEventArgs e) { Finish(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            progress.Dispose();
            window.FormClosing -= Closing;
            window.Resize -= Resized;
            window.MouseDown -= MouseDown;
        }
    }

    // Every menu owns its animation lifetime, including language and nested tray menus.
    internal sealed class MenuMotion : IDisposable
    {
        private static readonly ConditionalWeakTable<ToolStrip, MenuMotion> owners = new ConditionalWeakTable<ToolStrip, MenuMotion>();
        private readonly ToolStripDropDown menu;
        private readonly UiTween opacity;
        private readonly Dictionary<ToolStripItem, UiTween> highlights = new Dictionary<ToolStripItem, UiTween>();
        private bool disposed;

        internal static void Attach(ToolStripDropDown menu)
        {
            owners.GetValue(menu, delegate(ToolStrip owner) { return new MenuMotion((ToolStripDropDown)owner); });
        }

        internal static double HoverAmount(ToolStripItem item)
        {
            MenuMotion owner;
            ToolStrip parent = item.GetCurrentParent();
            if (parent == null || !owners.TryGetValue(parent, out owner) || owner.disposed)
                return item.Selected ? 1 : 0;
            UiTween highlight;
            if (!owner.highlights.TryGetValue(item, out highlight))
            {
                highlight = new UiTween(delegate { if (!item.IsDisposed) item.Invalidate(); });
                owner.highlights.Add(item, highlight);
            }
            double target = item.Selected ? 1 : 0;
            if (parent.Visible) highlight.AnimateTo(target, 120);
            else highlight.Snap(target);
            return highlight.Value;
        }

        private MenuMotion(ToolStripDropDown menu)
        {
            this.menu = menu;
            opacity = new UiTween(delegate(double value) { if (!menu.IsDisposed) menu.Opacity = Math.Max(.01, value); });
            menu.Opening += Opening;
            menu.Opened += Opened;
            menu.Closed += Closed;
            menu.Disposed += MenuDisposed;
        }

        private void Opening(object sender, System.ComponentModel.CancelEventArgs e)
        {
            opacity.Snap(UiMotion.Enabled ? 0 : 1);
        }
        private void Opened(object sender, EventArgs e) { opacity.AnimateTo(1, 150); }
        private void Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            opacity.Snap(1);
            foreach (UiTween highlight in highlights.Values) highlight.Snap(0);
        }
        private void MenuDisposed(object sender, EventArgs e) { Dispose(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            opacity.Dispose();
            foreach (UiTween highlight in highlights.Values) highlight.Dispose();
            highlights.Clear();
            menu.Opening -= Opening;
            menu.Opened -= Opened;
            menu.Closed -= Closed;
            menu.Disposed -= MenuDisposed;
            owners.Remove(menu);
        }
    }
}
