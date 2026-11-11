using System;
using System.Collections;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static partial class Regression
    {
        private static void PreviewFixes152()
        {
            string dir = Path.Combine(output, "name-collision");
            Directory.CreateDirectory(dir);
            bool extensions = true;
            int builds = 0;
            Func<FolderNameIndex> build = delegate
            {
                builds++;
                var names = new FolderNameIndex();
                names.AddItem(extensions ? "report.txt" : "report", Path.Combine(dir, "report.txt"), true);
                names.AddItem(extensions ? "report.txt.png" : "report.txt", Path.Combine(dir, "report.txt.png"), true);
                return names;
            };
            using (var cache = new FolderLookupCache())
            {
                Check(cache.Resolve(dir, "report.txt", build, extensions) == Path.Combine(dir, "report.txt"), "visible TXT resolves");
                extensions = false;
                Check(cache.Resolve(dir, "report.txt", build, extensions) == Path.Combine(dir, "report.txt.png"), "existing name now resolves to the PNG after hiding extensions");
                Check(cache.Resolve(dir, "report", build, extensions) == Path.Combine(dir, "report.txt") && builds == 2, "unchanged display mode reuses the refreshed index");
                extensions = true;
                Check(cache.Resolve(dir, "report.txt", build, extensions) == Path.Combine(dir, "report.txt") && builds == 3, "showing extensions again restores the TXT mapping");
            }
            CheckOfficeLayout152();
            CheckSettings152();
            string bad = Path.Combine(output, "undecodable.png");
            File.WriteAllText(bad, "invalid image");
            using (var result = ImagePreviewLoader.Load(bad, new Size(720, 540), CancellationToken.None))
                Check(result.ErrorKey == "image_unavailable", "decode failure retains its user-facing message");
            using (var player = new MediaPlayback(IntPtr.Zero))
                player.Open(Path.Combine(output, "missing-test-clip.mp4"));
            string log = File.ReadAllText(Path.Combine(AppLog.DirectoryPath, AppLog.FileName));
            Check(log.Contains("Image preview (.png)") && log.Contains("Media playback (.mp4)") && log.Contains("FileNotFoundException"),
                "the log button's file contains both original decode and media errors");
        }

        private static void CheckOfficeLayout152()
        {
            const string rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var size = new Size(800, 450);
            foreach (string flag in new[] { "1", "true" })
            {
                string path = Path.Combine(output, "filtered-" + flag + ".xlsx");
                using (var s = File.Create(path))
                using (var z = new ZipArchive(s, ZipArchiveMode.Create))
                {
                    OfficeEntry(z, "_rels/.rels", "<Relationships><Relationship Id='d' Type='" + rel + "/officeDocument' Target='xl/workbook.xml'/></Relationships>");
                    OfficeEntry(z, "xl/workbook.xml", "<workbook xmlns:r='" + rel + "'><workbookPr date1904='" + flag + "'/><sheets><sheet name='Data' r:id='s'/></sheets></workbook>");
                    OfficeEntry(z, "xl/_rels/workbook.xml.rels", "<Relationships><Relationship Id='s' Type='" + rel + "/worksheet' Target='sheet.xml'/><Relationship Id='t' Type='" + rel + "/styles' Target='styles.xml'/></Relationships>");
                    OfficeEntry(z, "xl/styles.xml", "<styleSheet><cellXfs><xf numFmtId='14'/></cellXfs></styleSheet>");
                    var rows = new StringBuilder("<worksheet><sheetData><row r='1'><c r='A1'><v>1</v></c></row>");
                    for (int i = 2; i < 100; i++) rows.Append("<row r='" + i + "' hidden='" + flag + "'><c t='inlineStr'><is><t>hidden</t></is></c></row>");
                    OfficeEntry(z, "xl/sheet.xml", rows + "<row r='100'><c t='inlineStr'><is><t>last</t></is></c></row></sheetData></worksheet>");
                }
                foreach (var view in new[] { null, new OfficeViewOptions() })
                using (var result = OfficePreviewReader.TryLoad(path, size, CancellationToken.None, OfficePreviewPalette.Default, view))
                    Check(result != null && result.ContentHeight == size.Height, "filtered sheet fits the viewport (" + flag + ")");
                var packages = (IList)typeof(OfficePreviewReader).GetField("packages", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                int checkedViews = 0;
                foreach (object package in packages)
                    if (Field<string>(package, "Path") == path)
                        foreach (DictionaryEntry pair in Field<IDictionary>(package, "Sheets"))
                        {
                            var values = Field<IDictionary>(pair.Value, "Values");
                            int[] visible = Field<int[]>(pair.Value, "VisibleRows");
                            Check(visible.Length == 2 && visible[0] == 1 && visible[1] == 100 && !values.Contains(16384),
                                "row labels, hidden text and hover limit use visible rows");
                            Check((string)values[0] == "1904-01-02", "saved date system applies (" + flag + ")");
                            checkedViews++;
                        }
                Check(checkedViews == 2, "both hover and pinned render paths validated");
            }
            string word = Path.Combine(output, "long-table.docx");
            using (var s = File.Create(word))
            using (var z = new ZipArchive(s, ZipArchiveMode.Create))
            {
                OfficeEntry(z, "_rels/.rels", "<Relationships><Relationship Id='d' Type='" + rel + "/officeDocument' Target='word/document.xml'/></Relationships>");
                var sentence = new StringBuilder();
                for (int i = 0; i < 160; i++) sentence.Append("Readable table content. ");
                OfficeEntry(z, "word/document.xml", "<document><body><tbl><tr><tc><p><r><t>" + sentence +
                    "END OF TABLE</t></r></p></tc></tr></tbl></body></document>");
            }
            var scroll = new OfficeViewOptions();
            using (var first = OfficePreviewReader.TryLoad(word, size, CancellationToken.None, OfficePreviewPalette.Default, scroll))
            {
                Check(first.ContentHeight > size.Height * 2 && first.DetailKey == null, "long Word cell expands and can be scrolled to its end");
                scroll.ScrollY = first.ContentHeight - size.Height;
            }
            using (var last = OfficePreviewReader.TryLoad(word, size, CancellationToken.None, OfficePreviewPalette.Default, scroll))
            {
                Check(CountDarkPixels(last.Image, new Rectangle(40, size.Height - 100, size.Width - 80, 65)) > 30, "Word table has readable glyphs at maximum scroll");
                last.Image.Save(Path.Combine(output, "word-table-bottom.png"));
            }
        }

        private static void CheckSettings152()
        {
            var settings = new AppSettings { AnimationsEnabled = false };
            using (var window = new SettingsWindow(settings, false, changed => settings = changed, null))
            {
                window.FitToArea(new Rectangle(0, 0, 1920, 1080), 1);
                Call(window, "SetUiScale", 2f);
                window.Size = window.MinimumSize;
                Call(window, "ArrangeSettings");
                var content = window.SettingsViewport;
                foreach (Control child in content.Controls)
                    if (child.Right > content.ClientSize.Width) throw new Exception("DPI overflow: " + child.Name);
                Check(window.MinimumSize.Width > 520, "minimum size follows monitor scale and all controls fit");
                window.FitToArea(Screen.PrimaryScreen.WorkingArea, 1);
                window.Show(); Pump(80);
                Rectangle work = Screen.FromControl(window).WorkingArea;
                IntPtr rect = System.Runtime.InteropServices.Marshal.AllocHGlobal(4 * sizeof(int));
                try
                {
                    int[] bounds = { work.Left, work.Top, work.Right, work.Bottom };
                    System.Runtime.InteropServices.Marshal.Copy(bounds, 0, rect, 4);
                    object[] message = { Message.Create(window.Handle, 0x02E0, new IntPtr(192 | (192 << 16)), rect) };
                    Call(window, "WndProc", message);
                    Check(Field<float>(window, "uiScale") == 2 && window.MinimumSize.Width == Math.Min(1040, work.Width - 32),
                        "the native DPI-change message updates minimum bounds for the target monitor");
                }
                finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(rect); }
                window.FitToArea(work, 1);
                Field<HoverButton>(window, "toggleShortcutButton").PerformClick();
                var menu = Field<ContextMenuStrip>(window, "shortcutMenu");
                var modifiers = (ShortcutField)menu.Items["modifiers"];
                var key = (ShortcutField)menu.Items["key"];
                Check(menu.ClientRectangle.Contains(modifiers.Bounds) && menu.ClientRectangle.Contains(key.Bounds), "both shortcut fields fit inside the popup");
                modifiers.ShowDropDown(); modifiers.DropDownItems["Ctrl+Shift"].PerformClick();
                key.ShowDropDown();
                var lastKey = key.DropDownItems["F11"]; lastKey.Select();
                Check(key.DropDown.ClientRectangle.Contains(lastKey.Bounds), "the last function key is reachable in the list");
                key.DropDownItems["F8"].PerformClick();
                Check(menu.Visible && !key.DropDown.Visible, "shortcut editor stays open after both choices");
                Check(key.Value == Keys.F8 && settings.ToggleShortcut != (modifiers.Value | key.Value), "shortcut options wait for Save");
                menu.Items["save-shortcut"].PerformClick();
                Check(!menu.Visible, "saving closes the shortcut editor");
                menu.Close();
                Check(settings.ToggleShortcut == (Keys.Control | Keys.Shift | Keys.F8) &&
                    Field<HoverButton>(window, "toggleShortcutButton").Text.Contains("Ctrl+Shift+F8"), "shortcut picker applies and displays the selected combination");
                window.Close();
            }
            string prefs = Path.Combine(output, "custom-keys.ini");
            settings.PinShortcut = Keys.Control | Keys.Alt | Keys.Shift | Keys.F7;
            settings.Save(prefs);
            Check(AppSettings.Load(prefs).ToggleShortcut == settings.ToggleShortcut && AppSettings.Load(prefs).PinShortcut == settings.PinShortcut, "both shortcuts survive restart");
            using (var active = new AppHotkeys(delegate { }, delegate { }, settings))
            {
                Check(active.ToggleRegistered && active.PinRegistered, "saved shortcuts register with Windows");
                var changed = settings.Clone();
                changed.ToggleShortcut = settings.PinShortcut;
                bool saved = false;
                try { active.Apply(changed, () => saved = true); } catch (InvalidOperationException) { }
                Check(!saved, "duplicate chords cannot overwrite settings");
                changed.ToggleShortcut = Keys.Alt | Keys.Shift | Keys.F9;
                try { active.Apply(changed, delegate { throw new IOException("test save failure"); }); } catch (IOException) { }
                using (var other = new AppHotkeys(delegate { }, delegate { }, changed))
                {
                    Check(other.ToggleRegistered && !other.PinRegistered, "failed save releases the new chord and retains the old pin");
                    try { active.Apply(changed, () => saved = true); } catch (InvalidOperationException) { }
                    Check(!saved, "a chord in use by another window cannot replace a working shortcut");
                }
                active.Apply(changed, () => saved = true);
                Check(saved && active.ToggleRegistered && active.PinRegistered, "a free chord can be registered immediately after a conflict");
            }
        }
    }
}
