using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace DesktopHover
{
    // Main settings window, opened from the tray icon or quick settings menu.
    internal sealed partial class SettingsWindow : Form
    {
        private AppSettings settings, lastApplied;
        private readonly Dictionary<Control, Rectangle> designBounds = new Dictionary<Control, Rectangle>();
        private float uiScale = 1;
        private Font scaledSmallFont, scaledBodyFont;
        private bool arranging, layoutReady, initiallySized, wideLayout;
        private float canvasWidth = 1000, columnWidth = 460, footerY = 716;
        private FormWindowState restoredState = FormWindowState.Normal;
        private readonly Timer saveTimer = new Timer { Interval = 300 };
        private bool pendingSave;
        private readonly Action<AppSettings> changed;
        private readonly Action<bool> pauseChanged;
        private readonly HoverButton[] presets = new HoverButton[4];
        private readonly HoverButton[] delays = new HoverButton[5];
        private readonly HoverButton languagePicker, themeToggle, githubProject;
        private readonly Label autosavedLabel;
        private const string ProjectUrl = "https://github.com/LaxZo/DesktopHover";
        private readonly ContextMenuStrip languageMenu;
        private readonly ToolTip windowToolTip = new ToolTip();
        private readonly HoverSlider widthSlider, volumeSlider, slideIntervalSlider;
        private readonly HoverToggle upscaleToggle, startupToggle, enabledToggle, animationsToggle, autoPlayToggle, loopVideoToggle;
        private readonly HoverToggle[] typeToggles = new HoverToggle[5];
        private static readonly string[] TypeKeys = { "type_images", "type_documents", "type_text", "type_audio", "type_video" };
        private bool paused, syncing;
        private readonly Font headingFont = new Font("Segoe UI", 15, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font titleFont = new Font("Segoe UI", 16, FontStyle.Bold, GraphicsUnit.Pixel);
        private readonly Font smallFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Font bodyFont = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel);
        private readonly Icon windowIcon;
        private readonly WindowMotion windowMotion;
        private static readonly int[] SizeWidths = { 480, 720, 1080, 1440 };
        private static readonly int[] SizeHeights = { 360, 540, 810, 1080 };
        private static readonly int[] DelayValues = { 0, 250, 450, 750, 1000 };
        private static readonly string[] SizeKeys = { "small", "medium", "large", "extra_large" };
        private static readonly string[] DelayKeys = { "instant", "fast", "natural", "slow", "one_second" };

        public SettingsWindow(AppSettings settings, bool paused,
            Action<AppSettings> changed, Action<bool> pauseChanged)
        {
            saveTimer.Tick += delegate { FlushSettings(); };
            this.settings = settings.Clone();
            lastApplied = this.settings.Clone();
            this.paused = paused;
            this.changed = changed;
            this.pauseChanged = pauseChanged;
            AutoScaleMode = AutoScaleMode.None;
            FormBorderStyle = FormBorderStyle.None;
            ClientSize = new Size(1000, 780);
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = true;
            MaximizeBox = true;
            MinimizeBox = true;
            KeyPreview = true;
            DoubleBuffered = true;
            BackColor = Brand.Background;
            ForeColor = Brand.Text;
            Font = bodyFont;
            windowIcon = Brand.CreateIcon();
            Icon = windowIcon;

            autosavedLabel = new Label
            {
                Name = "autosaved-status",
                Font = smallFont,
                AutoEllipsis = true,
                TextAlign = ContentAlignment.MiddleLeft
            };
            autosavedLabel.SetBounds(24, 1158, 162, 32);
            Controls.Add(autosavedLabel);

            githubProject = new HoverButton { Name = "github-project", Compact = true, ProjectLink = true };
            githubProject.SetBounds(194, 1158, 40, 32);
            githubProject.Click += delegate { OpenProject(); };
            Controls.Add(githubProject);

            themeToggle = new HoverButton { Name = "theme-toggle", Compact = true, ThemeSelector = true };
            themeToggle.SetBounds(242, 1158, 40, 32);
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
            languagePicker.SetBounds(290, 1158, 110, 32);
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
            MenuMotion.Attach(languageMenu);
            SystemCursors.UseHand(languageMenu);
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
                ScheduleApply();
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
                ScheduleApply();
            };
            Controls.Add(volumeSlider);

            slideIntervalSlider = new HoverSlider
            {
                Name = "slide-interval-slider",
                Minimum = 1,
                Maximum = 10,
                Step = 1,
                Value = 3
            };
            slideIntervalSlider.SetBounds(24, 560, 376, 30);
            slideIntervalSlider.ValueChanged += delegate
            {
                if (syncing) return;
                this.settings.SlideIntervalSeconds = slideIntervalSlider.Value;
                ScheduleApply();
            };
            Controls.Add(slideIntervalSlider);

            upscaleToggle = AddToggle(616);
            upscaleToggle.Click += delegate { this.settings.Upscale = !this.settings.Upscale; Apply(); };
            startupToggle = AddToggle(658);
            startupToggle.Click += delegate { this.settings.StartWithWindows = !this.settings.StartWithWindows; Apply(); };
            enabledToggle = AddToggle(700);
            enabledToggle.Click += delegate
            {
                UpdatePaused(!this.paused);
                if (this.pauseChanged != null) this.pauseChanged(this.paused);
            };
            animationsToggle = AddToggle(742);
            animationsToggle.Name = "animations-toggle";
            animationsToggle.Click += delegate
            {
                this.settings.AnimationsEnabled = !this.settings.AnimationsEnabled;
                Apply();
            };

            autoPlayToggle = AddToggle(786);
            autoPlayToggle.Name = "autoplay-toggle";
            autoPlayToggle.Click += delegate { this.settings.AutoPlayMedia = !this.settings.AutoPlayMedia; Apply(); };
            loopVideoToggle = AddToggle(828);
            loopVideoToggle.Name = "loop-video-toggle";
            loopVideoToggle.Click += delegate { this.settings.LoopVideo = !this.settings.LoopVideo; Apply(); };
            for (int i = 0; i < typeToggles.Length; i++)
            {
                int index = i;
                var toggle = AddToggle(874 + i * 42);
                toggle.Name = TypeKeys[i];
                toggle.Click += delegate
                {
                    switch (index)
                    {
                        case 0: this.settings.PreviewImages = !this.settings.PreviewImages; break;
                        case 1: this.settings.PreviewDocuments = !this.settings.PreviewDocuments; break;
                        case 2: this.settings.PreviewText = !this.settings.PreviewText; break;
                        case 3: this.settings.PreviewAudio = !this.settings.PreviewAudio; break;
                        case 4: this.settings.PreviewVideo = !this.settings.PreviewVideo; break;
                    }
                    Apply();
                };
                typeToggles[i] = toggle;
            }

            Controls.Add(languagePicker);
            SyncControls();
            windowMotion = new WindowMotion(this, true, true);
            foreach (Control control in Controls) designBounds.Add(control, control.Bounds);
            InitializeChrome();
            layoutReady = true;
            ArrangeSettings();
        }

        private void ShowLanguageMenu()
        {
            if (languageMenu.Visible) { languageMenu.Close(); return; }
            languageMenu.Show(languagePicker, new Point(languagePicker.Width, -5), ToolStripDropDownDirection.AboveLeft);
        }

        private void OpenProject()
        {
            try
            {
                using (Process browser = Process.Start(new ProcessStartInfo(ProjectUrl) { UseShellExecute = true })) { }
            }
            catch (Win32Exception) { ShowProjectLinkError(); }
            catch (InvalidOperationException) { ShowProjectLinkError(); }
        }

        private void ShowProjectLinkError()
        {
            MessageBox.Show(this, T("project_link_error") + "\r\n\r\n" + ProjectUrl,
                "Desktop Hover", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private HoverToggle AddToggle(int y)
        {
            var toggle = new HoverToggle();
            toggle.SetBounds(24, y, 376, 40);
            Controls.Add(toggle);
            return toggle;
        }

        private void ScheduleApply()
        {
            pendingSave = true; saveTimer.Stop(); saveTimer.Start();
            autosavedLabel.Text = T("saving");
            for (int i = 0; i < presets.Length; i++)
                presets[i].Selected = settings.MaxWidth == SizeWidths[i] && settings.MaxHeight == SizeHeights[i];
            if (content != null) content.Invalidate();
        }
        private void FlushSettings()
        {
            saveTimer.Stop();
            if (!pendingSave) return;
            pendingSave = false; Apply();
        }
        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            FlushSettings(); base.OnFormClosing(e);
        }

        private void Apply()
        {
            saveTimer.Stop(); pendingSave = false;
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
            saveTimer.Stop(); pendingSave = false;
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
            UiMotion.Enabled = settings.AnimationsEnabled;
            settings.Language = L10n.Normalize(settings.Language);
            Brand.SetTheme(settings.Theme);
            BackColor = Brand.Background;
            ForeColor = Brand.Text;
            ApplyTitleBarTheme();
            UpdateChrome();
            foreach (Control control in content == null ? Controls : content.Controls)
            {
                control.BackColor = Brand.Background;
                control.ForeColor = Brand.Text;
            }
            languageMenu.BackColor = Brand.Surface;
            languageMenu.ForeColor = Brand.Text;
            autosavedLabel.Text = T("autosaved");
            autosavedLabel.ForeColor = Brand.Muted;
            windowToolTip.SetToolTip(autosavedLabel, autosavedLabel.Text);
            githubProject.AccessibleName = T("project_link");
            githubProject.AccessibleDescription = ProjectUrl;
            windowToolTip.SetToolTip(githubProject, githubProject.AccessibleName + "\r\n" + ProjectUrl);
            themeToggle.Selected = Brand.IsLight;
            themeToggle.AccessibleName = String.Format(L10n.Culture(settings.Language), T("theme_switch"),
                T(Brand.IsLight ? "theme_dark" : "theme_light"));
            themeToggle.AccessibleDescription = T("theme") + ": " + T(Brand.IsLight ? "theme_light" : "theme_dark");
            windowToolTip.SetToolTip(themeToggle, themeToggle.AccessibleName);
            Text = "Desktop Hover — " + T("settings");
            AccessibleName = Text;
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
            slideIntervalSlider.Value = settings.SlideIntervalSeconds;
            slideIntervalSlider.AccessibleName = String.Format(L10n.Culture(settings.Language), T("slide_interval_accessible"),
                settings.SlideIntervalSeconds);
            slideIntervalSlider.AccessibleDescription = T("slide_interval_detail");
            upscaleToggle.Checked = settings.Upscale;
            LocalizeToggle(upscaleToggle, "upscale", "upscale_detail");
            startupToggle.Checked = settings.StartWithWindows;
            LocalizeToggle(startupToggle, "startup", "startup_detail");
            enabledToggle.Checked = !paused;
            LocalizeToggle(enabledToggle, paused ? "preview_paused" : "preview_on", "preview_detail");
            animationsToggle.Checked = settings.AnimationsEnabled;
            LocalizeToggle(animationsToggle, "animations", "animations_detail");
            autoPlayToggle.Checked = settings.AutoPlayMedia;
            LocalizeToggle(autoPlayToggle, "autoplay", "autoplay_detail");
            loopVideoToggle.Checked = settings.LoopVideo;
            LocalizeToggle(loopVideoToggle, "loop_video", "loop_video_detail");
            bool[] enabledTypes = { settings.PreviewImages, settings.PreviewDocuments, settings.PreviewText, settings.PreviewAudio, settings.PreviewVideo };
            for (int i = 0; i < typeToggles.Length; i++)
            {
                typeToggles[i].Checked = enabledTypes[i];
                LocalizeToggle(typeToggles[i], TypeKeys[i], "preview_detail");
            }
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
            if (WindowState == FormWindowState.Minimized) WindowState = restoredState;
            if (!initiallySized)
            {
                Rectangle area = Screen.FromPoint(screenPoint).WorkingArea;
                FitToArea(area, PreviewWindow.GetScale(screenPoint));
                Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2),
                    area.Top + Math.Max(0, (area.Height - Height) / 2));
            }
            windowMotion.ShowAt(Location);
            Activate();
        }

        internal void FitToArea(Rectangle area, float dpiScale)
        {
            initiallySized = true;
            SetUiScale(dpiScale);
            MinimumSize = new Size(Math.Min((int)Math.Round(520 * uiScale), area.Width - 32),
                Math.Min((int)Math.Round(440 * uiScale), area.Height - 32));
            Size desired = SizeFromClientSize(new Size((int)Math.Round(1000 * uiScale), (int)Math.Round(780 * uiScale)));
            Size = new Size(Math.Min(desired.Width, area.Width - 32), Math.Min(desired.Height, area.Height - 32));
            ArrangeSettings();
        }

        private void SetUiScale(float value)
        {
            value = Math.Max(.75f, value);
            if (scaledSmallFont != null && Math.Abs(uiScale - value) < .01f) return;
            uiScale = value;
            Font oldSmall = scaledSmallFont, oldBody = scaledBodyFont;
            scaledSmallFont = new Font(smallFont.FontFamily, smallFont.Size * uiScale, smallFont.Style, GraphicsUnit.Pixel);
            scaledBodyFont = new Font(bodyFont.FontFamily, bodyFont.Size * uiScale, bodyFont.Style, GraphicsUnit.Pixel);
            autosavedLabel.Font = scaledSmallFont; languageMenu.Font = scaledBodyFont;
            if (oldSmall != null) oldSmall.Dispose(); if (oldBody != null) oldBody.Dispose();
            PositionChrome();
        }

        // Keep the original single-column coordinates as the source for controls and text.
        // The same mapping makes every resized hit target match its painted section.
        private Rectangle MapContent(Rectangle rect)
        {
            bool right = wideLayout && rect.Y >= 616 && rect.Y < 1090;
            float origin = right ? 56 + columnWidth : 24;
            return Rectangle.Round(new RectangleF(origin + (rect.X - 24) * columnWidth / 376,
                rect.Y + (rect.Y >= 840 ? 44 : 0) - (right ? 444 : 0), rect.Width * columnWidth / 376, rect.Height));
        }

        private Rectangle ControlBounds(Control control, Rectangle original)
        {
            if (control == autosavedLabel) return Rectangle.Round(new RectangleF(24, footerY, canvasWidth - 280, 32));
            if (control == githubProject) return Rectangle.Round(new RectangleF(canvasWidth - 230, footerY, 40, 32));
            if (control == themeToggle) return Rectangle.Round(new RectangleF(canvasWidth - 182, footerY, 40, 32));
            if (control == languagePicker) return Rectangle.Round(new RectangleF(canvasWidth - 134, footerY, 110, 32));
            return MapContent(original);
        }

        private void ArrangeSettings()
        {
            if (!layoutReady || arranging || WindowState == FormWindowState.Minimized) return;
            arranging = true;
            Point scroll = content.AutoScrollPosition;
            content.SuspendLayout();
            try
            {
                content.AutoScrollPosition = Point.Empty;
                // Reserve a scrollbar gutter so adding a scrollbar never creates horizontal scrolling.
                canvasWidth = Math.Max(424, (content.ClientSize.Width - SystemInformation.VerticalScrollBarWidth) / uiScale);
                wideLayout = canvasWidth >= 860;
                columnWidth = wideLayout ? (canvasWidth - 80) / 2 : canvasWidth - 48;
                footerY = Math.Max(wideLayout ? 716 : 1202, content.ClientSize.Height / uiScale + 80 - 54);
                foreach (var item in designBounds)
                {
                    Rectangle rect = ControlBounds(item.Key, item.Value);
                    item.Key.Bounds = new Rectangle((int)Math.Round(rect.X * uiScale), (int)Math.Round((rect.Y - 80) * uiScale),
                        (int)Math.Round(rect.Width * uiScale), (int)Math.Round(rect.Height * uiScale));
                    var button = item.Key as HoverButton; if (button != null) button.UiScale = uiScale;
                    var toggle = item.Key as HoverToggle; if (toggle != null) toggle.UiScale = uiScale;
                    var slider = item.Key as HoverSlider; if (slider != null) slider.UiScale = uiScale;
                }
                content.AutoScrollMinSize = new Size(0, (int)Math.Ceiling((footerY + 42 - 80) * uiScale));
                content.AutoScrollPosition = new Point(-scroll.X, -scroll.Y);
            }
            finally { content.ResumeLayout(); arranging = false; }
            Invalidate(true);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            if (WindowState != FormWindowState.Minimized) restoredState = WindowState;
            PositionChrome();
            ArrangeSettings();
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyTitleBarTheme();
            ApplyWindowShape();
        }

        private void ApplyTitleBarTheme()
        {
            if (!IsHandleCreated) return;
            try
            {
                int dark = Brand.IsLight ? 0 : 1;
                int caption = ColorTranslator.ToWin32(Brand.Background);
                int text = ColorTranslator.ToWin32(Brand.Text);
                int border = ColorTranslator.ToWin32(Brand.Border);
                // Native chrome is drawn by DWM, independently of the form's BackColor.
                // Explicit colors also follow the app when Windows uses a different theme.
                DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int)); // USE_IMMERSIVE_DARK_MODE
                DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int)); // CAPTION_COLOR
                DwmSetWindowAttribute(Handle, 36, ref text, sizeof(int)); // TEXT_COLOR
                DwmSetWindowAttribute(Handle, 34, ref border, sizeof(int)); // BORDER_COLOR
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            // Older Windows versions ignore unsupported DWM attributes.
        }
        protected override void OnResizeBegin(EventArgs e) { windowMotion.Finish(); base.OnResizeBegin(e); }
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            SetUiScale(PreviewWindow.GetScale(Location));
            ArrangeSettings();
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

        private void PaintSettings(object sender, PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(content.AutoScrollPosition.X, content.AutoScrollPosition.Y - 80 * uiScale);
            g.ScaleTransform(uiScale, uiScale);

            FillRound(g, new RectangleF(24, 87, canvasWidth - 48, 34), 9, Brand.Surface);
            using (var brush = new SolidBrush(paused ? Brand.Paused : Brand.Accent))
                g.FillEllipse(brush, 37, 100, 8, 8);
            DrawText(g, T(paused ? "paused" : "active"), Font, Brand.Text, new Rectangle(53, 94, 100, 22));
            DrawText(g, T("surfaces"), smallFont, Brand.Muted,
                new Rectangle(170, 94, (int)canvasWidth - 208, 22), TextFormatFlags.Right);

            if (wideLayout)
            {
                using (var pen = new Pen(Brand.Border)) g.DrawLine(pen, columnWidth + 40, 139, columnWidth + 40, 692);
                UiDrawing.Text(g, T("settings"), headingFont,
                    Rectangle.Round(new RectangleF(columnWidth + 56, 139, columnWidth, 25)), Brand.Text,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            DrawText(g, T("image_size"), headingFont, Brand.Text, new Rectangle(24, 139, 376, 25));
            string size = settings.MaxWidth + " \u00d7 " + settings.MaxHeight + " px";
            DrawText(g, T("maximum_size"), smallFont, Brand.Muted, new Rectangle(24, 319, 188, 20));
            DrawText(g, size, Font, Brand.Text, new Rectangle(209, 316, 191, 24), TextFormatFlags.Right);
            DrawText(g, T("aspect_ratio"), smallFont, Brand.Muted, new Rectangle(24, 340, 376, 20));
            using (var pen = new Pen(Brand.Border)) g.DrawLine(pen, 24, 367, 24 + columnWidth, 367);
            DrawText(g, T("delay"), headingFont, Brand.Text, new Rectangle(24, 373, 376, 23));
            DrawText(g, T("volume"), headingFont, Brand.Text, new Rectangle(24, 449, 300, 23));
            DrawText(g, settings.VolumePercent + "%", Font, Brand.Accent,
                new Rectangle(330, 449, 70, 23), TextFormatFlags.Right);
            DrawText(g, T("volume_detail"), smallFont, Brand.Muted, new Rectangle(24, 474, 376, 20));
            DrawText(g, T("slide_interval"), headingFont, Brand.Text, new Rectangle(24, 530, 278, 24));
            DrawText(g, String.Format(L10n.Culture(settings.Language), T("slide_interval_value"), settings.SlideIntervalSeconds),
                Font, Brand.Accent, new Rectangle(302, 530, 98, 24), TextFormatFlags.Right);
            DrawText(g, T("slide_interval_detail"), smallFont, Brand.Muted, new Rectangle(24, 590, 376, 20));
            DrawText(g, T("file_types"), headingFont, Brand.Text, new Rectangle(24, 840, 376, 25));
            DrawText(g, T("shortcut_toggle"), smallFont, Brand.Muted, new Rectangle(24, 1092, 376, 22));
            DrawText(g, T("shortcut_pin"), smallFont, Brand.Muted, new Rectangle(24, 1116, 376, 22));
            using (var pen = new Pen(Brand.Border)) g.DrawLine(pen, 24, footerY - 12, canvasWidth - 24, footerY - 12);
        }

        private void DrawText(Graphics g, string text, Font font, Color color, Rectangle rectangle)
        {
            DrawText(g, text, font, color, rectangle, TextFormatFlags.Left);
        }

        private void DrawText(Graphics g, string text, Font font, Color color, Rectangle rectangle, TextFormatFlags alignment)
        {
            if (rectangle.Y >= 139 && rectangle.Y < 1090) rectangle = MapContent(rectangle);
            else if (rectangle.Y >= 1090)
                rectangle = Rectangle.Round(new RectangleF(24, wideLayout ? rectangle.Y - 444 : rectangle.Y + 44, columnWidth, rectangle.Height));
            UiDrawing.Text(g, text, font, rectangle, color,
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
                FlushSettings(); saveTimer.Dispose();
                if (windowMotion != null) windowMotion.Dispose();
                languageMenu.Dispose();
                windowToolTip.Dispose();
                headingFont.Dispose();
                titleFont.Dispose();
                smallFont.Dispose();
                bodyFont.Dispose();
                windowIcon.Dispose();
                if (scaledSmallFont != null) scaledSmallFont.Dispose();
                if (scaledBodyFont != null) scaledBodyFont.Dispose();
            }
            base.Dispose(disposing);
        }

        [DllImport("user32.dll")] private static extern bool ReleaseCapture();
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
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
            e.ArrowColor = UiMotion.Blend(Brand.Muted, Brand.Accent, MenuMotion.HoverAmount(e.Item));
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

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var button = e.Item as ToolStripButton;
            if (button != null && button.Checked)
                SettingsWindow.FillRound(e.Graphics, new RectangleF(0, 0, e.Item.Width, e.Item.Height), 6, Brand.SelectedSurface);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = Brand.RoundRect(new RectangleF(.5f, .5f, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1), 9))
            using (var pen = new Pen(Brand.Border)) e.Graphics.DrawPath(pen, path);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var item = e.Item as ToolStripMenuItem;
            bool isChecked = item != null && item.Checked;
            if (isChecked)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                SettingsWindow.FillRound(e.Graphics, new RectangleF(0, 0, e.Item.Width, e.Item.Height), 6,
                    Brand.SelectedSurface);
            }
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var item = e.Item as ToolStripMenuItem;
            e.TextColor = !e.Item.Enabled ? Brand.Muted : item != null && item.Checked ? Brand.Accent :
                UiMotion.Blend(Brand.Text, Brand.Accent, MenuMotion.HoverAmount(e.Item));
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
        internal float UiScale = 1;
        private int LogicalWidth { get { return (int)Math.Round(Width / UiScale); } }
        private int LogicalHeight { get { return (int)Math.Round(Height / UiScale); } }

        private bool selected;
        private readonly UiTween hoverMotion, selectionMotion, pressMotion;
        public string Detail { get; set; }
        public bool Compact { get; set; }
        public bool LanguageSelector { get; set; }
        public bool ThemeSelector { get; set; }
        public bool ProjectLink { get; set; }
        public bool Selected
        {
            get { return selected; }
            set
            {
                if (selected == value) return;
                selected = value;
                UpdateMotion(selectionMotion, value ? 1 : 0, 180);
            }
        }

        public HoverButton()
        {
            hoverMotion = new UiTween(delegate(double value) { Invalidate(); });
            selectionMotion = new UiTween(delegate(double value) { Invalidate(); });
            pressMotion = new UiTween(delegate(double value) { Invalidate(); });
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SystemCursors.UseHand(this);
            TabStop = true;
            AccessibleRole = AccessibleRole.PushButton;
        }

        private void UpdateMotion(UiTween motion, double target, int duration)
        {
            if (!IsHandleCreated || !Visible) motion.Snap(target);
            else motion.AnimateTo(target, duration);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            hoverMotion.Snap(0);
            pressMotion.Snap(0);
            selectionMotion.Snap(selected ? 1 : 0);
        }
        protected override void OnMouseEnter(EventArgs e) { UpdateMotion(hoverMotion, 1, 140); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { UpdateMotion(hoverMotion, 0, 180); UpdateMotion(pressMotion, 0, 100); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) UpdateMotion(pressMotion, 1, 70);
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e) { UpdateMotion(pressMotion, 0, 150); base.OnMouseUp(e); }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            if (!Capture) UpdateMotion(pressMotion, 0, 150);
            base.OnMouseCaptureChanged(e);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) UpdateMotion(pressMotion, 1, 70);
            base.OnKeyDown(e);
        }
        protected override void OnKeyUp(KeyEventArgs e) { UpdateMotion(pressMotion, 0, 150); base.OnKeyUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { UpdateMotion(pressMotion, 0, 100); Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Brand.Background);
            g.ScaleTransform(UiScale, UiScale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Color fill = UiMotion.Blend(Brand.Surface, Brand.SelectedSurface, selectionMotion.Value);
            float inset = (float)pressMotion.Value * 1.5f;
            var bounds = new RectangleF(.5f + inset, .5f + inset, LogicalWidth - 1 - inset * 2, LogicalHeight - 1 - inset * 2);
            SettingsWindow.FillRound(g, bounds, 9, fill);
            using (var path = Brand.RoundRect(bounds, 9))
            using (var pen = new Pen(UiMotion.Blend(Brand.Border, Brand.Accent, selectionMotion.Value)))
                g.DrawPath(pen, path);
            double emphasis = Focused && ShowFocusCues ? 1 : Math.Max(selectionMotion.Value, hoverMotion.Value);
            Color textColor = UiMotion.Blend(Brand.Text, Brand.Accent, emphasis);
            if (LanguageSelector)
            {
                Brand.DrawLanguageIcon(g, new Rectangle(10, (LogicalHeight - 21) / 2, 21, 21), textColor);
                UiDrawing.Text(g, Text, Font, new Rectangle(40, 0, LogicalWidth - 69, LogicalHeight),
                    textColor,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding | TextFormatFlags.EndEllipsis);
                int mid = LogicalHeight / 2;
                using (var pen = new Pen(UiMotion.Blend(Brand.Muted, Brand.Accent, emphasis), 1.5f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, selected
                        ? new[] { new Point(LogicalWidth - 20, mid - 2), new Point(LogicalWidth - 16, mid + 2), new Point(LogicalWidth - 12, mid - 2) }
                        : new[] { new Point(LogicalWidth - 20, mid + 2), new Point(LogicalWidth - 16, mid - 2), new Point(LogicalWidth - 12, mid + 2) });
                }
            }
            else if (ThemeSelector)
            {
                DrawThemeIcon(g, textColor);
            }
            else if (ProjectLink)
            {
                DrawGitHubIcon(g, textColor);
            }
            else if (Compact)
            {
                UiDrawing.Text(g, Text, Font, new Rectangle(0, 0, LogicalWidth, LogicalHeight), textColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            }
            else
            {
                UiDrawing.Text(g, Text, Font, new Rectangle(13, 5, LogicalWidth - 34, 21),
                    textColor, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
                using (var detailFont = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                    UiDrawing.Text(g, Detail, detailFont, new Rectangle(13, 25, LogicalWidth - 26, 16),
                        Brand.Muted, TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter);
                if (selected)
                {
                    using (var pen = new Pen(Brand.Accent, 1.7f))
                        g.DrawLines(pen, new[] { new Point(LogicalWidth - 24, 18), new Point(LogicalWidth - 20, 22), new Point(LogicalWidth - 13, 14) });
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                hoverMotion.Dispose();
                selectionMotion.Dispose();
                pressMotion.Dispose();
            }
            base.Dispose(disposing);
        }

        // GitHub Octicons mark-github-16, converted to GDI+ curves. See THIRD-PARTY-NOTICES.md.
        private void DrawGitHubIcon(Graphics graphics, Color color)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform((LogicalWidth - 20) / 2f, (LogicalHeight - 20) / 2f);
                graphics.ScaleTransform(20f / 16f, 20f / 16f);
                using (var path = new GraphicsPath())
                using (var brush = new SolidBrush(color))
                {
                    path.AddBezier(7.984f, 0f, 12.406f, 0f, 16.0f, 3.61f, 16.0f, 8.03f);
                    path.AddBezier(16.0f, 8.03f, 16.0f, 11.53f, 13.781f, 14.359f, 10.953f, 15.438f);
                    path.AddBezier(10.953f, 15.438f, 10.484f, 15.625f, 10.093f, 15.359f, 10.093f, 14.891f);
                    path.AddLine(10.093f, 14.891f, 10.093f, 13.266f);
                    path.AddBezier(10.093f, 13.266f, 10.093f, 12.406f, 9.734f, 11.656f, 9.203f, 11.312f);
                    path.AddBezier(9.203f, 11.312f, 11.297f, 11.047f, 12.75f, 9.594f, 12.75f, 7.672f);
                    path.AddBezier(12.75f, 7.672f, 12.75f, 6.859f, 12.484f, 6.062f, 11.984f, 5.469f);
                    path.AddBezier(11.984f, 5.469f, 12.188f, 4.937f, 12.156f, 3.844f, 11.938f, 3.422f);
                    path.AddBezier(11.938f, 3.422f, 11.313f, 3.344f, 10.453f, 3.672f, 9.969f, 4.109f);
                    path.AddBezier(9.969f, 4.109f, 9.406f, 3.938f, 8.781f, 3.844f, 8.016f, 3.844f);
                    path.AddBezier(8.016f, 3.844f, 7.25f, 3.844f, 6.625f, 3.938f, 6.031f, 4.125f);
                    path.AddBezier(6.031f, 4.125f, 5.531f, 3.672f, 4.688f, 3.344f, 4.063f, 3.422f);
                    path.AddBezier(4.063f, 3.422f, 3.828f, 3.875f, 3.797f, 4.969f, 4.0f, 5.484f);
                    path.AddBezier(4.0f, 5.484f, 3.531f, 6.047f, 3.25f, 6.891f, 3.25f, 7.672f);
                    path.AddBezier(3.25f, 7.672f, 3.25f, 9.594f, 4.703f, 11.078f, 6.766f, 11.328f);
                    path.AddBezier(6.766f, 11.328f, 6.579f, 11.484f, 6.375f, 11.719f, 6.109f, 11.984f);
                    path.AddBezier(6.109f, 11.984f, 5.75f, 12.344f, 5.5f, 12.484f, 5.109f, 12.484f);
                    path.AddBezier(5.109f, 12.484f, 4.718f, 12.484f, 4.391f, 12.281f, 4.078f, 11.829f);
                    path.AddBezier(4.078f, 11.829f, 3.672f, 11.25f, 3.281f, 10.969f, 2.828f, 10.969f);
                    path.AddBezier(2.828f, 10.969f, 2.516f, 10.969f, 2.203f, 11.109f, 2.203f, 11.297f);
                    path.AddBezier(2.203f, 11.297f, 2.203f, 11.391f, 2.266f, 11.469f, 2.453f, 11.484f);
                    path.AddBezier(2.453f, 11.484f, 2.812f, 11.531f, 3.0f, 11.781f, 3.172f, 12.203f);
                    path.AddBezier(3.172f, 12.203f, 3.61f, 13.25f, 4.219f, 13.812f, 5.25f, 13.812f);
                    path.AddBezier(5.25f, 13.812f, 5.5f, 13.812f, 5.781f, 13.75f, 6.0f, 13.656f);
                    path.AddLine(6.0f, 13.656f, 6.0f, 14.906f);
                    path.AddBezier(6.0f, 14.906f, 6.0f, 15.328f, 5.594f, 15.609f, 5.172f, 15.453f);
                    path.AddBezier(5.172f, 15.453f, 2.0582f, 14.3135f, -0.0092f, 11.3467f, 0f, 8.031f);
                    path.AddBezier(0f, 8.031f, 0f, 3.61f, 3.563f, 0f, 7.984f, 0f);
                    path.CloseFigure();
                    graphics.FillPath(brush, path);
                }
            }
            finally { graphics.Restore(state); }
        }

        private void DrawThemeIcon(Graphics graphics, Color color)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.TranslateTransform((LogicalWidth - 20) / 2f, (LogicalHeight - 20) / 2f);
                using (var pen = new Pen(color, 1.6f))
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
        internal float UiScale = 1;
        private int LogicalWidth { get { return (int)Math.Round(Width / UiScale); } }
        private int LogicalHeight { get { return (int)Math.Round(Height / UiScale); } }

        private bool isChecked;
        private readonly UiTween checkMotion, hoverMotion;
        public string Detail { get; set; }
        public string DefaultActionText { get; set; }
        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                UpdateMotion(checkMotion, value ? 1 : 0, 180);
                AccessibilityNotifyClients(AccessibleEvents.StateChange, -1);
            }
        }

        public HoverToggle()
        {
            checkMotion = new UiTween(delegate(double value) { Invalidate(); });
            hoverMotion = new UiTween(delegate(double value) { Invalidate(); });
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            SystemCursors.UseHand(this);
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
        }

        private void UpdateMotion(UiTween motion, double target, int duration)
        {
            if (!IsHandleCreated || !Visible) motion.Snap(target);
            else motion.AnimateTo(target, duration);
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            hoverMotion.Snap(0);
            checkMotion.Snap(isChecked ? 1 : 0);
        }
        protected override void OnMouseEnter(EventArgs e) { UpdateMotion(hoverMotion, 1, 140); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { UpdateMotion(hoverMotion, 0, 180); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Brand.Background);
            g.ScaleTransform(UiScale, UiScale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            double emphasis = Focused && ShowFocusCues ? 1 : hoverMotion.Value;
            UiDrawing.Text(g, Text, Font, new Rectangle(0, 0, LogicalWidth - 58, 22), UiMotion.Blend(Brand.Text, Brand.Accent, emphasis),
                TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            using (var detailFont = new Font("Segoe UI", 11, FontStyle.Regular, GraphicsUnit.Pixel))
                UiDrawing.Text(g, Detail, detailFont, new Rectangle(0, 22, LogicalWidth - 58, 16), Brand.Muted,
                    TextFormatFlags.NoPadding | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
            float trackTop = (LogicalHeight - 24) / 2f;
            SettingsWindow.FillRound(g, new RectangleF(LogicalWidth - 43, trackTop, 42, 24), 12,
                UiMotion.Blend(Brand.ToggleOff, Brand.Accent, checkMotion.Value));
            using (var brush = new SolidBrush(UiMotion.Blend(Brand.ToggleThumb,
                Brand.IsLight ? Brand.ToggleThumb : Brand.Background, checkMotion.Value)))
                g.FillEllipse(brush, LogicalWidth - 40 + 18 * (float)checkMotion.Value, trackTop + 3, 18, 18);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { checkMotion.Dispose(); hoverMotion.Dispose(); }
            base.Dispose(disposing);
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
        internal float UiScale = 1;
        private int LogicalWidth { get { return (int)Math.Round(Width / UiScale); } }
        private int LogicalHeight { get { return (int)Math.Round(Height / UiScale); } }

        private int value;
        private bool dragging, hovering;
        private readonly UiTween valueMotion, hoverMotion;
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
                // The saved value changes immediately; only the painted thumb eases into place.
                if (!IsHandleCreated || !Visible || dragging) valueMotion.Snap(next);
                else valueMotion.AnimateTo(next, 150);
                AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1);
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        public HoverSlider()
        {
            valueMotion = new UiTween(delegate(double next) { Invalidate(); });
            hoverMotion = new UiTween(delegate(double next) { Invalidate(); });
            Minimum = 320;
            Maximum = 1600;
            Step = 40;
            value = 720;
            valueMotion.Snap(value);
            TabStop = true;
            SystemCursors.UseHand(this);
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
            double ratio = Math.Max(0, Math.Min(1, (x - 10.0 * UiScale) / Math.Max(1, Width - 20 * UiScale)));
            int newValue = Minimum + (int)Math.Round((Maximum - Minimum) * ratio / Step) * Step;
            Value = newValue;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            dragging = true;
            UpdateHover();
            Capture = true;
            SetFromX(e.X);
        }
        protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if (dragging) SetFromX(e.X); }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); dragging = false; Capture = false; UpdateHover(); }
        protected override void OnMouseCaptureChanged(EventArgs e)
        {
            base.OnMouseCaptureChanged(e);
            if (!Capture) { dragging = false; UpdateHover(); }
        }
        protected override void OnMouseEnter(EventArgs e) { hovering = true; UpdateHover(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hovering = false; UpdateHover(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { UpdateHover(); Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { UpdateHover(); Invalidate(); base.OnLostFocus(e); }
        private void UpdateHover()
        {
            double target = hovering || dragging || Focused ? 1 : 0;
            if (!IsHandleCreated || !Visible) hoverMotion.Snap(target);
            else hoverMotion.AnimateTo(target, 150);
        }
        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            dragging = hovering = false;
            hoverMotion.Snap(0);
            valueMotion.Snap(value);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Brand.Background);
            g.ScaleTransform(UiScale, UiScale);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            double shownValue = Math.Max(Minimum, Math.Min(Maximum, valueMotion.Value));
            float x = 10 + (LogicalWidth - 20) * (float)(shownValue - Minimum) / Math.Max(1, Maximum - Minimum);
            SettingsWindow.FillRound(g, new RectangleF(10, 12, LogicalWidth - 20, 5), 2.5f, Brand.Border);
            if (x > 10) SettingsWindow.FillRound(g, new RectangleF(10, 12, x - 10, 5), 2.5f, Brand.Accent);
            float radius = 7 + (float)hoverMotion.Value * 1.5f;
            using (var brush = new SolidBrush(UiMotion.Blend(Brand.Accent, Brand.AccentHover, hoverMotion.Value)))
                g.FillEllipse(brush, x - radius, 14.5f - radius, radius * 2, radius * 2);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { valueMotion.Dispose(); hoverMotion.Dispose(); }
            base.Dispose(disposing);
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
