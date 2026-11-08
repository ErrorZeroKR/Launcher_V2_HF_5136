using System;
using System.IO;
using System.Net;
using System.Text.Json;

namespace KartRider
{
    /// <summary>런처 설정 (Profile\launcher_settings.json). 설정 패널에서 바꾸면 자동 저장.</summary>
    public class LauncherSettings
    {
        // 패킷 로그
        public int PacketLogMode { get; set; } = 0;        // 0 끔 / 1 이름만 / 2 이름 + 헥스
        public bool PacketLogSent { get; set; } = true;    // 송신 패킷도 표시
        public int PacketHexBytes { get; set; } = 64;      // 헥스로 보여줄 최대 바이트

        // 서버 (다음 실행부터 적용)
        public string ServerIP { get; set; } = "127.0.0.1";
        public int ServerPort { get; set; } = 39312;

        // 게임 실행
        public bool RunAsAdmin { get; set; } = true;
        public string LaunchArgs { get; set; } = "-profile:launcher";
        public bool RemoveNgsOn { get; set; } = true;
        public bool RemoveSecModule { get; set; } = false;
        public string DatapackOffElement { get; set; } = "<datapackOff path='DataRaw' checkRfl='false'/>";   // Data 방식으로 바꿀 때 KartRider.xml 에서 뺀 항목 (DataRaw 로 다시 바꿀 때 복원)

        // 실험 기능 (아이템 인식 / 카트·펫 스펙은 항상 클라이언트 데이터를 읽으므로 더 이상 설정이 아님)
        public int SettingsVersion { get; set; } = 0;
        public bool AutoAddNewItems { get; set; } = true;        // 게임 데이터에 새로 생긴 아이템을 인벤토리에 자동 추가
        public bool LoadContentDocs { get; set; } = false;       // 퀘스트·상점·이벤트 등 문서 읽기 (실험) [재시작 필요]
        public bool PinLog { get; set; } = false;                // 게임 시작 시 PIN 내용을 로그에 출력

        // 런처 (다음 실행부터 적용)
        public bool ShowConsole { get; set; } = false;
        public int LogKeepCount { get; set; } = 20;
        public bool ShowSettingsPanel { get; set; } = true;
        public int LogHiddenMask { get; set; } = LogCat.DefaultHiddenMask;   // 로그 창에서 숨길 항목(비트마스크, LogCat 참고)

        public static LauncherSettings Current = new LauncherSettings();
        private static readonly object saveLock = new object();

        public static string FilePath
        {
            get { return Path.Combine(Environment.CurrentDirectory, "Profile", "launcher_settings.json"); }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    LauncherSettings s = JsonSerializer.Deserialize<LauncherSettings>(File.ReadAllText(FilePath));
                    if (s != null) Current = s;
                }
            }
            catch { Current = new LauncherSettings(); }
            if (Current.SettingsVersion < 2)
            {
                // 이전 빌드에서 저장된 설정은 새 기본값(최소 기능)으로 초기화
                Current.PacketLogMode = 0;
                Current.RemoveSecModule = false;
                Current.LoadContentDocs = false;
                Current.PinLog = false;
                Current.SettingsVersion = 2;
            }
            Current.Sanitize();
        }

        public static void Save()
        {
            lock (saveLock)
            {
                try
                {
                    Current.Sanitize();
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var opt = new JsonSerializerOptions();
                    opt.WriteIndented = true;
                    File.WriteAllText(FilePath, JsonSerializer.Serialize(Current, opt));
                }
                catch (Exception ex) { Console.WriteLine("[설정] 저장 실패: " + ex.Message); }
            }
        }

        private void Sanitize()
        {
            if (PacketLogMode < 0 || PacketLogMode > 2) PacketLogMode = 2;
            if (PacketHexBytes < 16) PacketHexBytes = 16;
            if (PacketHexBytes > 1024) PacketHexBytes = 1024;
            IPAddress ip;
            if (string.IsNullOrWhiteSpace(ServerIP) || !IPAddress.TryParse(ServerIP.Trim(), out ip)) ServerIP = "127.0.0.1";
            ServerIP = ServerIP.Trim();
            if (ServerPort < 1 || ServerPort > 65535) ServerPort = 39312;
            if (LaunchArgs == null) LaunchArgs = "";
            if (LogKeepCount < 1) LogKeepCount = 1;
            if (LogKeepCount > 500) LogKeepCount = 500;
        }

        public string Describe()
        {
            string[] modes = { "끔", "이름만", "이름+헥스" };
            return string.Format("문서 읽기(실험) {0}, PIN 로그 {1} / 패킷 로그 {2}{3} / 서버 {4}:{5} / 실행 인자 '{6}', 관리자 {7} / PIN: NgsOn 제거 {8}, secModule 제거 {9}",
                LoadContentDocs, PinLog,
                modes[PacketLogMode], PacketLogSent ? "(송신 포함)" : "(수신만)", ServerIP, ServerPort,
                LaunchArgs, RunAsAdmin, RemoveNgsOn, RemoveSecModule);
        }
    }
}
