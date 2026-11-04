using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// Figma UI 판(2026-10-24) — 정수배 배율, 버튼 상태 그림, 일시정지·설정 창이 게임 상태를 따라 켜지는지, 설정 창이 ESC를 먼저 쓰는지.
/// 그림이 Figma와 맞는지는 `CaptureUiPreview`로 찍어 Figma 그림과 나란히 본다.
/// </summary>
public class PixelUiTests
{
    PixelCanvas pc;
    bool hadSave;
    string saveBackup;

    [SetUp]
    public void Setup()
    {
        // 화면 버튼이 저장까지 부른다 — 진짜 세이브(검증 폴더도 같은 경로)를 SaveServiceTests처럼 지켜 둔다
        hadSave = File.Exists(SaveService.SavePath);
        saveBackup = hadSave ? File.ReadAllText(SaveService.SavePath) : null;
        ProfileService.Reset();
        UiModal.Reset();
        GameState.Set(GameScene.Run);
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
        if (hadSave) File.WriteAllText(SaveService.SavePath, saveBackup);
        else if (File.Exists(SaveService.SavePath)) File.Delete(SaveService.SavePath);
        ProfileService.Reset();
    }

    [Test]
    public void Scale_FillsScreen_WholeAtStandardResolutions()
    {
        Assert.AreEqual(3f, PixelCanvas.ScaleFor(1920, 1080), "1080p는 정확히 3배");
        Assert.AreEqual(2f, PixelCanvas.ScaleFor(1280, 720));
        Assert.AreEqual(4f, PixelCanvas.ScaleFor(2560, 1440));
        Assert.AreEqual(3f, PixelCanvas.ScaleFor(1920, 1078), "거의 정수배면 정수배로(도트 고르게, 넘쳐도 2픽셀)");
        Assert.AreEqual(1075f / 360f, PixelCanvas.ScaleFor(1920, 1075), 1e-4f, "정수배로 하면 2픽셀 넘게 넘칠 땐 꽉 채우기");
        Assert.AreEqual(482f / 360f, PixelCanvas.ScaleFor(857.4f, 482f), 1e-4f, "에디터 Game 창 — 1배로 작아지지 않고 화면을 채운다");
        Assert.AreEqual(768f / 360f, PixelCanvas.ScaleFor(1366, 768), 1e-4f, "노트북 1366×768도 꽉 채운다");
        Assert.AreEqual(600f / 640f, PixelCanvas.ScaleFor(600, 400), 1e-5f, "640×360보다 작으면 맞춰 줄인다");
    }

    [Test]
    public void ButtonTones_ChangeBrightnessOnly_KeepOutline()
    {
        var fill = new Color(0.58f, 0.63f, 0.72f, 1f);  // Figma 버튼 청회색
        var ink = Px.Ink;                               // 테두리
        Assert.Greater(PxArt.Apply(fill, PxArt.Tone.Hover).r, fill.r, "마우스 올림은 밝게");
        Assert.Less(PxArt.Apply(fill, PxArt.Tone.Pressed).r, fill.r, "눌림은 어둡게");
        Assert.AreEqual(ink, PxArt.Apply(ink, PxArt.Tone.Hover), "테두리는 그대로");
        var off = PxArt.Apply(fill, PxArt.Tone.Disabled);
        Assert.Less(Mathf.Abs(off.r - off.b), Mathf.Abs(fill.r - fill.b), "비활성은 회색 쪽으로");
        Assert.AreEqual(0f, PxArt.Apply(new Color(1f, 1f, 1f, 0f), PxArt.Tone.Hover).a, "투명은 투명");
    }

    [UnityTest]
    public IEnumerator PauseMenu_ShowsOnlyWhilePaused()
    {
        var pause = pc.GetComponent<PauseMenu>();
        yield return null;
        Assert.IsFalse(pause.IsShown, "사냥 중엔 안 보인다");

        GameState.Set(GameScene.Pause);
        yield return null;
        Assert.IsTrue(pause.IsShown, "일시정지면 보인다");
        Assert.AreEqual(0, pause.NavIndex, "처음엔 ▶(계속)를 고른다");

        GameState.Set(GameScene.Run);
        yield return null;
        Assert.IsFalse(pause.IsShown);
    }

    [UnityTest]
    public IEnumerator Settings_TakesEscapeFirst_SoPauseIsNotResumed()
    {
        GameState.Set(GameScene.Pause);
        var settings = pc.GetComponent<SettingsWindow>();
        SettingsWindow.Open();
        yield return null;
        Assert.IsTrue(settings.IsShown);
        Assert.IsTrue(UiModal.OwnsEscape, "설정 창이 떠 있으면 ESC는 창 몫(GameFlow가 일시정지를 안 푼다)");

        SettingsWindow.Close(); // ESC로 닫을 때처럼 — 창은 이 프레임 끝(LateUpdate)까지 남아 있다
        Assert.IsTrue(UiModal.OwnsEscape, "닫는 그 프레임의 ESC는 아직 창 몫");
        yield return null;
        yield return null;
        Assert.IsFalse(settings.IsShown);
        Assert.IsFalse(UiModal.OwnsEscape, "닫힌 뒤엔 일시정지 ESC가 다시 돈다");
    }

    [UnityTest]
    public IEnumerator UiModal_StillOwnsEscape_OnTheFrameItClosed()
    {
        UiModal.Push();
        Assert.IsTrue(UiModal.Open);
        UiModal.Pop();
        Assert.IsFalse(UiModal.Open);
        Assert.IsTrue(UiModal.OwnsEscape, "닫은 그 프레임 — Update 순서와 상관없이 같은 ESC로 일시정지가 안 풀리게");
        yield return null;
        Assert.IsFalse(UiModal.OwnsEscape);
    }

    [UnityTest]
    public IEnumerator CombatHud_RedCellIsHp_ExpAtBottom_FollowsGameScene()
    {
        var hud = pc.GetComponent<CombatHud>();
        hud.Preview = new CombatHud.PreviewState { Hp = 50f, MaxHp = 100f, ExpRatio = 0.5f, TimeLeft = 47.2f, Combo = 0 };
        yield return null;
        Assert.IsTrue(hud.IsShown, "사냥 중엔 HUD");
        Assert.AreEqual((66f + 62f) / 192f, hud.HpFillAmount, 1e-4f, "빨간 칸(틀 그림 x 66~188)을 체력 비율만큼, 도트 단위로");
        Assert.AreEqual("50/100", hud.HpLabel);
        Assert.AreEqual("48", hud.TimerLabel, "남은 초는 올림(원본 `Math.ceil`)");
        Assert.AreEqual(319f, hud.ExpFillWidth, 1e-4f, "경험치 = 맨 아래 바(안쪽 638도트)의 비율만큼");
        Assert.IsFalse(hud.ComboShown, "콤보 0이면 숨김(원본)");
        Assert.IsFalse(hud.BossBarShown, "보스가 없으면 보스 체력바 없음");
        Assert.AreEqual(FieldLayout.Platforms.GetLength(0) + 1, hud.MinimapLineCount, "미니맵 = 발판마다 한 줄 + 땅");

        hud.Preview = new CombatHud.PreviewState { Hp = 0f, MaxHp = 100f, Combo = 12, BossRatio = 0.3f };
        yield return null;
        Assert.AreEqual(66f / 192f, hud.HpFillAmount, 1e-4f, "체력 0이면 빨간 칸이 하나도 안 남는다");
        Assert.IsTrue(hud.ComboShown);
        Assert.IsTrue(hud.BossBarShown);

        GameState.Set(GameScene.Lobby);
        yield return null;
        Assert.IsFalse(hud.IsShown, "로비에선 HUD 없음(원본도 로비에서 숨긴다)");
    }

    [UnityTest]
    public IEnumerator Title_ShowsOnTitle_LoadNeedsSave_PopupConfirmRunsCallback()
    {
        GameState.Set(GameScene.Title);
        var title = pc.GetComponent<TitleMenu>();
        yield return null;
        Assert.IsTrue(title.IsShown, "타이틀 화면");
        Assert.AreEqual(0, title.NavIndex, "처음엔 '새 원정'");

        var popup = pc.GetComponent<CommonPopup>();
        bool confirmed = false;
        CommonPopup.Confirm("새 원정", "지울까요?", () => confirmed = true, "시작");
        yield return null;
        Assert.IsTrue(popup.IsShown, "공용 팝업이 뜬다");
        Assert.IsTrue(UiModal.OwnsEscape, "팝업이 떠 있으면 ESC는 팝업 몫");
        PxButton ok = null;
        foreach (var b in popup.Root.GetComponentsInChildren<PxButton>()) if (b.name == "Ok") ok = b;
        Assert.IsNotNull(ok);
        ok.Click();
        Assert.IsTrue(confirmed, "확인을 누르면 콜백");
        yield return null;
        Assert.IsFalse(popup.IsShown, "누르면 닫힌다");
    }

    [UnityTest]
    public IEnumerator Awaken_FramesFollowBranch_FirstTierAsks_ThenLearns()
    {
        GameState.Set(GameScene.Lobby);
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        p.level = 4; // SP 3
        var awaken = pc.GetComponent<AwakenScreen>();
        AwakenScreen.Open();
        yield return null;
        Assert.IsTrue(awaken.IsShown);
        StringAssert.StartsWith("node_open", awaken.NodeFrameName(0, 0), "아직 갈래를 안 골랐으면 두 줄 다 은테");
        StringAssert.StartsWith("node_open", awaken.NodeFrameName(1, 0));
        StringAssert.StartsWith("node_locked", awaken.NodeFrameName(0, 5), "6번째 칸은 아직 없는 층");

        awaken.ClickNodeForTest(0, 0); // 1줄(중력) 1층 — 갈래가 정해지므로 한 번 더 묻는다
        Assert.AreEqual(0, p.mageTier, "묻기 전엔 안 배운다");
        PxButton ok = null;
        foreach (var b in pc.GetComponent<CommonPopup>().Root.GetComponentsInChildren<PxButton>(true)) if (b.name == "Ok") ok = b;
        yield return null;
        ok.Click();
        Assert.AreEqual(MageBranch.Gravity, p.mageBranch);
        Assert.AreEqual(1, p.mageTier);
        yield return null;
        yield return null;
        StringAssert.StartsWith("node_learned", awaken.NodeFrameName(0, 0), "배운 층은 금테");
        StringAssert.StartsWith("node_locked", awaken.NodeFrameName(1, 0), "다른 갈래는 회색");

        awaken.ClickNodeForTest(0, 1); // 2층(SP 2) — 이미 갈래가 정해져 바로 배운다
        Assert.AreEqual(2, p.mageTier);
        AwakenScreen.Close();
    }

    [Test]
    public void Lobby_BuildingsInOrder_DoomDaysCountDown()
    {
        Assert.AreEqual(0, LobbyWorld.Nearest(LobbyWorld.XOf(0)), "환생 건물 앞");
        Assert.AreEqual(-1, LobbyWorld.Nearest(LobbyWorld.CenterX), "가운데(나오는 곳)는 건물 사이라 들어갈 곳이 없다");
        Assert.AreEqual(3, LobbyWorld.Nearest(LobbyWorld.XOf(3) - 0.3f), "던전 건물 앞");
        Assert.LessOrEqual(LobbyWorld.XOf(3) - LobbyWorld.XOf(0) + 0.75f, 7.5f, "건물 넷이 한 화면(7.5유닛)에 들어간다");
        Assert.AreEqual(new[] { "환생", "가챠", "훈련소", "던전" }, System.Array.ConvertAll(LobbyWorld.Buildings, b => b.Name));

        var p = new PlayerProfile();
        Assert.AreEqual(7, p.doomDays, "멸망까지 7일로 시작(사용자 결정)");
        for (int i = 0; i < 7; i++) { Assert.IsTrue(p.CanEnterStage); p.SpendDay(); }
        Assert.AreEqual(0, p.doomDays);
        Assert.IsFalse(p.CanEnterStage, "0일이면 스테이지에 못 들어간다");
        p.SpendDay();
        Assert.AreEqual(0, p.doomDays, "0 밑으로는 안 내려간다");
    }

    [Test]
    public void FigmaArt_AndFont_AreInResources()
    {
        Assert.IsNotNull(Px.Font, "DNF Bit Bit v2 글꼴");
#if UNITY_EDITOR
        var fontImporter = (UnityEditor.TrueTypeFontImporter)UnityEditor.AssetImporter.GetAtPath("Assets/Resources/Fonts/DNFBitBitv2.ttf");
        Assert.AreEqual(UnityEditor.FontRenderingMode.HintedRaster, fontImporter.fontRenderingMode,
                        "글자 가장자리가 번지면 안 된다(10/25 사용자 \"UI가 흐릿하다\")");
#endif
        foreach (var path in new[] { "Pause/band", "Common/title_plate", "Common/icon_frame", "Common/window_frame",
                                     "Common/button", "Common/button_small", "Icons/play", "Icons/gear", "Icons/exit",
                                     "Icons/close", "Hud/status_frame", "Hud/slot_frame", "Hud/icon_attack",
                                     "Hud/icon_skill", "Hud/icon_jump", "Hud/boss_bar", "Hud/portrait", "Common/cursor" })
        {
            var s = PxArt.Get(path);
            Assert.IsNotNull(s, path);
            Assert.AreEqual(FilterMode.Point, s.texture.filterMode, path + " — 도트가 번지면 안 된다");
            Assert.IsTrue(s.texture.isReadable, path + " — 버튼 상태 그림을 만들려면 읽을 수 있어야 한다");
        }
        Assert.AreEqual(new Vector2(640f, 128f), PxArt.Get("Pause/band").rect.size, "Figma 원본 크기 그대로");
    }
}

}
