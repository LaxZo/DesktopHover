using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    // Main settings window, opened from the tray icon or quick settings menu.
    internal sealed class SettingsWindow : Form
    {
        private AppSettings settings, lastApplied;
        private readonly Action<AppSettings> changed;
        private readonly Action<bool> pauseChanged;
        private readonly Action exitRequested;
        private readonly HoverButton[] presets = new HoverButton[4];
        private readonly HoverButton[] delays = new HoverButton[5];
        private readonly HoverButton languagePicker, themeToggle;
        private readonly ContextMenuStrip languageMenu;
        private readonly HoverButton minimize, close;
        private readonly ToolTip windowToolTip = new ToolTip();
        private readonly HoverSlider widthSlider, volumeSlider;
        private readonly HoverToggle upscaleToggle, startupToggle, enabledToggle;
        private bool paused, syncing;
        private readonly Font headingFont = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font titleFont = new Font("Segoe UI", 21, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font smallFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font bodyFont = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Icon windowIcon;
        private static readonly int[] SizeWidths = { 480, 720, 1080, 1440 };
        private static readonly int[] SizeHeights = { 360, 540, 810, 1080 };
        private static readonly int[] DelayValues = { 0, 250, 450, 750, 1000 };
        private static readonly string[] SizeKeys = { "small", "medium", "large", "extra_large" };
        private static readonly string[] DelayKeys = { "instant", "fast", "natural", "slow", "one_second" };

        public SettingsWindow(AppSettings settings, bool paused,
            Action<AppSettings> changed, Action<bool> pauseChanged, Action exitRequested)
        {
            this.settings = settings.Clone();
            lastApplied = this.settings.Clone();
            this.paused = paused;
            this.changed = changed;
            this.pauseChanged = pauseChanged;
            this.exitRequested = exitRequested;
            AutoScaleMode = AutoScaleMode.None;
            ClientSize = new Size(424, 768);
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            MaximizeBox = false;
            MinimizeBox = false;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Brand.Background;
            ForeColor = Brand.Text;
            Font = bodyFont;
            windowIcon = Brand.CreateIcon();
            Icon = windowIcon;

            minimize = new HoverButton { Name = "minimize-settings", Text = "\u2212", Compact = true };
            minimize.SetBounds(330, 20, 32, 32);
            minimize.Click += delegate { Close(); };
            Controls.Add(minimize);

            close = new HoverButton { Name = "quit-app", Text = "\u00d7", Compact = true };
            close.SetBounds(368, 20, 32, 32);
            close.Click += QuitApp;
            Controls.Add(close);

            themeToggle = new HoverButton { Name = "theme-toggle", Compact = true, ThemeSelector = true };
            themeToggle.SetBounds(242, 716, 40, 32);
            themeToggle.Click += delegate
            {
                this.settings.Theme = Brand.IsLight ? "dark" : "light";
                Apply();
            };
            Controls.Add(themeToggle);

            languagePicker = new HoverButton
            {
                Name = "language-picker",
                Compact = true,
                LanguageSelector = true,
                Font = smallFont
            };
            languagePicker.SetBounds(290, 716, 110, 32);
            languageMenu = new ContextMenuStrip
            {
                Name = "language-menu",
                Font = bodyFont,
                BackColor = Brand.Surface,
                ForeColor = Brand.Text,
                ShowImageMargin = false,
                ShowCheckMargin = true,
                DropShadowEnabled = false,
                Padding = new Padding(5),
                Renderer = new HoverMenuRenderer()
            };
            foreach (L10n.LanguageOption language in L10n.Languages)
            {
                string code = language.Code;
                var item = new ToolStripMenuItem(language.NativeName)
                {
                    Name = "language-" + code,
                    Tag = code,
                    AutoSize = false,
                    Size = new Size(272, 30)
                };
                item.Click += delegate { this.settings.Language = code; Apply(); };
                languageMenu.Items.Add(item);
            }
            languageMenu.AutoSize = false;
            languageMenu.Size = new Size(240, languageMenu.Items.Count * 30 + languageMenu.Padding.Vertical);
            languageMenu.Opened += delegate
            {
                languagePicker.Selected = true;
                using (GraphicsPath path = Brand.RoundRect(new RectangleF(0, 0, languageMenu.Width, languageMenu.Height), 9))
                {
                    Region oldRegion = languageMenu.Region;
                    languageMenu.Region = new Region(path);
                    if (oldRegion != null) oldRegion.Dispose();
                }
                foreach (ToolStripMenuItem item in languageMenu.Items)
                    if (item.Checked) { item.Select(); break; }
            };
            languageMenu.Closed += delegate { languagePicker.Selected = false; };
            languagePicker.Click += delegate { ShowLanguageMenu(); };

            for (int i = 0; i < presets.Length; i++)
            {
                int index = i;
                var button = new HoverButton
                {
                    Detail = SizeWidths[i] + " \u00d7 " + SizeHeights[i] + " px"
                };
                button.SetBounds(24 + (i % 2) * 194, 172 + (i / 2) * 54, 182, 46);
                button.Click += delegate
                {
                    this.settings.MaxWidth = SizeWidths[index];
                    this.settings.MaxHeight = SizeHeights[index];
                    Apply();
                };
                Controls.Add(button);
                presets[i] = button;
            }

            widthSlider = new HoverSlider
            {
                Minimum = 320,
                Maximum = 1600,
                Step = 40
            };
            widthSlider.SetBounds(24, 286, 376, 30);
            widthSlider.ValueChanged += delegate
            {
                if (syncing) return;
                this.settings.MaxWidth = widthSlider.Value;
                this.settings.MaxHeight = widthSlider.Value * 3 / 4;
                Apply();
            };
            Controls.Add(widthSlider);

            for (int i = 0; i < delays.Length; i++)
            {
                int index = i;
                var button = new HoverButton
                {
                    Name = "delay-" + DelayValues[i],
                    Compact = true,
                    Font = smallFont
                };
                button.SetBounds(24 + i * 76, 397, 72, 36);
                button.Click += delegate { this.settings.DelayMs = DelayValues[index]; Apply(); };
                Controls.Add(button);
                delays[i] = button;
            }

            volumeSlider = new HoverSlider
            {
                Name = "volume-slider",
                Minimum = 0,
                Maximum = 100,
                Step = 1,
                Value = 100
            };
            volumeSlider.SetBounds(24, 494, 376, 30);
            volumeSlider.ValueChanged += delegate
            {
                if (syncing) return;
                this.settings.VolumePercent = volumeSlider.Value;
                Apply();
            };
            Controls.Add(volumeSlider);

            upscaleToggle = AddToggle(533);
            upscaleToggle.Click += delegate { this.settings.Upscale = !this.settings.Upscale; Apply(); };
            startupToggle = AddToggle(587);
            startupToggle.Click += delegate { this.settings.StartWithWindows = !this.settings.StartWithWindows; Apply(); };
            enabledToggle = AddToggle(641);
            enabledToggle.Click += delegate
            {
                UpdatePaused(!this.paused);
                if (this.pauseChanged != null) this.pauseChanged(this.paused);
            };

            Controls.Add(languagePicker);
            SyncControls();
        }

        private void QuitApp(object sender, EventArgs e)
        {
            if (exitRequested != null) exitRequested();
        }

        private void ShowLanguageMenu()
        {
            if (languageMenu.Visible) { languageMenu.Close(); return; }
            languageMenu.Show(languagePicker, new Point(languagePicker.Width, -5), ToolStripDropDownDirection.AboveLeft);
        }

        private HoverToggle AddToggle(int y)
        {
            var toggle = new HoverToggle();
            toggle.SetBounds(24, y, 376, 48);
            Controls.Add(toggle);
            return toggle;
        }

        private void Apply()
        {
            SyncControls();
            try
            {
                if (changed != null) changed(settings.Clone());
                lastApplied = settings.Clone();
            }
            catch (Exception error)
            {
                settings = lastApplied.Clone();
                SyncControls();
                MessageBox.Show(this, T("save_error") + "\r\n\r\n" + error.Message,
                    "Desktop Hover", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // Also permits the owner to restore values if persistence was unsuccessful.
        public void UpdateSettings(AppSettings value)
        {
            settings = value.Clone();
            lastApplied = settings.Clone();
            SyncControls();
        }

        public void UpdatePaused(bool value)
        {
            paused = value;
            SyncControls();
        }

        private void SyncControls()
        {
            syncing = true;
            settings.Language = L10n.Normalize(settings.Language);
            Brand.SetTheme(settings.Theme);
            BackColor = Brand.Background;
            ForeColor = Brand.Text;
            foreach (Control control in Controls)
            {
                control.BackColor = Brand.Background;
                control.ForeColor = Brand.Text;
            }
            languageMenu.BackColor = Brand.Surface;
            languageMenu.ForeColor = Brand.Text;
            themeToggle.Selected = Brand.IsLight;
            themeToggle.AccessibleName = String.Format(L10n.Culture(settings.Language), T("theme_switch"),
                T(Brand.IsLight ? "theme_dark" : "theme_light"));
            themeToggle.AccessibleDescription = T("theme") + ": " + T(Brand.IsLight ? "theme_light" : "theme_dark");
            windowToolTip.SetToolTip(themeToggle, themeToggle.AccessibleName);
            Text = "Desktop Hover — " + T("settings");
            AccessibleName = Text;
            minimize.AccessibleName = T("minimize_settings");
            close.AccessibleName = T("exit_accessible");
            windowToolTip.SetToolTip(minimize, minimize.AccessibleName);
            windowToolTip.SetToolTip(close, close.AccessibleName);
            L10n.LanguageOption language = L10n.Languages[L10n.LanguageIndex(settings.Language)];
            languagePicker.Text = language.ShortName;
            languagePicker.AccessibleName = T("language") + ": " + language.NativeName;
            windowToolTip.SetToolTip(languagePicker, languagePicker.AccessibleName);
            languageMenu.AccessibleName = T("language");
            foreach (ToolStripMenuItem item in languageMenu.Items)
            {
                item.BackColor = Brand.Surface;
                item.ForeColor = Brand.Text;
                item.Checked = (string)item.Tag == settings.Language;
            }
            for (int i = 0; i < presets.Length; i++)
            {
                presets[i].Selected = settings.MaxWidth == SizeWidths[i] && settings.MaxHeight == SizeHeights[i];
                presets[i].Text = T(SizeKeys[i]);
                presets[i].AccessibleName = String.Format(L10n.Culture(settings.Language), T("size_accessible"),
                    presets[i].Text, SizeWidths[i], SizeHeights[i]);
            }
            for (int i = 0; i < delays.Length; i++)
            {
                delays[i].Selected = settings.DelayMs == DelayValues[i];
                string seconds = (DelayValues[i] / 1000.0).ToString(DelayValues[i] == 0 ? "0" : "0.00", L10n.Culture(settings.Language));
                delays[i].Text = String.Format(L10n.Culture(settings.Language), T("delay_seconds"), seconds);
                delays[i].AccessibleName = String.Format(L10n.Culture(settings.Language), T("delay_accessible"), DelayValues[i]);
                windowToolTip.SetToolTip(delays[i], T(DelayKeys[i]));
            }
            widthSlider.Value = settings.MaxWidth;
            widthSlider.AccessibleName = T("width_accessible");
            volumeSlider.Value = settings.VolumePercent;
            volumeSlider.AccessibleName = String.Format(L10n.Culture(settings.Language), T("volume_accessible"), settings.VolumePercent);
            volumeSlider.AccessibleDescription = T("volume_detail");
            upscaleToggle.Checked = settings.Upscale;
            LocalizeToggle(upscaleToggle, "upscale", "upscale_detail");
            startupToggle.Checked = settings.StartWithWindows;
            LocalizeToggle(startupToggle, "startup", "startup_detail");
            enabledToggle.Checked = !paused;
            LocalizeToggle(enabledToggle, paused ? "preview_paused" : "preview_on", "preview_detail");
            syncing = false;
            Invalidate(true);
        }

        private string T(string key) { return L10n.Text(settings.Language, key); }

        private void LocalizeToggle(HoverToggle toggle, string titleKey, string detailKey)
        {
            toggle.Text = T(titleKey);
            toggle.Detail = T(detailKey);
            toggle.AccessibleName = toggle.Text;
            toggle.AccessibleDescription = toggle.Detail;
            toggle.DefaultActionText = T("toggle");
            toggle.Invalidate();
        }

        public void ShowCentered(Point screenPoint)
        {
            Rectangle area = Screen.FromPoint(screenPoint).WorkingArea;
            int x = area.Left + Math.Max(0, (area.Width - Width) / 2);
            int y = area.Top + Math.Max(0, (area.Height - Height) / 2);
            Location = new Point(x, y);
            if (!Visible) Show();
            Activate();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            using (GraphicsPath path = Brand.RoundRect(new RectangleF(0, 0, Width, Height), 16))
                Region = new Region(path);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && e.Y < 80)
            {
                ReleaseCapture();
                SendMessage(Handle, 0x00A1, new IntPtr(2), IntPtr.Zero);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.Escape)
            {
                if (languageMenu.Visible) languageMenu.Close();
                else Close();
                return true;
            }
            if (languagePicker.Focused && (keyData == Keys.Up || keyData == Keys.Down || keyData == (Keys.Alt | Keys.Down) || keyData == Keys.F4))
            {
                ShowLanguageMenu();
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath border = Brand.RoundRect(new RectangleF(.5f, .5f, Width - 1, Height - 1), 16))
            using (var pen = new Pen(Brand.Border))
                g.DrawPath(pen, border);
            Brand.DrawLogo(g, new Rectangle(24, 23, 40, 40));
            DrawText(g, "Desktop Hover", titleFont, Brand.Text, new Rectangle(76, 20, 246, 30));

            FillRound(g, new RectangleF(24, 87, 376, 34), 9, Brand.Surface);
            using (var brush = new SolidBrush(paused ? Brand.Paused : Brand.Accent))
                g.FillEllipse(brush, 37, 100, 8, 8);
            DrawText(g, T(paused ? "paused" : "active"), Font, Brand.Text, new Rectangle(53, 94, 100, 22));
            DrawText(g, T("surfaces"), smallFont, Brand.Muted,
                new Rectangle(147, 94, 239, 22), TextFormatFlags.Right);

            DrawText(g, T("image_size"), headingFont, Brand.Text, new Rectangle(24, 139, 376, 25));
            string size = settings.MaxWidth + " \u00d7 " + settings.MaxHeight + " px";
            DrawText(g, T("maximum_size"), smallFont, Brand.Muted, new Rectangle(24, 319, 188, 20));
            DrawText(g, size, Font, Brand.Text, new Rectangle(209, 316, 191, 24), TextFormatFlags.Right);
            DrawText(g, T("aspect_ratio"), smallFont, Brand.Muted, new Rectangle(24, 340, 376, 20));
            using (var pen = new Pen(Brand.Border)) g.DrawLine(pen, 24, 367, 400, 367);
            DrawText(g, T("delay"), headingFont, Brand.Text, new Rectangle(24, 373, 376, 23));
            DrawText(g, T("volume"), headingFont, Brand.Text, new Rectangle(24, 449, 300, 23));
            DrawText(g, settings.VolumePercent + "%", Font, Brand.Accent,
                new Rectangle(330, 449, 70, 23), TextFormatFlags.Right);
            DrawText(g, T("volume_detail"), smallFont, Brand.Muted, new Rectangle(24, 474, 376, 20));
            using (var pen = new Pen(Brand.Border)) g.DrawLine(pen, 24, 704, 400, 704);
            DrawText(g, T("autosaved"), smallFont, Brand.Muted, new Rectangle(24, 722, 212, 23));
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rectangle)
        {
            DrawText(g, text, font, color, rectangle, TextFormatFlags.Left);
        }

        private static void DrawText(Graphics g, string text, Font font, Color color, Rectangle rectangle, TextFormatFlags alignment)
        {
            TextRenderer.DrawText(g, text, font, rectangle, color,
                alignment | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        }

        internal static void FillRound(Graphics g, RectangleF rect, float radius, Color color)
        {
            using (GraphicsPath path = Brand.RoundRect(rect, radius))
            using (var brush = new SolidBrush(color)) g.FillPath(brush, path);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                languageMenu.Dispose();
                windowToolTip.Dispose();
                headingFont.Dispose();
                titleFont.Dispose();
                smallFont.Dispose();
                bodyFont.Dispose();
                windowIcon.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
    }

    internal sealed class HoverMenuRenderer : ToolStripProfessionalRenderer
    {
        public HoverMenuRenderer() { RoundedEdges = false; }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var pen = new Pen(Brand.Border))
                e.Graphics.DrawLine(pen, 8, e.Item.Height / 2, e.Item.Width - 8, e.Item.Height / 2);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Brand.Muted;
            Rectangle bounds = e.ArrowRectangle;
            bounds.Y = (e.Item.Height - bounds.Height) / 2;
            e.ArrowRectangle = bounds;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(Brand.Surface);
        }

        protected override void OnRenderImageMargin(ToolStripRenderEventArgs e) { }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Brand.RoundRect(new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 9))
            using (var pen = new Pen(Brand.Border)) e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item as ToolStripMenuItem;
            if (e.Item.Selected || (item != null && item.Checked))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                SettingsWindow.FillRound(e.Graphics, new RectangleF(0, 0, e.Item.Width, e.Item.Height), 6,
                    e.Item.Selected ? Brand.HoverSurface : Brand.SelectedSurface);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var item = e.Item as ToolStripMenuItem;
            e.TextColor = item != null && item.Checked ? Brand.Accent : Brand.Text;
            // Native text bounds omit the extra row padding; center against the whole row.
            e.TextRectangle = new Rectangle(e.TextRectangle.X, 0, e.TextRectangle.Width, e.Item.Height);
            e.TextFormat = (e.TextFormat & ~TextFormatFlags.Bottom) |
                TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int x = e.ImageRectangle.Left + e.ImageRectangle.Width / 2;
            int y = e.Item.Height / 2;
            using (var pen = new Pen(Brand.Accent, 1.7f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                e.Graphics.DrawLines(pen, new[] { new Point(x - 5, y), new Point(x - 1, y + 4), new Point(x + 6, y - 4) });
            }
        }
    }

    internal sealed class HoverButton : Button
    {
        private bool selected, hovering;
        public string Detail { get; set; }
        public bool Compact { get; set; }
        public bool LanguageSelector { get; set; }
        public bool ThemeSelector { get; set; }
        public bool Selected { get { return selected; } set { selected = value; Invalidate(); } }

        public HoverButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Brand.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = selected ? Brand.SelectedSurface : (hovering ? Brand.HoverSurface : Brand.Surface);
            SettingsWindow.FillRound(g, new RectangleF(0.5f, 0.5f, Width - 1, Height - 1), 9, fill);
            using (var path = Brand.RoundRect(new RectangleF(.5f, .5f, Width - 1, Height - 1), 9))
            using (var pen = new Pen(selected || Focused ? Brand.Accent : Brand.Border))
                g.DrawPath(pen, path);
            if (LanguageSelector)
            {
                Brand.DrawLanguageIcon(g, new Rectangle(10, (Height - 21) / 2, 21, 21), Brand.Accent);
                TextRenderer.DrawText(g, Text, Font, new Rectangle(40, 0, Width - 69, Height),
                    selected ? Brand.Accent : Brand.Text,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                int mid = Height / 2;
                using (var pen = new Pen(selected ? Brand.Accent : Brand.Muted, 1.5f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, selected
                        ? new[] { new Point(Width - 20, mid - 2), new Point(Width - 16, mid + 2), new Point(Width - 12, mid - 2) }
                        : new[] { new Point(Width - 20, mid + 2), new Point(Width - 16, mid - 2), new Point(Width - 12, mid + 2) });
                }
            }
            else if (ThemeSelector)
            {
                DrawThemeIcon(g);
            }
            else if (Compact)
            {
                TextRenderer.DrawText(g, Text, Font, ClientRectangle, selected ? Brand.Accent : Brand.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                TextRenderer.DrawText(g, Text, Font, new Rectangle(13, 5, Width - 34, 21),
                    selected ? Brand.Accent : Brand.Text, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
                using (var detailFont = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                    TextRenderer.DrawText(g, Detail, detailFont, new Rectangle(13, 25, Width - 26, 16),
                        Brand.Muted, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
                if (selected)
                {
                    using (var pen = new Pen(Brand.Accent, 1.7f))
                        g.DrawLines(pen, new[] { new Point(Width - 24, 18), new Point(Width - 20, 22), new Point(Width - 13, 14) });
                }
            }
        }

        private void DrawThemeIcon(Graphics graphics)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform((Width - 20) / 2f, (Height - 20) / 2f);
                using (var pen = new Pen(selected ? Brand.Accent : Brand.Text, 1.6f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    if (Brand.IsLight)
                    {
                        using (var moon = new GraphicsPath())
                        {
                            moon.AddBezier(17, 11, 16, 16, 12, 19, 7, 17);
                            moon.AddBezier(7, 17, 2, 15, 0, 10, 3, 5);
                            moon.AddBezier(3, 5, 4, 3, 7, 1, 10, 2);
                            moon.AddBezier(10, 2, 7, 5, 8, 10, 11, 12);
                            moon.AddBezier(11, 12, 13, 13, 15, 13, 17, 11);
                            moon.CloseFigure();
                            graphics.DrawPath(pen, moon);
                        }
                    }
                    else
                    {
                        graphics.DrawEllipse(pen, 6, 6, 8, 8);
                        for (int ray = 0; ray < 8; ray++)
                        {
                            double angle = ray * Math.PI / 4;
                            float x = (float)Math.Cos(angle), y = (float)Math.Sin(angle);
                            graphics.DrawLine(pen, 10 + x * 7, 10 + y * 7, 10 + x * 9, 10 + y * 9);
                        }
                    }
                }
            }
            finally { graphics.Restore(state); }
        }
    }

    internal sealed class HoverToggle : Button
    {
        private bool isChecked, hovering;
        public string Detail { get; set; }
        public string DefaultActionText { get; set; }
        public bool Checked
        {
            get { return isChecked; }
            set { isChecked = value; Invalidate(); AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); }
        }

        public HoverToggle()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        protected override void OnMouseEnter(EventArgs e) { hovering = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Brand.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (hovering) SettingsWindow.FillRound(g, new RectangleF(0, 0, Width, Height), 8, Brand.Surface);
            if (Focused)
                using (var path = Brand.RoundRect(new RectangleF(.5f, .5f, Width - 1, Height - 1), 8))
                using (var pen = new Pen(Brand.Accent)) g.DrawPath(pen, path);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 4, Width - 58, 22), Brand.Text,
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
            using (var detailFont = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                TextRenderer.DrawText(g, Detail, detailFont, new Rectangle(0, 27, Width - 58, 16), Brand.Muted,
                    TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
            SettingsWindow.FillRound(g, new RectangleF(Width - 43, 12, 42, 24), 12,
                isChecked ? Brand.Accent : Brand.ToggleOff);
            using (var brush = new SolidBrush(isChecked && !Brand.IsLight ? Brand.Background : Brand.ToggleThumb))
                g.FillEllipse(brush, Width - (isChecked ? 22 : 40), 15, 18, 18);
        }

        protected override AccessibleObject CreateAccessibilityInstance() { return new ToggleAccessibleObject(this); }
        private sealed class ToggleAccessibleObject : ControlAccessibleObject
        {
            private readonly HoverToggle owner;
            public ToggleAccessibleObject(HoverToggle owner) : base(owner) { this.owner = owner; }
            public override AccessibleStates State { get { return base.State | (owner.Checked ? AccessibleStates.Checked : AccessibleStates.None); } }
            public override string DefaultAction { get { return owner.DefaultActionText ?? "Toggle"; } }
            public override void DoDefaultAction() { owner.PerformClick(); }
        }
    }

    internal sealed class HoverSlider : Control
    {
        private int value;
        private bool dragging;
        public int Minimum { get; set; }
        public int Maximum { get; set; }
        public int Step { get; set; }
        public event EventHandler ValueChanged;
        public int Value
        {
            get { return value; }
            set
            {
                int next = Math.Max(Minimum, Math.Min(Maximum, value));
                if (this.value == next) return;
                this.value = next;
                Invalidate();
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public HoverSlider()
        {
            Minimum = 320;
            Maximum = 1600;
            Step = 40;
            value = 720;
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.Slider;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.Selectable, true);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Left || key == Keys.Right || key == Keys.Up || key == Keys.Down ||
                key == Keys.Home || key == Keys.End || key == Keys.PageUp || key == Keys.PageDown || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Value -= Step; break;
                case Keys.Right: case Keys.Up: Value += Step; break;
                case Keys.PageDown: Value -= Step * 4; break;
                case Keys.PageUp: Value += Step * 4; break;
                case Keys.Home: Value = Minimum; break;
                case Keys.End: Value = Maximum; break;
                default: return;
            }
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private void SetFromX(int x)
        {
            double ratio = Math.Max(0, Math.Min(1, (x - 10.0) / Math.Max(1, Width - 20)));
            int newValue = Minimum + (int)Math.Round((Maximum - Minimum) * ratio / Step) * Step;
            Value = newValue;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            Capture = true;
            SetFromX(e.X);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) SetFromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; }
        protected override void OnMouseCaptureChanged(EventArgs e) { base.OnMouseCaptureChanged(e); if (!Capture) dragging = false; }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Brand.Background);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float x = 10 + (Width - 20) * (value - Minimum) / (float)Math.Max(1, Maximum - Minimum);
            SettingsWindow.FillRound(g, new RectangleF(10, 12, Width - 20, 5), 2.5f, Brand.Border);
            if (x > 10) SettingsWindow.FillRound(g, new RectangleF(10, 12, x - 10, 5), 2.5f, Brand.Accent);
            if (Focused)
                using (var pen = new Pen(Brand.Accent)) g.DrawEllipse(pen, x - 11, 3.5f, 22, 22);
            using (var brush = new SolidBrush(Brand.Accent)) g.FillEllipse(brush, x - 7, 7.5f, 14, 14);
        }

        protected override AccessibleObject CreateAccessibilityInstance() { return new SliderAccessibleObject(this); }
        private sealed class SliderAccessibleObject : ControlAccessibleObject
        {
            private readonly HoverSlider owner;
            public SliderAccessibleObject(HoverSlider owner) : base(owner) { this.owner = owner; }
            public override string Value
            {
                get { return owner.Value.ToString(); }
                set { int next; if (int.TryParse(value, out next)) owner.Value = next; }
            }
        }
    }
}
