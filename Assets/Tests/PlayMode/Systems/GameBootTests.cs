using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 씬 분할 1차(2026-11-04) 통합 테스트 — **실제 게임 씬**(`00_Persistent` → `20_Combat`)을 불러와 시작 과정을 확인한다.
/// 다른 테스트는 빈 씬에 컴포넌트를 직접 만들지만, 이건 씬 파일 자체가 맞게 구성됐는지·시작 순서가 맞는지를 본다.
///
/// ⚠️ `GameBoot`는 세이브를 읽고, 화면이 바뀌면 `AutoSave`가 쓴다 — 개발자의 진짜 세이브를 건드리지 않게 **테스트 전용 경로**
/// (`SaveService.PathOverride`, 임시 폴더)를 쓴다. 테스트가 중간에 죽어도 진짜 세이브는 안전하다. 늘 새 프로필로 시작한다.
/// </summary>
public class GameBootTests
{
    string tempSave;

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
        // GameBoot가 Awake에서 월드 씬을 겹쳐 부른다 — 다 뜰 때까지 기다린다.
        float until = Time.realtimeSinceStartup + 10f;
        while (!SceneManager.GetSceneByName(GameBoot.WorldScene).isLoaded && Time.realtimeSinceStartup < until) yield return null;
        yield return null; // Start까지 한 프레임
    }

    [UnityTearDown]
    public IEnumerator Teardown()
    {
        // 경로·시간부터 되돌린다 — 아래 씬 내리기가 실패해도 다음 테스트가 테스트용 경로를 물려받지 않게
        SaveService.PathOverride = null;
        Time.timeScale = 1f;
        try { if (File.Exists(tempSave)) File.Delete(tempSave); } catch (IOException) { }
        foreach (var name in new[] { GameBoot.WorldScene, PersistentScene })
        {
            var scene = SceneManager.GetSceneByName(name);
            if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        }
        ResetStatics();
    }

    const string PersistentScene = "00_Persistent";

    static void ResetStatics()
    {
        GameState.Reset();
        RunState.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunTransient.Reset();
        CombatModifiers.Reset();
        HitFeel.Reset();
        ProfileService.Reset();
    }

    const CharacterId SavedCharacter = CharacterId.Gunner; // 기본값(마법사)과 다르게
    const int SavedGold = 1234;

    [Test]
    public void Boot_LoadsTheSave_BeforeAnythingReadsTheProfile()
    {
        Assert.AreEqual(SavedGold, ProfileService.Current.gold, "GameBoot가 세이브를 불러왔다");
        var rig = InScene<PlayerRig>(GameBoot.WorldScene).Single();
        Assert.AreEqual(SavedCharacter, rig.Current,
            "PlayerRig는 Awake에서 프로필의 캐릭터를 읽는다 — 세이브가 그보다 먼저 불려야 저장된 캐릭터(메카닉)로 켜진다");
    }

    [Test]
    public void Boot_UsesTheTestSavePath_NotTheRealOne()
    {
        Assert.AreEqual(tempSave, SaveService.SavePath, "이 테스트는 진짜 세이브 위치를 쓰면 안 된다");
    }

    static T[] InScene<T>(string scene) where T : Component =>
        SceneManager.GetSceneByName(scene).GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    [Test]
    public void Boot_LoadsTheWorld_AndMakesItTheActiveScene()
    {
        Assert.IsTrue(SceneManager.GetSceneByName(GameBoot.WorldScene).isLoaded, "GameBoot가 월드 씬을 불러온다");
        Assert.AreEqual(GameBoot.WorldScene, SceneManager.GetActiveScene().name,
            "월드 씬이 활성 씬이어야 사냥 중 새로 생기는 투사체·이펙트가 월드 씬에 생긴다(상주 씬에 쌓이지 않게)");
        var probe = new GameObject("TestBootProbe");
        Assert.AreEqual(GameBoot.WorldScene, probe.scene.name);
        Object.Destroy(probe);
    }

    [Test]
    public void Boot_StartsAtTitle_WithOneOfEachSharedObject()
    {
        Assert.AreEqual(GameScene.Title, GameState.Current, "시작 화면은 타이틀");
        Assert.AreEqual(1, Object.FindObjectsByType<PixelCanvas>(FindObjectsSortMode.None).Length, "UI 판은 하나(자동 생성이 겹치지 않음)");
        Assert.AreEqual(1, Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None).Length, "EventSystem은 하나");
        Assert.AreEqual(1, Object.FindObjectsByType<Camera>(FindObjectsSortMode.None).Length, "카메라는 상주 씬의 하나뿐");
        Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length, "AudioListener는 하나");
        Assert.AreEqual(1, Object.FindObjectsByType<StageVisuals>(FindObjectsSortMode.None).Length, "스테이지 그림도 하나");
        Assert.IsNotNull(PixelCanvas.Instance.Flow, "UI 판이 같은 씬의 GameFlow에 연결돼 있다(화면이 씬을 뒤지지 않는다)");
        Assert.AreEqual(Color.black, Camera.main.backgroundColor, "타이틀엔 월드가 없으니 카메라는 검게 지운다(카메라를 끄지 않는다)");
    }

    [Test]
    public void Camera_TracksThePlayer_AcrossScenes()
    {
        var player = InScene<PlayerRig>(GameBoot.WorldScene).Single();
        Assert.AreSame(player.transform, CameraFollow2D.Target, "플레이어(20_Combat)가 카메라(00_Persistent)에 스스로 등록한다");
    }

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

    /// <summary>전투 담당은 전투 씬에 하나, 같은 씬의 Player를 직렬화로 가리킨다 — 흐름(상주 씬)은 등록(`Current`)으로 찾는다(2026-11-05).</summary>
    [Test]
    public void CombatDirector_LivesInTheWorld_AndPointsAtThePlayer()
    {
        var director = InScene<CombatDirector>(GameBoot.WorldScene).Single();
        Assert.AreSame(director, CombatDirector.Current, "전투 씬이 뜨면 담당이 등록된다");
        Assert.AreSame(InScene<PlayerRig>(GameBoot.WorldScene).Single().gameObject, director.Player);
        Assert.IsEmpty(InScene<CombatDirector>(PersistentScene), "상주 씬엔 없다");
    }

    /// <summary>잔상·섬영 베기 효과는 씬의 Player에 하나씩 붙어 있다(2026-11-05 — 예전엔 `PlayerRig.Awake`가 붙였다).</summary>
    [Test]
    public void Player_HasItsEffects_FromTheScene()
    {
        var player = InScene<PlayerRig>(GameBoot.WorldScene).Single();
        Assert.AreEqual(1, player.GetComponents<PlayerAfterimages>().Length, "잔상(마법사 순간이동·섬영 고속 이동)");
        Assert.AreEqual(1, player.GetComponents<BladeSlashVfx>().Length, "섬영 회전베기 효과");
    }

    [Test]
    public void BothPlatformSets_AreRegistered_SoTheBossArenaShowsItsPlatforms()
    {
        var sets = InScene<PlatformSet>(GameBoot.WorldScene);
        Assert.AreEqual(2, sets.Length);
        PlatformSet.Activate(true);
        Assert.IsTrue(sets.Single(s => s.bossArena).gameObject.activeSelf, "보스 무대 발판이 켜진다(예전엔 꺼진 채 저장돼 안 켜졌다)");
        Assert.IsFalse(sets.Single(s => !s.bossArena).gameObject.activeSelf);
        PlatformSet.Activate(false);
        Assert.IsTrue(sets.Single(s => !s.bossArena).gameObject.activeSelf);
        Assert.IsFalse(sets.Single(s => s.bossArena).gameObject.activeSelf);
    }

    [UnityTest]
    public IEnumerator Flow_TitleToLobbyToRunToResult_Works()
    {
        var flow = PixelCanvas.Instance.Flow;
        flow.EnterLobby();
        yield return null;
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.IsNotNull(LobbyWorld.Instance, "로비 건물(월드 씬)이 있다");

        flow.StartRun(1, RunMode.Normal);
        yield return null;
        Assert.AreEqual(GameScene.Run, GameState.Current);
        Assert.AreEqual(1f, Time.timeScale);

        flow.EndRun(RunEndReason.Timeout);
        Assert.AreEqual(GameScene.Result, GameState.Current);
        flow.EnterLobby();
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
    }
}

}
