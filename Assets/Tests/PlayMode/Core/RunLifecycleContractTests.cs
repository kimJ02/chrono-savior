using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 런 사이클 착수 전제 3종(`GameState`/`RunState`/`CombatEvents`)의 계약 검증.
/// 이 셋은 팀원 세션도 읽는 공유 계약(`docs/worksplit.md` 3절)이라, 동작이 바뀌면 여기서 먼저 깨져야 한다.
///
/// 셋 다 정적 상태 + 정적 이벤트라 테스트끼리 오염된다 — 매 테스트 전후로 반드시 리셋한다
/// (`ProfileService` 오염을 이미 겪은 것과 같은 종류).
/// </summary>
public class RunLifecycleContractTests
{
    [SetUp]
    public void Setup()
    {
        GameState.Reset();
        RunState.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        GameState.Reset();
        RunState.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
    }

    [Test]
    public void GameState_StartsInLobby_AndIsRunningOnlyDuringRun()
    {
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.IsFalse(GameState.IsRunning, "로비인데 IsRunning이 true — 적/스폰이 로비에서도 돌게 된다");

        GameState.Set(GameScene.Run);
        Assert.IsTrue(GameState.IsRunning);

        // 원본 frame()은 scene이 'run'이 아니면 dt=0으로 전부 멈춘다(project_test.html:7169)
        GameState.Set(GameScene.Pause);
        Assert.IsFalse(GameState.IsRunning, "일시정지인데 IsRunning이 true");
        GameState.Set(GameScene.Result);
        Assert.IsFalse(GameState.IsRunning, "결과 화면인데 IsRunning이 true");
    }

    [Test]
    public void GameState_Changed_FiresOnlyOnActualTransition()
    {
        int fired = 0;
        GameScene last = GameScene.Lobby;
        GameState.Changed += s => { fired++; last = s; };

        GameState.Set(GameScene.Run);
        Assert.AreEqual(1, fired);
        Assert.AreEqual(GameScene.Run, last);

        GameState.Set(GameScene.Run); // 같은 값 재설정
        Assert.AreEqual(1, fired, "같은 화면으로 다시 Set했는데 이벤트가 또 발생함");
    }

    [Test]
    public void RunState_Begin_ResetsCountersAndSetsTimeByMode()
    {
        RunState.Begin(3, RunMode.Normal);
        Assert.AreEqual(3, RunState.Region);
        Assert.AreEqual(RunState.NormalTime, RunState.TimeLeft, 0.001f);
        Assert.AreEqual(0, RunState.Kills);
        Assert.AreEqual(0, RunState.Fury);
        Assert.IsFalse(RunState.Over);

        RunState.Begin(5, RunMode.Boss);
        Assert.AreEqual(RunState.BossTime, RunState.TimeLeft, 0.001f, "보스 스테이지 제한시간이 원본(180초)과 다름");
    }

    [Test]
    public void RunState_Tick_ReturnsTrueExactlyWhenTimeRunsOut()
    {
        RunState.Begin(1, RunMode.Normal);
        Assert.IsFalse(RunState.Tick(10f), "아직 시간이 남았는데 종료를 알림");
        Assert.AreEqual(RunState.NormalTime - 10f, RunState.TimeLeft, 0.001f);

        Assert.IsTrue(RunState.Tick(RunState.NormalTime), "시간이 다 됐는데 종료를 안 알림");
        Assert.AreEqual(0f, RunState.TimeLeft, 0.001f, "남은 시간이 음수로 내려감");
    }

    [Test]
    public void RunState_RegisterKill_CountsFuryForBossButNotKills()
    {
        RunState.Begin(1, RunMode.Normal);

        RunState.RegisterKill(isBoss: false);
        Assert.AreEqual(1, RunState.Kills);
        Assert.AreEqual(1, RunState.Fury);

        // 원본: run.fury++는 보스 포함 전부(:1825), run.kills++는 보스 제외(:1857)
        RunState.RegisterKill(isBoss: true);
        Assert.AreEqual(1, RunState.Kills, "보스를 처치 수에 포함시킴 — 원본은 제외한다(:1857)");
        Assert.AreEqual(2, RunState.Fury, "보스 처치가 살기에 안 쌓임 — 원본은 쌓인다(:1825)");
    }

    [Test]
    public void CombatEvents_RelaySignalsToSubscribers()
    {
        GameObject killed = null;
        float buffDuration = 0f;
        int gold = 0, exp = 0;

        CombatEvents.EnemyKilled += e => killed = e;
        CombatEvents.ShrineBuffGranted += d => buffDuration = d;
        CombatEvents.RewardGranted += (g, x) => { gold = g; exp = x; };

        var dummy = new GameObject("TestEnemyDummy");
        CombatEvents.RaiseEnemyKilled(dummy);
        CombatEvents.RaiseShrineBuffGranted(15f);
        CombatEvents.RaiseRewardGranted(12, 34);

        Assert.AreSame(dummy, killed);
        Assert.AreEqual(15f, buffDuration, 0.001f);
        Assert.AreEqual(12, gold);
        Assert.AreEqual(34, exp);

        Object.DestroyImmediate(dummy);
    }

    [Test]
    public void CombatEvents_Reset_DropsSubscribers()
    {
        int fired = 0;
        CombatEvents.EnemyKilled += _ => fired++;
        CombatEvents.Reset(); RunEvents.Reset();
        CombatEvents.RaiseEnemyKilled(null);
        Assert.AreEqual(0, fired, "Reset 후에도 옛 구독자가 남아 호출됨 — 테스트 간 오염 원인");
    }
}
}
