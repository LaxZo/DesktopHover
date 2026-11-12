using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static partial class Regression
    {
        private static void PinnedUpdates(string videoSource)
        {
            if (videoSource == null)
            {
                videoSource = Path.Combine(output, "original.avi");
                WriteVideo(videoSource);
            }
            using (var window = new PreviewWindow(true))
            {
                var settings = new AppSettings { Language = "lv", AutoPlayMedia = false, AnimationsEnabled = false, VolumePercent = 80 };
                window.Configure(settings);
                string path = Path.Combine(output, "rewritten" + Path.GetExtension(videoSource));
                File.Copy(videoSource, path, true);
                window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
                Until(() => window.MediaState == "media_paused", 16000);
                var volume = Field<HoverSlider>(window, "mediaVolume");
                var player = Field<System.Windows.Media.MediaPlayer>(Field<MediaPlayback>(window, "media"), "player");
                volume.Value = 25;
                settings.Theme = "light"; window.Configure(settings);
                Check(volume.Value == 25 && Math.Abs(player.Volume - .25) < .001, "changing theme preserves the pinned volume");
                settings.VolumePercent = 35; window.Configure(settings);
                Check(volume.Value == 35 && Math.Abs(player.Volume - .35) < .001, "changing the global volume updates the player");
                Field<HoverSlider>(window, "mediaSeek").Value = 500;
                Until(() => window.PlaybackPosition.TotalMilliseconds > 100, 3000);
                double position = window.PlaybackPosition.TotalMilliseconds;
                Until(() => Field<string>(window, "contentStamp") != null, 3000);
                string empty = Path.Combine(output, "empty-media.tmp");
                File.WriteAllBytes(empty, new byte[0]);
                File.Replace(empty, path, null);
                Until(() => window.MediaState == "file_unavailable", 5000);
                Check(window.CurrentPath == path && window.Visible, "a zero-byte media rewrite retains the pinned path");
                RefreshAndWait(window);
                Check(window.CurrentPath == path && window.MediaState == "file_unavailable",
                    "manual refresh during a rewrite keeps watching the same file");
                File.Copy(videoSource, path, true);
                Until(() => window.MediaState == "media_paused", 16000);
                Check(Math.Abs(window.PlaybackPosition.TotalMilliseconds - position) < 200,
                    "the completed rewrite restores the paused position without repinning");
                MarkdownWrappedMatches(window);
                ArchiveEntryLimit(window);
                RefreshCachedImages(window);
            }
        }

        private static void MarkdownWrappedMatches(PreviewWindow window)
        {
            string path = Path.Combine(output, "wrapped-find.md");
            string paragraph = "Piespraustajā logā var aplūkot attēlus un dokumentus, meklēt vārdus garākā tekstā un atskaņot video ar skaņu.";
            File.WriteAllLines(path, new[] { paragraph, paragraph });
            window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
            Until(() => Field<TextPreviewContent>(window, "textPreview") != null, 5000);
            window.ClientSize = new Size(520, 440);
            Call(window, "OpenFind");
            var lines = Field<TextPreviewContent>(window, "textPreview").Lines;
            Check(lines.Length > 2 && lines[1].MarkdownBlock == lines[0].MarkdownBlock, "the paragraph wraps across visual rows");
            string first = lines[0].Text.Substring(lines[0].Text.LastIndexOf(' ') + 1);
            string second = lines[1].Text.Split(' ')[0];
            Field<TextBox>(window, "findText").Text = first + " " + second;
            var count = Field<ToolStripLabel>(window, "findCount");
            Check(count.Text == "1 / 2", "a wrapped phrase is counted once per paragraph");
            using (var bitmap = new Bitmap(520, 40))
            using (var g = Graphics.FromImage(bitmap))
                for (int i = 0; i < 2; i++)
                    Check((bool)Call(window, "PaintFoundText", g, i, 0, lines[i].Text, window.Font, new Rectangle(0, 0, 520, 40)),
                        "both visual parts of the selected phrase can be highlighted");
            Call(window, "HandleFindKey", Keys.F3);
            window.ClientSize = new Size(900, 500);
            Check(count.Text == "2 / 2", "resizing keeps the same logical match selected");
        }

        private static void ArchiveEntryLimit(PreviewWindow window)
        {
            string path = Path.Combine(output, "archive-limit.zip");
            using (var file = File.Create(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                for (int i = 0; i < DocumentArchiveGuard.MaximumEntries; i++) zip.CreateEntry(i + ".txt");
            Check(TextPreviewContent.Load(path).ArchiveFileCount == DocumentArchiveGuard.MaximumEntries, "ZIP entry limit is inclusive");
            using (var file = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Update)) zip.CreateEntry("extra.txt");
            window.ShowFile(path, new Point(300, 300), Rectangle.Empty);
            Until(() => window.MediaState == "archive_entry_limit", 5000);
            Check(Field<string>(window, "description").Contains(DocumentArchiveGuard.MaximumEntries.ToString()),
                "the ZIP limit is displayed in the preview");
            File.WriteAllText(path, "invalid archive");
            Call(window, "RefreshPinned");
            Until(() => window.MediaState == "archive_unavailable", 5000);
        }

        private static void RefreshCachedImages(PreviewWindow window)
        {
            string first = Path.Combine(output, "cached-one.bmp"), second = Path.Combine(output, "cached-two.bmp");
            var size = new Size(1600, 1200);
            foreach (string path in new[] { first, second })
            {
                WriteCacheBitmap(path, Color.Red);
                using (var cached = ImagePreviewLoader.Load(path, size, CancellationToken.None))
                    Check(cached.Image.GetPixel(0, 0).R == 255, "image is cached");
                DateTime written = File.GetLastWriteTimeUtc(path);
                WriteCacheBitmap(path, Color.Blue);
                File.SetLastWriteTimeUtc(path, written);
            }
            window.ShowFile(first, new Point(300, 300), Rectangle.Empty);
            Until(() => window.HasImage && !window.IsImageLoading, 5000);
            Check(Field<Bitmap>(window, "preview").GetPixel(0, 0).R == 255, "unchanged metadata serves the cached pixels");
            RefreshAndWait(window);
            Check(Field<Bitmap>(window, "preview").GetPixel(0, 0).B == 255, "refresh decodes the pinned image again");
            using (var kept = ImagePreviewLoader.Load(second, size, CancellationToken.None))
                Check(kept.Image.GetPixel(0, 0).R == 255, "refresh leaves other image cache entries usable");
        }

        private static void WriteCacheBitmap(string path, Color color)
        {
            using (var bitmap = new Bitmap(64, 48))
            {
                using (var graphics = Graphics.FromImage(bitmap)) graphics.Clear(color);
                bitmap.Save(path, ImageFormat.Bmp);
            }
        }
    }
}
