using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 게임 뼈대(로비↔사냥↔일시정지↔결과) 검증 — 원본 `startRun`/`pauseRun`/`resumeRun`/`endRun`/`toLobby`
/// (project_test.html:4293·:4332·:4346·:4354·:4382).
///
/// ⚠️ **`Time.timeScale`을 반드시 되돌려야 한다.** `GameFlow`는 사냥이 아닐 때 `timeScale = 0`으로
/// 게임을 멈추는데(확정 설계), 테스트가 0인 채로 끝나면 **그 뒤에 도는 다른 테스트의
/// `WaitForSeconds`가 영원히 안 끝난다** — 스위트 전체가 멈춘다. TearDown에서 1로 복구한다.
/// </summary>
public class GameFlowTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f; // 위 주석 참고 — 이걸 빠뜨리면 스위트가 멈춘다
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        }
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
    }

    /// <summary>게임 흐름 + 전투 담당(`TestFlow`). 플레이어 리셋을 보는 테스트는 그 플레이어를 넘긴다(실제 씬에선 직렬화 참조).</summary>
    static GameFlow NewController(GameObject player = null) => TestFlow.Create(player);

    /// <summary>런 오브젝트 흉내 — 표식만 있으면 `RunTransient.DestroyAll()`이 지워야 한다.</summary>
    static GameObject NewTransient()
    {
        var go = new GameObject("TestTransient");
        go.AddComponent<RunTransient>();
        return go;
    }

    /// <summary>타이틀 "새 원정" — 세이브를 지우고 새 프로필로 로비에(2026-11-05, 예전엔 TitleMenu가 직접 했다). 진짜 세이브는 안 건드린다.</summary>
    [Test]
    public void StartNewExpedition_ClearsTheSave_AndEntersTheLobby()
    {
        string temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"ChronoSavior_GameFlowTests_{System.Guid.NewGuid():N}.json");
        SaveService.PathOverride = temp;
        try
        {
            ProfileService.Current.gold = 777;
            Assert.IsTrue(SaveService.Save());
            var flow = NewController();

            flow.StartNewExpedition();

            Assert.IsFalse(SaveService.HasSave, "세이브가 지워진다");
            Assert.AreEqual(0, ProfileService.Current.gold, "새 프로필");
            Assert.AreEqual(GameScene.Lobby, GameState.Current);
        }
        finally
        {
            SaveService.PathOverride = TestSaveSandbox.Path;
            if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp);
        }
    }

    // ────────────────────────── 시작 ──────────────────────────

    /// <summary>
    /// 원본 `startRun`(:4293) — 런 상태가 초기화되고 화면이 사냥으로 바뀌고 시간이 다시 흐른다.
    /// </summary>
    [UnityTest]
    public IEnumerator StartRun_BeginsRunState_AndUnfreezesTime()
    {
        var rc = NewController();
        yield return null; // Start()가 EnterTitle()로 타이틀에 넣는다

        // 원본은 로비에서 시작하지만(`:1466`) 디자인에 타이틀 화면이 생겨(Figma 35:2) 그쪽이 먼저다.
        Assert.AreEqual(GameScene.Title, GameState.Current, "타이틀 화면에서 시작해야 한다");
        Assert.AreEqual(0f, Time.timeScale, "타이틀에선 게임이 멈춰 있어야 한다");

        rc.StartRun(3, RunMode.Normal);

        Assert.AreEqual(GameScene.Run, GameState.Current);
        Assert.AreEqual(1f, Time.timeScale, "사냥 중엔 시간이 흘러야 한다");
        Assert.AreEqual(3, RunState.Region);
        Assert.AreEqual(RunState.NormalTime, RunState.TimeLeft, 0.01f, "일반 사냥 제한시간 90초(:695)");
        Assert.IsFalse(RunState.Over);
        Assert.AreEqual(0, RunState.Kills);
    }

    /// <summary>
    /// 원본 `startRun`의 `enemies = []; projectiles = []; zones = []`(:4318) —
    /// 지난 런의 잔해가 남아 있으면 로비에 갔다 와도 몹이 그대로 서 있는 게임이 된다.
    /// </summary>
    [UnityTest]
    public IEnumerator StartRun_ClearsLeftoverFieldObjects()
    {
        var rc = NewController();
        yield return null;

        NewTransient();
        NewTransient();
        Assert.AreEqual(2, RunTransient.ActiveCount);

        rc.StartRun(1, RunMode.Normal);
        Assert.AreEqual(0, RunTransient.ActiveCount, "새 사냥을 시작했는데 지난 런의 오브젝트가 남았다");
    }

    /// <summary>
    /// 원본 `resetPlayerForRun()`(:1511) — 플레이어가 시작 위치·풀피로 되돌아간다.
    /// `IRunResettable`만 보고 부르므로 섬영·드루이드 키트도 구현만 하면 자동으로 걸린다.
    /// </summary>
    [UnityTest]
    public IEnumerator StartRun_ResetsPlayer_ToStartPositionAndFullHp()
    {
        var player = new GameObject("TestPlayer");
        player.tag = "Player";
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        player.AddComponent<CircleCollider2D>().radius = 0.5f;
        var health = player.AddComponent<PlayerHealth>();
        player.AddComponent<CharacterMover2D>();
        player.transform.position = new Vector3(18f, 4f, 0f); // 엉뚱한 자리로 옮겨두고
        yield return null;

        health.TakeDamage(20f, null);
        Assert.Less(health.CurrentHp, health.MaxHp);

        var rc = NewController(player);
        yield return null;
        rc.StartRun(1, RunMode.Normal);
        yield return null;

        Assert.AreEqual(CharacterMover2D.RunStartX, player.transform.position.x, 0.01f,
            "원본 시작 위치 220px(=2.2유닛)로 돌아와야 한다(:1513)");
        Assert.AreEqual(health.MaxHp, health.CurrentHp, 0.01f, "런 시작인데 풀피가 아니다");
    }

    // ────────────────────────── 일시정지 ──────────────────────────

    /// <summary>
    /// 원본 `pauseRun`/`resumeRun`(:4332·:4346). ESC가 **로비로 나가는 키가 아니라** 일시정지라는 게
    /// 핵심이다 — 로비로는 일시정지 화면에서 한 단계 더 거쳐서 간다(:1027).
    /// </summary>
    [UnityTest]
    public IEnumerator PauseAndResume_TogglesSceneAndTime()
    {
        var rc = NewController();
        yield return null;
        rc.StartRun(1, RunMode.Normal);

        rc.Pause();
        Assert.AreEqual(GameScene.Pause, GameState.Current);
        Assert.AreEqual(0f, Time.timeScale, "일시정지인데 시간이 흐른다");

        rc.Resume();
        Assert.AreEqual(GameScene.Run, GameState.Current);
        Assert.AreEqual(1f, Time.timeScale);
    }

    /// <summary>
    /// 원본 `pauseRun`의 `if (run.over || state.scene !== 'run') return`.
    /// 시작 화면이 로비에서 타이틀로 바뀌었어도(Figma 35:2) 지켜야 할 규칙은 같다:
    /// **사냥 중이 아닌 화면에서 ESC를 눌러도 화면이 바뀌지 않는다.**
    /// </summary>
    [UnityTest]
    public IEnumerator Pause_DoesNothingOutsideRun()
    {
        var rc = NewController();
        yield return null;

        var before = GameState.Current;
        Assert.AreNotEqual(GameScene.Run, before, "이 테스트는 사냥이 아닌 화면에서 시작해야 한다");

        rc.Pause();
        Assert.AreEqual(before, GameState.Current, "사냥이 아닌데 ESC로 일시정지로 갔다");

        // 로비에서도 같은지 확인한다(예전 테스트가 보던 상황).
        rc.EnterLobby();
        rc.Pause();
        Assert.AreEqual(GameScene.Lobby, GameState.Current, "로비에서 ESC를 눌러도 일시정지로 가면 안 된다");
    }

    // ────────────────────────── 종료 ──────────────────────────

    /// <summary>
    /// 원본은 플레이어 체력이 0이 되는 그 자리에서 `endRun('dead')`를 부른다(:1927).
    /// 이 경로가 끊기면 죽어도 게임이 안 끝난다(예전 R키 부활이 가리고 있던 문제).
    /// </summary>
    [UnityTest]
    public IEnumerator PlayerDeath_EndsRunWithDeadReason()
    {
        var player = new GameObject("TestPlayer");
        player.tag = "Player";
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        player.AddComponent<CircleCollider2D>().radius = 0.5f;
        var health = player.AddComponent<PlayerHealth>();
        player.AddComponent<CharacterMover2D>();

        var rc = NewController();
        yield return null;
        rc.StartRun(1, RunMode.Normal);
        yield return null;

        health.TakeDamage(99999f, null);
        yield return null;

        Assert.AreEqual(GameScene.Result, GameState.Current, "죽었는데 결과 화면으로 안 갔다");
        Assert.AreEqual(RunEndReason.Dead, rc.LastEndReason);
        Assert.IsTrue(RunState.Over);
        Assert.AreEqual(0f, Time.timeScale);
    }

    /// <summary>원본 `if (run.timeLeft <= 0) endRun('timeout')`(:4413).</summary>
    [UnityTest]
    public IEnumerator TimeRunningOut_EndsRunWithTimeout()
    {
        var rc = NewController();
        yield return null;
        rc.StartRun(1, RunMode.Normal);

        // 90초를 실제로 기다릴 순 없으니 남은 시간을 직접 태운다(RunState.Tick은 순수 계산이다).
        RunState.Tick(RunState.NormalTime + 1f);
        yield return null; // CombatDirector.Update가 0이 된 걸 보고 종료를 요청한다

        Assert.AreEqual(GameScene.Result, GameState.Current);
        Assert.AreEqual(RunEndReason.Timeout, rc.LastEndReason);
    }

    /// <summary>
    /// 원본 `endRun`의 첫 줄 `if (run.over) return`(:4355) — 시간 종료와 사망이 같은 프레임에 겹쳐도
    /// 결과가 두 번 뜨지 않는다.
    /// </summary>
    [UnityTest]
    public IEnumerator EndRun_IsIdempotent()
    {
        var rc = NewController();
        yield return null;
        rc.StartRun(1, RunMode.Normal);

        rc.EndRun(RunEndReason.Timeout);
        rc.EndRun(RunEndReason.Dead); // 두 번째는 무시돼야 한다

        Assert.AreEqual(RunEndReason.Timeout, rc.LastEndReason, "이미 끝난 런의 사유가 덮어써졌다");
    }

    // ────────────────────────── 로비 복귀 ──────────────────────────

    /// <summary>원본 `toLobby()`(:4382) — 필드를 비우고 로비로. 시간은 멈춘 채로 둔다.</summary>
    [UnityTest]
    public IEnumerator EnterLobby_ClearsFieldAndFreezes()
    {
        var rc = NewController();
        yield return null;
        rc.StartRun(1, RunMode.Normal);
        NewTransient();

        rc.EnterLobby();

        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.AreEqual(0, RunTransient.ActiveCount, "로비로 나왔는데 필드에 오브젝트가 남았다");
    }

    /// <summary>
    /// `timeScale = 0`이 실제로 "게임이 멈춘다"를 보장하는지 — 이게 성립해야 적·스폰너에
    /// `IsRunning` 가드를 하나도 안 넣어도 된다(확정 설계의 근거).
    /// </summary>
    [UnityTest]
    public IEnumerator Lobby_FreezesGameplayDeltaTime()
    {
        var rc = NewController();
        yield return null;
        rc.EnterLobby();

        yield return null;
        Assert.AreEqual(0f, Time.deltaTime, 1e-6f,
            "로비에서 Time.deltaTime이 0이 아니면 몹 스폰 타이머가 계속 돈다");
    }
}

}
