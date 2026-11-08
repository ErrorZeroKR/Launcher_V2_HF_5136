using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// 런처 로그 창. 마우스 드래그로 선택/복사 가능, 색상 구분, 자동 줄바꿈, 자동 스크롤, 전용 스크롤바.
    /// 항목별 표시/숨김(설정 > 로그 표시)은 화면에만 적용되고 로그 파일에는 모든 줄이 저장됨.
    /// </summary>
    public class LogPanel : UserControl
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, ref Point lParam);

        private const int WM_SETREDRAW = 0x000B;
        private const int EM_GETSCROLLPOS = 0x0400 + 221;
        private const int EM_SETSCROLLPOS = 0x0400 + 222;
        private const int MaxLines = 6000;      // 화면에 이 줄 수를 넘으면
        private const int KeepLines = 5000;     // 이만큼만 남기고 오래된 줄 삭제
        private const int MaxHistory = 20000;   // 숨긴 줄도 다시 보여줄 수 있게 메모리에 보관하는 최대 줄 수
        private const int KeepHistory = 15000;

        private sealed class Entry
        {
            public string Ts;
            public string Msg;
            public int Cat;
            public Color Color;
        }

        private readonly RichTextBox rtb;
        private readonly Panel viewHost;
        private readonly ThemedScrollBar vbar;
        private readonly CheckBox chkAuto;
        private readonly ToolTip tip;

        /// <summary>툴바 오른쪽 끝의 ⚙ 버튼을 눌렀을 때 (로그 설정 팝업을 열기 위해)</summary>
        public event EventHandler SettingsRequested;
        private readonly System.Windows.Forms.Timer timer;
        private readonly List<Entry> history = new List<Entry>();
        private readonly LogClassifier classifier = new LogClassifier();
        private Entry pendingDash;       // 값 목록을 여는 구분선: 어느 항목인지 다음 줄에서 정해지므로 잠시 보류
        private int lineCount;
        private bool busy;               // 프로그램이 스크롤/갱신하는 동안에는 사용자 스크롤로 취급하지 않음
        private Color trackColor = UiTheme.Text;

        public LogPanel()
        {
            BackColor = UiTheme.Border;
            Padding = new Padding(1);

            rtb = new RichTextBox();
            rtb.ReadOnly = true;
            rtb.BorderStyle = BorderStyle.None;
            rtb.BackColor = Color.FromArgb(11, 15, 20);
            rtb.ForeColor = UiTheme.Text;
            rtb.Font = UiTheme.MonoFont(9.5F);
            rtb.WordWrap = true;                                   // 가로로 길어지지 않게 자동 줄바꿈
            rtb.DetectUrls = false;
            rtb.HideSelection = false;
            rtb.ScrollBars = RichTextBoxScrollBars.ForcedVertical; // 기본 스크롤바는 화면 밖으로 숨기고 전용 스크롤바 사용
            // 마우스로 드래그해서 선택하면 자동 스크롤을 잠시 멈춤 (선택이 풀리지 않도록)
            rtb.MouseUp += (s, e) => { if (rtb.SelectionLength > 0 && chkAuto != null) chkAuto.Checked = false; };
            rtb.VScroll += (s, e) => OnViewScrolled();

            vbar = new ThemedScrollBar(true);
            vbar.Scrolled += (s, e) =>
            {
                busy = true;
                try
                {
                    Point p = new Point(0, vbar.Value);
                    SendMessage(rtb.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref p);
                }
                finally { busy = false; }
                if (chkAuto != null) chkAuto.Checked = vbar.AtEnd; // 맨 아래면 자동 스크롤 재개
            };

            viewHost = new Panel();
            viewHost.Dock = DockStyle.Fill;
            viewHost.BackColor = Color.FromArgb(11, 15, 20);
            viewHost.Controls.Add(vbar);   // 먼저 추가한 것이 앞에 그려짐
            viewHost.Controls.Add(rtb);
            viewHost.Resize += (s, e) => { LayoutView(); UpdateThumb(); };

            var menu = new ContextMenuStrip();
            menu.Items.Add("선택 복사", null, (s, e) => CopySelection());
            menu.Items.Add("전체 복사", null, (s, e) => CopyAll());
            menu.Items.Add("모두 선택", null, (s, e) => rtb.SelectAll());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("지우기", null, (s, e) => ClearLog());
            rtb.ContextMenuStrip = menu;

            var bar = new Panel();
            bar.Dock = DockStyle.Top;
            bar.Height = 40;
            bar.BackColor = UiTheme.Panel;

            var left = new FlowLayoutPanel();
            left.Dock = DockStyle.Fill;
            left.BackColor = Color.Transparent;
            left.WrapContents = false;
            left.FlowDirection = FlowDirection.LeftToRight;
            left.Padding = new Padding(8, 6, 4, 0);

            var title = new Label();
            title.Text = "로그";
            title.AutoSize = true;
            title.Font = UiTheme.UiFont(10F, FontStyle.Bold);
            title.ForeColor = UiTheme.Text;
            title.BackColor = Color.Transparent;
            title.Margin = new Padding(4, 5, 10, 0);

            chkAuto = new CheckBox();
            chkAuto.Text = "자동 스크롤";
            chkAuto.Checked = true;
            chkAuto.AutoSize = true;
            chkAuto.ForeColor = UiTheme.Text;
            chkAuto.BackColor = Color.Transparent;
            chkAuto.Font = UiTheme.UiFont(8.5F);
            chkAuto.Margin = new Padding(4, 5, 12, 0);
            chkAuto.CheckedChanged += (s, e) => { if (chkAuto.Checked) ScrollToEnd(); };

            Button btnCopy = MakeSmallButton("복사", 56);
            Button btnClear = MakeSmallButton("지우기", 62);
            Button btnFolder = MakeSmallButton("폴더", 56);
            btnCopy.Click += (s, e) => CopySelection();
            btnClear.Click += (s, e) => ClearLog();
            btnFolder.Click += (s, e) => OpenLogFolder();

            left.Controls.Add(title);
            left.Controls.Add(chkAuto);
            left.Controls.Add(btnCopy);
            left.Controls.Add(btnClear);
            left.Controls.Add(btnFolder);

            // 맨 오른쪽: 로그 설정 버튼 (누르면 카테고리별 설정 팝업)
            var right = new Panel();
            right.Dock = DockStyle.Right;
            right.Width = 56;
            right.BackColor = Color.Transparent;
            Button btnSettings = UiTheme.MakeButton("⚙", false, 40, 28);
            btnSettings.Font = UiTheme.UiFont(11F);
            btnSettings.Location = new Point(4, 6);
            btnSettings.Click += (s, e) => { EventHandler h = SettingsRequested; if (h != null) h(this, EventArgs.Empty); };
            right.Controls.Add(btnSettings);

            bar.Controls.Add(left);    // Fill 먼저
            bar.Controls.Add(right);

            tip = new ToolTip();
            tip.SetToolTip(btnCopy, "드래그로 선택한 부분만 복사 (선택이 없으면 전체)");
            tip.SetToolTip(btnClear, "화면의 로그만 지움 (로그 파일은 그대로)");
            tip.SetToolTip(btnFolder, "로그 파일이 있는 폴더 열기");
            tip.SetToolTip(btnSettings, "로그 설정 (표시 항목 · 패킷 로그 · 진단)");
            tip.SetToolTip(chkAuto, "끄면 새 로그가 와도 화면이 움직이지 않음 (맨 아래로 내리면 다시 켜짐)");

            Controls.Add(viewHost);
            Controls.Add(bar);
            LayoutView();

            LogCat.Changed += OnFilterChanged;
            VisibleChanged += (s, e) =>
            {
                if (!Visible) return;
                LayoutView();
                if (chkAuto.Checked) ScrollToEnd();
                UpdateThumb();
            };

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 100;
            timer.Tick += OnTick;
            timer.Start();
        }

        private static Button MakeSmallButton(string text, int width)
        {
            Button b = UiTheme.MakeButton(text, false, width, 26);
            b.Font = UiTheme.UiFont(8.5F);
            b.Margin = new Padding(3, 0, 3, 0);
            return b;
        }

        /// <summary>기본(네이티브) 세로 스크롤바는 오른쪽 화면 밖으로 밀어내고 그 자리에 전용 스크롤바를 올림</summary>
        private void LayoutView()
        {
            if (viewHost == null || vbar == null || rtb == null) return;
            int w = viewHost.ClientSize.Width;
            int h = viewHost.ClientSize.Height;
            if (w <= 0 || h <= 0) return;
            int barW = vbar.Width;
            vbar.SetBounds(w - barW, 0, barW, h);
            rtb.SetBounds(0, 0, Math.Max(1, w - barW + SystemInformation.VerticalScrollBarWidth), h);
        }

        /// <summary>전용 스크롤바의 위치/크기를 로그 창 상태에 맞춤</summary>
        private void UpdateThumb()
        {
            if (rtb == null || vbar == null || !rtb.IsHandleCreated) return;
            Point p = Point.Empty;
            SendMessage(rtb.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref p);
            int page = rtb.ClientSize.Height;
            int total = 0;
            if (rtb.TextLength > 0)
            {
                Point last = rtb.GetPositionFromCharIndex(rtb.TextLength - 1);
                total = last.Y + p.Y + rtb.Font.Height + 4;
            }
            vbar.SetState(p.Y, Math.Max(0, total - page), page);
        }

        // 마우스 휠/드래그 선택 등 사용자가 스크롤했을 때
        private void OnViewScrolled()
        {
            UpdateThumb();
            if (!busy && chkAuto != null) chkAuto.Checked = vbar.AtEnd;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                LogCat.Changed -= OnFilterChanged;
                if (timer != null) timer.Dispose();
                if (tip != null) tip.Dispose();
            }
            base.Dispose(disposing);
        }

        private void OnTick(object sender, EventArgs e)
        {
            if (!AppLog.HasPending) return;
            var batch = new List<string>(256);
            string line;
            while (batch.Count < 400 && AppLog.TryDequeue(out line)) batch.Add(line);
            if (batch.Count > 0) AppendLines(batch);
        }

        private void AppendLines(List<string> lines)
        {
            busy = true;
            bool keepView = !chkAuto.Checked;
            Point scroll = Point.Empty;
            int selStart = 0, selLen = 0;
            if (keepView)
            {
                SendMessage(rtb.Handle, EM_GETSCROLLPOS, IntPtr.Zero, ref scroll);
                selStart = rtb.SelectionStart;
                selLen = rtb.SelectionLength;
            }
            SendMessage(rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                foreach (string line in lines)
                {
                    string ts = "";
                    string msg = line;
                    if (line.Length > 13 && line[2] == ':' && line[5] == ':' && line[8] == '.')
                    {
                        ts = line.Substring(0, 13);
                        msg = line.Substring(13);
                    }
                    bool opensDash;
                    int cat = classifier.Classify(msg, out opensDash);
                    if (pendingDash != null)
                    {
                        pendingDash.Cat = cat;   // 값 목록의 첫 줄을 보고 구분선의 항목을 확정
                        Commit(pendingDash);
                        pendingDash = null;
                    }
                    var entry = new Entry();
                    entry.Ts = ts;
                    entry.Msg = msg;
                    entry.Cat = cat;
                    entry.Color = ColorFor(msg, trackColor);
                    if (msg.Length > 0) trackColor = entry.Color;
                    if (opensDash) { pendingDash = entry; continue; }
                    Commit(entry);
                }
                TrimView(ref selStart, ref selLen);
            }
            finally
            {
                if (keepView)
                {
                    rtb.Select(selStart, selLen);
                    SendMessage(rtb.Handle, EM_SETSCROLLPOS, IntPtr.Zero, ref scroll);
                }
                else
                {
                    rtb.SelectionStart = rtb.TextLength;
                    rtb.SelectionLength = 0;
                    rtb.ScrollToCaret();
                }
                SendMessage(rtb.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                rtb.Invalidate();
                UpdateThumb();
                busy = false;
            }
        }

        /// <summary>기록에 추가하고, 보이는 항목이면 화면에도 추가</summary>
        private void Commit(Entry e)
        {
            history.Add(e);
            if (history.Count > MaxHistory) history.RemoveRange(0, history.Count - KeepHistory);
            if (!LogCat.IsHidden(e.Cat)) Render(e);
        }

        private void Render(Entry e)
        {
            if (e.Ts.Length > 0) AppendColored(e.Ts, Color.FromArgb(88, 101, 122));
            AppendColored(e.Msg + "\n", e.Color);
            lineCount++;
        }

        private void TrimView(ref int selStart, ref int selLen)
        {
            if (lineCount <= MaxLines) return;
            int remove = lineCount - KeepLines;
            string all = rtb.Text;
            int idx = 0;
            for (int i = 0; i < remove; i++)
            {
                int next = all.IndexOf('\n', idx);
                if (next < 0) break;
                idx = next + 1;
            }
            if (idx > 0)
            {
                rtb.Select(0, idx);
                rtb.SelectedText = "";
                lineCount -= remove;
                selLen = 0;
                selStart = 0;
            }
        }

        /// <summary>표시 항목 설정이 바뀌면 숨겨 둔 줄까지 포함해서 화면을 다시 그림</summary>
        private void OnFilterChanged(object sender, EventArgs e)
        {
            if (!rtb.IsHandleCreated) return;
            busy = true;
            SendMessage(rtb.Handle, WM_SETREDRAW, IntPtr.Zero, IntPtr.Zero);
            try
            {
                rtb.Clear();
                lineCount = 0;
                var visible = new List<Entry>();
                for (int i = history.Count - 1; i >= 0 && visible.Count < KeepLines; i--)
                    if (!LogCat.IsHidden(history[i].Cat)) visible.Add(history[i]);
                for (int i = visible.Count - 1; i >= 0; i--) Render(visible[i]);
            }
            finally
            {
                rtb.SelectionStart = rtb.TextLength;
                rtb.SelectionLength = 0;
                if (chkAuto.Checked) rtb.ScrollToCaret();
                SendMessage(rtb.Handle, WM_SETREDRAW, new IntPtr(1), IntPtr.Zero);
                rtb.Invalidate();
                UpdateThumb();
                busy = false;
            }
        }

        private void AppendColored(string text, Color color)
        {
            rtb.SelectionStart = rtb.TextLength;
            rtb.SelectionLength = 0;
            rtb.SelectionColor = color;
            rtb.AppendText(text);
        }

        private static Color ColorFor(string m, Color previous)
        {
            if (m.Length == 0) return previous;
            if (m.StartsWith("   ") || m.StartsWith("\t")) return previous; // 예외 스택 등 이어지는 줄
            if (m.StartsWith("[오류]") || m.StartsWith("[SOCKET ERR]") || m.Contains("Exception")) return UiTheme.Err;
            if (m.StartsWith("[Rho]"))
                return (m.Contains("건너뜀") || m.Contains("실패") || m.Contains("못 찾은")) ? UiTheme.Warn : Color.FromArgb(86, 212, 221);
            if (m.StartsWith("[PIN]")) return Color.FromArgb(188, 140, 255);
            if (m.StartsWith("[서버]")) return UiTheme.Ok;
            if (m.StartsWith("[패킷 수신]")) return Color.FromArgb(130, 160, 205);
            if (m.StartsWith("[패킷 송신")) return Color.FromArgb(94, 234, 212);
            if (m.StartsWith("[설정]")) return Color.FromArgb(121, 192, 255);
            if (m.StartsWith("[런처]")) return Color.FromArgb(121, 192, 255);
            if (m.StartsWith("[KartSpec]") || m.StartsWith("[FlyingPet]")) return Color.FromArgb(255, 203, 107);
            if (m.StartsWith("[로그]")) return UiTheme.Muted;
            return UiTheme.Text;
        }

        private void ScrollToEnd()
        {
            busy = true;
            try
            {
                rtb.SelectionStart = rtb.TextLength;
                rtb.SelectionLength = 0;
                rtb.ScrollToCaret();
            }
            finally
            {
                busy = false;
            }
            UpdateThumb();
        }

        private void CopySelection()
        {
            SetClipboard(rtb.SelectionLength > 0 ? rtb.SelectedText : rtb.Text);
        }

        private void CopyAll()
        {
            SetClipboard(rtb.Text);
        }

        private static void SetClipboard(string s)
        {
            if (string.IsNullOrEmpty(s)) return;
            try { Clipboard.SetText(s.Replace("\r\n", "\n").Replace("\n", Environment.NewLine)); }
            catch { }
        }

        public void ClearLog()
        {
            rtb.Clear();
            history.Clear();
            pendingDash = null;
            lineCount = 0;
            UpdateThumb();
        }

        private void OpenLogFolder()
        {
            try
            {
                string p = AppLog.LogPath;
                if (!string.IsNullOrEmpty(p) && File.Exists(p))
                {
                    var psi = new ProcessStartInfo("explorer.exe", "/select,\"" + p + "\"");
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
                else if (!string.IsNullOrEmpty(AppLog.LogDirectory) && Directory.Exists(AppLog.LogDirectory))
                {
                    var psi = new ProcessStartInfo(AppLog.LogDirectory);
                    psi.UseShellExecute = true;
                    Process.Start(psi);
                }
            }
            catch { }
        }
    }
}
