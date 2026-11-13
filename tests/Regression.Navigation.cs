using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media.Imaging;

namespace DesktopHover
{
    internal static partial class Regression
    {
        private static void PreviewNavigation()
        {
            SharedStringWorkbooks();
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { AnimationsEnabled = false, Language = "lv" });
                DelimiterAndScrolling(window);
                PagedImages(window);
                string pdf = Path.Combine(output, "jump.pdf");
                WritePdf(pdf);
                window.ShowFile(pdf, new Point(120, 120), Rectangle.Empty);
                Until(() => !Field<bool>(window, "documentLoading") && window.HasImage, 16000);
                JumpTo(window, 2);
                Until(() => !Field<bool>(window, "documentLoading") && window.HasImage, 16000);
                Check(Field<Bitmap>(window, "preview").GetPixel(80, 80).B > 240, "the PDF page picker displays page two");
            }
        }

        private static void SharedStringWorkbooks()
        {
            string path = Path.Combine(output, "shared-values.xlsx");
            const string rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            using (var file = File.Create(path))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                OfficeEntry(zip, "xl/workbook.xml", "<workbook xmlns:r='" + rel + "'><sheets><sheet name='Numbers' r:id='a'/>" +
                    "<sheet name='Words' r:id='b'/></sheets></workbook>");
                OfficeEntry(zip, "xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id='a' Type='" + rel +
                    "/worksheet' Target='numbers.xml'/><Relationship Id='b' Type='" + rel + "/worksheet' Target='words.xml'/>" +
                    "<Relationship Id='s' Type='" + rel + "/sharedStrings' Target='strings.xml'/></Relationships>");
                OfficeEntry(zip, "xl/numbers.xml", "<worksheet><sheetData><row><c><v>42</v></c><c t='b'><f>A1&gt;1</f><v/></c>" +
                    "<c t='b'><f>A1&gt;1</f><v>1</v></c><c t='b'><v>0</v></c><c t='b'/></row></sheetData></worksheet>");
                OfficeEntry(zip, "xl/words.xml", "<worksheet><sheetData><row><c t='s'><v>50001</v></c><c t='s'><v>0</v></c>" +
                    "<c t='s'><v>50001</v></c><c t='s'><v>1</v></c></row></sheetData></worksheet>");
                var table = new StringBuilder("<sst><si><t>first</t></si><si/>");
                for (int i = 2; i < 50001; i++) table.Append("<si><t>").Append(i).Append("</t></si>");
                table.Append("<si><r><t>last </t></r><rPh><t>phonetics</t></rPh><r><t>word</t></r></si></sst>");
                OfficeEntry(zip, "xl/strings.xml", table.ToString());
            }
            foreach (string name in new[] { "Numbers", "Words" })
            using (var result = OfficePreviewReader.TryLoad(path, new Size(800, 500), CancellationToken.None,
                OfficePreviewPalette.Default, new OfficeViewOptions { SheetName = name }))
            {
                Check(result != null && result.Image != null, "renders " + name + " with a large shared string table");
                var packages = (IList)typeof(OfficePreviewReader).GetField("packages", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                object package = packages[packages.Count - 1];
                Check(!Field<IDictionary>(package, "Documents").Contains("xl/strings.xml"), "shared text does not require an XML document tree");
                object sheet = Field<IDictionary>(package, "Sheets")["xl/" + name.ToLowerInvariant() + ".xml|pinned"];
                var values = Field<Dictionary<int, string>>(sheet, "Values");
                if (name == "Numbers")
                {
                    Check(!Field<IDictionary>(package, "Parts").Contains("xl/strings.xml"), "numeric worksheet skips the shared string part");
                    Check(values[0] == "42" && values[1] == "=A1>1" && values[2] == "TRUE" && values[3] == "FALSE" && values[4] == "",
                        "empty, cached and uncached Boolean cells retain distinct values");
                }
                else Check(values[0] == "last word" && values[1] == "first" && values[2] == values[0] && values[3] == "",
                    "high indices, duplicate references, empty items and rich text resolve correctly");
            }
        }

        private static void DelimiterAndScrolling(PreviewWindow window)
        {
            string csv = Path.Combine(output, "decimals.csv");
            File.WriteAllText(csv, "1,50;2,75\n3,40;4,85");
            window.ShowFile(csv, new Point(100, 100), Rectangle.Empty);
            Until(() => Field<TextPreviewContent>(window, "textPreview") != null, 5000);
            var menu = Field<ToolStripDropDownButton>(window, "csvButton");
            Check(menu.Visible && menu.Text == "Atdalītājs" && Field<TextPreviewContent>(window, "textPreview").TableRows[0][0] == "1,50",
                "a headerless semicolon CSV with decimal commas opens correctly");
            menu.DropDownItems["csv_comma"].PerformClick();
            Until(() => Field<TextPreviewContent>(window, "textPreview") != null && Field<TextPreviewContent>(window, "textPreview").TableColumns == 3, 5000);
            RefreshAndWait(window);
            Check(Field<TextPreviewContent>(window, "textPreview").TableRows[0][1] == "50;2" && ((ToolStripMenuItem)menu.DropDownItems["csv_comma"]).Checked,
                "manual comma delimiter survives refresh");
            string other = Path.Combine(output, "declared.csv");
            File.WriteAllText(other, "sep=;\r\na;b\nc;d");
            window.ShowFile(other, new Point(100, 100), Rectangle.Empty);
            Until(() => Field<TextPreviewContent>(window, "textPreview") != null, 5000);
            Check(((ToolStripMenuItem)menu.DropDownItems["csv_auto"]).Checked && Field<TextPreviewContent>(window, "textPreview").TableColumns == 2,
                "opening another CSV resets its delimiter and reads the Excel declaration");
            Check(TextPreviewContent.Load(other, CancellationToken.None, ',').TableRows[0][0] == "a;b",
                "overriding a declaration still skips its metadata record");
            foreach (string ext in new[] { ".txt", ".cs", ".md" })
            {
                string path = Path.Combine(output, "scroll" + ext);
                var lines = new StringBuilder();
                for (int i = 0; i < 120; i++) lines.AppendLine("Row " + i);
                File.WriteAllText(path, lines.ToString());
                window.ShowFile(path, new Point(100, 100), Rectangle.Empty);
                Until(() => Field<TextPreviewContent>(window, "textPreview") != null, 5000);
                int visible = Field<int>(window, "textVisibleLines");
                PreviewKey(window, Keys.PageDown);
                Check(Field<int>(window, "textOffset") == visible, ext + " scrolls a screen");
                PreviewKey(window, Keys.PageUp); PreviewKey(window, Keys.Down);
                Check(Field<int>(window, "textOffset") == 1 && !menu.Visible, ext + " scrolls a single row");
            }
        }

        private static void PreviewKey(PreviewWindow window, Keys key)
        {
            Call(window, "ProcessCmdKey", new Message(), key);
        }

        private static void JumpTo(PreviewWindow window, int page)
        {
            PreviewKey(window, Keys.Control | Keys.G);
            var input = Field<TextBox>(window, "pageNumber");
            Check(input.Visible && input.Focused, "Ctrl+G focuses the page number");
            input.Text = page.ToString();
            var enter = Message.Create(input.Handle, 0x100, (IntPtr)Keys.Enter, IntPtr.Zero);
            if (!input.PreProcessMessage(ref enter)) SendKeyMessage(input.Handle, enter.Msg, enter.WParam, enter.LParam);
            Check(!Field<ToolStripControlHost>(window, "pageInput").Available,
                "the focused input submits a valid page on Enter (" + page + "/" + Property<int>(window, "AvailablePages") +
                ", modifiers " + Control.ModifierKeys + ", input " + input.Text + ")");
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
        private static extern IntPtr SendKeyMessage(IntPtr window, int message, IntPtr key, IntPtr flags);

        private static void WriteTiffPages(string path, int pages)
        {
            var encoder = new TiffBitmapEncoder { Compression = TiffCompressOption.Lzw };
            var colors = new[] { System.Windows.Media.Colors.Red, System.Windows.Media.Colors.Blue, System.Windows.Media.Colors.Green };
            for (int i = 0; i < pages; i++)
            {
                int width = 2400 + i * 20, height = 600;
                var palette = new BitmapPalette(new[] { colors[i] });
                var frame = BitmapSource.Create(width, height, 96, 96, System.Windows.Media.PixelFormats.Indexed8,
                    palette, new byte[width * height], width);
                encoder.Frames.Add(BitmapFrame.Create(frame));
            }
            using (var stream = File.Create(path)) encoder.Save(stream);
        }

        private static void PagedImages(PreviewWindow window)
        {
            string path = Path.Combine(output, "three-pages.tiff");
            WriteTiffPages(path, 3);
            using (var hover = new PreviewWindow())
            {
                hover.ShowFile(path, new Point(100, 100), Rectangle.Empty);
                Until(() => hover.HasImage, 5000);
                Check(Field<Bitmap>(hover, "preview").GetPixel(0, 0).R == 255, "TIFF hover shows its first page");
            }
            window.ShowFile(path, new Point(100, 100), Rectangle.Empty);
            Until(() => window.HasImage, 5000);
            var counter = Field<ToolStripLabel>(window, "pageLabel");
            Check(counter.Text == "1 / 3", "TIFF counter shows all pages");
            counter.PerformClick();
            var input = Field<TextBox>(window, "pageNumber");
            input.Text = "4"; PreviewKey(window, Keys.Enter);
            Check(input.Visible && Field<int>(window, "pageIndex") == 0, "an out-of-range page keeps editing");
            PreviewKey(window, Keys.Escape);
            Check(!input.Visible && window.Visible, "Escape dismisses the input without closing the pinned window");
            JumpTo(window, 2);
            Until(() => window.HasImage && !window.IsImageLoading, 5000);
            Check(counter.Text == "2 / 3" && Field<Bitmap>(window, "preview").GetPixel(0, 0).B == 255, "TIFF selects the second frame");
            Call(window, "ChangeZoom", 8f);
            Until(() => Field<Bitmap>(window, "preview").Width == 2420, 8000);
            Check(Field<Bitmap>(window, "preview").GetPixel(0, 0).B == 255, "zoom decodes detail from the selected frame");
            RefreshAndWait(window);
            Check(counter.Text == "2 / 3" && Field<float>(window, "zoom") == 8 && Field<Bitmap>(window, "preview").GetPixel(0, 0).B == 255,
                "TIFF refresh retains frame and zoom");
            Field<ToolStripButton>(window, "nextButton").PerformClick();
            Until(() => window.HasImage && !window.IsImageLoading, 5000);
            Check(counter.Text == "3 / 3" && Field<Bitmap>(window, "preview").GetPixel(0, 0).G > 100, "next shows the third frame");
            Field<ToolStripButton>(window, "previousButton").PerformClick();
            Until(() => window.HasImage && !window.IsImageLoading, 5000);
            Check(Field<Bitmap>(window, "preview").GetPixel(0, 0).B == 255, "previous retrieves the correct cached frame");
            WriteTiffPages(path, 1); RefreshAndWait(window);
            Check(!counter.Visible && Field<int>(window, "pageIndex") == 0 && Field<Bitmap>(window, "preview").GetPixel(0, 0).R == 255,
                "refresh clamps a removed TIFF frame and hides one-page navigation");
        }
    }
}
