using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static class AppLog
    {
        private static readonly object gate = new object();
        private static int initialized;
        internal const int MaximumBytes = 1024 * 1024;
        internal static string DirectoryPath
        { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DesktopHover", "logs"); } }

        internal static void Initialize()
        {
            if (Interlocked.Exchange(ref initialized, 1) != 0) return;
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            { Write("Unhandled exception; terminating=" + e.IsTerminating, e.ExceptionObject as Exception); };
            TaskScheduler.UnobservedTaskException += delegate(object sender, UnobservedTaskExceptionEventArgs e)
            { Write("Unobserved background task", e.Exception); };
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                Write("UI thread", e.Exception);
                // Keep WinForms' familiar Continue/Quit choice; logging must not
                // silently swallow the error or restart the app behind the user.
                using (var dialog = new ThreadExceptionDialog(e.Exception))
                    if (dialog.ShowDialog() == DialogResult.Abort) Application.Exit();
            };
        }

        internal static void Write(string context, Exception error, string directory = null)
        {
            try
            {
                string detail = error == null ? "No exception details available." : error.ToString();
                if (detail.Length > 16000) detail = detail.Substring(0, 16000) + "\r\n[truncated]";
                int processId;
                using (var process = Process.GetCurrentProcess()) processId = process.Id;
                string entry = DateTime.UtcNow.ToString("o") + " | Desktop Hover " + AppInfo.Version +
                    " | PID " + processId + " | " + context + "\r\n" + detail + "\r\n\r\n";
                byte[] bytes = new UTF8Encoding(false).GetBytes(entry);
                lock (gate)
                {
                    directory = directory ?? DirectoryPath;
                    Directory.CreateDirectory(directory);
                    string path = Path.Combine(directory, "desktop-hover.log");
                    if (File.Exists(path) && new FileInfo(path).Length + bytes.Length > MaximumBytes)
                    {
                        string first = path + ".1", second = path + ".2";
                        if (File.Exists(second)) File.Delete(second);
                        if (File.Exists(first)) File.Move(first, second);
                        File.Move(path, first);
                    }
                    using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite))
                        stream.Write(bytes, 0, bytes.Length);
                }
            }
            catch { /* Diagnostics must never prevent previewing, saving or shutting down. */ }
        }
    }
}
