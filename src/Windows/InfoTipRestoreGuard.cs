using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    /// <summary>Restores temporarily disabled tooltip controls even if the main process exits abruptly.</summary>
    internal sealed class InfoTipRestoreGuard : IDisposable
    {
        internal sealed class Lease
        {
            internal IntPtr Window, Owner, Root, ListView;
            internal uint Process, Thread;
            internal bool MatchesWindow()
            {
                uint process;
                return Window != IntPtr.Zero && Native.Class(Window) == "tooltips_class32" &&
                    Native.GetWindowThreadProcessId(Window, out process) == Thread && process == Process &&
                    GetWindow(Window, 4) == Owner && Native.IsShellRoot(Root) &&
                    (ListView != IntPtr.Zero
                        ? Native.GetAncestor(ListView, 2) == Root && Native.ListViewToolTip(ListView) == Window
                        : Native.GetAncestor(Owner, 2) == Root);
            }
            internal bool Restore()
            {
                // Folder Options may have changed while we held the control. Never
                // reactivate Windows file descriptions that the user disabled there.
                return !MatchesWindow() || PostMessage(Window, 0x0401,
                    WindowsInfoTipsEnabled() ? new IntPtr(1) : IntPtr.Zero, IntPtr.Zero); // TTM_ACTIVATE
            }
        }

        private Process worker;
        private StreamWriter input;
        private readonly Dictionary<IntPtr, Lease> remembered = new Dictionary<IntPtr, Lease>();
        private readonly HashSet<IntPtr> registered = new HashSet<IntPtr>();
        private DateTime nextStart = DateTime.MinValue;
        private bool starting, disposed;

        internal bool Remember(Lease lease)
        {
            if (disposed || starting || lease == null || lease.Window == IntPtr.Zero) return false;
            if (!remembered.ContainsKey(lease.Window) && remembered.Count >= 64) return false;
            Lease previous;
            bool known = remembered.TryGetValue(lease.Window, out previous), accepted = false;
            if (known && !Object.ReferenceEquals(previous, lease)) registered.Remove(lease.Window);
            remembered[lease.Window] = lease;
            try
            {
                if (!EnsureWorker()) return false;
                if (!registered.Contains(lease.Window)) WriteLease(lease);
                accepted = true;
                return true;
            }
            catch (Exception error) { AppLog.Write("Windows tooltip restore helper", error); Disconnect(); return false; }
            finally
            {
                // A failed new registration never disabled this control. Do not let
                // transient startup failures fill the bounded active-lease list.
                if (!accepted && !known) remembered.Remove(lease.Window);
            }
        }

        private bool EnsureWorker()
        {
            if (worker != null)
            {
                if (!worker.HasExited) return input != null;
                Disconnect(); worker.Dispose(); worker = null;
            }
            if (DateTime.UtcNow < nextStart) return false;
            nextStart = DateTime.UtcNow.AddSeconds(1); // Bound repeated startup/crash failures.
            starting = true;
            try
            {
                var start = new ProcessStartInfo(Application.ExecutablePath, "--infotip-restore-guard")
                { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true };
                worker = Process.Start(start);
                input = worker.StandardInput; input.AutoFlush = true;
                if (disposed) { Disconnect(); return false; }
                // The parent owns the authoritative list. A replacement helper must
                // protect existing hidden controls as well as the newly hovered one.
                var leases = new Lease[remembered.Count]; remembered.Values.CopyTo(leases, 0);
                foreach (Lease item in leases) WriteLease(item);
                return true;
            }
            finally { starting = false; }
        }

        private void WriteLease(Lease lease)
        {
            input.WriteLine(String.Format(CultureInfo.InvariantCulture, "D {0} {1} {2} {3} {4} {5}",
                lease.Window.ToInt64(), lease.Process, lease.Thread, lease.Owner.ToInt64(), lease.Root.ToInt64(), lease.ListView.ToInt64()));
            registered.Add(lease.Window);
        }

        private void Disconnect()
        {
            // EOF lets a still-running helper restore its controls before replacement.
            var previous = input; input = null; registered.Clear();
            try { if (previous != null) previous.Dispose(); } catch { }
        }

        internal void Forget(IntPtr window)
        {
            remembered.Remove(window); registered.Remove(window);
            try { if (input != null) input.WriteLine("R " + window.ToInt64().ToString(CultureInfo.InvariantCulture)); }
            catch { Disconnect(); }
        }

        internal static bool TryRunWorker(string[] args)
        {
            if (args.Length != 1 || args[0] != "--infotip-restore-guard") return false;
            var leases = new Dictionary<long, Lease>();
            try
            {
                using (var reader = new StreamReader(Console.OpenStandardInput()))
                {
                    // The parent owns the only pipe writer. EOF also occurs after a crash/forced exit.
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        string[] values = line.Split(' ');
                        long window;
                        if (values.Length < 2 || !Int64.TryParse(values[1], out window) || window == 0) break;
                        if (values[0] == "R" && values.Length == 2) { leases.Remove(window); continue; }
                        uint process, thread;
                        long owner, root, listView;
                        if (values[0] != "D" || values.Length != 7 || (!leases.ContainsKey(window) && leases.Count >= 64) ||
                            !UInt32.TryParse(values[2], out process) || !UInt32.TryParse(values[3], out thread) ||
                            !Int64.TryParse(values[4], out owner) || !Int64.TryParse(values[5], out root) ||
                            !Int64.TryParse(values[6], out listView)) break;
                        // Validate again at restoration: windows can be destroyed or handles reused.
                        leases[window] = new Lease { Window = new IntPtr(window), Process = process, Thread = thread,
                            Owner = new IntPtr(owner), Root = new IntPtr(root), ListView = new IntPtr(listView) };
                    }
                }
            }
            catch { }
            finally { foreach (Lease lease in leases.Values) { try { lease.Restore(); } catch { } } }
            return true;
        }

        public void Dispose()
        {
            disposed = true; remembered.Clear(); Disconnect();
            if (worker != null)
            {
                // EOF lets the guard restore Explorer controls before the EXE is replaced.
                try { worker.WaitForExit(2000); } catch { }
                worker.Dispose(); worker = null;
            }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        private static bool WindowsInfoTipsEnabled()
        {
            uint flags;
            SHGetSettings(out flags, 0x00002000); // SSF_SHOWINFOTIP
            return (flags & (1u << 11)) != 0; // SHELLFLAGSTATE.fShowInfoTip
        }
        [DllImport("shell32.dll")] private static extern void SHGetSettings(out uint flags, uint mask);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
