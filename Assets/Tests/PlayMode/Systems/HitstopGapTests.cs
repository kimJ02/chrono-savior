using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 히트스톱 쉬는 시간(2026-10-25 사용자 "모든 히트마다 걸지 말고 어느 정도 시간을 두고") — 한 번 멈췄다 풀리면
/// `GameFlow.HitstopGap` 동안은 새로 안 멈춘다. 성소·보스 처치처럼 긴 멈춤은 예외. 원본은 맞힐 때마다 멈췄다(:1683).
/// ⚠️ `Time.timeScale`을 TearDown에서 꼭 1로 되돌린다(GameFlowTests 주석 참고).
/// </summary>
public class HitstopGapTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
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
    }

    GameFlow rc;

    // GameFlow는 첫 프레임 Start()에서 타이틀로 가며 시간을 멈춘다 — 그 뒤에 사냥을 시작해야 한다(GameFlowTests와 같은 순서).
    IEnumerator StartRun()
    {
        rc = new GameObject("TestGameFlow").AddComponent<GameFlow>();
        yield return null;
        rc.StartRun(1, RunMode.Normal);
        Assert.AreEqual(GameScene.Run, GameState.Current);
        Assert.AreEqual(1f, Time.timeScale, "사냥 중엔 시간이 흐른다 — 여기서부터 멈춤을 잰다");
    }

    // 배치 모드는 한 프레임이 1ms도 안 돼서 프레임 수로 세면 멈춤(0.045초)이 다 안 끝난다 — 실제 시간으로 기다린다.
    static IEnumerator UntilUnfrozen()
    {
        float until = Time.realtimeSinceStartup + 1f;
        while (Time.timeScale == 0f && Time.realtimeSinceStartup < until) yield return null;
    }

    [UnityTest]
    public IEnumerator SecondHit_RightAfterFirstHitstop_DoesNotFreezeAgain()
    {
        yield return StartRun();

        rc.Hitstop(HitFeel.HitstopHit);
        Assert.AreEqual(0f, Time.timeScale, "첫 타격은 멈춘다");
        yield return UntilUnfrozen();
        Assert.AreEqual(1f, Time.timeScale);

        rc.Hitstop(HitFeel.HitstopHit);
        Assert.AreEqual(1f, Time.timeScale, "풀리자마자 들어온 타격은 쉬는 시간이라 안 멈춘다");

        yield return new WaitForSecondsRealtime(GameFlow.HitstopGap + 0.05f);
        rc.Hitstop(HitFeel.HitstopHit);
        Assert.AreEqual(0f, Time.timeScale, "쉬는 시간이 지나면 다시 멈춘다");
    }

    [UnityTest]
    public IEnumerator NormalHit_ShakesLightly_WithAGap()
    {
        yield return StartRun();
        HitFeel.Reset();
        CombatEvents.RaiseEnemyDamaged(null);
        Assert.AreEqual(HitFeel.HitShakePx, HitFeel.ShakeMag, "평타 적중 — 약하게 흔든다(사용자 2026-10-28)");
        HitFeel.Reset();
        CombatEvents.RaiseEnemyDamaged(null);
        Assert.AreEqual(0f, HitFeel.ShakeMag, "간격 안의 다음 평타는 안 흔든다");
        CombatEvents.RaiseEnemyTickDamaged(null);
        Assert.AreEqual(HitFeel.TickShakePx, HitFeel.ShakeMag, "지속 피해 틱은 간격을 따로 세고, 평타보다 약하게 흔든다");
        Assert.Less(HitFeel.TickShakePx, HitFeel.HitShakePx);
        HitFeel.Reset();
        CombatEvents.RaiseEnemyTickDamaged(null);
        Assert.AreEqual(0f, HitFeel.ShakeMag, "틱도 간격 안에선 안 흔든다");
        yield return new WaitForSecondsRealtime(HitFeel.HitShakeGap + 0.05f);
        CombatEvents.RaiseEnemyDamaged(null);
        Assert.AreEqual(HitFeel.HitShakePx, HitFeel.ShakeMag, "간격이 지나면 다시 흔든다");
    }

    [UnityTest]
    public IEnumerator LongHitstop_LikeBossKill_IgnoresTheGap()
    {
        yield return StartRun();
        rc.Hitstop(HitFeel.HitstopHit);
        yield return UntilUnfrozen();
        Assert.AreEqual(1f, Time.timeScale);

        rc.Hitstop(HitFeel.HitstopBossKill);
        Assert.AreEqual(0f, Time.timeScale, "보스 처치 같은 긴 멈춤은 쉬는 시간과 상관없이 건다");
    }

    [UnityTest]
    public IEnumerator HitstopWhileFrozen_StillExtendsLikeOriginal()
    {
        yield return StartRun();
        rc.Hitstop(HitFeel.HitstopHit);
        rc.Hitstop(HitFeel.HitstopKill); // 멈춰 있는 동안 처치 — 원본처럼 max로 늘어난다
        float start = Time.realtimeSinceStartup;
        yield return UntilUnfrozen();
        Assert.AreEqual(1f, Time.timeScale, "결국 풀린다");
        Assert.GreaterOrEqual(Time.realtimeSinceStartup - start, HitFeel.HitstopKill * 0.8f, "처치 멈춤 길이만큼 멈춰야 한다");
    }
}

}
