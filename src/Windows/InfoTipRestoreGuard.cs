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
            internal IntPtr Window, Owner, Root;
            internal uint Process, Thread;
            internal bool MatchesWindow()
            {
                uint process;
                return Window != IntPtr.Zero && Native.Class(Window) == "tooltips_class32" &&
                    Native.GetWindowThreadProcessId(Window, out process) == Thread && process == Process &&
                    GetWindow(Window, 4) == Owner && Native.IsShellRoot(Root) &&
                    Native.GetAncestor(Owner, 2) == Root;
            }
            internal bool Restore()
            {
                return !MatchesWindow() || PostMessage(Window, 0x0401, new IntPtr(1), IntPtr.Zero); // TTM_ACTIVATE
            }
        }

        private Process worker;
        private StreamWriter input;

        internal bool Remember(Lease lease)
        {
            try
            {
                if (worker == null)
                {
                    var start = new ProcessStartInfo(Application.ExecutablePath, "--infotip-restore-guard")
                    { UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true };
                    worker = Process.Start(start);
                    input = worker.StandardInput;
                    input.AutoFlush = true;
                }
                if (worker.HasExited) return false;
                input.WriteLine(String.Format(CultureInfo.InvariantCulture, "D {0} {1} {2} {3} {4}",
                    lease.Window.ToInt64(), lease.Process, lease.Thread, lease.Owner.ToInt64(), lease.Root.ToInt64()));
                return true;
            }
            catch { return false; }
        }

        internal void Forget(IntPtr window)
        {
            try { if (input != null) input.WriteLine("R " + window.ToInt64().ToString(CultureInfo.InvariantCulture)); }
            catch { }
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
                        long owner, root;
                        if (values[0] != "D" || values.Length != 6 || leases.Count >= 64 ||
                            !UInt32.TryParse(values[2], out process) || !UInt32.TryParse(values[3], out thread) ||
                            !Int64.TryParse(values[4], out owner) || !Int64.TryParse(values[5], out root)) break;
                        // Validate again at restoration: windows can be destroyed or handles reused.
                        leases[window] = new Lease { Window = new IntPtr(window), Process = process, Thread = thread,
                            Owner = new IntPtr(owner), Root = new IntPtr(root) };
                    }
                }
            }
            catch { }
            finally { foreach (Lease lease in leases.Values) { try { lease.Restore(); } catch { } } }
            return true;
        }

        public void Dispose()
        {
            try { if (input != null) input.Dispose(); } catch { }
            input = null;
            if (worker != null) { worker.Dispose(); worker = null; }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
    }
}
