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
        "Settings/icon_screen", "Settings/icon_resolution", "Settings/icon_master", "Settings/icon_bgm", "Settings/icon_sfx",
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

    /// <summary>
    /// 2026-11-18 Figma 새 도트 아이콘 — 마법사 스킬 10개(30×30 흰 도트)·유물 자물쇠(32×32). 안 깨지게: 점 필터·밉맵 없음·원본 크기 그대로,
    /// 각성창은 30×30 칸에 1:1(32 칸 가운데 1도트 안쪽), 유물창은 아직 없는 유물 칸에 자물쇠(예전 "?" 글자 대신).
    /// </summary>
    [UnityTest]
    public IEnumerator NewDotIcons_DrawnOneToOne()
    {
        foreach (var branch in new[] { "explosion", "gravity" })
            for (int t = 1; t <= 5; t++)
            {
                var s = PxArt.Get($"Skills/mage_{branch}_{t}");
                Assert.IsNotNull(s, $"mage_{branch}_{t}");
                Assert.AreEqual(new Vector2(30f, 30f), s.rect.size, $"mage_{branch}_{t}: 원본 30×30");
                Assert.AreEqual(FilterMode.Point, s.texture.filterMode, $"mage_{branch}_{t}: 점 필터");
                Assert.AreEqual(1, s.texture.mipmapCount, $"mage_{branch}_{t}: 밉맵 없음");
            }
        var lockIcon = PxArt.Get("Relic/lock");
        Assert.IsNotNull(lockIcon);
        Assert.AreEqual(new Vector2(32f, 32f), lockIcon.rect.size);
        Assert.AreEqual(FilterMode.Point, lockIcon.texture.filterMode);

        ProfileService.Current.character = CharacterId.Mage;
        var awaken = pc.GetComponent<AwakenScreen>();
        awaken.ShowForCapture();
        yield return null;
        int checkedIcons = 0;
        foreach (var img in awaken.Root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            if (img.name == "Icon" && img.sprite != null && img.sprite.name.StartsWith("mage_"))
            {
                Assert.AreEqual(new Vector2(30f, 30f), img.rectTransform.sizeDelta, img.sprite.name + " — 30×30 칸(1:1)");
                checkedIcons++;
            }
        Assert.AreEqual(10, checkedIcons, "마법사 각성창에 아이콘 10개");
        awaken.HideForCapture();

        var relic = pc.GetComponent<RelicScreen>();
        relic.ShowForCapture();
        yield return null;
        int locksShown = 0;
        foreach (var img in relic.Root.GetComponentsInChildren<UnityEngine.UI.Image>(false))
            if (img.name == "Lock" && img.enabled) { Assert.AreSame(lockIcon, img.sprite); Assert.AreEqual(new Vector2(32f, 32f), img.rectTransform.sizeDelta); locksShown++; }
        Assert.Greater(locksShown, 0, "새 프로필 — 아직 없는 유물 칸에 자물쇠");
        relic.HideForCapture();
    }

    /// <summary>
    /// 2026-11-22 드라이브 새 스킬 아이콘 — 살수(집중·칼날폭풍)·엔지니어(레이저·설치기). 30×30 점 필터, 각성창 칸에 1:1.
    /// 칼날폭풍 3층(벽 타기)만 그림이 없어 층 번호가 보인다 — 그림이 오면 이 예외를 지울 것.
    /// </summary>
    [UnityTest]
    public IEnumerator SkillIcons_BladeAndGunner_DrawnOneToOne()
    {
        var names = new System.Collections.Generic.List<string>();
        for (int t = 1; t <= 5; t++)
        {
            names.Add($"blade_focus_{t}");
            if (t != 3) names.Add($"blade_storm_{t}");
            names.Add($"gunner_laser_{t}");
            names.Add($"gunner_installer_{t}");
        }
        foreach (var n in names)
        {
            var s = PxArt.Get("Skills/" + n);
            Assert.IsNotNull(s, n);
            Assert.AreEqual(new Vector2(30f, 30f), s.rect.size, n + ": 원본 30×30");
            Assert.AreEqual(FilterMode.Point, s.texture.filterMode, n + ": 점 필터");
            Assert.AreEqual(1, s.texture.mipmapCount, n + ": 밉맵 없음");
        }

        var awaken = pc.GetComponent<AwakenScreen>();
        foreach (var (id, prefix, expected) in new[] { (CharacterId.Blade, "blade_", 9), (CharacterId.Gunner, "gunner_", 10) })
        {
            ProfileService.Current.character = id;
            awaken.ShowForCapture();
            yield return null;
            int shown = 0;
            foreach (var img in awaken.Root.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                if (img.name == "Icon" && img.enabled && img.sprite != null && img.sprite.name.StartsWith(prefix))
                {
                    Assert.AreEqual(new Vector2(30f, 30f), img.rectTransform.sizeDelta, img.sprite.name + " — 30×30 칸(1:1)");
                    shown++;
                }
            Assert.AreEqual(expected, shown, id + " 각성창에 보이는 아이콘 수");
            awaken.HideForCapture();
        }
    }

    /// <summary>
    /// 2026-11-22 각성창 [스탯] 탭 — 위 패널 스탯 바 5줄, 아래 창 강화 목록·상세. 구매 단위(×1/×10/최대)는 원본 `previewGoldUpgrade`·`buyGoldUpgrade` 그대로:
    /// 1·10은 골드가 모자라도 그 수만큼 셈하고(못 누르게만 한다), 최대는 가진 골드로 살 수 있는 만큼, 산 뒤 값은 지금 값과 같은 식을 거친다.
    /// </summary>
    [UnityTest]
    public IEnumerator StatTab_BarsAndUpgradeDetail_FollowOriginalBuyRules()
    {
        var prof = ProfileService.Current;
        prof.character = CharacterId.Mage;
        prof.level = 12;
        prof.gold = 0;
        var awaken = pc.GetComponent<AwakenScreen>();
        awaken.ShowForCapture();
        awaken.ShowTabForTest(true);
        yield return null;

        Assert.AreEqual("LV.12", awaken.StatValueText(0));
        Assert.AreEqual("100", awaken.StatValueText(1), "최대체력(체력 강화 0)");
        Assert.AreEqual("10", awaken.StatValueText(2), "공격력(강화 0)");
        Assert.AreEqual("100%", awaken.StatValueText(3), "이동속도");

        // ×10: 골드 0이어도 "LV.0 → LV.10"을 보이되 못 누른다
        awaken.PickUpgradeForTest(0, 1);
        Assert.AreEqual("최대 체력", awaken.DetailNameText);
        Assert.AreEqual("LV.0", awaken.BeforeLevelText);
        Assert.AreEqual("LV.10", awaken.AfterLevelText);
        Assert.IsFalse(awaken.BuyEnabled, "골드가 모자라면 못 누른다");

        // 최대: 골드 0이면 0단계 — 상세는 그대로 LV.0, 못 누른다
        awaken.PickUpgradeForTest(0, 2);
        Assert.AreEqual("LV.0", awaken.AfterLevelText);
        Assert.IsFalse(awaken.BuyEnabled);

        // 골드 50 — 20 + 22 = 42 ≤ 50, 다음 25는 모자라 2단계(원본 previewGoldUpgrade)
        prof.gold = 50;
        var pv = GoldUpgradeConfig.Preview(UpgradeStat.Hp, 0, prof.gold, GoldUpgradeConfig.BuyMax);
        Assert.AreEqual(2, pv.count);
        Assert.AreEqual(GoldUpgradeConfig.Cost(UpgradeStat.Hp, 0) + GoldUpgradeConfig.Cost(UpgradeStat.Hp, 1), pv.cost);
        awaken.PickUpgradeForTest(0, 2);
        Assert.AreEqual("LV.2", awaken.AfterLevelText);
        Assert.IsTrue(awaken.BuyEnabled);

        // 산 뒤 값은 실제로 산 뒤의 스탯과 같다(미리보기와 어긋나면 안 된다)
        float predicted = PlayerStatCalculator.StatAtUpgradeLevel(prof, UpgradeStat.Hp, 2);
        awaken.BuyForTest();
        Assert.AreEqual(2, prof.GetUpgradeLevel(UpgradeStat.Hp));
        Assert.AreEqual(50 - pv.cost, prof.gold);
        Assert.AreEqual(predicted, PlayerStatCalculator.ComputeMaxHp(prof));
        Assert.AreEqual("150", awaken.StatValueText(1), "체력 2단계 = 100 + 2×25");

        // ×10: 충분한 골드면 정확히 10단계, 합계만큼만 깎인다
        prof.gold = 5000;
        long expect = 0;
        for (int i = 0; i < 10; i++) expect += GoldUpgradeConfig.Cost(UpgradeStat.Atk, 0 + i);
        awaken.PickUpgradeForTest(1, 1);
        awaken.BuyForTest();
        Assert.AreEqual(10, prof.GetUpgradeLevel(UpgradeStat.Atk));
        Assert.AreEqual(5000 - expect, prof.gold);

        awaken.HideForCapture();
    }

    /// <summary>강화 목록은 세 줄만 온전히 보이고, 고른 줄이 안 보이면 목록이 밀린다(스크롤 표시 따라감).</summary>
    [UnityTest]
    public IEnumerator StatTab_ListScrollsToKeepSelectionVisible()
    {
        var awaken = pc.GetComponent<AwakenScreen>();
        awaken.ShowForCapture();
        awaken.ShowTabForTest(true);
        yield return null;
        var rows = new System.Collections.Generic.List<RectTransform>();
        foreach (var b in awaken.Root.GetComponentsInChildren<PxButton>(true))
            if (b.name.StartsWith("Row")) rows.Add((RectTransform)b.transform);
        Assert.AreEqual(5, rows.Count);
        Assert.AreEqual(0f, rows[0].anchoredPosition.y, "처음엔 맨 위 줄이 목록 맨 위");
        awaken.PickUpgradeForTest(4, 0); // 마지막 강화(치명타)를 고르면
        Assert.AreEqual(-56f, rows[4].anchoredPosition.y, "목록이 밀려 마지막 줄이 세 번째 자리(2×28)에 온다");

        // 마우스 휠로 위아래를 오간다 — 아래로 밀려도 되돌아올 수 있어야 한다(사용자 2026-11-22). 고른 줄은 그대로.
        awaken.ScrollListForTest(true);
        awaken.ScrollListForTest(true);
        awaken.ScrollListForTest(true);
        Assert.AreEqual(0, awaken.ListTop, "휠을 위로 굴리면 맨 위까지 돌아온다(더 안 내려간다)");
        awaken.ScrollListForTest(false);
        awaken.ScrollListForTest(false);
        awaken.ScrollListForTest(false);
        Assert.AreEqual(2, awaken.ListTop, "아래로 굴려도 끝(5줄 − 3줄)에서 멈춘다");
        awaken.PickUpgradeForTest(0, 0); // 맨 위 줄을 고르면(키보드·클릭) 다시 보이게 올라간다
        Assert.AreEqual(0, awaken.ListTop);
        awaken.HideForCapture();
    }

    /// <summary>
    /// 2026-11-22 유물 소환창 — 6줄×5열 = 30칸 한 쪽(35종이라 두 쪽), 오른쪽 보유 시간의 파편·보유 유물 수, 확률 판(원본 값 55/30/12/3), [1]·[10].
    /// 아직 없는 유물은 어두운 칸 + 자물쇠.
    /// </summary>
    [UnityTest]
    public IEnumerator Relic_Layout_30SlotsPerPage_TwoPages_UnownedIsDarkWithLock()
    {
        var prof = ProfileService.Current;
        prof.shards = 1234;
        var relic = pc.GetComponent<RelicScreen>();
        relic.ShowForCapture();
        yield return null;

        Assert.AreEqual(30, relic.VisibleSlotCount, "첫 쪽은 30칸");
        Assert.AreEqual($"보유 유물 수: 0/{ChronoSavior.Core.ItemDatabase.All.Length}", relic.OwnedCountText);
        Assert.AreEqual("보유 시간의 파편: 1,234", relic.ShardText);
        relic.PageForTest(1);
        Assert.AreEqual(ChronoSavior.Core.ItemDatabase.All.Length - 30, relic.VisibleSlotCount, "둘째 쪽은 남은 유물만");

        int slots = 0;
        foreach (var b in relic.Root.GetComponentsInChildren<PxButton>(true))
            if (b.name.StartsWith("Slot")) { slots++; Assert.IsTrue(b.Art.sprite.name.StartsWith("slot_off"), "처음엔 전부 없는 유물 — 어두운 칸"); }
        Assert.AreEqual(30, slots);
        foreach (var t in relic.Root.GetComponentsInChildren<UnityEngine.UI.Text>(true))
            if (t.name == "RateValue0") Assert.AreEqual("55%", t.text, "노말 확률은 원본 가중치 55/100");
        relic.HideForCapture();
    }

    /// <summary>2026-11-22 회귀 결과창 — 새 프로필: 이전 최고 stage 1 → 회귀 후 stage 1, 획득 파편 0, Lv.1, 처치 0. 정복한 지역이 있으면 그 파편이 보인다.</summary>
    [UnityTest]
    public IEnumerator Regression_ShowsStageBeforeAfter_Shards_Level_Kills()
    {
        var reg = pc.GetComponent<RegressionScreen>();
        reg.ShowForCapture();
        yield return null;
        // 2026-11-22 병합 — 챕터 구조라 환생해도 진행이 남는다: 왼쪽 = 가장 멀리 깬 스테이지(새 프로필은 없음), 오른쪽 = 이어서 할 스테이지
        Assert.AreEqual("-", reg.ValueText(0));
        Assert.AreEqual("1-1", reg.ValueText(1), "새 프로필은 1-1부터");
        Assert.AreEqual("0", reg.ValueText(2));
        Assert.AreEqual("Lv.1", reg.ValueText(3));
        Assert.AreEqual("0", reg.ValueText(4));
        reg.HideForCapture();
    }

    static string TextOf(PxScreen s, string name)
    {
        foreach (var t in s.Root.GetComponentsInChildren<UnityEngine.UI.Text>(true)) if (t.name == name) return t.text;
        return null;
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

    /// <summary>
    /// 프리팹 UI 2호(2026-11-23) — 공용 팝업 모양은 프리팹(`Resources/UI/Views/CommonPopupView`)에서 온다.
    /// 프리팹이 없으면 코드 모양으로 대신 지어져 겉으론 멀쩡하므로, 빠진 걸 이 테스트가 잡는다
    /// (처음엔 메뉴 `ChronoSavior/UI 프리팹/공용 팝업 만들기`로 만들어야 통과한다).
    /// </summary>
    [UnityTest]
    public IEnumerator Popup_LayoutComesFromViewPrefab_OneSetOnly()
    {
        Assert.IsNotNull(Resources.Load<CommonPopupView>(CommonPopupView.ResourcePath),
                         "공용 팝업 프리팹이 없다 — 메뉴 ChronoSavior/UI 프리팹/공용 팝업 만들기");
        var popup = pc.GetComponent<CommonPopup>();
        CommonPopup.Alert("알림", "내용");
        yield return null;
        Assert.IsTrue(popup.UsesViewPrefab, "모양을 프리팹에서 불러온다");
        Assert.AreEqual(1, popup.Root.GetComponentsInChildren<CommonPopupView>(true).Length, "모양은 한 벌뿐(코드로 또 짓지 않는다)");
        Assert.AreEqual(4, popup.Root.GetComponentsInChildren<PxButton>(true).Length, "버튼 넷(X·취소·큰 확인·확인)뿐 — 두 벌로 생기지 않는다");
        popup.DismissForTest();
    }

    /// <summary>
    /// 공용 팝업의 글자·키보드 순서가 예전과 같다(프리팹이든 대체 코드 모양이든) —
    /// 제목·내용·버튼 글자는 코드가 채우고, 키보드 순서는 취소(0) → 큰 확인(1) → 확인(2), X는 순서 밖.
    /// </summary>
    [UnityTest]
    public IEnumerator Popup_TextsAndNavOrderKept()
    {
        var popup = pc.GetComponent<CommonPopup>();
        bool ok = false;
        CommonPopup.Confirm("새 원정", "지울까요?", () => ok = true, "시작", "그만");
        yield return null;
        Assert.AreEqual(2, popup.NavIndex, "묻기는 처음에 오른쪽 확인을 고른다");

        Assert.AreEqual("새 원정", TextOf(popup, "Title"));
        Assert.AreEqual("지울까요?", TextOf(popup, "Body"));
        Assert.AreEqual("시작", Find(popup, "Ok").Inner.GetComponentInChildren<UnityEngine.UI.Text>().text, "확인 버튼 글자");
        Assert.AreEqual("그만", Find(popup, "Cancel").Inner.GetComponentInChildren<UnityEngine.UI.Text>().text, "취소 버튼 글자");

        Find(popup, "Cancel").OnHover?.Invoke();
        Assert.AreEqual(0, popup.NavIndex, "취소 = 0번");
        Find(popup, "OkWide").OnHover?.Invoke();
        Assert.AreEqual(1, popup.NavIndex, "큰 확인 = 1번");
        Find(popup, "Close").OnHover?.Invoke();
        Assert.AreEqual(1, popup.NavIndex, "X는 키보드 순서에 없다 — 선택이 그대로");

        Find(popup, "Ok").Click();
        Assert.IsTrue(ok, "확인을 누르면 콜백");
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
            Assert.AreEqual("0", settings.ValueTextForTest(3), "배경음은 넷째 줄(화면 모드·해상도·마스터 다음)");
            float x0 = settings.KnobXForTest(1);
            GameSettings.BgmVolume = 10;
            yield return null;
            Assert.AreEqual("100", settings.ValueTextForTest(3));
            Assert.Greater(settings.KnobXForTest(1), x0 + 150f, "음량이 오르면 손잡이가 오른쪽 끝으로");
            Assert.IsNotNull(Find(settings, "Apply"), "적용 버튼");
            Find(settings, "Apply").Click();
            Assert.IsFalse(SettingsWindow.IsOpen, "적용 = 닫기(값은 바꾸는 즉시 적용된다)");
        }
        finally { GameSettings.BgmVolume = bgm; SettingsWindow.Close(); }
    }

    /// <summary>마스터 볼륨(2026-11-16 새 기능) — 0~10, 효과음 등 모든 소리에 곱하는 배율.</summary>
    [Test]
    public void MasterVolume_ClampsAndScales()
    {
        int keep = GameSettings.MasterVolume;
        try
        {
            GameSettings.MasterVolume = 15;
            Assert.AreEqual(GameSettings.VolumeMax, GameSettings.MasterVolume);
            Assert.AreEqual(1f, GameSettings.MasterScale, 1e-6f);
            GameSettings.MasterVolume = 5;
            Assert.AreEqual(0.5f, GameSettings.MasterScale, 1e-6f);
            GameSettings.MasterVolume = -3;
            Assert.AreEqual(0f, GameSettings.MasterScale, 1e-6f, "0이면 소리가 안 난다");
        }
        finally { GameSettings.MasterVolume = keep; }
    }

    /// <summary>결과창 "준 피해량·받은 피해" 집계 — 깎인 체력만 더하고, 사냥을 새로 시작하면 0.</summary>
    [Test]
    public void RunDamage_Accumulates_AndResetsOnBegin()
    {
        RunState.Begin(1, RunMode.Normal);
        RunState.NoteDamageDealt(12.4f);
        RunState.NoteDamageDealt(0f);
        RunState.NoteDamageDealt(-5f);
        RunState.NoteDamageTaken(7f);
        Assert.AreEqual(12, RunState.DamageDealt);
        Assert.AreEqual(7, RunState.DamageTaken);
        var snap = RunState.Capture();
        RunState.Begin(1, RunMode.Normal);
        Assert.AreEqual(0, RunState.DamageDealt, "새 사냥은 0부터");
        RunState.Restore(snap);
        Assert.AreEqual(12, RunState.DamageDealt, "스냅샷이 피해 집계도 들고 간다");

        RunState.Begin(1, RunMode.Normal);
        for (int i = 0; i < 5; i++) RunState.NoteDamageDealt(0.3f); // 소수 체력 — 타격마다 반올림하면 0이 된다
        Assert.AreEqual(2, RunState.DamageDealt, "소수까지 모아 두고 읽을 때 한 번 반올림(1.5 → 2)");
        RunState.MarkOver();
        RunState.NoteDamageDealt(100f);
        Assert.AreEqual(2, RunState.DamageDealt, "사냥이 끝난 뒤(같은 프레임의 화상 틱 등)는 안 센다");
        RunState.Reset();
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
        Assert.AreEqual("준 피해량", Label(result, "StatLabel0").text, "Figma 통계 세 줄");
        Assert.AreEqual("받은 피해", Label(result, "StatLabel2").text);
        StringAssert.Contains("토벌", Label(result, "BarText0").text, "진행 막대 1 = 그 스테이지 토벌(2026-11-19 \"1-1 토벌 n/100\")");
        Assert.IsNull(Label(result, "BarText1"), "시간 동조율 막대는 뺐다(2026-11-18)");
        Assert.IsNull(Label(result, "BarText2"), "멸망까지 막대는 뺐다(2026-11-18)");
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
