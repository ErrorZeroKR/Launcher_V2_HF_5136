using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>다크 테마 체크박스: 둥근 상자, 켜면 파란색 + 흰 체크 표시. 줄 전체를 눌러도 토글됨.</summary>
    public class ThemedCheckBox : CheckBox
    {
        private bool hover;

        public ThemedCheckBox()
        {
            // 투명 배경 + 더블 버퍼 조합은 검은 막대/글자 겹침이 생기므로 쓰지 않고, 카드 배경색(Panel)으로 직접 칠함
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw, true);
            AutoSize = false;
            Size = new Size(320, 24);
            Font = UiTheme.UiFont(9F);
            ForeColor = UiTheme.Text;
            BackColor = UiTheme.Panel;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        private static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = radius * 2f;
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int box = Math.Max(14, Height - 8);
            float top = (Height - box) / 2f;
            var r = new RectangleF(1.5f, top, box - 1f, box - 1f);
            bool on = Checked;
            Color fill = on ? (Enabled ? UiTheme.Accent : Color.FromArgb(60, 80, 120)) : UiTheme.Panel2;
            Color border = on ? fill : (hover && Enabled ? UiTheme.Muted : UiTheme.Border);
            using (GraphicsPath path = Round(r, 4f))
            using (var br = new SolidBrush(fill))
            using (var pen = new Pen(border, 1.3f))
            {
                g.FillPath(br, path);
                g.DrawPath(pen, path);
            }
            if (on)
            {
                using (var pen = new Pen(Color.White, 2f))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    g.DrawLines(pen, new[]
                    {
                        new PointF(r.X + r.Width * 0.24f, r.Y + r.Height * 0.52f),
                        new PointF(r.X + r.Width * 0.43f, r.Y + r.Height * 0.71f),
                        new PointF(r.X + r.Width * 0.77f, r.Y + r.Height * 0.30f)
                    });
                }
            }

            var tr = new Rectangle(box + 10, 0, Math.Max(10, Width - box - 10), Height);
            TextRenderer.DrawText(g, Text, Font, tr, Enabled ? ForeColor : UiTheme.Muted,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }
}
