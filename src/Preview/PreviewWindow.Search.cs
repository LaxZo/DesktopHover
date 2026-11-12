using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private struct FindMatch
        {
            internal int Row, Column, Offset;
        }
        private readonly List<FindMatch> matches = new List<FindMatch>();
        private ToolStrip findBar;
        private HoverSearchBox findField;
        private ToolStripControlHost findHost;
        private TextBox findText;
        private ToolStripLabel findLabel, findCount;
        private ToolStripButton findPrevious, findNext, findClose;
        private ToolStripButton findButton;
        private bool findOpen;
        private int matchIndex = -1;

        private void InitializeFindBar()
        {
            findBar = new ToolStrip { Visible = false, AutoSize = false, CanOverflow = false, Dock = DockStyle.None,
                GripStyle = ToolStripGripStyle.Hidden, Renderer = new PinnedToolbarRenderer(), TabStop = false };
            findLabel = new ToolStripLabel();
            findField = new HoverSearchBox(scale) { Name = "preview-search-field" };
            findText = findField.Editor;
            findText.Name = "preview-search"; findText.MaxLength = 500;
            findField.ClearButton.Name = "clear-preview-search";
            findHost = new ToolStripControlHost(findField) { AutoSize = false, Margin = Padding.Empty };
            findCount = new ToolStripLabel();
            findPrevious = new ToolStripButton("‹") { Tag = "previous" };
            findNext = new ToolStripButton("›") { Tag = "next" };
            findClose = new ToolStripButton("×") { Tag = "close_advanced" };
            findPrevious.Click += delegate { StepMatch(-1); };
            findNext.Click += delegate { StepMatch(1); };
            findClose.Click += delegate { CloseFind(); };
            findText.TextChanged += delegate { RebuildMatches(); };
            findText.KeyDown += delegate(object sender, KeyEventArgs e)
            { if (HandleFindKey(e.KeyData)) e.SuppressKeyPress = true; };
            findBar.Items.AddRange(new ToolStripItem[] { findLabel, findHost, findCount, findPrevious, findNext, findClose });
            Controls.Add(findBar);
        }
        private int LayoutFindBar(int top)
        {
            bool show = findOpen && textPreview != null;
            findBar.Visible = show;
            if (!show) return 0;
            findBar.Font = toolbarFont; findBar.BackColor = Brand.Surface; findBar.ForeColor = Brand.Text;
            foreach (var button in new[] { findPrevious, findNext, findClose })
            { button.AutoSize = false; button.Size = new Size(Px(30), Px(28)); }
            findField.SetScale(scale); findField.RefreshTheme();
            findField.ClearButton.AccessibleName = L10n.Text(language, "clear_search");
            findLabel.Text = findText.AccessibleName = L10n.Text(language, "find_preview");
            findPrevious.AccessibleName = findPrevious.ToolTipText = L10n.Text(language,"find_previous");
            findNext.AccessibleName = findNext.ToolTipText = L10n.Text(language,"find_next");
            findClose.AccessibleName = findClose.ToolTipText = L10n.Text(language,"close_advanced");
            findBar.SetBounds(1,top,Math.Max(1,ClientSize.Width-2),Px(34));
            findBar.Padding = new Padding(Px(8),0,Px(8),0);
            int fieldWidth = Math.Min(Px(260),Math.Max(Px(115),ClientSize.Width-findLabel.GetPreferredSize(Size.Empty).Width-Px(170)));
            findHost.Size = new Size(fieldWidth, Px(32));
            return findBar.Height;
        }
        private void OpenFind()
        {
            findOpen = true; LayoutInteractive(); RebuildMatches();
            findText.Focus(); findText.SelectAll();
        }
        private void ResetFind()
        {
            findOpen = false; matchIndex = -1; matches.Clear();
            if (findBar != null && !findBar.IsDisposed) { findBar.Visible = false; findText.Clear(); }
        }
        private void CloseFind() { ResetFind(); Focus(); LayoutInteractive(); }
        private bool HandleFindKey(Keys keys)
        {
            if (!findOpen) return false;
            if (keys == Keys.Escape) { CloseFind(); return true; }
            if (keys == Keys.Enter || keys == Keys.F3) { StepMatch(1); return true; }
            if (keys == (Keys.Shift | Keys.Enter) || keys == (Keys.Shift | Keys.F3)) { StepMatch(-1); return true; }
            return false;
        }
        private void RebuildMatches()
        {
            if (!findOpen || textPreview == null) return;
            matches.Clear(); matchIndex = -1;
            string term = findText.Text;
            if (term.Length != 0)
            {
                for (int row = 0; row < textPreview.RowCount; row++)
                {
                    if (HasTable)
                    {
                        string[] cells = textPreview.TableRows[row];
                        for (int col = 0; col < cells.Length; col++) FindIn(cells[col],term,row,col);
                    }
                    else FindIn(textPreview.Lines[row].Text,term,row,0);
                }
            }
            StepMatch(1);
        }
        private void FindIn(string value,string term,int row,int col)
        {
            int at = 0;
            while ((at = value.IndexOf(term,at,StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                matches.Add(new FindMatch { Row = row, Column = col, Offset = at });
                at += term.Length;
            }
        }
        private void StepMatch(int step)
        {
            if (matches.Count > 0)
            {
                matchIndex = (matchIndex + step + matches.Count) % matches.Count;
                var hit = matches[matchIndex];
                if (hit.Row < textOffset || hit.Row >= textOffset + textVisibleLines)
                    textOffset = Math.Max(0, Math.Min(hit.Row - textVisibleLines / 2,textPreview.RowCount - textVisibleLines));
                if (HasTable && (hit.Column < tableColumn || hit.Column >= tableColumn + tableVisibleColumns))
                { tableColumn = hit.Column; LayoutTable(); UpdateToolbar(); }
            }
            findCount.Text = findText.TextLength == 0 ? "" : (matchIndex + 1) + " / " + matches.Count;
            findPrevious.Enabled = findNext.Enabled = matches.Count > 0;
            Invalidate(imageArea);
        }

        private bool PaintFoundText(Graphics g,int row,int col,string value,Font font,Rectangle bounds)
        {
            if (!findOpen || matchIndex < 0 || matchIndex >= matches.Count) return false;
            var hit = matches[matchIndex];
            if (hit.Row != row || hit.Column != col || hit.Offset + findText.TextLength > value.Length) return false;
            var state = g.Save();
            try
            {
                g.SetClip(bounds,System.Drawing.Drawing2D.CombineMode.Intersect);
                int length = findText.TextLength;
                int prefix = hit.Offset;
                string left = value.Substring(0,prefix), term = value.Substring(prefix,length);
                int before = TextRenderer.MeasureText(g,left,font,Size.Empty,MarkdownFlags).Width;
                int mark = Math.Max(1,TextRenderer.MeasureText(g,term,font,Size.Empty,MarkdownFlags).Width);
                int shift = Math.Max(0,before + Math.Min(mark,bounds.Width) - bounds.Width + Px(4));
                int x = bounds.Left - Math.Min(before,shift);
                var line = new Rectangle(x,bounds.Top,Math.Max(1,bounds.Right-x),bounds.Height);
                TextRenderer.DrawText(g,value,font,line,Brand.Text,MarkdownFlags);
                var highlight = new Rectangle(x+before,bounds.Top,mark,bounds.Height);
                Color bg = SystemInformation.HighContrast ? SystemColors.Highlight : Brand.AccentFill;
                Color fg = SystemInformation.HighContrast ? SystemColors.HighlightText : Brand.AccentMark(Color.White);
                using (var brush = new SolidBrush(bg)) g.FillRectangle(brush, highlight);
                TextRenderer.DrawText(g,term,font,highlight,fg,MarkdownFlags);
                return true;
            }
            finally { g.Restore(state); }
        }
    }
}
