using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private int tableColumn, tableVisibleColumns = 1;
        private bool HasTable { get { return textPreview != null && textPreview.TableRows != null; } }

        private void LayoutTable()
        {
            textGutterWidth = Math.Min(Px(46), imageArea.Width / 4);
            tableVisibleColumns = Math.Max(1, Math.Min(textPreview.TableColumns,
                (imageArea.Width - textGutterWidth) / Math.Max(1, Px(125))));
            tableColumn = Math.Max(0, Math.Min(tableColumn, textPreview.TableColumns - tableVisibleColumns));
        }

        private void ScrollTableColumns(int delta)
        {
            tableColumn += delta;
            LayoutTable(); UpdateToolbar(); Invalidate(imageArea);
            UpdateTableTip(PointToClient(Cursor.Position));
        }

        private void ScrollText(int delta)
        {
            textOffset = Math.Max(0, Math.Min(textPreview.RowCount - textVisibleLines, textOffset + delta));
            Invalidate(imageArea);
            if (HasTable) UpdateTableTip(PointToClient(Cursor.Position));
        }

        private int TableEdge(int column)
        {
            if (textPreview.ArchiveFileCount >= 0 && tableVisibleColumns == 2 && column == 1)
                return imageArea.Right - (imageArea.Width - textGutterWidth) / 4;
            return imageArea.Left + textGutterWidth + (imageArea.Width - textGutterWidth) * column / tableVisibleColumns;
        }

        private void UpdateTableTip(Point point)
        {
            string value = "";
            if (imageArea.Contains(point) && point.Y >= imageArea.Top + textLineHeight && point.X >= TableEdge(0))
            {
                int row = textOffset + (point.Y - imageArea.Top) / textLineHeight - 1;
                int col = 0;
                while (col + 1 < tableVisibleColumns && point.X >= TableEdge(col + 1)) col++;
                col += tableColumn;
                if (row < textPreview.RowCount && col < textPreview.TableRows[row].Length) value = textPreview.TableRows[row][col];
            }
            if (contentTip.GetToolTip(this) != value) contentTip.SetToolTip(this, value);
        }

        private void PaintTable(Graphics graphics)
        {
            int top = imageArea.Top, left = imageArea.Left, right = imageArea.Right;
            int rows = Math.Min(textVisibleLines, textPreview.RowCount - textOffset);
            const TextFormatFlags flags = TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine |
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.PreserveGraphicsClipping;
            using (var header = new SolidBrush(Brand.SelectedSurface))
            using (var gutter = new SolidBrush(Brand.EditorGutter))
            using (var border = new Pen(Brand.Border))
            {
                graphics.FillRectangle(header, left, top, imageArea.Width, textLineHeight);
                graphics.FillRectangle(gutter, left, top + textLineHeight, textGutterWidth, rows * textLineHeight);
                for (int col = 0; col < tableVisibleColumns; col++)
                {
                    int x = TableEdge(col), width = TableEdge(col + 1) - x;
                    string label = textPreview.ArchiveFileCount >= 0 ? L10n.Text(language, tableColumn + col == 0 ? "archive_path" : "archive_size") : OfficePreviewReader.ColumnName(tableColumn + col);
                    TextRenderer.DrawText(graphics, label, titleFont,
                        new Rectangle(x, top, width, textLineHeight), Brand.Text, flags | TextFormatFlags.HorizontalCenter);
                    graphics.DrawLine(border, x, top, x, top + (rows + 1) * textLineHeight);
                }
                for (int i = 0; i < rows; i++)
                {
                    int row = textOffset + i, y = top + (i + 1) * textLineHeight;
                    string[] values = textPreview.TableRows[row];
                    TextRenderer.DrawText(graphics, (row + 1).ToString(CultureInfo.InvariantCulture), descriptionFont,
                        new Rectangle(left, y, textGutterWidth, textLineHeight), Brand.Muted, flags | TextFormatFlags.HorizontalCenter);
                    for (int col = 0; col < tableVisibleColumns && tableColumn + col < values.Length; col++)
                    {
                        int x = TableEdge(col), width = TableEdge(col + 1) - x;
                        var cell = new Rectangle(x + Px(6), y, Math.Max(1, width - Px(12)), textLineHeight);
                        string value = values[tableColumn + col];
                        if (!PaintFoundText(graphics, row, tableColumn + col, value, descriptionFont, cell))
                            TextRenderer.DrawText(graphics, value, descriptionFont, cell, Brand.Text, flags);
                    }
                    graphics.DrawLine(border, left, y, right, y);
                }
                graphics.DrawLine(border, left, top + (rows + 1) * textLineHeight, right, top + (rows + 1) * textLineHeight);
            }
        }
    }
}
