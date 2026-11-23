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

    [SetUp]
    public void Setup()
    {
        // 화면 버튼이 저장까지 부른다 — 세이브는 테스트 전체 임시 경로(`TestSaveSandbox`)로 간다. 지난 테스트가 남긴 파일은 지운다.
        // 안전장치가 안 켜졌으면 지우기 전에 멈춘다(진짜 세이브를 지우지 않게).
        Assert.AreEqual(TestSaveSandbox.Path, SaveService.SavePath, "테스트 세이브 안전장치(TestSaveSandbox)가 꺼져 있다");
        if (File.Exists(SaveService.SavePath)) File.Delete(SaveService.SavePath);
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
        if (SaveService.SavePath == TestSaveSandbox.Path && File.Exists(SaveService.SavePath)) File.Delete(SaveService.SavePath);
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

    /// <summary>화면을 옮기는 동안엔 화면이 입력을 받지 않는다 — 마우스(판의 GraphicRaycaster)·키(PxScreen) 모두. 끝나면 다시 받는다(4-0).</summary>
    [UnityTest]
    public IEnumerator WhileTransitioning_ScreensTakeNoInput_ThenResume()
    {
        var raycaster = pc.GetComponent<UnityEngine.UI.GraphicRaycaster>();
        try
        {
            GameState.SetTransitioning(true);
            yield return null;
            Assert.IsFalse(PixelCanvas.AcceptsInput);
            Assert.IsFalse(raycaster.enabled, "전환 중엔 클릭을 받지 않는다");

            GameState.SetTransitioning(false);
            yield return null;
            Assert.IsTrue(PixelCanvas.AcceptsInput);
            Assert.IsTrue(raycaster.enabled, "끝나면 다시 받는다");
        }
        finally { GameState.SetTransitioning(false); }
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

    /// <summary>
    /// 프리팹 UI 시범(2026-11-22) — 일시정지 모양은 프리팹(`Resources/UI/Views/PauseMenuView`)에서 온다.
    /// 프리팹이 없으면 화면은 코드 모양으로 대신 지어져 겉으론 멀쩡하므로, 빠진 걸 이 테스트가 잡는다
    /// (처음엔 메뉴 `ChronoSavior/UI 프리팹/일시정지 화면 만들기`로 만들어야 통과한다).
    /// </summary>
    [UnityTest]
    public IEnumerator PauseMenu_LayoutComesFromViewPrefab()
    {
        Assert.IsNotNull(Resources.Load<PauseMenuView>(PauseMenuView.ResourcePath),
                         "일시정지 화면 프리팹이 없다 — 메뉴 ChronoSavior/UI 프리팹/일시정지 화면 만들기");
        var pause = pc.GetComponent<PauseMenu>();
        GameState.Set(GameScene.Pause);
        yield return null;
        Assert.IsTrue(pause.UsesViewPrefab, "모양을 프리팹에서 불러온다");
        Assert.AreEqual(1, pause.Root.GetComponentsInChildren<PauseMenuView>(true).Length, "모양은 한 벌뿐(코드로 또 짓지 않는다)");
        Assert.AreEqual(3, pause.Root.GetComponentsInChildren<PxButton>(true).Length, "버튼 셋(계속·설정·로비로)뿐 — 두 벌로 생기지 않는다");
    }

    /// <summary>
    /// 일시정지 버튼 셋이 예전과 같은 순서·동작으로 연결된다(프리팹이든 대체 코드 모양이든) —
    /// 마우스를 올리면 그 버튼이 키보드 선택이 되고(<see cref="PxScreen.NavIndex"/>), ⚙는 설정 창을 연다.
    /// ▶·↩는 `GameFlow`를 부르는데 테스트 판엔 흐름이 없어(<see cref="PixelCanvas.Flow"/> null) 누르기만 해 본다.
    /// </summary>
    [UnityTest]
    public IEnumerator PauseMenu_ButtonsKeepOrderAndActions()
    {
        var pause = pc.GetComponent<PauseMenu>();
        GameState.Set(GameScene.Pause);
        yield return null;

        var buttons = pause.Root.GetComponentsInChildren<PxButton>(true);
        Assert.AreEqual(3, buttons.Length);
        for (int i = 0; i < buttons.Length; i++)
        {
            Assert.IsNotNull(buttons[i].Art, $"버튼 {i} 그림이 연결됐다(상태 그림을 만들 수 있게)");
            Assert.IsNotNull(buttons[i].Inner, $"버튼 {i} 안쪽(눌림 1도트)이 연결됐다");
            buttons[i].OnHover?.Invoke();
            Assert.AreEqual(i, pause.NavIndex, $"버튼 {i}에 마우스를 올리면 키보드 선택도 {i}번 — 순서 ▶ ⚙ ↩");
        }

        Assert.IsFalse(SettingsWindow.IsOpen);
        buttons[1].Click();
        Assert.IsTrue(SettingsWindow.IsOpen, "⚙는 설정 창을 연다");
        SettingsWindow.Close();

        Assert.DoesNotThrow(() => buttons[0].Click(), "▶ 계속 — 흐름이 없는 판에서도 터지지 않는다");
        Assert.DoesNotThrow(() => buttons[2].Click(), "↩ 로비로 — 흐름이 없는 판에서도 터지지 않는다");
        yield return null;
    }

    /// <summary>
    /// 프리팹 UI 3호(2026-11-23) — 타이틀 모양은 프리팹(`Resources/UI/Views/TitleMenuView`)에서 온다.
    /// 프리팹이 없으면 코드 모양으로 대신 지어져 겉으론 멀쩡하므로, 빠진 걸 이 테스트가 잡는다
    /// (처음엔 메뉴 `ChronoSavior/UI 프리팹/타이틀 화면 만들기`로 만들어야 통과한다).
    /// </summary>
    [UnityTest]
    public IEnumerator Title_LayoutComesFromViewPrefab_OneSetOnly()
    {
        Assert.IsNotNull(Resources.Load<TitleMenuView>(TitleMenuView.ResourcePath),
                         "타이틀 프리팹이 없다 — 메뉴 ChronoSavior/UI 프리팹/타이틀 화면 만들기");
        var title = pc.GetComponent<TitleMenu>();
        GameState.Set(GameScene.Title);
        yield return null;
        Assert.IsTrue(title.UsesViewPrefab, "모양을 프리팹에서 불러온다");
        Assert.AreEqual(1, title.Root.GetComponentsInChildren<TitleMenuView>(true).Length, "모양은 한 벌뿐(코드로 또 짓지 않는다)");
        Assert.AreEqual(4, title.Root.GetComponentsInChildren<PxButton>(true).Length, "메뉴 판 넷뿐 — 두 벌로 생기지 않는다");
    }

    /// <summary>
    /// 타이틀 메뉴가 예전과 같은 순서·글자·동작이고(프리팹이든 대체 코드 모양이든) ◆가 고른 메뉴를 따라간다 —
    /// 마우스를 올리면 그 메뉴가 키보드 선택이 되고(↑/↓ 순서 새 원정 → 불러오기 → 설정 → 나가기), ◆는 그 판의 세로 가운데·글자 왼쪽에 붙는다.
    /// "설정"은 설정 창을 연다. 좌표 자체는 고정하지 않는다(프리팹에서 옮겨도 통과해야 하므로 상대 위치만 본다).
    /// </summary>
    [UnityTest]
    public IEnumerator Title_MenusKeepOrderAndActions_CursorFollows()
    {
        var title = pc.GetComponent<TitleMenu>();
        GameState.Set(GameScene.Title);
        yield return null;

        string[] expected = { "새 원정", "불러오기", "설정", "나가기" };
        var buttons = title.Root.GetComponentsInChildren<PxButton>(true);
        Assert.AreEqual(4, buttons.Length);
        RectTransform cursor = null;
        foreach (var rt in title.Root.GetComponentsInChildren<RectTransform>(true)) if (rt.name == "Cursor") cursor = rt;
        Assert.IsNotNull(cursor, "◆ 커서");

        for (int i = 0; i < buttons.Length; i++)
        {
            Assert.AreEqual(expected[i], buttons[i].GetComponentInChildren<UnityEngine.UI.Text>(true).text, $"메뉴 {i} 글자");
            buttons[i].OnHover?.Invoke();
            Assert.AreEqual(i, title.NavIndex, $"메뉴 {i}에 마우스를 올리면 키보드 선택도 {i}번");

            var plate = (RectTransform)buttons[i].transform;
            float plateMid = -plate.anchoredPosition.y + plate.sizeDelta.y / 2f;
            float cursorMid = -cursor.anchoredPosition.y + cursor.sizeDelta.y / 2f;
            Assert.AreEqual(plateMid, cursorMid, 1f, $"◆가 메뉴 {i} 판의 세로 가운데");
            Assert.Less(cursor.anchoredPosition.x + cursor.sizeDelta.x, plate.anchoredPosition.x + plate.sizeDelta.x / 2f,
                        $"◆가 메뉴 {i} 글자 왼쪽");
        }

        Assert.IsFalse(SettingsWindow.IsOpen);
        buttons[2].Click();
        Assert.IsTrue(SettingsWindow.IsOpen, "설정은 설정 창을 연다");
        SettingsWindow.Close();
        yield return null;
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
    public IEnumerator CombatHud_RedBarIsHp_GoldBarShown_ExpAtBottom_FollowsGameScene()
    {
        var hud = pc.GetComponent<CombatHud>();
        hud.Preview = new CombatHud.PreviewState { Hp = 50f, MaxHp = 100f, ExpRatio = 0.5f, TimeLeft = 47.2f, Combo = 0, Gold = 12345 };
        yield return null;
        Assert.IsTrue(hud.IsShown, "사냥 중엔 HUD");
        Assert.AreEqual(0.5f, hud.HpFillAmount, 1e-4f, "체력 막대 안쪽 칸(156도트)을 체력 비율만큼, 도트 단위로");
        Assert.AreEqual("12,345", hud.GoldLabel, "골드 막대(2026-11-22 새 HUD) — 천 단위 쉼표");
        Assert.AreEqual("50/100", hud.HpLabel);
        Assert.AreEqual("48", hud.TimerLabel, "남은 초는 올림(원본 `Math.ceil`)");
        Assert.AreEqual(319f, hud.ExpFillWidth, 1e-4f, "경험치 = 맨 아래 바(안쪽 638도트)의 비율만큼");
        Assert.IsFalse(hud.ComboShown, "콤보 0이면 숨김(원본)");
        Assert.IsFalse(hud.BossBarShown, "보스가 없으면 보스 체력바 없음");
        Assert.AreEqual(FieldLayout.Platforms.GetLength(0) + 1, hud.MinimapLineCount, "미니맵 = 발판마다 한 줄 + 땅");

        hud.Preview = new CombatHud.PreviewState { Hp = 0f, MaxHp = 100f, Combo = 12, BossRatio = 0.3f, Buffs = "살기 x3  차지 50%" };
        yield return null;
        Assert.AreEqual(0f, hud.HpFillAmount, 1e-4f, "체력 0이면 빨간 칸이 하나도 안 남는다");
        Assert.AreEqual("살기 x3  차지 50%", hud.BuffsLabel, "버프 줄(원본 #buffs)이 상태창 아래에 뜬다");
        Assert.IsTrue(hud.ComboShown);
        Assert.IsTrue(hud.BossBarShown);

        GameState.Set(GameScene.Lobby);
        yield return null;
        Assert.IsFalse(hud.IsShown, "로비에선 HUD 없음(원본도 로비에서 숨긴다)");
    }

    /// <summary>
    /// 보스 체력바는 **상단 가운데, 보스 이름 아래**(2026-11-10 사용자 A안 — 예전엔 화면 한가운데를 가로질렀다). 왼쪽 위 상태창과 안 겹친다.
    /// 처음엔 0에서 차오르고, 맞으면 연노랑 잔상이 남았다 따라 줄어든다.
    /// </summary>
    [UnityTest]
    public IEnumerator CombatHud_BossBar_TopCenterUnderName_IntroAndDamageTrail()
    {
        var hud = pc.GetComponent<CombatHud>();
        RunState.Begin(1, RunMode.Boss);
        hud.Preview = new CombatHud.PreviewState { Hp = 50f, MaxHp = 100f, BossRatio = 1f };
        yield return null;
        Assert.IsTrue(hud.BossBarShown);
        Assert.AreEqual("덩어리 지배자", hud.BossNameLabel, "1지역 보스 이름이 바 위에");
        var r = hud.BossBarRect;
        Assert.AreEqual(320f, r.anchoredPosition.x + r.sizeDelta.x / 2f, 0.6f, "가로 가운데");
        Assert.Less(-r.anchoredPosition.y + r.sizeDelta.y, 120f, "화면 위쪽(640×360 기준 y 120 위)");
        Assert.GreaterOrEqual(-r.anchoredPosition.y, 44f, "왼쪽 위 플레이어 판(y 4~44) 아래라 안 겹친다");
        Assert.Less(hud.BossFillWidth, 394f, "첫 프레임은 아직 차오르는 중");

        yield return new WaitForSeconds(CombatHud.BossIntroTime + 0.1f);
        Assert.AreEqual(394f, hud.BossFillWidth, 0.6f, "다 찼다");

        hud.Preview = new CombatHud.PreviewState { Hp = 50f, MaxHp = 100f, BossRatio = 0.5f };
        yield return null;
        yield return null;
        Assert.AreEqual(197f, hud.BossFillWidth, 0.6f, "빨강은 바로 깎인다");
        Assert.Greater(hud.BossTrailWidth, 255f, "잔상은 아직 남아 있다");
        yield return new WaitForSeconds(CombatHud.BossTrailTime + 0.1f);
        Assert.AreEqual(197f, hud.BossTrailWidth, 0.6f, "잔상이 따라왔다");
        RunState.Reset();
    }

    [UnityTest]
    public IEnumerator Title_ShowsOnTitle_LoadNeedsSave_PopupConfirmRunsCallback()
    {
        // 실제 마우스가 에디터 Game 창의 메뉴 칸 위에 있으면 EventSystem이 "마우스 올림"을 보내 고른 칸이 바뀐다(`PxButton.OnHover`) —
        // 처음 고른 칸을 보는 이 검사엔 사람 손이 섞이면 안 되므로 포인터 이벤트를 끈다(2026-11-22 "처음엔 새 원정 Expected 0 But was 2").
        var es = UnityEngine.EventSystems.EventSystem.current;
        if (es != null) es.enabled = false;
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
        StringAssert.StartsWith("node_learned", awaken.NodeFrameName(0, 0), "배운 층은 흰 테두리");
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
                                     "Icons/close", "Hud/player_hp_bg", "Hud/player_hp_bar", "Hud/gold_bar", "Hud/boss_hp_bg", "Hud/boss_hp_bar",
                                     "Hud/boss_skull", "Hud/minimap_bg", "Hud/slot_frame", "Hud/icon_attack",
                                     "Hud/icon_skill", "Hud/icon_jump", "Common/cursor" })
        {
            var s = PxArt.Get(path);
            Assert.IsNotNull(s, path);
            Assert.AreEqual(FilterMode.Point, s.texture.filterMode, path + " — 도트가 번지면 안 된다");
            Assert.IsTrue(s.texture.isReadable, path + " — 버튼 상태 그림을 만들려면 읽을 수 있어야 한다");
        }
        Assert.AreEqual(new Vector2(640f, 128f), PxArt.Get("Pause/band").rect.size, "Figma 원본 크기 그대로");
    }

    /// <summary>버프 줄 문구 — 원본 `buffTxt`(:6283~)와 같은 조건: 아무 것도 안 걸려 있으면 빈 줄, 피해 배율은 1.01 넘을 때만.</summary>
    [Test]
    public void BuffLine_FollowsOriginalConditions()
    {
        CombatModifiers.Reset();
        RunState.Reset();
        Assert.AreEqual("", CombatHud.BuffLine(null), "걸린 게 없으면 빈 줄(원본도 빈 문자열)");
        CombatModifiers.GrantShrineBuff(5f);
        string line = CombatHud.BuffLine(null);
        StringAssert.Contains("성소의 가호", line, "성소 버프가 걸리면 남은 초를 보여 준다");
        StringAssert.Contains("피해 ×", line, "성소 버프는 피해 배율을 올리므로 배율도 뜬다");
        CombatModifiers.Reset();
    }
}

}
