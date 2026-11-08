using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace KartRider
{
    /// <summary>
    /// 기본(두껍고 밝은) 스크롤바를 숨긴 ListView. 대신 옆에 ThemedScrollBar 를 붙여서 씀.
    /// 마우스 휠 / 키보드 스크롤은 그대로 동작하고, 스크롤 위치는 바깥에서 TopItem 으로 읽어서 스크롤바에 반영함.
    /// </summary>
    public class ThemedListView : ListView
    {
        [DllImport("user32.dll")]
        private static extern bool ShowScrollBar(IntPtr hWnd, int wBar, bool bShow);

        private const int SB_BOTH = 3;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (!IsHandleCreated) return;
            int msg = m.Msg;
            // 크기 변경 / 항목 변경 등으로 기본 스크롤바가 다시 나타날 수 있는 메시지 뒤에 바로 숨김
            if (msg == 0x0005 || msg == 0x0083 || msg == 0x0047 || msg == 0x007D || msg == 0x0115 || (msg >= 0x1000 && msg < 0x1200))
                ShowScrollBar(Handle, SB_BOTH, false);
        }
    }
}
