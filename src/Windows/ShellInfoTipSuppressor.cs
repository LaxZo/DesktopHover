using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;

namespace DesktopHover
{
    /// <summary>Dismisses Explorer file infotips without changing Windows preferences.</summary>
    internal sealed class ShellInfoTipSuppressor : IDisposable
    {
        private const uint EventObjectShow = 0x8002;
        private const uint TtmActivate = 0x0401;
        private readonly WinEventProc callback;
        private readonly Func<bool> canSuppress;
        private IntPtr hook, root, view;
        private DateTime lastSweep = DateTime.MinValue;
        private bool handlingEvent, disposed;
        private readonly InfoTipRestoreGuard restoreGuard = new InfoTipRestoreGuard();
        private readonly Dictionary<IntPtr, InfoTipRestoreGuard.Lease> disabled = new Dictionary<IntPtr, InfoTipRestoreGuard.Lease>();

        internal ShellInfoTipSuppressor(Func<bool> canSuppress)
        {
            this.canSuppress = canSuppress;
            callback = OnWindowShown;
            // Out of context: no code injection. The UI message loop receives these callbacks.
            hook = SetWinEventHook(EventObjectShow, EventObjectShow, IntPtr.Zero,
                callback, 0, 0, 0x0002); // WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS
        }

        internal void Track(IntPtr surface, IntPtr shellRoot, Point cursor)
        {
            if (handlingEvent) return;
            if (disposed || !canSuppress()) { Clear(); return; }
            IntPtr fileView = FindFileView(surface);
            if (fileView == IntPtr.Zero || Native.GetAncestor(fileView, 2) != shellRoot ||
                !Native.IsShellRoot(shellRoot)) { Clear(); return; }
            bool changed = root != shellRoot || view != fileView;
            if (changed) Clear();
            root = shellRoot;
            view = fileView;
            // Catch a tooltip that predates our hook/scope, and retain a fallback if hooks fail.
            if (changed || (DateTime.UtcNow - lastSweep).TotalMilliseconds >= 300)
            {
                lastSweep = DateTime.UtcNow;
                EnumWindows(delegate(IntPtr window, IntPtr parameter)
                {
                    DismissAtCursor(window, cursor);
                    return true;
                }, IntPtr.Zero);
            }
        }

        internal bool DismissAtCursor(IntPtr window, Point cursor)
        {
            // WinEvent callbacks can reenter while native calls or Process.Start pump
            // messages. Guard every entry path, including the timer's EnumWindows sweep.
            if (handlingEvent || disposed) return false;
            handlingEvent = true;
            try { return DismissAtCursorCore(window, cursor); }
            finally
            {
                handlingEvent = false;
                if (disposed) restoreGuard.Dispose();
            }
        }

        private bool DismissAtCursorCore(IntPtr window, Point cursor)
        {
            if (disposed || root == IntPtr.Zero || view == IntPtr.Zero || !canSuppress() ||
                Native.Class(window) != "tooltips_class32" || !IsWindowVisible(window)) return false;
            if (!Native.IsShellRoot(root) || !IsWindowVisible(view) || Native.GetAncestor(view, 2) != root)
            { Clear(); return false; }
            NativeRect bounds;
            if (!GetWindowRect(view, out bounds) || !bounds.Contains(cursor)) return false;

            uint shellProcess, tipProcess;
            Native.GetWindowThreadProcessId(root, out shellProcess);
            Native.GetWindowThreadProcessId(window, out tipProcess);
            if (shellProcess == 0 || tipProcess != shellProcess) return false;
            // The taskbar and other Explorer windows share its PID. Verify the owner too.
            IntPtr owner = GetWindow(window, 4); // GW_OWNER
            if (Native.GetAncestor(owner, 2) != root) return false;
            IntPtr underneath = Native.WindowFromPoint(new Native.POINT { X = cursor.X, Y = cursor.Y });
            if (underneath != window && FindFileView(underneath) != view) return false;

            // POP alone lets Explorer show the same tip again, causing a periodic flash.
            // Deactivate this known-visible control until we leave the view or pause/exit.
            if (!disabled.ContainsKey(window))
            {
                if (disabled.Count >= 64) return false;
                IntPtr targetView = view;
                uint process;
                uint thread = Native.GetWindowThreadProcessId(window, out process);
                var lease = new InfoTipRestoreGuard.Lease { Window = window, Process = process,
                    Thread = thread, Owner = owner, Root = root };
                // Record before changing Explorer. If our process dies, pipe EOF triggers restoration.
                if (!restoreGuard.Remember(lease)) return false;
                // A nested pause, view change or exit may have cleared the scope while
                // the restore helper was starting. Do not deactivate a stale control.
                bool stillAllowed = canSuppress() && lease.MatchesWindow();
                if (!stillAllowed || disposed || root != lease.Root || view != targetView)
                {
                    restoreGuard.Forget(window);
                    return false;
                }
                disabled.Add(window, lease);
            }
            return InfoTipRestoreGuard.PostMessage(window, TtmActivate, IntPtr.Zero, IntPtr.Zero);
        }

        private void OnWindowShown(IntPtr eventHook, uint eventType, IntPtr window,
            int objectId, int childId, uint threadId, uint eventTime)
        {
            if (disposed || handlingEvent || window == IntPtr.Zero || objectId != 0 || childId != 0) return;
            try
            {
                Native.POINT cursor;
                if (Native.GetCursorPos(out cursor)) DismissAtCursor(window, new Point(cursor.X, cursor.Y));
            }
            catch { /* A closing/restarting Explorer window must never interrupt the application. */ }
        }

        private static IntPtr FindFileView(IntPtr window)
        {
            // DirectUIHWND also occurs in navigation/toolbars; require the actual folder view.
            // Windows 11's Items View uses this nested native host instead of SHELLDLL_DefView.
            if (Native.Class(window) == "DirectUIHWND")
            {
                IntPtr host = GetParent(window);
                if (Native.Class(host) == "DUIViewWndClassName" && Native.Class(GetParent(host)) == "ShellTabWindowClass")
                    return window;
            }
            for (int i = 0; window != IntPtr.Zero && i < 24; i++)
            {
                if (Native.Class(window) == "SHELLDLL_DefView") return window;
                window = GetParent(window);
            }
            return IntPtr.Zero;
        }

        internal void Clear()
        {
            root = view = IntPtr.Zero;
            lastSweep = DateTime.MinValue;
            RestoreTips();
        }

        private void RestoreTips()
        {
            var leases = new InfoTipRestoreGuard.Lease[disabled.Count];
            disabled.Values.CopyTo(leases, 0);
            disabled.Clear();
            foreach (InfoTipRestoreGuard.Lease lease in leases)
            {
                if (lease.Restore()) restoreGuard.Forget(lease.Window);
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Clear();
            restoreGuard.Dispose();
            if (hook != IntPtr.Zero) { UnhookWinEvent(hook); hook = IntPtr.Zero; }
            GC.KeepAlive(callback);
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect
        {
            public int Left, Top, Right, Bottom;
            internal bool Contains(Point point)
            { return point.X >= Left && point.X < Right && point.Y >= Top && point.Y < Bottom; }
        }
        private delegate void WinEventProc(IntPtr hook, uint eventType, IntPtr window,
            int objectId, int childId, uint threadId, uint eventTime);
        private delegate bool EnumWindowProc(IntPtr window, IntPtr parameter);
        [DllImport("user32.dll")] private static extern IntPtr SetWinEventHook(uint eventMin,
            uint eventMax, IntPtr module, WinEventProc callback, uint process, uint thread, uint flags);
        [DllImport("user32.dll")] private static extern bool UnhookWinEvent(IntPtr hook);
        [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowProc callback, IntPtr parameter);
        [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out NativeRect rect);
        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll")] private static extern IntPtr GetParent(IntPtr window);
    }
}
