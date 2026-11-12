using System;
using System.Drawing;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static partial class Regression
    {
        private static void PopupSpacing()
        {
            var settings = new AppSettings { Language = "system", AnimationsEnabled = false };
            using (var window = new SettingsWindow(settings, false, null, null))
            {
                window.Show();
                foreach (float dpi in new[] { 1f, 1.5f })
                {
                    window.FitToArea(Screen.FromControl(window).WorkingArea, dpi);
                    foreach (string name in new[] { "Language", "Theme", "Shortcut" })
                    {
                        if (name == "Shortcut") Call(window, "ShowShortcutMenu", false);
                        else Call(window, "Show" + name + "Menu");
                        var menu = Field<ContextMenuStrip>(window, name.ToLowerInvariant() + "Menu");
                        CheckMenuRows(menu, name, dpi);
                        if (name == "Shortcut")
                        {
                            var modifiers = (ShortcutField)menu.Items["modifiers"];
                            modifiers.ShowDropDown();
                            CheckMenuRows(modifiers.DropDown, "modifiers", dpi);
                            modifiers.HideDropDown();
                            var keys = (ShortcutField)menu.Items["key"];
                            keys.ShowDropDown();
                            var first = keys.DropDownItems[0].Bounds;
                            var sixth = keys.DropDownItems[5].Bounds;
                            Check(first.Top == sixth.Top && first.Left == keys.DropDown.ClientSize.Width - sixth.Right,
                                "six-key grid has equal outer margins at scale " + dpi);
                            keys.HideDropDown();
                        }
                        menu.Close();
                    }
                }
                window.Close();
            }
            Pump(80);
        }

        private static void CheckMenuRows(ToolStripDropDown menu, string name, float dpi)
        {
            Pump(25);
            Snapshot(menu, name + "-insets-" + (int)(dpi * 100) + ".png");
            foreach (ToolStripItem row in menu.Items)
            {
                Rectangle bounds = row.Bounds;
                int left = bounds.Left, right = menu.ClientSize.Width - bounds.Right;
                Check(left > 0 && right == left && menu.ClientRectangle.Contains(bounds) &&
                    (row is ToolStripSeparator || left == (int)Math.Round(4 * dpi)),
                    name + " row " + row.Name + " has equal insets at scale " + dpi);
                if (row is ToolStripMenuItem)
                    Check(menu.GetItemAt(left + 1, bounds.Top + bounds.Height / 2) == row &&
                        menu.GetItemAt(bounds.Right - 2, bounds.Top + bounds.Height / 2) == row,
                        name + " row responds across its visible width");
                var choice = row as ToolStripMenuItem;
                if (choice != null && choice.Checked && !SystemInformation.HighContrast)
                    CheckChoiceMark(menu, choice);
            }
        }

        private static void CheckChoiceMark(ToolStripDropDown menu, ToolStripMenuItem choice)
        {
            using (var image = new Bitmap(menu.Width, menu.Height))
            {
                menu.DrawToBitmap(image, menu.ClientRectangle);
                Rectangle row = choice.Bounds;
                int centerX = row.Right - 2 * Math.Max(4, row.Height / 4);
                int centerY = row.Top + row.Height / 2;
                for (int x = centerX - 6; x <= centerX + 6; x++)
                    for (int y = centerY - 5; y <= centerY + 5; y++)
                        if (image.GetPixel(x, y).ToArgb() == Brand.Accent.ToArgb()) return;
                throw new InvalidOperationException(choice.Text + " has no check at the right of its row");
            }
        }
    }
}
