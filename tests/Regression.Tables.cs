using System;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static partial class Regression
    {
        private static void TabularPreviewData()
        {
            string path = Path.Combine(output, "table.csv");
            File.WriteAllText(path, "name,description,price\r\n\"A, B\",\"line 1\r\nline \"\"2\"\"\",\r\n");
            var text = TextPreviewContent.Load(path);
            Check(text.RowCount == 2 && text.TableColumns == 3 && text.TableRows[1][0] == "A, B" &&
                text.TableRows[1][1] == "line 1 line \"2\"" && text.TableRows[1][2] == "", "CSV quoted delimiters, multiline fields, escaped quotes and empty cells");
            File.WriteAllText(path, "Name;Price\nRīga;4,25\n");
            text = TextPreviewContent.Load(path);
            Check(text.TableRows[1][1] == "4,25", "semicolon CSV keeps decimal commas");
            File.WriteAllText(path, "sep=;\r\n\"a;b\";c");
            text = TextPreviewContent.Load(path);
            Check(text.RowCount == 1 && text.TableRows[0][0] == "a;b", "Excel separator declaration");
            File.WriteAllText(path, "");
            Check(TextPreviewContent.Load(path).RowCount == 0, "empty spreadsheet text file");
            string tsv = Path.Combine(output, "table.tsv");
            File.WriteAllText(tsv, "first\t\tlast\n\"a\tb\"\t24", Encoding.Unicode);
            text = TextPreviewContent.Load(tsv);
            Check(text.TableColumns == 3 && text.TableRows[0][1] == "" && text.TableRows[1][1] == "24" &&
                text.EncodingName == "UTF-16 LE", "TSV preserves quoted tabs in one field and uses the detected encoding");
            var large = new StringBuilder();
            for (int i = 0; i < 305; i++) large.Append(new string(',', 130)).Append("\r\n");
            File.WriteAllText(path, large.ToString());
            text = TextPreviewContent.Load(path);
            Check(text.Truncated && text.RowCount == 300 && text.TableColumns == 128, "large tables stay inside preview limits");

            var size = new Size(600, 360);
            string word = Path.Combine(output, "short-word.docx");
            using (var file = File.Create(word))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                OfficeEntry(zip, "word/document.xml", "<document><body><p><r><t>Visible paragraph</t></r></p></body></document>");
            using (var first = OfficePreviewReader.TryLoad(word, size, CancellationToken.None, OfficePreviewPalette.Default, new OfficeViewOptions()))
            using (var after = OfficePreviewReader.TryLoad(word, size, CancellationToken.None, OfficePreviewPalette.Default, new OfficeViewOptions { ScrollY = 2000 }))
            {
                int ink = CountDarkPixels(first.Image, new Rectangle(Point.Empty, size));
                Check(ink > 0 && CountDarkPixels(after.Image, new Rectangle(Point.Empty, size)) == ink, "outdated Word scroll offset paints the available paragraph");
            }
            foreach (bool explicitWidth in new[] { false, true })
            {
                string sheet = Path.Combine(output, "hidden-columns-" + explicitWidth + ".xlsx");
                WriteOfficeFixture(sheet, true);
                using (var file = File.Open(sheet, FileMode.Open, FileAccess.ReadWrite))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Update))
                {
                    zip.GetEntry("xl/worksheets/sheet1.xml").Delete();
                    OfficeEntry(zip, "xl/worksheets/sheet1.xml", "<worksheet><cols><col min='1' max='8' hidden='1' " +
                        (explicitWidth ? "width='8.43'" : "") + "/></cols><sheetData><row><c r='A1' t='inlineStr'><is><t>Hidden</t></is></c>" +
                        "<c r='I1' t='inlineStr'><is><t>Visible</t></is></c></row></sheetData></worksheet>");
                }
                foreach (var view in new[] { null, new OfficeViewOptions() })
                using (var result = OfficePreviewReader.TryLoad(sheet, size, CancellationToken.None, OfficePreviewPalette.Default, view))
                {
                    Check(result != null && CountDarkPixels(result.Image, new Rectangle(45, 50, 105, 22)) > 4,
                        "first visible Excel column contains data after eight hidden columns");
                    var packages = (System.Collections.IList)typeof(OfficePreviewReader).GetField("packages",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).GetValue(null);
                    foreach (object package in packages)
                        if (Field<string>(package, "Path") == sheet)
                        {
                            var sheets = Field<System.Collections.IDictionary>(package, "Sheets");
                            object content = sheets["xl/worksheets/sheet1.xml|" + (view == null ? "hover" : "pinned")];
                            var values = Field<System.Collections.IDictionary>(content, "Values");
                            Check(!values.Contains(0) && (string)values[8] == "Visible" && Field<int[]>(content, "VisibleColumns")[0] == 8,
                                "hidden Excel cells and headers are excluded with or without explicit width");
                        }
                }
            }
        }

        private static void TableWindowAndHotkeys()
        {
            string path = Path.Combine(output, "wide.csv");
            var records = new StringBuilder();
            for (int row = 0; row < 80; row++)
                records.AppendLine("first,b,c,d,e,f,g,h,i,j,k,l,m,last");
            File.WriteAllText(path, records.ToString());
            foreach (bool pinned in new[] { false, true })
            using (var window = new PreviewWindow(pinned))
            {
                window.Configure(new AppSettings { Theme = "dark", AnimationsEnabled = false });
                Check(window.ShowFile(path, new Point(100, 100), Rectangle.Empty), "CSV opens through the normal preview path");
                Until(() => Field<TextPreviewContent>(window, "textPreview") != null, 5000);
                foreach (string theme in new[] { "dark", "light", "glass" })
                {
                    window.Configure(new AppSettings { Theme = theme, AnimationsEnabled = false });
                    if (pinned) Snapshot(Field<ToolStrip>(window, "toolbar"), "pinned-toolbar-" + theme + ".png");
                    using (var bitmap = new Bitmap(window.Width, window.Height))
                    {
                        window.DrawToBitmap(bitmap, window.ClientRectangle);
                        Rectangle area = Field<Rectangle>(window, "imageArea");
                        Check(bitmap.GetPixel(area.Left + 5, area.Top + 4).ToArgb() == Brand.SelectedSurface.ToArgb(),
                            "table header uses the " + theme + " palette");
                    }
                }
                if (pinned)
                {
                    Call(window, "Navigate", 1); Call(window, "ScrollText", 999);
                    Check(Field<int>(window, "tableColumn") > 0 && Field<int>(window, "textOffset") > 0, "pinned table reaches later columns and rows");
                    File.WriteAllText(path, "short,value");
                    Call(window, "RefreshPinned");
                    Until(() => Field<TextPreviewContent>(window, "textPreview") != null && Field<TextPreviewContent>(window, "textPreview").RowCount == 1, 5000);
                    Check(Field<int>(window, "tableColumn") == 0 && Field<int>(window, "textOffset") == 0, "refresh clamps both table offsets");
                }
            }
            var settings = new AppSettings { ToggleShortcut = Keys.Control | Keys.Alt | Keys.Shift | Keys.F8,
                PinShortcut = Keys.Control | Keys.Alt | Keys.Shift | Keys.F9 };
            using (var first = new AppHotkeys(delegate { }, delegate { }, settings))
            using (var second = new AppHotkeys(delegate { }, delegate { }, settings))
            {
                Check(first.ToggleRegistered && first.PinRegistered && !second.ToggleRegistered && !second.PinRegistered, "startup shortcut conflict is detected");
                bool saved = false;
                second.Apply(settings, () => saved = true);
                Check(saved && !second.ToggleRegistered, "saving other settings succeeds during conflict");
                first.Dispose();
                second.Apply(settings, delegate { });
                Check(second.ToggleRegistered && second.PinRegistered, "resaving unchanged shortcuts recovers after release");
            }
            CheckShorterPinnedWord();
            using (var context = new HoverContext())
            {
                Field<System.Windows.Forms.Timer>(context, "timer").Stop();
                Field<AppSettings>(context, "settings").ShowWindowsInfoTips = true;
                typeof(HoverContext).GetField("paused", Private).SetValue(context, true);
                PreviewWindow preview = Field<PreviewWindow>(context, "preview");
                preview.ShowFile(path, new Point(100, 100), Rectangle.Empty);
                Call(context, "Tick", null, EventArgs.Empty);
                int generation = Field<int>(context, "generation"), revision = Field<int>(preview, "pathRevision");
                for (int i = 0; i < 20; i++) Call(context, "Tick", null, EventArgs.Empty);
                Check(preview.CurrentPath == null && Field<bool>(context, "hoverSuspended") &&
                    generation == Field<int>(context, "generation") && revision == Field<int>(preview, "pathRevision"),
                    "pause clears the active preview once across repeated timer ticks");
                context.ExitThread();
            }
            using (var window = new SettingsWindow(new AppSettings { Theme = "dark", AnimationsEnabled = false }, false, null, null))
            {
                window.Show();
                foreach (string theme in new[] { "light", "glass", "dark" })
                {
                    window.UpdateSettings(new AppSettings { Theme = theme, AnimationsEnabled = false });
                    Call(window, "ShowShortcutMenu", false);
                    var menu = Field<ContextMenuStrip>(window, "shortcutMenu");
                    var modifiers = (ShortcutField)menu.Items["modifiers"];
                    modifiers.ShowDropDown();
                    Check(modifiers.DropDown.Visible && modifiers.DropDown.BackColor == Brand.Surface &&
                        ((ToolStripMenuItem)modifiers.DropDownItems[Shortcut.Format(modifiers.Value)]).Checked &&
                        Field<ToolTip>(window, "windowToolTip").OwnerDraw, "shortcut editor presents the checked option in the " + theme + " theme");
                    Snapshot(menu, "shortcut-editor-" + theme + ".png");
                    Snapshot(modifiers.DropDown, "shortcut-choices-" + theme + ".png");
                    modifiers.HideDropDown();
                    var keys = (ShortcutField)menu.Items["key"];
                    keys.ShowDropDown();
                    Snapshot(keys.DropDown, "shortcut-keys-" + theme + ".png");
                    menu.Close();
                }
                if (!SystemInformation.HighContrast)
                using (var timer = new System.Windows.Forms.Timer { Interval = 50 })
                {
                    bool themed = false;
                    timer.Tick += delegate
                    {
                        foreach (Form form in Application.OpenForms)
                            if (form.Text == "Notice test")
                            {
                                themed = form.BackColor == Brand.Background && form.AcceptButton is HoverButton && form.CancelButton == form.AcceptButton;
                                timer.Stop(); form.Close(); break;
                            }
                    };
                    timer.Start(); AppNotice.Show(window, "Preview information", "Notice test", MessageBoxIcon.Information);
                    Check(themed, "themed notice keeps standard keyboard dismissal");
                }
            }
        }

        private static void CheckShorterPinnedWord()
        {
            string path = Path.Combine(output, "shrinking.docx");
            var paragraphs = new StringBuilder();
            for (int i = 0; i < 160; i++) paragraphs.Append("<p><r><t>Paragraph ").Append(i).Append("</t></r></p>");
            Action<string> write = delegate(string body)
            {
                using (var file = File.Create(path))
                using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
                    OfficeEntry(zip, "word/document.xml", "<document><body>" + body + "</body></document>");
            };
            write(paragraphs.ToString());
            using (var window = new PreviewWindow(true))
            {
                window.Configure(new AppSettings { Theme = "light", AnimationsEnabled = false });
                window.ShowFile(path, new Point(100, 100), Rectangle.Empty);
                Until(() => Field<int>(window, "officeContentHeight") > 2000, 5000);
                Field<VScrollBar>(window, "officeScroll").Value = 1500;
                Pump(300);
                write("<p><r><t>Only one paragraph remains</t></r></p>");
                Call(window, "RefreshPinned");
                Until(() => window.HasImage && Field<int>(window, "officeContentHeight") == Field<int>(window, "officePageHeight") &&
                    Field<int>(window, "officePageHeight") > 0, 5000);
                Bitmap image = Field<Bitmap>(window, "preview");
                Check(Field<OfficeViewOptions>(window, "officeView").ScrollY == 0 && CountDarkPixels(image, new Rectangle(Point.Empty, image.Size)) > 0,
                    "refresh of a shortened pinned Word document shows its text at the corrected offset");
            }
        }
    }
}
