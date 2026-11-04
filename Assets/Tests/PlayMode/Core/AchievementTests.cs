using NUnit.Framework;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 업적 — 원본 `ACHIEVEMENTS`(project_test.html:802)와 `checkAchievements()`(`:1451`).
/// 보상은 없고 진행 지표라, **한 번 달성하면 다시 알리지 않는다**는 것과
/// **회귀해도 안 지워진다**는 두 성질이 핵심이다.
/// </summary>
public class AchievementTests
{
    [SetUp]
    public void Setup() { ProfileService.Reset(); CombatModifiers.Reset(); }

    [TearDown]
    public void Teardown() { ProfileService.Reset(); CombatModifiers.Reset(); }

    /// <summary>조건을 만족하면 달성되고, **두 번째 확인에서는 새 달성으로 안 잡힌다**.</summary>
    [Test]
    public void CheckNew_ReportsEachAchievementOnlyOnce()
    {
        var p = ProfileService.Current;
        p.stats.totalKills = 1;

        var first = Achievements.CheckNew(p);
        Assert.IsTrue(first.Exists(a => a.id == "kill1"), "조건을 채웠는데 달성이 안 됐다");

        var second = Achievements.CheckNew(p);
        Assert.IsFalse(second.Exists(a => a.id == "kill1"), "이미 달성한 업적이 또 새 달성으로 잡혔다");
        Assert.IsTrue(p.achieved.Contains("kill1"));
    }

    /// <summary>조건을 안 채웠으면 달성되지 않는다.</summary>
    [Test]
    public void CheckNew_DoesNotGrantUnearned()
    {
        var p = ProfileService.Current;
        p.stats.totalKills = 499;

        Achievements.CheckNew(p);
        Assert.IsFalse(p.achieved.Contains("kill500"));

        p.stats.totalKills = 500;
        Achievements.CheckNew(p);
        Assert.IsTrue(p.achieved.Contains("kill500"));
    }

    /// <summary>
    /// 업적과 누적 통계는 **회귀해도 남는다** — "이번 생"이 아니라 "지금까지"를 보는 지표다.
    /// 여기가 지워지면 장기 목표가 매 시간 회귀마다 초기화된다.
    /// </summary>
    [Test]
    public void Achievements_SurviveRegression()
    {
        var p = ProfileService.Current;
        p.stats.totalKills = 600;
        p.stats.goldEarned = 5000;
        Achievements.CheckNew(p);
        p.MarkBossCleared(1);

        p.DoRegression();

        Assert.IsTrue(p.achieved.Contains("kill500"), "회귀했다고 업적이 사라졌다");
        Assert.AreEqual(600, p.stats.totalKills, "누적 통계가 초기화됐다");
        Assert.AreEqual(5000, p.stats.goldEarned);
    }

    /// <summary>시간 회귀 자체도 업적이다 — 시간 회귀 직후 확인하면 잡힌다.</summary>
    [Test]
    public void Regression_UnlocksRegressionAchievement()
    {
        var p = ProfileService.Current;
        p.MarkBossCleared(1);
        p.DoRegression();

        Achievements.CheckNew(p);
        Assert.IsTrue(p.achieved.Contains("reborn1"));
        Assert.Greater(p.stats.shardsEarned, 0, "누적 시간의 파편가 안 쌓였다");
    }

    /// <summary>누적 획득 골드는 **쓴 만큼 줄지 않는다**(업적 '축재'의 기준).</summary>
    [Test]
    public void GoldEarned_DoesNotDecreaseWhenSpent()
    {
        var p = ProfileService.Current;
        p.AddGold(1000);
        p.gold -= 900; // 강화에 씀

        Assert.AreEqual(100, p.gold);
        Assert.AreEqual(1000, p.stats.goldEarned, "쓴 골드가 누적 획득량에서 빠졌다");
    }

    /// <summary>최고 콤보가 프로필 통계에 남는다 — 세션 값만 보면 껐다 켜면 사라진다.</summary>
    [Test]
    public void MaxCombo_IsRecordedOnProfile()
    {
        for (int i = 0; i < 7; i++) CombatModifiers.AddCombo();
        Assert.AreEqual(7, ProfileService.Current.stats.maxCombo);

        CombatModifiers.Tick(CombatModifiers.ComboWindow + 0.1f); // 콤보가 끊겨도
        Assert.AreEqual(7, ProfileService.Current.stats.maxCombo, "기록이 콤보와 같이 사라졌다");
    }

    /// <summary>업적 id가 중복되면 달성 기록이 서로를 덮는다.</summary>
    [Test]
    public void AchievementIds_AreUnique()
    {
        var seen = new System.Collections.Generic.HashSet<string>();
        foreach (var a in Achievements.All)
            Assert.IsTrue(seen.Add(a.id), $"업적 아이디 중복: {a.id}");
    }
}

}
