using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed class AdvancedSettingsWindow : Form
    {
        private AppSettings settings;
        private readonly Action<string, bool> change;
        private readonly Action<MediaKind, bool> changeCategory;
        private readonly Action reset;
        private readonly Label title, hint, searchLabel, status;
        private readonly TextBox search;
        private readonly Panel formats;
        private readonly HoverSearchBox searchField;
        private readonly HoverScrollPanel formatHost;
        private readonly HoverButton allowAll;
        private readonly WindowCaptionButton close;
        private readonly Dictionary<string, CheckBox> checks = new Dictionary<string, CheckBox>();
        private readonly Dictionary<MediaKind, HoverToggle> sectionToggles = new Dictionary<MediaKind, HoverToggle>();
        private static readonly MediaKind[] kinds = { MediaKind.Image, MediaKind.Document, MediaKind.Text, MediaKind.Audio, MediaKind.Video };
        private static readonly string[] keys = { "type_images", "type_documents", "type_text", "type_audio", "type_video" };
        private readonly Font bodyFont, titleFont, sectionFont, buttonFont;
        private readonly float scale;
        private bool syncing, arranging;

        internal AdvancedSettingsWindow(AppSettings value, Action<string, bool> change, Action<MediaKind, bool> changeCategory, Action reset, float scale)
        {
            settings = value.Clone(); this.change = change; this.changeCategory = changeCategory; this.reset = reset; this.scale = scale;
            AutoScaleMode = AutoScaleMode.None; FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual; ShowInTaskbar = false;
            DoubleBuffered = true; KeyPreview = true;
            bodyFont = new Font("Segoe UI", 13 * scale, GraphicsUnit.Pixel);
            titleFont = new Font("Segoe UI", 17 * scale, FontStyle.Bold, GraphicsUnit.Pixel);
            sectionFont = new Font("Segoe UI", 13, FontStyle.Bold, GraphicsUnit.Pixel);
            buttonFont = new Font("Segoe UI", 13, FontStyle.Regular, GraphicsUnit.Pixel);
            Font = bodyFont;
            title = new Label { Font = titleFont, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            title.MouseDown += delegate(object sender, MouseEventArgs e)
            { if (e.Button == MouseButtons.Left) { ReleaseCapture(); SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero); } };
            hint = new Label { AutoEllipsis = true };
            searchLabel = new Label { TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            searchField = new HoverSearchBox(scale) { Name = "format-search-field" };
            search = searchField.Editor;
            formatHost = new HoverScrollPanel(scale) { Name = "format-scroll-host" };
            formats = formatHost.Content; formats.Name = "format-list";
            search.TextChanged += delegate { formats.AutoScrollPosition = Point.Empty; ArrangeFormats(); };
            formats.Resize += delegate { ArrangeFormats(); };
            status = new Label { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            allowAll = new HoverButton { Name = "allow-all-formats", Compact = true, UiScale = scale, Font = buttonFont };
            allowAll.Click += delegate { this.reset(); };
            close = new WindowCaptionButton(2) { Name = "close-advanced", UiScale = scale };
            close.Click += delegate { Close(); };
            Controls.AddRange(new Control[] { title, hint, searchLabel, searchField, formatHost, status, allowAll, close });
            for (int i = 0; i < kinds.Length; i++)
            {
                MediaKind kind = kinds[i];
                var toggle = new HoverToggle { Name = "format-section-" + kind.ToString().ToLowerInvariant(), Font = sectionFont, UiScale = scale };
                toggle.Click += delegate { if (!syncing) this.changeCategory(kind, !toggle.Checked); };
                toggle.Enter += delegate { formatHost.EnsureVisible(toggle); };
                sectionToggles.Add(kind, toggle); formats.Controls.Add(toggle);
            }
            foreach (string extension in PreviewFormats.Extensions)
            {
                string selected = extension;
                var box = new HoverCheckBox { Name = "allow" + extension, Text = extension, AccessibleName = extension, UiScale = scale };
                box.CheckedChanged += delegate { if (!syncing) this.change(selected, box.Checked); };
                box.Enter += delegate { formatHost.EnsureVisible(box); };
                SystemCursors.UseHand(box);
                checks.Add(extension, box); formats.Controls.Add(box);
            }
            ClientSize = new Size(Px(720), Px(640));
            UpdateSettings(settings);
        }

        private int Px(int value) { return (int)Math.Round(value * scale); }
        private string T(string key) { return L10n.Text(settings.Language, key); }

        internal void UpdateSettings(AppSettings value)
        {
            settings = value.Clone(); syncing = true;
            try
            {
                Text = AccessibleName = title.Text = T("advanced_settings");
                hint.Text = T("allowed_formats_hint");
                searchLabel.Text = search.AccessibleName = T("formats_search");
                searchField.ClearButton.AccessibleName = T("clear_search");
                formatHost.ScrollBar.AccessibleName = T("formats_scroll");
                formatHost.ScrollBar.UpName = T("scroll_up"); formatHost.ScrollBar.DownName = T("scroll_down");
                close.AccessibleName = T("close_advanced");
                allowAll.Text = allowAll.AccessibleName = T("allow_all_formats");
                allowAll.Enabled = settings.DisabledExtensions.Length > 0;
                status.Text = T("autosaved");
                WindowBackdrop.Apply(this, settings.Theme);
                BackColor = Brand.Background; ForeColor = Brand.Text;
                foreach (Control control in Controls) { control.BackColor = Brand.Background; control.ForeColor = Brand.Text; }
                searchField.RefreshTheme(); formatHost.RefreshTheme();
                hint.ForeColor = status.ForeColor = Brand.Muted;
                for (int i = 0; i < kinds.Length; i++)
                {
                    int total = 0, allowed = 0;
                    foreach (string extension in PreviewFormats.Extensions)
                        if (MediaFiles.Kind("preview" + extension) == kinds[i])
                        { total++; if (!settings.IsExtensionDisabled(extension)) allowed++; }
                    HoverToggle toggle = sectionToggles[kinds[i]];
                    toggle.Text = toggle.AccessibleName = T(keys[i]);
                    toggle.Checked = settings.Allows(kinds[i]);
                    toggle.Detail = toggle.Checked ? String.Format(L10n.Culture(settings.Language), T("formats_allowed_count"), allowed, total)
                        : T("formats_section_off");
                    toggle.AccessibleDescription = toggle.Detail; toggle.DefaultActionText = T("toggle");
                }
                foreach (var pair in checks)
                {
                    pair.Value.Checked = !settings.IsExtensionDisabled(pair.Key);
                    pair.Value.Enabled = settings.Allows(MediaFiles.Kind("preview" + pair.Key));
                    pair.Value.BackColor = Brand.Background; pair.Value.ForeColor = Brand.Text;
                    pair.Value.AccessibleDescription = hint.Text;
                }
                Invalidate(true);
            }
            finally { syncing = false; }
        }

        internal void ShowFor(Form owner)
        {
            Rectangle work = Screen.FromControl(owner).WorkingArea;
            Size = new Size(Math.Min(Px(720), work.Width - Px(24)), Math.Min(Px(640), work.Height - Px(24)));
            Location = new Point(Math.Max(work.Left, Math.Min(work.Right - Width, owner.Left + (owner.Width - Width) / 2)),
                Math.Max(work.Top, Math.Min(work.Bottom - Height, owner.Top + (owner.Height - Height) / 2)));
            Show(owner); search.Focus();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (formats == null) return;
            title.SetBounds(Px(22), Px(10), ClientSize.Width - Px(90), Px(34));
            close.SetBounds(ClientSize.Width - Px(54), Px(10), Px(38), Px(32));
            hint.SetBounds(Px(22), Px(58), ClientSize.Width - Px(44), Px(57));
            searchLabel.SetBounds(Px(22), Px(122), Px(160), Px(28));
            searchField.SetBounds(Px(186), Px(118), ClientSize.Width - Px(208), Px(40));
            formatHost.SetBounds(Px(22), Px(166), ClientSize.Width - Px(44), Math.Max(Px(80), ClientSize.Height - Px(230)));
            status.SetBounds(Px(22), ClientSize.Height - Px(48), Math.Max(1, ClientSize.Width - Px(300)), Px(32));
            allowAll.SetBounds(ClientSize.Width - Px(264), ClientSize.Height - Px(48), Px(242), Px(32));
            using (GraphicsPath path = Brand.RoundRect(new RectangleF(0, 0, Width, Height), Px(12)))
            { Region previous = Region; Region = new Region(path); if (previous != null) previous.Dispose(); }
            ArrangeFormats();
        }

        private void ArrangeFormats()
        {
            if (arranging || formats == null) return;
            arranging = true; formats.SuspendLayout();
            Point scroll = formats.AutoScrollPosition;
            try
            {
                formats.AutoScrollPosition = Point.Empty;
                string filter = search.Text.Trim();
                int available = Math.Max(1, formatHost.ViewportWidth);
                int columns = Math.Max(1, available / Px(132)), width = available / columns, y = 0, tab = 0;
                foreach (MediaKind kind in kinds)
                {
                    var visible = new List<CheckBox>();
                    foreach (string extension in PreviewFormats.Extensions)
                    {
                        if (MediaFiles.Kind("preview" + extension) != kind) continue;
                        CheckBox box = checks[extension];
                        bool matches = extension.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0;
                        box.Visible = matches;
                        if (matches) visible.Add(box);
                    }
                    HoverToggle heading = sectionToggles[kind]; heading.Visible = visible.Count > 0;
                    if (visible.Count == 0) continue;
                    heading.TabIndex = tab++;
                    heading.SetBounds(0, y, available - Px(8), Px(44)); y += Px(54);
                    for (int i = 0; i < visible.Count; i++)
                    {
                        visible[i].SetBounds((i % columns) * width, y + (i / columns) * Px(44), width - Px(8), Px(36));
                        visible[i].TabIndex = tab++;
                    }
                    y += ((visible.Count + columns - 1) / columns) * Px(44) + Px(20);
                }
                formats.AutoScrollMinSize = new Size(0, y);
                formats.AutoScrollPosition = new Point(0, -scroll.Y);
            }
            finally { formats.ResumeLayout(); arranging = false; formatHost.SyncScroll(); }
        }

        protected override void OnPaintBackground(PaintEventArgs e) { WindowBackdrop.Clear(e.Graphics, this); }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape) { Close(); return true; }
            if (keyData == (Keys.Control | Keys.F)) { search.Focus(); search.SelectAll(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void Dispose(bool disposing)
        { base.Dispose(disposing); if (disposing) { bodyFont.Dispose(); titleFont.Dispose(); sectionFont.Dispose(); buttonFont.Dispose(); } }
        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr window, int message, IntPtr wParam, IntPtr lParam);
    }
}
