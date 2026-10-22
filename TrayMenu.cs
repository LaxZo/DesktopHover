using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed class TrayMenu : ContextMenuStrip
    {
        private readonly Func<AppSettings> readSettings;
        private readonly Func<bool> readPaused;
        private readonly Action<AppSettings> applySettings;
        private readonly Font menuFont = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly ToolStripMenuItem open, enabled, size, delay, volume, upscale, startup, quit;
        private readonly ToolStripMenuItem[] sizes = new ToolStripMenuItem[4];
        private readonly ToolStripMenuItem[] delays = new ToolStripMenuItem[5];
        private readonly ToolStripMenuItem[] volumes = new ToolStripMenuItem[5];
        private static readonly int[] Widths = { 480, 720, 1080, 1440 };
        private static readonly int[] Heights = { 360, 540, 810, 1080 };
        private static readonly int[] Delays = { 0, 250, 450, 750, 1000 };
        private static readonly int[] Volumes = { 0, 25, 50, 75, 100 };
        private static readonly string[] SizeKeys = { "small", "medium", "large", "extra_large" };
        private static readonly string[] DelayKeys = { "instant", "fast", "natural", "slow", "one_second" };

        internal TrayMenu(Func<AppSettings> readSettings, Func<bool> readPaused,
            Action<AppSettings> applySettings, Action<bool> setPaused, Action showSettings, Action exit)
        {
            this.readSettings = readSettings;
            this.readPaused = readPaused;
            this.applySettings = applySettings;
            Font = menuFont;
            Style(this);
            open = AddItem(Items, "settings", delegate { showSettings(); });
            Items.Add(new ToolStripSeparator());
            enabled = AddItem(Items, "preview", delegate { setPaused(!readPaused()); RefreshState(); });
            size = AddItem(Items, "image-size", null);
            Style((ToolStripDropDownMenu)size.DropDown);
            for (int i = 0; i < sizes.Length; i++)
            {
                int index = i;
                sizes[i] = AddItem(size.DropDownItems, "size-" + Widths[i], delegate
                {
                    Change(delegate(AppSettings value) { value.MaxWidth = Widths[index]; value.MaxHeight = Heights[index]; });
                });
            }
            delay = AddItem(Items, "delay", null);
            Style((ToolStripDropDownMenu)delay.DropDown);
            for (int i = 0; i < delays.Length; i++)
            {
                int index = i;
                delays[i] = AddItem(delay.DropDownItems, "delay-" + Delays[i], delegate
                {
                    Change(delegate(AppSettings value) { value.DelayMs = Delays[index]; });
                });
            }
            volume = AddItem(Items, "volume", null);
            Style((ToolStripDropDownMenu)volume.DropDown);
            for (int i = 0; i < volumes.Length; i++)
            {
                int index = i;
                volumes[i] = AddItem(volume.DropDownItems, "volume-" + Volumes[i], delegate
                {
                    Change(delegate(AppSettings value) { value.VolumePercent = Volumes[index]; });
                });
            }
            upscale = AddItem(Items, "upscale", delegate { Change(delegate(AppSettings value) { value.Upscale = !value.Upscale; }); });
            startup = AddItem(Items, "startup", delegate { Change(delegate(AppSettings value) { value.StartWithWindows = !value.StartWithWindows; }); });
            Items.Add(new ToolStripSeparator());
            quit = AddItem(Items, "quit", delegate { exit(); });
            Opening += delegate { RefreshState(); };
            RefreshState();
        }

        private static ToolStripMenuItem AddItem(ToolStripItemCollection items, string name, Action action)
        {
            var item = new ToolStripMenuItem { Name = name, Padding = new Padding(2, 6, 2, 6) };
            if (action != null) item.Click += delegate { action(); };
            items.Add(item);
            return item;
        }

        private void Style(ToolStripDropDownMenu menu)
        {
            menu.Font = menuFont;
            menu.BackColor = Brand.Surface;
            menu.ForeColor = Brand.Text;
            menu.ShowImageMargin = false;
            menu.ShowCheckMargin = true;
            menu.DropShadowEnabled = false;
            menu.Padding = new Padding(5);
            menu.Renderer = new HoverMenuRenderer();
            menu.Opened += delegate
            {
                using (GraphicsPath path = Brand.RoundRect(new RectangleF(0, 0, menu.Width, menu.Height), 9))
                {
                    Region previous = menu.Region;
                    menu.Region = new Region(path);
                    if (previous != null) previous.Dispose();
                }
            };
        }

        private void Change(Action<AppSettings> edit)
        {
            AppSettings updated = readSettings().Clone();
            edit(updated);
            try { applySettings(updated); }
            catch (Exception error)
            {
                MessageBox.Show(L10n.Text(readSettings().Language, "save_error") + "\r\n\r\n" + error.Message,
                    "Desktop Hover", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RefreshState();
        }

        internal void RefreshState()
        {
            AppSettings current = readSettings();
            string language = current.Language;
            open.Text = L10n.Text(language, "settings");
            enabled.Text = L10n.Text(language, readPaused() ? "preview_paused" : "preview_on");
            enabled.Checked = !readPaused();
            size.Text = L10n.Text(language, "image_size");
            delay.Text = L10n.Text(language, "delay");
            volume.Text = L10n.Text(language, "volume");
            upscale.Text = L10n.Text(language, "upscale");
            upscale.Checked = current.Upscale;
            startup.Text = L10n.Text(language, "startup");
            startup.Checked = current.StartWithWindows;
            quit.Text = L10n.Text(language, "exit");
            for (int i = 0; i < sizes.Length; i++)
            {
                sizes[i].Text = L10n.Text(language, SizeKeys[i]) + "  ·  " + Widths[i] + " × " + Heights[i] + " px";
                sizes[i].Checked = current.MaxWidth == Widths[i] && current.MaxHeight == Heights[i];
            }
            for (int i = 0; i < delays.Length; i++)
            {
                delays[i].Text = L10n.Text(language, DelayKeys[i]);
                delays[i].Checked = current.DelayMs == Delays[i];
            }
            for (int i = 0; i < volumes.Length; i++)
            {
                volumes[i].Text = Volumes[i].ToString(L10n.Culture(language)) + "%";
                volumes[i].AccessibleName = String.Format(L10n.Culture(language),
                    L10n.Text(language, "volume_accessible"), Volumes[i]);
                volumes[i].Checked = current.VolumePercent == Volumes[i];
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) menuFont.Dispose();
        }
    }
}
