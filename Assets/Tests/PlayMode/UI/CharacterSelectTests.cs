using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 로비 캐릭터 선택(2026-11-05 씬 분할 2차 2a) — 고르기는 세이브의 캐릭터 값만 바꾸고 저장하며, 전투의 플레이어(`PlayerRig`)가 그 값을 따라간다.
/// 화면은 월드를 찾지 않는다(`UiWorldLookupTests`). 세이브는 테스트 전체 임시 경로(`TestSaveSandbox`).
/// </summary>
public class CharacterSelectTests
{
    PixelCanvas pc;

    [SetUp]
    public void Setup()
    {
        Assert.AreEqual(TestSaveSandbox.Path, SaveService.SavePath, "테스트 세이브 안전장치(TestSaveSandbox)가 꺼져 있다");
        if (File.Exists(SaveService.SavePath)) File.Delete(SaveService.SavePath);
        ProfileService.Reset();
        UiModal.Reset();
        GameState.Set(GameScene.Lobby);
        pc = PixelCanvas.Create();
    }

    [TearDown]
    public void Teardown()
    {
        CharacterSelectScreen.Close();
        if (pc != null) Object.DestroyImmediate(pc.gameObject);
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && (go.name.StartsWith("Test") || go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)) Object.DestroyImmediate(go);
        if (SaveService.SavePath == TestSaveSandbox.Path && File.Exists(SaveService.SavePath)) File.Delete(SaveService.SavePath);
        UiModal.Reset();
        GameState.Reset();
        ProfileService.Reset();
    }

    static void Pick(CharacterSelectScreen screen, int card) =>
        typeof(CharacterSelectScreen).GetMethod("Pick", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(screen, new object[] { card });

    [UnityTest]
    public IEnumerator Picking_WritesTheProfile_Saves_AndThePlayerFollows()
    {
        var player = new GameObject("TestPlayer");
        player.AddComponent<SpriteRenderer>();
        player.AddComponent<CircleCollider2D>();
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        player.AddComponent<CharacterMover2D>();
        player.AddComponent<MageAttack>();
        player.AddComponent<GunnerAttack>();
        var rig = player.AddComponent<PlayerRig>();

        var screen = pc.GetComponent<CharacterSelectScreen>();
        CharacterSelectScreen.Open();
        yield return null;

        Pick(screen, 1); // 둘째 카드 = 메카닉
        Assert.AreEqual(CharacterId.Gunner, ProfileService.Current.character, "고르면 세이브의 캐릭터 값이 바뀐다");
        Assert.IsFalse(CharacterSelectScreen.IsOpen, "고르면 창이 닫힌다");
        Assert.IsTrue(File.Exists(SaveService.SavePath), "고르면 저장한다");
        StringAssert.Contains($"\"character\":{(int)CharacterId.Gunner}", File.ReadAllText(SaveService.SavePath).Replace(" ", ""));

        yield return null;
        Assert.AreEqual(CharacterId.Gunner, rig.Current, "플레이어가 다음 프레임에 프로필을 따라간다");
    }

    [Test]
    public void LockedCard_DoesNothing()
    {
        var screen = pc.GetComponent<CharacterSelectScreen>();
        CharacterSelectScreen.Open();
        Pick(screen, 3); // "???" — 아직 없는 캐릭터
        Assert.AreEqual(CharacterId.Mage, ProfileService.Current.character);
        Assert.IsFalse(File.Exists(SaveService.SavePath), "잠긴 카드는 저장도 안 한다");
    }

    [Test]
    public void Selectable_IsTheThreeCharactersWithKits()
    {
        CollectionAssert.AreEqual(new[] { CharacterId.Mage, CharacterId.Gunner, CharacterId.Blade }, CharacterSelectScreen.Selectable.ToArray());
    }
}

}
