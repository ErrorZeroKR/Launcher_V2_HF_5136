using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>다크 테마 전용 슬림 스크롤바 (세로/가로). 마우스 올리면 굵어지고, 드래그/트랙 클릭 지원</summary>
    public class ThemedScrollBar : Control
    {
        private readonly bool vertical;
        private int maximum;   // 스크롤 가능한 최대 값 (전체 크기 - 보이는 크기)
        private int pageSize;  // 보이는 크기
        private int value;
        private bool hover;
        private bool dragging;
        private int dragOffset;

        /// <summary>사용자가 막대를 움직였을 때만 발생</summary>
        public event EventHandler Scrolled;

        public ThemedScrollBar(bool vertical)
        {
            this.vertical = vertical;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(11, 15, 20);
            if (vertical) Width = 12; else Height = 12;
            TabStop = false;
        }

        public int Value { get { return value; } }

        /// <summary>맨 끝(또는 스크롤할 내용이 없음)인지</summary>
        public bool AtEnd { get { return maximum <= 0 || value >= maximum - 2; } }

        /// <summary>프로그램이 현재 상태를 알려 줌 (이벤트 발생 안 함)</summary>
        public void SetState(int val, int max, int page)
        {
            if (dragging) return;
            max = Math.Max(0, max);
            page = Math.Max(1, page);
            val = Math.Max(0, Math.Min(val, max));
            if (val == value && max == maximum && page == pageSize) return;
            value = val;
            maximum = max;
            pageSize = page;
            Invalidate();
        }

        private RectangleF ThumbRect()
        {
            if (maximum <= 0) return RectangleF.Empty;
            var t = new RectangleF(2, 2, Math.Max(1, Width - 4), Math.Max(1, Height - 4));
            float total = maximum + pageSize;
            float len = vertical ? t.Height : t.Width;
            float thumbLen = Math.Max(28f, len * pageSize / total);
            if (thumbLen > len) thumbLen = len;
            float pos = (len - thumbLen) * value / maximum;
            return vertical
                ? new RectangleF(t.X, t.Y + pos, t.Width, thumbLen)
                : new RectangleF(t.X + pos, t.Y, thumbLen, t.Height);
        }

        private static GraphicsPath Pill(RectangleF r)
        {
            var p = new GraphicsPath();
            float d = Math.Max(1f, Math.Min(r.Width, r.Height));
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            RectangleF th = ThumbRect();
            if (th.IsEmpty) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color c = dragging ? Color.FromArgb(120, 136, 160) : (hover ? Color.FromArgb(94, 108, 132) : Color.FromArgb(62, 73, 92));
            float inset = (hover || dragging) ? 1f : 2.5f;   // 평소엔 가늘게, 올리면 굵게
            if (vertical) th.Inflate(-inset, 0); else th.Inflate(0, -inset);
            using (GraphicsPath path = Pill(th))
            using (var br = new SolidBrush(c))
            {
                e.Graphics.FillPath(br, path);
            }
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            hover = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || maximum <= 0) return;
            RectangleF th = ThumbRect();
            float m = vertical ? e.Y : e.X;
            float start = vertical ? th.Y : th.X;
            float len = vertical ? th.Height : th.Width;
            if (m >= start && m <= start + len)
            {
                dragging = true;
                dragOffset = (int)(m - start);
                Capture = true;
                Invalidate();
            }
            else
            {
                Change(value + (m < start ? -1 : 1) * Math.Max(1, pageSize * 9 / 10)); // 트랙 클릭: 한 페이지씩
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            RectangleF th = ThumbRect();
            float trackStart = 2f;
            float trackLen = (vertical ? Height : Width) - 4f;
            float thumbLen = vertical ? th.Height : th.Width;
            float range = trackLen - thumbLen;
            if (range <= 0) return;
            float m = (vertical ? e.Y : e.X) - dragOffset - trackStart;
            m = Math.Max(0f, Math.Min(range, m));
            Change((int)Math.Round(maximum * (double)m / range));
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (dragging)
            {
                dragging = false;
                Capture = false;
                Invalidate();
            }
        }

        private void Change(int v)
        {
            v = Math.Max(0, Math.Min(maximum, v));
            if (v == value) return;
            value = v;
            Invalidate();
            EventHandler h = Scrolled;
            if (h != null) h(this, EventArgs.Empty);
        }
    }
}
