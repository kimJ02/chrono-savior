using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 2026-11-16 Figma 새 판(팀원 UI zip + Figma 설정 아이콘) — 일시정지·공용 팝업·설정·결과창.
/// 그림 키가 다 들어왔는지, 팝업의 버튼 셋·X가 묻기/알림에 맞게 동작하는지, 설정 음량이 0~100으로 보이고 막대가 따라오는지.
/// 배치가 Figma와 맞는지는 `CaptureUiPreview` 캡처를 Figma 그림과 나란히 본다.
/// </summary>
public class UiFigmaUpdateTests
{
    PixelCanvas pc;

    [SetUp]
    public void Setup()
    {
        Assert.AreEqual(TestSaveSandbox.Path, SaveService.SavePath, "테스트 세이브 안전장치(TestSaveSandbox)가 꺼져 있다");
        if (File.Exists(SaveService.SavePath)) File.Delete(SaveService.SavePath);
        ProfileService.Reset();
        UiModal.Reset();
        GameState.Set(GameScene.Title);
        pc = PixelCanvas.Create();
    }

    [TearDown]
    public void Teardown()
    {
        if (pc != null) Object.DestroyImmediate(pc.gameObject);
        foreach (var go in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
            Object.DestroyImmediate(go.gameObject);
        UiModal.Reset();
        GameState.Set(GameScene.Run);
        if (SaveService.SavePath == TestSaveSandbox.Path && File.Exists(SaveService.SavePath)) File.Delete(SaveService.SavePath);
        ProfileService.Reset();
    }

    static readonly string[] Keys =
    {
        "Pause/band", "Icons/play", "Icons/gear", "Icons/exit",
        "Common/window_tall", "Common/close_frame", "Icons/close_x", "Common/button_wide", "Common/button", "Common/title_plate",
        "Settings/window", "Settings/row", "Settings/arrow_frame", "Settings/arrow", "Settings/value_box", "Settings/number_box",
        "Settings/volume_back", "Settings/volume_fill", "Settings/volume_knob", "Settings/apply",
        "Settings/icon_screen", "Settings/icon_resolution", "Settings/icon_bgm", "Settings/icon_sfx",
        "Result/banner", "Result/divider", "Result/reward_box", "Result/name_plate", "Result/stats_box",
        "Result/bar_back", "Result/bar_frame", "Result/bar_fill", "Result/button",
    };

    [Test]
    public void AllNewArtKeys_Load_PixelExact()
    {
        foreach (var k in Keys)
        {
            var s = PxArt.Get(k);
            Assert.IsNotNull(s, "그림이 없다: " + k);
            Assert.AreEqual(FilterMode.Point, s.texture.filterMode, k + " — 흐리게 들어왔다");
        }
        Assert.AreEqual(new Vector2(512f, 304f), PxArt.Get("Common/window_tall").rect.size, "새 창 틀 512×304");
    }

    PxButton Find(PxScreen s, string name)
    {
        foreach (var b in s.Root.GetComponentsInChildren<PxButton>(true)) if (b.name == name) return b;
        return null;
    }

    [UnityTest]
    public IEnumerator Popup_Confirm_LeftCancelRightOk_XCancels()
    {
        var popup = pc.GetComponent<CommonPopup>();
        bool ok = false, cancelled = false;
        CommonPopup.Confirm("새 원정", "지울까요?", () => ok = true, "시작", "취소", () => cancelled = true);
        yield return null;
        Assert.IsTrue(popup.IsShown);
        Assert.IsTrue(popup.CancelShown && popup.OkShown, "묻기 = 왼쪽 취소·오른쪽 확인");
        Assert.IsFalse(popup.WideShown, "묻기에선 가운데 큰 버튼을 숨긴다");
        Assert.IsNotNull(Find(popup, "Close"), "X 닫기");
        popup.DismissForTest();
        Assert.IsTrue(cancelled, "X는 묻기에서 취소");
        Assert.IsFalse(ok);
        popup.DismissForTest();
        Find(popup, "Ok").Click(); // 닫힌 뒤 또 눌려도
        Assert.IsFalse(ok, "콜백은 한 번만");
    }

    [UnityTest]
    public IEnumerator Popup_Alert_OneWideButton_XAlsoConfirms()
    {
        var popup = pc.GetComponent<CommonPopup>();
        int calls = 0;
        CommonPopup.Alert("유물 개방", "뽑을 수 있는 유물이 없다.", () => calls++);
        yield return null;
        Assert.IsTrue(popup.WideShown, "알림 = 가운데 큰 확인");
        Assert.IsFalse(popup.CancelShown || popup.OkShown, "알림에선 작은 버튼 둘을 숨긴다");
        popup.DismissForTest();
        popup.DismissForTest();
        Assert.AreEqual(1, calls, "알림의 X는 확인과 같다 — 한 번만");
    }

    [Test]
    public void Volume_ShownAs0To100_SliderMapsPositionToSteps()
    {
        Assert.AreEqual(0, SettingsWindow.VolumeShown(0));
        Assert.AreEqual(50, SettingsWindow.VolumeShown(5), "Figma 예시 '50' = 저장값 5");
        Assert.AreEqual(100, SettingsWindow.VolumeShown(GameSettings.VolumeMax));
        Assert.AreEqual(0, PxSlider.ValueAt(-5f, 208f, 10), "막대 왼쪽 밖 = 0");
        Assert.AreEqual(5, PxSlider.ValueAt(104f, 208f, 10), "가운데 = 5");
        Assert.AreEqual(10, PxSlider.ValueAt(300f, 208f, 10), "오른쪽 밖 = 10");
    }

    [UnityTest]
    public IEnumerator Settings_VolumeKnobFollowsValue_AndShowsTimesTen()
    {
        int bgm = GameSettings.BgmVolume;
        try
        {
            GameState.Set(GameScene.Pause);
            var settings = pc.GetComponent<SettingsWindow>();
            SettingsWindow.Open();
            GameSettings.BgmVolume = 0;
            yield return null;
            yield return null;
            Assert.IsTrue(settings.IsShown);
            Assert.AreEqual("0", settings.ValueTextForTest(2));
            float x0 = settings.KnobXForTest(0);
            GameSettings.BgmVolume = 10;
            yield return null;
            Assert.AreEqual("100", settings.ValueTextForTest(2));
            Assert.Greater(settings.KnobXForTest(0), x0 + 150f, "음량이 오르면 손잡이가 오른쪽 끝으로");
            Assert.IsNotNull(Find(settings, "Apply"), "적용 버튼");
            Find(settings, "Apply").Click();
            Assert.IsFalse(SettingsWindow.IsOpen, "적용 = 닫기(값은 바꾸는 즉시 적용된다)");
        }
        finally { GameSettings.BgmVolume = bgm; SettingsWindow.Close(); }
    }

    [UnityTest]
    public IEnumerator Result_BuildsWithNewArt_NoMissingPictures()
    {
        var result = pc.GetComponent<RunResultScreen>();
        result.PreviewReason = RunEndReason.BossDead;
        result.ShowForCapture(false);
        yield return null;
        foreach (var img in result.Root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            Assert.IsFalse(img.sprite == null && img.color.r > 0.9f && img.color.b > 0.9f && img.color.g < 0.1f, "그림이 빠진 자리(분홍): " + img.name);
        Assert.IsNotNull(Find(result, "Retry"));
        Assert.IsNotNull(Find(result, "Ok"));
        Assert.IsFalse(Label(result, "Cause").enabled, "끝까지 버티면 붕괴 원인 줄이 없다");
        Assert.AreEqual("다시 입장", Find(result, "Retry").Inner.GetComponentInChildren<UnityEngine.UI.Text>().text);
        Assert.AreEqual("마을로", Find(result, "Ok").Inner.GetComponentInChildren<UnityEngine.UI.Text>().text);
        result.HideForCapture();
    }

    static UnityEngine.UI.Text Label(PxScreen s, string name)
    {
        foreach (var t in s.Root.GetComponentsInChildren<UnityEngine.UI.Text>(true)) if (t.name == name) return t;
        return null;
    }

    /// <summary>쓰러졌을 때도 같은 틀 — 붉은 제목·붕괴 원인 줄·처치 수가 첫 칸·재동기화/성문으로(예전 화면과 같은 내용).</summary>
    [UnityTest]
    public IEnumerator Result_DeadVariant_SameFrame_CauseAndButtons()
    {
        var result = pc.GetComponent<RunResultScreen>();
        result.PreviewReason = RunEndReason.Dead;
        result.ShowForCapture(false);
        yield return null;
        Assert.AreEqual("시간선 붕괴", Label(result, "Title").text);
        Assert.IsTrue(Label(result, "Cause").enabled, "쓰러지면 붕괴 원인 줄");
        StringAssert.StartsWith("붕괴 원인:", Label(result, "Cause").text);
        Assert.AreEqual("처치 수", Label(result, "CellLabel0").text, "쓰러지면 처치 수가 첫 칸");
        Assert.AreEqual("재동기화", Find(result, "Retry").Inner.GetComponentInChildren<UnityEngine.UI.Text>().text);
        Assert.AreEqual("성문으로", Find(result, "Ok").Inner.GetComponentInChildren<UnityEngine.UI.Text>().text);
        result.HideForCapture();
    }
}

}
