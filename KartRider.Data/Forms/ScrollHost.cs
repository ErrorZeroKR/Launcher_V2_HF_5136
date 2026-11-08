using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// 카드를 위에서 아래로 쌓아 보여주는 스크롤 영역 (전용 슬림 스크롤바 + 마우스 휠).
    /// 설정이 늘어나도 카드만 추가하면 자동으로 스크롤됨.
    /// </summary>
    public class ScrollHost : Panel
    {
        public readonly Panel Content;
        private readonly ThemedScrollBar bar;
        private int offset;
        private int nextY;

        public ScrollHost()
        {
            BackColor = UiTheme.Bg;
            Content = new Panel();
            Content.BackColor = UiTheme.Bg;
            bar = new ThemedScrollBar(true);
            bar.Scrolled += (s, e) => { offset = bar.Value; Content.Top = -offset; };
            Controls.Add(bar);       // 먼저 추가한 것이 앞에 그려짐
            Controls.Add(Content);
            WheelRouter.Register(this);
        }

        /// <summary>스크롤이 필요한지 (내용이 보이는 영역보다 큰지)</summary>
        public bool CanScroll { get { return Content.Height > ClientSize.Height; } }

        /// <summary>카드를 맨 아래에 추가 (카드 사이 간격 12)</summary>
        public void AddCard(Control card)
        {
            card.Left = 0;
            card.Top = nextY;
            card.Visible = true;
            Content.Controls.Add(card);
            nextY += card.Height + 12;
            Content.Height = nextY - 12;
            LayoutHost();
        }

        /// <summary>카드 높이가 바뀐 뒤(예: 입력란이 나타나거나 사라짐) 카드들을 위에서부터 다시 쌓음</summary>
        public void RelayoutCards()
        {
            var cards = new List<Control>();
            foreach (Control c in Content.Controls) cards.Add(c);
            cards.Sort((a, b) => a.Top.CompareTo(b.Top));
            int gap = (int)Math.Round(12 * DeviceDpi / 96f);
            int yy = 0;
            foreach (Control c in cards) { c.Top = yy; yy += c.Height + gap; }
            Content.Height = Math.Max(0, yy - gap);
            LayoutHost();
        }

        /// <summary>쌓인 카드를 모두 떼어냄 (카드는 없애지 않으므로 다시 AddCard 로 붙일 수 있음)</summary>
        public void ClearCards()
        {
            Content.Controls.Clear();
            nextY = 0;
            offset = 0;
            Content.Height = 0;
            LayoutHost();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutHost();
        }

        private void LayoutHost()
        {
            if (Content == null || bar == null) return;
            bool need = CanScroll;
            bar.Visible = need;
            int barW = bar.Width;
            int w = ClientSize.Width - (need ? barW + barW / 2 : 0);
            Content.Left = 0;
            Content.Width = Math.Max(50, w);
            foreach (Control c in Content.Controls) c.Width = Content.Width;
            bar.SetBounds(ClientSize.Width - barW, 0, barW, ClientSize.Height);
            ScrollTo(offset);
        }

        private void ScrollTo(int value)
        {
            int max = Math.Max(0, Content.Height - ClientSize.Height);
            offset = Math.Max(0, Math.Min(max, value));
            Content.Top = -offset;
            bar.SetState(offset, max, Math.Max(1, ClientSize.Height));
        }

        /// <summary>마우스 휠 (delta: 한 칸 = 120)</summary>
        public void WheelScroll(int delta)
        {
            int step = (int)(56 * DeviceDpi / 96f);
            ScrollTo(offset - delta * step / 120);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) WheelRouter.Unregister(this);
            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// 마우스 휠을 커서 아래의 ScrollHost 로 보냄 (그 안의 숫자 입력칸 등이 휠로 값이 바뀌는 것도 막아 줌).
    /// 아이템 추가 카드가 떠 있는 동안에는 Suspended 로 꺼 둠.
    /// </summary>
    internal sealed class WheelRouter : IMessageFilter
    {
        private static WheelRouter instance;
        private static readonly List<ScrollHost> hosts = new List<ScrollHost>();
        public static bool Suspended;

        public static void Register(ScrollHost h)
        {
            if (instance == null)
            {
                instance = new WheelRouter();
                Application.AddMessageFilter(instance);
            }
            hosts.Add(h);
        }

        public static void Unregister(ScrollHost h)
        {
            hosts.Remove(h);
        }

        public bool PreFilterMessage(ref Message m)
        {
            if (m.Msg != 0x020A || Suspended) return false;   // WM_MOUSEWHEEL
            Point p = Cursor.Position;
            foreach (ScrollHost h in hosts)
            {
                if (!h.IsHandleCreated || !h.Visible || !h.CanScroll) continue;
                if (!h.RectangleToScreen(h.ClientRectangle).Contains(p)) continue;
                int delta = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);
                h.WheelScroll(delta);
                return true;
            }
            return false;
        }
    }
}
