using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Systems
{
/// <summary>
/// **몹이 죽으면 무엇을 얼마나 주나** — 통계·이번 런 집계·지역 토벌·경험치·골드·연쇄 보너스·업적(2026-11-06 정리 순서 5-2, 예전엔 `EnemySpawner` 안에 있었다).
/// 원본 `killEnemy()`(project_test.html:1793)와 보스 분기 `onBossKilled`(:4282).
///
/// 역할 나눔: 처치 이벤트를 듣고 몹의 데이터를 넘기는 건 <see cref="EnemySpawner"/>(몹↔데이터를 기억한다), 경험치 구슬을 세우는 것도 스포너(생성이라).
/// 여기는 상태를 갖지 않는다. 종류별 기본 보상은 <see cref="EnemyData"/>, 지역·엘리트 배율은 <see cref="DifficultyScalingConfig"/>.
/// 난수 호출 순서는 옮기기 전과 같다(골드 드랍 확률 → 골드 양).
/// </summary>
public static class EnemyRewards
{
    /// <summary>
    /// 일반 몹 처치 보상 — EXP는 항상, 골드는 확률 드랍(엘리트는 항상). 성소·보스는 이 경로를 타지 않는다(원본도 각각 따로 처리한다 —
    /// 성소 :1797에서 `return`, 보스는 <see cref="GrantBossKill"/>).
    /// </summary>
    /// <param name="data">그 몹의 종류 데이터 — 없으면 기본 보상(EXP 8, 골드 5~10).</param>
    public static void GrantKill(EnemyHealth enemy, EnemyData data)
    {
        bool elite = enemy.GetComponent<EnemyElite>() != null;

        ProfileService.Current.RecordEnemyKill(elite);

        // 결과 화면·HUD가 읽는 이번 런 집계. 원본도 `run.kills`와 누적 통계를 따로 센다(:1857).
        RunState.RegisterKill(isBoss: false);
        // 지역 토벌 진행도 — 100마리를 채우면 그 지역 보스가 열린다(원본 :1866).
        ProfileService.Current.RegisterRegionKill(RunState.Region);

        int enemyLevel = enemy.Level;
        float sc = DifficultyScalingConfig.RewardMultiplier(RunState.Region);
        // 원본 `eliteMult = e.elite ? CONFIG.elite.rewardMult : 1`(project_test.html:1810).
        if (elite) sc *= DifficultyScalingConfig.EliteRewardMult;

        float baseExp = data != null ? data.exp : 8f;
        int goldMin = data != null ? data.goldMin : 5;
        int goldMax = data != null ? data.goldMax : 10;

        // 수행의 굴레 — 경험치 획득 배수(원본 `expMultAll()` :1305).
        int expGained = Mathf.RoundToInt(baseExp * sc * CombatModifiers.ExpMultiplier);
        ProfileService.Current.AddExp(expGained);
        int goldGained = 0;

        // 원본 `if (Math.random() < goldDropChance || e.boss || e.elite)`(`:1818`) — 엘리트는 확률 무시하고 항상 드랍.
        if (elite || Random.value < DifficultyScalingConfig.GoldDropChance)
        {
            int rolled = Random.Range(goldMin, goldMax + 1); // Random.Range(int)는 상한이 배타적이라 +1
            // 원본 `goldMultAll()`(:1304) — 콤보가 재화도 늘린다.
            goldGained = Mathf.RoundToInt(rolled * sc * CombatModifiers.GoldMultiplier);
            ProfileService.Current.AddGold(goldGained);
        }

        // 연쇄 처치 — 0.8초 안에 3마리째부터 골드 보너스(원본 `killEnemy`의 연쇄 분기, :1828).
        int chainBonus = CombatModifiers.RegisterKillForChain(enemyLevel, isBoss: false);
        if (chainBonus > 0)
        {
            ProfileService.Current.AddGold(chainBonus);
            goldGained += chainBonus;
            // 원본 연쇄 분기(:1835~:1836) — 멈춤 0.10, 엘리트면 연쇄 수만큼 세게 흔든다.
            CombatEvents.RequestHitstop(HitFeel.HitstopChain);
            if (elite) HitFeel.Shake(Mathf.Min(14f, 5f + CombatModifiers.ChainKills), 0.25f);
        }

        // 원본 `run.goldEarned += g; run.expEarned += exp`(:1816·:1820) — 결과 화면에 쓴다.
        RunState.RegisterReward(goldGained, expGained);

        Achievements.CheckNew(ProfileService.Current);
    }

    /// <summary>
    /// 보스 처치 보상 — 원본 `onBossKilled`(project_test.html:4282) + `killEnemy`의 보스 보상 분기.
    /// **처치 수엔 안 들어가지만 살기에는 들어간다**(`run.fury++`는 보스 포함, `run.kills++`는 제외). 정복하면 다음 지역이 열린다.
    /// </summary>
    public static void GrantBossKill(int region)
    {
        float sc = DifficultyScalingConfig.RewardMultiplier(region);

        int exp = Mathf.RoundToInt(Boss.RewardExp * sc);
        int gold = Mathf.RoundToInt(Boss.RewardGold * sc); // 보스는 확률 무시하고 항상 드랍(:1818)
        ProfileService.Current.AddExp(exp);
        ProfileService.Current.AddGold(gold);
        RunState.RegisterReward(gold, exp);
        RunState.RegisterKill(isBoss: true);

        ProfileService.Current.RecordBossKill(region); // 통계 + 정복 — 다음 지역이 열린다
        Achievements.CheckNew(ProfileService.Current);
    }
}
}
