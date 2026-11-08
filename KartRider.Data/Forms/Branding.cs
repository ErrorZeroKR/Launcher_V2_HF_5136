using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows.Forms;
using Launcher.Properties;

namespace KartRider
{
    /// <summary>프로그램 버전 정보 (헤더 제목 옆과 로그에 표시). 버전을 올릴 때 여기만 고치면 됨.</summary>
    public static class AppInfo
    {
        /// <summary>프로그램 버전 (헤더에 v1.0.0 으로 표시)</summary>
        public const string Version = "1.0.0";

        /// <summary>내부 빌드 표시 (로그 첫머리에 출력되어, 어떤 빌드가 실행 중인지 확인용)</summary>
        public const string Build = "r39";

        /// <summary>실행 파일의 빌드(수정) 시각</summary>
        public static string BuildTime
        {
            get
            {
                try { return File.GetLastWriteTime(Environment.ProcessPath).ToString("yyyy-MM-dd HH:mm"); }
                catch { return ""; }
            }
        }
    }

    /// <summary>
    /// 런처 로고. 창 아이콘 · 실행 파일 아이콘 · 헤더 로고가 같은 이미지를 쓰므로 한 곳만 바꾸면 모두 같이 바뀜.
    ///  - 기본: Properties\favicon.ico  (프로젝트의 ApplicationIcon 이자 Resources.Icon — 이 파일을 교체하고 다시 빌드)
    ///  - 빌드 없이 바로 확인하려면 실행 폴더의 Profile\logo.png (또는 logo.jpg / logo.bmp / logo.ico) 를 두면 그 이미지를 우선 사용
    /// </summary>
    public static class AppLogo
    {
        private static readonly string[] OverrideFiles = { "logo.png", "logo.jpg", "logo.jpeg", "logo.bmp", "logo.ico" };

        /// <summary>외부 로고 파일이 있으면 그 경로 (없으면 null)</summary>
        public static string OverridePath()
        {
            foreach (string name in OverrideFiles)
            {
                string p = Path.Combine(Environment.CurrentDirectory, "Profile", name);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static bool IsIco(string path)
        {
            return path.EndsWith(".ico", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>헤더 로고용 이미지 (크게 읽어 두고 그릴 때 줄임)</summary>
        public static Bitmap LoadBitmap()
        {
            try
            {
                string p = OverridePath();
                if (p != null)
                {
                    if (IsIco(p))
                    {
                        using (var ic = new Icon(p, new Size(128, 128))) return ic.ToBitmap();
                    }
                    using (Image img = Image.FromFile(p)) return new Bitmap(img);   // 복사본을 써서 파일이 잠기지 않게 함
                }
            }
            catch { }
            try
            {
                using (var ic = new Icon(Resources.Icon, new Size(128, 128))) return ic.ToBitmap();
            }
            catch { return null; }
        }

        /// <summary>창 제목 표시줄/작업 표시줄 아이콘</summary>
        public static Icon LoadIcon()
        {
            try
            {
                string p = OverridePath();
                if (p != null)
                {
                    if (IsIco(p)) return new Icon(p);
                    using (Image img = Image.FromFile(p))
                    using (var bmp = new Bitmap(img, new Size(64, 64)))
                    {
                        return Icon.FromHandle(bmp.GetHicon());   // 창 아이콘으로 계속 쓰므로 핸들은 닫지 않음
                    }
                }
            }
            catch { }
            return Resources.Icon;
        }
    }

    /// <summary>로고 이미지를 고품질로 줄여서 그리는 컨트롤 (비율 유지, 가운데 맞춤, 투명 배경)</summary>
    public class LogoBox : Control
    {
        private Image image;

        public LogoBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            TabStop = false;
        }

        public Image Image
        {
            get { return image; }
            set { image = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (image == null || Width <= 0 || Height <= 0) return;
            Graphics g = e.Graphics;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            float scale = Math.Min((float)Width / image.Width, (float)Height / image.Height);
            float w = image.Width * scale;
            float h = image.Height * scale;
            g.DrawImage(image, (Width - w) / 2f, (Height - h) / 2f, w, h);
        }
    }

    /// <summary>작은 알약 모양 표시 (예: v1.0.0)</summary>
    public class PillLabel : Control
    {
        public PillLabel(string text)
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Font = UiTheme.UiFont(8.5F, FontStyle.Bold);
            ForeColor = UiTheme.Accent;
            Text = text;
            Size = GetPreferredSize(Size.Empty);
        }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size t = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
            return new Size(t.Width + t.Height + 6, t.Height + 8);
        }

        private static GraphicsPath Pill(RectangleF r)
        {
            var p = new GraphicsPath();
            float d = Math.Max(1f, r.Height);
            p.AddArc(r.X, r.Y, d, d, 90, 180);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 180);
            p.CloseFigure();
            return p;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1.5f, Height - 1.5f);
            using (GraphicsPath path = Pill(r))
            using (var fill = new SolidBrush(UiTheme.Panel2))
            using (var pen = new Pen(UiTheme.Border, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
            TextRenderer.DrawText(g, Text, Font, new Rectangle(0, 0, Width, Height), ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        }
    }
}
