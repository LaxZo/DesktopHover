using System;
using System.Drawing;
using System.Windows.Forms;

namespace DesktopHover
{
    internal static class AppNotice
    {
        internal static void Show(Form owner, string message, string title, MessageBoxIcon kind)
        {
            if (SystemInformation.HighContrast)
            {
                MessageBox.Show(owner, message, title, MessageBoxButtons.OK, kind);
                return;
            }
            using (var dialog = new Form())
            using (var icon = Brand.CreateIcon())
            using (var glyph = (kind == MessageBoxIcon.Warning ? SystemIcons.Warning : SystemIcons.Information).ToBitmap())
            {
                dialog.Text = title; dialog.Icon = icon;
                dialog.StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.ShowInTaskbar = owner == null;
                dialog.MinimizeBox = dialog.MaximizeBox = false;
                dialog.BackColor = Brand.Background; dialog.ForeColor = Brand.Text;
                dialog.Font = SystemFonts.MessageBoxFont;
                dialog.HandleCreated += delegate { WindowBackdrop.ApplyCaption(dialog); };
                Rectangle work = owner == null ? Screen.FromPoint(Cursor.Position).WorkingArea : Screen.FromControl(owner).WorkingArea;
                float scale;
                using (Graphics g = (owner ?? dialog).CreateGraphics()) scale = g.DpiX / 96f;
                int pad = (int)Math.Ceiling(18 * scale), symbol = (int)Math.Ceiling(32 * scale);
                int width = Math.Min((int)(440 * scale), work.Width - 2 * pad);
                int textX = 2 * pad + symbol, textWidth = Math.Max(1, width - textX - pad);
                int measured = TextRenderer.MeasureText(message, dialog.Font, new Size(textWidth, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPrefix).Height + pad;
                int textHeight = Math.Max(symbol, Math.Min(work.Height / 2, measured));
                var body = new TextBox { Multiline = true, ReadOnly = true, BorderStyle = BorderStyle.None, Text = message,
                    BackColor = Brand.Background, ForeColor = Brand.Text, TabStop = true, AccessibleName = title,
                    ScrollBars = measured > textHeight ? ScrollBars.Vertical : ScrollBars.None };
                body.SetBounds(textX, pad, textWidth, textHeight);
                var picture = new PictureBox { Image = glyph, SizeMode = PictureBoxSizeMode.Zoom };
                picture.SetBounds(pad, pad, symbol, symbol);
                var ok = new HoverButton { Text = "OK", AccessibleName = "OK", Compact = true,
                    UiScale = scale, DialogResult = DialogResult.OK, BackColor = Brand.Background, ForeColor = Brand.Text };
                int buttonWidth = (int)(88 * scale), buttonHeight = (int)(34 * scale);
                ok.SetBounds(width - pad - buttonWidth, body.Bottom + pad, buttonWidth, buttonHeight);
                dialog.ClientSize = new Size(width, ok.Bottom + pad);
                dialog.Controls.AddRange(new Control[] { picture, body, ok });
                dialog.AcceptButton = dialog.CancelButton = ok;
                dialog.ActiveControl = ok;
                dialog.ShowDialog(owner);
            }
        }
    }
}
