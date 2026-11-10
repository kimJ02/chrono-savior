using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 실제 게임 씬 통합 테스트 — `00_Persistent`를 부팅하고, **진짜 씬 전환**(타이틀 → `10_Lobby` → `20_Combat` → 결과 → 재도전 → 로비)을 돌린다
/// (씬 분할 1차 2026-11-04, 2차 3-4 2026-11-05에 전환 기준으로 다시 씀). 다른 테스트는 빈 씬에 컴포넌트를 직접 만들고 가짜 로더를 쓰지만,
/// 이건 씬 파일 자체가 맞게 구성됐는지·불러오고 내리는 순서가 맞는지를 본다.
///
/// ⚠️ `GameBoot`는 세이브를 읽고, 화면이 바뀌면 `AutoSave`가 쓴다 — 개발자의 진짜 세이브를 건드리지 않게 **테스트 전용 경로**
/// (`SaveService.PathOverride`, 임시 폴더)를 쓴다. 테스트가 중간에 죽어도 진짜 세이브는 안전하다. 늘 새 프로필로 시작한다.
/// </summary>
public class GameBootTests
{
    string tempSave;

    const string PersistentScene = GameFlow.PersistentScene;
    static readonly string[] WorldScenes = { GameFlow.LobbyScene, GameFlow.CombatScene };

    [UnitySetUp]
    public IEnumerator Setup()
    {
        tempSave = Path.Combine(Path.GetTempPath(), $"ChronoSavior_GameBootTests_{System.Guid.NewGuid():N}.json"); // 실행마다 다른 파일
        SaveService.PathOverride = tempSave;
        ResetStatics();
        // 기본값과 다른 세이브를 둔다 — 부팅이 '다른 컴포넌트가 프로필을 읽기 전에' 이걸 불러오는지 보려고.
        ProfileService.Current.character = SavedCharacter;
        ProfileService.Current.gold = SavedGold;
        Assert.IsTrue(SaveService.Save());
        ProfileService.Reset(); // 메모리는 기본값으로 — 부팅이 파일에서 다시 읽어야 위 값이 된다

        yield return SceneManager.LoadSceneAsync(PersistentScene, LoadSceneMode.Additive);
        yield return null; // GameFlow.Start(타이틀)까지 한 프레임
    }

    [UnityTearDown]
    public IEnumerator Teardown()
    {
        // 경로·시간부터 되돌린다 — 아래 씬 내리기가 실패해도 다음 테스트가 테스트용 경로를 물려받지 않게
        SaveService.PathOverride = TestSaveSandbox.Path; // 테스트 전체 임시 경로로 되돌린다(null이면 진짜 경로)
        Time.timeScale = 1f;
        try { if (File.Exists(tempSave)) File.Delete(tempSave); } catch (IOException) { }
        // 상주 씬(흐름)을 먼저 내려 진행 중인 전환을 멈춘 뒤, 남은 월드 씬을 전부(재도전 중이면 같은 이름 둘) 내린다.
        var persistent = SceneManager.GetSceneByName(PersistentScene);
        if (persistent.isLoaded) yield return SceneManager.UnloadSceneAsync(persistent);
        foreach (var scene in Loaded(WorldScenes)) if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
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
        FieldLayout.SetBossArena(false);
    }

    const CharacterId SavedCharacter = CharacterId.Gunner; // 기본값(마법사)과 다르게
    const int SavedGold = 1234;

    static GameFlow Flow => PixelCanvas.Instance.Flow;

    /// <summary>지금 불러와 있는 그 이름의 씬들(같은 이름이 둘일 수 있다 — 재도전 중).</summary>
    static List<Scene> Loaded(params string[] names)
    {
        var list = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && names.Contains(s.name)) list.Add(s);
        }
        return list;
    }

    static T[] InScene<T>(string scene) where T : Component =>
        Loaded(scene).SelectMany(s => s.GetRootGameObjects()).SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    /// <summary>전환을 요청하고 끝날 때까지(최대 10초) 기다린다 — 결과가 <paramref name="expect"/>여야 한다.</summary>
    static IEnumerator Go(System.Func<System.Action<bool>, bool> begin, bool expect = true)
    {
        bool? result = null;
        Assert.IsTrue(begin(ok => result = ok), "전환 요청을 받아들인다");
        float until = Time.realtimeSinceStartup + 10f;
        while (result == null && Time.realtimeSinceStartup < until) yield return null;
        Assert.AreEqual(expect, result, result == null ? "10초 안에 전환이 안 끝났다" : "전환 결과가 다르다");
        Assert.IsFalse(Flow.IsTransitioning);
        yield return null; // 판이 다음 프레임에 입력을 다시 연다
        Assert.IsTrue(PixelCanvas.Instance.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled, "전환이 끝나면(성공·실패 모두) 화면이 다시 클릭을 받는다");
    }

    static IEnumerator ToLobby() => Go(d => Flow.EnterLobby(d));
    static IEnumerator ToRun() => Go(d => Flow.StartRun(1, RunMode.Normal, d));

    // ───────────────────────── 부팅 ─────────────────────────

    [Test]
    public void Boot_LoadsTheSave_AndUsesTheTestSavePath()
    {
        Assert.AreEqual(SavedGold, ProfileService.Current.gold, "GameBoot가 세이브를 불러왔다");
        Assert.AreEqual(tempSave, SaveService.SavePath, "이 테스트는 진짜 세이브 위치를 쓰면 안 된다");
    }

    [Test]
    public void Boot_StartsAtTitle_WithNoWorld_AndOneOfEachSharedObject()
    {
        Assert.AreEqual(GameScene.Title, GameState.Current, "시작 화면은 타이틀");
        Assert.IsTrue(Flow.CurrentWorld.IsNone, "타이틀엔 월드가 없다");
        Assert.IsEmpty(Loaded(WorldScenes), "부팅이 월드 씬을 불러오지 않는다(타이틀 → 로비·사냥으로 갈 때 GameFlow가 부른다)");
        Assert.AreEqual(1, Object.FindObjectsByType<PixelCanvas>(FindObjectsSortMode.None).Length, "UI 판은 하나(자동 생성이 겹치지 않음)");
        Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, "EventSystem은 하나");
        Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, "카메라는 상주 씬의 하나뿐");
        Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, "AudioListener는 하나");
        Assert.IsNotNull(Flow, "UI 판이 같은 씬의 GameFlow에 연결돼 있다(화면이 씬을 뒤지지 않는다)");
        Assert.AreEqual(Color.black, Camera.main.backgroundColor, "타이틀엔 월드가 없으니 카메라는 검게 지운다(카메라를 끄지 않는다)");
    }

    // ───────────────────────── 전환 한 바퀴 ─────────────────────────

    /// <summary>
    /// 타이틀 → 로비 → 사냥 → 결과 → 재도전 → 로비. 매번 **새 월드 하나만** 떠 있고 그게 활성 씬이다(새 오브젝트가 그 월드에 생긴다),
    /// 이전 월드는 내려간다. 재도전은 같은 이름의 전투 씬을 새로 불러오고 옛 것을 내린다.
    /// </summary>
    [UnityTest]
    public IEnumerator Flow_TitleLobbyRunResultRetryLobby_KeepsExactlyOneWorld()
    {
        yield return ToLobby();
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.AreEqual(0f, Time.timeScale);
        AssertOnlyWorld(GameFlow.LobbyScene);
        Assert.IsNotNull(WorldView.Lobby, "로비 월드가 화면 창구에 꽂혀 있다");
        Assert.IsNotNull(WorldView.Lobby.AvatarPosition, "로비에서 걷는 캐릭터 위치가 보인다");
        Assert.IsNull(CombatDirector.Current, "로비엔 전투 담당이 없다");

        yield return ToRun();
        Assert.AreEqual(GameScene.Run, GameState.Current);
        Assert.AreEqual(1f, Time.timeScale);
        AssertOnlyWorld(GameFlow.CombatScene);
        Assert.IsNull(WorldView.Lobby, "로비 씬이 내려가면 로비 창구도 빈다");
        Assert.AreSame(InScene<CombatDirector>(GameFlow.CombatScene).Single(), CombatDirector.Current);
        var firstCombat = Loaded(GameFlow.CombatScene).Single();

        Flow.EndRun(RunEndReason.Timeout);
        Assert.AreEqual(GameScene.Result, GameState.Current);

        yield return ToRun(); // 결과 화면의 "다시 원정"
        Assert.AreEqual(GameScene.Run, GameState.Current);
        AssertOnlyWorld(GameFlow.CombatScene);
        Assert.AreNotEqual(firstCombat, Loaded(GameFlow.CombatScene).Single(), "재도전은 전투 씬을 새로 불러온다");
        Assert.IsFalse(RunState.Over, "새 런이다");

        yield return ToLobby();
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        AssertOnlyWorld(GameFlow.LobbyScene);
        Assert.IsNull(CombatDirector.Current, "전투 씬이 내려가면 담당 등록도 빠진다");
    }

    static void AssertOnlyWorld(string name)
    {
        var worlds = Loaded(WorldScenes);
        Assert.AreEqual(1, worlds.Count, "월드 씬은 하나만 떠 있다 — 지금: " + string.Join(", ", worlds.Select(w => w.name)));
        Assert.AreEqual(name, worlds[0].name);
        Assert.AreEqual(worlds[0], SceneManager.GetActiveScene(), "지금 월드가 활성 씬");
        Assert.AreEqual(name, Flow.CurrentWorld.SceneName);
        var probe = new GameObject("TestBootProbe");
        Assert.AreEqual(worlds[0], probe.scene, "새 오브젝트가 지금 월드에 생긴다(상주 씬에 쌓이지 않게)");
        Object.Destroy(probe);
    }

    /// <summary>사냥 중에 생긴 것(투사체·몹 등 런 오브젝트)은 전투 씬과 함께 사라진다 — 로비로 나가면 남지 않는다(예전 `ClearField`가 하던 일).</summary>
    [UnityTest]
    public IEnumerator LeavingCombat_RemovesEverythingTheRunCreated()
    {
        yield return ToRun();
        var leftover = new GameObject("TestRunLeftover");
        leftover.AddComponent<RunTransient>();
        Assert.AreEqual(SceneManager.GetActiveScene(), leftover.scene, "사냥 중에 생긴 오브젝트는 전투 씬에 생긴다");

        yield return ToLobby();
        yield return null; // Destroy 반영
        Assert.IsTrue(leftover == null, "로비로 나왔는데 지난 런의 오브젝트가 남았다");
        // 0이어야 한다 — 전투 씬이 뜨는 첫 프레임에 생긴 것(예전엔 스포너의 첫 웨이브)이 이전 활성 씬에 생기면 전투 씬과 같이 안 내려가 여기 남는다.
        Assert.AreEqual(0, RunTransient.ActiveCount, "전투 씬 밖에 생긴 런 오브젝트가 남았다");
    }

    /// <summary>
    /// 로비 → 사냥 → 로비 왕복 뒤 상태별 기대값(2026-11-06 정리 순서 4-5, `docs/state-ownership.md`).
    /// 로비에 남아야 하는 것: 로비 창구·카메라 기준점. 전투 씬과 같이 사라져야 하는 것: 전투 창구·담당 등록,
    /// 몹·중력장·드론·포탑 목록, 그리고 **이벤트 구독**(정적 버스·프로필 이벤트 — 전투 쪽 구독자가 남으면 내려간 오브젝트를 붙잡고 있는 것 — 개수가 왕복 전과 같아야 한다).
    /// </summary>
    [UnityTest]
    public IEnumerator LobbyRunLobby_RoundTrip_LeavesOnlyLobbyState()
    {
        yield return ToLobby();
        var lobbySubscribers = SubscriberCounts();

        yield return ToRun();
        float until = Time.realtimeSinceStartup + 10f;
        while (EnemyHealth.Active.Count == 0 && Time.realtimeSinceStartup < until) yield return null;
        Assert.Greater(EnemyHealth.Active.Count, 0, "사냥 중에 몹이 나와야 왕복 확인이 의미가 있다");
        Assert.IsNotNull(WorldView.Combat);
        Assert.AreNotEqual(lobbySubscribers, SubscriberCounts(), "사냥 중엔 전투 쪽 구독자가 붙어 있어야 한다(검사가 비어 있지 않게)");

        yield return ToLobby();
        yield return null; // Destroy 반영

        Assert.IsNotNull(WorldView.Lobby, "로비 창구가 남아 있다");
        var lobbyAnchor = InScene<Transform>(GameFlow.LobbyScene).Single(t => t.name == "CameraAnchor");
        Assert.AreSame(lobbyAnchor, CameraFollow2D.Target, "카메라는 지금 로비 씬의 기준점");
        Assert.IsNull(WorldView.Combat, "전투 창구가 비었다");
        Assert.IsNull(CombatDirector.Current, "전투 담당 등록이 빠졌다");
        Assert.AreEqual(0, EnemyHealth.Active.Count, "몹 목록이 비었다");
        Assert.AreEqual(0, GravityWellZone.Active.Count, "중력장 목록이 비었다");
        Assert.AreEqual(0, GunnerDrone.Active.Count, "드론 목록이 비었다");
        Assert.AreEqual(0, GunnerTurret.Active.Count, "포탑 목록이 비었다");
        Assert.AreEqual(0, RunTransient.ActiveCount, "런 오브젝트가 남지 않았다");
        Assert.AreEqual(lobbySubscribers, SubscriberCounts(), "이벤트 구독이 왕복 전과 같다 — 전투 쪽 구독이 남았다");
    }

    /// <summary>
    /// 이벤트마다 지금 구독자 수(이름=수) — 정적 이벤트 버스 전부와 지금 프로필의 이벤트(`LeveledUp`, 전투의 `PlayerHealth`가 구독).
    /// 필드형 이벤트는 같은 이름의 숨은 필드에 대리자가 있다.
    /// </summary>
    static string SubscriberCounts()
    {
        const System.Reflection.BindingFlags Hidden = System.Reflection.BindingFlags.NonPublic;
        var parts = new List<string>();
        void Count(System.Type t, object owner)
        {
            var scope = owner == null ? System.Reflection.BindingFlags.Static : System.Reflection.BindingFlags.Instance;
            foreach (var e in t.GetEvents(scope | System.Reflection.BindingFlags.Public))
            {
                var field = t.GetField(e.Name, scope | Hidden);
                Assert.IsNotNull(field, $"{t.Name}.{e.Name}의 대리자 필드를 못 찾았다");
                var d = field.GetValue(owner) as System.Delegate;
                parts.Add($"{t.Name}.{e.Name}={(d == null ? 0 : d.GetInvocationList().Length)}");
            }
        }
        foreach (var t in new[] { typeof(CombatEvents), typeof(RunEvents), typeof(EnemySpawnRequestBus), typeof(GameState), typeof(ProfileService) })
            Count(t, null);
        Count(typeof(PlayerProfile), ProfileService.Current);
        return string.Join(", ", parts);
    }

    /// <summary>
    /// 전투 씬 불러오기가 실패하면 로비로 되돌아온다 — 로비 월드가 다시 켜지고(창구·카메라도) 활성 씬이 되며, 날짜는 그대로.
    /// 진짜 로더를 감싸 '불러오기'만 실패시킨다(멈추기·켜기·내리기는 진짜로).
    /// </summary>
    [UnityTest]
    public IEnumerator FailedCombatLoad_ReturnsToTheLobbyWorld()
    {
        yield return ToLobby();
        var lobby = Loaded(GameFlow.LobbyScene).Single();
        int days = ProfileService.Current.doomDays;
        var real = Flow.Scenes;
        Flow.Scenes = new FailingLoads(real);
        try
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("화면 전환 실패"));
            yield return Go(d => Flow.StartRun(1, RunMode.Normal, d), expect: false);
        }
        finally { Flow.Scenes = real; }

        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.AreEqual(0f, Time.timeScale);
        Assert.AreEqual(days, ProfileService.Current.doomDays, "들어가지 못했는데 날짜가 줄었다");
        AssertOnlyWorld(GameFlow.LobbyScene);
        Assert.AreEqual(lobby, Loaded(GameFlow.LobbyScene).Single(), "같은 로비 씬 그대로");
        Assert.IsTrue(lobby.GetRootGameObjects().All(r => r.activeSelf), "멈췄던 로비 월드가 다시 켜진다");
        Assert.IsNotNull(WorldView.Lobby, "로비 창구가 다시 꽂힌다");
        Assert.AreEqual("CameraAnchor", CameraFollow2D.Target.name, "카메라가 다시 로비 기준점에");
    }

    /// <summary>
    /// 사냥 중 로비 불러오기가 실패하면 사냥으로 되돌아온다 — 멈췄던 전투 씬이 다시 켜지며 담당 등록·화면 창구·**카메라가 플레이어를 다시 본다**
    /// (Codex 3-4 리뷰 — 꺼질 때 놓은 카메라 대상이 안 돌아오던 구멍), 시간도 다시 흐른다.
    /// </summary>
    [UnityTest]
    public IEnumerator FailedLobbyLoad_FromCombat_ResumesTheRunWithCamera()
    {
        yield return ToRun();
        var director = CombatDirector.Current;
        var player = InScene<PlayerRig>(GameFlow.CombatScene).Single();
        var real = Flow.Scenes;
        Flow.Scenes = new FailingLoads(real);
        try
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("화면 전환 실패"));
            yield return Go(d => Flow.EnterLobby(d), expect: false);
        }
        finally { Flow.Scenes = real; }

        Assert.AreEqual(GameScene.Run, GameState.Current);
        Assert.AreEqual(1f, Time.timeScale);
        AssertOnlyWorld(GameFlow.CombatScene);
        Assert.AreSame(director, CombatDirector.Current, "같은 전투 담당이 다시 등록된다");
        Assert.AreSame(director, WorldView.Combat);
        Assert.AreSame(player.transform, CameraFollow2D.Target, "카메라가 다시 플레이어를 본다");
    }

    /// <summary>재도전하면 지난 런에서 생긴 것은 사라진다(새 전투 씬의 필드 준비 + 옛 씬 내리기).</summary>
    [UnityTest]
    public IEnumerator Retry_RemovesThePreviousRunsObjects()
    {
        yield return ToRun();
        var leftover = new GameObject("TestRunLeftover");
        leftover.AddComponent<RunTransient>();
        Flow.EndRun(RunEndReason.Dead);

        yield return ToRun();
        yield return null; // Destroy 반영
        Assert.IsTrue(leftover == null, "재도전했는데 지난 런의 오브젝트가 남았다");
        Assert.AreEqual(0, RunTransient.ActiveCount);
    }

    /// <summary>로비가 켜지며 만드는 것(건물·카메라 기준점)은 로비 씬에 속한다 — 불러오는 동안 활성 씬이 아직 이전 씬이어도(로비 오브젝트의 자식으로 만든다).</summary>
    [UnityTest]
    public IEnumerator LobbyBuildingsAndCameraAnchor_BelongToTheLobbyScene()
    {
        yield return ToRun(); // 활성 씬이 전투인 상태에서 로비로
        yield return ToLobby();
        var lobby = Loaded(GameFlow.LobbyScene).Single();
        var world = InScene<LobbyWorld>(GameFlow.LobbyScene).Single();
        var buildings = world.transform.Find("LobbyBuildings");
        Assert.IsNotNull(buildings, "건물이 세워졌다");
        Assert.AreEqual(lobby, buildings.gameObject.scene);
        Assert.AreEqual(lobby, CameraFollow2D.Target.gameObject.scene, "카메라 기준점도 로비 씬");
    }

    sealed class FailingLoads : IWorldScenes
    {
        readonly IWorldScenes inner;
        public FailingLoads(IWorldScenes inner) { this.inner = inner; }
        public WorldSceneOp Load(string sceneName) => WorldSceneOp.Failed("테스트가 실패시킴: " + sceneName);
        public WorldSceneOp Unload(WorldHandle world) => inner.Unload(world);
        public void Activate(WorldHandle world) => inner.Activate(world);
        public void SetRunning(WorldHandle world, bool running) => inner.SetRunning(world, running);
    }

    /// <summary>
    /// 카메라(00_Persistent)의 대상은 지금 화면의 담당이 정한다(2026-11-05 2b) — 로비는 로비 월드의 가운데 기준점에 **바로** 붙고(시간이 멈춘 화면),
    /// 사냥은 전투 담당이 플레이어를 넘긴다. 카메라 쪽엔 '지금 로비인지' 분기가 없다.
    /// </summary>
    [UnityTest]
    public IEnumerator Camera_FollowsTheLobbyAnchor_ThenThePlayer()
    {
        var cam = Camera.main.transform; // 테스트는 실행 코드가 아니라 찾아도 된다
        yield return ToLobby();
        yield return null;
        Assert.AreEqual("CameraAnchor", CameraFollow2D.Target.name, "로비에선 가운데 기준점");
        Assert.AreEqual(LobbyWorld.CenterX, cam.position.x, 0.01f, "로비 카메라는 필드 가운데에 바로 선다(멈춘 시간이라 부드럽게 옮길 수 없다)");

        yield return ToRun();
        var player = InScene<PlayerRig>(GameFlow.CombatScene).Single();
        Assert.AreSame(player.transform, CameraFollow2D.Target, "사냥에선 전투 담당이 플레이어를 넘긴다");
    }

    /// <summary>로비 아바타는 저장된 캐릭터(메카닉)의 그림을 입는다 — 세이브는 로비 씬이 뜨기 전에 불려 있다.</summary>
    [UnityTest]
    public IEnumerator LobbyAvatar_WearsTheSavedCharacter()
    {
        yield return ToLobby();
        yield return null; // LobbyAvatar가 프로필을 읽는 프레임
        var avatar = InScene<LobbyAvatar>(GameFlow.LobbyScene).Single();
        Assert.AreSame(avatar.Art.SpriteOf(SavedCharacter), avatar.GetComponent<SpriteRenderer>().sprite);
    }

    // ───────────────────────── 전투 씬 구성 (사냥에 들어가서 본다) ─────────────────────────

    /// <summary>
    /// 공용 카메라의 화면 크기 설정은 **씬에 저장된 값**이다(2026-11-05) — 예전엔 `CameraFollow2D.Awake`가 실행 중에 덮어써서 씬엔
    /// 기본값(100PPU·320×180)이 남아 있었다. 실행 중에 고치는 코드가 없으니, 지금 보이는 값이 곧 씬 파일의 값이다.
    /// 테스트 어셈블리가 URP를 참조하지 않아 전체 이름으로 찾는다(같은 이름의 옛 2D 패키지 컴포넌트가 따로 있다).
    /// </summary>
    [Test]
    public void SharedCamera_IsPixelPerfect960x540_AsSavedInTheScene()
    {
        var cam = InScene<Camera>(PersistentScene).Single();
        Assert.IsTrue(cam.orthographic);
        Component ppc = cam.GetComponents<Component>()
            .SingleOrDefault(c => c != null && c.GetType().FullName == "UnityEngine.Rendering.Universal.PixelPerfectCamera");
        Assert.IsNotNull(ppc, "픽셀 퍼펙트 카메라가 씬에 없다 — 도트가 정수배로 안 커진다");
        var t = ppc.GetType();
        Assert.AreEqual(StageArtConfig.PixelsPerUnit, (int)t.GetProperty("assetsPPU").GetValue(ppc), "1유닛 = 128px");
        Assert.AreEqual(StageArtConfig.RefWidth, (int)t.GetProperty("refResolutionX").GetValue(ppc));
        Assert.AreEqual(StageArtConfig.RefHeight, (int)t.GetProperty("refResolutionY").GetValue(ppc));
        Assert.AreEqual("UpscaleRenderTexture", t.GetProperty("gridSnapping").GetValue(ppc).ToString());
        Assert.AreEqual("StretchFill", t.GetProperty("cropFrame").GetValue(ppc).ToString(), "창 크기와 상관없이 보이는 범위가 같아야 한다");
        Assert.AreEqual(1, cam.GetComponents<Component>().Count(c => c != null && c.GetType() == ppc.GetType()), "PPC가 둘 붙지 않는다");
    }

    /// <summary>
    /// 플레이어는 캐릭터 그림 에셋에서 저장된 캐릭터(메카닉)의 그림·색을 입고 시작한다(2026-11-05 — 예전엔 씬의 PlayerRig 배열).
    /// PlayerRig는 Awake에서 프로필을 읽는다 — 세이브가 그보다 먼저 불려 있어야 한다.
    /// </summary>
    [UnityTest]
    public IEnumerator Player_WearsTheSavedCharactersArt_FromTheArtAsset()
    {
        yield return ToRun();
        var rig = InScene<PlayerRig>(GameFlow.CombatScene).Single();
        Assert.AreEqual(SavedCharacter, rig.Current);
        Assert.IsNotNull(rig.Art, "Player에 캐릭터 그림 에셋이 연결돼 있다");
        var sr = rig.GetComponent<SpriteRenderer>();
        Assert.AreSame(rig.Art.SpriteOf(SavedCharacter), sr.sprite);
        Assert.AreEqual(rig.Art.TintOf(SavedCharacter), sr.color);
    }

    /// <summary>화면 창구(`WorldView`, 2026-11-05 2b) — 전투 씬이 뜨면 전투 담당이 꽂히고, 같은 씬의 Player·포탈을 가리킨다.</summary>
    [UnityTest]
    public IEnumerator WorldView_HasTheCombatScenesDirector_WithItsPortal()
    {
        yield return ToRun();
        var director = InScene<CombatDirector>(GameFlow.CombatScene).Single();
        Assert.AreSame(director, WorldView.Combat, "전투 담당이 화면 창구에 꽂혀 있다");
        Assert.AreSame(InScene<BossPortal>(GameFlow.CombatScene).Single(), director.BossPortal);
        Assert.Greater(WorldView.Combat.PlayerMaxHp, 0f, "플레이어 체력을 읽는다");
        Assert.IsNotNull(WorldView.Combat.PlayerKit, "지금 캐릭터 키트(스킬 칸)를 읽는다");
    }

    /// <summary>스포너(전투 씬)와 스테이지 선택 화면(상주 씬)이 같은 몬스터 목록 에셋을 본다 — 화면이 월드를 찾지 않는다(2026-11-05).</summary>
    [UnityTest]
    public IEnumerator Spawner_AndStageSelect_ShareOneMonsterCatalog()
    {
        yield return ToRun();
        var spawner = InScene<EnemySpawner>(GameFlow.CombatScene).Single();
        var stage = InScene<StageSelectScreen>(PersistentScene).Single();
        Assert.IsNotNull(spawner.Catalog);
        Assert.AreSame(spawner.Catalog, stage.Catalog);
    }

    /// <summary>전투 담당은 전투 씬에 하나, 같은 씬의 Player를 직렬화로 가리킨다 — 흐름(상주 씬)은 등록(`Current`)으로 찾는다(2026-11-05).</summary>
    [UnityTest]
    public IEnumerator CombatDirector_LivesInTheWorld_AndPointsAtThePlayer()
    {
        yield return ToRun();
        var director = InScene<CombatDirector>(GameFlow.CombatScene).Single();
        Assert.AreSame(director, CombatDirector.Current, "전투 씬이 뜨면 담당이 등록된다");
        Assert.AreSame(InScene<PlayerRig>(GameFlow.CombatScene).Single().gameObject, director.Player);
        Assert.IsEmpty(InScene<CombatDirector>(PersistentScene), "상주 씬엔 없다");
    }

    /// <summary>잔상·섬영 베기 효과는 씬의 Player에 하나씩 붙어 있다(2026-11-05 — 예전엔 `PlayerRig.Awake`가 붙였다).</summary>
    [UnityTest]
    public IEnumerator Player_HasItsEffects_FromTheScene()
    {
        yield return ToRun();
        var player = InScene<PlayerRig>(GameFlow.CombatScene).Single();
        Assert.AreEqual(1, player.GetComponents<PlayerAfterimages>().Length, "잔상(마법사 순간이동·섬영 고속 이동)");
        Assert.AreEqual(1, player.GetComponents<BladeSlashVfx>().Length, "섬영 회전베기 효과");

        // 효과 머티리얼은 씬의 연결 칸(2026-11-06 3.5-e). 살수 베기 프레임은 라이선스 그림이라 키로 불러온다(2026-11-07) — 가져오기 규격은 VfxTests.
        Assert.AreSame(TestPrefabs.SpriteFlash, player.GetComponent<MageChargeVfx>().FlashMaterial);
        Assert.AreSame(TestPrefabs.SpriteFlash, player.GetComponent<PlayerAfterimages>().GhostMaterial);
    }

    /// <summary>전투 담당이 같은 씬의 두 발판 세트를 직렬화로 들고 무대에 맞춰 켠다(2026-11-05 3-0 — 예전엔 정적 목록이 모든 씬의 세트를 켰다).</summary>
    [UnityTest]
    public IEnumerator Director_SwitchesItsOwnPlatformSets_ByArena()
    {
        yield return ToRun();
        var sets = InScene<PlatformSet>(GameFlow.CombatScene);
        Assert.AreEqual(2, sets.Length);
        var director = InScene<CombatDirector>(GameFlow.CombatScene).Single();
        Assert.AreSame(sets.Single(s => !s.bossArena), director.NormalPlatforms);
        Assert.AreSame(sets.Single(s => s.bossArena), director.BossPlatforms);
        Assert.IsTrue(director.NormalPlatforms.gameObject.activeSelf, "일반 사냥은 일반 무대 발판");
        Assert.IsFalse(director.BossPlatforms.gameObject.activeSelf);
        director.BeginField(RunMode.Boss);
        Assert.IsTrue(director.BossPlatforms.gameObject.activeSelf, "보스 무대 발판이 켜진다(예전엔 꺼진 채 저장돼 안 켜졌다)");
        Assert.IsFalse(director.NormalPlatforms.gameObject.activeSelf);
        director.BeginField(RunMode.Normal);
        Assert.IsTrue(director.NormalPlatforms.gameObject.activeSelf);
        Assert.IsFalse(director.BossPlatforms.gameObject.activeSelf);
    }

    /// <summary>
    /// 실제 전투 씬의 스포너가 **종류별 프리팹**으로 몹을 세운다(2026-11-05 생성 책임 2-2) — 세운 몹에 그 종류의 행동이 처음부터 있고(박쥐는 중력 없음),
    /// 보스·성소도 목록의 프리팹에서 나온다. 예전엔 기본 프리팹에 행동 부품을 붙이고 다시 찾게 했다.
    /// </summary>
    [UnityTest]
    public IEnumerator Spawner_BuildsEachKindFromItsPrefab()
    {
        yield return ToRun();
        var spawner = InScene<EnemySpawner>(GameFlow.CombatScene).Single();
        var expected = new System.Collections.Generic.Dictionary<EnemyType, System.Type>
        {
            [EnemyType.Flyer] = typeof(ChronoSavior.Enemies.EnemyFlyerMotion),
            [EnemyType.Charger] = typeof(ChronoSavior.Enemies.EnemyChargerMotion),
            [EnemyType.Shooter] = typeof(ChronoSavior.Enemies.EnemyShooterMotion),
            [EnemyType.Splitter] = typeof(ChronoSavior.Enemies.EnemySplitOnDeath),
        };
        foreach (EnemyType t in System.Enum.GetValues(typeof(EnemyType)))
        {
            var before = new System.Collections.Generic.HashSet<ChronoSavior.Enemies.EnemyMove>(Object.FindObjectsByType<ChronoSavior.Enemies.EnemyMove>(FindObjectsSortMode.None));
            EnemySpawnRequestBus.Request(new Vector2(3f, FieldBounds.GroundY + 0.5f), t, 0.1f);
            var fresh = Object.FindObjectsByType<ChronoSavior.Enemies.EnemyMove>(FindObjectsSortMode.None).Where(m => !before.Contains(m)).ToList();
            Assert.AreEqual(1, fresh.Count, $"{t} 하나가 세워진다");
            var spawned = fresh[0];
            if (expected.TryGetValue(t, out var behaviour)) Assert.IsNotNull(spawned.GetComponent(behaviour), $"{t}에 {behaviour.Name}가 처음부터 있다");
            if (t == EnemyType.Flyer) Assert.AreEqual(0f, spawned.GetComponent<Rigidbody2D>().gravityScale, "박쥐는 중력을 안 받는다");
            Object.Destroy(spawned.gameObject);
        }

        // 1지역은 지역 전용 보스(덩어리 지배자, 2026-11-10), 그 밖은 원본 공통 보스 — 둘 다 프리팹에서 선다.
        var spawnBoss = typeof(EnemySpawner).GetMethod("SpawnBoss", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        spawnBoss.Invoke(spawner, null);
        var blob = Object.FindFirstObjectByType<ChronoSavior.Enemies.BlobLordBoss>();
        Assert.IsNotNull(blob, "1지역 보스 프리팹에서 덩어리 지배자가 선다");
        Assert.IsTrue(blob.GetComponent<ChronoSavior.Enemies.EnemyHealth>().isBoss);
        Object.Destroy(blob.gameObject);
        RunState.Begin(2, RunMode.Normal);
        spawnBoss.Invoke(spawner, null);
        var boss = Object.FindFirstObjectByType<ChronoSavior.Enemies.Boss>();
        Assert.IsNotNull(boss, "2지역은 공통 보스 프리팹에서 보스가 선다");
        Assert.IsTrue(boss.GetComponent<ChronoSavior.Enemies.EnemyHealth>().isBoss);
        typeof(EnemySpawner).GetMethod("SpawnShrine", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(spawner, null);
        var shrine = Object.FindFirstObjectByType<ChronoSavior.Enemies.Shrine>();
        Assert.IsNotNull(shrine, "성소 프리팹에서 성소가 선다");
        Assert.IsTrue(shrine.GetComponent<ChronoSavior.Enemies.EnemyMove>().isStructure);
        Assert.AreEqual(RigidbodyType2D.Kinematic, shrine.GetComponent<Rigidbody2D>().bodyType);
    }

    /// <summary>
    /// 보스·성소가 받는 수치와 크기(2026-11-06 정리 순서 5-1 — 이 설정을 `EnemySetup`으로 옮기면서 고정한 특성 테스트).
    /// 보스: 지역 배율 × 회귀 장벽 체력·공격, 레벨, 넉백 배율, 속도. 성소: 지역 기준 레벨(±1)의 체력, 스폰 보호.
    /// 둘 다 그림은 몬스터 목록의 크기(`bossSize`·`shrineSize`), 판정은 원본 가로·세로 중 작은 쪽의 반지름.
    /// </summary>
    [UnityTest]
    public IEnumerator BossAndShrine_GetTheirStatsAndSizes()
    {
        // 세운 보스·성소는 전투 씬(지금 활성 씬)에 생겨 TearDown이 씬째 내린다. 보스 등장 흔들림·성소 버프 같은 정적 상태는 ResetStatics가 지운다.
        yield return ToRun();
        var spawner = InScene<EnemySpawner>(GameFlow.CombatScene).Single();
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int region = RunState.Region, regressions = ProfileService.Current.regressions;

        // 1지역 전용 보스(덩어리 지배자) — 체력·레벨은 공통 곡선, 그림·판정 크기는 자기 것(2026-11-10).
        typeof(EnemySpawner).GetMethod("SpawnBoss", flags).Invoke(spawner, null);
        var blob = Object.FindFirstObjectByType<ChronoSavior.Enemies.BlobLordBoss>();
        var blobHealth = blob.GetComponent<ChronoSavior.Enemies.EnemyHealth>();
        Assert.AreEqual(ChronoSavior.Enemies.Boss.HpForRegion(region) * RegressionConfig.WallEnemyHp(region, regressions), blobHealth.MaxHp, 0.01f);
        Assert.AreEqual(ChronoSavior.Enemies.Boss.LevelForRegion(region), blobHealth.Level);
        AssertSize(blob.gameObject, ChronoSavior.Enemies.BlobLordBoss.VisualSize, ChronoSavior.Enemies.BlobLordBoss.HitRadius);
        Object.Destroy(blob.gameObject);

        // 그 밖의 지역은 원본 공통 보스
        RunState.Begin(region = 2, RunMode.Normal);
        typeof(EnemySpawner).GetMethod("SpawnBoss", flags).Invoke(spawner, null);
        var boss = Object.FindFirstObjectByType<ChronoSavior.Enemies.Boss>();
        var bossHealth = boss.GetComponent<ChronoSavior.Enemies.EnemyHealth>();
        var bossMove = boss.GetComponent<ChronoSavior.Enemies.EnemyMove>();
        Assert.AreEqual(ChronoSavior.Enemies.Boss.HpForRegion(region) * RegressionConfig.WallEnemyHp(region, regressions), bossHealth.MaxHp, 0.01f);
        Assert.AreEqual(ChronoSavior.Enemies.Boss.LevelForRegion(region), bossHealth.Level);
        Assert.AreEqual(ChronoSavior.Enemies.Boss.KnockbackMultiplier, bossHealth.knockbackMultiplier, 0.0001f);
        Assert.AreEqual(JsMath.Round(ChronoSavior.Enemies.Boss.DamageForRegion(region) * RegressionConfig.WallEnemyDamage(region, regressions)), bossMove.attackPower, 0.01f); // SetupBoss가 원본처럼 반올림한다(1지역 30은 정수라 예전엔 안 드러났다)
        Assert.AreEqual(ChronoSavior.Enemies.Boss.MoveSpeed, bossMove.moveSpeed, 0.0001f);
        AssertSize(boss.gameObject, spawner.Catalog.bossSize, Mathf.Min(EntitySizeConfig.BossWidth, EntitySizeConfig.BossHeight) / 2f);

        typeof(EnemySpawner).GetMethod("SpawnShrine", flags).Invoke(spawner, null);
        var shrine = Object.FindFirstObjectByType<ChronoSavior.Enemies.Shrine>();
        var shrineHealth = shrine.GetComponent<ChronoSavior.Enemies.EnemyHealth>();
        int baseLevel = RegionConfig.RecommendedLevel(region);
        Assert.That(shrineHealth.Level, Is.InRange(baseLevel, baseLevel + 1), "성소 레벨은 지역 기준 +0~1");
        Assert.AreEqual(JsMath.Round(ChronoSavior.Enemies.Shrine.HpForLevel(shrineHealth.Level)), shrineHealth.MaxHp, 0.01f); // 원본처럼 반올림
        Assert.IsTrue(shrine.GetComponent<ChronoSavior.Enemies.EnemyMove>().IsSpawnProtected, "성소는 세워지자마자 잠깐 보호된다");
        AssertSize(shrine.gameObject, spawner.Catalog.shrineSize, Mathf.Min(EntitySizeConfig.ShrineWidth, EntitySizeConfig.ShrineHeight) / 2f);
    }

    static void AssertSize(GameObject go, Vector2 visual, float hitRadius)
    {
        var size = go.GetComponent<SpriteRenderer>().bounds.size;
        Assert.AreEqual(visual.x, size.x, 0.01f, $"{go.name} 그림 가로");
        Assert.AreEqual(visual.y, size.y, 0.01f, $"{go.name} 그림 세로");
        Assert.AreEqual(hitRadius, go.GetComponent<CircleCollider2D>().radius * go.transform.localScale.x, 0.001f, $"{go.name} 판정 반지름");
    }

    /// <summary>스테이지 그림(땅·배경)은 지금 월드에 하나 — 로비와 전투가 각자 자기 그림을 갖고, 같이 떠 있지 않는다.</summary>
    [UnityTest]
    public IEnumerator StageVisuals_OnePerCurrentWorld()
    {
        yield return ToLobby();
        Assert.AreEqual(1, Object.FindObjectsByType<StageVisuals>(FindObjectsSortMode.None).Length, "로비 그림 하나");
        yield return ToRun();
        Assert.AreEqual(1, Object.FindObjectsByType<StageVisuals>(FindObjectsSortMode.None).Length, "전투 그림 하나");
    }
}

}
