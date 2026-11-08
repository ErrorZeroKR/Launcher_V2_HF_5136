using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;
using RiderData;

namespace KartRider
{
    /// <summary>
    /// 아이템 추가 패널: 런처가 게임 데이터에서 인식한 아이템을 분류별로 골라서 추가 (분류 ID / 아이템 ID를 따로 찾을 필요 없음).
    /// 런처 화면 중앙에 겹쳐 뜨는 카드 안에서 사용. 게임이 접속해 있을 때 추가하면 바로 지급 패킷이 전송됨.
    /// </summary>
    public class ItemPicker : UserControl
    {
        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int LVM_SCROLL = 0x1014;

        private readonly ListView lvCat;
        private readonly ListView lvItems;
        private readonly TextBox txtSearch;
        private readonly Label lblInfo;
        private readonly Label lblConn;
        private readonly Button btnAdd;
        private readonly Panel manualRow;
        private readonly TextBox txtType;
        private readonly TextBox txtCode;
        private readonly System.Windows.Forms.Timer connTimer;
        private Dictionary<int, SortedDictionary<int, string>> data = new Dictionary<int, SortedDictionary<int, string>>();
        private Dictionary<int, string> catNames = new Dictionary<int, string>();
        private bool sending;
        private string lastSignature;
        private Dictionary<int, string> gameNames = new Dictionary<int, string>();   // 분류 ID -> 게임 데이터의 분류명(태그)
        private readonly Font subFont = UiTheme.UiFont(8.5F);
        private readonly Font idFont = UiTheme.UiFont(8F);   // 분류 버튼 오른쪽의 ID 표시용
        private ImageList catSizer, itemSizer;               // 목록 줄 높이를 정하는 이미지 목록 (크기만 사용)
        private Panel leftHost, itemsHost;                   // 목록을 담는 영역 (목록 높이를 줄 수 × 줄 높이로 직접 정함)
        private Button btnManual, btnManualAdd;
        private ThemedScrollBar catBar, itemBar;             // 목록 옆의 슬림 스크롤바 (런처 디자인)
        private System.Windows.Forms.Timer barTimer;         // 목록 스크롤 위치를 스크롤바에 반영
        private int catBaseRow, itemBaseRow;                 // 줄 높이의 기준값 (글자 크기에서 계산)
        private static bool categoriesLogged;

        /// <summary>닫기(✕) 버튼을 눌렀을 때</summary>
        public event EventHandler CloseRequested;

        public ItemPicker()
        {
            BackColor = UiTheme.Bg;
            ForeColor = UiTheme.Text;
            Font = UiTheme.UiFont(9F);

            // ----- 위: 제목 + 닫기 -----
            var header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 58;
            header.BackColor = UiTheme.Panel;

            var btnClose = UiTheme.MakeButton("✕", false, 46, 34);
            btnClose.Font = UiTheme.UiFont(10F);
            btnClose.Location = new Point(10, 12);
            btnClose.Click += (s, e) => { EventHandler h = CloseRequested; if (h != null) h(this, EventArgs.Empty); };
            var closeHost = new Panel();
            closeHost.Dock = DockStyle.Right;
            closeHost.Width = 66;
            closeHost.BackColor = Color.Transparent;
            closeHost.Controls.Add(btnClose);

            var lblTitle = new Label();
            lblTitle.Text = "아이템 추가";
            lblTitle.AutoSize = true;
            lblTitle.Font = UiTheme.UiFont(13F, FontStyle.Bold);
            lblTitle.ForeColor = UiTheme.Text;
            lblTitle.BackColor = Color.Transparent;
            lblTitle.Location = new Point(20, 6);
            var lblSub = new Label();
            lblSub.Text = "분류를 고르고 아이템을 선택하세요 (여러 개 선택 가능)";
            lblSub.AutoSize = true;
            lblSub.Font = UiTheme.UiFont(8.5F);
            lblSub.ForeColor = UiTheme.Muted;
            lblSub.BackColor = Color.Transparent;
            lblSub.Location = new Point(22, 34);
            header.Controls.Add(lblTitle);
            header.Controls.Add(lblSub);
            header.Controls.Add(closeHost);

            // ----- 아래: 상태 + 버튼 -----
            var bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 70;
            bottom.BackColor = UiTheme.Panel;

            var bottomRight = new Panel();
            bottomRight.Dock = DockStyle.Right;
            bottomRight.Width = 270;
            bottomRight.BackColor = Color.Transparent;
            btnManual = UiTheme.MakeButton("직접 입력", false, 110, 42);
            WireDisabledLook(btnManual, false);
            btnManual.Font = UiTheme.UiFont(9F);
            btnManual.Location = new Point(10, 14);
            btnManual.Click += (s, e) => { manualRow.Visible = !manualRow.Visible; };
            btnAdd = UiTheme.MakeButton("추가", true, 130, 42);
            WireDisabledLook(btnAdd, true);
            btnAdd.Location = new Point(130, 14);
            btnAdd.Click += (s, e) => AddSelected();
            bottomRight.Controls.Add(btnManual);
            bottomRight.Controls.Add(btnAdd);

            var bottomLeft = new Panel();
            bottomLeft.Dock = DockStyle.Fill;
            bottomLeft.Padding = new Padding(20, 8, 0, 6);
            bottomLeft.BackColor = Color.Transparent;
            lblConn = new Label();
            lblConn.Dock = DockStyle.Bottom;
            lblConn.Height = 24;
            lblConn.Font = UiTheme.UiFont(8.5F);
            lblConn.BackColor = Color.Transparent;
            lblInfo = new Label();
            lblInfo.Dock = DockStyle.Fill;
            lblInfo.Font = UiTheme.UiFont(9F);
            lblInfo.ForeColor = UiTheme.Muted;
            lblInfo.BackColor = Color.Transparent;
            bottomLeft.Controls.Add(lblInfo);   // Fill 먼저
            bottomLeft.Controls.Add(lblConn);

            bottom.Controls.Add(bottomLeft);    // Fill 먼저
            bottom.Controls.Add(bottomRight);

            // ----- 왼쪽: 분류 -----
            var left = new Panel();
            leftHost = left;
            left.Dock = DockStyle.Left;
            left.Width = 250;
            left.Padding = new Padding(16, 12, 6, 12);
            left.BackColor = UiTheme.Bg;
            lvCat = MakeList(false);
            lvCat.Columns.Add("", 200);
            lvCat.Dock = DockStyle.None;   // 높이는 LayoutLists 에서 줄 수 × 줄 높이로 정함
            // 두 줄 표시(분류 이름 + 회색 게임 분류명): 직접 그리고, 이미지 목록 크기로 행 높이를 키움
            int rowH = lvCat.Font.Height + lvCat.Font.Height / 2 + 6;   // 한 줄(이름 + ID)이라 줄 높이를 낮게
            var rowSizer = new ImageList();
            rowSizer.ColorDepth = ColorDepth.Depth32Bit;
            rowSizer.ImageSize = new Size(1, Math.Min(256, rowH));
            lvCat.SmallImageList = rowSizer;
            catSizer = rowSizer;
            catBaseRow = rowH;
            lvCat.OwnerDraw = true;
            lvCat.DrawItem += (s, e) => PaintCategoryRow(e.Graphics, e.Item, e.Bounds);
            lvCat.DrawSubItem += (s, e) => { if (e.ColumnIndex == 0) PaintCategoryRow(e.Graphics, e.Item, e.Bounds); };
            lvCat.DrawColumnHeader += (s, e) => e.DrawDefault = true;
            lvCat.SelectedIndexChanged += (s, e) => FillItems();
            lvCat.SizeChanged += (s, e) => FitColumns();
            lvCat.ClientSizeChanged += (s, e) => FitColumns();   // 세로 스크롤바가 생기거나 사라져 폭이 달라질 때도 열 너비를 다시 맞춤
            left.Controls.Add(lvCat);
            catBar = new ThemedScrollBar(true);
            left.Controls.Add(catBar);
            catBar.Scrolled += (s, e) => ScrollListTo(lvCat, catSizer, catBar.Value);
            left.SizeChanged += (s, e) => LayoutLists();

            // ----- 가운데: 검색 + 아이템 목록 + (직접 입력 줄) -----
            var center = new Panel();
            center.Dock = DockStyle.Fill;
            center.Padding = new Padding(6, 12, 16, 12);
            center.BackColor = UiTheme.Bg;

            itemsHost = new Panel();
            itemsHost.Dock = DockStyle.Fill;
            itemsHost.BackColor = UiTheme.Bg;
            itemsHost.SizeChanged += (s, e) => LayoutLists();
            lvItems = MakeList(true);
            lvItems.Columns.Add("ID", 80);
            lvItems.Columns.Add("이름", 300);
            lvItems.Dock = DockStyle.None;
            itemsHost.Controls.Add(lvItems);
            itemBar = new ThemedScrollBar(true);
            itemsHost.Controls.Add(itemBar);
            itemBar.Scrolled += (s, e) => ScrollListTo(lvItems, itemSizer, itemBar.Value);
            itemBaseRow = lvItems.Font.Height + 10;
            itemSizer = new ImageList();
            itemSizer.ColorDepth = ColorDepth.Depth32Bit;
            itemSizer.ImageSize = new Size(1, itemBaseRow);
            lvItems.SmallImageList = itemSizer;
            lvItems.DoubleClick += (s, e) => AddSelected();
            lvItems.SizeChanged += (s, e) => FitColumns();
            lvItems.ClientSizeChanged += (s, e) => FitColumns();
            lvItems.SelectedIndexChanged += (s, e) => UpdateSelectionInfo();

            manualRow = new Panel();
            manualRow.Dock = DockStyle.Bottom;
            manualRow.Height = 46;
            manualRow.Visible = false;
            manualRow.BackColor = Color.Transparent;
            manualRow.Controls.Add(MakeText("분류 ID", 0, 14));
            txtType = MakeNumberBox(56, 9, 70);
            manualRow.Controls.Add(txtType);
            manualRow.Controls.Add(MakeText("아이템 ID", 140, 14));
            txtCode = MakeNumberBox(210, 9, 90);
            manualRow.Controls.Add(txtCode);
            btnManualAdd = UiTheme.MakeButton("직접 추가", false, 100, 32);
            WireDisabledLook(btnManualAdd, false);
            btnManualAdd.Font = UiTheme.UiFont(9F);
            btnManualAdd.Location = new Point(316, 7);
            btnManualAdd.Click += (s, e) => AddManual();
            manualRow.Controls.Add(btnManualAdd);

            txtSearch = new TextBox();
            txtSearch.Dock = DockStyle.Top;
            txtSearch.BorderStyle = BorderStyle.FixedSingle;
            txtSearch.BackColor = UiTheme.Panel2;
            txtSearch.ForeColor = UiTheme.Text;
            txtSearch.Font = UiTheme.UiFont(10F);
            txtSearch.TextChanged += (s, e) => FillItems();
            var searchLabel = new Label();
            searchLabel.Text = "이름 또는 ID로 검색";
            searchLabel.Dock = DockStyle.Top;
            searchLabel.Height = 20;
            searchLabel.Font = UiTheme.UiFont(8.5F);
            searchLabel.ForeColor = UiTheme.Muted;
            var gap = new Panel();
            gap.Dock = DockStyle.Top;
            gap.Height = 8;

            center.Controls.Add(itemsHost);   // Fill 먼저
            center.Controls.Add(manualRow);   // Bottom
            center.Controls.Add(gap);
            center.Controls.Add(txtSearch);
            center.Controls.Add(searchLabel);

            // 추가 순서: Fill → Left → Bottom → Top
            Controls.Add(center);
            Controls.Add(left);
            Controls.Add(bottom);
            Controls.Add(header);

            barTimer = new System.Windows.Forms.Timer();
            barTimer.Interval = 40;
            barTimer.Tick += (s, e) => { if (Visible) SyncBars(); };
            barTimer.Start();

            connTimer = new System.Windows.Forms.Timer();
            connTimer.Interval = 600;
            connTimer.Tick += (s, e) => { if (Visible) UpdateConnection(); };
            connTimer.Start();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && connTimer != null) connTimer.Dispose();
            if (disposing && barTimer != null) barTimer.Dispose();
            base.Dispose(disposing);
        }

        private static Label MakeText(string text, int x, int y)
        {
            var l = new Label();
            l.Text = text;
            l.AutoSize = true;
            l.Font = UiTheme.UiFont(8.5F);
            l.ForeColor = UiTheme.Muted;
            l.BackColor = Color.Transparent;
            l.Location = new Point(x, y);
            return l;
        }

        private static TextBox MakeNumberBox(int x, int y, int w)
        {
            var t = new TextBox();
            t.SetBounds(x, y, w, 26);
            t.BorderStyle = BorderStyle.FixedSingle;
            t.BackColor = UiTheme.Panel2;
            t.ForeColor = UiTheme.Text;
            t.Font = UiTheme.UiFont(9.5F);
            t.MaxLength = 6;
            t.KeyPress += (s, e) => { if (!(char.IsDigit(e.KeyChar) || char.IsControl(e.KeyChar))) e.Handled = true; };
            return t;
        }

        private static ListView MakeList(bool multi)
        {
            var lv = new ThemedListView();
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.HideSelection = false;
            lv.MultiSelect = multi;
            lv.HeaderStyle = ColumnHeaderStyle.None;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = Color.FromArgb(11, 15, 20);
            lv.ForeColor = UiTheme.Text;
            lv.Font = UiTheme.UiFont(10F);
            try { typeof(ListView).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(lv, true, null); } catch { }
            UiTheme.DarkScrollbars(lv);
            return lv;
        }

        /// <summary>
        /// 목록은 한 줄 단위로만 스크롤되어서, 목록 높이가 줄 높이의 배수에서 1px 만 모자라도 한 줄이 통째로 빈 공간이 됨.
        /// 그래서 목록 높이를 (보이는 줄 수 × 줄 높이 + 여유 4px)로 직접 정하고, 줄 높이는 그 높이에 맞게 조절함.
        /// 남는 높이는 목록 밖(아래 여백)에 둠.
        /// </summary>
        private static void LayoutList(Control host, ListView lv, ImageList sizer, int baseRow, ThemedScrollBar bar)
        {
            if (host == null || lv == null || sizer == null || baseRow <= 0) return;
            int x = host.Padding.Left, y = host.Padding.Top;
            int w = Math.Max(0, host.ClientSize.Width - host.Padding.Horizontal);
            int h = host.ClientSize.Height - host.Padding.Vertical;
            int barW = bar != null ? bar.Width : 0;
            int gap = bar != null ? Math.Max(2, barW / 2) : 0;
            int lw = Math.Max(0, w - barW - gap);
            int lh;
            if (h < baseRow + 4) lh = Math.Max(0, h);
            else
            {
                int n = Math.Max(1, (int)Math.Round((h - 4) / (double)baseRow));
                int rowH = Math.Max(16, Math.Min(256, (h - 4) / n));
                if (sizer.ImageSize.Height != rowH) sizer.ImageSize = new Size(1, rowH);
                lh = n * rowH + 4;
            }
            lv.SetBounds(x, y, lw, lh);
            if (bar != null) bar.SetBounds(x + w - barW, y, barW, lh);
        }

        private void LayoutLists()
        {
            LayoutList(leftHost, lvCat, catSizer, catBaseRow, catBar);
            LayoutList(itemsHost, lvItems, itemSizer, itemBaseRow, itemBar);
        }

        // ---------- 목록 <-> 스크롤바 ----------
        /// <summary>스크롤바를 끌었을 때: 목록을 그 위치(맨 위 줄 번호)로 스크롤</summary>
        private static void ScrollListTo(ListView lv, ImageList sizer, int newTop)
        {
            if (lv == null || sizer == null || !lv.IsHandleCreated || lv.Items.Count == 0) return;
            int cur = lv.TopItem != null ? lv.TopItem.Index : 0;
            int delta = newTop - cur;
            if (delta == 0) return;
            SendMessage(lv.Handle, LVM_SCROLL, IntPtr.Zero, (IntPtr)(delta * Math.Max(1, sizer.ImageSize.Height)));
        }

        /// <summary>목록의 현재 스크롤 위치(휠/키보드로 바뀐 것 포함)를 스크롤바에 반영. 스크롤할 내용이 없으면 스크롤바를 숨김</summary>
        private static void SyncBar(ListView lv, ThemedScrollBar bar, ImageList sizer)
        {
            if (lv == null || bar == null || sizer == null || !lv.IsHandleCreated) return;
            int rowH = Math.Max(1, sizer.ImageSize.Height);
            int page = Math.Max(1, lv.ClientSize.Height / rowH);
            int max = Math.Max(0, lv.Items.Count - page);
            int top = 0;
            try { top = lv.TopItem != null ? lv.TopItem.Index : 0; } catch { }
            bool need = max > 0;
            if (bar.Visible != need) bar.Visible = need;
            bar.SetState(top, max, page);
        }

        private void SyncBars()
        {
            SyncBar(lvCat, catBar, catSizer);
            SyncBar(lvItems, itemBar, itemSizer);
        }

        /// <summary>목록 폭에 맞춰 열 너비 조정 (DPI/창 크기와 상관없이)</summary>
        private void FitColumns()
        {
            // 생성 도중(두 목록이 모두 만들어지기 전)에 크기 이벤트가 먼저 올 수 있음
            if (lvCat == null || lvItems == null) return;
            if (lvCat.Columns.Count > 0) lvCat.Columns[0].Width = Math.Max(50, lvCat.ClientSize.Width - 2);
            if (lvItems.Columns.Count > 1)
            {
                int idW = Math.Max(60, (int)(lvItems.ClientSize.Width * 0.15));
                lvItems.Columns[0].Width = idW;
                lvItems.Columns[1].Width = Math.Max(80, lvItems.ClientSize.Width - idW - 2);
            }
        }

        /// <summary>패널을 열 때마다 호출. 인식된 데이터가 그대로면 목록을 다시 만들지 않아서 빠르게 열림</summary>
        public void Reload()
        {
            string sig = Signature();
            if (sig == lastSignature && lvCat.Items.Count > 0)
            {
                if (txtSearch.Text.Length > 0) txtSearch.Text = "";
                UpdateConnection();
                return;
            }
            lastSignature = sig;

            data = new Dictionary<int, SortedDictionary<int, string>>();
            catNames = new Dictionary<int, string>();
            gameNames = new Dictionary<int, string>();
            txtSearch.Text = "";
            LoadData();
            FillCategories();
            FitColumns();
            UpdateConnection();
            LogCategories();
        }

        private static string Signature()
        {
            int total = 0, cats = 0;
            try
            {
                foreach (Dictionary<ushort, string> c in new List<Dictionary<ushort, string>>(NewRider.items.Values)) { total += c.Count; cats++; }
            }
            catch { }
            return total + "/" + cats + "/" + Kart.kartName.Count + "/" + KartRhoFile.ItemTableTags.Count + "/" + KartRhoFile.LoadVersion;
        }

        public void FocusSearch()
        {
            txtSearch.Focus();
        }

        // ---------- 데이터 ----------
        private void LoadData()
        {
            try
            {
                foreach (KeyValuePair<ushort, Dictionary<ushort, string>> cat in new List<KeyValuePair<ushort, Dictionary<ushort, string>>>(NewRider.items))
                {
                    var sorted = new SortedDictionary<int, string>();
                    foreach (KeyValuePair<ushort, string> it in new List<KeyValuePair<ushort, string>>(cat.Value)) sorted[it.Key] = it.Value;
                    if (sorted.Count > 0) data[cat.Key] = sorted;
                }
            }
            catch { }

            // 상점 데이터에 카트가 없으면, 인식한 카트 이름 표로 채움 (분류 3 = 카트)
            if (!data.ContainsKey(3) && Kart.kartName.Count > 0)
            {
                var karts = new SortedDictionary<int, string>();
                foreach (KeyValuePair<int, KartTable> k in new List<KeyValuePair<int, KartTable>>(Kart.kartName))
                    karts[k.Key] = k.Value.Name;
                if (karts.Count > 0) data[3] = karts;
            }

            BuildCategoryNames();
            BuildGameTagNames();
        }

        /// <summary>HF 내장 Item.xml 의 태그별 아이템 ID 와 가장 많이 겹치는 분류에 한국어 이름을 붙임</summary>
        private void BuildCategoryNames()
        {
            catNames[3] = "카트";
            try
            {
                string path = @"Profile\Item.xml";
                if (!File.Exists(path)) return;
                var doc = new XmlDocument();
                doc.Load(path);
                var tagIds = new Dictionary<string, List<int>>();
                foreach (XmlNode n in doc.SelectNodes("//*[@id]"))
                {
                    var el = (XmlElement)n;
                    int id;
                    if (!int.TryParse(el.GetAttribute("id"), out id)) continue;
                    List<int> list;
                    if (!tagIds.TryGetValue(el.Name, out list)) { list = new List<int>(); tagIds[el.Name] = list; }
                    list.Add(id);
                }
                var bestOverlap = new Dictionary<int, int>();
                foreach (KeyValuePair<string, List<int>> tag in tagIds)
                {
                    string ko;
                    if (!ItemCategories.TagNames.TryGetValue(tag.Key, out ko)) continue;
                    foreach (KeyValuePair<int, SortedDictionary<int, string>> cat in data)
                    {
                        if (cat.Key == 3) continue;
                        int c = 0;
                        foreach (int id in tag.Value) if (cat.Value.ContainsKey(id)) c++;
                        if (c >= 2 && c >= tag.Value.Count * 0.3)
                        {
                            int prevBest;
                            if (!bestOverlap.TryGetValue(cat.Key, out prevBest) || c > prevBest)
                            {
                                bestOverlap[cat.Key] = c;
                                catNames[cat.Key] = ko;
                            }
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 게임 데이터(itemTable)의 분류명(kart, balloon, headBand ...)을 아이템 id 가 가장 많이 겹치는 분류 ID 에 짝지음.
        /// 분류의 한국어 이름이 코드에 없어도, 이 이름으로 어떤 분류인지 알 수 있음.
        /// </summary>
        private void BuildGameTagNames()
        {
            gameNames.Clear();
            try
            {
                var tags = new List<KeyValuePair<string, HashSet<int>>>(KartRhoFile.ItemTableTags);
                foreach (KeyValuePair<int, SortedDictionary<int, string>> cat in data)
                {
                    string best = null;
                    int bestCount = 0, bestSize = int.MaxValue;
                    foreach (KeyValuePair<string, HashSet<int>> tag in tags)
                    {
                        int c = 0;
                        foreach (int id in cat.Value.Keys) if (tag.Value.Contains(id)) c++;
                        if (c == 0) continue;
                        // 많이 겹칠수록 우선, 같으면 더 작은(구체적인) 분류를 우선
                        if (c > bestCount || (c == bestCount && tag.Value.Count < bestSize)) { best = tag.Key; bestCount = c; bestSize = tag.Value.Count; }
                    }
                    if (best != null && bestCount >= Math.Max(1, cat.Value.Count / 5)) gameNames[cat.Key] = best;
                }
            }
            catch { }
        }

        private string GameName(int cat)
        {
            string g;
            return gameNames.TryGetValue(cat, out g) ? g : null;
        }

        /// <summary>분류 목록의 한 줄: 위에 분류 이름 (개수), 아래에 회색 게임 분류명, 맨 오른쪽 가운데에 작고 회색인 분류 ID</summary>
        private void PaintCategoryRow(Graphics g, ListViewItem item, Rectangle b)
        {
            bool sel = item.Selected;
            using (var br = new SolidBrush(sel ? UiTheme.Accent : lvCat.BackColor)) g.FillRectangle(br, b);
            int padX = (int)(10 * lvCat.DeviceDpi / 96f);
            const TextFormatFlags f = TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;

            // 오른쪽 가운데: 분류 ID
            int idArea = 0;
            if (item.Tag is int)
            {
                string idText = ((int)item.Tag).ToString();
                int idW = TextRenderer.MeasureText(g, idText, idFont, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix).Width;
                TextRenderer.DrawText(g, idText, idFont, new Rectangle(b.Right - padX - idW, b.Y, idW, b.Height),
                    sel ? Color.FromArgb(205, 222, 255) : UiTheme.Muted,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
                idArea = idW + padX;   // 글자와 ID 사이 간격
            }

            // 왼쪽: 분류 이름 (+ 게임 분류명이 있으면 아래 회색 줄)
            string sub = item.SubItems.Count > 1 ? item.SubItems[1].Text : "";
            bool hasSub = sub.Length > 0;
            int h1 = lvCat.Font.Height, h2 = subFont.Height;
            int top = b.Y + Math.Max(0, (b.Height - (h1 + (hasSub ? h2 : 0))) / 2);
            int w = Math.Max(10, b.Width - padX * 2 - idArea);
            TextRenderer.DrawText(g, item.Text, lvCat.Font, new Rectangle(b.X + padX, top, w, h1), sel ? Color.White : UiTheme.Text, f);
            if (hasSub)
                TextRenderer.DrawText(g, sub, subFont, new Rectangle(b.X + padX, top + h1, w, h2), sel ? Color.FromArgb(205, 222, 255) : UiTheme.Muted, f);

            using (var pen = new Pen(Color.FromArgb(26, 34, 44)))
                g.DrawLine(pen, b.X + padX, b.Bottom - 1, b.Right - padX, b.Bottom - 1);
        }

        /// <summary>분류 이름 아래의 회색 보조 줄: 영문 게임 분류명은 보여주지 않음 (분류 ID 는 오른쪽에 따로 표시)</summary>
        private string CatSubText(int cat)
        {
            return "";
        }

        /// <summary>분류 이름: ItemCategories.Names(직접 지정) → 자동 인식 → "분류 N"</summary>
        private string CatName(int cat)
        {
            string n;
            if (ItemCategories.Names.TryGetValue(cat, out n) && !string.IsNullOrEmpty(n)) return n;
            return catNames.TryGetValue(cat, out n) ? n : "분류 " + cat;
        }

        /// <summary>이 분류를 보여줄지: 화이트리스트에 있고(비어 있으면 전부) Hidden 에 없어야 함</summary>
        private static bool IsShown(int id)
        {
            if (ItemCategories.Hidden.Contains(id)) return false;
            return ItemCategories.Whitelist.Count == 0 || ItemCategories.Whitelist.Contains(id);
        }

        /// <summary>표시할 분류 ID 목록: 화이트리스트에 있는 분류만, ItemCategories.Order 먼저, 나머지는 ID 순</summary>
        private List<int> OrderedCategories()
        {
            var result = new List<int>();
            foreach (int id in ItemCategories.Order)
                if (data.ContainsKey(id) && IsShown(id) && !result.Contains(id)) result.Add(id);
            var rest = new List<int>(data.Keys);
            rest.Sort();
            foreach (int id in rest)
                if (IsShown(id) && !result.Contains(id)) result.Add(id);
            return result;
        }

        /// <summary>
        /// 처음 열 때 한 번, 현재 분류 목록을 ItemCategories.Names 에 그대로 복사해 붙여 넣을 수 있는 형태로 로그에 남김.
        /// 예시 아이템 이름이 같이 나와서 분류 이름이 맞는지 바로 확인할 수 있음.
        /// </summary>
        private void LogCategories()
        {
            if (categoriesLogged || data.Count == 0) return;
            categoriesLogged = true;
            var sb = new StringBuilder();
            sb.Append("[런처] 아이템 분류 목록 — 이름은 Forms/ItemCategories.cs 의 Names 로 고치고, 보여줄 분류는 같은 파일의 Whitelist 에 ID 로 추가해요");
            List<int> shown = OrderedCategories();
            var logList = new List<int>(shown);
            var others = new List<int>(data.Keys);
            others.Sort();
            foreach (int id in others) if (!shown.Contains(id)) logList.Add(id);   // 화이트리스트 밖의 분류도 확인할 수 있게 뒤에 함께 표시
            foreach (int cat in logList)
            {
                SortedDictionary<int, string> items = data[cat];
                string source = ItemCategories.Names.ContainsKey(cat) ? "직접 지정" : (catNames.ContainsKey(cat) ? "자동 인식" : "이름 없음");
                var samples = new List<string>();
                foreach (KeyValuePair<int, string> it in items)
                {
                    if (!string.IsNullOrEmpty(it.Value)) samples.Add(it.Value);
                    if (samples.Count >= 3) break;
                }
                sb.AppendLine();
                sb.Append("    { ").Append(cat).Append(", \"").Append(CatName(cat)).Append("\" },   // ")
                  .Append(items.Count).Append("개 · ").Append(source)
                  .Append(shown.Contains(cat) ? "" : " · 숨김(화이트리스트 밖)")
                  .Append(GameName(cat) != null ? " · 게임 분류명: " + GameName(cat) : "")
                  .Append(samples.Count > 0 ? " · 예: " + string.Join(", ", samples) : "");
            }
            Console.WriteLine(sb.ToString());
        }


        private void FillCategories()
        {
            lvCat.BeginUpdate();
            lvCat.Items.Clear();
            lvItems.Items.Clear();
            foreach (int cat in OrderedCategories())
            {
                var it = new ListViewItem(CatName(cat) + "  (" + data[cat].Count + ")");
                it.SubItems.Add(CatSubText(cat));
                it.Tag = cat;
                lvCat.Items.Add(it);
            }
            lvCat.EndUpdate();

            if (lvCat.Items.Count == 0)
            {
                lblInfo.Text = "인식된 아이템이 없어요. 데이터를 읽는 중이면 잠시 뒤 다시 열어 주세요 (직접 입력은 가능)";
                lblInfo.ForeColor = UiTheme.Warn;
                btnAdd.Enabled = false;
                return;
            }
            btnAdd.Enabled = true;
            lvCat.Items[0].Selected = true;
        }

        private void FillItems()
        {
            if (lvCat == null || lvItems == null || txtSearch == null || lblInfo == null) return;
            if (lvCat.SelectedItems.Count == 0) return;
            int cat = (int)lvCat.SelectedItems[0].Tag;
            SortedDictionary<int, string> items;
            if (!data.TryGetValue(cat, out items)) return;
            string q = txtSearch.Text.Trim();
            var rows = new List<ListViewItem>();
            foreach (KeyValuePair<int, string> it in items)
            {
                string name = string.IsNullOrEmpty(it.Value) ? "(이름 없음)" : it.Value;
                if (q.Length > 0 && name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && !it.Key.ToString().StartsWith(q)) continue;
                var row = new ListViewItem(new[] { it.Key.ToString(), name });
                row.Tag = it.Key;
                rows.Add(row);
                if (rows.Count >= 5000) break;
            }
            lvItems.BeginUpdate();
            lvItems.Items.Clear();
            lvItems.Items.AddRange(rows.ToArray());
            lvItems.EndUpdate();
            FitColumns();
            lblInfo.Text = CatName(cat) + ": " + rows.Count + "개 표시 (전체 " + items.Count + "개) — 더블클릭하면 바로 추가";
            lblInfo.ForeColor = UiTheme.Muted;
        }

        private void UpdateSelectionInfo()
        {
            if (lvCat == null || lvItems == null || lblInfo == null) return;
            if (lvItems.SelectedItems.Count > 0 && lvCat.SelectedItems.Count > 0)
            {
                lblInfo.Text = "선택 " + lvItems.SelectedItems.Count + "개 — 추가를 누르면 접속 중인 게임에 지급해요";
                lblInfo.ForeColor = UiTheme.Text;
            }
        }

        // ---------- 동작 ----------
        private static bool Connected()
        {
            return GameLink.IsConnected();
        }

        /// <summary>접속 상태 표시 + 게임이 접속해 있지 않으면 추가 / 직접 입력 버튼을 비활성화</summary>
        private void UpdateConnection()
        {
            bool on = Connected();
            if (on)
            {
                lblConn.Text = "● 게임 접속됨 — 바로 추가할 수 있어요";
                lblConn.ForeColor = UiTheme.Ok;
            }
            else
            {
                lblConn.Text = "● 게임 접속 대기 중 — 로비에 들어간 뒤 추가하세요";
                lblConn.ForeColor = UiTheme.Warn;
            }
            bool hasCats = lvCat != null && lvCat.Items.Count > 0;
            if (btnAdd != null) btnAdd.Enabled = on && hasCats && !sending;
            if (btnManual != null) btnManual.Enabled = on;
            if (btnManualAdd != null) btnManualAdd.Enabled = on && !sending;
        }

        /// <summary>
        /// 비활성화되면 어두운 바탕 + 또렷한 회색 글자로 보이게 함.
        /// (기본 Flat 버튼은 비활성화 때 글자색 설정을 무시하고 아주 어두운 색으로 그려서 잘 안 보였음 -> 직접 그림)
        /// </summary>
        private static void WireDisabledLook(Button b, bool primary)
        {
            Action apply = () =>
            {
                if (b.Enabled)
                {
                    b.BackColor = primary ? UiTheme.Accent : UiTheme.Panel2;
                    b.ForeColor = Color.White;
                    b.FlatAppearance.BorderSize = primary ? 0 : 1;
                    b.Cursor = Cursors.Hand;
                }
                else
                {
                    b.FlatAppearance.BorderSize = 1;
                    b.Cursor = Cursors.Default;
                }
                b.Invalidate();
            };
            b.EnabledChanged += (s, e) => apply();
            b.Paint += (s, e) =>
            {
                if (b.Enabled) return;
                Graphics g = e.Graphics;
                g.Clear(Color.FromArgb(30, 38, 52));
                using (var pen = new Pen(UiTheme.Border, 1f)) g.DrawRectangle(pen, 0, 0, b.Width - 1, b.Height - 1);
                TextRenderer.DrawText(g, b.Text, b.Font, b.ClientRectangle, Color.FromArgb(160, 170, 186),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            };
        }

        private void AddSelected()
        {
            if (sending || lvItems.SelectedItems.Count == 0 || lvCat.SelectedItems.Count == 0) return;
            if (!Connected())
            {
                lblInfo.Text = "게임이 접속해 있어야 추가할 수 있어요.";
                lblInfo.ForeColor = UiTheme.Warn;
                return;
            }
            int cat = (int)lvCat.SelectedItems[0].Tag;
            var ids = new List<int>();
            foreach (ListViewItem it in lvItems.SelectedItems) ids.Add((int)it.Tag);
            StartSending(cat, ids);
            lblInfo.Text = ids.Count + "개 추가 요청을 보냈어요";
            lblInfo.ForeColor = UiTheme.Ok;
        }

        private void AddManual()
        {
            short type, code;
            if (!short.TryParse(txtType.Text, out type) || !short.TryParse(txtCode.Text, out code))
            {
                lblInfo.Text = "분류 ID와 아이템 ID를 숫자로 입력해 주세요.";
                lblInfo.ForeColor = UiTheme.Warn;
                return;
            }
            if (sending) return;
            if (!Connected())
            {
                lblInfo.Text = "게임이 접속해 있어야 추가할 수 있어요.";
                lblInfo.ForeColor = UiTheme.Warn;
                return;
            }
            StartSending(type, new List<int> { code });
            lblInfo.Text = "분류 " + type + " / ID " + code + " 추가 요청을 보냈어요";
            lblInfo.ForeColor = UiTheme.Ok;
        }

        private void StartSending(int cat, List<int> ids)
        {
            sending = true;
            btnAdd.Enabled = false;
            var t = new Thread(() =>
            {
                try
                {
                    foreach (int id in ids)
                    {
                        GetKart.SendItem((short)cat, (short)id);
                        Thread.Sleep(250);
                    }
                }
                catch (Exception ex) { AppLog.Error("아이템 추가 중 예외", ex); }
                finally
                {
                    sending = false;
                    try { if (!IsDisposed) BeginInvoke(new Action(UpdateConnection)); } catch { }
                }
            });
            t.IsBackground = true;
            t.Start();
        }
    }
}
