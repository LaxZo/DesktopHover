using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed partial class TextPreviewContent
    {
        internal TextPreviewLine[] MarkdownSource;
        internal bool IsMarkdown { get { return MarkdownSource != null; } }
        internal int MarkdownWidth;
        internal float MarkdownScale;
        private static readonly Regex listMarker = new Regex(@"^(\s*)([-+*]|\d{1,9}[.)])\s+", RegexOptions.CultureInvariant);

        private void FormatMarkdown()
        {
            var blocks = new List<TextPreviewLine>();
            char fence = '\0'; int fenceLength = 0;
            foreach (var source in Lines)
            {
                string value = source.Text.TrimStart();
                char first = value.Length == 0 ? '\0' : value[0];
                int n = 0; while (n < value.Length && value[n] == first) n++;
                if ((first == 96 || first == '~') && n >= 3 && source.Text.Length - value.Length <= 3)
                {
                    if (fence == '\0') { fence = first; fenceLength = n; continue; }
                    if (first == fence && n >= fenceLength && value.Substring(n).Trim().Length == 0) { fence = '\0'; continue; }
                }
                var line = new TextPreviewLine { Text = source.Text, CodeBlock = fence != '\0', MarkdownBlock = blocks.Count };
                if (!line.CodeBlock)
                {
                    if (first == '#' && n <= 6 && (n == value.Length || value[n] == ' '))
                    { line.Heading = n; line.Text = Regex.Replace(value.Substring(n).Trim(), @"\s+#+$", ""); }
                    else
                    {
                        var marker = listMarker.Match(source.Text);
                        if (marker.Success)
                        {
                            string prefix = marker.Groups[2].Value;
                            line.ListItem = true;
                            line.Text = marker.Groups[1].Value + (Char.IsDigit(prefix[0]) ? prefix : "•") + " " + source.Text.Substring(marker.Length);
                        }
                    }
                }
                blocks.Add(line);
            }
            Lines = MarkdownSource = blocks.ToArray();
        }
    }

    public sealed partial class PreviewWindow
    {
        private Font MarkdownFont(int heading)
        {
            return new Font("Segoe UI", Px(heading == 1 ? 21 : heading == 2 ? 18 : heading > 0 ? 16 : 14),
                heading > 0 ? FontStyle.Bold : FontStyle.Regular, GraphicsUnit.Pixel);
        }
        private const TextFormatFlags MarkdownFlags = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix |
            TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.PreserveGraphicsClipping;

        private bool LayoutMarkdown(int width)
        {
            if (textPreview.MarkdownWidth == width && textPreview.MarkdownScale == scale) return false;
            textPreview.MarkdownWidth = width; textPreview.MarkdownScale = scale;
            var rows = new List<TextPreviewLine>();
            using (var g = CreateGraphics())
            foreach (var block in textPreview.MarkdownSource)
            {
                int available = Math.Max(Px(50), width - Px(block.ListItem ? 56 : 32));
                if (block.Text.Length == 0 || block.CodeBlock) { rows.Add(block); continue; }
                using (Font font = MarkdownFont(block.Heading))
                {
                    string rest = block.Text;
                    while (TextRenderer.MeasureText(g, rest, font, Size.Empty, MarkdownFlags).Width > available && rest.Length > 1)
                    {
                        int lo = 1, hi = rest.Length;
                        while (lo + 1 < hi)
                        {
                            int mid = (lo + hi) / 2;
                            if (TextRenderer.MeasureText(g,rest.Substring(0,mid),font,Size.Empty,MarkdownFlags).Width <= available) lo = mid;
                            else hi = mid;
                        }
                        int space = rest.LastIndexOf(' ', lo);
                        int cut = space > 0 ? space : lo;
                        rows.Add(new TextPreviewLine { Text = rest.Substring(0,cut), Heading = block.Heading, ListItem = block.ListItem,
                            MarkdownBlock = block.MarkdownBlock, MarkdownOffset = block.Text.Length - rest.Length });
                        rest = rest.Substring(cut).TrimStart();
                    }
                    if (rest.Length > 0) rows.Add(new TextPreviewLine { Text = rest, Heading = block.Heading, ListItem = block.ListItem,
                        MarkdownBlock = block.MarkdownBlock, MarkdownOffset = block.Text.Length - rest.Length });
                }
            }
            textPreview.Lines = rows.ToArray(); return true;
        }
        private void PaintMarkdown(Graphics g)
        {
            using (var background = new SolidBrush(Brand.EditorGutter))
            {
                for (int i = 0; i < textVisibleLines && i + textOffset < textPreview.RowCount; i++)
                {
                    int row = i + textOffset; var line = textPreview.Lines[row];
                    int y = imageArea.Top + Px(8) + i * textLineHeight;
                    int x = imageArea.Left + Px(line.ListItem ? 32 : 16);
                    if (line.CodeBlock) g.FillRectangle(background, imageArea.Left + Px(6), y, imageArea.Width - Px(12), textLineHeight);
                    var bounds = new Rectangle(x,y,Math.Max(1,imageArea.Right - x - Px(8)),textLineHeight);
                    using (Font body = MarkdownFont(line.Heading))
                    {
                        Font font = line.CodeBlock ? codeFont : body;
                        if (!PaintFoundText(g,row,0,line.Text,font,bounds))
                            TextRenderer.DrawText(g,line.Text,font,bounds,line.Heading > 0 ? Brand.AccentHover : Brand.Text,MarkdownFlags);
                    }
                }
            }
        }
    }
}
