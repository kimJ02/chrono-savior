using NUnit.Framework;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>환산 전투력·권장 전투력(2026-11-22 사용자 요청, GPT와 정함 — `Core/CombatPower`).</summary>
public class CombatPowerTests
{
    [SetUp] public void Setup() { ProfileService.Reset(); RunState.Reset(); CombatModifiers.Reset(); }
    [TearDown] public void Teardown() { ProfileService.Reset(); RunState.Reset(); CombatModifiers.Reset(); }

    [Test]
    public void Recommended_RisesEveryStep_AndChaptersJoinSmoothly()
    {
        Assert.AreEqual(CombatPower.ReferencePower, CombatPower.RecommendedFor(1, 1), "1-1 = 기준");
        int prev = 0;
        for (int c = 1; c <= 2; c++)
            for (int s = 1; s <= ChapterConfig.BossStage; s++)
            {
                int r = CombatPower.RecommendedFor(c, s);
                if (s < ChapterConfig.BossStage) Assert.Greater(r, prev, $"{c}-{s} 권장이 앞 스테이지보다 낮다");
                if (s < ChapterConfig.BossStage) prev = r;
            }
        Assert.Less(CombatPower.RecommendedFor(1, 5), CombatPower.RecommendedFor(2, 1), "1-5가 2-1보다 높으면 안 된다(중간 보스 배율을 넣지 않는다)");
        Assert.AreEqual(0, CombatPower.RecommendedFor(9, 1), "없는 챕터");
    }

    [Test]
    public void Power_GrowsWithUpgradesAndLevel_AndIgnoresRunOnlyBuffs()
    {
        var fresh = new PlayerProfile();
        int basePower = CombatPower.Of(fresh);
        Assert.Greater(basePower, 0);

        Assert.Greater(CombatPower.Of(SavePresets.Build(2)), basePower, "슬롯 2(Lv16 스킬 5층)가 새 프로필보다 세다");
        Assert.Greater(CombatPower.Of(SavePresets.Build(3)), CombatPower.Of(SavePresets.Build(2)), "슬롯 3(강화 15)이 슬롯 2보다 세다");

        CombatModifiers.Reset();
        int before = CombatPower.Of(fresh);
        RunState.Begin(1, RunMode.Normal);
        for (int i = 0; i < 30; i++) RunState.RegisterKill(false); // 사냥 중에만 붙는 살기 — 전투력엔 안 들어간다
        Assert.Greater(RunState.Fury, 0, "살기가 안 쌓였다(테스트 전제)");
        Assert.AreEqual(before, CombatPower.Of(fresh), "사냥 중 버프가 전투력을 바꿨다");
    }

    [Test]
    public void Grade_ThreeBands()
    {
        Assert.AreEqual(2, CombatPower.Grade(10_000, 10_000));
        Assert.AreEqual(1, CombatPower.Grade(7_000, 10_000));
        Assert.AreEqual(0, CombatPower.Grade(6_999, 10_000));
    }
}

}
