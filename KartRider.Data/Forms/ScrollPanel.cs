using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// 카드를 세로로 쌓아 보여주는 스크롤 영역 (전용 스크롤바 + 마우스 휠).
    /// AddCard 로 카드를 추가하면 폭은 영역 폭에 맞춰지고, 카드 높이가 바뀌면 자동으로 다시 배치됨.
    /// </summary>
    public class ScrollPanel : Panel
    {
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(Point p);

        private const int WM_MOUSEWHEEL = 0x020A;
        private const int Gap = 12;   // 카드 사이 간격 (논리 px, 아래에서 DPI 배율 적용)

        private readonly Panel content;
        private readonly ThemedScrollBar bar;
        private readonly List<Control> cards = new List<Control>();
        private readonly WheelFilter filter;
        private int scrollY;
        private bool layouting;

        public ScrollPanel()
        {
            BackColor = UiTheme.Bg;

            content = new Panel();
            content.BackColor = Color.Transparent;
            content.Location = new Point(0, 0);

            bar = new ThemedScrollBar(true);
            bar.Dock = DockStyle.Right;
            bar.Visible = false;
            bar.Scrolled += (s, e) => { scrollY = bar.Value; content.Top = -scrollY; };

            Controls.Add(bar);
            Controls.Add(content);
            Resize += (s, e) => Relayout();

            filter = new WheelFilter(this);
            Application.AddMessageFilter(filter);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Application.RemoveMessageFilter(filter);
            base.Dispose(disposing);
        }

        public void AddCard(Control card)
        {
            cards.Add(card);
            content.Controls.Add(card);
            card.SizeChanged += (s, e) => { if (!layouting) Relayout(); };
            Relayout();
        }

        /// <summary>카드 폭/위치를 다시 계산하고 스크롤바를 갱신</summary>
        public void Relayout()
        {
            if (layouting) return;
            layouting = true;
            try
            {
                int gap = (int)(Gap * DeviceDpi / 96f);
                int w = Math.Max(60, ClientSize.Width - bar.Width - gap / 2);   // 스크롤바 자리는 항상 비워 둠
                int y = 0;
                foreach (Control c in cards)
                {
                    c.SetBounds(0, y, w, c.Height);
                    y += c.Height + gap;
                }
                int total = Math.Max(0, y - gap);
                content.Size = new Size(w, Math.Max(total, 1));
                int max = Math.Max(0, total - ClientSize.Height);
                if (scrollY > max) scrollY = max;
                content.Top = -scrollY;
                bar.Visible = max > 0;
                bar.SetState(scrollY, max, Math.Max(1, ClientSize.Height));
            }
            finally { layouting = false; }
        }

        private void ScrollByWheel(int delta)
        {
            int max = Math.Max(0, content.Height - ClientSize.Height);
            if (max <= 0) return;
            int step = (int)(delta / 120.0 * 56 * DeviceDpi / 96f);
            scrollY = Math.Max(0, Math.Min(max, scrollY - step));
            content.Top = -scrollY;
            bar.SetState(scrollY, max, Math.Max(1, ClientSize.Height));
        }

        /// <summary>마우스가 이 영역 위에 있을 때만 휠을 가로채서 스크롤 (다른 화면/카드 위에서는 건드리지 않음)</summary>
        private sealed class WheelFilter : IMessageFilter
        {
            private readonly ScrollPanel owner;
            public WheelFilter(ScrollPanel owner) { this.owner = owner; }

            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg != WM_MOUSEWHEEL) return false;
                if (owner.IsDisposed || !owner.Visible || !owner.IsHandleCreated) return false;
                Point pt = Cursor.Position;
                if (!owner.RectangleToScreen(owner.ClientRectangle).Contains(pt)) return false;
                Control c = Control.FromChildHandle(WindowFromPoint(pt));
                while (c != null && c != owner) c = c.Parent;
                if (c == null) return false;
                int delta = (short)((long)m.WParam >> 16);
                owner.ScrollByWheel(delta);
                return true;
            }
        }
    }
}
