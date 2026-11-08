using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// 메인 화면: 왼쪽(6) 로그, 오른쪽(4) 설정.
    /// 오른쪽은 위에서부터 서버 주소·포트 → 닉네임 → 게임 실행 → 실험 기능 순서 (멀티플레이에서 중요한 것부터).
    /// 새 설정 묶음은 SettingsCard 를 상속한 카드를 만들어 right.AddCard(...) 에 추가하면 됨 (길어지면 자동 스크롤).
    /// 로그 관련 설정(표시 항목 / 패킷 / 진단)은 로그 창 오른쪽 위 ⚙ 버튼으로 여는 팝업에 있음.
    /// </summary>
    public class MainView : UserControl
    {
        public LogPanel Log { get; private set; }

        public MainView()
        {
            BackColor = UiTheme.Bg;

            var table = new TableLayoutPanel();
            table.Dock = DockStyle.Fill;
            table.BackColor = UiTheme.Bg;
            table.ColumnCount = 2;
            table.RowCount = 1;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F));
            table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            Log = new LogPanel();
            Log.Dock = DockStyle.Fill;
            Log.Margin = new Padding(0, 0, 8, 0);

            var right = new ScrollHost();
            right.Dock = DockStyle.Fill;
            right.Margin = new Padding(8, 0, 0, 0);
            right.AddCard(new ServerCard());
            right.AddCard(new PlayerCard());
            right.AddCard(new LaunchCard());
            right.AddCard(new ExperimentCard());

            table.Controls.Add(Log, 0, 0);
            table.Controls.Add(right, 1, 0);
            Controls.Add(table);
        }
    }

    /// <summary>
    /// 로그 설정 팝업 안의 내용: 카테고리 버튼(표시 항목 / 패킷 로그 / 진단·기타)을 고르면 해당 카드 하나만 보여줌.
    /// </summary>
    public class LogSettingsPanel : UserControl
    {
        /// <summary>닫기(✕) 버튼을 눌렀을 때</summary>
        public event EventHandler CloseRequested;

        public LogSettingsPanel()
        {
            BackColor = UiTheme.Bg;

            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 58;
            header.BackColor = UiTheme.Panel;

            var closeHost = new Panel();
            closeHost.Dock = DockStyle.Right;
            closeHost.Width = 66;
            closeHost.BackColor = Color.Transparent;
            Button btnClose = UiTheme.MakeButton("✕", false, 46, 34);
            btnClose.Font = UiTheme.UiFont(10F);
            btnClose.Location = new Point(10, 12);
            btnClose.Click += (s, e) => { EventHandler h = CloseRequested; if (h != null) h(this, EventArgs.Empty); };
            closeHost.Controls.Add(btnClose);

            var title = new Label();
            title.Text = "로그 설정";
            title.AutoSize = true;
            title.Font = UiTheme.UiFont(13F, FontStyle.Bold);
            title.ForeColor = UiTheme.Text;
            title.BackColor = Color.Transparent;
            title.Location = new Point(20, 6);
            var sub = new Label();
            sub.Text = "로그 창에 보여줄 항목과 패킷 · 진단 옵션이에요.";
            sub.AutoSize = true;
            sub.Font = UiTheme.UiFont(8.5F);
            sub.ForeColor = UiTheme.Muted;
            sub.BackColor = Color.Transparent;
            sub.Location = new Point(22, 34);
            header.Controls.Add(title);
            header.Controls.Add(sub);
            header.Controls.Add(closeHost);

            var body = new Panel();
            body.Dock = DockStyle.Fill;
            body.Padding = new Padding(16, 12, 16, 12);
            body.BackColor = UiTheme.Bg;

            var cats = new CategoryPanel();
            cats.Dock = DockStyle.Fill;
            cats.AddCategory("표시 항목", new LogFilterCard());
            cats.AddCategory("패킷 · 진단", new LogOptionsCard());
            cats.ShowCategory(0);
            body.Controls.Add(cats);

            Controls.Add(body);     // Fill 먼저
            Controls.Add(header);
        }
    }

    /// <summary>
    /// 카테고리 버튼(맨 위)을 고르면 해당 카드 하나만 보여주는 패널.
    /// 설정 묶음이 늘어도 카테고리만 추가하면 됨 (내용이 길면 그 안에서 스크롤).
    /// </summary>
    public class CategoryPanel : UserControl
    {
        private readonly TableLayoutPanel tabs;
        private readonly ScrollHost host;
        private readonly List<Button> buttons = new List<Button>();
        private readonly List<Control> cards = new List<Control>();

        public CategoryPanel()
        {
            BackColor = UiTheme.Bg;

            tabs = new TableLayoutPanel();
            tabs.Dock = DockStyle.Top;
            tabs.Height = 44;
            tabs.RowCount = 1;
            tabs.BackColor = UiTheme.Bg;
            tabs.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            host = new ScrollHost();
            host.Dock = DockStyle.Fill;

            Controls.Add(host);   // Fill 먼저
            Controls.Add(tabs);
        }

        public void AddCategory(string title, Control card)
        {
            int idx = cards.Count;
            cards.Add(card);

            // 보이지 않는 카드도 먼저 이 패널에 붙여 둠: 화면 배율(DPI) 보정은 화면 구성에 붙어 있는 컨트롤에만 적용되므로,
            // 처음에 떼어 두면 크기/간격이 보정되지 않아 UI 가 깨짐 (선택할 때 목록 영역으로 옮겨짐)
            card.Visible = false;
            Controls.Add(card);

            Button b = UiTheme.MakeButton(title, false, 80, 32);
            b.Font = UiTheme.UiFont(9F);
            b.Dock = DockStyle.Fill;
            b.Margin = new Padding(0, 0, 6, 10);
            b.Click += (s, e) => ShowCategory(idx);
            buttons.Add(b);

            tabs.ColumnCount = buttons.Count;
            tabs.ColumnStyles.Clear();
            for (int i = 0; i < buttons.Count; i++)
                tabs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / buttons.Count));
            tabs.Controls.Add(b, idx, 0);
        }

        public void ShowCategory(int idx)
        {
            if (idx < 0 || idx >= cards.Count) return;
            for (int i = 0; i < buttons.Count; i++)
            {
                bool active = i == idx;
                buttons[i].BackColor = active ? UiTheme.Accent : UiTheme.Panel2;
                buttons[i].FlatAppearance.BorderColor = active ? UiTheme.Accent : UiTheme.Border;
            }
            host.ClearCards();
            host.AddCard(cards[idx]);
        }
    }
}
