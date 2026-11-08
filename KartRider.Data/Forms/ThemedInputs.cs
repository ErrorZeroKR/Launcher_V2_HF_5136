using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace KartRider
{
    internal static class InputPaint
    {
        public static GraphicsPath Round(RectangleF r, float radius)
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

        /// <summary>입력란 바탕: 둥근 상자 (포커스=파란 테두리, 오류=빨간 테두리, 마우스 올림=밝은 테두리)</summary>
        public static void DrawBox(Graphics g, int width, int height, Color outer, bool focused, bool hover, bool invalid)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(outer);
            var r = new RectangleF(0.5f, 0.5f, width - 1.5f, height - 1.5f);
            Color border = invalid ? UiTheme.Err : (focused ? UiTheme.Accent : (hover ? UiTheme.Muted : UiTheme.Border));
            using (GraphicsPath path = Round(r, 7f))
            using (var fill = new SolidBrush(UiTheme.Panel2))
            using (var pen = new Pen(border, focused || invalid ? 1.6f : 1.2f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }
    }

    /// <summary>
    /// 런처 디자인에 맞춘 한 줄 입력란 (둥근 어두운 상자 + 포커스 테두리).
    /// 안쪽은 기본 TextBox 이지만 테두리를 없애고 바탕색을 맞춰서 한 덩어리로 보임.
    /// </summary>
    public class ThemedTextBox : Control
    {
        private readonly TextBox tb;
        private bool focused, hover, invalid;

        public ThemedTextBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Panel;
            Size = new Size(300, 36);

            tb = new TextBox();
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = UiTheme.Panel2;
            tb.ForeColor = UiTheme.Text;
            tb.Font = UiTheme.UiFont(9.5F);
            tb.GotFocus += (s, e) => { focused = true; Invalidate(); };
            tb.LostFocus += (s, e) => { focused = false; Invalidate(); };
            tb.TextChanged += (s, e) => OnTextChanged(EventArgs.Empty);
            tb.MouseEnter += (s, e) => { hover = true; Invalidate(); };
            tb.MouseLeave += (s, e) => { hover = false; Invalidate(); };
            Controls.Add(tb);
            LayoutInner();
        }

        public override string Text
        {
            get { return tb.Text; }
            set { if (tb.Text != value) tb.Text = value ?? ""; }
        }

        public int MaxLength
        {
            get { return tb.MaxLength; }
            set { tb.MaxLength = value; }
        }

        public string PlaceholderText
        {
            get { return tb.PlaceholderText; }
            set { tb.PlaceholderText = value; }
        }

        /// <summary>입력값이 올바르지 않으면 빨간 테두리로 표시</summary>
        public bool Invalid
        {
            get { return invalid; }
            set { if (invalid == value) return; invalid = value; Invalidate(); }
        }

        private void LayoutInner()
        {
            if (tb == null) return;
            int padX = (int)(12 * DeviceDpi / 96f);
            int h = tb.PreferredHeight;
            tb.SetBounds(padX, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - padX * 2), h);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInner(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); LayoutInner(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); tb.Focus(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); tb.Focus(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            InputPaint.DrawBox(e.Graphics, Width, Height, Parent != null ? Parent.BackColor : UiTheme.Panel, focused, hover, invalid);
        }
    }

    /// <summary>
    /// 런처 디자인에 맞춘 숫자 입력란: 숫자만 입력, 오른쪽 ▲▼ 로 증감, 위/아래 방향키 지원.
    /// </summary>
    public class ThemedNumberBox : Control
    {
        private readonly TextBox tb;
        private int min = 0, max = 100, step = 1, val;
        private bool focused, hover;
        private int hot;   // 0 = 없음, 1 = ▲, 2 = ▼

        public event EventHandler ValueChanged;

        public ThemedNumberBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Panel;
            Size = new Size(120, 36);

            tb = new TextBox();
            tb.BorderStyle = BorderStyle.None;
            tb.BackColor = UiTheme.Panel2;
            tb.ForeColor = UiTheme.Text;
            tb.Font = UiTheme.UiFont(9.5F);
            tb.Text = "0";
            tb.KeyPress += (s, e) => { if (!(char.IsDigit(e.KeyChar) || char.IsControl(e.KeyChar))) e.Handled = true; };
            tb.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up) { Value = val + step; e.Handled = true; }
                else if (e.KeyCode == Keys.Down) { Value = val - step; e.Handled = true; }
            };
            tb.TextChanged += (s, e) =>
            {
                int v;
                if (int.TryParse(tb.Text, out v)) SetValue(v, false);   // 입력 중에는 글자를 고치지 않음
            };
            tb.GotFocus += (s, e) => { focused = true; Invalidate(); };
            tb.LostFocus += (s, e) => { focused = false; tb.Text = val.ToString(); Invalidate(); };   // 벗어나면 범위에 맞춘 값으로 정리
            tb.MouseEnter += (s, e) => { hover = true; Invalidate(); };
            tb.MouseLeave += (s, e) => { hover = false; Invalidate(); };
            Controls.Add(tb);
            LayoutInner();
        }

        public int Minimum { get { return min; } set { min = value; SetValue(val, true); } }
        public int Maximum { get { return max; } set { max = value; SetValue(val, true); } }
        public int Increment { get { return step; } set { step = Math.Max(1, value); } }
        public int Value { get { return val; } set { SetValue(value, true); } }

        private void SetValue(int v, bool updateText)
        {
            v = Math.Max(min, Math.Min(max, v));
            bool changed = v != val;
            val = v;
            if (updateText && tb.Text != v.ToString()) tb.Text = v.ToString();
            if (changed)
            {
                Invalidate();
                EventHandler h = ValueChanged;
                if (h != null) h(this, EventArgs.Empty);
            }
        }

        private int ArrowWidth { get { return (int)(28 * DeviceDpi / 96f); } }

        private void LayoutInner()
        {
            if (tb == null) return;
            int padX = (int)(12 * DeviceDpi / 96f);
            int h = tb.PreferredHeight;
            tb.SetBounds(padX, Math.Max(0, (Height - h) / 2), Math.Max(10, Width - padX - ArrowWidth - 4), h);
        }

        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutInner(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); LayoutInner(); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; hot = 0; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            int h = e.X >= Width - ArrowWidth ? (e.Y < Height / 2 ? 1 : 2) : 0;
            if (h != hot) { hot = h; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            if (e.X >= Width - ArrowWidth)
            {
                tb.Focus();
                Value = val + (e.Y < Height / 2 ? step : -step);
            }
            else tb.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            InputPaint.DrawBox(g, Width, Height, Parent != null ? Parent.BackColor : UiTheme.Panel, focused, hover, false);

            int aw = ArrowWidth;
            int x0 = Width - aw;
            using (var pen = new Pen(UiTheme.Border, 1f))
                g.DrawLine(pen, x0, 6, x0, Height - 7);

            DrawChevron(g, x0 + aw / 2f, Height * 0.30f, true, hot == 1);
            DrawChevron(g, x0 + aw / 2f, Height * 0.70f, false, hot == 2);
        }

        private static void DrawChevron(Graphics g, float cx, float cy, bool up, bool active)
        {
            using (var pen = new Pen(active ? UiTheme.Accent : UiTheme.Muted, 1.7f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                float d = 3.2f;
                g.DrawLines(pen, up
                    ? new[] { new PointF(cx - 4, cy + d / 2), new PointF(cx, cy - d / 2), new PointF(cx + 4, cy + d / 2) }
                    : new[] { new PointF(cx - 4, cy - d / 2), new PointF(cx, cy + d / 2), new PointF(cx + 4, cy - d / 2) });
            }
        }
    }
}
