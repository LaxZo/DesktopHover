using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingColor = System.Drawing.Color;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;

namespace DesktopHover
{
    /// <summary>A passive, non-activating image, audio or video preview. Use on the UI thread.</summary>
    public sealed class PreviewWindow : Form
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
        private string fileName;
        private string description;
        private Rectangle imageArea;
        private Rectangle titleArea;
        private Rectangle descriptionArea;
        private Font titleFont;
        private Font descriptionFont;
        private float scale = 1F;
        private int sourceWidth;
        private int sourceHeight;
        private long sourceBytes;
        private int maximumPreviewWidth=720;
        private int maximumPreviewHeight=540;
        private bool upscale;
        private string language="en";
        private MediaPlayback media;
        private MediaKind mediaKind;
        private string mediaState;
        private DrawingPoint mediaCursor;
        private Rectangle mediaItemBounds;
        private DateTime mediaDeadline;
        private readonly Timer mediaTimer;

        public string CurrentPath { get; private set; }
        internal DrawingSize DisplayedImageSize { get { return imageArea.Size; } }
        internal bool IsMediaPreview { get { return mediaKind != MediaKind.None; } }
        internal string MediaState { get { return mediaState; } }
        internal TimeSpan PlaybackPosition { get { return media == null ? TimeSpan.Zero : media.Position; } }
        internal void Configure(AppSettings settings)
        {
            maximumPreviewWidth=settings.MaxWidth;
            maximumPreviewHeight=settings.MaxHeight;
            upscale=settings.Upscale;
            language=L10n.Normalize(settings.Language);
            UpdateLanguage();
            ClearPreview();
        }

        public PreviewWindow()
        {
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
            mediaTimer = new Timer { Interval = 200 };
            mediaTimer.Tick += delegate
            {
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
        }

        private void UpdateLanguage()
        {
            AccessibleName=L10n.Text(language,"preview_title");
            Text=AccessibleName+" — Desktop Hover";
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams parameters = base.CreateParams;
                // The WPF video surface owns its pixels; parent paints must not cover it.
                parameters.Style |= WsClipChildren;
                parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
                parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return parameters;
            }
        }

        public bool ShowImage(string path, DrawingPoint cursor, Rectangle itemBounds)
        {
            if (IsDisposed || String.IsNullOrEmpty(path))
                return false;
            if (mediaKind != MediaKind.None) ClearPreview();
            UpdateLanguage();

            try
            {
                Rectangle workingArea = Screen.FromPoint(cursor).WorkingArea;
                float newScale = GetScale(cursor);
                bool changedScale = Math.Abs(scale - newScale) > 0.01F;
                if (changedScale)
                {
                    scale = newScale;
                    SetFonts();
                }

                int imagePadding = Px(3);
                int footerPadding = Px(12);
                int footerHeight = Px(48);
                int maximumWidth = Math.Max(1, Math.Min(maximumPreviewWidth,
                    workingArea.Width - imagePadding * 2 - Px(32)));
                int maximumHeight = Math.Max(1, Math.Min(maximumPreviewHeight,
                    workingArea.Height - imagePadding - footerPadding - footerHeight - Px(32)));

                // A screen change may require a new decode at its appropriate resolution.
                if (preview == null || changedScale ||
                    !String.Equals(CurrentPath, path, StringComparison.OrdinalIgnoreCase))
                {
                    int width;
                    int height;
                    long bytes;
                    Bitmap replacement = LoadImage(path, maximumWidth, maximumHeight,
                        out width, out height, out bytes);
                    Bitmap previous = preview;
                    preview = replacement;
                    if (previous != null)
                        previous.Dispose();

                    sourceWidth = width;
                    sourceHeight = height;
                    sourceBytes = bytes;
                    CurrentPath = path;
                    fileName = Path.GetFileName(path);
                    description = String.Format(L10n.Culture(language),
                        "{0} × {1} px  ·  {2}", sourceWidth, sourceHeight, FormatBytes(sourceBytes));
                }

                double fit = Math.Min(
                    (double)maximumWidth / preview.Width,
                    (double)maximumHeight / preview.Height);
                if(!upscale) fit=Math.Min(1.0,fit);
                int imageWidth = Math.Max(1, (int)Math.Round(preview.Width * fit));
                int imageHeight = Math.Max(1, (int)Math.Round(preview.Height * fit));
                int contentWidth = Math.Min(maximumWidth, Math.Max(Px(220), imageWidth));
                ClientSize = new DrawingSize(contentWidth + imagePadding * 2,
                    imageHeight + imagePadding + footerPadding + footerHeight);
                imageArea = new Rectangle(imagePadding + (contentWidth - imageWidth) / 2,
                    imagePadding, imageWidth, imageHeight);
                int footerWidth = Math.Max(1, ClientSize.Width - footerPadding * 2);
                titleArea = new Rectangle(footerPadding + Px(22), imageArea.Bottom + Px(11),
                    Math.Max(1, footerWidth - Px(22)), Px(19));
                descriptionArea = new Rectangle(footerPadding, titleArea.Bottom + Px(1),
                    footerWidth, Px(17));

                Location = ChooseLocation(cursor, itemBounds, workingArea, Size, Px(18));
                if (!Visible)
                    Show();
                SetWindowPos(Handle, new IntPtr(-1), Left, Top, Width, Height,
                    SwpNoActivate | SwpShowWindow);
                Invalidate();
                return true;
            }
            catch
            {
                // Unsupported codecs, disappearing files, and malformed images are skipped.
                ClearPreview();
                return false;
            }
        }

        internal bool ShowFile(string path, DrawingPoint cursor, Rectangle itemBounds)
        {
            MediaKind kind = MediaFiles.Kind(path);
            if (kind == MediaKind.Image) return ShowImage(path, cursor, itemBounds);
            if (kind == MediaKind.Audio || kind == MediaKind.Video) return ShowMedia(path, kind, cursor, itemBounds);
            ClearPreview();
            return false;
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
                var session = new MediaPlayback(Handle);
                media = session;
                session.Opened += delegate
                {
                    if (media != session || IsDisposed) return;
                    try
                    {
                        mediaState = "media_playing";
                        LayoutMedia();
                        session.Play();
                        UpdateMediaDescription();
                        Invalidate();
                    }
                    catch { MediaFailed(session); }
                };
                session.Ended += delegate
                {
                    if (media != session || IsDisposed) return;
                    mediaState = "media_ended";
                    mediaTimer.Stop();
                    UpdateMediaDescription();
                    Invalidate();
                };
                session.Failed += delegate { MediaFailed(session); };
                mediaDeadline = DateTime.UtcNow.AddSeconds(15);
                LayoutMedia();
                UpdateMediaDescription();
                mediaTimer.Start();
                session.Open(path);
                return true;
            }
            catch
            {
                ClearPreview();
                return false;
            }
        }

        private void MediaFailed(MediaPlayback session)
        {
            if (media != session || IsDisposed) return;
            media = null;
            session.Dispose();
            mediaTimer.Stop();
            mediaState = "media_unavailable";
            UpdateMediaDescription();
            Invalidate();
        }

        private void LayoutMedia()
        {
            Rectangle workingArea = Screen.FromPoint(mediaCursor).WorkingArea;
            int padding = Px(12), footerHeight = Px(48);
            int maxWidth = Math.Max(1, Math.Min(maximumPreviewWidth, workingArea.Width - padding * 2 - Px(32)));
            int maxHeight = Math.Max(1, Math.Min(maximumPreviewHeight, workingArea.Height - padding * 2 - footerHeight - Px(32)));
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
            int contentWidth = Math.Min(maxWidth, Math.Max(Px(220), width));
            ClientSize = new DrawingSize(contentWidth + padding * 2, height + padding * 2 + footerHeight);
            imageArea = new Rectangle(padding + (contentWidth - width) / 2, padding, width, height);
            titleArea = new Rectangle(padding + Px(22), imageArea.Bottom + Px(11), contentWidth - Px(22), Px(19));
            descriptionArea = new Rectangle(padding, titleArea.Bottom + Px(1), contentWidth, Px(17));
            Location = ChooseLocation(mediaCursor, mediaItemBounds, workingArea, Size, Px(18));
            if (!Visible) Show();
            if (media != null && media.IsOpen && media.HasVideo) media.SetVideoBounds(imageArea);
            SetWindowPos(Handle, new IntPtr(-1), Left, Top, Width, Height, SwpNoActivate | SwpShowWindow);
            Invalidate();
        }

        private void UpdateMediaDescription()
        {
            if (mediaState == "media_unavailable")
            {
                description = FormatBytes(sourceBytes);
                return;
            }
            if (media != null && media.IsOpen && mediaState != "media_loading")
            {
                description = FormatTime(media.Position);
                if (media.Duration > TimeSpan.Zero) description += " / " + FormatTime(media.Duration);
                description += "  ·  " + FormatBytes(sourceBytes);
                if (mediaState == "media_ended") description += "  ·  " + L10n.Text(language, mediaState);
            }
            else description = L10n.Text(language, mediaState ?? "media_loading") + "  ·  " + FormatBytes(sourceBytes);
        }

        private static string FormatTime(TimeSpan value)
        {
            if (value.TotalHours >= 1) return ((int)value.TotalHours).ToString(CultureInfo.InvariantCulture) + ":" + value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
            return value.ToString(@"mm\:ss", CultureInfo.InvariantCulture);
        }

        public void ClearPreview()
        {
            if (mediaTimer != null) mediaTimer.Stop();
            MediaPlayback previousMedia = media;
            media = null;
            if (previousMedia != null) previousMedia.Dispose();
            mediaKind = MediaKind.None;
            mediaState = null;
            if (!IsDisposed)
                Hide();
            CurrentPath = null;
            fileName = null;
            description = null;
            Bitmap previous = preview;
            preview = null;
            if (previous != null)
                previous.Dispose();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (preview == null && mediaKind == MediaKind.None)
                return;

            Graphics graphics = e.Graphics;
            if (mediaKind != MediaKind.None)
            {
                PaintMedia(graphics);
                PaintFooter(graphics);
                return;
            }
            int checker = Px(10);
            using (SolidBrush first = new SolidBrush(DrawingColor.FromArgb(43, 46, 51)))
            using (SolidBrush second = new SolidBrush(DrawingColor.FromArgb(53, 56, 61)))
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
            graphics.DrawImage(preview, imageArea);

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
            if (message.Msg == 0x0084) // WM_NCHITTEST: pass through to Explorer.
            {
                message.Result = new IntPtr(-1);
                return;
            }
            if (message.Msg == 0x0021) // WM_MOUSEACTIVATE: never focus this window.
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
            titleFont = new Font("Segoe UI", 13F * scale, FontStyle.Regular, GraphicsUnit.Pixel);
            descriptionFont = new Font("Segoe UI", 11F * scale, FontStyle.Regular, GraphicsUnit.Pixel);
        }

        private static Bitmap LoadImage(string path, int maximumWidth, int maximumHeight,
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

            // BMP encoding detaches the GDI bitmap from the WIC source and its file streams.
            Bitmap detached;
            using (MemoryStream encoded = new MemoryStream())
            {
                BmpBitmapEncoder encoder = new BmpBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(decoded));
                encoder.Save(encoded);
                encoded.Position = 0;
                using (Bitmap temporary = new Bitmap(encoded))
                {
                    detached = temporary.Clone(new Rectangle(0, 0, temporary.Width, temporary.Height),
                        System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                }
            }

            // BMP readers do not consistently retain alpha. Copy premultiplied pixels explicitly
            // so PNG, GIF, ICO, and other transparent formats show the checkerboard correctly.
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

        private static float GetScale(DrawingPoint cursor)
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
