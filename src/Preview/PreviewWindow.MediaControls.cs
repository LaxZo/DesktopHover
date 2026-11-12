using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DesktopHover
{
    public sealed partial class PreviewWindow
    {
        private Panel mediaControls;
        private HoverSlider mediaSeek, mediaVolume;
        private Label mediaTime;
        private MuteButton mediaMute;
        private bool updatingMediaControls, draggingSeek, mediaMuted;

        private void InitializeMediaControls()
        {
            mediaControls = new Panel { Visible = false };
            mediaSeek = new HoverSlider { Minimum = 0, Maximum = 1000, Step = 5, Value = 0 };
            mediaVolume = new HoverSlider { Minimum = 0, Maximum = 100, Step = 5, Value = volumePercent };
            mediaTime = new Label { TextAlign = ContentAlignment.MiddleCenter };
            mediaMute = new MuteButton();
            mediaControls.Controls.AddRange(new Control[] { mediaSeek, mediaTime, mediaVolume, mediaMute });
            Controls.Add(mediaControls);
            mediaSeek.MouseDown += delegate { draggingSeek = true; };
            mediaSeek.MouseUp += delegate { draggingSeek = false; SeekFromControl(); };
            mediaSeek.MouseCaptureChanged += delegate { if (draggingSeek && !mediaSeek.Capture) { draggingSeek = false; SeekFromControl(); } };
            mediaSeek.ValueChanged += delegate { if (!updatingMediaControls && !draggingSeek) SeekFromControl(); };
            mediaVolume.ValueChanged += delegate
            {
                if (updatingMediaControls) return;
                volumePercent = mediaVolume.Value; ApplyMediaVolume();
            };
            mediaMute.CheckedChanged += delegate
            {
                if (updatingMediaControls) return;
                mediaMuted = mediaMute.Checked; ApplyMediaVolume();
            };
        }

        private void ApplyMediaVolume()
        {
            if (media != null) media.SetVolume(mediaMuted ? 0 : volumePercent);
            UpdateMediaControls();
        }

        private void SeekFromControl()
        {
            if (updatingMediaControls || media == null || !media.IsOpen || media.Duration <= TimeSpan.Zero) return;
            var session = media;
            session.Seek(TimeSpan.FromTicks((long)(session.Duration.Ticks * (mediaSeek.Value / 1000.0))));
            if (media != session) return;
            mediaState = playbackPaused ? "media_paused" : "media_playing";
            mediaTimer.Start(); UpdateMediaDescription(); UpdateMediaControls(); Invalidate();
        }

        private static string TimelineTime(TimeSpan time)
        {
            return time.TotalHours >= 1 ? ((int)time.TotalHours) + ":" + time.Minutes.ToString("00") + ":" + time.Seconds.ToString("00")
                : ((int)time.TotalMinutes) + ":" + time.Seconds.ToString("00");
        }

        private void UpdateMediaControls()
        {
            if (mediaControls == null) return;
            bool available = media != null && media.IsOpen;
            mediaControls.Visible = available;
            if (!available) return;
            updatingMediaControls = true;
            try
            {
                TimeSpan duration = media.Duration;
                TimeSpan position = media.HasEnded ? duration : media.Position;
                mediaSeek.Enabled = duration > TimeSpan.Zero;
                if (!draggingSeek) mediaSeek.Value = duration <= TimeSpan.Zero ? 0 : Math.Max(0, Math.Min(1000, (int)(position.Ticks * 1000.0 / duration.Ticks)));
                mediaTime.Text = TimelineTime(position) + " / " + TimelineTime(duration);
                mediaVolume.Value = volumePercent; mediaMute.Checked = mediaMuted;
                mediaMute.Text = L10n.Text(language, "mute");
                mediaSeek.AccessibleName = L10n.Text(language, "seek");
                mediaVolume.AccessibleName = String.Format(L10n.Culture(language), L10n.Text(language, "volume_accessible"), volumePercent);
                mediaMute.AccessibleName = mediaMute.Text;
                foreach (Control control in mediaControls.Controls) { control.BackColor = Brand.Background; control.ForeColor = Brand.Text; }
                mediaControls.BackColor = Brand.Background;
            }
            finally { updatingMediaControls = false; }
        }

        private int LayoutMediaControls(int top)
        {
            UpdateMediaControls();
            if (mediaControls == null || !mediaControls.Visible) return 0;
            int height = Px(40), width = Math.Max(1, ClientSize.Width - Px(24));
            mediaControls.SetBounds(Px(12), top, width, height);
            mediaControls.Font = toolbarFont;
            mediaMute.UiScale = scale;
            int muteWidth = Px(52) + TextRenderer.MeasureText(mediaMute.Text, mediaMute.Font, Size.Empty,
                TextFormatFlags.NoPadding | TextFormatFlags.SingleLine).Width;
            int volumeWidth = Px(92), timeWidth = Px(116);
            int seekWidth = Math.Max(Px(80), width - muteWidth - volumeWidth - timeWidth);
            mediaSeek.UiScale = mediaVolume.UiScale = scale;
            mediaSeek.SetBounds(0, Px(4), seekWidth, Px(32));
            mediaTime.SetBounds(seekWidth, 0, timeWidth, height);
            mediaVolume.SetBounds(seekWidth + timeWidth, Px(4), volumeWidth, Px(32));
            mediaMute.SetBounds(width - muteWidth, Px(4), muteWidth, Px(30));
            return height;
        }
    }

    internal sealed class MuteButton : HoverCheckBox
    {
        protected override void DrawIndicator(Graphics g, RectangleF box, double selected, Color surface)
        {
            var state = g.Save();
            try
            {
                g.TranslateTransform(box.Left, box.Top);
                g.ScaleTransform(UiScale, UiScale);
                Color ink = Enabled ? UiMotion.Blend(Brand.Text, Brand.Accent, selected) : Brand.Muted;
                using (var pen = new Pen(ink, 1.6f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round })
                {
                    g.DrawPolygon(pen, new[] { new PointF(1, 6.5f), new PointF(4, 6.5f), new PointF(9, 2),
                        new PointF(9, 16), new PointF(4, 11.5f), new PointF(1, 11.5f) });
                    if (Checked)
                    {
                        g.DrawLine(pen, 12, 6.5f, 17, 11.5f);
                        g.DrawLine(pen, 17, 6.5f, 12, 11.5f);
                    }
                    else
                    {
                        g.DrawArc(pen, 6, 4.5f, 8, 9, -50, 100);
                        g.DrawArc(pen, 4, 1, 13, 16, -48, 96);
                    }
                }
            }
            finally { g.Restore(state); }
        }
    }
}
