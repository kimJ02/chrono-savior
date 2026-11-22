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

        /// <summary>구매 단위 "최대"(원본 `upgradeBuyQty === 'max'`).</summary>
        public const int BuyMax = -1;
        /// <summary>원본 `buyGoldUpgrade`·`previewGoldUpgrade`가 "최대"일 때 한 번에 사는 상한(project_test.html:7045·1271).</summary>
        public const int MaxBuyBatch = 10000;

        /// <summary>구매 단위가 몇 번 사는 일인지 — 1·10은 그 수, <see cref="BuyMax"/>는 <see cref="MaxBuyBatch"/>.</summary>
        public static int BatchLimit(int qty) => qty == BuyMax ? MaxBuyBatch : Mathf.Max(1, qty);

        /// <summary>
        /// 원본 `previewGoldUpgrade(k, qty)`(project_test.html:1269) — 지금 단계에서 `qty`단계를 살 때 몇 단계(count)에 얼마(cost).
        /// 1·10은 돈이 모자라도 그 수만큼 셈하고(버튼을 못 누르게 하는 건 화면), "최대"는 가진 골드로 살 수 있는 만큼만 센다.
        /// </summary>
        public static (int count, long cost) Preview(UpgradeStat stat, int currentLevel, long gold, int qty)
        {
            int limit = BatchLimit(qty);
            int count = 0;
            long costSum = 0;
            int lv = currentLevel;
            while (count < limit)
            {
                int cost = Cost(stat, lv);
                if (cost <= 0) break; // 단계가 아주 높아 비용이 int를 넘으면 더 세지 않는다
                if (qty == BuyMax && costSum + cost > gold) break;
                costSum += cost; lv++; count++;
            }
            return (count, costSum);
        }
    }
}
