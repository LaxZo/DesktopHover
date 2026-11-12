using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static partial class Regression
    {
        private static void NewFormats()
        {
            var at = new Point(120,120);
            foreach (string ext in new[] { ".zip", ".edoc", ".asice" })
            {
                string file = Path.Combine(output,"sample"+ext);
                using (var stream = File.Create(file))
                using (var zip = new ZipArchive(stream,ZipArchiveMode.Create))
                {
                    OfficeEntry(zip,"mimetype","container"); zip.CreateEntry("docs/");
                    OfficeEntry(zip,"docs/līgums.txt","abc"); OfficeEntry(zip,"note.pdf","1234");
                    OfficeEntry(zip,"META-INF/signatures.xml","signed");
                }
                var listing = TextPreviewContent.Load(file);
                Check(listing.ArchiveFileCount == (ext == ".zip" ? 4 : 2) && listing.TableRows.Any(r => r[0] == "docs/līgums.txt"),
                    ext + " counts payloads and retains Unicode paths");
                Check(listing.TableRows.Any(r => r[0] == "docs/") == (ext == ".zip") && listing.TableRows.Any(r => r[0].StartsWith("META-INF")) == (ext == ".zip"),
                    ext + " folder and signature metadata presentation");
                using (var hover = new PreviewWindow(false))
                {
                    Check(hover.ShowFile(file,at,Rectangle.Empty),"hover accepts " + ext);
                    Until(() => Field<TextPreviewContent>(hover,"textPreview") != null,5000);
                }
            }
            ExplorerZipPreview(at);
            string md = Path.Combine(output,"headings.md");
            File.WriteAllText(md,"# Virsraksts\n- Punkts\n~~~cs\n# literal\n~~~\n" + String.Concat(Enumerable.Repeat("Long paragraph. ",25)));
            var markdown = TextPreviewContent.Load(md);
            Check(markdown.IsMarkdown && markdown.Lines.Length == 4 && markdown.Lines[0].Heading == 1 &&
                markdown.Lines[1].Text == "• Punkts" && markdown.Lines[2].CodeBlock && markdown.Lines[2].Text == "# literal","Markdown blocks and fenced code");
            string svg = Path.Combine(output,"vector.svg");
            File.WriteAllText(svg,"<svg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 50'><defs><circle id='dot' cx='25' cy='25' r='12'/></defs><g fill='#df1255' transform='translate(25 0)'><use href='#dot'/></g></svg>");
            using (var img = Task.Run(() => ImagePreviewLoader.Load(svg,new Size(200,140),CancellationToken.None)).Result)
            {
                Check(img.Image != null,"SVG renders on the background image worker");
                Check(img.Width == 100 && img.Height == 50 && img.Image.Size == new Size(200,100) &&
                    img.Image.GetPixel(100,50).R > 205 && img.Image.GetPixel(5,5).A == 0,"SVG viewBox, transform, reference, fill and transparency");
            }
            string fonts = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            string ttf = Path.Combine(fonts,"arial.ttf");
            foreach (string ext in new[] { ".ttf", ".otf" })
            {
                string path = Path.Combine(output,"sample-font"+ext);
                string otf = Environment.GetEnvironmentVariable("DESKTOPHOVER_TEST_OTF") ?? Directory.GetFiles(fonts,"*.otf").FirstOrDefault();
                File.Copy(ext == ".ttf" ? ttf : otf ?? ttf,path,true);
                using (var sample = Task.Run(() => DocumentPreviewContent.Load(path,new Size(650,320),CancellationToken.None)).Result)
                {
                    Check(sample.Image != null && sample.NoteKey == "font_sample","background font preview " + ext);
                    int ink = 0;
                    for (int y=222; y<295; y+=2) for (int x=18; x<400; x+=2)
                        if (sample.Image.GetPixel(x,y) != sample.Image.GetPixel(0,0)) ink++;
                    Check(ink > 80,"font sample includes the accented-letter rows " + ext);
                }
            }
            foreach (string ext in new[] { ".lua", ".dart" })
            {
                string code = Path.Combine(output,"source"+ext);
                File.WriteAllText(code,ext == ".lua" ? "local x = [=[word]=] -- note" : "final x = 'word'; // note");
                var spans = TextPreviewContent.Load(code).Lines[0].Spans;
                Check(spans.Any(p=>p.Kind==TextTokenKind.Keyword) && spans.Any(p=>p.Kind==TextTokenKind.Comment) &&
                    spans.Any(p=>p.Kind==TextTokenKind.String),"syntax coloring " + ext);
            }
            var old = Thread.CurrentThread.CurrentUICulture;
            try
            {
                foreach (string code in new[] { "lt-LT","et-EE","en-US","fi-FI" })
                {
                    Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo(code);
                    var explicitCode = code == "fi-FI" ? "en" : code.Substring(0,2);
                    Check(L10n.Text("system","find_preview") == L10n.Text(explicitCode,"find_preview") &&
                        L10n.Culture("system").TwoLetterISOLanguageName == explicitCode,"Windows language " + code);
                }
                Check(new AppSettings().Language == "system" && L10n.Normalize("lv") == "lv","automatic default and explicit language");
                foreach (var entry in (Dictionary<string,string[]>)typeof(L10n).GetField("Strings",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null))
                    Check(entry.Value.Length == L10n.Languages.Length && entry.Value.All(v=>!String.IsNullOrWhiteSpace(v)),"complete translations for " + entry.Key);
                foreach (var entry in (Dictionary<string,string[]>)typeof(FeatureText).GetField("strings",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null))
                    foreach (string locale in new[] { "lt","et" }) Check(!String.IsNullOrEmpty(L10n.Text(locale,entry.Key)),locale+" feature "+entry.Key);
            }
            finally { Thread.CurrentThread.CurrentUICulture = old; }
            using (var pinned = new PreviewWindow(true))
            {
                pinned.Configure(new AppSettings { Language="lv", AnimationsEnabled=false });
                foreach (string path in new[] { svg, Path.Combine(output,"sample-font.ttf") })
                {
                    Check(pinned.ShowFile(path,at,Rectangle.Empty),"pinned visual opens " + Path.GetExtension(path));
                    Until(()=>Field<Bitmap>(pinned,"preview")!=null,5000);
                }
                Check(pinned.ShowFile(md,at,Rectangle.Empty),"pinned Markdown opens");
                Until(()=>Field<TextPreviewContent>(pinned,"textPreview")!=null,5000);
                Check(Field<TextPreviewContent>(pinned,"textPreview").Lines.Length>4,"Markdown reflows paragraphs");
                Snapshot(pinned,"markdown.png");
                string csv = Path.Combine(output,"find.csv");
                File.WriteAllLines(csv,Enumerable.Repeat("a,b,c,d,e,f,g,h,i,j,k,l,m,n",70).Concat(new[] { "a,b,c,d,e,f,g,h,i,j,k,l,m,AdAtA adata" }));
                Check(pinned.ShowFile(csv,at,Rectangle.Empty),"pinned CSV opens");
                Until(()=>Field<TextPreviewContent>(pinned,"textPreview")!=null,5000);
                object[] key = { new Message(), Keys.Control|Keys.F };
                Check((bool)Call(pinned,"ProcessCmdKey",key),"Ctrl+F opens the search");
                Field<TextBox>(pinned,"findText").Text="adata";
                Check(Field<ToolStripLabel>(pinned,"findCount").Text=="1 / 2" && Field<int>(pinned,"textOffset")>0 &&
                    Field<int>(pinned,"tableColumn")>0,"case-insensitive match scrolls to the row and column");
                Call(pinned,"HandleFindKey",Keys.Enter);
                Check(Field<ToolStripLabel>(pinned,"findCount").Text=="2 / 2","second match in the same cell");
                Snapshot(pinned,"find.png");
                Call(pinned,"HandleFindKey",Keys.F3);
                Check(Field<int>(pinned,"matchIndex")==0,"next match wraps");
                Call(pinned,"HandleFindKey",Keys.Shift|Keys.F3);
                Check(Field<int>(pinned,"matchIndex")==1,"previous match wraps");
                Field<TextBox>(pinned,"findText").Text="absent";
                Check(Field<ToolStripLabel>(pinned,"findCount").Text=="0 / 0","empty result");
                var findField = Field<HoverSearchBox>(pinned, "findField");
                Check(findField.Editor.BorderStyle == BorderStyle.None && findField.ClearButton.Visible,
                    "pinned search uses the themed field with a clear button");
                findField.ClearButton.PerformClick();
                Check(findField.Editor.TextLength == 0 && !findField.ClearButton.Visible && findField.Editor.Focused,
                    "clearing a pinned search returns focus to the editor");
                Call(pinned,"HandleFindKey",Keys.Escape);
                Check(pinned.Visible && !Field<bool>(pinned,"findOpen"),"Escape returns to the pinned file");
                foreach (string ext in new[] { ".log", ".lua", ".tsv" })
                {
                    string file = Path.Combine(output,"long-match"+ext);
                    string line = new string('a',420) + "needle";
                    File.WriteAllText(file,(ext == ".tsv" ? "field\t" : "") + line);
                    Check(pinned.ShowFile(file,at,Rectangle.Empty),"opens search sample " + ext);
                    Until(()=>Field<TextPreviewContent>(pinned,"textPreview")!=null,5000);
                    Call(pinned,"OpenFind"); Field<TextBox>(pinned,"findText").Text="needle";
                    Check(Field<ToolStripLabel>(pinned,"findCount").Text=="1 / 1","long-line match " + ext);
                    using (var image = new Bitmap(pinned.Width,pinned.Height))
                    {
                        pinned.DrawToBitmap(image,pinned.ClientRectangle);
                        Rectangle area = Field<Rectangle>(pinned,"imageArea");
                        Color mark = SystemInformation.HighContrast ? SystemColors.Highlight : Brand.AccentFill;
                        bool visible = false;
                        for (int y=area.Top; !visible && y<area.Bottom; y+=2)
                            for (int x=area.Left; !visible && x<area.Right; x+=2) visible = image.GetPixel(x,y).ToArgb()==mark.ToArgb();
                        Check(visible,"highlight remains visible beyond the right edge " + ext);
                    }
                }
            }
        }

        private static void ExplorerZipPreview(Point at)
        {
            string directory = Directory.CreateDirectory(Path.Combine(output, "Explorer ZIP")).FullName;
            string archive = Path.Combine(directory, "Priekšskatījums.ZIP");
            File.Copy(Path.Combine(output, "sample.zip"), archive, true);
            Directory.CreateDirectory(Path.Combine(directory, "Real directory.zip"));
            object shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));
            object folder = null, item = null;
            try
            {
                folder = ((dynamic)shell).NameSpace(directory);
                item = ((dynamic)folder).ParseName(Path.GetFileName(archive));
                string displayName = (string)((dynamic)item).Name;
                using (var resolver = new ShellResolver())
                {
                    string found = (string)Call(resolver, "FindUnique", folder, displayName);
                    Check(found == archive, "Explorer resolves a compressed file by its actual shell display name");
                    Check((string)Call(resolver, "FindUnique", folder, Path.GetFileName(archive)) == archive,
                        "Explorer also resolves the full ZIP filename");
                    Check(Call(resolver, "FindUnique", folder, "Real directory.zip") == null,
                        "a filesystem directory with a ZIP suffix stays excluded");
                    using (var preview = new PreviewWindow(false))
                    {
                        Check(preview.ShowFile(found, at, Rectangle.Empty), "resolved Explorer ZIP enters hover preview");
                        Until(() => Field<TextPreviewContent>(preview, "textPreview") != null, 5000);
                        Check(Field<TextPreviewContent>(preview, "textPreview").TableRows.Any(r => r[0] == "docs/līgums.txt"),
                            "hover displays the archived document after Explorer resolution");
                    }
                }
            }
            finally
            {
                if (item != null) Marshal.ReleaseComObject(item);
                if (folder != null) Marshal.ReleaseComObject(folder);
                Marshal.ReleaseComObject(shell);
            }
        }
    }
}
