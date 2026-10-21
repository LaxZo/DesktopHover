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
    /// <summary>A passive, non-activating image tooltip. Use on the UI thread.</summary>
    public sealed class PreviewWindow : Form
    {
        private const long MaximumFileBytes = 100L * 1024L * 1024L;
        private const long MaximumPixels = 100000000L;
        private const int WsExTransparent = 0x20;
        private const int WsExToolWindow = 0x80;
        private const int WsExNoActivate = 0x08000000;
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

        public string CurrentPath { get; private set; }
        internal DrawingSize DisplayedImageSize { get { return imageArea.Size; } }
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
                parameters.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
                parameters.ClassStyle |= 0x00020000; // CS_DROPSHADOW
                return parameters;
            }
        }

        public bool ShowImage(string path, DrawingPoint cursor, Rectangle itemBounds)
        {
            if (IsDisposed || String.IsNullOrEmpty(path))
                return false;

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

                int padding = Px(12);
                int footerHeight = Px(48);
                int maximumWidth = Math.Max(1, Math.Min(maximumPreviewWidth,
                    workingArea.Width - padding * 2 - Px(32)));
                int maximumHeight = Math.Max(1, Math.Min(maximumPreviewHeight,
                    workingArea.Height - padding * 2 - footerHeight - Px(32)));

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
                ClientSize = new DrawingSize(contentWidth + padding * 2,
                    imageHeight + padding * 2 + footerHeight);
                imageArea = new Rectangle(padding + (contentWidth - imageWidth) / 2,
                    padding, imageWidth, imageHeight);
                titleArea = new Rectangle(padding+Px(22), imageArea.Bottom + Px(11),
                    contentWidth-Px(22), Px(19));
                descriptionArea = new Rectangle(padding, titleArea.Bottom + Px(1),
                    contentWidth, Px(17));

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

        public void ClearPreview()
        {
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
            if (preview == null)
                return;

            Graphics graphics = e.Graphics;
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
