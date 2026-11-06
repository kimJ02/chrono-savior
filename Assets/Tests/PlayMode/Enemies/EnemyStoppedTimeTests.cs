using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 몹이 **멈춰 있어야 할 때** 아무것도 하지 않는지(2026-11-07 최종 점검에서 원본대로 고친 것).
/// 원본은 스폰 보호 중 적 갱신을 통째로 건너뛰고(`if (e.spawnInvuln > 0) { …; continue; }` :4023),
/// 멈춘 시간(일시정지·결과·히트스톱)엔 `updateRun` 자체를 부르지 않는다(`frame()` :7168~7171).
/// </summary>
public class EnemyStoppedTimeTests
{
    /// <summary>행동 부품 대신 — 몇 번 불렸는지만 센다.</summary>
    sealed class CountingMotion : MonoBehaviour, IEnemyMotion
    {
        public int Calls;
        public float GetHorizontalSpeed(float dt, Transform target, float baseSpeed) { Calls++; return 0f; }
    }

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Test")) Object.Destroy(go);
    }

    static EnemyMove NewEnemy(Vector3 pos, bool withMotion)
    {
        var go = new GameObject("TestStoppedEnemy") { tag = "Enemy" };
        go.transform.position = pos;
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        if (withMotion) go.AddComponent<CountingMotion>(); // EnemyMove.Awake가 찾도록 먼저
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<EnemyHealth>();
        return go.AddComponent<EnemyMove>();
    }

    /// <summary>스폰 보호 중엔 행동 부품(궁수 사격·멧돼지 돌진 상태)을 부르지 않고, 풀리면 부른다.</summary>
    [UnityTest]
    public IEnumerator SpawnProtected_Enemy_DoesNotRunItsBehaviour()
    {
        var move = NewEnemy(new Vector3(5f, 3f, 0f), withMotion: true);
        var motion = move.GetComponent<CountingMotion>();
        move.SetSpawnProtection(0.3f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(0, motion.Calls, "보호 중에 행동 부품이 불렸다");

        move.SetSpawnProtection(0f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.Greater(motion.Calls, 0, "보호가 풀렸는데 행동 부품이 안 불린다(검사가 비어 있지 않게)");
    }

    /// <summary>시간이 멈춘 동안 겹친 몹은 접촉 피해를 주지 않고, 시간이 흐르면 준다.</summary>
    [UnityTest]
    public IEnumerator Contact_DoesNotHurt_WhileTimeIsStopped()
    {
        var player = new GameObject("TestStoppedPlayer");
        player.transform.position = new Vector3(5f, 3f, 0f);
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        player.AddComponent<CircleCollider2D>().radius = 0.5f;
        var hp = player.AddComponent<PlayerHealth>();
        var move = NewEnemy(new Vector3(5f, 3f, 0f), withMotion: false);
        move.Target = player.transform;
        move.SetSpawnProtection(0f); // 막 나온 몹의 보호(2초)는 이 테스트와 상관없다
        Time.timeScale = 0f;            // 첫 프레임부터 멈춘 채로(한 번 맞으면 무적 시간이 다음 타격을 가린다)
        float full = hp.CurrentHp;
        for (int i = 0; i < 5; i++) yield return null;
        Assert.AreEqual(full, hp.CurrentHp, 1e-4f, "멈춘 시간에 접촉 피해를 받았다");

        Time.timeScale = 1f;
        for (int i = 0; i < 5; i++) yield return null;
        Assert.Less(hp.CurrentHp, full, "시간이 흐르면 겹친 몹이 때린다(검사가 비어 있지 않게)");
    }
}

}
