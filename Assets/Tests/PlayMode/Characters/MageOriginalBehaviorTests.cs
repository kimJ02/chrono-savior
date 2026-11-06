using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 마법사가 원본(project_test.html, 8/21 최종판)과 다르게 돌던 5가지 — 2026-10-03 원본 대조에서 발견해 바로잡았다.
/// 1. 중력점이 늘 맵 바닥 아래(-0.2)에 생김(높이 부호) · 2. 중력 노출이 안 빠짐 · 3. 마법탄 지형 처리(중력탄이 발판에서
/// 터짐, 0차 탄이 발판에 막힘, 폭발탄이 천장에서 안 터짐) · 4. 순간이동 착지 · 5. 수직 조준 탄이 늘 오른쪽에서 나감.
/// </summary>
public class MageOriginalBehaviorTests
{
    static readonly string[] SpawnedNames =
    {
        "TestMage", "TestEnemy", "TestPlatform", "MageBolt", "FireTrailZone", "GravityWellZone",
    };

    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            foreach (var n in SpawnedNames)
                if (go.name == n) { Object.DestroyImmediate(go); break; }
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        ProfileService.Reset();
        GravityWellZone.ClearAllForTests();
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunTransient.Reset();
        FieldLayout.SetBossArena(false); // 일반 사냥 무대의 발판 데이터로 잰다
    }

    // ───────────────────────── 도우미 ─────────────────────────

    static EnemyHealth NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = go.AddComponent<EnemyMove>();
        var health = go.AddComponent<EnemyHealth>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(move, 0f);
        var rb = go.GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        move.enabled = false; // 배회 AI가 판정 위치를 흔들지 않게
        health.SetMaxHp(100000f);
        return health;
    }

    /// <summary>실제 씬처럼 발판 콜라이더를 깐다 — 예전 마법탄은 이 콜라이더(Ground 레이어)에 닿는 순간 터지거나 사라졌다.</summary>
    static void NewPlatformCollider(int index)
    {
        int ground = LayerMask.NameToLayer("Ground");
        Assume.That(ground, Is.GreaterThanOrEqualTo(0), "Ground 레이어가 없다");
        var go = new GameObject("TestPlatform") { layer = ground };
        go.transform.position = new Vector3(FieldLayout.Platforms[index, 0], FieldLayout.Platforms[index, 1], 0f);
        go.AddComponent<BoxCollider2D>().size = new Vector2(FieldLayout.Platforms[index, 2], FieldLayout.PlatformThickness);
    }

    static MageProjectile SpawnBolt(Vector2 pos, Vector2 velocity, bool explosive, bool gravityOrb, int tier) =>
        MageProjectile.Spawn(TestPrefabs.MageBolt, pos, velocity, damage: 5f, pierce: 2, life: 1.0f, sizeMul: 1f, color: Color.white,
            infinitePierce: false, charged: false, explosive: explosive, explosionPower: 0.26f, gravityOrb: gravityOrb,
            gravityCharge: 0f, tier: tier, casterPos: pos, casterFacing: 1);

    /// <summary>발이 <paramref name="feet"/>에 오게 마법사를 세운다(몸 반지름 0.25 = 키 0.5).</summary>
    static MageAttack NewMage(Vector2 feet)
    {
        var go = new GameObject("TestMage");
        go.transform.position = feet + new Vector2(0f, 0.25f);
        go.AddComponent<CircleCollider2D>().radius = 0.25f;
        var rb = go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterMover2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        return go.AddComponent<MageAttack>().Equipped();
    }

    static void Call(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, method + "()가 없다");
        m.Invoke(obj, args);
    }

    static float FeetY(MageAttack mage) => mage.transform.position.y - 0.25f;

    // ───────────────────────── 1. 중력점 높이 ─────────────────────────

    [Test]
    public void GravityWell_SpawnsAtImpactHeight_NotUnderTheMap()
    {
        MageSkillEffects.DetonateGravityOrb(new Vector2(6f, 2.9f), 1, 0f, 1);
        Assert.AreEqual(1, GravityWellZone.Active.Count);
        Assert.AreEqual(2.9f, GravityWellZone.Active[0].transform.position.y, 1e-4f,
            "중력점이 터진 높이가 아니라 다른 데 생겼다 — 예전엔 부호 실수로 늘 바닥 아래(-0.2)였다");

        GravityWellZone.ClearAllForTests();
        MageSkillEffects.DetonateGravityOrb(new Vector2(8f, -1f), 1, 0f, 1);
        Assert.AreEqual(FieldBounds.GroundY + MageSpecConfig.GravityGroundClearance,
            GravityWellZone.Active[0].transform.position.y, 1e-4f, "바닥보다 아래로는 못 내려가야 한다(원본 groundY - 20)");
    }

    // ───────────────────────── 2. 중력 노출 감소 ─────────────────────────

    [UnityTest]
    public IEnumerator GravityExposure_DecaysOverTime_LikeOriginal()
    {
        var now = NewEnemy(new Vector3(3f, 0.36f, 0f)).GetComponent<IVulnerable>();
        var later = NewEnemy(new Vector3(6f, 0.36f, 0f)).GetComponent<IVulnerable>();
        now.AddGravityExposure(1.0f);
        later.AddGravityExposure(1.0f);

        now.AddGravityExposure(0.3f); // 바로 더하면 1.3 ≥ 1.25
        Assert.IsTrue(now.IsVulnerable, "임계치를 넘었는데 취약이 안 걸렸다");

        yield return new WaitForSeconds(1.0f); // 원본은 초당 0.35씩 빠진다 → 0.65 이하
        later.AddGravityExposure(0.3f);
        Assert.IsFalse(later.IsVulnerable,
            "1초 쉬었는데도 노출이 그대로 남아 취약해졌다 — 원본은 중력점 밖에서 노출이 빠진다(:1727)");
    }

    // ───────────────────────── 3. 마법탄 지형 처리 ─────────────────────────

    [UnityTest]
    public IEnumerator GravityOrb_PassesThroughPlatform_AndAnchorsOnTheGround()
    {
        NewPlatformCollider(0); // 1층 첫 발판(x 3.2, 윗면 1.425)
        SpawnBolt(new Vector2(3.2f, 2.2f), new Vector2(0f, -10.8f), explosive: false, gravityOrb: true, tier: 1);

        float t = 0f;
        while (GravityWellZone.Active.Count == 0 && t < 0.8f) { yield return null; t += Time.deltaTime; }

        Assert.AreEqual(1, GravityWellZone.Active.Count, "중력탄이 안 터졌다");
        Assert.AreEqual(FieldBounds.GroundY + MageSpecConfig.GravityGroundClearance,
            GravityWellZone.Active[0].transform.position.y, 0.05f,
            "중력탄이 발판에서 터졌다 — 원본은 발판을 지나 바닥·벽·천장에서만 고정된다");
    }

    [UnityTest]
    public IEnumerator PlainBolt_Tier0_PassesThroughPlatform()
    {
        NewPlatformCollider(0);
        var bolt = SpawnBolt(new Vector2(3.2f, 2.2f), new Vector2(0f, -10.8f), explosive: false, gravityOrb: false, tier: 0);

        yield return new WaitForSeconds(0.15f); // 발판(1.425)을 지나 0.6쯤

        Assert.IsTrue(bolt != null, "0차 마법탄이 발판에 막혀 사라졌다 — 원본은 지형을 지나간다");
        Assert.Less(bolt.transform.position.y, 1.2f, "발판 아래로 지나가야 한다");
    }

    [UnityTest]
    public IEnumerator ExplosiveBolt_ExplodesAtTheCeiling()
    {
        // x=1.0 위쪽엔 발판이 없다. 적은 탄 길에서 비켜 두고(직격 아님) 폭발 반경(1.11) 안에만 둔다.
        var enemy = NewEnemy(new Vector3(1.8f, FieldLayout.ProjectileCeilingY - 0.2f, 0f));
        SpawnBolt(new Vector2(1.0f, FieldLayout.ProjectileCeilingY - 1.0f), new Vector2(0f, 10.8f),
            explosive: true, gravityOrb: false, tier: 1);

        yield return new WaitForSeconds(0.3f);

        Assert.Less(enemy.CurrentHp, enemy.MaxHp, "폭발탄이 천장에서 안 터졌다 — 원본은 y < 64px에서 터진다");
    }

    [UnityTest]
    public IEnumerator ExplosiveBolt_ExplodesOnPlatformFromAbove()
    {
        // 발판 콜라이더 없이 데이터(FieldLayout)만으로 착탄을 잰다 — 원본 groundYBelow와 같은 방식.
        var enemy = NewEnemy(new Vector3(10.0f, 1.7f, 0f)); // 1층 둘째 발판(x 9.0, 윗면 1.425) 위, 탄 길에서 1.0 옆
        var bolt = SpawnBolt(new Vector2(9.0f, 2.6f), new Vector2(0f, -10.8f), explosive: true, gravityOrb: false, tier: 1);

        yield return new WaitForSeconds(0.3f);

        Assert.IsTrue(bolt == null, "폭발탄이 발판을 지나갔다");
        Assert.Less(enemy.CurrentHp, enemy.MaxHp, "발판 위에서 터졌어야 할 폭발에 옆 적이 안 맞았다");
    }

    // ───────────────────────── 4. 순간이동 착지 ─────────────────────────

    [Test]
    public void Teleport_Down_OnGround_StaysAboveTheGround()
    {
        var mage = NewMage(new Vector2(5f, 0f));
        Call(mage, "TeleportTowards", 0f, -1f, 1, 1);
        Assert.AreEqual(0.01f, FeetY(mage), 1e-3f, "바닥 아래로 순간이동했다");
    }

    [Test]
    public void Teleport_Down_FromPlatform_DoesNotSinkThroughIt()
    {
        float top = FieldLayout.PlatformTopY(0);
        var mage = NewMage(new Vector2(FieldLayout.Platforms[0, 0], top));
        Call(mage, "TeleportTowards", 0f, -1f, 1, 1);
        Assert.AreEqual(top + 0.01f, FeetY(mage), 1e-3f,
            "밟고 있던 발판을 뚫고 내려갔다 — 원본은 지금 발 높이 아래의 발판·바닥보다 아래로 못 간다");
    }

    [Test]
    public void Teleport_Up_FromTopPlatform_StopsAtTheCeiling()
    {
        float top = FieldLayout.PlatformTopY(12); // 4층
        var mage = NewMage(new Vector2(FieldLayout.Platforms[12, 0], top));
        Call(mage, "TeleportTowards", 0f, 1f, 1, 1);
        Assert.AreEqual(FieldLayout.TeleportCeilingY, FeetY(mage), 1e-3f, "천장 위로 순간이동했다");
    }

    [Test]
    public void Teleport_Sideways_MovesOriginalDistance_AndLaysFlameLineAtFeetHeight()
    {
        var mage = NewMage(new Vector2(5f, 0f));
        Call(mage, "TeleportTowards", 1f, 0f, 1, 1);

        Assert.AreEqual(5f + MageSpecConfig.TeleportDistanceX, mage.transform.position.x, 1e-3f);
        Assert.AreEqual(0.01f, FeetY(mage), 1e-3f);

        var flames = Object.FindObjectsByType<FireTrailZone>(FindObjectsSortMode.None);
        Assert.AreEqual(MageSpecConfig.TeleportTrailPoints(1) + 1, flames.Length, "불길 수가 원본(n+1곳)과 다르다");
        foreach (var f in flames)
            Assert.AreEqual(MageSpecConfig.TeleportFlameLineHeight, f.transform.position.y, 0.02f,
                "불길이 원본 높이(발에서 0.28 위)에 안 깔렸다");
    }

    // ───────────────────────── 5. 수직 조준 탄 위치 ─────────────────────────

    [Test]
    public void StraightUpShot_LeavesFromTheFacingSide()
    {
        var mage = NewMage(new Vector2(5f, 0f));
        mage.GetComponent<CharacterMover2D>().SetFacing(-1);

        Call(mage, "FireAimed", 0f, MageBranch.None, 0, 0f, 1f); // 위로 똑바로

        var bolt = Object.FindFirstObjectByType<MageProjectile>();
        Assert.IsNotNull(bolt, "마법탄이 안 나갔다");
        Assert.AreEqual(5f - 0.26f, bolt.transform.position.x, 1e-3f,
            "왼쪽을 보고 위로 쐈는데 오른쪽에서 나갔다 — 원본은 바라보는 쪽(p.facing * 26)에서 나간다");
    }
}
}
