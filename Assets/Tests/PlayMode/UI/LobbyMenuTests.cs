using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.UI;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>로비 ESC 메뉴(2026-11-22 사용자 — 일시정지 메뉴를 로비 모드로: 계속 · 설정 · 타이틀로).</summary>
public class LobbyMenuTests
{
    PixelCanvas pc;

    [SetUp]
    public void Setup()
    {
        ProfileService.Reset();
        UiModal.Reset();
        GameState.Set(GameScene.Lobby);
        pc = PixelCanvas.Create();
    }

    [TearDown]
    public void Teardown()
    {
        if (pc != null) Object.DestroyImmediate(pc.gameObject);
        foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            Object.DestroyImmediate(es.gameObject);
        UiModal.Reset();
        GameState.Reset();
        ProfileService.Reset();
    }

    [UnityTest]
    public IEnumerator LobbyMenu_Opens_BlocksLobby_AndEscapeCloses()
    {
        var menu = pc.GetComponent<PauseMenu>();
        PauseMenu.OpenLobbyMenu();
        yield return null;
        Assert.IsTrue(menu.IsShown, "로비 메뉴가 안 떴다");
        Assert.IsTrue(UiModal.Open, "메뉴가 떠 있는 동안 로비 조작을 막아야 한다");

        typeof(PauseMenu).GetMethod("OnEscape", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(menu, null);
        yield return null;
        Assert.IsFalse(menu.IsShown, "ESC로 닫혀야 한다");
        Assert.IsFalse(UiModal.Open);
    }

    [UnityTest]
    public IEnumerator LobbyMenu_ClosesWhenLeavingLobby()
    {
        PauseMenu.OpenLobbyMenu();
        yield return null;
        GameState.Set(GameScene.Title);
        yield return null;
        Assert.IsFalse(PauseMenu.LobbyMenuOpen, "로비를 떠났는데 메뉴가 남았다");
    }
}

}
