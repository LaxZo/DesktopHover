using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DesktopHover
{
    internal static class Program
    {
        internal static string StatusFile;
        internal static readonly EventWaitHandle OpenSettingsEvent=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\DesktopHover_OpenSettings");
        [STAThread] static void Main(string[] args)
        {
            if (InfoTipRestoreGuard.TryRunWorker(args)) return;
            if (DocumentPreviewWorker.TryRunWorker(args)) return;
            try { Native.SetProcessDpiAwarenessContext(new IntPtr(-4)); }
            catch { Native.SetProcessDPIAware(); }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if(args.Length==2 && args[0]=="--status-file") StatusFile=args[1];
            if(args.Length==1 && (args[0]=="--enable-startup" || args[0]=="--disable-startup"))
            { Startup.SetEnabled(args[0]=="--enable-startup"); return; }
            if(args.Length==3 && args[0]=="--render-test")
            {
                using(var f=new PreviewWindow())
                {
                    if(!f.ShowImage(args[1],new Point(300,300),new Rectangle(280,280,80,80)))
                        throw new InvalidOperationException("Image render failed.");
                    using(var b=new Bitmap(f.Width,f.Height))
                    { f.DrawToBitmap(b,new Rectangle(Point.Empty,b.Size)); b.Save(args[2],System.Drawing.Imaging.ImageFormat.Png); }
                    // A preview must not retain a file handle that blocks normal file operations.
                    using(var check=new FileStream(args[1],FileMode.Open,FileAccess.ReadWrite,FileShare.None)) { }
                    f.ClearPreview();
                }
                return;
            }
            if(args.Length==4 && args[0]=="--probe")
            {
                try
                {
                    using(var resolver=new ShellResolver())
                    {
                        var item=resolver.Resolve(new Point(int.Parse(args[1]),int.Parse(args[2])));
                        File.WriteAllText(args[3],(item==null ? "NO_IMAGE" : item.Path+"\r\n"+item.Bounds)+"\r\n"+resolver.Diagnostic);
                    }
                }
                catch(Exception e) { File.WriteAllText(args[3],e.ToString()); }
                return;
            }
            if(args.Length==2 && args[0]=="--preview-test")
            {
                using(var f=new PreviewWindow())
                {
                    bool ok=f.ShowImage(args[1],new Point(300,300),new Rectangle(280,280,80,80));
                    File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"preview-test.txt"),ok?"PASS":"FAIL");
                    var t=new System.Windows.Forms.Timer {Interval=5000};
                    t.Tick+=delegate { t.Stop(); f.Close(); Application.ExitThread(); };
                    t.Start(); Application.Run();
                }
                return;
            }
            bool created;
            using(var mutex=new Mutex(true,@"Local\DesktopHover_Nauri",out created))
            {
                if(!created) { OpenSettingsEvent.Set(); return; }
                using(var context=new HoverContext())
                {
                    if(args.Length==1 && args[0]=="--settings") OpenSettingsEvent.Set();
                    Application.Run(context);
                }
            }
        }
    }

    internal sealed class HoverContext : ApplicationContext
    {
        private readonly PreviewWindow preview=new PreviewWindow();
        private readonly NotifyIcon tray;
        private readonly Icon trayIcon;
        private readonly TrayMenu trayMenu;
        private readonly ShellInfoTipSuppressor infoTips;
        private AppSettings settings;
        private SettingsWindow settingsWindow;
        private readonly System.Windows.Forms.Timer timer;
        private readonly AutoResetEvent request=new AutoResetEvent(false);
        private readonly object gate=new object();
        private readonly Thread worker;
        private volatile bool stopping;
        private bool paused, busy;
        private Point last, requestedPoint;
        private IntPtr lastSurface;
        private int generation, requestGeneration, resultGeneration;
        private HoverItem result, shown;
        private bool resultReady;
        private DateTime moved=DateTime.UtcNow, lastCheck=DateTime.MinValue;
        private int delay=450;
        internal HoverContext()
        {
            // Create the handle on the UI thread before the worker starts.
            IntPtr ignored=preview.Handle;
            settings=AppSettings.Load();
            settings.StartWithWindows=Startup.IsEnabled();
            delay=settings.DelayMs;
            preview.Configure(settings);
            trayIcon=Brand.CreateIcon();
            tray=new NotifyIcon {Icon=trayIcon,Visible=true};
            trayMenu=new TrayMenu(
                delegate { settings.StartWithWindows=Startup.IsEnabled(); return settings.Clone(); },
                delegate { return paused; }, ApplySettings, SetPaused, ShowSettings, delegate { ExitThread(); });
            trayMenu.Opening+=delegate { Reset(); };
            trayMenu.Closed+=delegate { if(!stopping) Reset(); };
            tray.ContextMenuStrip=trayMenu;
            infoTips=new ShellInfoTipSuppressor(delegate
            {
                return !stopping && !paused && !trayMenu.Visible && !Native.ButtonsDown() &&
                    (Native.GetAsyncKeyState(27)&0x8000)==0;
            });
            UpdateTrayText();
            tray.MouseClick+=delegate(object sender,MouseEventArgs e)
            { if(e.Button==MouseButtons.Left) ShowSettings(); };
            worker=new Thread(Work){IsBackground=true,Name="Desktop Hover shell resolver"};
            worker.SetApartmentState(ApartmentState.STA);
            worker.Start();
            timer=new System.Windows.Forms.Timer {Interval=75};
            timer.Tick+=Tick;
            timer.Start();
        }
        private void ShowSettings()
        {
            Reset();
            if(settingsWindow==null || settingsWindow.IsDisposed)
            {
                settings.StartWithWindows=Startup.IsEnabled();
                settingsWindow=new SettingsWindow(settings.Clone(),paused,ApplySettings,
                    SetPaused);
            }
            settingsWindow.UpdatePaused(paused);
            settingsWindow.ShowCentered(Cursor.Position);
        }
        private void SetPaused(bool value)
        {
            paused=value;
            Reset();
            UpdateTrayText();
            if(settingsWindow!=null && !settingsWindow.IsDisposed) settingsWindow.UpdatePaused(paused);
        }
        private void ApplySettings(AppSettings updated)
        {
            updated.Validate();
            bool previousStartup=Startup.IsEnabled();
            try
            {
                if(previousStartup!=updated.StartWithWindows) Startup.SetEnabled(updated.StartWithWindows);
                updated.Save();
            }
            catch
            {
                try { if(previousStartup!=Startup.IsEnabled()) Startup.SetEnabled(previousStartup); } catch { }
                throw;
            }
            settings=updated.Clone();
            delay=settings.DelayMs;
            preview.Configure(settings);
            UpdateTrayText();
            Reset();
            if(settingsWindow!=null && !settingsWindow.IsDisposed) settingsWindow.UpdateSettings(settings);
        }
        private void UpdateTrayText()
        {
            tray.Text="Desktop Hover — "+L10n.Text(settings.Language,paused?"tray_paused":"tray_active");
        }
        private void Reset()
        {
            generation++; shown=null; infoTips.Clear(); preview.ClearPreview(); moved=DateTime.UtcNow;
            lastCheck=DateTime.MinValue;
        }
        private void Tick(object sender,EventArgs e)
        {
            if(Program.OpenSettingsEvent.WaitOne(0)) ShowSettings();
            Native.POINT p; if(!Native.GetCursorPos(out p)) return;
            Point now=new Point(p.X,p.Y);
            if(paused || trayMenu.Visible || Native.ButtonsDown() || (Native.GetAsyncKeyState(27)&0x8000)!=0)
            { Reset(); last=now; return; }
            IntPtr surface=Native.WindowFromPoint(p);
            IntPtr root=Native.GetAncestor(surface,2);
            bool isShell=Native.IsShellRoot(root);
            // A Windows infotip can cover the pointer even before our preview's delay expires.
            // Dismiss it and let the next tick resolve the file through the exposed Explorer view.
            if(!isShell && infoTips.DismissAtCursor(surface,now)) return;
            if(!isShell) { Reset(); last=now; return; }
            bool motion=Math.Abs(now.X-last.X)>3 || Math.Abs(now.Y-last.Y)>3;
            if(motion)
            {
                // Small moves within the same file must not restart audio, video or animated GIFs.
                if(!preview.IsMediaPreview || shown==null || !shown.Bounds.Contains(now)) Reset();
                last=now;
            }
            if(surface!=lastSurface) { Reset(); lastSurface=surface; }
            infoTips.Track(surface,root,now);
            if(shown!=null)
            {
                if(!shown.Bounds.Contains(now)) { Reset(); return; }
            }
            lock(gate)
            {
                if(resultReady)
                {
                    resultReady=false;
                    if(resultGeneration==generation)
                    {
                        if(result!=null && result.Bounds.Contains(now) && Native.IsShellRoot(result.Root))
                        {
                            if(root!=result.Root || surface!=result.Surface) { preview.ClearPreview(); shown=null; return; }
                            if(shown==null || !string.Equals(shown.Path,result.Path,StringComparison.OrdinalIgnoreCase))
                            {
                                    if(preview.ShowFile(result.Path,now,result.Bounds)) shown=result;
                                else shown=null;
                                if(Program.StatusFile!=null)
                                    try { File.WriteAllText(Program.StatusFile,shown==null?"RENDER_FAILED":"SHOWN "+shown.Path+"\r\n"+preview.Bounds); } catch { }
                            }
                            else shown=result;
                        }
                        else if(shown!=null) { preview.ClearPreview(); shown=null; }
                    }
                }
                if(!busy && (DateTime.UtcNow-moved).TotalMilliseconds>=delay &&
                   (DateTime.UtcNow-lastCheck).TotalMilliseconds>=(shown==null?600:1500))
                {
                    busy=true; requestedPoint=now; requestGeneration=generation; lastCheck=DateTime.UtcNow; request.Set();
                }
            }
        }
        private void Work()
        {
            using(var resolver=new ShellResolver())
            while(!stopping)
            {
                request.WaitOne();
                if(stopping) break;
                Point point; int g;
                lock(gate) { point=requestedPoint; g=requestGeneration; }
                HoverItem item=null;
                try { item=resolver.Resolve(point); }
                catch(Exception error)
                {
                    if(Program.StatusFile!=null) try { File.AppendAllText(Program.StatusFile,error.ToString()+"\r\n"); } catch { }
                }
                if(Program.StatusFile!=null) try { File.AppendAllText(Program.StatusFile,"Resolved "+(item==null?"NONE":item.Path)+"\r\n"+resolver.Diagnostic); } catch { }
                lock(gate) { result=item; resultGeneration=g; resultReady=true; busy=false; }
            }
        }
        protected override void ExitThreadCore()
        {
            stopping=true; infoTips.Dispose(); request.Set(); timer.Stop(); tray.Visible=false; preview.Close();
            if(settingsWindow!=null) settingsWindow.Close();
            base.ExitThreadCore();
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing)
            {
                stopping=true; infoTips.Dispose(); request.Set(); timer.Dispose(); tray.Dispose(); trayMenu.Dispose(); trayIcon.Dispose(); preview.Dispose();
                if(settingsWindow!=null) settingsWindow.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    internal static class Startup
    {
        private const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        internal static bool IsEnabled()
        {
            using(var key=Registry.CurrentUser.OpenSubKey(Key))
                return key!=null && string.Equals(key.GetValue("DesktopHover") as string,Command(),StringComparison.OrdinalIgnoreCase);
        }
        private static string Command() { return "\""+Application.ExecutablePath+"\""; }
        internal static void SetEnabled(bool value)
        {
            using(var key=Registry.CurrentUser.CreateSubKey(Key))
            { if(value) key.SetValue("DesktopHover",Command()); else key.DeleteValue("DesktopHover",false); }
        }
    }
}
