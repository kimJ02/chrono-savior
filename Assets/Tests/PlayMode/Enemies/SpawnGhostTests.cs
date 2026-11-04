using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 스폰 보호 중 반투명 유령 연출(2026-10-24 사용자 "적 스폰 중일 때 효과") — 원본
/// `if (e.spawnInvuln > 0) ctx.globalAlpha = 0.4 + 0.1 * Math.sin(state.time * 10)`(project_test.html:4772~4773).
/// </summary>
public class SpawnGhostTests
{
    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
    }

    static (EnemyMove move, SpriteRenderer sr) NewEnemy(Color color, float protect)
    {
        var go = new GameObject("TestEnemy") { tag = "Enemy" };
        var sr = go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = go.AddComponent<EnemyMove>();
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        move.moveSpeed = 0f;
        sr.color = color; // 스포너가 Instantiate 뒤에 칠하는 것처럼(EnemySpawner)
        move.SetSpawnProtection(protect);
        return (move, sr);
    }

    [Test]
    public void GhostAlpha_IsOriginalFormula()
    {
        Assert.AreEqual(0.4f, EnemyMove.SpawnGhostAlpha(0f), 1e-5f);
        Assert.AreEqual(0.5f, EnemyMove.SpawnGhostAlpha(Mathf.PI / 20f), 1e-5f, "sin 최대 → 0.5");
        Assert.AreEqual(0.3f, EnemyMove.SpawnGhostAlpha(3f * Mathf.PI / 20f), 1e-5f, "sin 최소 → 0.3");
    }

    [UnityTest]
    public IEnumerator SpawnProtected_IsTranslucent_KeepsColor_ThenOpaque()
    {
        var kind = new Color(1f, 0.55f, 0.4f, 1f);
        var (move, sr) = NewEnemy(kind, 0.5f);
        yield return null;
        yield return null;
        Assert.IsTrue(move.IsSpawnProtected, "전제: 스폰 보호 중");
        Assert.That(sr.color.a, Is.InRange(0.3f - 1e-4f, 0.5f + 1e-4f), "보호 중엔 반투명(0.4 ± 0.1)");
        Assert.AreEqual(kind.r, sr.color.r, 1e-5f, "색은 스포너가 칠한 그대로");
        Assert.AreEqual(kind.g, sr.color.g, 1e-5f);
        Assert.AreEqual(kind.b, sr.color.b, 1e-5f);

        yield return new WaitForSeconds(0.7f);
        Assert.IsFalse(move.IsSpawnProtected, "보호가 끝났다");
        Assert.AreEqual(1f, sr.color.a, 1e-5f, "보호가 끝나면 다시 불투명");
        Assert.AreEqual(kind.r, sr.color.r, 1e-5f, "색은 그대로");
    }
}

}
