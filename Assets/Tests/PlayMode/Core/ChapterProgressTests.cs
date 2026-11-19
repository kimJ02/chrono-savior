using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 챕터·스테이지 구조(2026-11-19 사용자 확정, `docs/chapter-stage-spec.md`) — 여는 순서, 스테이지별 누적 처치, 중간 보스 포탈,
/// 챕터 3~5 잠금, 환생해도 진행 유지, 파편은 이번 생 보스만, 난이도가 1-5 → 2-1로 매끄럽게.
/// </summary>
public class ChapterProgressTests
{
    [SetUp] public void Setup() { ProfileService.Reset(); RunState.Reset(); }
    [TearDown] public void Teardown() { ProfileService.Reset(); RunState.Reset(); }

    static void Kill(PlayerProfile p, int c, int s, int n)
    {
        for (int i = 0; i < n; i++) p.RegisterStageKill(c, s);
    }

    [Test]
    public void Stages_OpenInOrder_AndBossNeedsAllFive()
    {
        var p = ProfileService.Current;
        Assert.IsTrue(p.IsStageOpen(1, 1));
        Assert.IsFalse(p.IsStageOpen(1, 2), "1-1을 안 깼는데 1-2가 열렸다");
        Assert.IsFalse(p.IsStageOpen(1, ChapterConfig.BossStage));

        Kill(p, 1, 1, 99);
        Assert.IsFalse(p.IsStageCleared(1, 1), "99마리인데 클리어");
        Assert.AreEqual(PlayerProfile.StageKillResult.StageCleared, p.RegisterStageKill(1, 1), "100마리째에 클리어");
        Assert.IsTrue(p.IsStageOpen(1, 2));
        Kill(p, 1, 1, 50);
        Assert.AreEqual(ChapterConfig.StageKillTarget, p.StageKills(1, 1), "100에서 더 안 센다");
        Assert.AreEqual(0, p.StageKills(1, 2), "스테이지끼리 처치 수가 섞였다");

        for (int s = 2; s <= 4; s++) Kill(p, 1, s, ChapterConfig.StageKillTarget);
        Assert.IsTrue(p.IsStageOpen(1, 5));
        Assert.IsFalse(p.IsStageOpen(1, ChapterConfig.BossStage), "5스테이지를 안 깼는데 보스가 열렸다");
    }

    [Test]
    public void StageFive_KillsOpenMidBoss_ClearOnlyByMidBoss()
    {
        var p = ProfileService.Current;
        Kill(p, 1, 5, 99);
        Assert.IsFalse(p.IsMidBossReady(1));
        Assert.AreEqual(PlayerProfile.StageKillResult.MidBossReady, p.RegisterStageKill(1, 5));
        Assert.IsTrue(p.IsMidBossReady(1), "100마리면 중간 보스 포탈");
        Assert.IsFalse(p.IsStageCleared(1, 5), "처치만으로 5스테이지가 클리어되면 안 된다");
        p.MarkStageCleared(1, 5); // 중간 보스 처치
        Assert.IsTrue(p.IsStageCleared(1, 5));
    }

    [Test]
    public void ChapterBoss_OpensNextChapter_ButChapterThreeIsLocked()
    {
        var p = ProfileService.Current;
        Assert.IsFalse(p.IsRegionOpen(2));
        p.RecordBossKill(1);
        Assert.IsTrue(p.IsRegionOpen(2), "1챕터 보스를 잡으면 2챕터");
        Assert.IsTrue(p.IsStageOpen(2, 1));
        p.RecordBossKill(2);
        Assert.IsFalse(p.IsRegionOpen(3), "챕터 3~5는 보스가 없어 잠금(사용자 2026-11-19)");
        Assert.IsFalse(p.IsStageOpen(3, 1));
    }

    [Test]
    public void Regression_KeepsProgress_AndPaysOnlyThisLifeBosses()
    {
        var p = ProfileService.Current;
        for (int s = 1; s <= 5; s++) p.MarkStageCleared(1, s);
        Kill(p, 2, 1, 37);
        p.RecordBossKill(1);
        int first = p.DoRegression();
        Assert.AreEqual(ChapterConfig.ShardsOfChapter(1), first);
        Assert.IsTrue(p.IsStageCleared(1, 5), "환생해도 깬 스테이지는 남는다");
        Assert.AreEqual(37, p.StageKills(2, 1), "환생해도 처치 수는 남는다");
        Assert.IsTrue(p.IsStageOpen(2, 1), "환생 뒤에도 다음 스테이지(2-1)부터");
        Assert.AreEqual(0, p.ShardPreview(), "같은 진행으로 파편을 또 받으면 안 된다");

        p.RecordBossKill(1); // 이번 생에 다시 잡으면 다시 받는다
        Assert.AreEqual(ChapterConfig.ShardsOfChapter(1), p.ShardPreview());
    }

    [Test]
    public void Difficulty_StagesStepSmoothlyIntoNextChapter()
    {
        float s5 = DifficultyScalingConfig.ScaledHp(100f, 1) * ChapterConfig.HpStageMult(5);
        float next1 = DifficultyScalingConfig.ScaledHp(100f, 2) * ChapterConfig.HpStageMult(1);
        Assert.AreEqual(Mathf.Pow(DifficultyScalingConfig.HpPerRegion, 0.2f), next1 / s5, 1e-3f, "1-5 → 2-1은 한 단계(×1.17)");
        for (int s = 2; s <= 5; s++)
            Assert.Greater(ChapterConfig.HpStageMult(s), ChapterConfig.HpStageMult(s - 1), "스테이지가 오를수록 체력이 오른다");
        Assert.AreEqual(1f, ChapterConfig.HpStageMult(1), 1e-4f);
    }

    [Test]
    public void RunState_RemembersStage_AndMidBossFlag()
    {
        RunState.Begin(1, RunMode.Normal, 5);
        Assert.AreEqual(5, RunState.Stage);
        Assert.IsFalse(RunState.IsMidBoss);
        RunState.EnterBossField();
        Assert.IsTrue(RunState.IsMidBoss, "5스테이지에서 포탈로 넘어가면 중간 보스전");
        RunState.Begin(1, RunMode.Boss);
        Assert.AreEqual(ChapterConfig.BossStage, RunState.Stage);
        Assert.IsFalse(RunState.IsMidBoss, "보스 스테이지는 중간 보스가 아니다");
    }
}

}
