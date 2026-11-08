using System;
using System.Text;
using KartRider.IO;
using KartRider_PacketName;

namespace KartRider
{
    /// <summary>
    /// 패킷 송수신 로그. 이름 + 길이(+ 앞부분 헥스)를 로그 창/파일에 남김.
    /// 설정(LauncherSettings)은 실시간으로 반영됨.
    /// </summary>
    public static class PacketLog
    {
        public static string NameOf(uint hash)
        {
            return Enum.IsDefined(typeof(PacketName), hash) ? ((PacketName)hash).ToString() : "알 수 없음 0x" + hash.ToString("X8");
        }

        public static void Received(InPacket p)
        {
            if (LauncherSettings.Current.PacketLogMode <= 0) return;
            try { Write("[패킷 수신]", p.ToArray(), true); }
            catch (Exception ex) { Console.WriteLine("[오류] 패킷 로그 실패(수신): " + ex.Message); }
        }

        public static void Sent(OutPacket p)
        {
            var s = LauncherSettings.Current;
            if (s.PacketLogMode <= 0 || !s.PacketLogSent) return;
            try { Write("[패킷 송신]", p.ToArray(), true); }
            catch (Exception ex) { Console.WriteLine("[오류] 패킷 로그 실패(송신): " + ex.Message); }
        }

        /// <summary>이름 없이 바이트만 보내는 송신 (첫 접속 메시지 등)</summary>
        public static void SentRaw(byte[] data)
        {
            var s = LauncherSettings.Current;
            if (s.PacketLogMode <= 0 || !s.PacketLogSent) return;
            try { Write("[패킷 송신(RAW)]", data, false); }
            catch (Exception ex) { Console.WriteLine("[오류] 패킷 로그 실패(RAW): " + ex.Message); }
        }

        private static void Write(string tag, byte[] data, bool hasName)
        {
            if (data == null) return;
            var s = LauncherSettings.Current;
            var sb = new StringBuilder();
            if (hasName && data.Length >= 4)
                sb.Append(tag).Append(' ').Append(NameOf(BitConverter.ToUInt32(data, 0))).Append(" (").Append(data.Length).Append("바이트)");
            else
                sb.Append(tag).Append(" (").Append(data.Length).Append("바이트)");

            if (s.PacketLogMode >= 2 && data.Length > 0)
            {
                int max = Math.Min(data.Length, Math.Max(16, s.PacketHexBytes));
                for (int i = 0; i < max; i += 16)
                {
                    sb.Append(Environment.NewLine).Append("    ");
                    int end = Math.Min(max, i + 16);
                    for (int j = i; j < end; j++) sb.Append(data[j].ToString("X2")).Append(' ');
                }
                if (data.Length > max)
                    sb.Append(Environment.NewLine).Append("    ... (+").Append(data.Length - max).Append("바이트 생략)");
            }
            Console.WriteLine(sb.ToString()); // 한 번에 출력해서 다른 스레드의 로그와 섞이지 않게 함
        }
    }
}
