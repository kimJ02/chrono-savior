using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 경험치 곡선이 원본(JS 실수) 그대로인지 — 원본 `expCurve`(project_test.html:706)·`gainExpMeta`(:1440)·구슬(:4466).
/// 2026-10-06 사용자가 SP +1로 마법사를 32레벨까지 올리고 대붕괴로 몹을 잡자 게임이 멈췄다. 필요 경험치가
/// `int`라 27레벨(30억)에서 넘쳐 음수가 됐고, 레벨업 반복(`while (exp >= 필요량)`)이 끝나지 않았다.
/// 아래는 전부 예전 코드(`float` 계산·`int` 결과·C# 반올림)였다면 실패한다.
/// </summary>
public class ExpCurveTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test") || go.name == "ExpOrb"))
                Object.DestroyImmediate(go);
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        RunState.Reset();
        RunTransient.Reset();
        CombatModifiers.Reset();
        ProfileService.Reset();
    }

    /// <summary>원본 `Math.floor(700 * Math.pow(1.8, l - 1))`을 JS처럼 double로 계산한 값.</summary>
    [Test]
    public void RequiredExp_MatchesOriginalDoubleCurve_PastIntRange()
    {
        Assert.AreEqual(2268L, PlayerProfile.RequiredExp(3), "float로 계산하면 2267이 된다");
        Assert.AreEqual(2623693L, PlayerProfile.RequiredExp(15));
        Assert.AreEqual(1686206144L, PlayerProfile.RequiredExp(26));
        Assert.AreEqual(3035171060L, PlayerProfile.RequiredExp(27), "27레벨부터 int(약 21억)를 넘는다");
        Assert.AreEqual(57351621105L, PlayerProfile.RequiredExp(32));
        Assert.AreEqual(6320126035573L, PlayerProfile.RequiredExp(40));
    }

    /// <summary>`long`도 넘는 65레벨부터는 최댓값에서 멈추고, 1레벨 미만(깨진 세이브)은 1레벨로 친다 — 둘 다 반복이 끝나게.</summary>
    [Test]
    public void RequiredExp_SaturatesAtLongMax_AndNeverDropsBelowLevel1()
    {
        Assert.Less(PlayerProfile.RequiredExp(64), long.MaxValue);
        Assert.Greater(PlayerProfile.RequiredExp(64), PlayerProfile.RequiredExp(63));
        Assert.AreEqual(long.MaxValue, PlayerProfile.RequiredExp(65));
        Assert.AreEqual(long.MaxValue, PlayerProfile.RequiredExp(int.MaxValue));
        Assert.AreEqual(700L, PlayerProfile.RequiredExp(0));
        Assert.AreEqual(700L, PlayerProfile.RequiredExp(int.MinValue));
    }

    /// <summary>사용자가 겪은 그대로 — 32레벨에서 경험치를 받는 순간 예전엔 멈췄다(필요량이 음수라 반복이 안 끝남).</summary>
    [Test]
    public void AddExp_AtLevel32_LevelsUpOnceAndCarriesOver()
    {
        var p = new PlayerProfile();
        p.level = 32;
        p.AddExp(30); // 몹 하나 — 레벨업 없음
        Assert.AreEqual(32, p.level);
        Assert.AreEqual(30L, p.exp);

        p.AddExp(PlayerProfile.RequiredExp(32)); // 딱 한 레벨, 30은 이월
        Assert.AreEqual(33, p.level);
        Assert.AreEqual(30L, p.exp);
    }

    /// <summary>`long` 끝에서도 넘쳐서 음수가 되지 않는다 — 최댓값에서 멈추고 한 레벨 오른다.</summary>
    [Test]
    public void AddExp_AtLongLimit_SaturatesInsteadOfOverflowing()
    {
        var p = new PlayerProfile();
        p.level = 70; // 필요량 = long 최댓값
        p.AddExp(long.MaxValue - 1);
        Assert.AreEqual(70, p.level);

        p.AddExp(10); // 그냥 더하면 음수 — 최댓값에서 멈춰 딱 한 레벨
        Assert.AreEqual(71, p.level);
        Assert.AreEqual(0L, p.exp);
    }

    /// <summary>원본 `Math.round`는 .5를 올린다 — 4레벨 구슬 4082 × 0.25 = 1020.5 → 1021(C# 반올림이면 1020).</summary>
    [UnityTest]
    public IEnumerator Orb_RoundsHalfUpLikeOriginal()
    {
        ProfileService.Current.level = 4;
        RunState.Begin(1, RunMode.Normal);

        yield return CollectOneOrb();

        Assert.AreEqual(1021L, ProfileService.Current.exp);
        Assert.AreEqual(1021L, RunState.ExpEarned);
    }

    /// <summary>30레벨 구슬 하나는 약 44억 — `int`를 넘는데도 그대로 받고 결과 화면 집계에도 들어간다.</summary>
    [UnityTest]
    public IEnumerator Orb_PastIntRange_GrantsFullAmount()
    {
        ProfileService.Current.level = 30;
        RunState.Begin(1, RunMode.Normal);

        yield return CollectOneOrb();

        const long expected = 4425279406L; // floor(17701117625 × 0.25 + 0.5)
        Assert.AreEqual(30, ProfileService.Current.level, "필요량의 25%라 레벨업은 없어야 한다");
        Assert.AreEqual(expected, ProfileService.Current.exp);
        Assert.AreEqual(expected, RunState.ExpEarned);
    }

    static IEnumerator CollectOneOrb()
    {
        // 플레이어를 구슬 바로 위에 둬서 첫 프레임에 획득되게 한다(`ExpOrbTests`와 같은 배치).
        var player = new GameObject("TestPlayer");
        player.tag = "Player";
        player.transform.position = new Vector3(5f, 0.6f - ExpOrb.PlayerCenterOffset, 0f);
        ExpOrb.Spawn(new Vector3(5f, 0.2f, 0f), null, player.transform);
        yield return null;
        yield return null;
    }
}

}
