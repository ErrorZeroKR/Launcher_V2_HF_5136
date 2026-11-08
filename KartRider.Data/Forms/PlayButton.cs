using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// 플레이 버튼: 큰 글자(굵게)와 직접 그린 ▶ 아이콘. 실행할 수 없는 상황이면 어둡게 비활성화 표시하고 아래에 이유를 작게 보여줌.
    /// </summary>
    public class PlayButton : Button
    {
        private bool hover, pressed;
        private string reason;
        private readonly Font mainFont = UiTheme.UiFont(16F, FontStyle.Bold);
        private readonly Font mainFontOff = UiTheme.UiFont(13F, FontStyle.Bold);
        private readonly Font reasonFont = UiTheme.UiFont(8.5F);

        public PlayButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Cursor = Cursors.Hand;
            Text = "플레이";
        }

        /// <summary>비활성화일 때 버튼 아래에 작게 보여줄 이유 (예: 데이터 읽는 중…)</summary>
        public string Reason
        {
            get { return reason; }
            set { if (reason == value) return; reason = value; Invalidate(); }
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            hover = pressed = false;
            Invalidate();
            base.OnEnabledChanged(e);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            Color bg, fg;
            if (!Enabled) { bg = Color.FromArgb(28, 35, 47); fg = Color.FromArgb(160, 170, 186); }
            else if (pressed) { bg = Color.FromArgb(31, 111, 235); fg = Color.White; }
            else if (hover) { bg = UiTheme.AccentHover; fg = Color.White; }
            else { bg = UiTheme.Accent; fg = Color.White; }
            g.Clear(bg);
            if (!Enabled)
                using (var pen = new Pen(UiTheme.Border, 1f))
                    g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            bool showReason = !Enabled && !string.IsNullOrEmpty(reason);
            Font f = Enabled ? mainFont : mainFontOff;
            const TextFormatFlags nf = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;
            Size big = new Size(int.MaxValue, int.MaxValue);
            Size ts = TextRenderer.MeasureText(g, Text, f, big, nf);
            int reasonH = showReason ? TextRenderer.MeasureText(g, reason, reasonFont, big, nf).Height : 0;

            int iconH = (int)(ts.Height * 0.86f);           // ▶ 아이콘은 글자 높이에 맞춰 크게
            int iconW = (int)(iconH * 0.86f);
            int gap = (int)(ts.Height * 0.42f);
            int total = iconW + gap + ts.Width;
            int x0 = (Width - total) / 2;
            int blockH = ts.Height + (showReason ? reasonH + 2 : 0);
            int y0 = (Height - blockH) / 2;
            float cy = y0 + ts.Height / 2f;

            using (var br = new SolidBrush(fg))
            {
                var tri = new PointF[]
                {
                    new PointF(x0, cy - iconH / 2f),
                    new PointF(x0, cy + iconH / 2f),
                    new PointF(x0 + iconW, cy)
                };
                g.FillPolygon(br, tri);
            }
            TextRenderer.DrawText(g, Text, f, new Rectangle(x0 + iconW + gap, y0, ts.Width + 6, ts.Height), fg, nf | TextFormatFlags.VerticalCenter);
            if (showReason)
                TextRenderer.DrawText(g, reason, reasonFont, new Rectangle(0, y0 + ts.Height + 2, Width, reasonH), fg, nf | TextFormatFlags.HorizontalCenter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { mainFont.Dispose(); mainFontOff.Dispose(); reasonFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
