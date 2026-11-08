using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>런처 공통 색상/글꼴/버튼 스타일 (다크 테마)</summary>
    public static class UiTheme
    {
        public static readonly Color Bg = Color.FromArgb(14, 17, 22);
        public static readonly Color Panel = Color.FromArgb(22, 27, 34);
        public static readonly Color Panel2 = Color.FromArgb(30, 37, 48);
        public static readonly Color Border = Color.FromArgb(42, 51, 66);
        public static readonly Color Text = Color.FromArgb(230, 237, 243);
        public static readonly Color Muted = Color.FromArgb(139, 152, 169);
        public static readonly Color Accent = Color.FromArgb(47, 129, 247);
        public static readonly Color AccentHover = Color.FromArgb(77, 148, 255);
        public static readonly Color Ok = Color.FromArgb(63, 185, 80);
        public static readonly Color Warn = Color.FromArgb(210, 153, 34);
        public static readonly Color Err = Color.FromArgb(248, 81, 73);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

        public static Font UiFont(float size, FontStyle style)
        {
            return new Font("맑은 고딕", size, style, GraphicsUnit.Point, 129);
        }

        public static Font UiFont(float size)
        {
            return UiFont(size, FontStyle.Regular);
        }

        public static Font MonoFont(float size)
        {
            return new Font("Consolas", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Button MakeButton(string text, bool primary, int width, int height)
        {
            var b = new Button();
            b.Text = text;
            b.Width = width;
            b.Height = height;
            b.FlatStyle = FlatStyle.Flat;
            b.UseVisualStyleBackColor = false;
            b.BackColor = primary ? Accent : Panel2;
            b.ForeColor = Color.White;
            b.Cursor = Cursors.Hand;
            b.Font = primary ? UiFont(11F, FontStyle.Bold) : UiFont(9F);
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Border;
            b.FlatAppearance.MouseOverBackColor = primary ? AccentHover : Color.FromArgb(40, 49, 64);
            b.FlatAppearance.MouseDownBackColor = primary ? Color.FromArgb(31, 111, 235) : Color.FromArgb(52, 63, 82);
            return b;
        }

        /// <summary>목록(ListView 등)의 기본 스크롤바를 어두운 테마로 (Windows 10 1809 이상, 실패해도 무시)</summary>
        public static void DarkScrollbars(Control c)
        {
            EventHandler apply = (s, e) => { try { SetWindowTheme(c.Handle, "DarkMode_Explorer", null); } catch { } };
            if (c.IsHandleCreated) apply(c, EventArgs.Empty);
            c.HandleCreated += apply;
        }

        /// <summary>제목 표시줄을 다크 모드로 (Windows 10 2004 이상, 실패해도 무시)</summary>
        public static void DarkTitleBar(Form f)
        {
            try
            {
                int v = 1;
                if (DwmSetWindowAttribute(f.Handle, 20, ref v, sizeof(int)) != 0)
                    DwmSetWindowAttribute(f.Handle, 19, ref v, sizeof(int));
            }
            catch { }
        }

        /// <summary>기존 폼(GetKart 등)에 다크 테마를 입힘</summary>
        public static void Apply(Control root)
        {
            root.BackColor = Panel;
            root.ForeColor = Text;
            var form = root as Form;
            if (form != null) form.HandleCreated += (s, e) => DarkTitleBar(form);
            foreach (Control c in root.Controls) ApplyControl(c);
        }

        private static void ApplyControl(Control c)
        {
            var b = c as Button;
            var t = c as TextBox;
            var cb = c as ComboBox;
            var l = c as Label;
            if (b != null)
            {
                b.FlatStyle = FlatStyle.Flat;
                b.UseVisualStyleBackColor = false;
                b.BackColor = Accent;
                b.ForeColor = Color.White;
                b.Cursor = Cursors.Hand;
                b.FlatAppearance.BorderSize = 0;
                b.FlatAppearance.MouseOverBackColor = AccentHover;
            }
            else if (t != null)
            {
                t.BackColor = Panel2;
                t.ForeColor = Text;
            }
            else if (cb != null)
            {
                cb.FlatStyle = FlatStyle.Flat;
                cb.BackColor = Panel2;
                cb.ForeColor = Text;
            }
            else if (l != null)
            {
                l.ForeColor = Text;
                l.BackColor = Color.Transparent;
            }
            foreach (Control child in c.Controls) ApplyControl(child);
        }
    }
}
