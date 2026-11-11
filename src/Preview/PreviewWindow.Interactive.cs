using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private readonly bool interactive;
        private ToolStrip toolbar;
        private ToolStripButton previousButton, nextButton, playbackButton, zoomOutButton, zoomInButton, fitButton;
        private ToolStripButton refreshButton;
        private Timer refreshTimer;
        private string observedChange;
        private ToolStripLabel pageLabel, zoomLabel, loadingLabel;
        private ToolStripSeparator zoomSeparator;
        private readonly ToolTip contentTip = UiDrawing.CreateToolTip();
        private Font toolbarFont;
        private int pdfPageIndex, pdfPageCount, textOffset, wheelRemainder;
        private bool playbackPaused, panning, interactiveSized, arrangingInteractive, fitMode = true;
        private float zoom = 1;
        private int documentReferenceWidth;
        private Point pan, panStart, mouseStart;
        private Rectangle viewport;
        private bool autoPlayMedia = true;
        private FormWindowState pinnedRestoreState = FormWindowState.Normal;
        private FormWindowState lastPinnedWindowState = FormWindowState.Normal;
        private Rectangle pinnedNormalBounds;
        private bool restoringPinnedBounds;

        internal bool IsInteractive { get { return interactive; } }

        internal void ActivatePinned()
        {
            if (!interactive || IsDisposed) return;
            if (WindowState == FormWindowState.Minimized) WindowState = pinnedRestoreState;
            if (!Visible) ShowPreview();
            Activate();
        }

        private void ShowPreview()
        {
            // An asynchronous result must not move, restore or reactivate a pinned window.
            if (!interactive || !Visible) entranceMotion.ShowAt(Location);
            RaiseHoverPreview();
        }

        private void RaiseHoverPreview()
        {
            if (interactive || !Visible || !IsHandleCreated || IsDisposed) return;
            // Form.TopMost can remain true after native z-order changes. Reassert the
            // actual window order for every preview, including asynchronous image loads.
            // Preserve the entrance animation's position and the foreground app's focus.
            SetWindowPos(Handle, new IntPtr(-1), 0, 0, 0, 0,
                SwpNoMove | SwpNoSize | SwpNoActivate);
        }

        private void InitializeInteractive()
        {
            if (!interactive) return;
            ShowInTaskbar = true; TopMost = false; MaximizeBox = MinimizeBox = true;
            AccessibleRole = AccessibleRole.Window; KeyPreview = true;
            toolbarFont = new Font("Segoe UI", 12, FontStyle.Regular, GraphicsUnit.Pixel);
            toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.None,
                AutoSize = false, BackColor = Brand.Background, ForeColor = Brand.Text, Font = toolbarFont,
                Renderer = new PinnedToolbarRenderer(), ShowItemToolTips = true, CanOverflow = true };
            SystemCursors.UseHand(toolbar);
            previousButton = AddAction("‹", "previous", () => Navigate(-1));
            pageLabel = new ToolStripLabel { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
            toolbar.Items.Add(pageLabel);
            nextButton = AddAction("›", "next", () => Navigate(1));
            playbackButton = AddAction("Ⅱ", "play_pause", TogglePlayback);
            zoomSeparator = new ToolStripSeparator(); toolbar.Items.Add(zoomSeparator);
            zoomOutButton = AddAction("−", "zoom_out", () => ChangeZoom(1 / 1.25f));
            zoomLabel = new ToolStripLabel { AutoSize = false, TextAlign = ContentAlignment.MiddleCenter };
            toolbar.Items.Add(zoomLabel);
            zoomInButton = AddAction("+", "zoom_in", () => ChangeZoom(1.25f));
            fitButton = AddAction("", "fit", FitImage);
            refreshButton = AddAction("↻", "refresh_preview", RefreshPinned);
            loadingLabel = new ToolStripLabel(); toolbar.Items.Add(loadingLabel);
            Controls.Add(toolbar);
            InitializeOfficeView();
            InitializeMediaControls();
            InitializePinnedChrome();
            refreshTimer = new Timer { Interval = 100 };
            refreshTimer.Tick += delegate { CheckFileChanges(); };
            refreshTimer.Start();
        }

        private ToolStripButton AddAction(string text, string key, Action action)
        {
            var button = new ToolStripButton(text) { Name = key, Tag = key, AutoSize = false,
                DisplayStyle = ToolStripItemDisplayStyle.Text, Margin = new Padding(2, 0, 2, 0) };
            button.Click += delegate { action(); };
            toolbar.Items.Add(button); return button;
        }

        private void PositionContent(Point cursor, Rectangle item, Rectangle area)
        {
            if (!interactive) Location = ChooseLocation(cursor, item, area, Size, Px(18));
            else LayoutInteractive();
        }

        private void LayoutInteractive()
        {
            if (!interactive || toolbar == null || arrangingInteractive || IsDisposed || WindowState == FormWindowState.Minimized) return;
            arrangingInteractive = true;
            try
            {
                if (!interactiveSized && CurrentPath != null)
                {
                    Rectangle work = Screen.FromPoint(mediaCursor).WorkingArea;
                    MinimumSize = new Size(Math.Min(Px(520), work.Width), Math.Min(Px(360), work.Height));
                    ClientSize = new Size(Math.Min(Px(900), Math.Max(1, work.Width - Px(32))),
                        Math.Min(Px(680), Math.Max(1, work.Height - Px(32))));
                    Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
                    interactiveSized = true;
                }
                float displayedWidth = imageArea.Width * zoom;
                Rectangle previousViewport = viewport;
                if (Math.Abs(toolbarFont.Size - 12 * scale) > .01f)
                {
                    Font previous = toolbarFont;
                    toolbarFont = new Font("Segoe UI", 12 * scale, FontStyle.Regular, GraphicsUnit.Pixel);
                    toolbar.Font = toolbarFont; previous.Dispose();
                }
                bool showTools = CurrentPath != null;
                int header = Px(44), tools = showTools ? Px(44) : 0, padding = Px(12), footer = Px(40);
                toolbar.Visible = showTools;
                toolbar.SetBounds(1, header, Math.Max(1, ClientSize.Width - 2), tools);
                toolbar.Padding = new Padding(Px(10), Px(6), Px(10), Px(6));
                foreach (ToolStripItem item in toolbar.Items)
                    if (item is ToolStripButton) item.Size = new Size(Px(34), Px(30));
                pageLabel.Size = new Size(Px(80), Px(30)); zoomLabel.Size = new Size(Px(62), Px(30));
                int transport = LayoutMediaControls(header + tools);
                viewport = new Rectangle(padding, header + tools + transport + padding,
                    Math.Max(1, ClientSize.Width - padding * 2),
                    Math.Max(1, ClientSize.Height - header - tools - transport - footer - padding * 2));
                LayoutOfficeView();
                Size source = preview == null ? Size.Empty : preview.Size;
                if (media != null && media.IsOpen && media.HasVideo) source = new Size(media.NaturalWidth, media.NaturalHeight);
                imageArea = viewport;
                if (!source.IsEmpty)
                {
                    double fit = Math.Min((double)viewport.Width / source.Width, (double)viewport.Height / source.Height);
                    Size fitted = new Size(Math.Max(1, (int)Math.Round(source.Width * fit)), Math.Max(1, (int)Math.Round(source.Height * fit)));
                    imageArea = new Rectangle(viewport.X + (viewport.Width - fitted.Width) / 2,
                        viewport.Y + (viewport.Height - fitted.Height) / 2, fitted.Width, fitted.Height);
                    if (!fitMode && previousViewport != viewport && previousViewport.Width > 0 && preview != null)
                        zoom = Math.Max(.1f, Math.Min(16, displayedWidth / Math.Max(1, imageArea.Width)));
                }
                if (fitMode) { zoom = 1; pan = Point.Empty; }
                ClampPan();
                titleArea = new Rectangle(padding, ClientSize.Height - footer, ClientSize.Width - padding * 2, Px(18));
                descriptionArea = new Rectangle(padding, ClientSize.Height - Px(32), ClientSize.Width - padding * 2, Px(22));
                if (textPreview != null)
                {
                    bool table = textPreview.TableRows != null;
                    textLineHeight = Px(table ? 26 : 21);
                    textVisibleLines = Math.Max(1, (viewport.Height - (table ? textLineHeight : Px(16))) / textLineHeight);
                    textOffset = Math.Max(0, Math.Min(textOffset, Math.Max(0, textPreview.RowCount - textVisibleLines)));
                    if (table) LayoutTable();
                    else using (Graphics g = CreateGraphics())
                        textGutterWidth = Math.Max(Px(38), TextRenderer.MeasureText(g, Math.Max(1, textPreview.Lines.Length).ToString(), codeFont).Width + Px(20));
                }
                if (media != null && media.IsOpen && media.HasVideo) media.SetVideoBounds(imageArea);
                Text = String.IsNullOrEmpty(fileName) ? L10n.Text(language, "pinned_preview") : fileName + " — Desktop Hover";
                UpdatePinnedChrome(); UpdateToolbar(); Invalidate(); QueueImageDetail();
            }
            finally { arrangingInteractive = false; }
        }

        private bool HasPlayback { get { return (documentSlides != null && documentSlides.Length > 1) || animatedGif != null || (media != null && media.IsOpen); } }
        private bool IsPreviewLoading { get { return textTask != null || imageTask != null || documentLoading || mediaState == "media_loading" ||
            mediaState == "preview_loading" || mediaState == "document_loading"; } }

        private void UpdateToolbar()
        {
            if (toolbar == null) return;
            UpdateMediaControls();
            toolbar.BackColor = Brand.Background; toolbar.ForeColor = Brand.Text;
            refreshButton.Enabled = CurrentPath != null;
            foreach (ToolStripItem item in toolbar.Items)
                if (item.Tag is string)
                {
                    item.ToolTipText = L10n.Text(language, (string)item.Tag);
                    item.AccessibleName = item.ToolTipText;
                }
            bool slides = documentSlides != null && documentSlides.Length > 0;
            int count = slides ? documentSlides.Length : pdfPageCount;
            int index = slides ? documentSlideIndex : pdfPageIndex;
            bool pages = count > 1;
            previousButton.Visible = nextButton.Visible = pageLabel.Visible = pages;
            previousButton.Enabled = pages && index > 0 && !documentLoading;
            nextButton.Enabled = pages && index + 1 < count && !documentLoading;
            pageLabel.Text = (index + 1) + " / " + (slides ? Math.Max(documentSlideTotal, count) : count);
            if (HasTable && textPreview.TableColumns > tableVisibleColumns)
            {
                previousButton.Visible = nextButton.Visible = pageLabel.Visible = true;
                previousButton.Enabled = tableColumn > 0;
                nextButton.Enabled = tableColumn + tableVisibleColumns < textPreview.TableColumns;
                pageLabel.Text = OfficePreviewReader.ColumnName(tableColumn) + "–" + OfficePreviewReader.ColumnName(tableColumn + tableVisibleColumns - 1);
            }
            playbackButton.Visible = HasPlayback;
            playbackButton.Enabled = HasPlayback;
            playbackButton.Text = playbackPaused ? "▶" : "Ⅱ";
            playbackButton.ToolTipText = L10n.Text(language, playbackPaused ? "play" : "pause") + " (Space)";
            playbackButton.AccessibleName = playbackButton.ToolTipText;
            bool canZoom = preview != null;
            zoomOutButton.Visible = zoomInButton.Visible = fitButton.Visible = zoomLabel.Visible = canZoom;
            zoomSeparator.Visible = canZoom && (pages || HasPlayback);
            zoomOutButton.Enabled = canZoom && zoom > .1001f;
            zoomInButton.Enabled = canZoom && zoom < 15.999f;
            fitButton.Enabled = canZoom;
            if (preview != null && documentReferenceWidth == 0) documentReferenceWidth = preview.Width;
            // Detail decoding improves pixels without changing the document's scale.
            int sourcePixels = mediaKind == MediaKind.Image && sourceWidth > 0 ? sourceWidth : documentReferenceWidth;
            zoomLabel.Text = Math.Round(imageArea.Width * zoom * 100 / Math.Max(1, sourcePixels)) + "%";
            zoomLabel.ToolTipText = L10n.Text(language, "zoom_level");
            fitButton.Text = L10n.Text(language, "fit");
            fitButton.Checked = fitMode;
            loadingLabel.Visible = IsPreviewLoading;
            loadingLabel.Text = L10n.Text(language, "preview_loading");
            toolbar.Invalidate();
        }

        private void Navigate(int delta)
        {
            if (HasTable) { ScrollTableColumns(delta * tableVisibleColumns); return; }
            if (documentLoading) return;
            if (documentSlides != null && documentSlides.Length > 0)
            {
                int target = Math.Max(0, Math.Min(documentSlides.Length - 1, documentSlideIndex + delta));
                if (target == documentSlideIndex) return;
                playbackPaused = true; slideshowTimer.Stop();
                ShowDocumentSlide(target); ClampPan(); UpdateToolbar();
            }
            else if (pdfPageCount > 1)
            {
                int target = Math.Max(0, Math.Min(pdfPageCount - 1, pdfPageIndex + delta));
                if (target != pdfPageIndex) LoadPdfPage(target);
            }
        }

        private void LoadPdfPage(int index)
        {
            CancelImageDetail();
            CancelDocumentLoad(); pdfPageIndex = index;
            documentLoading = true; mediaState = "document_loading";
            documentProgress = new DocumentProgress();
            documentCancellation = new System.Threading.CancellationTokenSource();
            var token = documentCancellation.Token;
            string path = CurrentPath; var size = new Size(1600, 1200);
            documentTask = Task.Factory.StartNew(delegate
            {
                int count;
                Bitmap image = DocumentPreviewWorker.TryLoadPdfPage(path, size, token, index, out count);
                var result = new DocumentPreviewResult(image, image == null ? "document_pdf_unavailable" : "document_pdf") { PageCount = count };
                try { result.FileBytes = new System.IO.FileInfo(path).Length; } catch { }
                return result;
            }, System.Threading.CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
            LayoutDocument(); UpdateToolbar();
            mediaTimer.Interval = 40; mediaTimer.Start(); Invalidate();
        }

        private void TogglePlayback()
        {
            if (!HasPlayback) return;
            playbackPaused = !playbackPaused;
            if (media != null && media.IsOpen)
            {
                MediaPlayback session = media;
                if (playbackPaused) session.Pause(); else session.Play();
                if (media != session) return;
                mediaState = playbackPaused ? "media_paused" : "media_playing";
                mediaTimer.Start(); UpdateMediaDescription();
            }
            if (playbackPaused) { slideshowTimer.Stop(); animationTimer.Stop(); }
            else
            {
                if (documentSlides != null && documentSlides.Length > 1) slideshowTimer.Start();
                if (animatedGif != null)
                {
                    if (!animatedGif.CanAdvance) animatedGif.Restart();
                    animationTimer.Interval = animatedGif.FrameDelay;
                    if (animatedGif.CanAdvance) animationTimer.Start();
                }
            }
            UpdateToolbar(); Invalidate();
        }

        private void FitImage()
        {
            if (preview == null) return;
            fitMode = true; zoom = 1; pan = Point.Empty;
            UpdateToolbar(); Invalidate(viewport);
        }

        private void ChangeZoom(float factor)
        { ZoomAt(factor, new Point(viewport.Left + viewport.Width / 2, viewport.Top + viewport.Height / 2)); }

        private void ZoomAt(float factor, Point anchor)
        {
            if (preview == null || imageArea.Width <= 0) return;
            Rectangle before = ZoomedImageArea();
            float next = Math.Max(.1f, Math.Min(16, zoom * factor));
            if (Math.Abs(next - zoom) < .0001f) return;
            anchor = new Point(Math.Max(viewport.Left, Math.Min(viewport.Right, anchor.X)), Math.Max(viewport.Top, Math.Min(viewport.Bottom, anchor.Y)));
            double imageX = (anchor.X - before.Left) / (double)before.Width;
            double imageY = (anchor.Y - before.Top) / (double)before.Height;
            zoom = next; fitMode = false;
            int width = (int)Math.Round(imageArea.Width * zoom), height = (int)Math.Round(imageArea.Height * zoom);
            pan = new Point((int)Math.Round(anchor.X - imageX * width - imageArea.X - (imageArea.Width - width) / 2.0),
                (int)Math.Round(anchor.Y - imageY * height - imageArea.Y - (imageArea.Height - height) / 2.0));
            ClampPan(); UpdateToolbar(); Invalidate(viewport); QueueImageDetail();
        }

        private Rectangle ZoomedImageArea()
        {
            if (!interactive) return imageArea;
            int width = Math.Max(1, (int)Math.Round(imageArea.Width * zoom)), height = Math.Max(1, (int)Math.Round(imageArea.Height * zoom));
            return new Rectangle(imageArea.X + (imageArea.Width - width) / 2 + pan.X,
                imageArea.Y + (imageArea.Height - height) / 2 + pan.Y, width, height);
        }

        private void ClampPan()
        {
            int x = Math.Max(0, (int)Math.Ceiling((imageArea.Width * zoom - viewport.Width) / 2));
            int y = Math.Max(0, (int)Math.Ceiling((imageArea.Height * zoom - viewport.Height) / 2));
            pan = new Point(Math.Max(-x, Math.Min(x, pan.X)), Math.Max(-y, Math.Min(y, pan.Y)));
        }

        protected override void OnResize(EventArgs e)
        {
            if (interactive && WindowState == FormWindowState.Normal && lastPinnedWindowState != FormWindowState.Normal &&
                !pinnedNormalBounds.IsEmpty && !restoringPinnedBounds)
            {
                Rectangle bounds = pinnedNormalBounds;
                restoringPinnedBounds = true;
                // WinForms reapplies native frame padding after WM_SIZE. Restore
                // our borderless bounds once that native transition has completed.
                BeginInvoke((MethodInvoker)delegate
                {
                    restoringPinnedBounds = false;
                    if (!IsDisposed && WindowState == FormWindowState.Normal) Bounds = bounds;
                });
            }
            lastPinnedWindowState = WindowState;
            base.OnResize(e);
            if (interactive)
            {
                if (WindowState != FormWindowState.Minimized) pinnedRestoreState = WindowState;
                LayoutInteractive();
                if (WindowState == FormWindowState.Normal && !restoringPinnedBounds) pinnedNormalBounds = Bounds;
            }
        }
        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            if (interactive && WindowState == FormWindowState.Normal && lastPinnedWindowState == FormWindowState.Normal && !restoringPinnedBounds)
                pinnedNormalBounds = Bounds;
        }
        protected override void OnResizeBegin(EventArgs e) { if (entranceMotion != null) entranceMotion.Finish(); base.OnResizeBegin(e); }
        protected override void OnResizeEnd(EventArgs e)
        {
            base.OnResizeEnd(e);
            if (!interactive) return;
            scale = GetScale(Location); SetFonts();
            LayoutInteractive();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (interactive)
            {
                if (keyData != Keys.F5 && keyData != Keys.Escape &&
                    ((mediaSeek != null && (mediaSeek.Focused || mediaVolume.Focused || mediaMute.Focused)) ||
                     (sheetPicker != null && sheetPicker.ComboBox.Focused)))
                    return base.ProcessCmdKey(ref msg, keyData);
                if (HasTable)
                {
                    switch (keyData)
                    {
                        case Keys.Left: ScrollTableColumns(-1); return true;
                        case Keys.Right: ScrollTableColumns(1); return true;
                        case Keys.Up: ScrollText(-1); return true;
                        case Keys.Down: ScrollText(1); return true;
                        case Keys.PageUp: ScrollText(-textVisibleLines); return true;
                        case Keys.PageDown: ScrollText(textVisibleLines); return true;
                    }
                }
                switch (keyData)
                {
                    case Keys.F5: RefreshPinned(); return true;
                    case Keys.Escape: Close(); return true;
                    case Keys.Space: if (HasPlayback) { TogglePlayback(); return true; } break;
                    case Keys.Left: case Keys.PageUp: Navigate(-1); return true;
                    case Keys.Right: case Keys.PageDown: Navigate(1); return true;
                    case Keys.Add: case Keys.Oemplus: case Keys.Shift | Keys.Oemplus: ChangeZoom(1.25f); return true;
                    case Keys.Subtract: case Keys.OemMinus: ChangeZoom(1 / 1.25f); return true;
                    case Keys.D0: case Keys.NumPad0: FitImage(); return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!interactive || e.Delta == 0) return;
            wheelRemainder += e.Delta;
            int steps = wheelRemainder / 120; wheelRemainder %= 120;
            if (steps == 0) return;
            if (IsOfficeView && (ModifierKeys & Keys.Control) == 0) { ScrollOffice(steps); return; }
            if (mediaKind == MediaKind.Text && textPreview != null)
            {
                if (HasTable && (ModifierKeys & Keys.Shift) != 0) { ScrollTableColumns(-steps); return; }
                int lines = SystemInformation.MouseWheelScrollLines;
                if (lines < 0) lines = textVisibleLines;
                ScrollText(-steps * lines); return;
            }
            if ((ModifierKeys & Keys.Control) == 0 && (pdfPageCount > 1 || documentSlides != null)) Navigate(-steps);
            else ZoomAt((float)Math.Pow(1.25, steps), e.Location);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Rectangle displayed = ZoomedImageArea();
            if (interactive && preview != null && e.Button == MouseButtons.Left && viewport.Contains(e.Location) &&
                (displayed.Width > viewport.Width || displayed.Height > viewport.Height))
            { panning = true; mouseStart = e.Location; panStart = pan; Capture = true; Cursor = Cursors.SizeAll; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (interactive && HasTable) UpdateTableTip(e.Location);
            base.OnMouseMove(e);
            if (!panning) return;
            pan = new Point(panStart.X + e.X - mouseStart.X, panStart.Y + e.Y - mouseStart.Y);
            ClampPan(); Invalidate(viewport);
        }
        private void StopPanning() { panning = false; Capture = false; Cursor = Cursors.Default; }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); if (interactive) StopPanning(); }
        protected override void OnMouseCaptureChanged(EventArgs e)
        { base.OnMouseCaptureChanged(e); if (!Capture) { panning = false; Cursor = Cursors.Default; } }
        protected override void OnMouseDoubleClick(MouseEventArgs e)
        { base.OnMouseDoubleClick(e); if (interactive && e.Button == MouseButtons.Left && viewport.Contains(e.Location)) FitImage(); }
    }

    internal sealed class PinnedToolbarRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e) { WindowBackdrop.Clear(e.Graphics, e.ToolStrip); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        { using (var pen = new Pen(Brand.Border)) e.Graphics.DrawLine(pen, 10, e.ToolStrip.Height - 1, e.ToolStrip.Width - 10, e.ToolStrip.Height - 1); }
        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e) { }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        { using (var pen = new Pen(Brand.Border)) e.Graphics.DrawLine(pen, e.Item.Width / 2, 7, e.Item.Width / 2, e.Item.Height - 7); }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            var button = e.Item as ToolStripButton;
            if (button == null)
            {
                e.TextColor = Brand.Muted; base.OnRenderItemText(e); return;
            }
            Graphics g = e.Graphics; GraphicsState state = g.Save();
            try
            {
                float s = e.Item.Height / 30f;
                g.TranslateTransform(e.Item.Width / 2f, e.Item.Height / 2f); g.ScaleTransform(s, s);
                g.SmoothingMode = SmoothingMode.AntiAlias;
                Color color = !button.Enabled ? Brand.Muted : button.Selected || button.Checked ? Brand.Accent : Brand.Text;
                using (var pen = new Pen(color, 1.7f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                using (var brush = new SolidBrush(color))
                {
                    string key = button.Tag as string;
                    if (key == "previous" || key == "next")
                    {
                        int direction = key == "next" ? 1 : -1;
                        g.DrawLines(pen, new[] { new PointF(-3 * direction, -6), new PointF(3 * direction, 0), new PointF(-3 * direction, 6) });
                    }
                    else if (key == "play_pause")
                    {
                        if (button.Text == "▶") g.FillPolygon(brush, new[] { new PointF(-4, -7), new PointF(7, 0), new PointF(-4, 7) });
                        else { g.DrawLine(pen, -3, -6, -3, 6); g.DrawLine(pen, 3, -6, 3, 6); }
                    }
                    else if (key == "fit")
                    {
                        g.DrawLines(pen, new[] { new PointF(-2, -6), new PointF(-7, -6), new PointF(-7, -1) });
                        g.DrawLines(pen, new[] { new PointF(2, -6), new PointF(7, -6), new PointF(7, -1) });
                        g.DrawLines(pen, new[] { new PointF(-7, 1), new PointF(-7, 6), new PointF(-2, 6) });
                        g.DrawLines(pen, new[] { new PointF(7, 1), new PointF(7, 6), new PointF(2, 6) });
                    }
                    else if (key == "refresh_preview")
                    {
                        for (int half = 0; half < 2; half++)
                        {
                            g.DrawBezier(pen, -7.5f, -2, -5.9f, -8.7f, 2, -9.3f, 7.1f, -3.1f);
                            g.DrawLines(pen, new[] { new PointF(7.1f, -7.8f), new PointF(7.1f, -3.1f), new PointF(2.4f, -3.1f) });
                            g.RotateTransform(180);
                        }
                    }
                    else
                    {
                        g.DrawEllipse(pen, -7, -7, 11, 11); g.DrawLine(pen, 3, 3, 7, 7);
                        g.DrawLine(pen, -4.5f, -1.5f, 1.5f, -1.5f);
                        if (key == "zoom_in") g.DrawLine(pen, -1.5f, -4.5f, -1.5f, 1.5f);
                    }
                }
            }
            finally { g.Restore(state); }
        }
    }
}
