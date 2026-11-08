using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Net;
using System.Windows.Forms;
using Set_Data;

namespace KartRider
{
    /// <summary>
    /// 설정 카드들이 공유하는 저장/동기화 허브.
    /// 한 카드에서 값을 바꾸면 같은 항목을 보여주는 다른 카드도 갱신하고, 잠시 후 Profile\launcher_settings.json 에 자동 저장.
    /// </summary>
    public static class SettingsHub
    {
        /// <summary>sender = 값을 바꾼 카드 (null 이면 모든 카드가 값을 다시 읽음)</summary>
        public static event EventHandler Changed;
        /// <summary>저장 직전에 한 번 발생 (닉네임처럼 설정 파일 밖에 저장하는 항목 처리용)</summary>
        public static event EventHandler Saving;
        private static System.Windows.Forms.Timer timer;

        public static void Touch(object source)
        {
            EventHandler h = Changed;
            if (h != null) h(source, EventArgs.Empty);
            if (timer == null)
            {
                timer = new System.Windows.Forms.Timer();
                timer.Interval = 500;
                timer.Tick += OnTick;
            }
            timer.Stop();
            timer.Start();
        }

        /// <summary>저장하지 않고 모든 카드가 현재 값을 다시 읽게 함 (예: 닉네임을 불러온 뒤)</summary>
        public static void Reload()
        {
            EventHandler h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }

        private static void OnTick(object sender, EventArgs e)
        {
            timer.Stop();
            EventHandler h = Saving;
            if (h != null) h(null, EventArgs.Empty);
            LauncherSettings.Save();
            Console.WriteLine("[설정] 저장됨");
        }
    }

    /// <summary>
    /// 설정 카드 공통 틀: 둥근 카드 + 제목 + 위에서 아래로 쌓는 배치 도우미.
    /// 새 설정 묶음(예: 멀티플레이)은 이 클래스를 상속해서 카드를 만들고 MainView 에 추가하면 됨.
    /// </summary>
    public abstract class SettingsCard : Panel
    {
        protected bool loading;
        protected int y;   // 다음에 배치할 y 위치 (논리 px)
        protected Label LastDesc;   // AddCheck 가 만든 가장 최근 설명 줄 (없으면 null)
        private readonly Label titleLabel;
        private readonly List<Control> stretchers = new List<Control>();

        protected SettingsCard(string title, string subtitle)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Panel;

            titleLabel = new Label();
            titleLabel.Text = title;
            titleLabel.AutoSize = true;
            titleLabel.Font = UiTheme.UiFont(10.5F, FontStyle.Bold);
            titleLabel.ForeColor = UiTheme.Text;
            titleLabel.BackColor = Color.Transparent;
            titleLabel.Location = new Point(16, 12);
            Controls.Add(titleLabel);
            y = 44;

            if (!string.IsNullOrEmpty(subtitle))
            {
                Label sub = MakeLabel(subtitle, 8.5F, UiTheme.Muted);
                sub.Location = new Point(16, 36);
                Controls.Add(sub);
                y = 62;
            }

            SettingsHub.Changed += OnHubChanged;
        }

        /// <summary>화면에 값을 채움 (loading 이 true 인 동안 호출되므로 변경 이벤트가 저장을 일으키지 않음)</summary>
        protected abstract void LoadValues();

        public void Reload()
        {
            loading = true;
            try { LoadValues(); }
            finally { loading = false; }
        }

        private void OnHubChanged(object sender, EventArgs e)
        {
            if (sender == this) return;
            Reload();
        }

        protected void Touch()
        {
            if (!loading) SettingsHub.Touch(this);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) SettingsHub.Changed -= OnHubChanged;
            base.Dispose(disposing);
        }

        // ---------- 그리기 / 폭 맞춤 ----------
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
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Parent != null ? Parent.BackColor : UiTheme.Bg);
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath p = Round(r, 8f))
            using (var br = new SolidBrush(UiTheme.Panel))
            using (var pen = new Pen(UiTheme.Border, 1f))
            {
                g.FillPath(br, p);
                g.DrawPath(pen, p);
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            ApplyStretch();
        }

        /// <summary>카드 폭에 맞춰 늘어나야 하는 컨트롤의 폭을 조정 (오른쪽 여백 = 왼쪽 여백)</summary>
        protected virtual void ApplyStretch()
        {
            if (titleLabel == null) return;
            int m = titleLabel.Left;
            foreach (Control c in stretchers) c.Width = Math.Max(20, ClientSize.Width - c.Left - m);
        }

        protected int Margin16 { get { return titleLabel.Left; } }

        // ---------- 배치 도우미 ----------
        protected static Label MakeLabel(string text, float size, Color color)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = UiTheme.UiFont(size);
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            return l;
        }

        protected void Stretch(Control c)
        {
            stretchers.Add(c);
        }

        /// <summary>체크박스 한 줄 (+ 아래 작은 설명)</summary>
        protected ThemedCheckBox AddCheck(string text, string desc)
        {
            LastDesc = null;
            var c = new ThemedCheckBox();
            c.Text = text;
            c.Location = new Point(16, y);
            Controls.Add(c);
            Stretch(c);
            y += 26;
            if (!string.IsNullOrEmpty(desc))
            {
                Label d = MakeLabel(desc, 8F, UiTheme.Muted);
                d.AutoSize = false;
                d.Size = new Size(300, 16);
                d.Location = new Point(40, y - 3);
                Controls.Add(d);
                Stretch(d);
                LastDesc = d;
                y += 20;
            }
            return c;
        }

        protected Label AddLabel(string text)
        {
            Label l = MakeLabel(text, 8.5F, UiTheme.Muted);
            l.Location = new Point(16, y);
            Controls.Add(l);
            y += 22;
            return l;
        }

        /// <summary>여러 줄 안내 문구 (폭에 맞춰 줄바꿈)</summary>
        protected Label AddNote(string text, int height)
        {
            Label l = MakeLabel(text, 8.5F, UiTheme.Muted);
            l.AutoSize = false;
            l.Size = new Size(300, height);
            l.Location = new Point(16, y);
            Controls.Add(l);
            Stretch(l);
            y += height + 4;
            return l;
        }

        /// <summary>캡션 + 런처 디자인 입력란 (카드 폭에 맞춰 늘어남)</summary>
        protected ThemedTextBox AddInput(string caption)
        {
            if (!string.IsNullOrEmpty(caption)) AddLabel(caption);
            var t = new ThemedTextBox();
            t.Location = new Point(16, y);
            t.Size = new Size(300, 36);
            Controls.Add(t);
            Stretch(t);
            y += 46;
            return t;
        }

        /// <summary>카드 안의 소제목 (첫 번째가 아니면 위에 구분선)</summary>
        protected void AddSection(string text)
        {
            if (y > 66)
            {
                var line = new Panel();
                line.BackColor = UiTheme.Border;
                line.Size = new Size(300, 1);
                line.Location = new Point(16, y + 2);
                Controls.Add(line);
                Stretch(line);
                y += 16;
            }
            Label l = MakeLabel(text, 9F, UiTheme.Accent);
            l.Font = UiTheme.UiFont(9F, FontStyle.Bold);
            l.Location = new Point(16, y);
            Controls.Add(l);
            y += 28;
        }

        // 접어 둔(숨긴) 줄: 높이 계산에서 제외. Visible 은 부모가 안 보이면 false 로 나오므로 따로 기억함
        private readonly HashSet<Control> hiddenRows = new HashSet<Control>();

        /// <summary>줄(컨트롤 묶음)을 보이거나 숨기고, 카드 높이와 아래 카드 위치를 다시 맞춤</summary>
        protected void SetRowsVisible(Control[] rows, bool visible)
        {
            foreach (Control r in rows)
            {
                r.Visible = visible;
                if (visible) hiddenRows.Remove(r); else hiddenRows.Add(r);
            }
            RefreshHeight();
            RequestRelayout();
        }

        /// <summary>카드 높이를 맨 아래 컨트롤 기준으로 다시 계산 (아래 여백 14 — 위 12 · 좌우 16 과 균형)</summary>
        protected void RefreshHeight()
        {
            int bottom = 0;
            foreach (Control child in Controls)
                if (!hiddenRows.Contains(child)) bottom = Math.Max(bottom, child.Bottom);
            Height = bottom + 14;
        }

        /// <summary>카드 높이가 바뀐 것을 목록(ScrollHost)에 알려서 아래 카드가 같이 밀리게 함</summary>
        protected void RequestRelayout()
        {
            for (Control p = Parent; p != null; p = p.Parent)
            {
                ScrollHost host = p as ScrollHost;
                if (host != null) { host.RelayoutCards(); return; }
            }
        }

        /// <summary>배치를 끝내고 카드 높이를 확정</summary>
        protected void Finish()
        {
            RefreshHeight();
        }
    }

    // =====================================================================
    //  메인 화면 오른쪽: 서버 / 닉네임 / 게임 실행 / 실험 기능
    // =====================================================================

    /// <summary>서버 주소와 포트 (멀티플레이에서 가장 중요한 접속 정보)</summary>
    public sealed class ServerCard : SettingsCard
    {
        private readonly ThemedTextBox txtIp;
        private readonly ThemedNumberBox numPort;
        private readonly Label lblIp, lblPort, lblNote;
        private readonly Control[] noteRows;
        private readonly System.Windows.Forms.Timer applyTimer;

        public ServerCard() : base("서버", "멀티플레이 접속 정보 · 바꾸면 서버를 바로 다시 시작해요")
        {
            lblIp = MakeLabel("서버 주소", 8.5F, UiTheme.Muted);
            lblIp.Location = new Point(16, y);
            Controls.Add(lblIp);
            lblPort = MakeLabel("포트", 8.5F, UiTheme.Muted);
            lblPort.Location = new Point(240, y);
            Controls.Add(lblPort);
            y += 22;

            txtIp = new ThemedTextBox();
            txtIp.Location = new Point(16, y);
            txtIp.Size = new Size(210, 36);
            txtIp.PlaceholderText = "127.0.0.1";
            Controls.Add(txtIp);
            numPort = new ThemedNumberBox();
            numPort.Minimum = 1;
            numPort.Maximum = 65535;
            numPort.Location = new Point(236, y);
            numPort.Size = new Size(124, 36);
            Controls.Add(numPort);
            y += 46;

            // 적용 실패 안내 (실패했을 때만 보이는 줄 — 카드의 맨 아래라서 나타나고 사라져도 다른 줄이 밀리지 않음)
            lblNote = MakeLabel("", 8F, UiTheme.Warn);
            lblNote.AutoSize = false;
            lblNote.Size = new Size(300, 32);
            lblNote.Location = new Point(16, y);
            Controls.Add(lblNote);
            Stretch(lblNote);
            y += 36;
            noteRows = new Control[] { lblNote };
            SetRowsVisible(noteRows, false);
            Finish();

            applyTimer = new System.Windows.Forms.Timer();
            applyTimer.Interval = 800;
            applyTimer.Tick += (s, e) => { applyTimer.Stop(); ApplyServer(); };

            txtIp.TextChanged += (s, e) =>
            {
                if (loading) return;
                IPAddress ip;
                bool ok = IPAddress.TryParse(txtIp.Text.Trim(), out ip);
                txtIp.Invalid = !ok;
                if (ok) { LauncherSettings.Current.ServerIP = txtIp.Text.Trim(); Touch(); QueueApply(); }
            };
            numPort.ValueChanged += (s, e) => { if (loading) return; LauncherSettings.Current.ServerPort = numPort.Value; Touch(); QueueApply(); };
            Reload();
        }

        /// <summary>입력을 멈추고 잠시 뒤에 서버를 다시 시작 (타이핑 중간 값으로 계속 다시 시작하지 않도록)</summary>
        private void QueueApply()
        {
            applyTimer.Stop();
            applyTimer.Start();
        }

        private void ApplyServer()
        {
            string ip = LauncherSettings.Current.ServerIP;
            int port = LauncherSettings.Current.ServerPort;
            if (RouterListener.Running && RouterListener.sIP == ip && RouterListener.port == port)
            {
                SetRowsVisible(noteRows, false);
                return;
            }
            string err;
            if (RouterListener.Restart(ip, port, out err))
            {
                SetRowsVisible(noteRows, false);
                return;
            }
            lblNote.Text = "이 주소로 서버를 시작하지 못했어요 (" + err + ") · "
                + (RouterListener.Running ? "이전 주소 " + RouterListener.sIP + ":" + RouterListener.port + " 로 계속 실행 중이에요" : "서버가 꺼져 있어요");
            SetRowsVisible(noteRows, true);
        }

        /// <summary>포트 입력란은 오른쪽에 고정 폭으로 두고 주소 입력란이 나머지 폭을 차지</summary>
        protected override void ApplyStretch()
        {
            base.ApplyStretch();
            if (txtIp == null || numPort == null || lblPort == null) return;
            int m = Margin16;
            int gap = Math.Max(6, m / 2);
            numPort.Left = ClientSize.Width - m - numPort.Width;
            txtIp.Width = Math.Max(60, numPort.Left - gap - txtIp.Left);
            lblPort.Left = numPort.Left + 2;
        }

        protected override void LoadValues()
        {
            LauncherSettings s = LauncherSettings.Current;
            txtIp.Text = s.ServerIP;
            txtIp.Invalid = false;
            numPort.Value = s.ServerPort;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && applyTimer != null) applyTimer.Dispose();
            base.Dispose(disposing);
        }
    }

    /// <summary>닉네임 (게임 프로필 파일에 저장)</summary>
    public sealed class PlayerCard : SettingsCard
    {
        private readonly ThemedTextBox txtNick;

        public PlayerCard() : base("닉네임", null)
        {
            txtNick = AddInput("플레이어 닉네임 (최대 16자)");
            txtNick.MaxLength = 16;
            txtNick.PlaceholderText = "닉네임";
            Finish();

            txtNick.TextChanged += (s, e) => { if (loading) return; SettingsHub.Touch(this); };
            SettingsHub.Saving += ApplyNick;
            Reload();
        }

        protected override void LoadValues()
        {
            txtNick.Text = SetRider.Nickname ?? "";
        }

        private void ApplyNick(object sender, EventArgs e)
        {
            string nick = txtNick.Text.Trim();
            if (nick.Length > 0 && nick.Length <= 16 && nick != SetRider.Nickname)
            {
                SetRider.Nickname = nick;
                SetRider.Save_SetRider();
                Console.WriteLine("[설정] 닉네임 변경: " + nick);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) SettingsHub.Saving -= ApplyNick;
            base.Dispose(disposing);
        }
    }

    /// <summary>게임 실행 방식</summary>
    public sealed class LaunchCard : SettingsCard
    {
        private readonly ThemedCheckBox chkNgs, chkSec, chkDatapack;
        private readonly ThemedTextBox txtPath;
        private readonly Label lblNote;
        private readonly Control[] pathRows;
        private readonly System.Windows.Forms.Timer applyTimer;
        private bool pendingOn;   // 체크는 했지만 아직 올바른 폴더가 없어 적용 전인 상태

        public LaunchCard() : base("게임 실행", null)
        {
            chkNgs = AddCheck("PIN: NgsOn 제거", "게임을 시작할 때 적용");
            chkSec = AddCheck("PIN: secModule 제거", "게임을 시작할 때 적용");
            // KartRider.xml 의 datapackOff 사용 여부 (켜면 아래에 읽을 폴더 입력란이 나타남)
            chkDatapack = AddCheck("datapackOff 활성화", "켜면 아래 폴더에서, 끄면 Data(aaa.pk)에서 읽어요. 바꾸면 바로 다시 읽어요");
            LastDesc.Height = 32;   // 설명이 두 줄이라 한 줄 더 확보
            y += 16;

            // 켰을 때만 보이는 줄: 읽을 폴더 입력란 + 상태 안내 (카드의 맨 아래라서 나타나고 사라져도 다른 줄이 밀리지 않음)
            Label lblPath = MakeLabel("읽을 폴더 (상대 또는 전체 경로)", 8.5F, UiTheme.Muted);
            lblPath.Location = new Point(16, y);
            Controls.Add(lblPath);
            y += 22;
            txtPath = new ThemedTextBox();
            txtPath.Location = new Point(16, y);
            txtPath.Size = new Size(300, 36);
            txtPath.PlaceholderText = "DataRaw";
            Controls.Add(txtPath);
            Stretch(txtPath);
            y += 44;
            lblNote = MakeLabel("", 8F, UiTheme.Muted);
            lblNote.AutoSize = false;
            lblNote.Size = new Size(300, 32);
            lblNote.Location = new Point(16, y);
            Controls.Add(lblNote);
            Stretch(lblNote);
            y += 36;
            pathRows = new Control[] { lblPath, txtPath, lblNote };
            SetRowsVisible(pathRows, false);
            Finish();

            applyTimer = new System.Windows.Forms.Timer();
            applyTimer.Interval = 700;
            applyTimer.Tick += (s, e) => { applyTimer.Stop(); ApplyPath(); };

            chkNgs.CheckedChanged += (s, e) => { if (loading) return; LauncherSettings.Current.RemoveNgsOn = chkNgs.Checked; Touch(); };
            chkSec.CheckedChanged += (s, e) => { if (loading) return; LauncherSettings.Current.RemoveSecModule = chkSec.Checked; Touch(); };
            chkDatapack.CheckedChanged += (s, e) => { if (loading) return; OnToggle(chkDatapack.Checked); };
            txtPath.TextChanged += (s, e) =>
            {
                if (loading || !chkDatapack.Checked) return;
                txtPath.Invalid = false;
                applyTimer.Stop();
                applyTimer.Interval = 700;   // 입력을 멈추고 잠시 뒤에 적용
                applyTimer.Start();
            };
            KartRhoFile.SourceChanged += OnSourceChanged;
            Reload();
        }

        private void SetNote(string text, Color color)
        {
            lblNote.Text = text;
            lblNote.ForeColor = color;
        }

        private void RevertToggle(bool on)
        {
            loading = true;
            try { chkDatapack.Checked = on; }
            finally { loading = false; }
            SetRowsVisible(pathRows, on);
        }

        /// <summary>체크를 바꾸면: 켜면 폴더 입력란을 보여주고 적용, 끄면 datapackOff 를 빼고 Data 로 다시 읽음</summary>
        private void OnToggle(bool on)
        {
            if (on)
            {
                pendingOn = true;
                if (string.IsNullOrWhiteSpace(txtPath.Text))
                {
                    loading = true;
                    try { txtPath.Text = KartRhoFile.RememberedPath(); }   // 이전에 쓰던 경로 (없으면 DataRaw)
                    finally { loading = false; }
                }
                SetRowsVisible(pathRows, true);
                ApplyPath();
            }
            else
            {
                applyTimer.Stop();
                if (KartRhoFile.Status == "읽는 중")
                {
                    RevertToggle(true);
                    SetNote("게임 데이터를 읽는 중이에요. 끝난 뒤에 바꿔 주세요", UiTheme.Warn);
                    return;
                }
                string gameDir = KartRhoFile.GameDir;
                if (!string.IsNullOrEmpty(gameDir))
                {
                    string msg;
                    if (!KartRhoFile.SetDatapackOff(gameDir, false, null, out msg))
                    {
                        RevertToggle(true);
                        SetNote(msg, UiTheme.Err);
                        return;
                    }
                    pendingOn = false;
                    Console.WriteLine("[설정] datapackOff 끔 → Data 방식 ({0})", msg);
                    WarnIfGameRunning();
                    SetRowsVisible(pathRows, false);
                    KartRhoFile.Reload(gameDir);
                    return;
                }
                pendingOn = false;
                SetRowsVisible(pathRows, false);
            }
        }

        /// <summary>입력한 폴더가 있으면 KartRider.xml 의 datapackOff path 를 그 값으로 하고 데이터를 다시 읽음</summary>
        private void ApplyPath()
        {
            if (!chkDatapack.Checked) return;
            string gameDir = KartRhoFile.GameDir;
            string p = txtPath.Text.Trim();
            if (string.IsNullOrEmpty(gameDir)) { SetNote("게임 폴더를 확인하지 못했어요", UiTheme.Err); return; }
            if (p.Length == 0) { txtPath.Invalid = true; SetNote("읽을 폴더를 입력해 주세요", UiTheme.Warn); return; }

            string full;
            try { full = System.IO.Path.IsPathRooted(p) ? p : System.IO.Path.GetFullPath(System.IO.Path.Combine(gameDir, p)); }
            catch { full = null; }
            if (full == null || !System.IO.Directory.Exists(full))
            {
                // 폴더가 없으면 KartRider.xml 은 건드리지 않음 (게임이 데이터를 못 찾게 되는 것을 막음)
                txtPath.Invalid = true;
                SetNote("폴더를 찾을 수 없어요: " + (full ?? p) + " — 올바른 폴더를 입력하면 적용돼요", UiTheme.Warn);
                return;
            }
            txtPath.Invalid = false;

            if (KartRhoFile.Status == "읽는 중")
            {
                SetNote("게임 데이터를 읽는 중이에요. 끝나면 적용할게요", UiTheme.Muted);
                applyTimer.Interval = 1000;
                applyTimer.Start();
                return;
            }

            // 이미 이 폴더로 읽는 중이면 다시 읽지 않음
            string cur = KartRhoFile.GetDatapackOffPath(gameDir);
            if (cur != null && string.Equals(cur, p, StringComparison.OrdinalIgnoreCase)
                && KartRhoFile.CurrentSource != null && KartRhoFile.CurrentSource.Kind == KartRhoFile.DataSourceKind.Folder)
            {
                pendingOn = false;
                UpdateNote(KartRhoFile.CurrentSource);
                return;
            }

            string msg;
            if (!KartRhoFile.SetDatapackOff(gameDir, true, p, out msg)) { SetNote(msg, UiTheme.Err); return; }
            pendingOn = false;
            Console.WriteLine("[설정] datapackOff 켬 → 폴더 '{0}' ({1})", p, msg);
            WarnIfGameRunning();
            KartRhoFile.Reload(gameDir);   // 안내 문구는 SourceChanged 로 갱신됨
        }

        private static void WarnIfGameRunning()
        {
            if (System.Diagnostics.Process.GetProcessesByName("KartRider").Length > 0)
                Console.WriteLine("[설정] 게임이 실행 중이라 게임에는 다시 실행해야 적용돼요 (런처의 스펙 데이터는 바로 바뀌어요)");
        }

        private void OnSourceChanged(KartRhoFile.GameDataSource src)
        {
            UpdateNote(src);
        }

        private void UpdateNote(KartRhoFile.GameDataSource src)
        {
            if (!chkDatapack.Checked) return;
            if (src != null && src.Kind == KartRhoFile.DataSourceKind.Folder)
            {
                txtPath.Invalid = false;
                SetNote("이 폴더에서 읽고 있어요 · 게임은 다시 실행하면 적용돼요", UiTheme.Muted);
            }
            else if (src != null && !string.IsNullOrEmpty(src.Message) && src.Message.Contains("폴더를 찾을 수 없어"))
                SetNote("폴더를 찾을 수 없어 Data 로 읽고 있어요", UiTheme.Warn);
            else if (!pendingOn)
                SetNote("", UiTheme.Muted);
        }

        protected override void LoadValues()
        {
            LauncherSettings s = LauncherSettings.Current;
            string gameDir = KartRhoFile.GameDir;
            bool xmlOn = !string.IsNullOrEmpty(gameDir)
                ? KartRhoFile.IsRawModeEnabled(gameDir)
                : (KartRhoFile.CurrentSource != null && KartRhoFile.CurrentSource.Kind == KartRhoFile.DataSourceKind.Folder);
            bool on = xmlOn || pendingOn;
            chkDatapack.Checked = on;
            if (on && !applyTimer.Enabled)
            {
                string p = xmlOn ? KartRhoFile.GetDatapackOffPath(gameDir) : null;
                if (!string.IsNullOrEmpty(p) && txtPath.Text != p) txtPath.Text = p;
                else if (string.IsNullOrEmpty(txtPath.Text)) txtPath.Text = KartRhoFile.RememberedPath();
            }
            SetRowsVisible(pathRows, on);
            UpdateNote(KartRhoFile.CurrentSource);
            chkNgs.Checked = s.RemoveNgsOn;
            chkSec.Checked = s.RemoveSecModule;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                KartRhoFile.SourceChanged -= OnSourceChanged;
                if (applyTimer != null) applyTimer.Dispose();
            }
            base.Dispose(disposing);
        }
    }

    /// <summary>(실험) 기능: 아직 쓰지 않거나 검증 중인 기능</summary>
    public sealed class ExperimentCard : SettingsCard
    {
        private const string NormalDesc = "읽기만 하고 아직 사용은 안 함 · 바꾸면 바로 다시 읽어요";
        private readonly ThemedCheckBox chkDocs;
        private readonly Label lblDesc;

        public ExperimentCard() : base("실험 기능", "검증 중인 기능이에요. 필요할 때만 켜세요")
        {
            chkDocs = AddCheck("퀘스트·상점·이벤트 문서 읽기 (실험)", NormalDesc);
            lblDesc = LastDesc;
            Finish();

            chkDocs.CheckedChanged += (s, e) =>
            {
                if (loading) return;
                if (KartRhoFile.Status == "읽는 중")
                {
                    // 읽는 도중에는 바꿀 수 없으니 되돌림
                    loading = true;
                    try { chkDocs.Checked = !chkDocs.Checked; }
                    finally { loading = false; }
                    ShowDesc("게임 데이터를 읽는 중이에요. 끝난 뒤에 바꿔 주세요", UiTheme.Warn);
                    return;
                }
                LauncherSettings.Current.LoadContentDocs = chkDocs.Checked;
                Touch();
                ShowDesc(NormalDesc, UiTheme.Muted);
                if (!string.IsNullOrEmpty(KartRhoFile.GameDir)) KartRhoFile.Reload(KartRhoFile.GameDir);   // 바로 다시 읽음
            };
            Reload();
        }

        private void ShowDesc(string text, Color color)
        {
            lblDesc.Text = text;
            lblDesc.ForeColor = color;
        }

        protected override void LoadValues()
        {
            chkDocs.Checked = LauncherSettings.Current.LoadContentDocs;
            ShowDesc(NormalDesc, UiTheme.Muted);
        }
    }

    // =====================================================================
    //  로그 설정 팝업 카드 (표시 항목 / 패킷 로그 / 진단·기타)
    // =====================================================================

    /// <summary>패킷 로그 + 진단·기타를 한 카드에 모은 로그 옵션</summary>
    public sealed class LogOptionsCard : SettingsCard
    {
        private readonly ThemedComboBox cmbMode;
        private readonly ThemedNumberBox numHex, numKeep;
        private System.Windows.Forms.Timer pruneTimer;
        private readonly ThemedCheckBox chkSent, chkPinLog, chkConsole;

        public LogOptionsCard() : base("패킷 · 진단", "바꾸면 바로 적용돼요")
        {
            // ----- 패킷 로그 -----
            AddSection("패킷 로그");
            cmbMode = new ThemedComboBox();
            cmbMode.Location = new Point(16, y);
            cmbMode.Size = new Size(150, 36);
            cmbMode.Items.Add("끔");
            cmbMode.Items.Add("이름만");
            cmbMode.Items.Add("이름 + 헥스");
            Controls.Add(cmbMode);
            numHex = new ThemedNumberBox();
            numHex.Minimum = 16;
            numHex.Maximum = 1024;
            numHex.Increment = 16;
            numHex.Location = new Point(176, y);
            numHex.Size = new Size(110, 36);
            Controls.Add(numHex);
            Label unit = MakeLabel("바이트", 8.5F, UiTheme.Muted);
            unit.Location = new Point(294, y + 10);
            Controls.Add(unit);
            y += 46;
            chkSent = AddCheck("송신 패킷도 표시", null);
            AddNote("게임이 서버에 접속하지 못하면 패킷이 하나도 나오지 않아요. 로그 창에서 숨기려면 '표시 항목'에서 '패킷 송수신'을 끄세요.", 48);

            // ----- 진단 · 기타 -----
            AddSection("진단 · 기타");
            chkPinLog = AddCheck("PIN 내용을 로그에 출력", "게임을 시작할 때 적용");
            chkConsole = AddCheck("콘솔 창 표시", "별도 콘솔 창에도 로그를 같이 보여줘요");
            y += 4;
            AddLabel("로그 파일 보관 개수 (오래된 파일은 바로 정리)");
            numKeep = new ThemedNumberBox();
            numKeep.Minimum = 1;
            numKeep.Maximum = 500;
            numKeep.Location = new Point(16, y);
            numKeep.Size = new Size(124, 36);
            Controls.Add(numKeep);
            y += 46;
            Finish();

            cmbMode.SelectedIndexChanged += (s, e) => { if (loading) return; LauncherSettings.Current.PacketLogMode = Math.Max(0, cmbMode.SelectedIndex); Touch(); };
            numHex.ValueChanged += (s, e) => { if (loading) return; LauncherSettings.Current.PacketHexBytes = numHex.Value; Touch(); };
            chkSent.CheckedChanged += (s, e) => { if (loading) return; LauncherSettings.Current.PacketLogSent = chkSent.Checked; Touch(); };
            chkPinLog.CheckedChanged += (s, e) => { if (loading) return; LauncherSettings.Current.PinLog = chkPinLog.Checked; Touch(); };
            chkConsole.CheckedChanged += (s, e) => { if (loading) return; LauncherSettings.Current.ShowConsole = chkConsole.Checked; Touch(); AppLog.SetConsoleVisible(chkConsole.Checked); };
            pruneTimer = new System.Windows.Forms.Timer();
            pruneTimer.Interval = 1200;   // 숫자를 다 입력한 뒤에 정리 (입력 도중의 값으로 로그가 지워지지 않도록)
            pruneTimer.Tick += (s, e) =>
            {
                pruneTimer.Stop();
                int n = AppLog.PruneOldLogs();
                if (n > 0) Console.WriteLine("[로그] 오래된 로그 {0}개를 정리했어요 (보관 {1}개)", n, LauncherSettings.Current.LogKeepCount);
            };
            numKeep.ValueChanged += (s, e) => { if (loading) return; LauncherSettings.Current.LogKeepCount = numKeep.Value; Touch(); pruneTimer.Stop(); pruneTimer.Start(); };
            Reload();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && pruneTimer != null) pruneTimer.Dispose();
            base.Dispose(disposing);
        }

        protected override void LoadValues()
        {
            LauncherSettings s = LauncherSettings.Current;
            cmbMode.SelectedIndex = Math.Max(0, Math.Min(2, s.PacketLogMode));
            numHex.Value = s.PacketHexBytes;
            chkSent.Checked = s.PacketLogSent;
            chkPinLog.Checked = s.PinLog;
            chkConsole.Checked = s.ShowConsole;
            numKeep.Value = s.LogKeepCount;
        }
    }

    /// <summary>로그 설정 팝업: 로그 창에 보여줄 항목 선택</summary>
    public sealed class LogFilterCard : SettingsCard
    {
        private readonly ThemedCheckBox[] chk = new ThemedCheckBox[LogCat.Count];

        public LogFilterCard() : base("로그 표시 항목", "끄면 로그 창에서만 숨겨요 · 로그 파일에는 모두 저장돼요")
        {
            int top = y;
            for (int i = 0; i < LogCat.Count; i++)
            {
                int idx = i;
                var c = new ThemedCheckBox();
                c.Text = LogCat.Names[i];
                c.Size = new Size(160, 24);
                c.Location = new Point(16 + (i % 2) * 170, top + (i / 2) * 28);
                if (i == LogCat.Error) c.Enabled = false;   // 오류는 항상 표시
                Controls.Add(c);
                chk[i] = c;
                if (i > 0)
                    c.CheckedChanged += (s, e) =>
                    {
                        if (loading) return;
                        LogCat.SetHidden(idx, !chk[idx].Checked);
                        SettingsHub.Touch(this);
                    };
            }
            y = top + ((LogCat.Count + 1) / 2) * 28 + 6;

            Button bDefault = UiTheme.MakeButton("기본값", false, 90, 28);
            bDefault.Font = UiTheme.UiFont(8.5F);
            bDefault.Location = new Point(16, y);
            bDefault.Click += (s, e) => { LogCat.SetMask(LogCat.DefaultHiddenMask); SettingsHub.Touch(this); };
            Button bAll = UiTheme.MakeButton("모두 보기", false, 90, 28);
            bAll.Font = UiTheme.UiFont(8.5F);
            bAll.Location = new Point(112, y);
            bAll.Click += (s, e) => { LogCat.SetMask(0); SettingsHub.Touch(this); };
            Controls.Add(bDefault);
            Controls.Add(bAll);
            y += 38;
            AddNote("스펙 상세값: 카트 성능 수십 줄 / 장착·파츠: 장착 아이템·파츠 값 / 주행 상태: PlaneCheck 반복 줄", 36);
            Finish();

            LogCat.Changed += OnLogCatChanged;
            Reload();
        }

        private void OnLogCatChanged(object sender, EventArgs e)
        {
            Reload();
        }

        protected override void LoadValues()
        {
            for (int i = 0; i < LogCat.Count; i++) chk[i].Checked = !LogCat.IsHidden(i);
        }

        /// <summary>폭에 맞춰 체크박스를 2열로 배치</summary>
        protected override void ApplyStretch()
        {
            base.ApplyStretch();
            if (chk[0] == null) return;
            int m = Margin16;
            int colW = Math.Max(60, (ClientSize.Width - m * 2) / 2);
            for (int i = 0; i < LogCat.Count; i++)
            {
                if (chk[i] == null) continue;
                chk[i].Width = colW - 4;
                chk[i].Left = m + (i % 2) * colW;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) LogCat.Changed -= OnLogCatChanged;
            base.Dispose(disposing);
        }
    }
}
