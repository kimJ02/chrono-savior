using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 보스를 잡은 **다음 사냥**이 입장 직후 "보스 격파"로 끝나던 버그의 회귀 검사(2026-09-28 사용자 발견).
///
/// 원인: 보스가 죽으면 결과 화면을 두 경로로 예약했다 — `RunEvents.BossDefeated` → `bossDeadTimer`(멈춘 시간 기준,
/// 초기화됨)와 `CombatEvents.EnemyKilled`(보스) → `Invoke(1.6초)`(게임 시간 기준, 취소 안 됨). 타이머가 먼저 런을
/// 끝내면 `timeScale = 0`인 결과·로비 동안 `Invoke`가 멈춘 채 남아 있다가, 다음 사냥에서 시간이 흐르자마자 터졌다.
/// 그래서 실제 게임처럼 **두 이벤트를 모두** 올린 뒤 다음 사냥이 멀쩡히 이어지는지 본다.
/// </summary>
public class BossDefeatCarryOverTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f; // GameFlow가 0으로 두고 끝나면 뒤 테스트의 대기가 안 끝난다
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        ResetStatics();
    }

    static void ResetStatics()
    {
        GameState.Reset();
        RunState.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunTransient.Reset();
        CombatModifiers.Reset();
        ProfileService.Reset();
        FieldLayout.SetBossArena(false);
    }

    [UnityTest]
    public IEnumerator BossKill_DoesNotEndTheNextRun()
    {
        var run = TestFlow.Create();
        yield return null;

        run.StartRun(1, RunMode.Normal);
        CombatDirector.Current.EnterBossField();
        yield return null;

        // 실제 보스 사망과 같은 두 이벤트: 스포너의 보스 격파 알림 + 처치 알림(보스 컴포넌트가 붙은 적).
        var boss = new GameObject("TestBoss");
        boss.AddComponent<Boss>().Equipped().enabled = false; // 행동은 필요 없다 — "보스인 적"이라는 표식만 쓴다
        boss.GetComponent<EnemyMove>().enabled = false;
        RunEvents.RaiseBossDefeated();
        CombatEvents.RaiseEnemyKilled(boss);

        yield return new WaitForSecondsRealtime(CombatDirector.BossDeadEndDelay + 0.3f);
        Assert.AreEqual(GameScene.Result, GameState.Current, "보스를 잡았는데 런이 안 끝났다");
        Assert.AreEqual(RunEndReason.BossDead, run.LastEndReason);

        // 결과 → 로비 → 다음 사냥. 예전엔 여기서 멈춰 있던 예약이 입장 직후 터졌다.
        run.EnterLobby();
        yield return null;
        run.StartRun(1, RunMode.Normal);

        float t = 0f;
        while (t < 2.5f) { yield return null; t += Time.deltaTime; } // 게임 시간으로 2.5초(옛 예약 1.6초보다 길게)

        Assert.AreEqual(GameScene.Run, GameState.Current,
            "보스를 잡은 다음 사냥이 입장 직후 끝났다 — 지난 런의 보스 격파 예약이 새어 들어왔다");
    }
}
}
