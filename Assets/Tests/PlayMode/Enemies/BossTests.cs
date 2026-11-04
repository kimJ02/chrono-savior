using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 지역 보스 — 원본 `spawnBoss()`(project_test.html:4155) · `updateBoss()`(`:4176`).
///
/// 상태 전이는 `GetHorizontalSpeed(dt, target, ...)`가 순수 함수라 프레임을 흘리지 않고 찍을 수 있다
/// (돌진귀 테스트와 같은 구조).
/// </summary>
public class BossTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test") || go.name == "EnemyBolt"))
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
        EnemySpawnRequestBus.Reset();
        ProfileService.Reset();
    }

    static Boss NewBoss(Vector3 pos)
    {
        var go = new GameObject("TestBoss");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var move = go.AddComponent<EnemyMove>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
                         .SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        var health = go.AddComponent<EnemyHealth>();
        health.SetMaxHp(1000f);
        var boss = go.AddComponent<Boss>();
        move.RefreshTypeBehaviour();
        return boss;
    }

    static GameObject NewTarget(Vector3 pos)
    {
        var go = new GameObject("TestTarget");
        go.transform.position = pos;
        return go;
    }

    static int BoltCount() => Object.FindObjectsByType<EnemyBolt>(FindObjectsSortMode.None).Length;

    // ────────────────────────── 스탯 곡선 ──────────────────────────

    /// <summary>
    /// 원본 `hpBase 800 × 2.15^(r-1)`, `dmgBase 30 × 1.4^(r-1)`(project_test.html:718·:4159).
    /// **잡몹과 같은 지역 성장률(2.15)** 이라 "첫 트라이 5지역쯤 보스 벽"이 원본 의도다(:719 주석).
    /// </summary>
    [Test]
    public void BossStats_GrowExponentiallyByRegion()
    {
        Assert.AreEqual(Boss.HpBase, Boss.HpForRegion(1), 0.01f);
        Assert.AreEqual(Boss.HpBase * Mathf.Pow(2.15f, 4), Boss.HpForRegion(5), 0.1f);
        Assert.AreEqual(Boss.DmgBase * Mathf.Pow(1.4f, 4), Boss.DamageForRegion(5), 0.01f);
    }

    /// <summary>원본 `lv = regionBaseLv(r) + 2`(:4157) — 잡몹보다 2레벨 높아 레벨 페널티가 더 세다.</summary>
    [Test]
    public void BossLevel_IsTwoAboveRegionBase()
    {
        Assert.AreEqual(RegionConfig.RecommendedLevel(3) + 2, Boss.LevelForRegion(3));
    }

    // ────────────────────────── 행동 선택 ──────────────────────────

    /// <summary>
    /// 플레이어가 **높은 발판 위**면 근접이 아니라 원거리(귀신불)로 전환한다(원본 `highUp` :4191).
    /// 이게 없으면 발판 위가 완전 안전지대가 되어 보스전이 성립하지 않는다.
    /// </summary>
    [UnityTest]
    public IEnumerator Boss_SpitsWhenPlayerIsHighUp()
    {
        var boss = NewBoss(new Vector3(10f, FieldBounds.GroundY, 0f));
        var target = NewTarget(new Vector3(10.5f, FieldBounds.GroundY + Boss.HighY + 1f, 0f));

        boss.GetHorizontalSpeed(1f, target.transform, 1f);    // idle 종료 → 행동 선택(SpitTele)
        Assert.AreEqual(0, BoltCount(), "예고 중엔 아직 안 쏜다");

        boss.GetHorizontalSpeed(Boss.SpitTele, target.transform, 1f); // 예고 끝 → 발사
        yield return null;

        Assert.AreEqual(Boss.SpitCount, BoltCount(), "귀신불이 3발 나가야 한다");
    }

    /// <summary>가까우면 내려찍기 — 좌우 충격파 2발(원본 `:4215`).</summary>
    [UnityTest]
    public IEnumerator Boss_SlamsWhenPlayerIsClose()
    {
        var boss = NewBoss(new Vector3(10f, FieldBounds.GroundY, 0f));
        var target = NewTarget(new Vector3(11f, FieldBounds.GroundY, 0f)); // slamRange(2.6) 안

        boss.GetHorizontalSpeed(1f, target.transform, 1f);   // → SlamTele
        Assert.AreEqual(0, BoltCount());

        boss.GetHorizontalSpeed(0.6f, target.transform, 1f); // 예고 끝 → 충격파
        yield return null;

        Assert.AreEqual(2, BoltCount(), "내려찍기는 좌우로 충격파 하나씩이다");
    }

    /// <summary>
    /// 내려찍기는 충격파만이 아니라 **발밑 즉시 판정**도 있다(원본 `:4219`) —
    /// 없으면 보스 발밑이 오히려 가장 안전한 자리가 된다.
    /// </summary>
    [UnityTest]
    public IEnumerator Boss_SlamAlsoHitsPlayerDirectly()
    {
        var boss = NewBoss(new Vector3(10f, FieldBounds.GroundY, 0f));
        boss.GetComponent<EnemyMove>().attackPower = 30f;

        var player = new GameObject("TestPlayerTarget");
        player.transform.position = new Vector3(10.3f, FieldBounds.GroundY, 0f);
        // ⚠️ Rigidbody2D를 **먼저** 붙여야 한다 — PlayerHealth.Awake가 그걸 캐시하는데,
        // 나중에 붙이면 null로 남아 넉백에서 터진다(AddComponent는 Awake를 그 자리에서 실행한다).
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        var hp = player.AddComponent<ChronoSavior.Characters.PlayerHealth>();
        yield return null;
        float before = hp.CurrentHp;

        boss.GetHorizontalSpeed(1f, player.transform, 1f);
        boss.GetHorizontalSpeed(0.6f, player.transform, 1f); // 슬램 발동

        Assert.Less(hp.CurrentHp, before, "보스 발밑인데 내려찍기에 안 맞았다");
    }

    /// <summary>돌진 중엔 접촉 피해가 ×1.4다(원본 `damagePlayer(b.dmg, b.x, 1.4)`, `:4262`).</summary>
    [Test]
    public void Boss_ChargeBoostsContactDamage()
    {
        var boss = NewBoss(new Vector3(10f, FieldBounds.GroundY, 0f));
        var target = NewTarget(new Vector3(16f, FieldBounds.GroundY, 0f)); // 4.8유닛 밖

        Assert.AreEqual(1f, boss.ContactDamageMultiplier, 1e-4f);

        // 돌진은 55% 확률이라 여러 번 굴려 상태에 도달시킨다.
        for (int i = 0; i < 200; i++)
        {
            boss.GetHorizontalSpeed(1f, target.transform, 1f);
            if (Mathf.Abs(boss.ContactDamageMultiplier - Boss.ChargeContactDamageMult) < 1e-4f) return;
        }
        Assert.Fail("멀리 있는데 200번을 굴려도 돌진에 한 번도 안 들어갔다");
    }

    // ────────────────────────── 페이즈 소환 ──────────────────────────

    /// <summary>
    /// 체력 66%·33%를 지날 때 부하 3마리씩(원본 `:4182`). **각 구간에서 한 번씩만** —
    /// 매 프레임 소환되면 화면이 잡몹으로 덮인다.
    /// </summary>
    [UnityTest]
    public IEnumerator Boss_SummonsOncePerPhase()
    {
        var requests = new List<EnemyType>();
        var protects = new List<float>();
        EnemySpawnRequestBus.Requested += (_, t, protect) => { requests.Add(t); protects.Add(protect); };

        var boss = NewBoss(new Vector3(10f, FieldBounds.GroundY, 0f));
        var health = boss.GetComponent<EnemyHealth>();
        var target = NewTarget(new Vector3(10.5f, FieldBounds.GroundY, 0f));

        boss.GetHorizontalSpeed(0.016f, target.transform, 1f);
        Assert.AreEqual(0, requests.Count, "체력이 가득한데 소환했다");

        health.TakeDamage(health.MaxHp * 0.4f, null); // 60% 남음 → 1페이즈
        yield return null;
        boss.GetHorizontalSpeed(0.016f, target.transform, 1f);
        Assert.AreEqual(Boss.SummonCount, requests.Count);

        // 같은 구간에서 더 굴려도 추가 소환은 없다.
        for (int i = 0; i < 10; i++) boss.GetHorizontalSpeed(0.016f, target.transform, 1f);
        Assert.AreEqual(Boss.SummonCount, requests.Count, "같은 페이즈에서 또 소환했다");

        health.TakeDamage(health.MaxHp * 0.35f, null); // 25% 남음 → 2페이즈
        yield return null;
        boss.GetHorizontalSpeed(0.016f, target.transform, 1f);
        Assert.AreEqual(Boss.SummonCount * 2, requests.Count);

        // 원본 `bossSummon`은 `opts.protect`를 안 넘긴다(`:4278`) — 그래서 **기본 2초**다.
        // 분열귀 새끼(0.35초)와 버스를 공유한다고 값까지 따라가면 안 된다: 부하가 나오자마자
        // 얻어맞고 사라져서, 보스전 압박이 원본보다 훨씬 약해진다.
        foreach (float p in protects)
            Assert.AreEqual(EnemySpawnRequestBus.DefaultSpawnProtect, p, 1e-4f,
                "보스 부하가 분열귀 새끼용 짧은 스폰 보호를 받고 있다");
    }

    // ────────────────────────── 진행도 ──────────────────────────

    /// <summary>보스는 거의 안 밀린다(원본 `e.boss ? 0.08`, `:1678`) — 넉백으로 벽에 몰 수 없다.</summary>
    [Test]
    public void Boss_ResistsKnockback()
    {
        Assert.Less(Boss.KnockbackMultiplier, 0.1f);
    }
}

}
