using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 시간 회귀(輪廻) — 원본 `doRebirth()`(project_test.html:6509)와 회귀 장벽(`:741`·`:973`~`:977`).
    ///
    /// ## 왜 필요한가
    /// 원본은 지역 입장료가 지역당 ×2.2로 지수 증가하고(`entryFeeOf`), 몹 체력도 ×2.15로 오른다
    /// (우리 게임은 입장료를 없앴다 — 2026-10-25 사용자, `RegionConfig.EntryFee`. 대신 멸망까지 남은 기간이 회귀를 민다).
    /// **한 생(生)의 골드 강화만으로는 반드시 벽에 부딪히게 설계돼 있고**, 그 벽을 넘는 유일한
    /// 수단이 시간 회귀다. 그래서 이게 없으면 지역 진행이 중간에서 그냥 멈춘다.
    ///
    /// ## 회귀 장벽 (소프트 벽)
    /// 지역마다 "권장 회귀 횟수"가 있고, 그보다 **모자란 만큼** 지수로 불리해진다(`:975`~`:977`):
    /// 몹 체력 ×2.8, 몹 피해 ×1.45, **내 피해 ×0.62**. 세 개가 동시에 걸려서 두 번쯤 모자라면
    /// 사실상 통과가 불가능해진다 — "더 강해져서 오라"가 아니라 "시간 회귀하고 오라"는 신호다.
    /// </summary>
    public static class RegressionConfig
    {
        // 원본 CONFIG.rebirthWall(project_test.html:741)
        public const float WallHpMult = 2.8f;
        public const float WallDmgMult = 1.45f;
        public const float WallPlayerDmgMult = 0.62f;

        /// <summary>
        /// 지역 하나를 정복했을 때 주는 시간의 파편 — 원본 `rpOfRegion(r) = max(1, floor(r*r*0.6))`
        /// (`:6502`). 뒤로 갈수록 가파르다: 1·2·5·9·15·21·29·38·48.
        /// </summary>
        public static int ShardsOfRegion(int region) => Mathf.Max(1, Mathf.FloorToInt(region * region * 0.6f));

        /// <summary>
        /// 그 지역의 권장 회귀 횟수 — 원본 `regionReqRebirth(r) = max(0, ceil((r-2)/2))`(`:973`).
        /// 1·2지역은 0회, 3·4지역 1회, 5·6지역 2회, 7·8지역 3회, 9지역 4회.
        /// </summary>
        public static int RequiredRegressions(int region) =>
            Mathf.Max(0, Mathf.CeilToInt((region - 2) / 2f));

        /// <summary>권장보다 몇 번 모자란가. 0이면 장벽이 없다.</summary>
        public static int Gap(int region, int regressions) =>
            Mathf.Max(0, RequiredRegressions(region) - regressions);

        public static float WallEnemyHp(int region, int regressions) => Mathf.Pow(WallHpMult, Gap(region, regressions));
        public static float WallEnemyDamage(int region, int regressions) => Mathf.Pow(WallDmgMult, Gap(region, regressions));

        /// <summary>**내 피해가 줄어든다**(1보다 작다). 원본 `rebirthWallPlayerDmgMult`(`:977`).</summary>
        public static float WallPlayerDamage(int region, int regressions) => Mathf.Pow(WallPlayerDmgMult, Gap(region, regressions));
    }
}
