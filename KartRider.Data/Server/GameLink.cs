using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

namespace KartRider
{
    /// <summary>
    /// 게임 클라이언트가 지금 실제로 접속해 있는지 판단.
    /// (예전에는 접속 세션 객체가 남아 있기만 하면 '접속됨'으로 봐서, 게임을 끈 뒤에도 접속 중으로 표시됐음)
    ///  - 세션이 끊김 처리(mDisconnected)됐거나 소켓이 닫혔으면 접속 아님
    ///  - 같은 PC 에서 접속한 게임이면 KartRider 프로세스가 실제로 실행 중이어야 접속으로 봄 (원격 접속은 세션 상태만 확인)
    /// </summary>
    public static class GameLink
    {
        private static volatile bool lastRunning = false;

        // KartRider 프로세스가 떠 있는지 1초마다 백그라운드에서 확인해 둠
        // (화면 스레드에서 프로세스 목록을 조회하면 PC 에 따라 화면이 순간 멈추기 때문)
        private static readonly System.Threading.Timer poll = new System.Threading.Timer(_ =>
        {
            try { lastRunning = Process.GetProcessesByName("KartRider").Length > 0; }
            catch { lastRunning = true; }   // 확인하지 못하면 세션 상태를 믿음
        }, null, 0, 1000);

        /// <summary>로컬 KartRider 프로세스가 실행 중인지 (최대 1초 전 기준)</summary>
        public static bool GameRunning
        {
            get { return poll != null && lastRunning; }
        }

        public static bool IsConnected()
        {
            try
            {
                SessionGroup g = RouterListener.MySession;
                if (g == null || g.Client == null) return false;
                if (g.Client.mDisconnected != 0) return false;
                Socket s = g.Client.Socket;
                if (s == null || !s.Connected) return false;
                var remote = s.RemoteEndPoint as IPEndPoint;
                if (remote != null && IPAddress.IsLoopback(remote.Address) && !GameRunning) return false;
                return true;
            }
            catch { return false; }
        }
    }
}
