using System.Collections.Generic;

namespace KartRider
{
    /// <summary>
    /// 아이템 추가 창의 "분류" 설정. 이 파일만 고치고 다시 빌드하면 분류 이름 / 표시 여부 / 순서가 바뀝니다.
    ///
    /// 분류 ID 는 게임 데이터의 itemCatId 입니다. 런처가 내장 Item.xml 과 아이템 ID 를 비교해서
    /// 분류 이름을 자동으로 붙이지만, 틀린 이름이 있으면 아래 Names 에 { 분류ID, "이름" } 을 적어서 덮어쓰세요.
    ///
    /// 처음 아이템 추가 창을 열면 로그에 "아이템 분류 목록"이 복사해서 붙여 넣을 수 있는 형태로 나옵니다.
    /// (분류 ID / 현재 이름 / 아이템 개수 / 예시 아이템 이름이 같이 나와서 맞는지 바로 확인할 수 있어요)
    /// </summary>
    public static class ItemCategories
    {
        /// <summary>직접 지정하는 분류 이름 (자동 인식보다 우선). 예: { 2, "페인트" },</summary>
        public static readonly Dictionary<int, string> Names = new Dictionary<int, string>
        {
            { 1, "캐릭터" },
            { 3, "카트바디" },
            { 4, "번호판" },
            { 6, "라이센스" },
            { 7, "체인저" },
            { 10, "미니게임 보석" },
            { 12, "리플레이 녹화 카메라" },
            { 13, "리플레이 관람권" },
            { 14, "업그레이드 킷" },
            { 15, "보석" },
            { 16, "핸드봉" },
            { 18, "유니폼" },
            { 19, "캐릭터 보너스 카드" },
            { 22, "이니셜 입력 카드" },
            { 23, "카드" },
            { 24, "갓챠" },
            { 25, "시나리오" },
            { 27, "스키드" },
            { 28, "마이룸" },
            { 29, "루찌 카드" },
            { 30, "필살기" },
            { 31, "네임펜" },
            { 33, "출석도장" },
            { 34, "보석" },
            { 35, "연장권" },
            { 36, "보스 모드 카드" },
            { 37, "플로터" },
            { 38, "플로터 활성킷" },
            { 39, "제로 스패너" },
            { 40, "가위 바위 보 보석" },
            { 41, "확성기" },
            { 43, "플랜트 파츠(엔진패치)" },
            { 44, "플랜트 파츠(핸들)" },
            { 45, "플랜트 파츠(바퀴)" },
            { 46, "플랜트 파츠(킷)" },
            { 47, "에너지 9개 쿠폰" },
            { 48, "마이스터 키" },
            { 49, "초기화 보호 좌물쇠" },
            { 50, "교환권" },
            { 51, "루콘카드" },
            { 53, "더블 초기화 보호 좌물쇠" },
            { 55, "웹이벤트 관련" },
            { 56, "코인" },
            { 57, "코인 카드" },
            { 58, "스킨 카드" },
            { 59, "낚시대" },
            { 60, "낚시" },
            { 61, "타코미터 카드" },
            { 63, "X/V1 엔진" },
            { 64, "X/V1 핸들" },
            { 65, "X/V1 바퀴" },
            { 66, "X/V1 부스터" },
            { 67, "파츠 조각" },
            { 70, "염색약" },
        };

        /// <summary>
        /// 아이템 추가 창에 보여줄 분류 ID (화이트리스트). 여기에 적은 분류만 표시됩니다.
        /// 아무것도 적지 않고 비워 두면 모든 분류를 표시합니다.
        /// (처음 아이템 추가 창을 열 때 로그에 나오는 분류 목록에서 필요한 ID 를 골라 추가하면 됩니다)
        /// </summary>
        public static readonly HashSet<int> Whitelist = new HashSet<int>
        {
            1, 2, 3, 4, 7, 8, 9, 11, 12, 16, 18, 20, 21, 26, 27, 52, 70
        };

        /// <summary>화이트리스트에 있어도 숨길 분류 ID 목록 (보통은 비워 두면 됩니다). 예: 7, 15</summary>
        public static readonly HashSet<int> Hidden = new HashSet<int>
        {
        };

        /// <summary>분류 표시 순서. 여기에 적은 ID 가 먼저, 나머지는 ID 순서대로 표시됩니다.</summary>
        public static readonly int[] Order = { 3 };

        /// <summary>
        /// 자동 이름 붙이기용: HF 내장 Item.xml 의 태그 이름 → 한국어 분류 이름.
        /// (Item.xml 의 해당 태그 아이템 ID 와 가장 많이 겹치는 분류에 이 이름을 붙임. 틀리면 Names 로 덮어쓰면 됩니다.)
        /// </summary>
        public static readonly Dictionary<string, string> TagNames = new Dictionary<string, string>
        {
            { "Character", "캐릭터" }, { "Paint", "페인트" }, { "Plate", "번호판" }, { "Goggle", "고글" }, { "Balloon", "풍선" },
            { "HeadBand", "밴드" }, { "HeadPhone", "헤드폰" }, { "HandGearL", "장갑" }, { "Uniform", "유니폼" }, { "Decal", "데칼" },
            { "Pet", "펫" }, { "FlyingPet", "플라잉펫" }, { "Aura", "오라" }, { "SkidMark", "스키드마크" }, { "RidColor", "네임펜" },
            { "BonusCard", "보너스 카드" }, { "Tachometer", "계기판" }, { "Dye", "염색약" }, { "MyRoom", "마이룸" }, { "emblem", "엠블럼" },
            { "Card", "카드" }, { "InitialCard", "초기 카드" }, { "SlotItem", "슬롯 아이템" }, { "ReplayTicket", "리플레이 티켓" },
            { "V1EffectData", "V1 코팅" }, { "V1BoosterEffectData", "V1 램프" }
        };
    }
}
