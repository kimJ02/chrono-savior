using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 전투 담당(`CombatDirector`)과 흐름(`GameFlow`)의 경계 — 2026-11-05 씬 분할 2차 1단계(`HANDOFF.md` "씬 분할 2차").
/// 흐름은 담당을 직접 부르고, 담당은 이벤트로만 알린다. 전투 씬이 없을 때 흐름이 안전한지, 담당이 꺼지면 전투 사건이 더 안 닿는지 본다.
/// ⚠️ `Time.timeScale`을 TearDown에서 1로 되돌린다(GameFlowTests 주석 참고).
/// </summary>
public class CombatDirectorTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        ResetStatics();
    }

    static void ResetStatics()
    {
        GameState.Reset();
        RunState.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        WorldView.Reset();
        RunTransient.Reset();
        CombatModifiers.Reset();
        HitFeel.Reset();
        ProfileService.Reset();
    }

    [UnityTest]
    public IEnumerator Director_RegistersWhileEnabled()
    {
        var flow = TestFlow.Create();
        yield return null;
        var director = Object.FindFirstObjectByType<CombatDirector>();
        Assert.AreSame(director, CombatDirector.Current, "켜지면 스스로 등록한다(흐름이 씬을 넘어 찾지 않게)");

        director.enabled = false;
        Assert.IsNull(CombatDirector.Current, "꺼지면 등록을 지운다");
        Assert.IsNotNull(flow);
    }

    /// <summary>담당이 둘이면(전투 씬이 두 번 뜬 전환 버그) 뒤엣것은 오류를 남기고 꺼진다 — 처치·사망을 두 번 처리하지 않게.</summary>
    [UnityTest]
    public IEnumerator SecondDirector_IsRefused()
    {
        var flow = TestFlow.Create();
        var first = CombatDirector.Current;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("전투 담당이 둘"));
        var second = new GameObject("TestCombatDirector2").AddComponent<CombatDirector>();
        yield return null;

        Assert.AreSame(first, CombatDirector.Current, "먼저 뜬 담당이 그대로다");
        Assert.IsFalse(second.enabled, "뒤엣것은 꺼진다");
        Object.DestroyImmediate(second.gameObject);
        Assert.AreSame(first, CombatDirector.Current, "꺼진 쪽이 없어져도 먼저 뜬 담당의 등록은 남는다");
        Assert.IsNotNull(flow);
    }

    /// <summary>담당이 꺼지면(전투 씬이 내려가면) 전투 사건이 더 닿지 않는다 — 정적 이벤트 구독이 대칭으로 풀린다.</summary>
    [UnityTest]
    public IEnumerator DisabledDirector_NoLongerEndsTheRun()
    {
        var flow = TestFlow.Create();
        yield return null;
        flow.StartRun(1, RunMode.Normal);
        var director = CombatDirector.Current;

        director.enabled = false;
        CombatEvents.RaisePlayerDied();
        Assert.AreEqual(GameScene.Run, GameState.Current, "꺼진 담당이 사망 이벤트를 받아 런을 끝냈다(구독이 안 풀림)");

        director.enabled = true;
        CombatEvents.RaisePlayerDied();
        Assert.AreEqual(GameScene.Result, GameState.Current, "다시 켜면 받는다");
        Assert.AreEqual(RunEndReason.Dead, flow.LastEndReason);
    }

    /// <summary>
    /// 전투 씬이 없을 때(부팅 첫 프레임 — 월드는 `GameBoot`가 겹쳐 부르는 중) 타이틀·로비는 그대로 가고,
    /// 사냥 시작은 하지 않고 오류를 남긴다(조용히 빈 필드로 들어가지 않게).
    /// </summary>
    [UnityTest]
    public IEnumerator WithoutCombatScene_TitleAndLobbyWork_ButRunDoesNotStart()
    {
        var flow = new GameObject("TestGameFlow").AddComponent<GameFlow>();
        flow.Scenes = new FakeWorldScenes(); // 씬 '불러오기'는 성공하지만 그 씬에 전투 담당이 없는 상황
        yield return null;
        Assert.AreEqual(GameScene.Title, GameState.Current);

        flow.EnterLobby();
        Assert.AreEqual(GameScene.Lobby, GameState.Current);

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("전투 담당"));
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("화면 전환 실패"));
        bool? entered = null;
        flow.StartRun(1, RunMode.Normal, ok => entered = ok);
        Assert.AreEqual(false, entered, "실패를 알려야 한다");
        Assert.AreEqual(GameScene.Lobby, GameState.Current, "전투 씬이 없는데 사냥으로 넘어갔다");
        Assert.AreEqual(0f, Time.timeScale);
    }

    /// <summary>보스 격파 뒤 지연(1.6초) 동안엔 런 타이머가 안 흘러서, 시간이 다 돼도 '시간 초과'로 뒤집히지 않는다(원본 :4289).</summary>
    [UnityTest]
    public IEnumerator BossDeadDelay_IsNotOverriddenByTimeout()
    {
        var flow = TestFlow.Create();
        yield return null;
        flow.StartRun(1, RunMode.Normal);
        CombatDirector.Current.EnterBossField();

        RunEvents.RaiseBossDefeated();
        RunState.Tick(RunState.TimeLeft - 0.01f); // 거의 다 쓴 상태 — 지연 중에 Tick이 돌면 시간 초과가 먼저 난다
        yield return new WaitForSecondsRealtime(CombatDirector.BossDeadEndDelay + 0.3f);

        Assert.AreEqual(GameScene.Result, GameState.Current);
        Assert.AreEqual(RunEndReason.BossDead, flow.LastEndReason, "보스를 잡았는데 시간 초과로 끝났다");
    }

    /// <summary>
    /// 시간이 멈춘 동안(히트스톱) 런 타이머·콤보는 그대로다 — 예전 흐름은 멈춘 동안 아예 Tick을 안 불렀고, 지금 담당은 0초로 부른다.
    /// 둘이 같은 결과인지(0을 빼는 호출이 아무것도 안 바꾸는지) 확인한다.
    /// </summary>
    [UnityTest]
    public IEnumerator WhileFrozen_TimersAndComboDoNotMove()
    {
        var flow = TestFlow.Create();
        yield return null;
        flow.StartRun(1, RunMode.Normal);
        CombatModifiers.AddCombo();
        flow.Hitstop(1f); // 길게 멈춘다
        float timeLeft = RunState.TimeLeft, comboLeft = CombatModifiers.ComboTimeLeft;
        for (int i = 0; i < 5; i++) yield return null;

        Assert.AreEqual(0f, Time.timeScale, "아직 멈춰 있다");
        Assert.AreEqual(timeLeft, RunState.TimeLeft, "멈춘 동안 런 타이머가 흘렀다");
        Assert.AreEqual(comboLeft, CombatModifiers.ComboTimeLeft, "멈춘 동안 콤보 창이 줄었다");
        Assert.AreEqual(1, CombatModifiers.Combo);
    }

    /// <summary>
    /// 보스 처치 멈춤(0.25초)은 격파 지연(1.6초) 중에도 풀린다 — 원본은 멈춤(`state.freeze`, :1847)이 끝나면 결과 화면(:4289)까지 월드가 계속 움직인다.
    /// 예전엔 흐름 하나가 격파 지연 중 히트스톱 만료 처리까지 건너뛰어, 결과 화면이 뜰 때까지 1.6초 내내 멈춰 있었다(2026-11-05 분리하며 원본대로).
    /// </summary>
    [UnityTest]
    public IEnumerator BossKillHitstop_EndsDuringTheDelay_LikeTheOriginal()
    {
        var flow = TestFlow.Create();
        yield return null;
        flow.StartRun(1, RunMode.Normal);
        CombatDirector.Current.EnterBossField();

        // 실제 보스 사망과 같은 두 알림 — 처치 연출은 담당(KillFeel) → 히트스톱 요청(CombatEvents) → 흐름(GameFlow.Hitstop) 경로를 탄다.
        var boss = new GameObject("TestBoss");
        boss.AddComponent<Boss>().enabled = false; // "보스인 적"이라는 표식만 쓴다
        boss.GetComponent<EnemyMove>().enabled = false;
        RunEvents.RaiseBossDefeated();
        CombatEvents.RaiseEnemyKilled(boss);
        Assert.AreEqual(0f, Time.timeScale, "처치 순간엔 멈춘다(담당 → 이벤트 → 흐름)");

        yield return new WaitForSecondsRealtime(HitFeel.HitstopBossKill + 0.2f);
        Assert.AreEqual(GameScene.Run, GameState.Current, "아직 결과 화면 전이다");
        Assert.AreEqual(1f, Time.timeScale, "멈춤이 끝나면 결과 화면까지 월드가 움직인다");
        yield return null;
        Assert.Greater(Time.deltaTime, 0f, "실제로 게임 시간이 흐른다(몹·투사체가 움직인다)");

        yield return new WaitForSecondsRealtime(CombatDirector.BossDeadEndDelay);
        Assert.AreEqual(GameScene.Result, GameState.Current, "지연이 끝나면 결과 화면");
    }
}

}
