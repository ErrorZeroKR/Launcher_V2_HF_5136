using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// 다크 테마 전용 드롭다운. 닫힌 모양(둥근 상자 + 화살표)과 펼친 목록을 직접 그려서
    /// 기본 ComboBox 의 흰색 화살표 버튼 없이 런처 디자인과 같은 모양으로 보임.
    /// (Items / SelectedIndex / SelectedItem / SelectedIndexChanged 는 ComboBox 와 같은 방식으로 사용)
    /// </summary>
    public class ThemedComboBox : Control
    {
        public readonly List<string> Items = new List<string>();
        private int selectedIndex = -1;
        private int hotIndex = -1;
        private bool hover;
        private int closedAt;
        private ToolStripDropDown dropDown;
        private ToolStripControlHost host;
        private ListBox list;

        public event EventHandler SelectedIndexChanged;

        public ThemedComboBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            BackColor = UiTheme.Panel2;
            ForeColor = UiTheme.Text;
            Font = UiTheme.UiFont(10.5F);
            Height = 34;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value < -1 || value >= Items.Count) value = -1;
                if (value == selectedIndex) return;
                selectedIndex = value;
                Invalidate();
                EventHandler h = SelectedIndexChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        public object SelectedItem
        {
            get { return (selectedIndex >= 0 && selectedIndex < Items.Count) ? Items[selectedIndex] : null; }
        }

        private bool IsOpen { get { return dropDown != null && dropDown.Visible; } }

        // ---------- 그리기 ----------
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
            g.Clear(Parent != null ? Parent.BackColor : UiTheme.Panel);

            bool open = IsOpen;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            Color border = (open || hover || Focused) ? UiTheme.Accent : UiTheme.Border;
            Color fill = hover ? Color.FromArgb(38, 47, 62) : UiTheme.Panel2;
            using (GraphicsPath path = Round(r, 7f))
            using (var br = new SolidBrush(fill))
            using (var pen = new Pen(border, 1.3f))
            {
                g.FillPath(br, path);
                g.DrawPath(pen, path);
            }

            string text = SelectedItem as string ?? "";
            var tr = new Rectangle(14, 0, Math.Max(10, Width - 14 - 36), Height);
            TextRenderer.DrawText(g, text, Font, tr, ForeColor, TextFlags());

            // 오른쪽 화살표 (열리면 위쪽 방향)
            float cx = Width - 22f, cy = Height / 2f;
            using (var pen = new Pen(open ? UiTheme.Accent : UiTheme.Muted, 1.8f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                PointF[] pts = open
                    ? new[] { new PointF(cx - 5, cy + 2), new PointF(cx, cy - 3), new PointF(cx + 5, cy + 2) }
                    : new[] { new PointF(cx - 5, cy - 2), new PointF(cx, cy + 3), new PointF(cx + 5, cy - 2) };
                g.DrawLines(pen, pts);
            }
        }

        private static TextFormatFlags TextFlags()
        {
            return TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix;
        }

        // ---------- 마우스 / 키보드 ----------
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Focus();
            if (IsOpen) { dropDown.Close(); return; }
            if (Environment.TickCount - closedAt < 250) return;   // 이 클릭으로 방금 닫힌 경우 다시 열지 않음
            ShowDropDown();
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Up || keyData == Keys.Down) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Down && selectedIndex < Items.Count - 1) SelectedIndex = selectedIndex + 1;
            else if (e.KeyCode == Keys.Up && selectedIndex > 0) SelectedIndex = selectedIndex - 1;
            else if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) ShowDropDown();
        }

        // ---------- 펼친 목록 ----------
        private void EnsureDropDown()
        {
            if (dropDown != null) return;
            list = new ListBox();
            list.BorderStyle = BorderStyle.None;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.Font = Font;
            list.ItemHeight = Font.Height + 12;
            list.BackColor = UiTheme.Panel2;
            list.ForeColor = UiTheme.Text;
            list.IntegralHeight = false;
            list.DrawItem += DrawListItem;
            list.MouseMove += (s, e) =>
            {
                int i = list.IndexFromPoint(e.Location);
                if (i != hotIndex) { hotIndex = i; list.Invalidate(); }
            };
            list.MouseLeave += (s, e) => { hotIndex = -1; list.Invalidate(); };
            list.MouseClick += (s, e) =>
            {
                int i = list.IndexFromPoint(e.Location);
                if (i >= 0) { SelectedIndex = i; dropDown.Close(); }
            };

            host = new ToolStripControlHost(list);
            host.AutoSize = false;
            host.Margin = Padding.Empty;
            host.Padding = Padding.Empty;

            dropDown = new ToolStripDropDown();
            dropDown.Padding = new Padding(1);
            dropDown.BackColor = UiTheme.Border;
            dropDown.Items.Add(host);
            dropDown.Closed += (s, e) => { closedAt = Environment.TickCount; Invalidate(); };
        }

        private void ShowDropDown()
        {
            if (Items.Count == 0) return;
            EnsureDropDown();
            list.Items.Clear();
            foreach (string s in Items) list.Items.Add(s);
            int rows = Math.Min(Items.Count, 12);
            list.Size = new Size(Math.Max(Width - 2, 80), rows * list.ItemHeight);
            host.Size = list.Size;
            hotIndex = selectedIndex;
            if (selectedIndex >= 0) list.TopIndex = Math.Max(0, selectedIndex - rows + 1);
            dropDown.Show(this, new Point(0, Height + 2));
            Invalidate();
        }

        private void DrawListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            bool selected = e.Index == selectedIndex;
            bool hot = e.Index == hotIndex;
            Color back = selected ? UiTheme.Accent : (hot ? Color.FromArgb(46, 56, 74) : UiTheme.Panel2);
            using (var br = new SolidBrush(back)) e.Graphics.FillRectangle(br, e.Bounds);
            var tr = new Rectangle(e.Bounds.X + 14, e.Bounds.Y, Math.Max(10, e.Bounds.Width - 14), e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, Convert.ToString(list.Items[e.Index]), list.Font, tr, selected ? Color.White : UiTheme.Text, TextFlags());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && dropDown != null) dropDown.Dispose();
            base.Dispose(disposing);
        }
    }
}
