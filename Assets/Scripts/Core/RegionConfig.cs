using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 지역(스테이지) 고정 수치 — 원본 `REGION_NAMES`(project_test.html:969),
    /// `regionBaseLv`(`:972`), `CONFIG.entryFee`(`:739`), `entryFeeOf`(`:6438`) 그대로.
    ///
    /// 진행 상태(어디까지 깼는지)는 여기가 아니라 <see cref="PlayerProfile"/>에 있다 —
    /// 이건 플레이어마다 다르지 않은 **설정값**이고 그건 **세이브 데이터**다
    /// (CLAUDE.md "세이브 데이터 vs 설정 데이터").
    /// </summary>
    public static class RegionConfig
    {
        public const int Count = 9;

        public static readonly string[] Names =
        {
            "푸른 초원", "버려진 마을", "안개 골짜기", "고블린 소굴", "폐허 성곽",
            "설산 고개", "화염 동굴", "망자의 묘지", "마왕성",
        };

        // 원본 CONFIG.entryFee(:739) — 지역당 지수 증가라 결국 감당이 안 되고, 그게 시간 회귀를 강제한다.
        const float EntryFeeBase = 300f;
        const float EntryFeeGrow = 2.2f;

        public static string NameOf(int region) =>
            region >= 1 && region <= Count ? Names[region - 1] : "?";

        /// <summary>
        /// 지역 보스 이름 — 전투 화면 보스 체력바 위에 뜬다(2026-11-10). 1지역은 팀원 기획 보스 **덩어리 지배자**.
        /// 나머지는 아직 기획된 보스가 없어(원본 공통 보스, 원본 이름은 지역 이름이 바뀌어 안 맞음) 비워 두고 <see cref="BossNameOf"/>가 "보스"로 대신한다.
        /// </summary>
        static readonly string[] BossNames = { "덩어리 지배자", null, null, null, null, null, null, null, null };

        public static string BossNameOf(int region) =>
            region >= 1 && region <= Count && !string.IsNullOrEmpty(BossNames[region - 1]) ? BossNames[region - 1] : "보스";

        /// <summary>
        /// 스테이지 입장료 — **받지 않는다**(2026-10-25 사용자 "입장료 없애고"). 원본은 지역마다 지수로 올라 결국 시간 회귀를 강제했다
        /// (<see cref="OriginalEntryFee"/>) — 의도적 편차, docs/original-parity.md. 이제 회귀를 미는 건 몹 체력 상승과 멸망까지 남은 기간이다.
        /// </summary>
        public static int EntryFee(int region) => 0;

        /// <summary>원본 `entryFeeOf(r)`(:6438) — 1지역은 무료. 원본 대조용으로만 남겨 둔다(게임은 <see cref="EntryFee"/>를 쓴다).</summary>
        public static int OriginalEntryFee(int region) =>
            region <= 1 ? 0 : Mathf.FloorToInt(EntryFeeBase * Mathf.Pow(EntryFeeGrow, region - 2));

        /// <summary>몹 표시 레벨 = 권장 레벨. 원본 `regionBaseLv(r) = (r-1)*3 + 1`(:972).</summary>
        public static int RecommendedLevel(int region) => (region - 1) * 3 + 1;

        /// <summary>
        /// 지역별 하늘 색 3단(위 → 중간 → 아래). 원본 `regionSkyTint(r)`(project_test.html:4483 부근)
        /// 그대로다 — 원본은 이 셋으로 세로 그라데이션을 그린다.
        ///
        /// **왜 필요했나**: 씬 빌더가 카메라 배경을 `Color.white`로 두고 있었다(초기 스캐폴딩의
        /// 자리표시자). 원본은 캔버스에 어두운 밤 하늘을 먼저 깔기 때문에, 흰 배경 위에서는
        /// ① 발판·몹이 눈에 찌르고 ② 로비 판이 `rgba(14,11,20,.92)`라 **8% 통과하는 흰색**이
        /// UI 전체를 뿌옇게 만들었다(사용자가 "깨져 보인다"고 지적한 증상).
        /// </summary>
        public static readonly Color[,] SkyTints = BuildSkyTints();

        static Color[,] BuildSkyTints()
        {
            // 원본 `tints` 배열의 헥사값 그대로.
            string[,] hex =
            {
                { "0b0b1e", "191430", "2a1a2e" }, { "0b101e", "141c30", "1a2a2e" }, { "10101c", "1e1e2e", "2e2a3a" },
                { "1e0b14", "301424", "3e1a2a" }, { "12121a", "222230", "32303e" }, { "0b141e", "142230", "1a303e" },
                { "1e0e0b", "301a14", "3e241a" }, { "140b1e", "241430", "301a3e" }, { "050508", "100c18", "1a1424" },
            };

            var result = new Color[Count, 3];
            for (int r = 0; r < Count; r++)
                for (int i = 0; i < 3; i++)
                    result[r, i] = Hex(hex[r, i]);
            return result;
        }

        /// <summary>
        /// 그 지역 하늘의 **중간 색**. 카메라는 단색으로만 지울 수 있어서, 3단 그라데이션 중
        /// 가운데를 대표값으로 쓴다(그라데이션·별·달은 원본 렌더링 쪽이라 아직 이식 범위 밖).
        /// </summary>
        public static Color SkyColor(int region)
        {
            int r = Mathf.Clamp(region - 1, 0, Count - 1);
            return SkyTints[r, 1];
        }

        static Color Hex(string s) => new Color(
            int.Parse(s.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(s.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f,
            int.Parse(s.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f);
    }
}
