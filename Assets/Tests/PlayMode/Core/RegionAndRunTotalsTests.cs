using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 로비가 읽는 값들 — 지역 표(입장료·해금)와 이번 런 집계(결과 화면에 뜨는 골드/경험치/처치수).
/// UI 자체(IMGUI)는 테스트하지 않고, **UI가 부르는 계산과 그 계산에 값을 넣어주는 배선**을 본다.
/// </summary>
public class RegionAndRunTotalsTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        RunState.Reset();
        RunTransient.Reset();
        CombatModifiers.Reset();
        ProfileService.Replace(new PlayerProfile());
    }

    // ────────────────────────── 지역 표 ──────────────────────────

    /// <summary>
    /// 원본 `entryFeeOf(r) = r <= 1 ? 0 : floor(300 × 2.2^(r-2))`(project_test.html:6438).
    /// **지역당 지수로 오르는 게 요점**이다 — 결국 감당이 안 되고 그게 시간 회귀를 강제하는 구조라
    /// 완만하게 고치면 게임의 진행 압력이 통째로 사라진다.
    /// </summary>
    [Test]
    public void EntryFee_IsFree_OriginalCurveKeptForReference()
    {
        for (int r = 1; r <= 9; r++) Assert.AreEqual(0, RegionConfig.EntryFee(r), $"stage {r} — 입장료 없음(사용자 2026-10-25)");
        Assert.AreEqual(0, RegionConfig.OriginalEntryFee(1), "원본도 1지역은 무료다");
        Assert.AreEqual(300, RegionConfig.OriginalEntryFee(2));
        Assert.AreEqual(Mathf.FloorToInt(300f * 2.2f), RegionConfig.OriginalEntryFee(3));
        Assert.AreEqual(Mathf.FloorToInt(300f * Mathf.Pow(2.2f, 7)), RegionConfig.OriginalEntryFee(9));
    }

    /// <summary>원본 `regionBaseLv(r) = (r-1)*3 + 1`(project_test.html:972).</summary>
    [Test]
    public void RecommendedLevel_MatchesOriginal()
    {
        Assert.AreEqual(1, RegionConfig.RecommendedLevel(1));
        Assert.AreEqual(4, RegionConfig.RecommendedLevel(2));
        Assert.AreEqual(25, RegionConfig.RecommendedLevel(9));
    }

    /// <summary>
    /// 원본 `regionOpen(r) = r === 1 || regions[r-1].bossCleared`(project_test.html:6434).
    /// 보스가 생기면서 임시 전체 개방(`debugUnlockAllRegions`)은 삭제됐고, 이제 이게 진짜 규칙이다.
    /// </summary>
    [Test]
    public void RegionOpen_RequiresPreviousBossCleared()
    {
        var profile = new PlayerProfile();

        Assert.IsTrue(profile.IsRegionOpen(1), "1지역은 항상 열려 있다");
        Assert.IsFalse(profile.IsRegionOpen(2), "1지역 보스를 안 잡았는데 2지역이 열렸다");

        profile.MarkBossCleared(1);
        Assert.IsTrue(profile.IsRegionOpen(2));
        Assert.IsFalse(profile.IsRegionOpen(3), "2지역 보스는 아직인데 3지역이 열렸다");
    }

    /// <summary>
    /// 원본 `:1866`~`:1869` — 그 지역에서 100마리를 채우면 보스가 열린다.
    /// **목표에 닿으면 더 안 센다**(게이지가 100/100에서 멈춘다).
    /// </summary>
    [Test]
    public void RegionKills_UnlockBossAtTarget_ThenStopCounting()
    {
        var profile = new PlayerProfile();
        int target = RunState.RegionKillTarget;

        for (int i = 0; i < target - 1; i++)
            Assert.IsFalse(profile.RegisterRegionKill(1), "목표 전에 보스가 열렸다");
        Assert.IsFalse(profile.IsBossUnlocked(1));

        Assert.IsTrue(profile.RegisterRegionKill(1), "목표를 채웠는데 해금 신호가 안 왔다");
        Assert.IsTrue(profile.IsBossUnlocked(1));
        Assert.AreEqual(target, profile.RegionKills(1));

        profile.RegisterRegionKill(1);
        Assert.AreEqual(target, profile.RegionKills(1), "목표를 넘겨서 계속 세고 있다");
    }

    /// <summary>지역별로 따로 센다 — 1지역에서 잡은 게 2지역 게이지를 채우면 안 된다.</summary>
    [Test]
    public void RegionKills_AreTrackedPerRegion()
    {
        var profile = new PlayerProfile();
        for (int i = 0; i < 10; i++) profile.RegisterRegionKill(1);

        Assert.AreEqual(10, profile.RegionKills(1));
        Assert.AreEqual(0, profile.RegionKills(2));
    }

    // ────────────────────────── 런 집계 ──────────────────────────

    /// <summary>원본 `run.fury++`는 보스 포함 전부, `run.kills++`는 보스 제외(:1825·:1857).</summary>
    [Test]
    public void BossKill_CountsFuryButNotKills()
    {
        RunState.Begin(1, RunMode.Boss);

        RunState.RegisterKill(isBoss: false);
        RunState.RegisterKill(isBoss: true);

        Assert.AreEqual(1, RunState.Kills, "보스는 처치 수에 안 들어간다");
        Assert.AreEqual(2, RunState.Fury, "살기는 보스도 센다");
    }

    /// <summary>새 런을 시작하면 지난 런 집계가 남아 있으면 안 된다(원본 `startRun`의 초기화, :4307).</summary>
    [Test]
    public void BeginRun_ClearsPreviousTotals()
    {
        RunState.Begin(1, RunMode.Normal);
        RunState.RegisterKill(isBoss: false);
        RunState.RegisterReward(500, 900);

        RunState.Begin(2, RunMode.Normal);

        Assert.AreEqual(0, RunState.Kills);
        Assert.AreEqual(0, RunState.GoldEarned);
        Assert.AreEqual(0, RunState.ExpEarned);
        Assert.AreEqual(2, RunState.Region);
        Assert.AreEqual(RunState.NormalTime, RunState.TimeLeft, 0.01f);
    }
}

}
