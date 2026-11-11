using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Windows.UI.ViewManagement;

namespace DesktopHover
{
    internal static partial class Regression
    {
        private static void WindowsAccentPreferences()
        {
            string path = Path.Combine(output, "accent.ini");
            File.WriteAllText(path, "theme=dark\r\nlanguage=lv\r\n");
            AppSettings settings = AppSettings.Load(path);
            Check(!settings.UseWindowsAccent, "older preferences retain the pink accent");
            var os = new UISettings().GetColorValue(UIColorType.Accent);
            Color expected = Color.FromArgb(os.R, os.G, os.B);
            foreach (string theme in new[] { "dark", "light", "glass" })
            {
                settings.Theme = theme;
                Brand.SetTheme(theme);
                Color pink = Brand.Accent, originalSelection = Brand.SelectedSurface;
                using (var window = new SettingsWindow(settings, false, value => { settings = value; settings.Save(path); }, null))
                {
                    var menu = Field<ContextMenuStrip>(window, "themeMenu");
                    var option = (ToolStripMenuItem)menu.Items["use-windows-accent"];
                    option.PerformClick();
                    Check(option.Checked && AppSettings.Load(path).UseWindowsAccent && Brand.AccentFill == expected &&
                        ((ToolStripMenuItem)menu.Items["theme-" + theme]).Checked, "settings use the saved Windows color independently of the " + theme + " theme");
                    Check(OfficePreviewPalette.Capture().Accent == Brand.Accent, "Office captures the accent used by the interface");
                    option.PerformClick();
                    Check(!option.Checked && !AppSettings.Load(path).UseWindowsAccent && settings.Theme == theme &&
                        Brand.Accent == pink && Brand.SelectedSurface == originalSelection, "turning the option off restores the original " + theme + " palette");
                }
                Color extreme = theme == "light" ? Color.White : Color.Black;
                Brand.SetPalette(theme, extreme);
                Check(Brand.AccentFill == extreme && Brand.Accent.GetBrightness() != extreme.GetBrightness() &&
                    Brand.AccentMark(Color.Gray).ToArgb() != extreme.ToArgb(), "text and marks stay distinct with extreme accent colors");
            }
            using (var tray = new TrayMenu(() => settings, () => false, value => settings = value, delegate { }, delegate { }, delegate { }))
            {
                var choices = ((ToolStripMenuItem)tray.Items["theme"]).DropDownItems;
                var accent = (ToolStripMenuItem)choices["use-windows-accent"];
                accent.PerformClick(); choices["theme-light"].PerformClick();
                Check(accent.Checked && settings.UseWindowsAccent && Brand.IsLight && Brand.AccentFill == expected, "changing the theme in the tray retains the accent option");
                accent.PerformClick();
                Check(!accent.Checked && !settings.UseWindowsAccent, "the tray can independently turn off Windows colors");
            }
            using (var context = new HoverContext())
            {
                Field<System.Windows.Forms.Timer>(context, "timer").Stop();
                settings.UseWindowsAccent = true;
                typeof(HoverContext).GetField("settings", Private).SetValue(context, settings);
                Call(context, "WatchWindowsAccent", true);
                Check(Field<UISettings>(context, "accentSource") != null, "live Windows color notifications subscribe on enable");
                var preview = Field<PreviewWindow>(context, "preview");
                string file = Path.Combine(output, "accent-preview.txt");
                File.WriteAllText(file, "Accent preview");
                typeof(HoverContext).GetField("lastPreviewPath", Private).SetValue(context, file);
                Call(context, "PinPreview");
                var pinned = Field<PreviewWindow>(context, "pinnedWindow");
                preview.Configure(new AppSettings()); pinned.Configure(new AppSettings());
                Action notify = delegate { Call(context, "WindowsColorsChanged", WindowsAccent.Source, null); };
                var worker = new Thread(new ThreadStart(notify)); worker.Start(); worker.Join();
                Until(() => Field<Color>(pinned, "appliedWindowsAccent") == expected && Field<Color>(preview, "appliedWindowsAccent") == expected, 3000);
                notify();
                settings.UseWindowsAccent = false; Call(context, "WatchWindowsAccent", false);
                preview.Configure(settings); pinned.Configure(settings); Pump(50);
                Check(Field<UISettings>(context, "accentSource") == null && Brand.WindowsAccentColor.IsEmpty &&
                    Field<Color>(pinned, "appliedWindowsAccent").IsEmpty, "queued color changes cannot re-enable a disabled accent");
                context.ExitThread();
            }
            Brand.SetTheme("dark");
        }
    }
}
