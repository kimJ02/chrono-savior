using System.Collections.Generic;
using NUnit.Framework;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 캐릭터별 성장 — 원본 `meta.charProgress[캐릭터] = { level, exp, spUsed }`(project_test.html:1124~:1265).
/// 2026-09-30 사용자 지시 "캐릭터별 분리는 원본대로": **레벨·경험치·SP만 캐릭터마다 따로**, 골드·골드 강화·
/// 시간의 파편·아이템은 모든 캐릭터가 같이 쓴다. 저장·옛 세이브 옮기기는 `SaveServiceTests`.
/// </summary>
public class CharacterProgressTests
{
    [SetUp]
    public void Setup() => ProfileService.Reset();

    [TearDown]
    public void Teardown() => ProfileService.Reset();

    /// <summary>원본 `gainExpMeta`(:1441)·`learnSkill`(:7023)은 지금 캐릭터의 칸(`charProg()`)만 건드린다.</summary>
    [Test]
    public void LevelExpSp_ArePerCharacter()
    {
        var p = ProfileService.Current; // 기본은 마법사
        p.AddExp(PlayerProfile.RequiredExp(1) + 5); // 1 → 2레벨, 5 남는다
        Assert.AreEqual(2, p.level);
        Assert.AreEqual(5, p.exp);
        Assert.IsTrue(p.TryLearnMageTier(MageBranch.Explosion), "마법사 SP 1로 1층을 못 배웠다");

        p.character = CharacterId.Gunner;
        Assert.AreEqual(1, p.level, "메카닉으로 바꿨는데 마법사 레벨이 보인다 — 원본은 캐릭터마다 따로(charProg)");
        Assert.AreEqual(0, p.exp);
        Assert.AreEqual(0, p.spUsed, "마법사가 쓴 SP가 메카닉 쪽에 잡혔다");
        Assert.AreEqual(0, p.SpAvailable);

        p.AddExp(30); // 메카닉으로 사냥 — 메카닉만 오른다
        Assert.AreEqual(30, p.exp);

        p.character = CharacterId.Mage;
        Assert.AreEqual(2, p.level, "메카닉 경험치가 마법사에게 들어갔다");
        Assert.AreEqual(5, p.exp);
        Assert.AreEqual(1, p.spUsed);
        Assert.AreEqual(30, p.ProgressOf(CharacterId.Gunner).exp);
    }

    /// <summary>원본은 골드(`meta.gold`)·골드 강화(`meta.upgrades`)를 캐릭터별로 나누지 않는다.</summary>
    [Test]
    public void GoldAndUpgrades_AreSharedByAllCharacters()
    {
        var p = ProfileService.Current;
        p.AddGold(1_000_000);

        p.character = CharacterId.Gunner;
        Assert.AreEqual(1_000_000, p.gold, "골드는 모든 캐릭터가 같이 쓴다");
        Assert.IsTrue(p.TryBuyUpgrade(UpgradeStat.Atk));

        p.character = CharacterId.Mage;
        Assert.AreEqual(1, p.upgrades.atk, "메카닉으로 산 골드 강화가 마법사에게 안 보인다 — 원본은 같이 쓴다");
        Assert.Less(p.gold, 1_000_000);
    }

    /// <summary>
    /// 원본 `doRebirth`(:6509) — `resetAllCharLevels()`·`resetAllCharSp()`로 **모든 캐릭터**를 1레벨·SP 0으로.
    /// '각인의 봉인'은 빌드(갈래·층)만 남기고, 쓴 SP는 봉인이 있어도 0이 된다.
    /// </summary>
    [Test]
    public void Regression_ResetsEveryCharacter_KeepSpecKeepsBuildButNotSp()
    {
        var p = ProfileService.Current;
        p.items.Add("keepSpec");
        p.MarkBossCleared(1); // 정복 지역이 없으면 회귀 자체가 안 된다
        p.level = 10; p.spUsed = 6; p.mageBranch = MageBranch.Gravity; p.mageTier = 3; // 마법사
        p.character = CharacterId.Gunner;
        p.level = 4; p.exp = 99; p.spUsed = 3; p.gunnerBranch = GunnerBranch.Laser; p.gunnerTier = 2;

        Assert.Greater(p.DoRegression(), 0);

        foreach (var id in new[] { CharacterId.Mage, CharacterId.Gunner, CharacterId.Blade })
        {
            var cp = p.ProgressOf(id);
            Assert.AreEqual(1, cp.level, $"{id} 레벨이 회귀 뒤에 남았다 — 원본 resetAllCharLevels는 모든 캐릭터");
            Assert.AreEqual(0, cp.exp);
            Assert.AreEqual(0, cp.spUsed, $"{id}가 쓴 SP가 남았다 — 원본 resetAllCharSp는 봉인과 상관없이 돈다");
        }
        Assert.AreEqual(MageBranch.Gravity, p.mageBranch, "봉인이 있는데 마법사 빌드가 지워졌다");
        Assert.AreEqual(3, p.mageTier);
        Assert.AreEqual(GunnerBranch.Laser, p.gunnerBranch, "봉인이 있는데 메카닉 빌드가 지워졌다");
        Assert.AreEqual(2, p.gunnerTier);
    }

    /// <summary>칸이 모자라거나 없는 세이브도 원본 `charProg`처럼 1레벨 칸을 채워서 연다.</summary>
    [Test]
    public void ProgressOf_FillsMissingSlots()
    {
        var p = new PlayerProfile();
        p.charProgress = new List<CharacterProgress> { new CharacterProgress { level = 7 } }; // 마법사 칸만 있는 세이브
        Assert.AreEqual(7, p.ProgressOf(CharacterId.Mage).level);
        Assert.AreEqual(1, p.ProgressOf(CharacterId.Blade).level, "모자란 칸은 1레벨로 채워야 한다");

        p.charProgress = null;
        Assert.AreEqual(1, p.level, "칸 목록이 통째로 없어도 1레벨로 시작해야 한다");
    }
}
}
