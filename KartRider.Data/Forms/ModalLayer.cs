using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// 카드(아이템 추가 / 로그 설정) 뒤에 깔리는 반투명 배경 층.
    /// 런처 화면 캡처 위에 어두운 막을 씌우고, 페이드 중에는 카드 이미지(CardImage)를 투명도(CardAlpha)를 바꿔 가며 직접 그림.
    /// 컨트롤 투명도(레이어드 창)를 쓰지 않고 이 층 하나만 그려서, 환경에 따라 멈추거나 검게 나오는 문제가 없음.
    /// </summary>
    public class ModalLayer : Panel
    {
        private Bitmap backdrop;
        private Bitmap cardImage;
        private int dimAlpha, cardAlpha;
        private Rectangle cardBounds;

        public ModalLayer()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = UiTheme.Bg;
        }

        /// <summary>어둡게 하기 전의 런처 화면 캡처 (없으면 단색 배경)</summary>
        public Bitmap Backdrop
        {
            get { return backdrop; }
            set { backdrop = value; Invalidate(); }
        }

        /// <summary>페이드 중에 그릴 카드 이미지 (null 이면 그리지 않음). 새 값을 넣으면 이전 이미지는 정리됨</summary>
        public Bitmap CardImage
        {
            get { return cardImage; }
            set
            {
                Bitmap old = cardImage;
                cardImage = value;
                if (old != null && !ReferenceEquals(old, value)) old.Dispose();
                Invalidate();
            }
        }

        /// <summary>카드 이미지를 그릴 위치/크기</summary>
        public Rectangle CardBounds
        {
            get { return cardBounds; }
            set { cardBounds = value; Invalidate(); }
        }

        /// <summary>배경 어둡기 (0~255)</summary>
        public int DimAlpha
        {
            get { return dimAlpha; }
            set
            {
                value = Math.Max(0, Math.Min(255, value));
                if (value == dimAlpha) return;
                dimAlpha = value;
                Invalidate();
            }
        }

        /// <summary>카드 이미지 투명도 (0 = 투명, 255 = 불투명)</summary>
        public int CardAlpha
        {
            get { return cardAlpha; }
            set
            {
                value = Math.Max(0, Math.Min(255, value));
                if (value == cardAlpha) return;
                cardAlpha = value;
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;

            if (backdrop != null) g.DrawImage(backdrop, new Rectangle(0, 0, backdrop.Width, backdrop.Height));
            else g.Clear(UiTheme.Bg);

            if (dimAlpha > 0)
            {
                using (var br = new SolidBrush(Color.FromArgb(dimAlpha, 4, 7, 12)))
                    g.FillRectangle(br, ClientRectangle);
            }

            if (cardImage != null && cardAlpha > 0)
            {
                if (cardAlpha >= 255)
                {
                    g.DrawImage(cardImage, cardBounds);
                }
                else
                {
                    using (var attr = new ImageAttributes())
                    {
                        var m = new ColorMatrix();
                        m.Matrix33 = cardAlpha / 255f;
                        attr.SetColorMatrix(m, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                        g.DrawImage(cardImage, cardBounds, 0, 0, cardImage.Width, cardImage.Height, GraphicsUnit.Pixel, attr);
                    }
                }
            }
        }
    }
}
