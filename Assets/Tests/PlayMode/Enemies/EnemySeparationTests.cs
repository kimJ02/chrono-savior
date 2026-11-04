using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 몹끼리 물리 충돌 없음(2026-10-25 사용자 "몬스터끼리 충돌이 일어나서 문제였다, 겹쳐도 된다"). 원형끼리 밀리다 위로 타고 올라가 쌓이던 원인을 끄고,
/// 원본처럼 겹쳐 지나간다.
/// </summary>
public class EnemySeparationTests
{
    [SetUp]
    public void Setup()
    {
        GameState.Reset();
        CombatModifiers.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
    }

    static void NewGround()
    {
        var g = new GameObject("TestGround");
        g.layer = LayerMask.NameToLayer("Ground");
        g.transform.position = new Vector3(12f, FieldBounds.GroundY - 0.5f, 0f);
        g.AddComponent<BoxCollider2D>().size = new Vector2(30f, 1f);
    }

    /// <summary>게임 몹처럼 Enemy 레이어에 둔 몹. 반지름 0.5, 스폰 보호 없음, 기본은 제자리.</summary>
    static EnemyMove NewEnemy(float x, float y, float speed = 0f)
    {
        var go = new GameObject("TestSepEnemy");
        go.tag = "Enemy";
        go.layer = LayerMask.NameToLayer("Enemy");
        go.transform.position = new Vector3(x, y, 0f);
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var move = go.AddComponent<EnemyMove>();
        move.moveSpeed = speed;
        move.wanderIntervalRange = new Vector2(100f, 100f); // 배회하다 방향을 안 바꾸게
        move.SetSpawnProtection(0f);
        return move;
    }

    static IEnumerator Physics(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
    }

    [Test]
    public void EnemyPhysicsContact_WithOtherEnemies_IsOff()
    {
        var e = NewEnemy(5f, 0.5f);
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        Assert.AreNotEqual(0, e.GetComponent<Rigidbody2D>().excludeLayers.value & (1 << enemyLayer),
                           "몹끼리 물리로 부딪히면 원형끼리 밀다가 위로 타고 올라간다");
    }

    [UnityTest]
    public IEnumerator OverlappingEnemies_StayOnGround_NoPushNoStacking()
    {
        NewGround();
        var a = NewEnemy(5f, FieldBounds.GroundY + 0.5f);
        var b = NewEnemy(5.1f, FieldBounds.GroundY + 0.5f);
        yield return Physics(1.2f);

        Assert.AreEqual(5f, a.WorldPosition.x, 0.02f, "몹끼리 밀어내지 않는다 — 겹쳐도 된다(원본처럼)");
        Assert.AreEqual(5.1f, b.WorldPosition.x, 0.02f, "몹끼리 밀어내지 않는다");
        Assert.AreEqual(FieldBounds.GroundY + 0.5f, a.WorldPosition.y, 0.05f, "위로 올라타면 안 된다");
        Assert.AreEqual(FieldBounds.GroundY + 0.5f, b.WorldPosition.y, 0.05f, "위로 올라타면 안 된다");
    }

    [UnityTest]
    public IEnumerator WalkingEnemy_PassesThroughAnother_WithoutClimbing()
    {
        NewGround();
        var standing = NewEnemy(5f, FieldBounds.GroundY + 0.5f);
        var walker = NewEnemy(6.5f, FieldBounds.GroundY + 0.5f, speed: 3f);
        walker.SetDirection(-1);
        yield return Physics(1.2f); // 첫 배회 방향 전환(Awake에서 1.5~3.5초로 정해짐)보다 짧게

        Assert.Less(walker.WorldPosition.x, standing.WorldPosition.x, "막히지 않고 지나간다(원본처럼 겹쳐 지나감)");
        Assert.AreEqual(FieldBounds.GroundY + 0.5f, walker.WorldPosition.y, 0.05f, "앞 몹 위로 타고 올라가면 안 된다");
        Assert.AreEqual(5f, standing.WorldPosition.x, 0.02f, "서 있는 몹은 밀리지 않는다");
    }
}

}
