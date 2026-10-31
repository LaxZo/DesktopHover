using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private readonly bool interactive;
        private ToolStrip toolbar;
        private ToolStripButton previousButton, nextButton, playbackButton, zoomOutButton, zoomInButton, fitButton;
        private ToolStripLabel pageLabel, zoomLabel;
        private readonly ToolTip contentTip = new ToolTip { ShowAlways = true };
        private Font toolbarFont;
        private int pdfPageIndex, pdfPageCount, textOffset;
        private bool playbackPaused, panning;
        private float zoom = 1;
        private Point pan, panStart, mouseStart;
        private bool autoPlayMedia = true;

        internal bool IsInteractive { get { return interactive; } }

        private void InitializeInteractive()
        {
            if (!interactive) return;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            ShowInTaskbar = true; TopMost = false; MaximizeBox = false; MinimizeBox = true;
            KeyPreview = true;
            toolbarFont = new Font("Segoe UI", 14, FontStyle.Regular, GraphicsUnit.Pixel);
            toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.None,
                AutoSize = false, BackColor = Brand.Surface, ForeColor = Brand.Text, Font = toolbarFont,
                Renderer = new HoverMenuRenderer(), ShowItemToolTips = true };
            previousButton = AddAction("‹", "previous", () => Navigate(-1));
            playbackButton = AddAction("Ⅱ", "play_pause", TogglePlayback);
            nextButton = AddAction("›", "next", () => Navigate(1));
            pageLabel = new ToolStripLabel { AutoSize = false, Width = 85, TextAlign = ContentAlignment.MiddleCenter };
            toolbar.Items.Add(pageLabel); toolbar.Items.Add(new ToolStripSeparator());
            zoomOutButton = AddAction("−", "zoom_out", () => ChangeZoom(1 / 1.25f));
            zoomLabel = new ToolStripLabel { AutoSize = false, Width = 65, TextAlign = ContentAlignment.MiddleCenter };
            toolbar.Items.Add(zoomLabel);
            zoomInButton = AddAction("+", "zoom_in", () => ChangeZoom(1.25f));
            fitButton = AddAction("", "fit", () => { zoom = 1; pan = Point.Empty; UpdateToolbar(); Invalidate(); });
            Controls.Add(toolbar);
        }
        private ToolStripButton AddAction(string text, string key, Action action)
        {
            var button = new ToolStripButton(text) { Name = key, Tag = key, AutoSize = false, Width = 44,
                DisplayStyle = ToolStripItemDisplayStyle.Text };
            button.Click += delegate { action(); };
            toolbar.Items.Add(button); return button;
        }

        private void PositionContent(Point cursor, Rectangle item, Rectangle area)
        {
            if (!interactive) { Location = ChooseLocation(cursor, item, area, Size, Px(18)); return; }
            int top = Px(40);
            imageArea.Offset(0, top); titleArea.Offset(0, top); descriptionArea.Offset(0, top);
            ClientSize = new Size(ClientSize.Width, ClientSize.Height + top);
            toolbar.SetBounds(0, 0, ClientSize.Width, top);
            if (!Visible)
                Location = new Point(area.Left + Math.Max(0, (area.Width - Width) / 2),
                    area.Top + Math.Max(0, (area.Height - Height) / 2));
            else
                Location = new Point(Math.Max(area.Left, Math.Min(Left, area.Right - Width)),
                    Math.Max(area.Top, Math.Min(Top, area.Bottom - Height)));
            Text = fileName + " — Desktop Hover";
            UpdateToolbar();
            contentTip.SetToolTip(this, fileName + "\n" + description);
        }

        private void UpdateToolbar()
        {
            if (toolbar == null) return;
            toolbar.BackColor = Brand.Surface; toolbar.ForeColor = Brand.Text;
            foreach (ToolStripItem item in toolbar.Items)
            {
                if (item.Tag is string)
                {
                    item.ToolTipText = L10n.Text(language, (string)item.Tag);
                    item.AccessibleName = item.ToolTipText;
                }
            }
            bool slides = documentSlides != null && documentSlides.Length > 0;
            int count = slides ? documentSlides.Length : pdfPageCount;
            int index = slides ? documentSlideIndex : pdfPageIndex;
            previousButton.Enabled = count > 1 && index > 0;
            nextButton.Enabled = count > 1 && index + 1 < count;
            pageLabel.Text = count > 0 ? (index + 1) + " / " + (slides ? Math.Max(documentSlideTotal, count) : count) : "";
            playbackButton.Enabled = slides || animatedGif != null || media != null;
            playbackButton.Text = playbackPaused ? "▶" : "Ⅱ";
            playbackButton.AccessibleName = L10n.Text(language, playbackPaused ? "play" : "pause");
            zoomOutButton.Enabled = zoomInButton.Enabled = fitButton.Enabled = preview != null;
            zoomLabel.Text = (int)Math.Round(zoom * 100) + "%";
            fitButton.Text = L10n.Text(language, "fit");
            toolbar.Invalidate();
        }

        private void Navigate(int delta)
        {
            if (documentSlides != null && documentSlides.Length > 0)
            {
                int target = Math.Max(0, Math.Min(documentSlides.Length - 1, documentSlideIndex + delta));
                if (target == documentSlideIndex) return;
                playbackPaused = true; slideshowTimer.Stop();
                ShowDocumentSlide(target); UpdateToolbar();
            }
            else if (pdfPageCount > 1)
            {
                int target = Math.Max(0, Math.Min(pdfPageCount - 1, pdfPageIndex + delta));
                if (target != pdfPageIndex) LoadPdfPage(target);
            }
        }

        private void LoadPdfPage(int index)
        {
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
                return new DocumentPreviewResult(image, image == null ? "document_pdf_unavailable" : "document_pdf") { PageCount = count };
            }, System.Threading.CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
            LayoutDocument(); entranceMotion.ShowAt(Location); UpdateToolbar();
            mediaTimer.Interval = 40; mediaTimer.Start(); Invalidate();
        }

        private void TogglePlayback()
        {
            playbackPaused = !playbackPaused;
            if (media != null && media.IsOpen)
            {
                MediaPlayback session = media;
                if (playbackPaused) session.Pause();
                else session.Play();
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
                    if (animatedGif.CanAdvance) animationTimer.Start();
                }
            }
            UpdateToolbar(); Invalidate();
        }

        private void ChangeZoom(float factor)
        {
            if (preview == null) return;
            zoom = Math.Max(1, Math.Min(4, zoom * factor));
            ClampPan(); UpdateToolbar(); Invalidate(imageArea);
        }
        private Rectangle ZoomedImageArea()
        {
            if (!interactive || zoom <= 1) return imageArea;
            int width = (int)Math.Round(imageArea.Width * zoom), height = (int)Math.Round(imageArea.Height * zoom);
            return new Rectangle(imageArea.X + (imageArea.Width - width) / 2 + pan.X,
                imageArea.Y + (imageArea.Height - height) / 2 + pan.Y, width, height);
        }
        private void ClampPan()
        {
            int x = (int)(imageArea.Width * (zoom - 1) / 2), y = (int)(imageArea.Height * (zoom - 1) / 2);
            pan = new Point(Math.Max(-x, Math.Min(x, pan.X)), Math.Max(-y, Math.Min(y, pan.Y)));
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (interactive)
            {
                switch (keyData)
                {
                    case Keys.Escape: Close(); return true;
                    case Keys.Space: TogglePlayback(); return true;
                    case Keys.Left: case Keys.PageUp: Navigate(-1); return true;
                    case Keys.Right: case Keys.PageDown: Navigate(1); return true;
                    case Keys.Add: case Keys.Oemplus: ChangeZoom(1.25f); return true;
                    case Keys.Subtract: case Keys.OemMinus: ChangeZoom(1 / 1.25f); return true;
                    case Keys.D0: zoom = 1; pan = Point.Empty; UpdateToolbar(); Invalidate(); return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (!interactive) return;
            if (mediaKind == MediaKind.Text && textPreview != null)
            {
                textOffset = Math.Max(0, Math.Min(Math.Max(0, textPreview.Lines.Length - textVisibleLines), textOffset + (e.Delta < 0 ? 3 : -3)));
                Invalidate(); return;
            }
            if ((ModifierKeys & Keys.Control) == 0 && (pdfPageCount > 1 || documentSlides != null)) Navigate(e.Delta < 0 ? 1 : -1);
            else ChangeZoom(e.Delta > 0 ? 1.25f : 1 / 1.25f);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (interactive && e.Button == MouseButtons.Left && zoom > 1 && imageArea.Contains(e.Location))
            { panning = true; mouseStart = e.Location; panStart = pan; Capture = true; }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!panning) return;
            pan = new Point(panStart.X + e.X - mouseStart.X, panStart.Y + e.Y - mouseStart.Y);
            ClampPan(); Invalidate(imageArea);
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); panning = false; Capture = false; }
    }
}
