using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>골드 강화 5종. 원본 `CONFIG.upgrades`의 키(`atk/hp/ms/as/crit`) — `as`는 C# 예약어라 `AtkSpeed`로 대체.</summary>
    public enum UpgradeStat { Atk, Hp, Ms, AtkSpeed, Crit }

    /// <summary>
    /// 골드 강화 비용표. 원본 `CONFIG.upgrades`(project_test.html:731) 그대로 —
    /// **효과는 선형(<see cref="PlayerStatCalculator"/>), 비용은 지수** — 이 비대칭이 스프린트 2가
    /// 검증하려는 핵심 곡선이다.
    /// </summary>
    public static class GoldUpgradeConfig
    {
        public static float CostBase(UpgradeStat stat) => stat switch
        {
            UpgradeStat.Atk => 22f,
            UpgradeStat.Hp => 20f,
            UpgradeStat.Ms => 18f,
            UpgradeStat.AtkSpeed => 18f,
            UpgradeStat.Crit => 25f,
            _ => 0f,
        };

        public static float CostMult(UpgradeStat stat) => stat switch
        {
            UpgradeStat.Atk => 1.14f,
            UpgradeStat.Hp => 1.14f,
            UpgradeStat.Ms => 1.22f,
            UpgradeStat.AtkSpeed => 1.22f,
            UpgradeStat.Crit => 1.28f,
            _ => 1f,
        };

        /// <summary>원본 `upCost(k) = floor(base × mult^lv)`, lv=구매 전 현재 단계(project_test.html:1268).</summary>
        public static int Cost(UpgradeStat stat, int currentLevel) =>
            Mathf.FloorToInt(CostBase(stat) * Mathf.Pow(CostMult(stat), currentLevel));
    }
}
