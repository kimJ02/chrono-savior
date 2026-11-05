#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 에디터 Play 진입(2026-11-05 씬 분할 2차 3-5) — `Editor/PlayFromPersistent`가 열어 둔 씬 이름을 `SessionState`에 적으면,
/// 상주 씬의 `GameFlow.Start`가 한 번 읽고 그 화면으로 바로 들어간다(로비 / 사냥 — 날짜는 안 씀). 진짜 `00_Persistent`를 부팅해 본다.
/// 세이브는 테스트 전체 임시 경로(`TestSaveSandbox`)라 진짜 세이브는 안 건드린다.
/// </summary>
public class EditorEntryTests
{
    [SetUp]
    public void Setup()
    {
        ResetStatics();
        SessionState.EraseString(GameFlow.EditorEntryKey);
    }

    [UnityTearDown]
    public IEnumerator Teardown()
    {
        SessionState.EraseString(GameFlow.EditorEntryKey);
        Time.timeScale = 1f;
        var persistent = SceneManager.GetSceneByName(GameFlow.PersistentScene);
        if (persistent.isLoaded) yield return SceneManager.UnloadSceneAsync(persistent);
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && (s.name == GameFlow.LobbyScene || s.name == GameFlow.CombatScene)) yield return SceneManager.UnloadSceneAsync(s);
        }
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

    /// <summary>상주 씬을 부팅하고 첫 전환이 끝날 때까지(최대 10초) 기다린다.</summary>
    static IEnumerator Boot()
    {
        yield return SceneManager.LoadSceneAsync(GameFlow.PersistentScene, LoadSceneMode.Additive);
        yield return null; // GameFlow.Start
        float until = Time.realtimeSinceStartup + 10f;
        while (GameState.IsTransitioning && Time.realtimeSinceStartup < until) yield return null;
        Assert.IsFalse(GameState.IsTransitioning, "10초 안에 진입이 안 끝났다");
    }

    [UnityTest]
    public IEnumerator LobbySceneOpened_StartsInTheLobby()
    {
        SessionState.SetString(GameFlow.EditorEntryKey, GameFlow.LobbyScene);
        yield return Boot();
        Assert.AreEqual(GameScene.Lobby, GameState.Current);
        Assert.IsTrue(SceneManager.GetSceneByName(GameFlow.LobbyScene).isLoaded);
        Assert.AreEqual("", SessionState.GetString(GameFlow.EditorEntryKey, ""), "한 번 읽고 지운다");
    }

    [UnityTest]
    public IEnumerator CombatSceneOpened_StartsARun_WithoutSpendingADay()
    {
        int days = ProfileService.Current.doomDays;
        SessionState.SetString(GameFlow.EditorEntryKey, GameFlow.CombatScene);
        yield return Boot();
        Assert.AreEqual(GameScene.Run, GameState.Current);
        Assert.AreEqual(1f, Time.timeScale);
        Assert.IsTrue(SceneManager.GetSceneByName(GameFlow.CombatScene).isLoaded);
        Assert.AreEqual(ProfileService.Current.doomDays, days, "디버그 진입은 날짜를 쓰지 않는다");
        Assert.AreEqual(PixelCanvas.Instance.Flow.defaultRegion, RunState.Region);
    }

    [UnityTest]
    public IEnumerator NoEntry_StartsAtTitle()
    {
        yield return Boot();
        Assert.AreEqual(GameScene.Title, GameState.Current);
        Assert.IsFalse(SceneManager.GetSceneByName(GameFlow.LobbyScene).isLoaded);
        Assert.IsFalse(SceneManager.GetSceneByName(GameFlow.CombatScene).isLoaded);
    }

    /// <summary>테스트가 빈 씬에 만든 흐름은 진입 값을 읽지 않는다 — 상주 씬의 흐름만(남은 값이 다른 테스트를 로비로 보내지 않게).</summary>
    [UnityTest]
    public IEnumerator FlowOutsideThePersistentScene_IgnoresTheEntry()
    {
        SessionState.SetString(GameFlow.EditorEntryKey, GameFlow.LobbyScene);
        var flow = TestFlow.Create();
        yield return null;
        Assert.AreEqual(GameScene.Title, GameState.Current);
        Assert.AreEqual(GameFlow.LobbyScene, SessionState.GetString(GameFlow.EditorEntryKey, ""), "상주 씬이 아니면 건드리지 않는다");
        Object.Destroy(flow.gameObject);
        foreach (var go in Object.FindObjectsByType<CombatDirector>(FindObjectsSortMode.None).Select(d => d.gameObject)) Object.Destroy(go);
    }
}

}
#endif
