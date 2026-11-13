using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace DesktopHover
{
    internal static partial class Regression
    {
        private static void Set(object target, string name, object value)
        { target.GetType().GetField(name, Private).SetValue(target, value); }

        private static void ReviewFixes()
        {
            GuardRecovery(); SpreadsheetFormats(); SpreadsheetBottomRow();
            OrientedImageSizing(); StableDocumentZoom(); DisplayNameRefresh(); Diagnostics();
        }

        private static void GuardRecovery()
        {
            // Invalid HWNDs cannot change Explorer; kill only this test's own helper.
            var first = new InfoTipRestoreGuard.Lease { Window = new IntPtr(1) };
            var second = new InfoTipRestoreGuard.Lease { Window = new IntPtr(2) };
            var forgotten = new InfoTipRestoreGuard.Lease { Window = new IntPtr(3) };
            using (var guard = new InfoTipRestoreGuard())
            {
                Set(guard, "nextStart", DateTime.UtcNow.AddMinutes(1));
                bool allDeferred = true;
                for (int i = 10; i < 90; i++)
                    allDeferred &= !guard.Remember(new InfoTipRestoreGuard.Lease { Window = new IntPtr(i) });
                Check(allDeferred && Field<Dictionary<IntPtr, InfoTipRestoreGuard.Lease>>(guard, "remembered").Count == 0,
                    "deferred registrations cannot exhaust the recovery lease limit");
                Set(guard, "nextStart", DateTime.MinValue);
                Check(guard.Remember(first) && guard.Remember(second) && guard.Remember(forgotten), "helper accepts active leases");
                guard.Forget(forgotten.Window);
                var old = Field<Process>(guard, "worker"); int previousId = old.Id;
                old.Kill(); Check(old.WaitForExit(3000), "test helper terminates");
                bool recovered = false;
                Until(() => recovered = guard.Remember(first), 4000);
                var replacement = Field<Process>(guard, "worker");
                Check(recovered && replacement.Id != previousId && !replacement.HasExited, "helper recovers automatically after a crash");
                var replayed = Field<HashSet<IntPtr>>(guard, "registered");
                Check(replayed.Count == 2 && replayed.Contains(second.Window) && !replayed.Contains(forgotten.Window),
                    "replacement protects all active leases and excludes forgotten handles");
                using (var observer = Process.GetProcessById(replacement.Id))
                {
                    guard.Dispose(); Check(observer.WaitForExit(3000), "helper exits after pipe shutdown");
                    Check(!guard.Remember(first), "disposed guard cannot spawn another helper");
                }
            }
        }

        private static void SpreadsheetFormats()
        {
            FormatCheck("46000", "yyyy-mm-dd", "2025-12-09");
            FormatCheck("46000.5625", "dd.mm.yyyy hh:mm", "09.12.2025 13:30");
            FormatCheck("0.5625", "h:mm AM/PM", "1:30 PM");
            FormatCheck("0.5625", "h:mm A/P", "1:30 P");
            FormatCheck("0.0625", "mm:ss", "30:00");
            FormatCheck("1.5", "[h]:mm:ss", "36:00:00");
            FormatCheck("1.5", "[mm]:ss", "2160:00");
            FormatCheck("46000", "[$-409]mmm d, yyyy", "Dec 9, 2025");
            FormatCheck("1234.5", "[$€-409]#,##0.00", "€1,234.50");
            FormatCheck("-1234.5", "#,##0.00;[Red](#,##0.00)", "(1,234.50)");
            FormatCheck("0.125", "0.0%", "12.5%");
            FormatCheck("12", "0\" days\"", "12 days");
            FormatCheck("12", "0\"; units\"", "12; units");
            FormatCheck("12", "[>=10]0.0;0.00", "12.0");
            FormatCheck("2", "[>=10]0.0;0.00", "2.00");
            FormatCheck("0", "0;-0;\"none\"", "none");
            FormatCheck("0", "0;-0;", "");
            FormatCheck("1", "yyyy-mm-dd", "1900-01-01");
            FormatCheck("60", "yyyy-mm-dd", "1900-02-29");
            Check(SpreadsheetNumberFormat.Format("0", 164, "yyyy-mm-dd", true) == "1904-01-01", "1904 workbook date system is respected");
            Check(SpreadsheetNumberFormat.Format("0.125", 10, null, false) == "12.50%", "built-in percentage format is retained");
            FormatCheck("46000", "[broken", "46000");
            FormatCheck("not a number", "yyyy-mm-dd", "not a number");
        }

        private static void FormatCheck(string value, string format, string expected)
        {
            string actual = SpreadsheetNumberFormat.Format(value, 164, format, false);
            Check(actual == expected, "Excel format " + format + ": expected '" + expected + "', actual '" + actual + "'");
        }

        private static void SpreadsheetBottomRow()
        {
            string path = Path.Combine(output, "formatted-last-row.xlsx");
            const string rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            using (var stream = File.Create(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                OfficeEntry(zip, "_rels/.rels", "<Relationships><Relationship Id='r1' Type='" + rel + "/officeDocument' Target='xl/workbook.xml'/></Relationships>");
                OfficeEntry(zip, "xl/workbook.xml", "<workbook xmlns:r='" + rel + "'><sheets><sheet name='Data' r:id='s1'/></sheets></workbook>");
                OfficeEntry(zip, "xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id='s1' Type='" + rel + "/worksheet' Target='worksheets/sheet1.xml'/><Relationship Id='sty' Type='" + rel + "/styles' Target='styles.xml'/></Relationships>");
                OfficeEntry(zip, "xl/styles.xml", "<styleSheet><numFmts><numFmt numFmtId='164' formatCode='yyyy-mm-dd'/><numFmt numFmtId='165' formatCode='0;-0;'/></numFmts><cellXfs><xf numFmtId='0'/><xf numFmtId='164'/><xf numFmtId='165'/></cellXfs></styleSheet>");
                var rows = new StringBuilder("<worksheet><sheetData>");
                for (int i = 1; i <= 120; i++) rows.Append("<row r='" + i + "'><c r='A" + i + "' t='inlineStr'><is><t>FINAL FULL TEXT gjpq</t></is></c>" +
                    (i == 1 ? "<c r='B1' s='1'><v>46000</v></c><c r='C1' s='2'><f>0</f><v>0</v></c>" : "") + "</row>");
                OfficeEntry(zip, "xl/worksheets/sheet1.xml", rows + "</sheetData></worksheet>");
            }
            var size = new Size(1600, 1200); var view = new OfficeViewOptions();
            int expectedInk = 0;
            using (var first = OfficePreviewReader.TryLoad(path, size, CancellationToken.None, OfficePreviewPalette.Default, view))
            {
                Check(first != null && first.ContentHeight > size.Height, "formatted sheet has scrollable content");
                // Compare the final row's actual glyph pixels with an identical, fully visible row.
                expectedInk = CountDarkPixels(first.Image, new Rectangle(90, 140, 440, 35));
                view.ScrollY = first.ContentHeight - size.Height;
            }
            using (var last = OfficePreviewReader.TryLoad(path, size, CancellationToken.None, OfficePreviewPalette.Default, view))
            {
                last.Image.Save(Path.Combine(output, "excel-last-row.png"));
                using (var crop = last.Image.Clone(new Rectangle(0, 1000, 1000, 200), last.Image.PixelFormat))
                    crop.Save(Path.Combine(output, "excel-last-row-crop.png"));
                int bottomInk = CountDarkPixels(last.Image, new Rectangle(90, 1150, 440, 45));
                Check(expectedInk > 100 && Math.Abs(bottomInk - expectedInk) < expectedInk / 8,
                    "last row retains complete glyphs including descenders at maximum scroll (" + expectedInk + "/" + bottomInk + ")");
            }
            var packages = (System.Collections.IList)typeof(OfficePreviewReader).GetField("packages", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            bool formatted = false;
            foreach (object package in packages)
                foreach (System.Collections.DictionaryEntry sheet in Field<System.Collections.IDictionary>(package, "Sheets"))
                {
                    var values = Field<System.Collections.IDictionary>(sheet.Value, "Values");
                    if (values.Contains(1) && (string)values[1] == "2025-12-09")
                    { formatted = true; Check(!values.Contains(2) || (string)values[2] == "", "empty zero format never exposes the cached formula"); }
                }
            Check(formatted, "XLSX custom format is applied through the reader and cache");
        }

        private static int CountDarkPixels(Bitmap bitmap, Rectangle rectangle)
        {
            int count = 0;
            for (int y = rectangle.Top; y < rectangle.Bottom; y++)
                for (int x = rectangle.Left; x < rectangle.Right; x++)
                { Color p = bitmap.GetPixel(x, y); if (p.R < 120 && p.G < 120 && p.B < 120) count++; }
            return count;
        }

        private static void OrientedImageSizing()
        {
            int width = 400, height = 800, stride = width * 4;
            byte[] pixels = new byte[stride * height];
            for (int i = 0; i < pixels.Length; i += 4) { pixels[i + 2] = 240; pixels[i + 3] = 255; }
            var source = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, pixels, stride);
            foreach (ushort orientation in new ushort[] { 1, 3, 5, 6, 7, 8 })
            {
                var metadata = new BitmapMetadata("jpg"); metadata.SetQuery("/app1/ifd/{ushort=274}", orientation);
                var encoder = new JpegBitmapEncoder { QualityLevel = 90 };
                encoder.Frames.Add(BitmapFrame.Create(source, null, metadata, null));
                string path = Path.Combine(output, "orientation-" + orientation + ".jpg");
                using (var stream = File.Create(path)) encoder.Save(stream);
                int originalWidth, originalHeight; long bytes;
                using (var loaded = PreviewWindow.LoadImage(path, 720, 540, out originalWidth, out originalHeight, out bytes))
                {
                    bool rotated = orientation >= 5;
                    Check(loaded.Size == (rotated ? new Size(720, 360) : new Size(270, 540)), "EXIF " + orientation + " fills the correct display bounds");
                    Check(originalWidth == (rotated ? 800 : 400) && originalHeight == (rotated ? 400 : 800), "EXIF source dimensions match displayed orientation");
                }
            }
        }

        private static void StableDocumentZoom()
        {
            using (var window = new PreviewWindow(true))
            {
                Set(window, "mediaKind", MediaKind.Document); Set(window, "imageArea", new Rectangle(0, 0, 800, 600));
                Set(window, "zoom", 2f); Set(window, "preview", new Bitmap(1600, 1200));
                Call(window, "UpdateToolbar"); var label = Field<ToolStripLabel>(window, "zoomLabel");
                Check(label.Text == "100%", "document starts with expected zoom");
                Set(window, "detailDue", DateTime.UtcNow.AddHours(1));
                Set(window, "detailPage", Field<int>(window, "pageIndex"));
                Set(window, "detailSlide", Field<int>(window, "documentSlideIndex"));
                Set(window, "detailCancellation", new CancellationTokenSource());
                Set(window, "detailTask", Task.FromResult(new ImagePreviewResult { Image = new Bitmap(3200, 2400) }));
                Call(window, "CompleteImageDetail");
                Check(Field<Bitmap>(window, "preview").Width == 3200 && label.Text == "100%", "actual detail completion preserves document zoom percentage");
                Call(window, "ClearImage"); Check(Field<int>(window, "documentReferenceWidth") == 0, "new files reset the document zoom reference");
            }
        }

        private static void DisplayNameRefresh()
        {
            string directory = Path.Combine(output, "display-names"); Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "report.txt"); int builds = 0;
            var current = new FolderNameIndex(); current.Add("report.txt", path, true);
            using (var cache = new FolderLookupCache())
            {
                Func<FolderNameIndex> build = () => { builds++; return current; };
                Check(cache.Resolve(directory, "report.txt", build) == path, "extensions-visible name resolves");
                current = new FolderNameIndex(); current.Add("report", path, true);
                current.Add("unsupported", path, false); current.Add("same", path, true); current.Add("same", path + ".png", true);
                Check(cache.Resolve(directory, "report", build) == path && builds == 2, "hidden-extension name resolves immediately without a filesystem event");
                for (int i = 0; i < 30; i++) cache.Resolve(directory, "missing", build);
                Check(builds == 2, "repeated unknown names do not rescan the folder continuously");
                Check(cache.Resolve(directory, "same", build) == null && cache.Resolve(directory, "unsupported", build) == null,
                    "ambiguous and unsupported names remain unresolved");
            }
        }

        private static void Diagnostics()
        {
            string directory = Path.Combine(output, "logs-" + Guid.NewGuid().ToString("N"));
            try { throw new InvalidOperationException("Regression logging sample"); }
            catch (Exception error) { AppLog.Write("Regression diagnostic", error, directory); }
            string path = Path.Combine(directory, "desktop-hover.log"); string logged = File.ReadAllText(path);
            Check(logged.Contains(AppInfo.Version) && logged.Contains("Regression diagnostic") && logged.Contains("Diagnostics()") && logged.Contains("Regression logging sample"),
                "error log records version, context, exception and call stack");
            for (int i = 0; i < 4; i++)
            {
                File.WriteAllBytes(path, new byte[AppLog.MaximumBytes]);
                AppLog.Write("Rotation", new Exception("bounded"), directory);
            }
            Check(Directory.GetFiles(directory).Length == 3 && new FileInfo(path).Length < 2000, "log rotation retains only three bounded files");
            AppLog.Write("Invalid destination", new Exception(), path);
            Check(File.Exists(path), "logging failure cannot throw into application code");
            Check(FileVersionInfo.GetVersionInfo(Application.ExecutablePath).FileVersion == AppInfo.Version + ".0", "EXE contains a real file version");
            using (var settings = new SettingsWindow(new AppSettings { AnimationsEnabled = false }, false, delegate { }, null))
                Check(settings.Text.Contains(AppInfo.Version), "settings window exposes the application version");
        }
    }
}
