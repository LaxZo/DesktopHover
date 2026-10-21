using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;

namespace DesktopHover
{
    internal sealed class HoverItem
    {
        public string Path;
        public Rectangle Bounds;
        public IntPtr Root;
        public IntPtr Surface;
    }

    internal static class Native
    {
        [StructLayout(LayoutKind.Sequential)] internal struct POINT { public int X, Y; }
        [DllImport("user32.dll")] internal static extern bool GetCursorPos(out POINT p);
        [DllImport("user32.dll")] internal static extern IntPtr WindowFromPoint(POINT p);
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr h, uint flags);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern int GetClassName(IntPtr h, StringBuilder text, int length);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string cls, string title);
        [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
        [DllImport("user32.dll")] internal static extern bool SetProcessDpiAwarenessContext(IntPtr value);
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
        internal static string Class(IntPtr h) { var b = new StringBuilder(256); GetClassName(h,b,256); return b.ToString(); }
        internal static bool IsShellRoot(IntPtr root)
        {
            string c = Class(root);
            if(c!="CabinetWClass" && c!="ExploreWClass" && c!="ExplorerWClass" && c!="Progman" && c!="WorkerW") return false;
            uint pid; GetWindowThreadProcessId(root,out pid);
            try { using(var p=Process.GetProcessById((int)pid)) return p.ProcessName.Equals("explorer",StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }
        internal static bool ButtonsDown()
        {
            return (GetAsyncKeyState(1)&0x8000)!=0 || (GetAsyncKeyState(2)&0x8000)!=0 || (GetAsyncKeyState(4)&0x8000)!=0;
        }
    }

    [ComImport, Guid("6D5140C1-7436-11CE-8034-00AA006009FA"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellServiceProvider
    {
        [PreserveSig] int QueryService(ref Guid service, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out object result);
    }

    // Only IOleWindow's first method is needed; the queried object is IShellBrowser.
    [ComImport, Guid("000214E2-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellBrowserWindow
    {
        [PreserveSig] int GetWindow(out IntPtr window);
    }

    internal sealed class ShellResolver : IDisposable
    {
        private object shell;
        internal string Diagnostic="";
        internal static bool Supported(string path)
        {
            return MediaFiles.Kind(path)!=MediaKind.None;
        }
        private object Shell { get { if(shell==null) shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")); return shell; } }
        internal HoverItem Resolve(Point point)
        {
            var p=new Native.POINT {X=point.X,Y=point.Y};
            IntPtr sourceSurface=Native.WindowFromPoint(p);
            IntPtr root=Native.GetAncestor(sourceSurface,2);
            Diagnostic="root="+root+" "+Native.Class(root)+"\r\n";
            if(!Native.IsShellRoot(root)) return null;
            AutomationElement item=AutomationElement.FromPoint(new System.Windows.Point(point.X,point.Y));
            bool inView=false;
            AutomationElement candidate=null;
            IntPtr hoveredTab=IntPtr.Zero;
            for(int i=0;i<20 && item!=null;i++)
            {
                var info=item.Current;
                if(info.ClassName=="ShellTabWindowClass") hoveredTab=new IntPtr(info.NativeWindowHandle);
                Diagnostic+="element "+info.Name+" "+info.ClassName+" "+info.ControlType.ProgrammaticName+"\r\n";
                if(candidate==null && (info.ControlType==ControlType.ListItem || info.ControlType==ControlType.DataItem)) candidate=item;
                if(info.ClassName=="SHELLDLL_DefView" || info.ClassName=="DirectUIHWND" || info.ClassName=="SysListView32" ||
                   info.ClassName=="UIItemsView" || info.ClassName=="DUIListView") inView=true;
                if(info.NativeWindowHandle==root.ToInt64()) break;
                item=TreeWalker.ControlViewWalker.GetParent(item);
            }
            if(candidate==null || !inView) return null;
            var c=candidate.Current;
            var r=c.BoundingRectangle;
            if(c.IsOffscreen || r.IsEmpty || !r.Contains(point.X,point.Y)) return null;
            string name=c.Name;
            if(string.IsNullOrWhiteSpace(name)) return null;
            string rootClass=Native.Class(root);
            string path=(rootClass=="Progman" || rootClass=="WorkerW") ? ResolveDesktop(name) : ResolveExplorer(root,name,hoveredTab);
            Diagnostic+="path="+path+"\r\n";
            if(path==null || !Supported(path) || !File.Exists(path)) return null;
            // Do not hydrate online-only cloud files or open network content merely by hovering.
            if(path.StartsWith(@"\\",StringComparison.Ordinal)) return null;
            try
            {
                var drive=new DriveInfo(System.IO.Path.GetPathRoot(path));
                if(drive.DriveType==DriveType.Network) return null;
                int attrs=(int)File.GetAttributes(path);
                if((attrs & (0x1000 | 0x40000 | 0x400000)) != 0) return null;
            }
            catch { return null; }
            return new HoverItem {Path=path,Root=root,Surface=sourceSurface,Bounds=Rectangle.FromLTRB((int)r.Left,(int)r.Top,(int)r.Right,(int)r.Bottom)};
        }
        private string ResolveDesktop(string name)
        {
            object folder=null;
            try { folder=((dynamic)Shell).NameSpace(0); return FindUnique(folder,name); }
            finally { Release(folder); }
        }
        private string ResolveExplorer(IntPtr root,string name,IntPtr hoveredTab)
        {
            object windows=null;
            IntPtr activeTab=hoveredTab!=IntPtr.Zero ? hoveredTab : Native.FindWindowEx(root,IntPtr.Zero,"ShellTabWindowClass",null);
            Diagnostic+="activeTab="+activeTab+"\r\n";
            try
            {
                windows=((dynamic)Shell).Windows();
                int count=((dynamic)windows).Count;
                for(int i=0;i<count;i++)
                {
                    object window=null, browser=null, document=null, folder=null;
                    try
                    {
                        window=((dynamic)windows).Item(i);
                        if(window==null) continue;
                        Guid id=new Guid("000214E2-0000-0000-C000-000000000046");
                        var service=window as IShellServiceProvider;
                        if(service==null || service.QueryService(ref id,ref id,out browser)<0) { Diagnostic+="no browser service\r\n"; continue; }
                        IntPtr hwnd;
                        if(((IShellBrowserWindow)browser).GetWindow(out hwnd)<0) continue;
                        Diagnostic+="browser hwnd="+hwnd+"\r\n";
                        // Windows 11 exposes one browser per tab. Match the visible tab, not the first folder with this HWND.
                        if(activeTab!=IntPtr.Zero ? hwnd!=activeTab : hwnd!=root) continue;
                        document=((dynamic)window).Document;
                        folder=((dynamic)document).Folder;
                        return FindUnique(folder,name);
                    }
                    catch(COMException e) { Diagnostic+=e.Message+"\r\n"; }
                    finally { Release(folder); Release(document); Release(browser); Release(window); }
                }
            }
            finally { Release(windows); }
            return null;
        }
        private static string FindUnique(object folder,string name)
        {
            if(folder==null) return null;
            object items=null;
            string match=null;
            try
            {
                items=((dynamic)folder).Items();
                int count=((dynamic)items).Count;
                if(count>20000) return null;
                for(int i=0;i<count;i++)
                {
                    object item=null;
                    try
                    {
                        item=((dynamic)items).Item(i);
                        string display=((dynamic)item).Name;
                        string path=((dynamic)item).Path;
                        if(!string.Equals(display,name,StringComparison.OrdinalIgnoreCase) &&
                           !string.Equals(System.IO.Path.GetFileName(path),name,StringComparison.OrdinalIgnoreCase)) continue;
                        if(!(bool)((dynamic)item).IsFileSystem || (bool)((dynamic)item).IsFolder || !Supported(path)) return null;
                        if(match!=null && !string.Equals(match,path,StringComparison.OrdinalIgnoreCase)) return null;
                        match=path;
                    }
                    finally { Release(item); }
                }
            }
            finally { Release(items); }
            return match;
        }
        private static void Release(object o) { if(o!=null && Marshal.IsComObject(o)) Marshal.ReleaseComObject(o); }
        public void Dispose() { Release(shell); shell=null; }
    }
}
