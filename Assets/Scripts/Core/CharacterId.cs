namespace ChronoSavior.Core
{
    /// <summary>
    /// 플레이어 캐릭터 4종. 원본 `CHARACTERS`(project_test.html:848)와 `WEAPONS`(`:841`)는 1:1로
    /// 대응해서(마법사→bow, 메카닉→gunner, 섬영→blade, 드루이드→druid) 하나로 합쳤다.
    ///
    /// **각 캐릭터는 스탯만 다른 게 아니라 조작 체계 자체가 다르다** — 섬영만 관성 가속 이동이고,
    /// Z 기본공격도 넷 다 완전히 다른 함수다(원본 `updatePlayer`가 무기별로 분기, `:3509`~`:3538`).
    /// 그래서 캐릭터마다 "키트" 컴포넌트를 따로 두고 `Characters.PlayerRig`가 하나만 켠다.
    /// </summary>
    public enum CharacterId { Mage, Gunner, Blade, Druid }

    /// <summary>캐릭터 고유의 상수. 지금은 스탯 배수 하나뿐이라 여기 모아둔다.</summary>
    public static class CharacterStats
    {
        /// <summary>
        /// 원본 `CHAR_STAT_MULT`(project_test.html:1282) — 섬영만 1.5, 나머지는 1.
        /// 원본 주석 그대로: "상수에 더하지 않고 완성된 스탯 전체에 곱해서 강화/아이템 투자량과
        /// 무관하게 원거리 대비 비율이 항상 유지되게 한다".
        ///
        /// ⚠️ **`statAtk`·`statMaxHp`에만 곱한다**(`:1284`,`:1285`). 이동속도·공격속도·치명타
        /// (`:1286`~`:1288`)엔 곱하지 않는다 — 원본을 그대로 옮긴 것이니 "일관성 있게" 바꾸지 말 것.
        /// </summary>
        public static float StatMultiplier(CharacterId id) => id == CharacterId.Blade ? 1.5f : 1f;
    }
}
