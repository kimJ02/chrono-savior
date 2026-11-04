using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 몬스터들의 성소 — 원본 `spawnShrine()`(project_test.html:3996)과 파괴 처리(`:1797`).
///
/// 성소의 핵심은 **버프의 방향이 둘**이라는 점이다: 살아 있는 동안은 적이 강해지고,
/// 부수면 그 힘이 플레이어에게 넘어온다. 한쪽만 구현하면 "빨리 부술수록 이득"이라는
/// 긴장이 사라지므로 양쪽을 다 본다.
/// </summary>
public class ShrineTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test") || go.name == "Shrine"))
                Object.DestroyImmediate(go);
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunState.Reset();
        RunTransient.Reset();
        ProfileService.Reset();
    }

    static GameObject NewShrine()
    {
        var go = new GameObject("TestShrine");
        go.tag = "Enemy";
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var move = go.AddComponent<EnemyMove>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
                         .SetValue(move, 0f);
        move.isStructure = true; // 원본 `if (e.shrine) continue;`(:4024) — 컴포넌트를 끄면 스폰 보호가 안 풀린다
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<EnemyHealth>();
        go.AddComponent<Shrine>();
        return go;
    }

    /// <summary>원본 `hp: round(70 * 1.4^(lv-1))`(:4001) — 레벨이 오를수록 부수기 어려워진다.</summary>
    [Test]
    public void ShrineHp_GrowsWithLevel()
    {
        Assert.AreEqual(Shrine.BaseHp, Shrine.HpForLevel(1), 0.01f);
        Assert.AreEqual(Shrine.BaseHp * Shrine.HpGrowPerLevel, Shrine.HpForLevel(2), 0.01f);
        Assert.AreEqual(Shrine.BaseHp * Mathf.Pow(Shrine.HpGrowPerLevel, 3), Shrine.HpForLevel(4), 0.01f);
    }

    /// <summary>
    /// 성소가 살아 있는 동안 **적**이 강해진다(원본 `run.shrineActive`, 적용 `:4040`·`:4148`).
    /// 플레이어 쪽 배수는 아직 안 붙는다 — 그건 부순 뒤의 보상이다.
    /// </summary>
    /// <remarks>
    /// 한 프레임을 기다리는 이유: 원본은 이 상태를 **매 프레임 다시 계산한다**(`:4414`).
    /// 세울 때 한 번 켜는 방식이 아니라서(스폰 보호 중인 성소는 아직 적을 강화하면 안 된다)
    /// `Awake` 직후가 아니라 첫 `Update` 뒤에 켜진다.
    /// </remarks>
    [UnityTest]
    public IEnumerator WhileAlive_EnemiesAreBuffed_PlayerIsNot()
    {
        Assert.AreEqual(1f, CombatModifiers.EnemyDamageMultiplier, 1e-4f, "성소가 없는데 적이 강하다");

        var shrine = NewShrine();
        yield return null;

        Assert.IsTrue(CombatModifiers.ShrineActive);
        Assert.AreEqual(CombatModifiers.ShrineEnemyDamageMult, CombatModifiers.EnemyDamageMultiplier, 1e-4f);
        Assert.AreEqual(CombatModifiers.ShrineEnemyMoveSpeedMult, CombatModifiers.EnemyMoveSpeedMultiplier, 1e-4f);
        Assert.AreEqual(1f, CombatModifiers.DamageMultiplier, 1e-4f,
            "성소가 살아 있는데 플레이어가 이득을 보면 안 된다 — 그건 부순 뒤의 보상이다");

        Object.DestroyImmediate(shrine);
    }

    /// <summary>부수면 적 버프가 꺼지고 플레이어 가호가 켜진다(원본 `:1798`).</summary>
    [UnityTest]
    public IEnumerator WhenDestroyed_BuffMovesToPlayer()
    {
        // `CombatEvents.ShrineBuffGranted` 수신은 평소 RunController가 하지만, 여기선 그것만 따로 잇는다.
        CombatEvents.ShrineBuffGranted += CombatModifiers.GrantShrineBuff;

        var shrine = NewShrine();
        var health = shrine.GetComponent<EnemyHealth>();
        health.TakeDamage(health.MaxHp * 10f, null);
        yield return null;

        Assert.IsFalse(CombatModifiers.ShrineActive, "부쉈는데 적 버프가 안 꺼졌다");
        Assert.AreEqual(1f, CombatModifiers.EnemyDamageMultiplier, 1e-4f);
        Assert.Greater(CombatModifiers.ShrineBuffLeft, 0f, "부쉈는데 플레이어 가호가 안 붙었다");
        // 성소를 때리면서 콤보도 같이 쌓였으므로 가호 배수만 따로 떼서 비교한다
        // (콤보 항까지 곱해진 값이 나오는 게 정상이다 — 원본 `dmgMultAll()`도 전부 곱한다).
        float comboPart = 1f + CombatModifiers.Combo * CombatModifiers.ComboDamagePer;
        Assert.AreEqual(CombatModifiers.ShrineDamageMult * comboPart,
                        CombatModifiers.DamageMultiplier, 1e-4f);
    }

    /// <summary>파괴 보상은 고정값이다(원본 exp 30 · gold 45, `:1799`~`:1801`).</summary>
    [UnityTest]
    public IEnumerator WhenDestroyed_GrantsFixedReward()
    {
        RunState.Begin(1, RunMode.Normal);
        var shrine = NewShrine();
        var health = shrine.GetComponent<EnemyHealth>();

        health.TakeDamage(health.MaxHp * 10f, null);
        yield return null;

        Assert.AreEqual(Shrine.RewardExp, ProfileService.Current.exp);
        Assert.AreEqual(Shrine.RewardGold, ProfileService.Current.gold);
        Assert.AreEqual(Shrine.RewardExp, RunState.ExpEarned, "결과 화면 집계에 안 들어갔다");
    }

    /// <summary>
    /// 성소는 **처치 수에 안 들어간다** — 원본이 성소 분기에서 바로 `return`해서 살기·연쇄·처치수
    /// 집계를 통째로 건너뛴다(`:1809`). 안 그러면 성소가 경험치 구슬 카운터까지 밀어버린다.
    /// </summary>
    [UnityTest]
    public IEnumerator ShrineDestruction_DoesNotCountAsKill()
    {
        RunState.Begin(1, RunMode.Normal);

        // 스포너의 처치 처리(`HandleEnemyDied`)를 실제로 태운다.
        var spawnerGO = new GameObject("TestSpawner");
        var spawner = spawnerGO.AddComponent<EnemySpawner>();
        spawner.spawnShrines = false;

        var shrine = NewShrine();
        var health = shrine.GetComponent<EnemyHealth>();
        var handler = typeof(EnemySpawner).GetMethod("HandleEnemyDied", BindingFlags.NonPublic | BindingFlags.Instance);
        health.Died += (h) => handler.Invoke(spawner, new object[] { h });

        health.TakeDamage(health.MaxHp * 10f, null);
        yield return null;

        Assert.AreEqual(0, RunState.Kills, "성소가 처치 수에 들어갔다");
        Assert.AreEqual(0, RunState.Fury, "성소가 살기에 들어갔다");

        Object.DestroyImmediate(spawnerGO);
    }
}

}
