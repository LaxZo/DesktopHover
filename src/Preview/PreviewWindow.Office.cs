using System;
using System.Windows.Forms;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private readonly OfficeViewOptions officeView = new OfficeViewOptions();
        private ToolStripComboBox sheetPicker;
        private VScrollBar officeScroll;
        private Timer officeTimer;
        private bool updatingOffice;
        private int officeContentHeight, officePageHeight;

        private bool IsOfficeView { get { return interactive && CurrentPath != null && OfficePreviewReader.SupportsTheming(CurrentPath); } }

        private void InitializeOfficeView()
        {
            sheetPicker = new ToolStripComboBox { DropDownStyle = ComboBoxStyle.DropDownList, AutoSize = false, Width = 130, Visible = false };
            toolbar.Items.Add(sheetPicker);
            sheetPicker.ComboBox.FlatStyle = FlatStyle.Flat;
            officeScroll = new VScrollBar { Visible = false, SmallChange = 80 };
            Controls.Add(officeScroll);
            officeTimer = new Timer { Interval = 120 };
            officeTimer.Tick += delegate
            {
                officeTimer.Stop();
                if (!IsOfficeView) return;
                CancelDocumentLoad(); StartDocumentLoad(CurrentPath, false); UpdateToolbar();
            };
            sheetPicker.SelectedIndexChanged += delegate
            {
                if (updatingOffice || sheetPicker.SelectedIndex < 0) return;
                officeView.SheetIndex = sheetPicker.SelectedIndex; officeView.ScrollY = 0;
                updatingOffice = true; officeScroll.Value = 0; updatingOffice = false;
                QueueOfficeView();
            };
            officeScroll.ValueChanged += delegate
            {
                if (updatingOffice) return;
                officeView.ScrollY = officeScroll.Value; QueueOfficeView();
            };
        }

        private void QueueOfficeView()
        {
            CancelDocumentLoad(); officeTimer.Stop(); officeTimer.Start();
        }

        private void ApplyOfficeResult(DocumentPreviewResult result)
        {
            if (!IsOfficeView || officeScroll == null || preview == null) return;
            updatingOffice = true;
            try
            {
                officeContentHeight = Math.Max(preview.Height, result.ContentHeight); officePageHeight = preview.Height;
                officeScroll.LargeChange = officePageHeight;
                officeScroll.Maximum = officeContentHeight - 1;
                officeScroll.Value = Math.Max(0, Math.Min(officeView.ScrollY, officeContentHeight - officePageHeight));
                officeView.ScrollY = officeScroll.Value;
                sheetPicker.Items.Clear();
                if (result.SheetNames != null)
                {
                    sheetPicker.Items.AddRange(result.SheetNames);
                    sheetPicker.SelectedIndex = result.SheetIndex;
                    officeView.SheetIndex = result.SheetIndex;
                }
                sheetPicker.Visible = sheetPicker.Items.Count > 1;
                sheetPicker.ToolTipText = sheetPicker.AccessibleName = L10n.Text(language, "worksheet");
                sheetPicker.BackColor = Brand.Surface; sheetPicker.ForeColor = Brand.Text;
            }
            finally { updatingOffice = false; }
        }

        private void LayoutOfficeView()
        {
            if (officeScroll == null) return;
            officeScroll.Visible = IsOfficeView && officeContentHeight > officePageHeight;
            if (!officeScroll.Visible) return;
            int width = SystemInformation.VerticalScrollBarWidth;
            officeScroll.SetBounds(viewport.Right - width, viewport.Top, width, viewport.Height);
            viewport.Width = Math.Max(1, viewport.Width - width - Px(4));
        }

        private void ScrollOffice(int steps)
        {
            if (officeScroll == null || !officeScroll.Visible) return;
            officeScroll.Value = Math.Max(0, Math.Min(officeContentHeight - officePageHeight,
                officeScroll.Value - steps * Math.Max(60, officePageHeight / 8)));
        }

        private void ClearOfficeView()
        {
            if (officeTimer == null) return;
            officeTimer.Stop(); updatingOffice = true;
            officeView.SheetIndex = officeView.ScrollY = officeContentHeight = officePageHeight = 0;
            officeScroll.Value = 0; officeScroll.Visible = false;
            sheetPicker.Items.Clear(); sheetPicker.Visible = false; updatingOffice = false;
        }
    }
}
