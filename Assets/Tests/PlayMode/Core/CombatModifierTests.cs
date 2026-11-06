using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 전투 배수 체인 — 원본 `dmgMultAll()`(project_test.html:1296) · `goldMultAll()`(`:1304`) ·
/// `levelFactor()`(`:1651`) · 연쇄 처치(`:1828`).
///
/// 전부 순수 계산이라 프레임을 흘릴 필요가 없다. **정적 상태라 매 테스트 리셋이 필수** —
/// 안 하면 앞 테스트의 콤보가 뒤 테스트의 배수를 부풀린다.
/// </summary>
public class CombatModifierTests
{
    [SetUp]
    public void Setup() { CombatModifiers.Reset(); RunState.Reset(); }

    [TearDown]
    public void Teardown() { CombatModifiers.Reset(); RunState.Reset(); }

    // ────────────────────────── 콤보 ──────────────────────────

    /// <summary>원본 `1 + combo.n * CONFIG.comboMult.dmg`(:1297) — 콤보당 피해 +1.2%.</summary>
    [Test]
    public void Combo_IncreasesDamageAndGoldMultipliers()
    {
        Assert.AreEqual(1f, CombatModifiers.DamageMultiplier, 1e-4f, "콤보 0에서는 배수가 1이다");

        for (int i = 0; i < 10; i++) CombatModifiers.AddCombo();

        Assert.AreEqual(10, CombatModifiers.Combo);
        Assert.AreEqual(1f + 10 * CombatModifiers.ComboDamagePer, CombatModifiers.DamageMultiplier, 1e-4f);
        Assert.AreEqual(1f + 10 * CombatModifiers.ComboMoneyPer, CombatModifiers.GoldMultiplier, 1e-4f);
    }

    /// <summary>
    /// 원본 `updateCombo`(:1630) — 3초 안에 다시 때리지 않으면 콤보가 통째로 0이 된다(감쇠가 아니라 리셋).
    /// 때릴 때마다 창이 **갱신**되는 것도 같이 본다(`combo.t = window`).
    /// </summary>
    [Test]
    public void Combo_ResetsAfterWindow_AndRefreshesOnHit()
    {
        CombatModifiers.AddCombo();
        CombatModifiers.Tick(CombatModifiers.ComboWindow - 0.1f);
        Assert.AreEqual(1, CombatModifiers.Combo, "창이 아직 안 끝났는데 콤보가 끊겼다");

        CombatModifiers.AddCombo(); // 창 갱신
        CombatModifiers.Tick(CombatModifiers.ComboWindow - 0.1f);
        Assert.AreEqual(2, CombatModifiers.Combo, "때렸으면 창이 다시 채워져야 한다");

        CombatModifiers.Tick(0.2f);
        Assert.AreEqual(0, CombatModifiers.Combo, "창이 지나면 0으로 리셋된다(감쇠 아님)");
    }

    /// <summary>이번 런 최고 콤보는 콤보가 끊겨도 남는다(원본 `meta.stats.maxCombo`, :1623).</summary>
    [Test]
    public void MaxCombo_SurvivesComboReset()
    {
        for (int i = 0; i < 5; i++) CombatModifiers.AddCombo();
        CombatModifiers.Tick(CombatModifiers.ComboWindow + 0.1f);

        Assert.AreEqual(0, CombatModifiers.Combo);
        Assert.AreEqual(5, RunState.MaxCombo);
    }

    /// <summary>
    /// 최고 콤보는 **이번 원정** 것이다 — 새 원정을 시작하면 0부터(결과 화면 '원정 통계', 2026-11-06 — 예전엔 세션 전체 최고가 보였다).
    /// 업적용 평생 최고는 프로필 `stats.maxCombo`에 남는다(`AchievementTests.MaxCombo_IsRecordedOnProfile`).
    /// </summary>
    [Test]
    public void MaxCombo_IsPerExpedition_ButProfileKeepsTheLifetimeBest()
    {
        ProfileService.Reset(); // 이 테스트는 프로필 통계도 본다(다른 테스트가 쌓은 값 없이)
        for (int i = 0; i < 7; i++) CombatModifiers.AddCombo();
        Assert.AreEqual(7, RunState.MaxCombo);

        RunState.Begin(1, RunMode.Normal); // 새 원정 — 실제 순서대로 런 기록 다음 전투 배수(`GameFlow.StartRun` → `CombatDirector.BeginField`)
        CombatModifiers.ResetForRun();
        Assert.AreEqual(0, RunState.MaxCombo, "새 원정인데 지난 원정 최고 콤보가 남았다");
        for (int i = 0; i < 3; i++) CombatModifiers.AddCombo();
        Assert.AreEqual(3, RunState.MaxCombo, "결과 화면엔 이번 원정 최고(3)가 보여야 한다");
        Assert.AreEqual(7, ProfileService.Current.stats.maxCombo, "평생 최고는 프로필에 그대로");
    }

    // ────────────────────────── 살기 ──────────────────────────

    /// <summary>
    /// 원본 `1 + run.fury * CONFIG.fury.dmgPer`(:1298) — **콤보와 달리 런 내내 안 사라진다**(눈덩이).
    /// 살기 수치는 `RunState.Fury`가 소유하므로 여기선 그걸 읽어 쓰는지만 본다.
    /// </summary>
    [Test]
    public void Fury_SnowballsAcrossTheRun()
    {
        RunState.Begin(1, RunMode.Normal);
        for (int i = 0; i < 50; i++) RunState.RegisterKill(isBoss: false);

        Assert.AreEqual(50, RunState.Fury);
        Assert.AreEqual(1f + 50 * CombatModifiers.FuryDamagePer, CombatModifiers.DamageMultiplier, 1e-4f);
        Assert.AreEqual(1f + 50 * CombatModifiers.FuryAttackSpeedPer, CombatModifiers.AttackSpeedMultiplier, 1e-4f);
    }

    /// <summary>새 런을 시작하면 살기도 콤보도 0에서 다시 시작한다(원본 `startRun` :4307).</summary>
    [Test]
    public void NewRun_ClearsComboAndFury()
    {
        RunState.Begin(1, RunMode.Normal);
        for (int i = 0; i < 20; i++) { RunState.RegisterKill(isBoss: false); CombatModifiers.AddCombo(); }

        RunState.Begin(2, RunMode.Normal);
        CombatModifiers.ResetForRun();

        Assert.AreEqual(1f, CombatModifiers.DamageMultiplier, 1e-4f);
    }

    // ────────────────────────── 레벨 페널티 ──────────────────────────

    /// <summary>
    /// 원본 `levelFactor(mobLv)`(:1651). **내 레벨이 높아도 이득은 없다**(상한 1) —
    /// 낮을 때만 레벨당 5%씩 깎이고 25%가 하한이다. 이게 상위 지역을 계속 도전으로 만드는 장치다.
    /// </summary>
    [Test]
    public void LevelFactor_PenalizesOnlyWhenUnderleveled()
    {
        Assert.AreEqual(1f, CombatModifiers.LevelFactor(myLevel: 10, mobLevel: 5), 1e-4f,
            "내 레벨이 높다고 추가 이득이 있으면 안 된다");
        Assert.AreEqual(1f, CombatModifiers.LevelFactor(myLevel: 5, mobLevel: 5), 1e-4f);

        Assert.AreEqual(1f - 3 * CombatModifiers.LevelPenaltyPer,
            CombatModifiers.LevelFactor(myLevel: 4, mobLevel: 7), 1e-4f);
    }

    /// <summary>아무리 차이가 나도 25% 아래로는 안 내려간다(원본 `CONFIG.levelPen.floor`).</summary>
    [Test]
    public void LevelFactor_HasFloor()
    {
        Assert.AreEqual(CombatModifiers.LevelPenaltyFloor,
            CombatModifiers.LevelFactor(myLevel: 1, mobLevel: 40), 1e-4f);
    }

    /// <summary>보스만 하한이 0.5다(원본 `e.boss ? Math.max(0.5, levelFactor(e.lv))`, :1662).</summary>
    [Test]
    public void LevelFactor_BossHasHigherFloor()
    {
        Assert.AreEqual(CombatModifiers.BossLevelPenaltyFloor,
            CombatModifiers.LevelFactor(myLevel: 1, mobLevel: 40, isBoss: true), 1e-4f);
    }

    // ────────────────────────── 연쇄 처치 ──────────────────────────

    /// <summary>
    /// 원본 `killEnemy`의 연쇄 분기(:1828) — 0.8초 안에 **3마리째부터** 골드 보너스가 붙는다.
    /// 공식은 `chainN × (4 + lv × 2) × goldMultAll()`.
    /// </summary>
    [Test]
    public void ChainKill_PaysBonusFromThirdKill()
    {
        Assert.AreEqual(0, CombatModifiers.RegisterKillForChain(enemyLevel: 3, isBoss: false), "1번째는 보너스 없음");
        Assert.AreEqual(0, CombatModifiers.RegisterKillForChain(enemyLevel: 3, isBoss: false), "2번째도 없음");

        int bonus = CombatModifiers.RegisterKillForChain(enemyLevel: 3, isBoss: false);
        Assert.AreEqual((int)JsMath.Round(3 * (4f + 3 * 2f) * CombatModifiers.GoldMultiplier), bonus);
    }

    /// <summary>0.8초가 지나면 연쇄가 끊기고 다시 1부터 센다.</summary>
    [Test]
    public void ChainKill_ResetsAfterWindow()
    {
        CombatModifiers.RegisterKillForChain(1, false);
        CombatModifiers.RegisterKillForChain(1, false);
        Assert.AreEqual(2, CombatModifiers.ChainKills);

        CombatModifiers.Tick(CombatModifiers.ChainKillWindow + 0.01f);
        Assert.AreEqual(0, CombatModifiers.ChainKills);

        CombatModifiers.RegisterKillForChain(1, false);
        Assert.AreEqual(1, CombatModifiers.ChainKills, "끊긴 뒤엔 1부터 다시 센다");
    }

    /// <summary>보스는 연쇄 보너스를 안 준다(원본 `&& !e.boss`).</summary>
    [Test]
    public void ChainKill_ExcludesBoss()
    {
        CombatModifiers.RegisterKillForChain(1, false);
        CombatModifiers.RegisterKillForChain(1, false);
        Assert.AreEqual(0, CombatModifiers.RegisterKillForChain(enemyLevel: 1, isBoss: true));
    }

    // ────────────────────────── 성소 버프 ──────────────────────────

    /// <summary>
    /// 원본 `player.shrineBuffT > 0 ? CONFIG.shrine.pDmg : 1`(:1300). 성소 자체는 아직 없지만
    /// 수신 쪽을 미리 붙여둬서, 성소를 만들면 `CombatEvents.ShrineBuffGranted`만 쏘면 되게 했다.
    /// </summary>
    [Test]
    public void ShrineBuff_BoostsDamageThenExpires()
    {
        CombatModifiers.GrantShrineBuff(1f);
        Assert.AreEqual(CombatModifiers.ShrineDamageMult, CombatModifiers.DamageMultiplier, 1e-4f);
        Assert.AreEqual(CombatModifiers.ShrineMoveSpeedMult, CombatModifiers.MoveSpeedMultiplier, 1e-4f);

        CombatModifiers.Tick(1.1f);
        Assert.AreEqual(1f, CombatModifiers.DamageMultiplier, 1e-4f, "지속시간이 끝났는데 버프가 남았다");
    }
}

}
