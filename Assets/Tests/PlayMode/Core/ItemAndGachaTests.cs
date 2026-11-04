using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 아이템 35종과 가챠 — 원본 `ITEMS`(project_test.html:758) · `rollItem`/`doGacha`(`:6541`~`:6588`).
///
/// 아이템은 **회귀해도 사라지지 않는 유일한 성장 축**이라, 집계 방식(합/곱)과 상한·천장이
/// 어긋나면 게임의 장기 곡선이 통째로 달라진다.
/// </summary>
public class ItemAndGachaTests
{
    [SetUp]
    public void Setup() { ProfileService.Reset(); RunState.Reset(); CombatModifiers.Reset(); }

    [TearDown]
    public void Teardown() { ProfileService.Reset(); RunState.Reset(); CombatModifiers.Reset(); }

    // ────────────────────────── 정의 ──────────────────────────

    /// <summary>원본 `ITEMS`는 35종이다. 개수가 줄면 가챠 확률 분포가 조용히 달라진다.</summary>
    [Test]
    public void ItemDatabase_HasAll35Items()
    {
        Assert.AreEqual(35, ItemDatabase.All.Length);

        var ids = new HashSet<string>();
        foreach (var d in ItemDatabase.All)
            Assert.IsTrue(ids.Add(d.id), $"아이디가 중복됐다: {d.id}");
    }

    /// <summary>
    /// 원본 `ITEM_GRADES`(:749) — **등급이 높을수록 개당 효과가 크지만 중복 상한이 낮다.**
    /// 원본 주석 그대로 "저등급도 끝까지 모으면 쓸모가 있고, 고등급은 '빨리 도달하는' 가치를 갖는다".
    /// </summary>
    [Test]
    public void Grades_HaveDecreasingCapAndWeight()
    {
        Assert.AreEqual(10, ItemDatabase.Cap(ItemGrade.Common));
        Assert.AreEqual(6, ItemDatabase.Cap(ItemGrade.Rare));
        Assert.AreEqual(4, ItemDatabase.Cap(ItemGrade.Epic));
        Assert.AreEqual(3, ItemDatabase.Cap(ItemGrade.Legend));

        Assert.AreEqual(55, ItemDatabase.Weight(ItemGrade.Common));
        Assert.AreEqual(30, ItemDatabase.Weight(ItemGrade.Rare));
        Assert.AreEqual(12, ItemDatabase.Weight(ItemGrade.Epic));
        Assert.AreEqual(3, ItemDatabase.Weight(ItemGrade.Legend));
    }

    // ────────────────────────── 집계 ──────────────────────────

    /// <summary>
    /// **합연산과 곱연산이 갈린다**(원본 `itemAdd` :1189 / `itemMul` :1199).
    /// 곱연산 아이템을 합연산으로 처리하면 후반 배수가 완전히 달라진다.
    /// </summary>
    [Test]
    public void Inventory_SeparatesAdditiveAndMultiplicative()
    {
        var inv = new ItemInventory();
        for (int i = 0; i < 3; i++) inv.Add("atk");   // 합연산: +6씩
        for (int i = 0; i < 2; i++) inv.Add("gold");  // 곱연산: ×1.15씩

        Assert.AreEqual(18f, inv.Add(ItemStat.Atk), 0.001f);
        Assert.AreEqual(1f, inv.Mul(ItemStat.Atk), 0.001f, "합연산 아이템이 곱연산에 섞였다");

        Assert.AreEqual(Mathf.Pow(1.15f, 2), inv.Mul(ItemStat.Gold), 0.001f);
        Assert.AreEqual(0f, inv.Add(ItemStat.Gold), 0.001f, "곱연산 아이템이 합연산에 섞였다");
    }

    /// <summary>중복 상한을 넘겨서 쌓이지 않는다(원본 `itemCap`).</summary>
    [Test]
    public void Inventory_RespectsCap()
    {
        var inv = new ItemInventory();
        for (int i = 0; i < 50; i++) inv.Add("deathBlast"); // 전설 = 상한 3

        Assert.AreEqual(3, inv.Count("deathBlast"));
        Assert.IsTrue(inv.IsFull("deathBlast"));
    }

    /// <summary>원본 `itemPow(id) = count * per`(`:1187`) — 특수 아이템의 총 세기.</summary>
    [Test]
    public void Inventory_PowScalesWithCount()
    {
        var inv = new ItemInventory();
        Assert.AreEqual(0f, inv.Pow("lifesteal"), 0.001f);

        inv.Add("lifesteal");
        inv.Add("lifesteal");
        Assert.AreEqual(0.08f, inv.Pow("lifesteal"), 0.001f); // per 0.04 × 2
    }

    // ────────────────────────── 실제 스탯 반영 ──────────────────────────

    /// <summary>
    /// 아이템이 **실제 스탯에 도달하는지** — 정의만 있고 안 곱하면 아무 의미가 없다.
    /// (전에 살기 공속이 계산만 되고 안 곱해지던 것과 같은 종류의 사고를 막는다.)
    /// </summary>
    [Test]
    public void StatItems_ReachComputedStats()
    {
        var p = ProfileService.Current;
        float atk0 = PlayerStatCalculator.ComputeAtk(p);
        float hp0 = PlayerStatCalculator.ComputeMaxHp(p);
        float crit0 = PlayerStatCalculator.ComputeCritChance(p);

        p.items.Add("atk");
        p.items.Add("hp");
        p.items.Add("crit");

        Assert.Greater(PlayerStatCalculator.ComputeAtk(p), atk0, "공격력 아이템이 스탯에 안 걸린다");
        Assert.Greater(PlayerStatCalculator.ComputeMaxHp(p), hp0, "체력 아이템이 스탯에 안 걸린다");
        Assert.Greater(PlayerStatCalculator.ComputeCritChance(p), crit0, "치명타 아이템이 스탯에 안 걸린다");
    }

    /// <summary>'전생의 투지'(곱연산)가 피해 배수 체인에 들어간다.</summary>
    [Test]
    public void DamageItem_ReachesDamageMultiplier()
    {
        RunState.Begin(1, RunMode.Normal);
        float before = CombatModifiers.DamageMultiplier;

        ProfileService.Current.items.Add("dmg");
        Assert.AreEqual(before * 1.12f, CombatModifiers.DamageMultiplier, 0.001f);
    }

    /// <summary>받는 피해 감소·쿨감은 **하한이 있다**(원본 `max(0.2, ...)` / `max(0.25, ...)`).</summary>
    [Test]
    public void DamageReductionAndCooldown_HaveFloors()
    {
        var p = ProfileService.Current;
        for (int i = 0; i < 4; i++) p.items.Add("dr"); // 영웅 상한 4 → 0.05×4 = 0.20
        for (int i = 0; i < 4; i++) p.items.Add("cd");

        Assert.GreaterOrEqual(PlayerStatCalculator.ComputeDamageTakenMultiplier(p), 0.2f);
        Assert.GreaterOrEqual(PlayerStatCalculator.ComputeCooldownMultiplier(p), 0.25f);
    }

    // ────────────────────────── 가챠 ──────────────────────────

    /// <summary>포인트가 모자라면 뽑히지 않는다(원본 `if (meta.shards < GACHA_COST) break`).</summary>
    [Test]
    public void Gacha_RequiresPoints()
    {
        var p = ProfileService.Current;
        p.shards = GachaService.Cost - 1;

        Assert.AreEqual(0, GachaService.Pull(p, 1).Count);
        Assert.AreEqual(GachaService.Cost - 1, p.shards, "실패했는데 포인트가 깎였다");
    }

    /// <summary>뽑으면 포인트가 그만큼 줄고 아이템이 늘어난다.</summary>
    [Test]
    public void Gacha_SpendsPointsAndGrantsItems()
    {
        var p = ProfileService.Current;
        p.shards = GachaService.Cost * 5;

        var got = GachaService.Pull(p, 5);

        Assert.AreEqual(5, got.Count);
        Assert.AreEqual(0, p.shards);
        int total = 0;
        foreach (var s in p.items.stacks) total += s.count;
        Assert.AreEqual(5, total);
    }

    /// <summary>
    /// 상한에 찬 아이템은 후보에서 빠진다(원본 `:6544` — "다 찬 뒤에도 계속 나와서 허탕 치는 일을 막는다").
    /// </summary>
    [Test]
    public void Gacha_ExcludesCappedItems()
    {
        var inv = new ItemInventory();
        for (int i = 0; i < 10; i++) inv.Add("atk"); // 일반 상한 10

        foreach (var d in GachaService.Pool(inv))
            Assert.AreNotEqual("atk", d.id, "상한에 찬 아이템이 후보에 남아 있다");
    }

    /// <summary>
    /// 천장 — 영웅+가 안 나온 횟수가 쌓이면 다음은 영웅 이상 확정(원본 `PITY_AT` :6540).
    /// 그리고 영웅+가 나오면 **0으로 리셋**된다.
    /// </summary>
    [Test]
    public void Gacha_PityForcesEpicOrBetter()
    {
        var p = ProfileService.Current;
        p.shards = GachaService.Cost;
        p.items.pity = GachaService.PityAt;

        var got = GachaService.Pull(p, 1);

        Assert.AreEqual(1, got.Count);
        Assert.IsTrue(got[0].grade == ItemGrade.Epic || got[0].grade == ItemGrade.Legend,
            $"천장이 찼는데 {ItemDatabase.GradeName(got[0].grade)}이 나왔다");
        Assert.AreEqual(0, p.items.pity, "영웅+가 나왔는데 천장이 안 풀렸다");
    }

    /// <summary>전부 상한에 차면 더 뽑히지 않고 **포인트도 안 깎인다**.</summary>
    [Test]
    public void Gacha_StopsWhenEverythingIsCapped()
    {
        var p = ProfileService.Current;
        foreach (var d in ItemDatabase.All)
            for (int i = 0; i < ItemDatabase.Cap(d.grade); i++) p.items.Add(d.id);

        p.shards = 1000;
        Assert.AreEqual(0, GachaService.Pull(p, 5).Count);
        Assert.AreEqual(1000, p.shards);
    }

    /// <summary>아이템은 **회귀해도 남는다** — 이게 영구 성장 축의 정의다.</summary>
    [Test]
    public void Items_SurviveRegression()
    {
        var p = ProfileService.Current;
        p.items.Add("atk");
        p.items.Add("lifesteal");
        p.MarkBossCleared(1);

        p.DoRegression();

        Assert.AreEqual(1, p.items.Count("atk"));
        Assert.AreEqual(1, p.items.Count("lifesteal"));
    }

    /// <summary>
    /// '시작의 유산' — 시간 회귀 후 골드 강화를 맨바닥이 아니라 몇 레벨 쥐고 시작한다(원본 `headstart` :6524).
    /// </summary>
    [Test]
    public void Headstart_GrantsUpgradeLevelsAfterRegression()
    {
        var p = ProfileService.Current;
        p.items.Add("headstart"); // per 2
        p.MarkBossCleared(1);

        p.DoRegression();

        Assert.AreEqual(2, p.upgrades.atk);
        Assert.AreEqual(2, p.upgrades.crit);
    }

    /// <summary>
    /// '각인의 봉인' — 회귀해도 **빌드(갈래·층)**가 유지된다(원본 `keepSpec` :6527). 쓴 SP는 봉인이 있어도 0이 된다 —
    /// 원본이 `resetAllCharSp()`(:6524)를 봉인 여부와 상관없이 부른다(2026-09-30 캐릭터별 성장 때 원본대로 바로잡음,
    /// 예전 테스트는 SP가 남는다고 봤다).
    /// </summary>
    [Test]
    public void KeepSpec_PreservesSpecializationAcrossRegression()
    {
        var p = ProfileService.Current;
        p.mageBranch = MageBranch.Explosion;
        p.mageTier = 4;
        p.spUsed = 10;
        p.MarkBossCleared(1);

        p.DoRegression();
        Assert.AreEqual(MageBranch.None, p.mageBranch, "봉인이 없으면 전문화는 날아가야 한다");

        p.items.Add("keepSpec");
        p.mageBranch = MageBranch.Gravity;
        p.mageTier = 3;
        p.spUsed = 6;
        p.MarkBossCleared(1);

        p.DoRegression();
        Assert.AreEqual(MageBranch.Gravity, p.mageBranch, "봉인이 있는데 전문화가 날아갔다");
        Assert.AreEqual(3, p.mageTier);
        Assert.AreEqual(0, p.spUsed, "원본은 봉인이 있어도 쓴 SP를 0으로 돌린다(resetAllCharSp)");
    }
}

}
