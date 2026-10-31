using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;

namespace DesktopHover
{
    /// <summary>A passive image, media, text or document preview. Use on the UI thread.</summary>
    public sealed partial class PreviewWindow : Form
    {
        private const long MaximumFileBytes = 100L * 1024L * 1024L;
        private const long MaximumPixels = 100000000L;
        private const int WsExTransparent = 0x20;
        private const int WsExToolWindow = 0x80;
        private const int WsExNoActivate = 0x08000000;
        private const int WsClipChildren = 0x02000000;
        private const int SwpNoActivate = 0x0010;
        private const int SwpShowWindow = 0x0040;

        private Bitmap preview;
        private AnimatedGif animatedGif;
        private readonly Timer animationTimer;
        private readonly Timer slideshowTimer;
        private readonly WindowMotion entranceMotion;
        private string fileName;
        private string description;
        private Rectangle imageArea;
        private Rectangle titleArea;
        private Rectangle descriptionArea;
        private Font titleFont;
        private Font descriptionFont;
        private Font codeFont;
        private TextPreviewContent textPreview;
        private int textLineHeight, textVisibleLines, textGutterWidth;
        private float scale = 1F;
        private int sourceWidth;
        private int sourceHeight;
        private long sourceBytes;
        private int maximumPreviewWidth=720;
        private int maximumPreviewHeight=540;
        private int volumePercent=100;
        private bool upscale;
        private string language="en";
        private MediaPlayback media;
        private MediaKind mediaKind;
        private string mediaState;
        private string documentMediaNoteKey;
        private DrawingPoint mediaCursor;
        private Rectangle mediaItemBounds;
        private DateTime mediaDeadline;
        private readonly Timer mediaTimer;
        private Task<ImagePreviewResult> imageTask;
        private System.Threading.CancellationTokenSource imageCancellation;
        private Task<DocumentPreviewResult> documentTask;
        private System.Threading.CancellationTokenSource documentCancellation;
        private byte[][] documentSlides;
        private int documentSlideIndex;
        private int slideIntervalSeconds=3;
        private AppSettings previewSettings = new AppSettings();
        private DocumentProgress documentProgress;
        private bool documentLoading;
        private int documentSlideTotal;
        private string documentDetail;

        public string CurrentPath { get; private set; }
        internal DrawingSize DisplayedImageSize { get { return imageArea.Size; } }
        internal bool IsMediaPreview { get { return mediaKind != MediaKind.None || animatedGif != null; } }
        internal string MediaState { get { return mediaState; } }
        internal bool IsImageLoading { get { return imageTask != null; } }
        internal bool HasImage { get { return preview != null; } }
        internal TimeSpan PlaybackPosition { get { return media == null ? TimeSpan.Zero : media.Position; } }
        internal void Configure(AppSettings settings)
        {
            int targetWidth = interactive ? Math.Max(640, settings.MaxWidth) : settings.MaxWidth;
            int targetHeight = interactive ? Math.Max(480, settings.MaxHeight) : settings.MaxHeight;
            bool resize = maximumPreviewWidth != targetWidth || maximumPreviewHeight != targetHeight || upscale != settings.Upscale;
            bool resumeLoop = settings.LoopVideo && !previewSettings.LoopVideo && mediaState == "media_ended";
            string reopen = interactive && resize ? CurrentPath : null;
            previewSettings = settings.Clone();
            autoPlayMedia = settings.AutoPlayMedia;
            UiMotion.Enabled = settings.AnimationsEnabled;
            Brand.SetTheme(settings.Theme);
            BackColor=Brand.Background;
            ForeColor=Brand.Text;
            maximumPreviewWidth=interactive ? Math.Max(640, settings.MaxWidth) : settings.MaxWidth;
            maximumPreviewHeight=interactive ? Math.Max(480, settings.MaxHeight) : settings.MaxHeight;
            volumePercent=Math.Max(0,Math.Min(100,settings.VolumePercent));
            slideIntervalSeconds=Math.Max(1,Math.Min(10,settings.SlideIntervalSeconds));
            upscale=settings.Upscale;
            language=L10n.Normalize(settings.Language);
            UpdateLanguage();
            if (media != null) media.SetVolume(volumePercent);
            if (slideshowTimer != null) slideshowTimer.Interval = slideIntervalSeconds * 1000;
            if (resize || !settings.Allows(mediaKind)) ClearPreview();
            else Invalidate();
            if (reopen != null) ShowFile(reopen, mediaCursor, mediaItemBounds);
            if (resumeLoop && media != null && media.IsOpen && media.HasVideo) TogglePlayback();
            UpdateToolbar();
        }

        public PreviewWindow() : this(false) { }

        internal PreviewWindow(bool interactive)
        {
            this.interactive = interactive;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            ShowInTaskbar = false;
            TopMost = true;
            BackColor = Brand.Background;
            ForeColor = Brand.Text;
            Icon = Brand.CreateIcon();
            UpdateLanguage();
            AccessibleRole = AccessibleRole.ToolTip;
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer, true);
            SetFonts();
            entranceMotion = new WindowMotion(this, false, false);
            animationTimer = new Timer();
            animationTimer.Tick += AdvanceAnimation;
            slideshowTimer = new Timer();
            slideshowTimer.Tick += AdvanceDocumentSlide;
            mediaTimer = new Timer { Interval = 200 };
            mediaTimer.Tick += delegate
            {
                if (imageTask != null) CompleteImageLoad();
                if (documentTask != null) CompleteDocumentLoad();
                if (media == null) return;
                if (mediaState == "media_loading" && DateTime.UtcNow >= mediaDeadline)
                {
                    MediaFailed(media);
                    return;
                }
                string previousDescription = description;
                UpdateMediaDescription();
                if (description != previousDescription) Invalidate(descriptionArea);
            };
            InitializeInteractive();
        }

        private void UpdateLanguage()
        {
            AccessibleName=L10n.Text(language,"preview_title");
            Text=AccessibleName+" — Desktop Hover";
        }

        protected override bool ShowWithoutActivation
        {
            get { return !interactive; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                // The WPF video surface owns its pixels; parent paints must not cover it.
                parameters.Style |= WsClipChildren;
                if (!interactive) parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
                parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return parameters;
            }
        }

        public bool ShowImage(string path, DrawingPoint cursor, Rectangle itemBounds)
        {
            if (IsDisposed || String.IsNullOrEmpty(path)) return false;
            if (String.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase) &&
                mediaKind == MediaKind.Image && (imageTask != null || preview != null)) return true;
            ClearPreview();
            try
            {
                CurrentPath = Path.GetFullPath(path);
                fileName = Path.GetFileName(path);
                mediaKind = MediaKind.Image;
                mediaState = "preview_loading";
                mediaCursor = cursor; mediaItemBounds = itemBounds;
                scale = GetScale(cursor); SetFonts(); UpdateLanguage();
                LayoutImage();
                entranceMotion.ShowAt(Location);
                imageCancellation = new System.Threading.CancellationTokenSource();
                var token = imageCancellation.Token;
                var size = interactive ? new DrawingSize(1600, 1200) : new DrawingSize(maximumPreviewWidth, maximumPreviewHeight);
                string source = CurrentPath;
                imageTask = Task.Factory.StartNew(() => ImagePreviewLoader.Load(source, size, token),
                    System.Threading.CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                mediaTimer.Interval = 40; mediaTimer.Start();
                Invalidate();
                return true;
            }
            catch { ClearPreview(); return false; }
        }

        private void CompleteImageLoad()
        {
            if (imageTask == null || !imageTask.IsCompleted) return;
            Task<ImagePreviewResult> task = imageTask; imageTask = null;
            imageCancellation.Dispose(); imageCancellation = null;
            mediaTimer.Stop(); mediaTimer.Interval = 200;
            ImagePreviewResult result = null;
            try { result = task.GetAwaiter().GetResult(); } catch { }
            using (result)
            {
                mediaState = result == null ? "image_unavailable" : result.ErrorKey;
                if (result != null && result.Image != null)
                {
                    preview = result.Image; animatedGif = result.Animation;
                    result.Image = null; result.Animation = null;
                    sourceWidth = result.Width; sourceHeight = result.Height; sourceBytes = result.Bytes;
                }
            }
            LayoutImage(); entranceMotion.ShowAt(Location); Invalidate();
            if (animatedGif != null && animatedGif.CanAdvance && !playbackPaused)
            {
                animationTimer.Interval = animatedGif.FrameDelay; animationTimer.Start();
            }
        }

        private void CancelImageLoad()
        {
            var task = imageTask; var cancellation = imageCancellation;
            imageTask = null; imageCancellation = null;
            if (cancellation == null) return;
            cancellation.Cancel();
            if (task == null) { cancellation.Dispose(); return; }
            task.ContinueWith(completed =>
            {
                try { using (var result = completed.GetAwaiter().GetResult()) { } }
                catch { }
                finally { cancellation.Dispose(); }
            }, TaskScheduler.Default);
        }

        private void LayoutImage()
        {
            Rectangle area = Screen.FromPoint(mediaCursor).WorkingArea;
            int padding = Px(3), footer = Px(12), footerHeight = Px(48);
            int maxWidth = Math.Max(1, Math.Min(maximumPreviewWidth, area.Width - padding * 2 - Px(32)));
            int maxHeight = Math.Max(1, Math.Min(maximumPreviewHeight, area.Height - padding - footer - footerHeight - Px(interactive ? 90 : 32)));
            int width = Math.Min(maxWidth, Px(360)), height = Math.Min(maxHeight, Px(160));
            if (preview != null)
            {
                double fit = Math.Min((double)maxWidth / preview.Width, (double)maxHeight / preview.Height);
                if (!upscale) fit = Math.Min(1, fit);
                width = Math.Max(1, (int)Math.Round(preview.Width * fit));
                height = Math.Max(1, (int)Math.Round(preview.Height * fit));
            }
            int contentWidth = Math.Min(maxWidth, Math.Max(Px(interactive ? 520 : 220), width));
            ClientSize = new DrawingSize(contentWidth + padding * 2, height + padding + footer + footerHeight);
            imageArea = new Rectangle(padding + (contentWidth - width) / 2, padding, width, height);
            titleArea = new Rectangle(footer + Px(22), imageArea.Bottom + Px(11), Math.Max(1, ClientSize.Width - footer * 2 - Px(22)), Px(19));
            descriptionArea = new Rectangle(footer, titleArea.Bottom + Px(1), Math.Max(1, ClientSize.Width - footer * 2), Px(17));
            description = preview == null ? L10n.Text(language, mediaState ?? "image_unavailable") :
                String.Format(L10n.Culture(language), "{0} × {1} px  ·  {2}", sourceWidth, sourceHeight, FormatBytes(sourceBytes));
            PositionContent(mediaCursor, mediaItemBounds, area);
        }

        internal bool ShowFile(string path, DrawingPoint cursor, Rectangle itemBounds)
        {
            MediaKind kind = MediaFiles.Kind(path);
            if (!previewSettings.Allows(kind)) { ClearPreview(); return false; }
            if (kind == MediaKind.Image) return ShowImage(path, cursor, itemBounds);
            if (kind == MediaKind.Text) return ShowText(path, cursor, itemBounds);
            if (kind == MediaKind.Document) return ShowDocument(path, cursor, itemBounds);
            if (kind == MediaKind.Audio || kind == MediaKind.Video) return ShowMedia(path, kind, cursor, itemBounds);
            ClearPreview();
            return false;
        }

        private bool ShowDocument(string path, DrawingPoint cursor, Rectangle itemBounds)
        {
            return LoadDocument(path, cursor, itemBounds, true);
        }

        private bool LoadDocument(string path, DrawingPoint cursor, Rectangle itemBounds, bool allowVideo)
        {
            if (IsDisposed) return false;
            ClearPreview();
            try
            {
                path = Path.GetFullPath(path);
                CurrentPath = path;
                fileName = Path.GetFileName(path);
                sourceBytes = new FileInfo(path).Length;
                mediaKind = MediaKind.Document;
                mediaState = "document_loading";
                mediaCursor = cursor;
                mediaItemBounds = itemBounds;
                scale = GetScale(cursor);
                SetFonts();
                AccessibleName = L10n.Text(language, "document_preview");
                Text = AccessibleName + " — Desktop Hover";
                LayoutDocument();
                entranceMotion.ShowAt(Location);
                SetWindowPos(Handle, interactive ? IntPtr.Zero : new IntPtr(-1), Left, Top, Width, Height, SwpNoActivate | SwpShowWindow | (interactive ? 4U : 0U));
                documentCancellation = new System.Threading.CancellationTokenSource();
                var token = documentCancellation.Token;
                var bounds = interactive && String.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase)
                    ? new DrawingSize(1600, 1200) : new DrawingSize(maximumPreviewWidth, maximumPreviewHeight);
                documentProgress = new DocumentProgress();
                DocumentProgress progress = documentProgress;
                documentLoading = true;
                documentTask = Task.Factory.StartNew(() => allowVideo ? DocumentPreviewContent.Load(path, bounds, token, progress.Publish)
                    : DocumentPreviewContent.LoadStill(path, bounds, token),
                    System.Threading.CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default);
                mediaTimer.Interval = 40; mediaTimer.Start();
                Invalidate();
                return true;
            }
            catch { ClearPreview(); return false; }
        }

        private void CompleteDocumentLoad()
        {
            if (documentProgress != null)
                using (DocumentPreviewResult available = documentProgress.Take())
                    if (available != null) ApplyDocumentResult(available, false);
            if (documentTask == null || !documentTask.IsCompleted) return;
            documentLoading = false;
            if (documentProgress != null) documentProgress.Dispose();
            documentProgress = null;
            Task<DocumentPreviewResult> completed = documentTask;
            documentTask = null;
            documentCancellation.Dispose();
            documentCancellation = null;
            mediaTimer.Stop(); mediaTimer.Interval = 200;
            DocumentPreviewResult result = null;
            try { result = completed.GetAwaiter().GetResult(); }
            catch { }
            using (result)
            {
                mediaState = result == null ? "document_unavailable" : result.NoteKey;
                if (result != null && !String.IsNullOrEmpty(result.MediaPath))
                {
                    if (StartDocumentMedia(result.MediaPath, result.NoteKey)) return;
                    LoadDocument(CurrentPath, mediaCursor, mediaItemBounds, false);
                    return;
                }
                else if (result != null) ApplyDocumentResult(result, true);
            }
            LayoutDocument(); entranceMotion.ShowAt(Location); Invalidate();
        }

        private void ApplyDocumentResult(DocumentPreviewResult result, bool complete)
        {
            mediaState = result.NoteKey;
            if (result.PageCount > 0) pdfPageCount = result.PageCount;
            documentDetail = result.DetailKey;
            bool hadSlides = documentSlides != null && documentSlides.Length > 0;
            if (!hadSlides)
            {
                if (preview != null) preview.Dispose();
                preview = result.Image; result.Image = null;
                documentSlideIndex = 0;
            }
            if (result.Slides != null)
            {
                documentSlides = result.Slides; result.Slides = null;
                documentSlideTotal = complete ? documentSlides.Length : Math.Max(result.SlideCount, documentSlides.Length);
            }
            if (complete && hadSlides && result.Slides == null) documentSlideTotal = documentSlides.Length;
            LayoutDocument(); entranceMotion.ShowAt(Location); Invalidate();
            if (documentSlides != null && documentSlides.Length > 1 && !slideshowTimer.Enabled && !playbackPaused)
            {
                slideshowTimer.Interval = slideIntervalSeconds * 1000;
                slideshowTimer.Start();
            }
        }

        private void AdvanceDocumentSlide(object sender, EventArgs e)
        {
            if (playbackPaused || documentSlides == null || documentSlides.Length < 2 || !Visible || mediaKind != MediaKind.Document)
            {
                slideshowTimer.Stop();
                return;
            }
            if (documentLoading && documentSlideIndex + 1 >= documentSlides.Length) return;
            int next = (documentSlideIndex + 1) % documentSlides.Length;
            ShowDocumentSlide(next);
        }

        private void ShowDocumentSlide(int next)
        {
            try
            {
                Bitmap replacement;
                using (var stream = new MemoryStream(documentSlides[next], false))
                using (Image image = Image.FromStream(stream, false, true))
                    replacement = DocumentPreviewWorker.CopyImage(image, new DrawingSize(maximumPreviewWidth, maximumPreviewHeight));
                Bitmap previous = preview;
                preview = replacement;
                if (previous != null) previous.Dispose();
                documentSlideIndex = next;
                UpdateToolbar();
                LayoutDocument();
                SetWindowPos(Handle, interactive ? IntPtr.Zero : new IntPtr(-1), Left, Top, Width, Height, SwpNoActivate | SwpShowWindow | (interactive ? 4U : 0U));
                Invalidate();
            }
            catch { slideshowTimer.Stop(); }
        }

        private void CancelDocumentLoad()
        {
            if (documentProgress != null) { documentProgress.Dispose(); documentProgress = null; }
            documentLoading = false; documentSlideTotal = 0; documentDetail = null;
            Task<DocumentPreviewResult> pending = documentTask;
            var cancellation = documentCancellation;
            documentTask = null;
            documentCancellation = null;
            if (cancellation == null) return;
            cancellation.Cancel();
            if (pending == null) { cancellation.Dispose(); return; }
            // No UI callbacks are queued: a stale result is disposed even after the window closes.
            pending.ContinueWith(task =>
            {
                try
                {
                    using (DocumentPreviewResult result = task.GetAwaiter().GetResult()) { }
                }
                catch { }
                finally { cancellation.Dispose(); }
            }, TaskScheduler.Default);
        }

        private void LayoutDocument()
        {
            Rectangle workingArea = Screen.FromPoint(mediaCursor).WorkingArea;
            int padding = Px(3), footerPadding = Px(12), footerHeight = Px(48);
            int maxWidth = Math.Max(1, Math.Min(maximumPreviewWidth, workingArea.Width - padding * 2 - Px(32)));
            int maxHeight = Math.Max(1, Math.Min(maximumPreviewHeight,
                workingArea.Height - padding - footerPadding - footerHeight - Px(interactive ? 90 : 32)));
            int width = Math.Min(maxWidth, Px(460)), height = Math.Min(maxHeight, Px(220));
            if (preview != null)
            {
                double fit = Math.Min((double)maxWidth / preview.Width, (double)maxHeight / preview.Height);
                // Presentation size follows the configured bounds even when only a
                // saved thumbnail is available; Upscale controls ordinary images.
                if (!upscale && !PowerPointPreviewReader.IsSupported(CurrentPath)) fit = Math.Min(1.0, fit);
                width = Math.Max(1, (int)Math.Round(preview.Width * fit));
                height = Math.Max(1, (int)Math.Round(preview.Height * fit));
            }
            int contentWidth = Math.Min(maxWidth, Math.Max(Px(interactive ? 520 : 220), width));
            ClientSize = new DrawingSize(contentWidth + padding * 2, height + padding + footerPadding + footerHeight);
            imageArea = new Rectangle(padding + (contentWidth - width) / 2, padding, width, height);
            int footerWidth = Math.Max(1, ClientSize.Width - footerPadding * 2);
            titleArea = new Rectangle(footerPadding + Px(22), imageArea.Bottom + Px(11),
                Math.Max(1, footerWidth - Px(22)), Px(19));
            descriptionArea = new Rectangle(footerPadding, titleArea.Bottom + Px(1), footerWidth, Px(17));
            string type = Path.GetExtension(CurrentPath).TrimStart('.').ToUpperInvariant();
            string note = L10n.Text(language, mediaState == "document_unavailable" || mediaState == "document_adobe_unavailable" ||
                mediaState == "document_limit" || mediaState == "document_pdf_unavailable"
                ? "document_preview" : mediaState);
            string slidePosition = documentSlides != null && documentSlides.Length > 0
                ? " · " + (documentSlideIndex + 1) + "/" + Math.Max(documentSlideTotal, documentSlides.Length) : "";
            if (pdfPageCount > 0) slidePosition = " · " + (pdfPageIndex + 1) + "/" + pdfPageCount;
            description = type + " · " + note + slidePosition + " · " + FormatBytes(sourceBytes);
            if (documentLoading) description += " · " + L10n.Text(language, "preview_loading");
            if (!String.IsNullOrEmpty(documentDetail)) description = L10n.Text(language, documentDetail);
            PositionContent(mediaCursor, mediaItemBounds, workingArea);
        }

        private void PaintDocumentCard(Graphics graphics)
        {
            using (var background = new SolidBrush(Brand.Surface)) graphics.FillRectangle(background, imageArea);
            var flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix;
            int centerX = imageArea.Left + imageArea.Width / 2;
            int top = imageArea.Top + Px(24);
            using (var pen = new System.Drawing.Pen(Brand.Accent, Px(2)))
            {
                graphics.DrawRectangle(pen, centerX - Px(15), top, Px(30), Px(38));
                for (int line = 0; line < 3; line++)
                    graphics.DrawLine(pen, centerX - Px(8), top + Px(12 + line * 7), centerX + Px(8), top + Px(12 + line * 7));
            }
            TextRenderer.DrawText(graphics, L10n.Text(language, mediaKind == MediaKind.Image ? "preview_title" : "document_preview"), titleFont,
                new Rectangle(imageArea.X + Px(8), top + Px(48), Math.Max(1, imageArea.Width - Px(16)), Px(24)), Brand.Text, flags);
            TextRenderer.DrawText(graphics, L10n.Text(language, mediaState), descriptionFont,
                new Rectangle(imageArea.X + Px(18), top + Px(80), Math.Max(1, imageArea.Width - Px(36)),
                    Math.Max(1, imageArea.Height - Px(112))), Brand.Muted, flags | TextFormatFlags.WordBreak);
        }

        private bool ShowText(string path, DrawingPoint cursor, Rectangle itemBounds)
        {
            if (IsDisposed) return false;
            ClearPreview();
            try
            {
                path = Path.GetFullPath(path);
                textPreview = TextPreviewContent.Load(path);
                mediaKind = MediaKind.Text;
                CurrentPath = path;
                fileName = Path.GetFileName(path);
                sourceBytes = textPreview.FileBytes;
                scale = GetScale(cursor);
                SetFonts();
                UpdateLanguage();
                AccessibleName = L10n.Text(language, textPreview.IsCode ? "code_preview" : "text_preview");
                Text = AccessibleName + " — Desktop Hover";

                Rectangle workingArea = Screen.FromPoint(cursor).WorkingArea;
                int padding = Px(3), footerPadding = Px(12), footerHeight = Px(48);
                int width = Math.Max(1, Math.Min(maximumPreviewWidth, workingArea.Width - padding * 2 - Px(32)));
                int maximumHeight = Math.Max(1, Math.Min(maximumPreviewHeight,
                    workingArea.Height - padding - footerPadding - footerHeight - Px(interactive ? 90 : 32)));
                textLineHeight = Px(21);
                int height = Math.Min(maximumHeight, Math.Max(Px(120),
                    textPreview.Lines.Length * textLineHeight + Px(16)));
                ClientSize = new DrawingSize(width + padding * 2, height + padding + footerPadding + footerHeight);
                imageArea = new Rectangle(padding, padding, width, height);
                int footerWidth = Math.Max(1, ClientSize.Width - footerPadding * 2);
                titleArea = new Rectangle(footerPadding + Px(22), imageArea.Bottom + Px(11),
                    Math.Max(1, footerWidth - Px(22)), Px(19));
                descriptionArea = new Rectangle(footerPadding, titleArea.Bottom + Px(1), footerWidth, Px(17));
                textVisibleLines = Math.Max(0, (imageArea.Height - Px(16)) / textLineHeight);
                bool truncated = textPreview.Truncated || textPreview.Lines.Length > textVisibleLines;
                using (Graphics graphics = CreateGraphics())
                using (StringFormat format = CodeFormat())
                {
                    string lastNumber = Math.Max(1, Math.Min(textVisibleLines, textPreview.Lines.Length)).ToString(CultureInfo.InvariantCulture);
                    textGutterWidth = Math.Max(Px(38), (int)Math.Ceiling(graphics.MeasureString(lastNumber, codeFont, DrawingPoint.Empty, format).Width) + Px(20));
                    int textWidth = Math.Max(0, width - textGutterWidth - Px(20));
                    for (int i = 0; i < Math.Min(textVisibleLines, textPreview.Lines.Length); i++)
                        if (graphics.MeasureString(textPreview.Lines[i].Text, codeFont, DrawingPoint.Empty, format).Width > textWidth)
                            truncated = true;
                }
                string type = textPreview.IsCode ? textPreview.LanguageName : L10n.Text(language, "text_preview");
                description = type + " · " + textPreview.EncodingName + " · " + FormatBytes(sourceBytes);
                if (truncated) description = L10n.Text(language, "text_truncated") + " · " + description;
                PositionContent(cursor, itemBounds, workingArea);
                entranceMotion.ShowAt(Location);
                SetWindowPos(Handle, interactive ? IntPtr.Zero : new IntPtr(-1), Left, Top, Width, Height, SwpNoActivate | SwpShowWindow | (interactive ? 4U : 0U));
                Invalidate();
                return true;
            }
            catch
            {
                ClearPreview();
                return false;
            }
        }

        private static StringFormat CodeFormat()
        {
            var format = (StringFormat)StringFormat.GenericTypographic.Clone();
            format.FormatFlags |= StringFormatFlags.MeasureTrailingSpaces | StringFormatFlags.NoWrap;
            return format;
        }

        private void PaintText(Graphics graphics)
        {
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SetClip(imageArea, CombineMode.Intersect);
                graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                using (var background = new SolidBrush(Brand.EditorBackground)) graphics.FillRectangle(background, imageArea);
                using (var gutter = new SolidBrush(Brand.EditorGutter))
                    graphics.FillRectangle(gutter, imageArea.Left, imageArea.Top, textGutterWidth, imageArea.Height);
                if (textPreview.Lines.Length == 0 ||
                    (textPreview.Lines.Length == 1 && textPreview.Lines[0].Text.Length == 0))
                {
                    TextRenderer.DrawText(graphics, L10n.Text(language, "text_empty"), codeFont, imageArea,
                        Brand.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    return;
                }
                using (StringFormat format = CodeFormat())
                using (var numbers = new SolidBrush(Brand.EditorLineNumber))
                {
                    int count = Math.Min(textVisibleLines, textPreview.Lines.Length - textOffset);
                    for (int i = 0; i < count; i++)
                    {
                        float y = imageArea.Top + Px(8) + i * textLineHeight;
                        string number = (i + textOffset + 1).ToString(CultureInfo.InvariantCulture);
                        float numberWidth = graphics.MeasureString(number, codeFont, DrawingPoint.Empty, format).Width;
                        graphics.DrawString(number, codeFont, numbers, imageArea.Left + textGutterWidth - Px(9) - numberWidth, y, format);
                        TextPreviewLine line = textPreview.Lines[i + textOffset];
                        float x = imageArea.Left + textGutterWidth + Px(10);
                        int end = 0;
                        foreach (TextSpan span in line.Spans)
                        {
                            if (span.Start > end) x = DrawCodeRun(graphics, line.Text.Substring(end, span.Start - end), TextTokenKind.Plain, x, y, format);
                            x = DrawCodeRun(graphics, line.Text.Substring(span.Start, span.Length), span.Kind, x, y, format);
                            end = span.Start + span.Length;
                            if (x >= imageArea.Right) break;
                        }
                        if (end < line.Text.Length && x < imageArea.Right)
                            DrawCodeRun(graphics, line.Text.Substring(end), TextTokenKind.Plain, x, y, format);
                    }
                }
            }
            finally { graphics.Restore(state); }
        }

        private float DrawCodeRun(Graphics graphics, string text, TextTokenKind kind, float x, float y, StringFormat format)
        {
            DrawingColor color = Brand.SyntaxColor(kind);
            using (var brush = new SolidBrush(color)) graphics.DrawString(text, codeFont, brush, x, y, format);
            return x + graphics.MeasureString(text, codeFont, DrawingPoint.Empty, format).Width;
        }

        private bool ShowMedia(string path, MediaKind kind, DrawingPoint cursor, Rectangle itemBounds)
        {
            if (IsDisposed) return false;
            ClearPreview();
            try
            {
                // Playback is local only, including when called independently of the shell resolver.
                path = Path.GetFullPath(path);
                if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
                    new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network) return false;
                var file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || ((int)file.Attributes & (0x1000 | 0x40000 | 0x400000)) != 0) return false;
                CurrentPath = path;
                fileName = file.Name;
                sourceBytes = file.Length;
                mediaKind = kind;
                mediaState = "media_loading";
                mediaCursor = cursor;
                mediaItemBounds = itemBounds;
                scale = GetScale(cursor);
                SetFonts();
                UpdateLanguage();
                AccessibleName = L10n.Text(language, kind == MediaKind.Video ? "video_preview" : "audio_preview");
                Text = AccessibleName + " — Desktop Hover";
                StartMediaSession(path, false);
                return true;
            }
            catch
            {
                ClearPreview();
                return false;
            }
        }

        private bool StartDocumentMedia(string path, string noteKey)
        {
            try
            {
                // Recheck the sidecar immediately before opening it. Its filename never
                // replaces the hovered project's identity or its footer metadata.
                path = Path.GetFullPath(path);
                if (path.StartsWith(@"\\", StringComparison.Ordinal) ||
                    new DriveInfo(Path.GetPathRoot(path)).DriveType == DriveType.Network) return false;
                var file = new FileInfo(path);
                if (!file.Exists || file.Length == 0 || file.Length > DocumentPreviewContent.MaximumFileBytes ||
                    (file.Attributes & (FileAttributes)(0x1000 | 0x40000 | 0x400000 | 0x400)) != 0) return false;
                documentMediaNoteKey = String.IsNullOrEmpty(noteKey) ? "document_sidecar" : noteKey;
                mediaKind = MediaKind.Video;
                mediaState = "media_loading";
                AccessibleName = L10n.Text(language, "video_preview");
                Text = AccessibleName + " — Desktop Hover";
                StartMediaSession(path, true);
                return true;
            }
            catch
            {
                MediaPlayback failed = media;
                media = null;
                if (failed != null) failed.Dispose();
                mediaTimer.Stop();
                documentMediaNoteKey = null;
                mediaKind = MediaKind.Document;
                return false;
            }
        }

        private void StartMediaSession(string path, bool requireVideo)
        {
            var session = new MediaPlayback(Handle, volumePercent);
            media = session;
            session.Opened += delegate
            {
                if (media != session || IsDisposed) return;
                // A project video preview must contain frames, even if a renamed
                // audio file happens to be accepted by the installed decoder.
                if (requireVideo && !session.HasVideo) { MediaFailed(session); return; }
                try
                {
                    playbackPaused = !autoPlayMedia;
                    mediaState = playbackPaused ? "media_paused" : "media_playing";
                    LayoutMedia();
                    if (!playbackPaused) session.Play();
                    UpdateMediaDescription();
                    Invalidate();
                }
                catch { MediaFailed(session); }
            };
            session.Ended += delegate
            {
                if (media != session || IsDisposed) return;
                if (previewSettings.LoopVideo && session.HasVideo && !playbackPaused && session.Duration > TimeSpan.Zero)
                {
                    // Leave the decoder's end notification before seeking. Recheck the
                    // session and pause state so closing/replacing the preview cannot restart it.
                    BeginInvoke((MethodInvoker)delegate
                    {
                        if (IsDisposed || media != session || playbackPaused) return;
                        if (!previewSettings.LoopVideo) { FinishMediaPlayback(); return; }
                        session.Play();
                        if (media != session) return;
                        mediaState = "media_playing";
                        mediaTimer.Start(); UpdateMediaDescription(); Invalidate();
                    });
                }
                else FinishMediaPlayback();
            };
            session.Failed += delegate { MediaFailed(session); };
            mediaDeadline = DateTime.UtcNow.AddSeconds(15);
            LayoutMedia();
            UpdateMediaDescription();
            mediaTimer.Start();
            session.Open(path);
        }

        private void FinishMediaPlayback()
        {
            mediaState = "media_ended";
            playbackPaused = true; UpdateToolbar();
            mediaTimer.Stop(); UpdateMediaDescription(); Invalidate();
        }

        private void MediaFailed(MediaPlayback session)
        {
            if (media != session || IsDisposed) return;
            media = null;
            session.Dispose();
            mediaTimer.Stop();
            if (documentMediaNoteKey != null)
            {
                // Unsupported codecs or damaged exports must not hide a usable image.
                // This retry excludes videos, so it cannot enter a playback retry loop.
                LoadDocument(CurrentPath, mediaCursor, mediaItemBounds, false);
                return;
            }
            mediaState = "media_unavailable";
            UpdateMediaDescription();
            Invalidate();
        }

        private void LayoutMedia()
        {
            Rectangle workingArea = Screen.FromPoint(mediaCursor).WorkingArea;
            int imagePadding = Px(3), footerPadding = Px(12), footerHeight = Px(48);
            int maxWidth = Math.Max(1, Math.Min(maximumPreviewWidth, workingArea.Width - imagePadding * 2 - Px(32)));
            int maxHeight = Math.Max(1, Math.Min(maximumPreviewHeight,
                workingArea.Height - imagePadding - footerPadding - footerHeight - Px(interactive ? 90 : 32)));
            int width = Math.Min(maxWidth, Px(360)), height = Math.Min(maxHeight, Px(120));
            if (media != null && media.IsOpen && media.HasVideo)
            {
                double fit = Math.Min(1.0, Math.Min((double)maxWidth / Math.Max(1, media.NaturalWidth),
                    (double)maxHeight / Math.Max(1, media.NaturalHeight)));
                width = Math.Max(1, (int)Math.Round(media.NaturalWidth * fit));
                height = Math.Max(1, (int)Math.Round(media.NaturalHeight * fit));
            }
            else if (mediaKind == MediaKind.Video && mediaState == "media_loading")
            {
                width = maxWidth;
                height = Math.Min(maxHeight, Math.Max(1, width * 9 / 16));
            }
            int contentWidth = Math.Min(maxWidth, Math.Max(Px(interactive ? 520 : 220), width));
            ClientSize = new DrawingSize(contentWidth + imagePadding * 2,
                height + imagePadding + footerPadding + footerHeight);
            imageArea = new Rectangle(imagePadding + (contentWidth - width) / 2, imagePadding, width, height);
            int footerWidth = Math.Max(1, ClientSize.Width - footerPadding * 2);
            titleArea = new Rectangle(footerPadding + Px(22), imageArea.Bottom + Px(11),
                Math.Max(1, footerWidth - Px(22)), Px(19));
            descriptionArea = new Rectangle(footerPadding, titleArea.Bottom + Px(1), footerWidth, Px(17));
            PositionContent(mediaCursor, mediaItemBounds, workingArea);
            entranceMotion.ShowAt(Location);
            if (media != null && media.IsOpen && media.HasVideo) media.SetVideoBounds(imageArea);
            SetWindowPos(Handle, interactive ? IntPtr.Zero : new IntPtr(-1), Left, Top, Width, Height, SwpNoActivate | SwpShowWindow | (interactive ? 4U : 0U));
            Invalidate();
        }

        private void UpdateMediaDescription()
        {
            string prefix = documentMediaNoteKey == null ? String.Empty :
                Path.GetExtension(CurrentPath).TrimStart('.').ToUpperInvariant() + " · " +
                L10n.Text(language, documentMediaNoteKey) + " · ";
            if (mediaState == "media_unavailable")
            {
                description = prefix + FormatBytes(sourceBytes);
                return;
            }
            if (media != null && media.IsOpen && mediaState != "media_loading")
            {
                description = prefix + FormatTime(media.Position);
                if (media.Duration > TimeSpan.Zero) description += " / " + FormatTime(media.Duration);
                description += "  ·  " + FormatBytes(sourceBytes);
                if (mediaState == "media_ended" || mediaState == "media_paused")
                    description += "  ·  " + L10n.Text(language, interactive && mediaState == "media_paused" ? "paused" : mediaState);
            }
            else description = prefix + L10n.Text(language, mediaState ?? "media_loading") + "  ·  " + FormatBytes(sourceBytes);
        }

        private static string FormatTime(TimeSpan value)
        {
            if (value.TotalHours >= 1) return ((int)value.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
            return value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        public void ClearPreview()
        {
            if (entranceMotion != null) entranceMotion.Finish();
            CancelImageLoad();
            CancelDocumentLoad();
            if (mediaTimer != null) mediaTimer.Stop();
            if (slideshowTimer != null) slideshowTimer.Stop();
            MediaPlayback previousMedia = media;
            media = null;
            if (previousMedia != null) previousMedia.Dispose();
            mediaKind = MediaKind.None;
            mediaState = null;
            documentMediaNoteKey = null;
            textPreview = null;
            pdfPageIndex = pdfPageCount = textOffset = 0; playbackPaused = false; zoom = 1; pan = DrawingPoint.Empty;
            if (!IsDisposed)
                Hide();
            CurrentPath = null;
            fileName = null;
            description = null;
            ClearImage();
        }

        private void ClearImage()
        {
            if (animationTimer != null) animationTimer.Stop();
            if (slideshowTimer != null) slideshowTimer.Stop();
            documentSlides = null;
            documentSlideIndex = 0;
            Bitmap previous = preview;
            preview = null;
            if (animatedGif != null)
            {
                animatedGif.Dispose();
                animatedGif = null;
            }
            else if (previous != null) previous.Dispose();
        }

        private void AdvanceAnimation(object sender, EventArgs e)
        {
            if (animatedGif == null || !Visible) { animationTimer.Stop(); return; }
            try
            {
                if (!animatedGif.Advance()) { animationTimer.Stop(); return; }
                animationTimer.Interval = animatedGif.FrameDelay;
                Invalidate(imageArea);
            }
            catch { ClearPreview(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (preview == null && mediaKind == MediaKind.None)
                return;

            Graphics graphics = e.Graphics;
            if (mediaKind == MediaKind.Text)
            {
                PaintText(graphics);
                PaintFooter(graphics);
                return;
            }
            if ((mediaKind == MediaKind.Document || mediaKind == MediaKind.Image) && preview == null)
            {
                PaintDocumentCard(graphics);
                PaintFooter(graphics);
                return;
            }
            if (mediaKind == MediaKind.Audio || mediaKind == MediaKind.Video)
            {
                PaintMedia(graphics);
                PaintFooter(graphics);
                return;
            }
            int checker = Px(10);
            using (SolidBrush first = new SolidBrush(Brand.CheckerFirst))
            using (SolidBrush second = new SolidBrush(Brand.CheckerSecond))
            {
                graphics.FillRectangle(first, imageArea);
                for (int y = 0; y < imageArea.Height; y += checker)
                {
                    for (int x = 0; x < imageArea.Width; x += checker)
                    {
                        if (((x / checker) + (y / checker)) % 2 != 0)
                        {
                            graphics.FillRectangle(second, imageArea.Left + x, imageArea.Top + y,
                                Math.Min(checker, imageArea.Width - x),
                                Math.Min(checker, imageArea.Height - y));
                        }
                    }
                }
            }

            graphics.CompositingMode = CompositingMode.SourceOver;
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            GraphicsState imageState = graphics.Save();
            graphics.SetClip(imageArea, CombineMode.Intersect);
            graphics.DrawImage(preview, ZoomedImageArea());
            graphics.Restore(imageState);

            PaintFooter(graphics);
        }

        private void PaintMedia(Graphics graphics)
        {
            // The native video child draws live frames; GDI draws only the audio/loading card.
            if (media != null && media.IsOpen && media.HasVideo) return;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            SettingsWindow.FillRound(graphics, imageArea, Px(9), Brand.Surface);
            string title = L10n.Text(language, mediaKind == MediaKind.Video ? "video_preview" : "audio_preview");
            TextFormatFlags flags = TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
            if (mediaState == "media_unavailable")
            {
                TextRenderer.DrawText(graphics, title, titleFont,
                    new Rectangle(imageArea.X + Px(8), imageArea.Y + Px(8), imageArea.Width - Px(16), Px(24)), Brand.Text, flags);
                TextRenderer.DrawText(graphics, L10n.Text(language, mediaState), descriptionFont,
                    new Rectangle(imageArea.X + Px(16), imageArea.Y + Px(38), imageArea.Width - Px(32), imageArea.Height - Px(46)),
                    Brand.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter |
                    TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
                return;
            }
            int centerX = imageArea.Left + imageArea.Width / 2;
            int centerY = imageArea.Top + imageArea.Height / 2 - Px(18);
            using (var pen = new System.Drawing.Pen(Brand.Accent, Px(3)))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                int[] bars = { 6, 14, 22, 11, 18 };
                for (int i = 0; i < bars.Length; i++)
                    graphics.DrawLine(pen, centerX + Px(8) * (i - 2), centerY - Px(bars[i]) / 2,
                        centerX + Px(8) * (i - 2), centerY + Px(bars[i]) / 2);
            }
            TextRenderer.DrawText(graphics, title, titleFont,
                new Rectangle(imageArea.X + Px(8), centerY + Px(16), imageArea.Width - Px(16), Px(24)), Brand.Text, flags);
        }

        private void PaintFooter(Graphics graphics)
        {
            TextFormatFlags textFlags = TextFormatFlags.Left | TextFormatFlags.SingleLine |
                TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix | TextFormatFlags.VerticalCenter;
            Brand.DrawLogo(graphics,new Rectangle(Px(12),imageArea.Bottom+Px(12),Px(16),Px(16)));
            TextRenderer.DrawText(graphics, fileName, titleFont, titleArea, ForeColor, textFlags);
            TextRenderer.DrawText(graphics, description, descriptionFont, descriptionArea,
                Brand.Muted, textFlags);
            using (System.Drawing.Pen border = new System.Drawing.Pen(Brand.Border))
                graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        protected override void WndProc(ref Message message)
        {
            if (!interactive && message.Msg == 0x0084) // WM_NCHITTEST: pass through to Explorer.
            {
                message.Result = new IntPtr(-1);
                return;
            }
            if (!interactive && message.Msg == 0x0021) // WM_MOUSEACTIVATE: never focus this window.
            {
                message.Result = new IntPtr(3);
                return;
            }
            base.WndProc(ref message);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ClearPreview();
                if (entranceMotion != null) entranceMotion.Dispose();
                contentTip.Dispose();
                if (toolbarFont != null) toolbarFont.Dispose();
                if (animationTimer != null) animationTimer.Dispose();
                if (slideshowTimer != null) slideshowTimer.Dispose();
                if (mediaTimer != null) mediaTimer.Dispose();
                if (preview != null)
                {
                    preview.Dispose();
                    preview = null;
                }
                if (titleFont != null)
                {
                    titleFont.Dispose();
                    titleFont = null;
                }
                if (descriptionFont != null)
                {
                    descriptionFont.Dispose();
                    descriptionFont = null;
                }
                if (codeFont != null)
                {
                    codeFont.Dispose();
                    codeFont = null;
                }
            }
            base.Dispose(disposing);
        }

        private int Px(int logicalPixels)
        {
            return Math.Max(1, (int)Math.Round(logicalPixels * scale));
        }

        private void SetFonts()
        {
            if (titleFont != null)
                titleFont.Dispose();
            if (descriptionFont != null)
                descriptionFont.Dispose();
            if (codeFont != null)
                codeFont.Dispose();
            titleFont = new Font("Segoe UI", 13F * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            descriptionFont = new Font("Segoe UI", 11F * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            codeFont = new Font("Consolas", 14F * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        internal static Bitmap LoadImage(string path, int maximumWidth, int maximumHeight,
            out int sourceWidth, out int sourceHeight, out long bytes)
        {
            int orientation=1;
            // DelayCreation reads dimensions without eagerly allocating the original pixels.
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                bytes = stream.Length;
                if (bytes <= 0 || bytes > MaximumFileBytes)
                    throw new InvalidDataException("Attēla fails ir pārāk liels.");
                BitmapDecoder decoder = BitmapDecoder.Create(stream,
                    BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
                if (decoder.Frames.Count == 0)
                    throw new InvalidDataException("Attēls nesatur kadrus.");
                sourceWidth = decoder.Frames[0].PixelWidth;
                sourceHeight = decoder.Frames[0].PixelHeight;
                try
                {
                    BitmapMetadata metadata=decoder.Frames[0].Metadata as BitmapMetadata;
                    if(metadata!=null)
                    {
                        object value=metadata.GetQuery("/app1/ifd/{ushort=274}") ?? metadata.GetQuery("/ifd/{ushort=274}");
                        if(value!=null) orientation=Convert.ToInt32(value);
                    }
                }
                catch { }
                if (sourceWidth <= 0 || sourceHeight <= 0 ||
                    (long)sourceWidth * sourceHeight > MaximumPixels)
                    throw new InvalidDataException("Attēla izmēri ir pārāk lieli.");
            }

            double ratio = Math.Min(1.0, Math.Min(
                (double)maximumWidth / sourceWidth, (double)maximumHeight / sourceHeight));
            BitmapImage decoded = new BitmapImage();
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete))
            {
                if (stream.Length <= 0 || stream.Length > MaximumFileBytes)
                    throw new InvalidDataException("Attēla fails ir pārāk liels.");
                decoded.BeginInit();
                decoded.CacheOption = BitmapCacheOption.OnLoad;
                decoded.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                decoded.StreamSource = stream;
                // A single dimension preserves the original aspect ratio, including WIC codecs.
                if (ratio < 1.0)
                    decoded.DecodePixelWidth = Math.Max(1, (int)Math.Floor(sourceWidth * ratio));
                decoded.EndInit();
                decoded.Freeze();
            }

            Bitmap detached = new Bitmap(decoded.PixelWidth, decoded.PixelHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);

            // Copy premultiplied pixels directly; avoid an extra BMP encode/decode and retain alpha.
            try
            {
                FormatConvertedBitmap pixels = new FormatConvertedBitmap(decoded,
                    PixelFormats.Pbgra32, null, 0);
                int stride = checked(pixels.PixelWidth * 4);
                byte[] buffer = new byte[checked(stride * pixels.PixelHeight)];
                pixels.CopyPixels(buffer, stride, 0);
                BitmapData locked = detached.LockBits(new Rectangle(0, 0, detached.Width,
                    detached.Height), ImageLockMode.WriteOnly,
                    System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                try
                {
                    for (int row = 0; row < pixels.PixelHeight; row++)
                        Marshal.Copy(buffer, row * stride,
                            IntPtr.Add(locked.Scan0, row * locked.Stride), stride);
                }
                finally
                {
                    detached.UnlockBits(locked);
                }
                RotateFlipType[] transforms = {
                    RotateFlipType.RotateNoneFlipNone, RotateFlipType.RotateNoneFlipNone,
                    RotateFlipType.RotateNoneFlipX, RotateFlipType.Rotate180FlipNone,
                    RotateFlipType.Rotate180FlipX, RotateFlipType.Rotate90FlipX,
                    RotateFlipType.Rotate90FlipNone, RotateFlipType.Rotate270FlipX,
                    RotateFlipType.Rotate270FlipNone
                };
                if(orientation>=2 && orientation<=8) detached.RotateFlip(transforms[orientation]);
                if(orientation>=5 && orientation<=8) { int oldWidth=sourceWidth; sourceWidth=sourceHeight; sourceHeight=oldWidth; }
                return detached;
            }
            catch
            {
                detached.Dispose();
                throw;
            }
        }

        private string FormatBytes(long bytes)
        {
            CultureInfo culture = L10n.Culture(language);
            if (bytes >= 1024L * 1024L)
                return (bytes / (1024.0 * 1024.0)).ToString("0.#", culture) + " MB";
            if (bytes >= 1024L)
                return (bytes / 1024.0).ToString("0.#", culture) + " KB";
            return bytes.ToString(culture) + " B";
        }

        private static DrawingPoint ChooseLocation(DrawingPoint cursor, Rectangle item,
            Rectangle workingArea, DrawingSize size, int gap)
        {
            if (item.IsEmpty)
                item = new Rectangle(cursor.X - 2, cursor.Y - 2, 4, 4);
            DrawingPoint[] candidates = new DrawingPoint[]
            {
                new DrawingPoint(cursor.X + gap, cursor.Y + gap),
                new DrawingPoint(item.Right + gap, cursor.Y - gap),
                new DrawingPoint(item.Left - size.Width - gap, cursor.Y - gap),
                new DrawingPoint(cursor.X + gap, item.Bottom + gap),
                new DrawingPoint(cursor.X + gap, item.Top - size.Height - gap),
                new DrawingPoint(cursor.X - size.Width - gap, item.Bottom + gap),
                new DrawingPoint(cursor.X - size.Width - gap, item.Top - size.Height - gap)
            };
            DrawingPoint best = candidates[0];
            long bestScore = Int64.MaxValue;
            for (int index = 0; index < candidates.Length; index++)
            {
                DrawingPoint candidate = new DrawingPoint(
                    Math.Max(workingArea.Left, Math.Min(candidates[index].X,
                        workingArea.Right - size.Width)),
                    Math.Max(workingArea.Top, Math.Min(candidates[index].Y,
                        workingArea.Bottom - size.Height)));
                Rectangle card = new Rectangle(candidate, size);
                Rectangle overlap = Rectangle.Intersect(card, item);
                long score = (long)Math.Max(0, overlap.Width) * Math.Max(0, overlap.Height) * 100L;
                if (card.Contains(cursor))
                    score += 1000000000L;
                score += Math.Abs(candidate.X - cursor.X) + Math.Abs(candidate.Y - cursor.Y) + index;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        internal static float GetScale(DrawingPoint cursor)
        {
            try
            {
                uint dpiX;
                uint dpiY;
                NativePoint point = new NativePoint();
                point.X = cursor.X;
                point.Y = cursor.Y;
                IntPtr monitor = MonitorFromPoint(point, 2);
                if (GetDpiForMonitor(monitor, 0, out dpiX, out dpiY) == 0 && dpiX > 0)
                    return Math.Max(1F, Math.Min(4F, dpiX / 96F));
            }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
            using (Graphics desktop = Graphics.FromHwnd(IntPtr.Zero))
                return Math.Max(1F, Math.Min(4F, desktop.DpiX / 96F));
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(NativePoint point, uint flags);

        [DllImport("shcore.dll")]
        private static extern int GetDpiForMonitor(IntPtr monitor, int dpiType,
            out uint dpiX, out uint dpiY);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);
    }
}
