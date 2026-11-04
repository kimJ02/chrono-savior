using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 난이도 스케일링("가상 지역 레벨") + 처치 보상 수치를 전부 여기 모았다(`docs/sprints/03-growth-curve-worksplit.md`
    /// 트랙 A "0. 착수 전 필독", 2026-09-08 추가). 나중에 밸런스를 조정할 때 이 파일 상수만 바꾸면
    /// 되고 `EnemySpawner`/`RunProgress` 로직 코드는 안 건드려도 된다 — 원본(`project_test.html`)
    /// 밸런스 자체가 완전히 정리된 게 아니라서(예: `CONFIG.souls`/`CONFIG.scale.soulGrow`처럼 아무 데서도
    /// 안 읽는 죽은 설정값이 실제로 있었다, `:701`,`:727`) 우리도 나중에 직접 세밀하게 조정할 가능성이
    /// 높다는 판단에서 나온 구조다.
    ///
    /// 지금 값은 전부 원본 그대로(project_test.html:699,:709,:712,:727) — 임의로 바꾸지 말 것,
    /// 조정은 나중에 실제 플레이해보고 사용자가 결정한다.
    /// </summary>
    public static class DifficultyScalingConfig
    {
        public const int KillsPerRegionLevel = 100; // 원본 regionKillTarget(:699)

        public const float HpPerRegion = 2.15f;     // CONFIG.scale.hpPerRegion(:727)
        public const float DmgPerRegion = 1.4f;     // CONFIG.scale.dmgPerRegion(:727)
        public const float RewardGrow = 1.42f;      // CONFIG.scale.rewardGrow(:727)

        public const float GoldDropChance = 0.75f;  // CONFIG.goldDropChance(:744)

        // ⚠️ 몹별 기본 스탯(체력·피해·경험치·골드)은 더 이상 여기 없다 — `Core/EnemyData` SO가 소유한다.
        // 몹이 7종이 되면서 "고블린 상수"를 여기 두면 종류마다 상수를 늘려야 하고, CLAUDE.md
        // "데이터(밸런스 값)" 규칙(스탯은 SO)에도 어긋난다. 이 파일은 **지역 배율만** 맡는다.
        // 원본도 같은 구조다: `CONFIG.enemyBase[type]`(종류별 스탯) × `CONFIG.scale`(지역 배율), `:3958`.

        // 엘리트(원본 CONFIG.elite, project_test.html:696). 종류와 무관하게 아무 몹이나 확률로 승격된다.
        // 꼬마 슬라임(splitlet)만 예외 — 원본이 `noElite: true`로 낳는다(`:1842`).
        public const float EliteChance = 0.08f;
        public const float EliteHpMult = 4f;
        public const float EliteDmgMult = 1.5f;
        public const float EliteRewardMult = 5f;
        public const float EliteScale = 1.35f;

        public static float ScaledHp(float baseHp, int regionLv) => baseHp * Mathf.Pow(HpPerRegion, regionLv - 1);
        public static float ScaledDmg(float baseDmg, int regionLv) => baseDmg * Mathf.Pow(DmgPerRegion, regionLv - 1);
        public static float RewardMultiplier(int regionLv) => Mathf.Pow(RewardGrow, regionLv - 1);
    }
}
