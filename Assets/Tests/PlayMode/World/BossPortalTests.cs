using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 보스 포탈 — **원본에 없는 구조다**(사용자 지시 2026-09-16).
///
/// 원본은 로비에서 `[지역]`/`[👹보스]` 버튼으로 보스전을 **별개의 런**으로 시작한다
/// (project_test.html:6464 · `startRun(region, 'boss')` :4293). 우리는 항상 일반 필드로 입장한 뒤
/// 필드 우측 끝의 포탈로 보스 필드에 들어간다.
///
/// 그래서 이 테스트가 지켜야 할 것은 "원본과 같은가"가 아니라 **바뀐 규칙이 서로 모순되지 않는가**다:
/// 잠금 조건 · 런 연속성(처치 수·살기가 이어지는가) · 타이머 전환 · 이중 입장 방지.
/// </summary>
public class BossPortalTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f; // RunController가 0으로 두고 끝나면 뒤 테스트의 대기가 안 끝난다
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        GameState.Reset();
        RunState.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunTransient.Reset();
        CombatModifiers.Reset();
        ProfileService.Reset();
        PlatformSet.Activate(false); // 무대를 일반으로 되돌린다(FieldBounds.MaxX도 같이 복구)
    }

    static BossPortal NewPortal()
    {
        var go = new GameObject("TestPortal");
        go.transform.position = new Vector3(FieldLayout.BossPortalX, FieldBounds.GroundY + 1f, 0f);
        go.AddComponent<SpriteRenderer>();
        return go.AddComponent<BossPortal>();
    }

    static GameObject NewPlayerAt(Vector3 pos)
    {
        var go = new GameObject("TestPlayer") { tag = "Player" };
        go.transform.position = pos;
        return go;
    }

    /// <summary>그 지역 누적 토벌을 목표치까지 채운다 — 포탈 해금 조건(`IsBossUnlocked`).</summary>
    static void FillRegionKills(int region)
    {
        var p = ProfileService.Current;
        for (int i = 0; i < RunState.RegionKillTarget; i++) p.RegisterRegionKill(region);
    }

    // ────────────────────────── 잠금 ──────────────────────────

    /// <summary>
    /// 누적 토벌 100마리를 채우기 전엔 잠겨 있다. 이게 유일한 해금 조건이다 —
    /// 로비에 보스 버튼이 없어졌으므로 **여기가 막히면 보스에 갈 방법이 아예 없다.**
    /// </summary>
    [Test]
    public void Portal_IsLockedUntilRegionKillTargetReached()
    {
        RunState.Begin(1, RunMode.Normal);
        var portal = NewPortal();

        Assert.IsFalse(portal.IsUnlocked, "토벌 0마리인데 포탈이 열려 있다");

        FillRegionKills(1);
        Assert.IsTrue(portal.IsUnlocked, $"토벌 {RunState.RegionKillTarget}마리를 채웠는데 안 열린다");
    }

    /// <summary>
    /// 해금은 **누적**이라 한 번 열리면 계속 열려 있다(`regionBossUnlocked`는 프로필에 저장된다).
    /// 그래서 다음 런에 들어가도 100마리를 다시 잡을 필요가 없다.
    /// </summary>
    [Test]
    public void Portal_StaysUnlockedInLaterRuns()
    {
        FillRegionKills(1);
        RunState.Begin(1, RunMode.Normal);
        var portal = NewPortal();
        Assert.IsTrue(portal.IsUnlocked);

        RunState.Begin(1, RunMode.Normal); // 새 런
        Assert.IsTrue(portal.IsUnlocked, "누적 해금인데 새 런에서 다시 잠겼다");
    }

    /// <summary>지역별로 따로 센다 — 1지역을 채워도 2지역 포탈은 잠겨 있다.</summary>
    [Test]
    public void Portal_UnlockIsPerRegion()
    {
        FillRegionKills(1);
        var portal = NewPortal();

        RunState.Begin(1, RunMode.Normal);
        Assert.IsTrue(portal.IsUnlocked);

        RunState.Begin(2, RunMode.Normal);
        Assert.IsFalse(portal.IsUnlocked, "1지역 토벌로 2지역 포탈이 열렸다");
    }

    // ────────────────────────── 표시 ──────────────────────────

    /// <summary>
    /// 보스 필드로 넘어간 뒤에는 포탈이 화면에서 사라진다 — 남아 있으면 같은 포탈을 또 밟는다.
    /// </summary>
    [Test]
    public void Portal_HidesOutsideNormalField()
    {
        GameState.Set(GameScene.Run);
        RunState.Begin(1, RunMode.Normal);
        var portal = NewPortal();
        Assert.IsTrue(portal.ShouldBeVisible, "일반 필드 사냥 중인데 포탈이 숨었다");

        RunState.EnterBossField();
        Assert.IsFalse(portal.ShouldBeVisible, "보스 필드인데 포탈이 남아 있다");

        RunState.Begin(1, RunMode.Normal);
        RunState.MarkOver();
        Assert.IsFalse(portal.ShouldBeVisible, "런이 끝났는데 포탈이 남아 있다");
    }

    /// <summary>플레이어가 멀리 있으면 상호작용 범위 밖이다(HUD 안내도 이 값으로 뜬다).</summary>
    [UnityTest]
    public IEnumerator Portal_DetectsPlayerOnlyWhenClose()
    {
        GameState.Set(GameScene.Run);
        RunState.Begin(1, RunMode.Normal);
        var portal = NewPortal();

        var player = NewPlayerAt(new Vector3(2.2f, FieldBounds.GroundY + 1f, 0f)); // 시작 지점
        yield return null;
        Assert.IsFalse(portal.PlayerInRange, "맵 반대쪽에 있는데 범위 안이라고 한다");

        player.transform.position = portal.transform.position;
        yield return null;
        Assert.IsTrue(portal.PlayerInRange, "포탈에 겹쳐 있는데 범위 밖이라고 한다");
    }

    // ────────────────────────── 전환 ──────────────────────────

    /// <summary>
    /// **런이 끊기지 않는다** — 일반 필드에서 쌓은 처치 수·살기·번 재화를 보스전에 그대로 들고 간다.
    /// 이게 이 구조의 핵심 보상이다(살기는 처치당 피해 +0.8%라, 100마리를 밀어낸 뒤 보스에
    /// 들어가면 그만큼 세진 상태로 싸운다). 여기가 초기화되면 포탈 방식을 쓸 이유가 없어진다.
    /// </summary>
    [Test]
    public void EnterBossField_KeepsRunProgressAndSwitchesTimer()
    {
        RunState.Begin(3, RunMode.Normal);
        RunState.RegisterKill(isBoss: false);
        RunState.RegisterKill(isBoss: false);
        RunState.RegisterReward(500, 120);
        RunState.Tick(40f); // 남은 시간을 줄여 둔다

        int kills = RunState.Kills, fury = RunState.Fury;
        Assert.Less(RunState.TimeLeft, RunState.NormalTime);

        Assert.IsTrue(RunState.EnterBossField());

        Assert.AreEqual(RunMode.Boss, RunState.Mode);
        Assert.AreEqual(RunState.BossTime, RunState.TimeLeft, 1e-3f,
            "보스 필드 제한시간이 새로 시작되지 않았다");
        Assert.AreEqual(3, RunState.Region, "지역이 바뀌었다");
        Assert.AreEqual(kills, RunState.Kills, "처치 수가 초기화됐다");
        Assert.AreEqual(fury, RunState.Fury, "살기가 초기화됐다");
        Assert.AreEqual(500, RunState.GoldEarned, "번 골드가 초기화됐다");
        Assert.AreEqual(120, RunState.ExpEarned, "번 경험치가 초기화됐다");
    }

    /// <summary>두 번 들어갈 수 없다 — 포탈을 연타해도 타이머가 계속 180초로 리셋되면 안 된다.</summary>
    [Test]
    public void EnterBossField_IsIgnoredWhenAlreadyInBossField()
    {
        RunState.Begin(1, RunMode.Normal);
        Assert.IsTrue(RunState.EnterBossField());

        RunState.Tick(30f);
        float left = RunState.TimeLeft;

        Assert.IsFalse(RunState.EnterBossField(), "이미 보스 필드인데 또 전환됐다");
        Assert.AreEqual(left, RunState.TimeLeft, 1e-3f, "재입장으로 타이머가 되돌아갔다");
    }

    /// <summary>끝난 런에선 전환되지 않는다.</summary>
    [Test]
    public void EnterBossField_IsIgnoredAfterRunIsOver()
    {
        RunState.Begin(1, RunMode.Normal);
        RunState.MarkOver();
        Assert.IsFalse(RunState.EnterBossField());
        Assert.AreEqual(RunMode.Normal, RunState.Mode);
    }

    // ────────────────────────── 컨트롤러 연동 ──────────────────────────

    /// <summary>
    /// 잠긴 포탈의 요청은 <see cref="RunController"/>가 무시한다.
    /// **판단은 컨트롤러 한 곳에서만** 한다(포탈은 요청만 던진다) — 두 곳에서 검사하면 한쪽만
    /// 고쳤을 때 조용히 갈린다.
    /// </summary>
    [UnityTest]
    public IEnumerator Controller_IgnoresPortalRequestWhenLocked()
    {
        var run = new GameObject("TestRunController").AddComponent<RunController>();
        yield return null;

        run.StartRun(1, RunMode.Normal);
        yield return null;

        RunEvents.RequestBossPortal(); // 토벌 0마리 — 잠겨 있다
        yield return null;

        Assert.AreEqual(RunMode.Normal, RunState.Mode, "잠긴 포탈로 보스 필드에 들어갔다");
    }

    /// <summary>해금된 뒤의 요청은 실제로 무대까지 바꾼다(맵 폭이 보스 무대 값으로 줄어든다).</summary>
    [UnityTest]
    public IEnumerator Controller_SwitchesArenaOnPortalRequest()
    {
        FillRegionKills(1);

        var run = new GameObject("TestRunController").AddComponent<RunController>();
        yield return null;

        run.StartRun(1, RunMode.Normal);
        yield return null;
        Assert.AreEqual(FieldLayout.NormalMapWidth, FieldBounds.MaxX, 1e-3f);

        RunEvents.RequestBossPortal();
        yield return null;

        Assert.AreEqual(RunMode.Boss, RunState.Mode);
        Assert.AreEqual(FieldLayout.BossMapWidth, FieldBounds.MaxX, 1e-3f,
            "보스 필드인데 맵 폭이 일반 무대 그대로다");
    }

    /// <summary>
    /// 보스를 잡으면 런이 끝난다 — 원본 `setTimeout(() => endRun('bossdead'), 1600)`(`:4289`).
    /// **예전엔 이 호출이 아예 없어서** 보스를 잡아도 제한시간까지 빈 무대에 남아 있었다.
    /// </summary>
    [UnityTest]
    public IEnumerator BossDefeated_EndsRunAfterDelay()
    {
        var run = new GameObject("TestRunController").AddComponent<RunController>();
        yield return null;

        run.StartRun(1, RunMode.Normal);
        run.EnterBossField();
        yield return null;

        RunEvents.RaiseBossDefeated();
        yield return null;
        Assert.AreEqual(GameScene.Run, GameState.Current, "지연 없이 즉시 결과 화면이 떴다");

        yield return new WaitForSecondsRealtime(RunController.BossDeadEndDelay + 0.2f);

        Assert.AreEqual(GameScene.Result, GameState.Current, "보스를 잡았는데 런이 안 끝났다");
        Assert.AreEqual(RunEndReason.BossDead, run.LastEndReason);
    }
}

}
