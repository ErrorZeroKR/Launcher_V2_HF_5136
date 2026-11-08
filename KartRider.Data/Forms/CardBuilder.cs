using System;
using System.Drawing;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>설정 카드(제목 + 항목 줄들)를 만드는 도우미. 만든 뒤 ScrollPanel.AddCard(card.Root) 로 추가.</summary>
    public sealed class CardBuilder
    {
        public readonly Panel Root = new Panel();
        public readonly TableLayoutPanel Grid = new TableLayoutPanel();
        private readonly int headerHeight;
        private int row;

        public CardBuilder(string title, string subtitle)
        {
            Root.BackColor = UiTheme.Panel;
            Root.Padding = new Padding(18, 14, 18, 14);

            Grid.Dock = DockStyle.Top;
            Grid.AutoSize = true;
            Grid.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Grid.BackColor = Color.Transparent;
            Grid.Margin = Padding.Empty;
            Grid.ColumnCount = 2;
            Grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            Grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Root.Controls.Add(Grid);   // 먼저 추가 (제목이 그 위에 쌓이도록)

            int h = 0;
            if (!string.IsNullOrEmpty(subtitle))
            {
                var sub = new Label();
                sub.Text = subtitle;
                sub.Dock = DockStyle.Top;
                sub.AutoSize = false;
                sub.Height = 22;
                sub.Font = UiTheme.UiFont(8.5F);
                sub.ForeColor = UiTheme.Muted;
                sub.BackColor = Color.Transparent;
                Root.Controls.Add(sub);
                h += 22;
            }
            var head = new Label();
            head.Text = title;
            head.Dock = DockStyle.Top;
            head.AutoSize = false;
            head.Height = 28;
            head.Font = UiTheme.UiFont(10.5F, FontStyle.Bold);
            head.ForeColor = UiTheme.Text;
            head.BackColor = Color.Transparent;
            Root.Controls.Add(head);
            h += 28;
            headerHeight = h;

            // 항목이 늘어나면 카드 높이도 따라서 늘어남
            Root.Height = Root.Padding.Vertical + headerHeight + 4;
            Grid.SizeChanged += (s, e) => { Root.Height = Root.Padding.Vertical + headerHeight + Grid.Height + 2; };
        }

        public int NextRow()
        {
            Grid.RowCount = row + 1;
            Grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            return row++;
        }

        /// <summary>체크박스 한 줄 + (있으면) 그 아래 작은 설명</summary>
        public CheckBox Check(string text, string desc)
        {
            var c = new CheckBox();
            c.Text = text;
            c.AutoSize = true;
            c.Font = UiTheme.UiFont(9.5F);
            c.ForeColor = UiTheme.Text;
            c.BackColor = Color.Transparent;
            c.Margin = new Padding(0, 8, 0, 0);
            int r = NextRow();
            Grid.Controls.Add(c, 0, r);
            Grid.SetColumnSpan(c, 2);
            if (!string.IsNullOrEmpty(desc)) Note(desc, 24);
            return c;
        }

        /// <summary>작은 설명 줄 (길면 … 으로 잘림)</summary>
        public Label Note(string text, int leftIndent)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = false;
            l.Height = 20;
            l.Dock = DockStyle.Fill;
            l.AutoEllipsis = true;
            l.Font = UiTheme.UiFont(8.5F);
            l.ForeColor = UiTheme.Muted;
            l.BackColor = Color.Transparent;
            l.Margin = new Padding(leftIndent, 0, 0, 0);
            int r = NextRow();
            Grid.Controls.Add(l, 0, r);
            Grid.SetColumnSpan(l, 2);
            return l;
        }

        /// <summary>눈에 띄는 안내 줄 (처음엔 숨김)</summary>
        public Label Notice(string text, Color color)
        {
            Label l = Note(text, 0);
            l.ForeColor = color;
            l.Font = UiTheme.UiFont(8.5F, FontStyle.Bold);
            l.Visible = false;
            return l;
        }

        /// <summary>왼쪽 이름 + 오른쪽 입력 컨트롤 한 줄</summary>
        public void Row(string labelText, Control c)
        {
            var l = new Label();
            l.Text = labelText;
            l.AutoSize = true;
            l.Font = UiTheme.UiFont(9F);
            l.ForeColor = UiTheme.Muted;
            l.BackColor = Color.Transparent;
            l.Anchor = AnchorStyles.Left;
            l.Margin = new Padding(0, 8, 16, 0);
            c.Margin = new Padding(0, 8, 0, 0);
            int r = NextRow();
            Grid.Controls.Add(l, 0, r);
            Grid.Controls.Add(c, 1, r);
        }
    }
}
