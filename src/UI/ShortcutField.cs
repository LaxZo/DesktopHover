using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopHover
{
    internal sealed class ShortcutField : ToolStripMenuItem
    {
        private readonly float scale;
        private readonly Font labelFont;
        private readonly bool keyGrid;
        internal Keys Value { get; private set; }
        private int Px(int value) { return (int)Math.Round(value * scale); }

        internal ShortcutField(string label, Keys value, Font font, Font labelFont, float scale, bool keyGrid = false)
            : base(label)
        {
            this.scale = scale; this.labelFont = labelFont; this.keyGrid = keyGrid;
            Font = font;
            AutoSize = false;
            Size = new Size(Math.Max(Px(188), TextRenderer.MeasureText(label, labelFont).Width + Px(38)), Px(48));
            Value = value;
            AccessibleName = label + ", " + Shortcut.Format(value);
            if (keyGrid) DropDown = new KeyGrid();
            ToolStripDropDown menu = DropDown;
            menu.Font = font;
            StyleMenu(menu, scale, true);
            if (!keyGrid)
                DropDownOpening += delegate { UiDrawing.FitMenuRows((ToolStripDropDownMenu)menu, GetCurrentParent(), scale); };
            menu.Closing += delegate(object sender, ToolStripDropDownClosingEventArgs e)
            {
                // WinForms cascades an ItemClicked dismissal up the whole menu tree.
                // The choice handler closes just this list after updating its value.
                if (e.CloseReason == ToolStripDropDownCloseReason.ItemClicked) e.Cancel = true;
            };
            DropDownOpened += delegate
            {
                ToolStripItem selected = DropDownItems[Shortcut.Format(Value)];
                if (selected != null) selected.Select();
            };
        }

        internal void AddChoice(Keys value)
        {
            string text = Shortcut.Format(value);
            ToolStripItem choice = keyGrid ? (ToolStripItem)new ToolStripButton { AutoToolTip = false } : new ToolStripMenuItem();
            choice.Text = choice.Name = choice.AccessibleName = text;
            choice.AutoSize = false; choice.Margin = Padding.Empty;
            int width = keyGrid ? Px(36) : Math.Max(Px(144), TextRenderer.MeasureText(text, Font).Width + Px(50));
            choice.Size = new Size(width, Px(28));
            SetChecked(choice, value == Value);
            choice.Click += delegate
            {
                SetChecked(DropDownItems[Shortcut.Format(Value)], false);
                Value = value; SetChecked(choice, true);
                AccessibleName = Text + ", " + text;
                HideDropDown(); Select(); Invalidate();
            };
            DropDownItems.Add(choice);
            if (keyGrid)
                DropDown.Size = new Size(DropDown.Padding.Horizontal + KeyGrid.Columns * choice.Width,
                    DropDown.Padding.Vertical + ((DropDownItems.Count + KeyGrid.Columns - 1) / KeyGrid.Columns) * choice.Height);
        }

        private static void SetChecked(ToolStripItem choice, bool value)
        {
            var button = choice as ToolStripButton;
            if (button != null) button.Checked = value;
            else if (choice != null) ((ToolStripMenuItem)choice).Checked = value;
        }

        private sealed class KeyGrid : ToolStripDropDown
        {
            internal const int Columns = 6;
            internal KeyGrid()
            {
                AutoSize = false; LayoutStyle = ToolStripLayoutStyle.Flow;
                var flow = (FlowLayoutSettings)LayoutSettings;
                flow.FlowDirection = FlowDirection.LeftToRight; flow.WrapContents = true;
            }
            protected override bool ProcessDialogKey(Keys keyData)
            {
                int index = 0;
                while (index < Items.Count && !Items[index].Selected) index++;
                switch (keyData)
                {
                    case Keys.Home: index = 0; break;
                    case Keys.End: index = Items.Count - 1; break;
                    case Keys.Up: index -= Columns; break;
                    case Keys.Down: index += Columns; break;
                    case Keys.Left: index--; break;
                    case Keys.Right: index++; break;
                    default: return base.ProcessDialogKey(keyData);
                }
                if (Items.Count > 0) Items[Math.Max(0, Math.Min(index, Items.Count - 1))].Select();
                return true;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && keyGrid && HasDropDownItems) DropDown.Dispose();
            base.Dispose(disposing);
        }

        internal static void StyleMenu(ToolStripDropDown menu, float scale, bool showChecks)
        {
            bool contrast = SystemInformation.HighContrast;
            menu.BackColor = contrast ? SystemColors.Menu : Brand.Surface;
            menu.ForeColor = contrast ? SystemColors.MenuText : Brand.Text;
            menu.Renderer = contrast ? (ToolStripRenderer)new ToolStripSystemRenderer() : new HoverMenuRenderer();
            menu.DropShadowEnabled = false;
            var list = menu as ToolStripDropDownMenu;
            if (list != null) { list.ShowCheckMargin = showChecks; list.ShowImageMargin = false; }
            menu.Padding = new Padding((int)Math.Round(5 * scale));
            menu.Opened += delegate
            {
                using (GraphicsPath path = Brand.RoundRect(new RectangleF(0, 0, menu.Width, menu.Height), 9))
                {
                    Region previous = menu.Region; menu.Region = new Region(path);
                    if (previous != null) previous.Dispose();
                }
            };
            MenuMotion.Attach(menu); SystemCursors.UseHand(menu);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            bool active = Selected || DropDown.Visible;
            bool contrast = SystemInformation.HighContrast;
            Color face = contrast ? (active ? SystemColors.Highlight : SystemColors.Menu) : active ? Brand.HoverSurface : Brand.EditorGutter;
            Color ink = contrast ? (active ? SystemColors.HighlightText : SystemColors.MenuText) : Brand.Text;
            Color detail = contrast ? ink : Brand.Muted;
            Color edge = contrast ? ink : active ? Brand.Accent : Brand.Border;
            Graphics graphics = e.Graphics;
            GraphicsState state = graphics.Save();
            try
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = Brand.RoundRect(new RectangleF(Px(2) + .5f, Px(2) + .5f, Width - Px(4) - 1, Height - Px(4) - 1), Px(7)))
                using (var brush = new SolidBrush(face))
                using (var pen = new Pen(edge, Math.Max(1, scale)))
                {
                    graphics.FillPath(brush, path); graphics.DrawPath(pen, path);
                }
                var flags = TextFormatFlags.SingleLine | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis;
                int x = Px(12), width = Width - x - Px(28);
                int middle = Height / 2;
                TextRenderer.DrawText(graphics, Text, labelFont, new Rectangle(x, Px(4), width, middle - Px(4)), detail, flags);
                TextRenderer.DrawText(graphics, Shortcut.Format(Value), Font, new Rectangle(x, middle - Px(2), width, Height - middle - Px(3)), ink, flags);
                float cx = Width - Px(16), cy = middle;
                using (var pen = new Pen(active ? edge : detail, 1.6f * scale) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                    graphics.DrawLines(pen, new[] { new PointF(cx - 2 * scale, cy - 4 * scale), new PointF(cx + 2 * scale, cy), new PointF(cx - 2 * scale, cy + 4 * scale) });
            }
            finally { graphics.Restore(state); }
        }
    }
}
