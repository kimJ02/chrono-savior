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

    /// <summary>
    /// 고르면 "이번 회차에서는 캐릭터를 바꿀 수 없다" 확인창이 뜬다(2026-11-22 사용자 요청 — feature/relics) — 그 확인(오른쪽 Ok)을 누른다.
    /// 팀원 UI 개편(프리팹 화면) 병합 때 이 단계를 테스트에 다시 넣었다(2026-11-24).
    /// </summary>
    void ConfirmPopup()
    {
        Assert.IsTrue(CommonPopup.IsOpen, "고르면 경고 확인창이 뜬다");
        System.Linq.Enumerable.First(pc.GetComponent<CommonPopup>().Root.GetComponentsInChildren<PxButton>(true), b => b.name == "Ok").Click();
    }

    [UnityTest]
    public IEnumerator Picking_WritesTheProfile_Saves_AndThePlayerFollows()
    {
        var player = new GameObject("TestPlayer");
        player.AddComponent<SpriteRenderer>();
        player.AddComponent<CircleCollider2D>();
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        player.AddComponent<CharacterMover2D>();
        player.AddComponent<MageAttack>().Equipped();
        player.AddComponent<GunnerAttack>().Equipped();
        var rig = player.AddComponent<PlayerRig>();

        var screen = pc.GetComponent<CharacterSelectScreen>();
        CharacterSelectScreen.Open();
        yield return null;

        Pick(screen, 1); // 둘째 카드 = 메카닉
        ConfirmPopup();
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

    /// <summary>
    /// 프리팹 UI 9호(2026-11-23) — 캐릭터 선택 모양은 프리팹(`Resources/UI/Views/CharacterSelectScreenView`)에서 오고,
    /// 선택칸 이름은 코드의 캐릭터 표 순서(마법사·메카닉·살수·히어로), 계열 칸에 마우스를 올리면 설명 창이 뜬다.
    /// (처음엔 메뉴 `ChronoSavior/UI 프리팹/캐릭터 선택 만들기`로 만들어야 통과한다.)
    /// </summary>
    [UnityTest]
    public IEnumerator LayoutFromViewPrefab_TooltipOnTypeHover()
    {
        // 실제 마우스가 Game 창 위에 있으면 마우스 올림이 섞인다 — 포인터 이벤트를 끈다(PixelUiTests의 같은 이유). EventSystem은 TearDown이 지운다.
        { var es = UnityEngine.EventSystems.EventSystem.current; if (es != null) es.enabled = false; }
        Assert.IsNotNull(Resources.Load<CharacterSelectScreenView>(CharacterSelectScreenView.ResourcePath),
                         "캐릭터 선택 프리팹이 없다 — 메뉴 ChronoSavior/UI 프리팹/캐릭터 선택 만들기");
        var screen = pc.GetComponent<CharacterSelectScreen>();
        CharacterSelectScreen.Open();
        yield return null;

        Assert.IsTrue(screen.IsShown, "로비에서 열면 보인다");
        Assert.IsTrue(screen.UsesViewPrefab, "모양을 프리팹에서 불러온다");
        var views = screen.Root.GetComponentsInChildren<CharacterSelectScreenView>(true);
        Assert.AreEqual(1, views.Length, "모양은 한 벌뿐");
        var v = views[0];
        CollectionAssert.AreEqual(new[] { "마법사", "메카닉", "살수", "히어로" }, v.slotNames.Select(t => t.text).ToArray(), "선택칸 순서 = 코드 순서");
        Assert.AreEqual("마법사", v.nameText.text, "처음엔 지금 캐릭터(마법사)");
        Assert.IsFalse(v.tooltip.activeSelf, "처음엔 설명 창이 닫혀 있다");

        UnityEngine.EventSystems.ExecuteEvents.Execute(v.types[0].hover.gameObject, new UnityEngine.EventSystems.PointerEventData(null),
                                                       UnityEngine.EventSystems.ExecuteEvents.pointerEnterHandler);
        yield return null;
        Assert.IsTrue(v.tooltip.activeSelf, "계열 칸에 마우스를 올리면 설명 창");
        Assert.AreEqual("폭발 계열", v.tipName.text);
        UnityEngine.EventSystems.ExecuteEvents.Execute(v.types[0].hover.gameObject, new UnityEngine.EventSystems.PointerEventData(null),
                                                       UnityEngine.EventSystems.ExecuteEvents.pointerExitHandler);
        yield return null;
        Assert.IsFalse(v.tooltip.activeSelf, "마우스가 나가면 닫힌다");
    }

    /// <summary>
    /// 프리팹으로 옮겨도 버튼 동작은 예전 그대로 — 선택칸을 한 번 누르면 보기만(이름·계열이 그 캐릭터로), 선택 버튼이 고르고 닫는다.
    /// 히어로(아직 없는 캐릭터) 칸은 마우스로 눌리지 않는다(키보드로만 골라 본다).
    /// </summary>
    [UnityTest]
    public IEnumerator Slots_ShowFirst_SelectButtonPicks()
    {
        { var es = UnityEngine.EventSystems.EventSystem.current; if (es != null) es.enabled = false; }
        var screen = pc.GetComponent<CharacterSelectScreen>();
        CharacterSelectScreen.Open();
        yield return null;
        var v = screen.Root.GetComponentInChildren<CharacterSelectScreenView>(true);
        Assert.AreEqual(0, screen.NavIndex, "처음엔 지금 캐릭터 칸(마법사)");

        v.slots[3].button.Click();
        yield return null;
        Assert.AreEqual("마법사", v.nameText.text, "히어로(잠긴 칸)는 마우스로 못 누른다 — 키보드로만 골라 본다(예전 그대로)");
        Assert.IsTrue(CharacterSelectScreen.IsOpen);

        v.slots[1].button.Click();
        yield return null;
        Assert.AreEqual("메카닉", v.nameText.text, "한 번 누르면 보기만");
        Assert.IsTrue(v.types[0].root.activeSelf && v.types[1].root.activeSelf, "있는 캐릭터는 계열 칸이 보인다");
        Assert.IsTrue(CharacterSelectScreen.IsOpen, "한 번 눌러선 안 닫힌다");
        Assert.AreEqual(CharacterId.Mage, ProfileService.Current.character, "보기만 했으니 그대로");

        v.select.button.Click();
        ConfirmPopup();
        Assert.AreEqual(CharacterId.Gunner, ProfileService.Current.character, "선택 버튼이 보던 캐릭터를 고른다");
        Assert.IsFalse(CharacterSelectScreen.IsOpen, "고르면 닫힌다");
    }

    [Test]
    public void Selectable_IsTheThreeCharactersWithKits()
    {
        CollectionAssert.AreEqual(new[] { CharacterId.Mage, CharacterId.Gunner, CharacterId.Blade }, CharacterSelectScreen.Selectable.ToArray());
    }
}

}
