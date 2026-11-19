using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 유물 30종(2026-11-19 테스트 유물 — 캐릭터 전용 15 + 공용 15)과 가챠.
/// 원본 35종 시절의 스탯·특수 아이템 테스트는 유물을 갈아 끼우면서 지웠다(사용자 2026-11-19 "원본 유물은 신경 안 써도 된다").
/// </summary>
public class ItemAndGachaTests
{
    [SetUp]
    public void Setup() { ProfileService.Reset(); RunState.Reset(); CombatModifiers.Reset(); }

    [TearDown]
    public void Teardown() { ProfileService.Reset(); RunState.Reset(); CombatModifiers.Reset(); }

    // ────────────────────────── 정의 ──────────────────────────

    /// <summary>30종 = 캐릭터마다 5종(전설 1종씩) + 공용 15종. 등급 합계 N12/R10/E5/L3. 아이디 중복 없음.</summary>
    [Test]
    public void RelicDatabase_Has30_FivePerCharacter_AndGradeSplit()
    {
        Assert.AreEqual(30, ItemDatabase.All.Length);
        var ids = new HashSet<string>();
        var perOwner = new Dictionary<RelicOwner, int>();
        var legendPerOwner = new Dictionary<RelicOwner, int>();
        var perGrade = new Dictionary<ItemGrade, int>();
        foreach (var d in ItemDatabase.All)
        {
            Assert.IsTrue(ids.Add(d.id), $"아이디가 중복됐다: {d.id}");
            Assert.IsNotEmpty(d.values, $"{d.id}: 랭크별 수치가 없다");
            foreach (var v in d.values)
                Assert.GreaterOrEqual(v.Length, ItemDatabase.Cap(d.grade), $"{d.id}: 랭크 상한만큼 수치가 없다");
            perOwner[d.owner] = perOwner.TryGetValue(d.owner, out var n) ? n + 1 : 1;
            perGrade[d.grade] = perGrade.TryGetValue(d.grade, out var g) ? g + 1 : 1;
            if (d.grade == ItemGrade.Legend) legendPerOwner[d.owner] = legendPerOwner.TryGetValue(d.owner, out var l) ? l + 1 : 1;
        }
        Assert.AreEqual(15, perOwner[RelicOwner.Common]);
        foreach (var o in new[] { RelicOwner.Mage, RelicOwner.Gunner, RelicOwner.Blade })
        {
            Assert.AreEqual(5, perOwner[o], $"{o} 전용 유물 수");
            Assert.AreEqual(1, legendPerOwner[o], $"{o} 전설 수");
        }
        Assert.AreEqual(12, perGrade[ItemGrade.Common]);
        Assert.AreEqual(10, perGrade[ItemGrade.Rare]);
        Assert.AreEqual(5, perGrade[ItemGrade.Epic]);
        Assert.AreEqual(3, perGrade[ItemGrade.Legend]);
    }

    /// <summary>랭크 상한 일반·희귀 3, 영웅·전설 2. 뽑기 확률은 Figma 70/27/2/1%.</summary>
    [Test]
    public void Grades_CapAndWeight()
    {
        Assert.AreEqual(3, ItemDatabase.Cap(ItemGrade.Common));
        Assert.AreEqual(3, ItemDatabase.Cap(ItemGrade.Rare));
        Assert.AreEqual(2, ItemDatabase.Cap(ItemGrade.Epic));
        Assert.AreEqual(2, ItemDatabase.Cap(ItemGrade.Legend));

        Assert.AreEqual(70, ItemDatabase.Weight(ItemGrade.Common));
        Assert.AreEqual(27, ItemDatabase.Weight(ItemGrade.Rare));
        Assert.AreEqual(2, ItemDatabase.Weight(ItemGrade.Epic));
        Assert.AreEqual(1, ItemDatabase.Weight(ItemGrade.Legend));
    }

    /// <summary>중복은 상한까지만 쌓인다.</summary>
    [Test]
    public void Inventory_RespectsCap()
    {
        var inv = new ItemInventory();
        for (int i = 0; i < 50; i++) inv.Add("flameCrown"); // 전설 = 상한 2
        Assert.AreEqual(2, inv.Count("flameCrown"));
        Assert.IsTrue(inv.IsFull("flameCrown"));
    }

    /// <summary>
    /// 켜지는 조건 — 캐릭터 전용은 그 캐릭터일 때만, 갈래 유물은 그 갈래를 골랐을 때만. 공용은 늘.
    /// 랭크별 수치는 랭크에 맞는 칸을 읽고, 상한이면 각성.
    /// </summary>
    [Test]
    public void Relics_RankFollowsOwnerAndBranch()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        p.mageBranch = MageBranch.None;
        p.items.Add("mageEcho");
        p.items.Add("emberLedger");
        p.items.Add("lantern");
        p.items.Add("spareRounds");

        Assert.AreEqual(1, Relics.Rank("mageEcho"), "기본공격 유물은 갈래가 없어도 켜진다");
        Assert.AreEqual(0, Relics.Rank("emberLedger"), "폭발 갈래를 안 골랐는데 켜졌다");
        Assert.AreEqual(1, Relics.Rank("lantern"), "공용은 늘 켜진다");
        Assert.AreEqual(0, Relics.Rank("spareRounds"), "메카닉 전용이 마법사에게 켜졌다");

        p.mageBranch = MageBranch.Explosion;
        Assert.AreEqual(1, Relics.Rank("emberLedger"));

        Assert.AreEqual(4f, Relics.V("mageEcho", 0), 0.001f, "1랭크 간격");
        p.items.Add("mageEcho");
        p.items.Add("mageEcho");
        Assert.AreEqual(3, Relics.Rank("mageEcho"));
        Assert.AreEqual(3f, Relics.V("mageEcho", 1), 0.001f, "3랭크 발 수");
        Assert.IsTrue(Relics.Awakened("mageEcho"), "상한이면 각성");
        Assert.IsFalse(Relics.Awakened("lantern"));
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
        for (int i = 0; i < 3; i++) inv.Add("lantern"); // 일반 상한 3

        foreach (var d in GachaService.Pool(inv))
            Assert.AreNotEqual("lantern", d.id, "상한에 찬 유물이 후보에 남아 있다");
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

    /// <summary>
    /// 개방 값 — 낼 수 있으면 파편이 빠지고 뽑기 수가 하나 오른다. 모자라거나 값이 0 이하면 **둘 다 그대로**(2026-11-06 4-4, `PlayerProfile.TryPayForPull`).
    /// </summary>
    [Test]
    public void PayForPull_ChargesShardsAndCountsPull_OrChangesNothing()
    {
        var p = ProfileService.Current;
        p.shards = GachaService.Cost;

        Assert.IsTrue(p.TryPayForPull(GachaService.Cost));
        Assert.AreEqual(0, p.shards);
        Assert.AreEqual(1, p.stats.pulls);

        Assert.IsFalse(p.TryPayForPull(GachaService.Cost), "파편이 없는데 냈다");
        Assert.IsFalse(p.TryPayForPull(0), "0으로 뽑기 수만 올릴 수 있다");
        Assert.AreEqual(0, p.shards);
        Assert.AreEqual(1, p.stats.pulls, "못 냈는데 뽑기 수가 올랐다");
    }

    /// <summary>뽑힌 아이템 기록 — 아이템이 더해지고, 천장은 일반·희귀면 1 오르고 영웅 이상이면 0(원본 `:6583`).</summary>
    [Test]
    public void RecordPull_AddsItemAndUpdatesPity()
    {
        var inv = ProfileService.Current.items;
        var common = System.Array.Find(ItemDatabase.All, d => d.grade == ItemGrade.Common);
        var epic = System.Array.Find(ItemDatabase.All, d => d.grade == ItemGrade.Epic);

        inv.RecordPull(common);
        inv.RecordPull(common);
        Assert.AreEqual(2, inv.Count(common.id));
        Assert.AreEqual(2, inv.pity);

        inv.RecordPull(epic);
        Assert.AreEqual(1, inv.Count(epic.id));
        Assert.AreEqual(0, inv.pity, "영웅이 나왔는데 천장이 안 풀렸다");
    }

    /// <summary>유물은 **회귀해도 남는다**.</summary>
    [Test]
    public void Items_SurviveRegression()
    {
        var p = ProfileService.Current;
        p.items.Add("lantern");
        p.items.Add("flameCrown");
        p.MarkBossCleared(1);

        p.DoRegression();

        Assert.AreEqual(1, p.items.Count("lantern"));
        Assert.AreEqual(1, p.items.Count("flameCrown"));
    }
}

}
