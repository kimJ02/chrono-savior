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

    /// <summary>런 오브젝트 흉내 — 표식만 있으면 `RunTransient.DestroyAllIn`이 지워야 한다.</summary>
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

    /// <summary>
    /// 원본 `toLobby()`(:4382) — 로비로. 시간은 멈춘 채로 둔다. 로비 씬을 불러오고 전투 씬을 내린다.
    /// (필드가 비는 건 전투 씬째 내려가기 때문 — 진짜 씬으로 `GameBootTests`가 본다.)
    /// </summary>
    [UnityTest]
    public IEnumerator EnterLobby_LoadsLobbyUnloadsCombatAndFreezes()
    {
        var rc = NewController();
        var scenes = (FakeWorldScenes)rc.Scenes;
        yield return null;
        rc.StartRun(1, RunMode.Normal);
        var combat = rc.CurrentWorld;
        Assert.AreEqual(GameFlow.CombatScene, combat.SceneName);

        bool? entered = null;
        rc.EnterLobby(ok => entered = ok);

        Assert.AreEqual(true, entered);
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.AreEqual(GameFlow.LobbyScene, rc.CurrentWorld.SceneName);
        Assert.AreEqual(rc.CurrentWorld.Id, scenes.Active.Id, "새 월드가 활성 씬");
        Assert.IsTrue(scenes.Unloaded.Exists(w => w.Id == combat.Id), "전투 씬을 내린다");
        Assert.IsFalse(rc.IsTransitioning);
    }

    // ────────────────────────── 화면 전환 (2026-11-05 씬 분할 2차 3-4) ──────────────────────────

    /// <summary>불러오기가 실패하면 바꾸기 전 화면·시간·월드로 되돌린다 — 날짜도 안 줄고, 이전 월드는 다시 켜지고 활성 씬으로.</summary>
    [UnityTest]
    public IEnumerator FailedLoad_RestoresPreviousScreenAndWorld()
    {
        var rc = NewController();
        var scenes = (FakeWorldScenes)rc.Scenes;
        yield return null;
        rc.EnterLobby();
        var lobby = rc.CurrentWorld;
        int daysBefore = ProfileService.Current.doomDays;

        scenes.FailNext = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("화면 전환 실패"));
        bool? entered = null;
        Assert.IsTrue(rc.StartRun(1, RunMode.Normal, ok => entered = ok), "요청은 받아들인다");

        Assert.AreEqual(false, entered);
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.AreEqual(daysBefore, ProfileService.Current.doomDays, "들어가지 못했는데 날짜가 줄었다");
        Assert.AreEqual(lobby.Id, rc.CurrentWorld.Id);
        Assert.AreEqual(lobby.Id, scenes.Active.Id, "이전 월드가 다시 활성 씬");
        var last = scenes.Running[scenes.Running.Count - 1];
        Assert.IsTrue(last.world.Id == lobby.Id && last.running, "이전 월드를 다시 켠다");
        Assert.IsFalse(scenes.Unloaded.Exists(w => w.Id == lobby.Id), "이전 월드를 내리지 않는다");
        Assert.IsFalse(rc.IsTransitioning);
    }

    /// <summary>불러오기가 예외를 던져도 실패로 처리한다 — 잠금이 풀리고, 이전 화면·월드로 돌아온다(Codex 3-4 리뷰).</summary>
    [UnityTest]
    public IEnumerator LoaderException_IsAFailure_AndReleasesTheLock()
    {
        var rc = NewController();
        var scenes = (FakeWorldScenes)rc.Scenes;
        yield return null;
        rc.EnterLobby();
        var lobby = rc.CurrentWorld;

        scenes.ThrowNext = true;
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException"));
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("화면 전환 실패"));
        bool? entered = null;
        rc.StartRun(1, RunMode.Normal, ok => entered = ok);

        Assert.AreEqual(false, entered);
        Assert.IsFalse(rc.IsTransitioning, "예외 뒤에도 잠금이 풀린다");
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.AreEqual(lobby.Id, rc.CurrentWorld.Id);
        Assert.AreEqual(lobby.Id, scenes.Active.Id);
    }

    /// <summary>옛 월드를 못 내려도 새 화면은 그대로이고, 그 월드는 다음 전환에 다시 내린다(꺼진 채 계속 쌓이지 않게, Codex 3-4 재리뷰).</summary>
    [UnityTest]
    public IEnumerator FailedUnload_IsRetriedOnTheNextTransition()
    {
        var rc = NewController();
        var scenes = (FakeWorldScenes)rc.Scenes;
        yield return null;
        rc.EnterLobby();
        var lobby = rc.CurrentWorld;

        scenes.FailUnloadNext = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("내리지 못했다"));
        bool? entered = null;
        rc.StartRun(1, RunMode.Normal, ok => entered = ok);
        Assert.AreEqual(true, entered, "새 화면은 떠 있다");
        Assert.AreEqual(GameScene.Run, GameState.Current);
        Assert.IsFalse(scenes.Unloaded.Exists(w => w.Id == lobby.Id));

        rc.EnterLobby();
        Assert.IsTrue(scenes.Unloaded.Exists(w => w.Id == lobby.Id), "남은 로비 월드를 다음 전환에 내린다");
    }

    /// <summary>새 월드에 들어가지 못하면 그 월드부터 멈춘다 — 내리기가 늦거나 실패해도 두 월드가 같이 켜져 있지 않게.</summary>
    [UnityTest]
    public IEnumerator FailedEnter_StopsTheFreshWorldBeforeRestoringTheOld()
    {
        var rc = new GameObject("TestGameFlow").AddComponent<GameFlow>(); // 전투 담당 없음 → 들어가기 실패
        var scenes = new FakeWorldScenes();
        rc.Scenes = scenes;
        yield return null;
        rc.EnterLobby();
        var lobby = rc.CurrentWorld;

        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("전투 담당"));
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("화면 전환 실패"));
        rc.StartRun(1, RunMode.Normal);

        int stopFresh = scenes.Running.FindIndex(r => r.world.SceneName == GameFlow.CombatScene && !r.running);
        int resumeOld = scenes.Running.FindLastIndex(r => r.world.Id == lobby.Id && r.running);
        Assert.GreaterOrEqual(stopFresh, 0, "새 월드를 멈춘다");
        Assert.Greater(resumeOld, stopFresh, "새 월드를 멈춘 뒤에 이전 월드를 켠다");
    }

    /// <summary>필드 정리는 그 씬 것만 — 재도전 중 멈춰 둔 옛 전투 씬의 런 오브젝트는 새 씬의 필드 준비가 지우지 않는다(Codex 3단계 종합 리뷰).</summary>
    [UnityTest]
    public IEnumerator DestroyAllIn_OnlyClearsThatScene()
    {
        var other = UnityEngine.SceneManagement.SceneManager.CreateScene("TestOtherWorld");
        var here = NewTransient();
        var there = NewTransient();
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(there, other);

        RunTransient.DestroyAllIn(here.scene);
        yield return null;
        Assert.IsTrue(here == null, "이 씬 것은 지운다");
        Assert.IsTrue(there != null, "다른 씬 것은 남는다");
        Assert.AreEqual(1, RunTransient.ActiveCount);
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(other);
        Assert.AreEqual(0, RunTransient.ActiveCount, "그 씬이 내려가면 같이 빠진다");
    }

    /// <summary>필드 준비에서 예외를 던지는 플레이어 부품(테스트용).</summary>
    sealed class ThrowingReset : MonoBehaviour, IRunResettable
    {
        public bool Armed;
        public void ResetForRun() { if (Armed) throw new System.InvalidOperationException("테스트: 필드 준비 실패"); }
    }

    /// <summary>
    /// 재도전에서 필드 준비가 예외로 실패하면 결과 화면으로 돌아오고, **날짜와 지난 런 기록이 그대로**다(Codex 3단계 종합 리뷰).
    /// </summary>
    [UnityTest]
    public IEnumerator FieldSetupException_RestoresDaysAndThePreviousRun()
    {
        var player = new GameObject("TestPlayer");
        var trap = player.AddComponent<ThrowingReset>();
        var rc = NewController(player);
        yield return null;
        rc.StartRun(2, RunMode.Normal);
        RunState.RegisterKill(false);
        RunState.RegisterReward(50, 7);
        RunState.NoteCombo(9);
        rc.EndRun(RunEndReason.Timeout);
        int days = ProfileService.Current.doomDays;

        trap.Armed = true;
        LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("InvalidOperationException"));
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("화면 전환 실패"));
        bool? entered = null;
        rc.StartRun(1, RunMode.Normal, ok => entered = ok);

        Assert.AreEqual(false, entered);
        Assert.AreEqual(GameScene.Result, GameState.Current);
        Assert.AreEqual(days, ProfileService.Current.doomDays, "들어가지 못했는데 날짜가 줄었다");
        Assert.AreEqual(2, RunState.Region, "지난 런 기록이 그대로");
        Assert.AreEqual(1, RunState.Kills);
        Assert.AreEqual(50, RunState.GoldEarned);
        Assert.AreEqual(9, RunState.MaxCombo, "결과 화면의 지난 원정 최고 콤보가 지워졌다");
        Assert.IsTrue(RunState.Over);
    }

    /// <summary>"새 원정"은 로비 씬이 뜬 뒤에만 세이브를 지운다 — 불러오기가 실패하면 진행이 그대로 남는다.</summary>
    [UnityTest]
    public IEnumerator NewExpedition_FailedLoad_KeepsSave()
    {
        var rc = NewController();
        var scenes = (FakeWorldScenes)rc.Scenes;
        yield return null;
        ProfileService.Current.gold = 123;
        SaveService.Save();
        Assert.IsTrue(SaveService.HasSave);

        scenes.FailNext = true;
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("화면 전환 실패"));
        rc.StartNewExpedition();

        Assert.IsTrue(SaveService.HasSave, "실패했는데 세이브가 지워졌다");
        Assert.AreEqual(123, ProfileService.Current.gold);
        Assert.AreEqual(GameScene.Title, GameState.Current);
    }

    /// <summary>옮기는 중엔 다른 전환·일시정지·종료 요청을 받지 않는다 — 끝나면 다시 받는다.</summary>
    [UnityTest]
    public IEnumerator WhileTransitioning_OtherRequestsAreRefused()
    {
        var rc = NewController();
        var scenes = (FakeWorldScenes)rc.Scenes;
        yield return null;
        rc.StartRun(1, RunMode.Normal);
        Assert.AreEqual(GameScene.Run, GameState.Current);

        scenes.HoldNext = true;
        Assert.IsTrue(rc.EnterLobby());
        Assert.IsTrue(rc.IsTransitioning);
        Assert.IsTrue(GameState.IsTransitioning);

        Assert.IsFalse(rc.StartRun(1, RunMode.Normal), "전환 중 사냥 시작을 받았다");
        Assert.IsFalse(rc.EnterTitle(), "전환 중 타이틀을 받았다");
        rc.Pause();
        Assert.AreEqual(GameScene.Run, GameState.Current, "전환 중 일시정지가 걸렸다");
        rc.EndRun(RunEndReason.Dead);
        Assert.IsFalse(RunState.Over, "전환 중 런 종료를 받았다");

        scenes.Release();
        yield return null; // 전환 코루틴이 끝난 불러오기를 본다
        Assert.IsFalse(rc.IsTransitioning);
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.IsTrue(rc.StartRun(1, RunMode.Normal), "전환이 끝나면 다시 받는다");
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
