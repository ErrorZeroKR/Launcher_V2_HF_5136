using System;

namespace KartRider
{
    /// <summary>
    /// 로그 창에 보여줄 항목 분류. 설정 > 로그 표시 에서 항목별로 켜고 끔 (로그 파일에는 항상 모두 저장).
    /// </summary>
    public static class LogCat
    {
        public const int Error = 0;        // 오류 (항상 표시)
        public const int Status = 1;       // 런처 상태 ([런처] [설정] [로그])
        public const int Data = 2;         // 게임 데이터 읽기 ([Rho])
        public const int Pin = 3;          // PIN 파일 준비
        public const int Server = 4;       // 서버 접속 / 로그인
        public const int Packet = 5;       // 패킷 송수신
        public const int SpecApplied = 6;  // 스펙 적용 결과 ([KartSpec] [FlyingPet])
        public const int SpecDump = 7;     // 스펙 상세값 (카트 성능 수십 줄)
        public const int Equip = 8;        // 장착 아이템 / 파츠 정보
        public const int Play = 9;         // 플레이 시작 정보
        public const int Ride = 10;        // 주행 중 상태 (PlaneCheck 반복 줄)
        public const int Other = 11;       // 기타
        public const int Count = 12;

        public static readonly string[] Names =
        {
            "오류 (항상 표시)", "런처 상태", "게임 데이터 읽기", "PIN 파일", "서버·로그인", "패킷 송수신",
            "스펙 적용 결과", "스펙 상세값", "장착·파츠 정보", "플레이 시작", "주행 상태", "기타"
        };

        /// <summary>기본으로 숨길 항목: 줄 수만 많은 상세값</summary>
        public const int DefaultHiddenMask = (1 << Status) | (1 << Packet) | (1 << SpecDump) | (1 << Equip) | (1 << Ride) | (1 << Other);

        public static event EventHandler Changed;

        public static bool IsHidden(int cat)
        {
            if (cat <= Error || cat >= Count) return false;
            return (LauncherSettings.Current.LogHiddenMask & (1 << cat)) != 0;
        }

        public static void SetHidden(int cat, bool hidden)
        {
            if (cat <= Error || cat >= Count) return;
            int m = LauncherSettings.Current.LogHiddenMask;
            int n = hidden ? (m | (1 << cat)) : (m & ~(1 << cat));
            SetMask(n);
        }

        public static void SetMask(int mask)
        {
            mask &= ~(1 << Error);
            if (mask == LauncherSettings.Current.LogHiddenMask) return;
            LauncherSettings.Current.LogHiddenMask = mask;
            EventHandler h = Changed;
            if (h != null) h(null, EventArgs.Empty);
        }
    }

    /// <summary>
    /// 로그 한 줄이 어느 항목인지 판단 (HF 원본의 태그 없는 줄도 패턴으로 분류).
    /// 구분선(-----)으로 둘러싸인 값 목록은 한 덩어리로 묶어서 같은 항목으로 처리.
    /// </summary>
    public sealed class LogClassifier
    {
        private bool inBlock;
        private int blockCat = -1;
        private int blockLines;
        private int prev = LogCat.Other;
        private bool pathNext;

        private static bool IsDash(string m)
        {
            if (m.Length < 10) return false;
            for (int i = 0; i < m.Length; i++) if (m[i] != '-') return false;
            return true;
        }

        /// <param name="opensBlockDash">값 목록을 여는 구분선이면 true (어느 항목인지 다음 줄에서 정해짐)</param>
        public int Classify(string m, out bool opensBlockDash)
        {
            opensBlockDash = false;
            if (m.Length == 0) return prev;
            if (m.StartsWith("   ") || m.StartsWith("\t")) return prev;   // 스택/헥스처럼 이어지는 줄

            bool dash = IsDash(m);
            if (inBlock)
            {
                blockLines++;
                if (dash)
                {
                    inBlock = false;
                    return Set(blockCat < 0 ? LogCat.Other : blockCat);
                }
                if (m.StartsWith("[") || blockLines > 400)
                {
                    inBlock = false;   // 구분선이 짝이 안 맞을 때를 대비한 안전장치
                }
                else
                {
                    if (blockCat < 0) blockCat = m.StartsWith("draftMulAccelFactor") ? LogCat.SpecDump : LogCat.Equip;
                    return Set(blockCat);
                }
            }

            if (dash)
            {
                inBlock = true;
                blockCat = -1;
                blockLines = 0;
                opensBlockDash = true;
                return Set(LogCat.Other);
            }
            if (m.StartsWith("SetRiderItem"))
            {
                inBlock = true;
                blockCat = LogCat.Equip;
                blockLines = 0;
                return Set(LogCat.Equip);
            }

            if (pathNext) { pathNext = false; return Set(LogCat.Pin); }

            if (m.StartsWith("[오류]") || m.StartsWith("[SOCKET ERR]") || m.StartsWith("Disconnected session") || m.Contains("Exception")) return Set(LogCat.Error);
            if (m.StartsWith("[패킷")) return Set(LogCat.Packet);
            if (m.StartsWith("[Rho]")) return Set(LogCat.Data);
            if (m.StartsWith("[PIN]")) return Set(LogCat.Pin);
            if (m.StartsWith("Backing up old PinFile")) { pathNext = true; return Set(LogCat.Pin); }
            if (m.StartsWith("Changing IP Addr") || m.StartsWith("Removing ")) return Set(LogCat.Pin);
            if (m.StartsWith("[서버]") || m.StartsWith("[Session]") || m.StartsWith("Login...") || m.StartsWith("Load server IP")) return Set(LogCat.Server);
            if (m.StartsWith("[KartSpec") || m.StartsWith("[FlyingPet]")) return Set(LogCat.SpecApplied);
            if (m.StartsWith("[런처]") || m.StartsWith("[설정]") || m.StartsWith("[로그]") || m.StartsWith("Process:") || m.StartsWith("Profile:") || m.StartsWith("NewKart:")) return Set(LogCat.Status);
            if (m.StartsWith("Part_Spec") || m.StartsWith("Tune_Type") || m.StartsWith("Plant_Spec") || m.StartsWith("Level_Spec") || m.StartsWith("Set_Kart")) return Set(LogCat.Equip);
            if (m.StartsWith("StartTimeAttack") || m.StartsWith("SpeedType") || m.StartsWith("랭킹 타임어택") || m.StartsWith("스토리 모드") || m.StartsWith("챌린저")) return Set(LogCat.Play);
            if (m.StartsWith("PlaneCheck")) return Set(LogCat.Ride);
            return Set(LogCat.Other);
        }

        private int Set(int cat)
        {
            prev = cat;
            return cat;
        }
    }
}
