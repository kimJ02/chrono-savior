using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 대붕괴 폭발 고리 — 원본 `gravityBurst`(project_test.html:2114·:2123 생성, :5239 그리기). 2026-10-06 사용자가
/// "X를 눌러도 터진 게 안 보인다"고 해서 넣었다. 그림만이라 피해·넉백은 `MageSkillTreeTests`가 본다.
/// </summary>
public class GravityBurstRingTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test") || go.name == "GravityWellZone" || go.name == "GravityBurst"))
                Object.DestroyImmediate(go);
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        ProfileService.Reset();
        GravityWellZone.ClearAllForTests();
        CombatModifiers.Reset();
        CombatEvents.Reset();
        RunEvents.Reset();
        RunTransient.Reset();
    }

    static void NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = go.AddComponent<EnemyMove>();
        var health = go.AddComponent<EnemyHealth>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        move.moveSpeed = 0f;
        health.SetMaxHp(100000f); // 죽으면 대상이 사라져 임계 판정이 흔들린다
    }

    [UnityTest]
    public IEnumerator Collapse_ShowsBurstAtWell_ThenDisappearsAfterOriginalLife()
    {
        var wellPos = new Vector2(5f, 0.36f);
        GravityWellZone.SpawnOrMerge(wellPos, tier: 5, chargeK: 0.2f);
        yield return null;

        MageSkillEffects.GravityCollapse(5, new Vector2(3f, 0.36f), 1, null);

        var bursts = Object.FindObjectsByType<GravityBurstRing>(FindObjectsSortMode.None);
        Assert.AreEqual(1, bursts.Length, "적이 없으면 임계가 아니다 — 고리는 하나여야 한다");
        Assert.AreEqual(wellPos.x, bursts[0].transform.position.x, 1e-4f, "고리가 중력점 자리에 안 생겼다");
        Assert.IsFalse(bursts[0].Critical);
        Assert.IsFalse(bursts[0].RingOnly, "첫 고리는 원판까지 그린다(원본 `ringOnly` 없음)");
        Assert.Greater(bursts[0].Radius, 0f);

        yield return new WaitForSeconds(MageSpecConfig.CollapseBurstLife + 0.1f);
        Assert.AreEqual(0, Object.FindObjectsByType<GravityBurstRing>(FindObjectsSortMode.None).Length,
            "원본 수명(0.42초)이 지났는데 고리가 남아 있다");
    }

    [UnityTest]
    public IEnumerator CriticalCollapse_AddsOuterRingOnly()
    {
        var wellPos = new Vector2(5f, 0.36f);
        GravityWellZone.SpawnOrMerge(wellPos, tier: 5, chargeK: 0.2f);
        for (int i = 0; i < 5; i++) NewEnemy(new Vector3(5f + (i - 2) * 0.2f, 0.36f, 0f)); // 5마리 = 임계(원본 `count >= 5`)
        yield return null;

        var (_, critical, _) = MageSkillEffects.GravityCollapse(5, new Vector2(3f, 0.36f), 1, null);
        Assert.IsTrue(critical, "전제: 5마리면 임계 붕괴");

        var bursts = Object.FindObjectsByType<GravityBurstRing>(FindObjectsSortMode.None);
        Assert.AreEqual(2, bursts.Length, "임계면 원판 고리 + 바깥 고리(`ringOnly`) 두 개");
        int outer = bursts[0].RingOnly ? 0 : 1;
        Assert.IsTrue(bursts[outer].RingOnly);
        Assert.IsTrue(bursts[0].Critical && bursts[1].Critical);
        Assert.Greater(bursts[outer].Radius, bursts[1 - outer].Radius, "바깥 고리가 더 커야 한다(원본 r × 1.38)");
    }
}

}
