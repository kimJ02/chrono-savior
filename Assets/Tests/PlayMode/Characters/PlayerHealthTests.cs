using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 스프린트 2(체력 & 데미지) — 플레이어 쪽 검증. 원본 `damagePlayer()`(project_test.html:1879)의
/// 무적시간·피격 반동·사망을 확인한다. 접촉 데미지는 원본이 쿨다운 없이 매 프레임 호출하고
/// 반복 피해 방지를 **무적시간이 전담**하므로(:4144~4148), 그 구조가 실제로 성립하는지가 핵심이다.
/// </summary>
public class PlayerHealthTests
{
    static GameObject NewPlayer(Vector3 pos)
    {
        var go = new GameObject("TestPlayer");
        go.tag = "Player";
        go.transform.position = pos;
        int layer = LayerMask.NameToLayer("Player");
        if (layer >= 0) go.layer = layer; // 실제 게임과 같은 물리 레이어(Player×Enemy 충돌 꺼짐)
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f; // 낙하와 분리
        go.AddComponent<PlayerHealth>();
        return go;
    }

    /// <summary>테스트가 중단돼도 오브젝트가 남아 다음 테스트의 물리에 끼어들지 않게 한다.</summary>
    [TearDown]
    public void Cleanup()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            if (go.name.StartsWith("Test") || go.name == "MageBolt") Object.DestroyImmediate(go);
        }
    }

    [UnityTest]
    public IEnumerator TakeDamage_ReducesHp_FromOriginalMaxHp()
    {
        var attacker = new GameObject("TestAttacker");
        var player = NewPlayer(new Vector3(2f, 0f, 0f));
        var hp = player.GetComponent<PlayerHealth>();

        Assert.AreEqual(100f, hp.MaxHp, 0.01f, "플레이어 최대 체력이 원본(baseHp 100)과 다르다");
        hp.TakeDamage(13f, attacker); // 고블린 접촉 데미지

        // ±10% 난수 → 11.7~14.3이 깎인다
        float lost = hp.MaxHp - hp.CurrentHp;
        Assert.GreaterOrEqual(lost, 11f, "피해가 예상 범위보다 적다");
        Assert.LessOrEqual(lost, 15f, "피해가 예상 범위보다 크다");

        Object.Destroy(attacker);
        Object.Destroy(player);
        yield return null;
    }

    /// <summary>
    /// 원본은 접촉 시 **쿨다운 없이 매 프레임** `damagePlayer`를 부르고, 반복 피해는 무적시간 0.9초가 막는다.
    /// 즉 연달아 여러 번 호출해도 딱 한 번만 들어가야 한다 — 이게 안 되면 접촉 즉시 즉사한다.
    /// </summary>
    [UnityTest]
    public IEnumerator InvulnWindow_BlocksRepeatedDamage()
    {
        var attacker = new GameObject("TestAttacker");
        var player = NewPlayer(new Vector3(2f, 0f, 0f));
        var hp = player.GetComponent<PlayerHealth>();

        hp.TakeDamage(13f, attacker);
        float afterFirst = hp.CurrentHp;
        Assert.Greater(hp.InvulnRemaining, 0f, "피격 후 무적시간이 안 걸렸다");

        for (int i = 0; i < 10; i++) hp.TakeDamage(13f, attacker); // 원본처럼 연타로 들어오는 상황
        Assert.AreEqual(afterFirst, hp.CurrentHp, 0.001f, "무적시간 중인데 반복 피해가 들어갔다");

        yield return new WaitForSeconds(1.0f); // 무적(0.9초) 만료 대기

        hp.TakeDamage(13f, attacker);
        Assert.Less(hp.CurrentHp, afterFirst, "무적이 풀렸는데도 피해가 안 들어갔다");

        Object.Destroy(attacker);
        Object.Destroy(player);
        yield return null;
    }

    /// <summary>
    /// 원본 `p.vx = sign(p.x - srcX) * 260; p.vy = min(p.vy, -220)`(:1908~1909).
    /// 원본 Y+가 화면 아래라 -220은 **위로** 튕기는 것 → Unity에선 +2.2.
    /// </summary>
    [UnityTest]
    public IEnumerator Knockback_PushesAwayFromSource_AndUpward()
    {
        var attacker = new GameObject("TestAttacker");
        attacker.transform.position = new Vector3(0f, 0f, 0f);
        var player = NewPlayer(new Vector3(2f, 0f, 0f)); // 가해자 오른쪽 → 오른쪽으로 밀려야
        var hp = player.GetComponent<PlayerHealth>();
        var rb = player.GetComponent<Rigidbody2D>();

        hp.TakeDamage(13f, attacker);

        Assert.Greater(rb.linearVelocity.x, 0.5f, "가해자 반대(오른쪽) 방향으로 안 밀렸다");
        Assert.GreaterOrEqual(rb.linearVelocity.y, 2.0f, "위로 튕기지 않았다(원본 -220 = 위로 2.2유닛/s)");

        Object.Destroy(attacker);
        Object.Destroy(player);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Died_EventFires_WhenHpReachesZero()
    {
        var attacker = new GameObject("TestAttacker");
        var player = NewPlayer(new Vector3(2f, 0f, 0f));
        var hp = player.GetComponent<PlayerHealth>();

        bool died = false;
        hp.Died += () => died = true;

        // 무적시간 때문에 한 번에 한 방씩만 들어간다 → 큰 피해로 한 번에 죽인다
        hp.TakeDamage(500f, attacker);

        Assert.IsTrue(hp.IsDead, "체력이 0 이하인데 IsDead가 아니다");
        Assert.AreEqual(0f, hp.CurrentHp, 0.001f, "체력이 음수로 남았다");
        Assert.IsTrue(died, "사망 이벤트가 발생하지 않았다");

        Object.Destroy(attacker);
        Object.Destroy(player);
        yield return null;
    }

    /// <summary>
    /// 통합 경로: 적이 실제로 플레이어에 접촉하면 체력이 깎이는지.
    /// 예전엔 `EnemyMove.TryAttack()`이 빈 스텁이라 플레이어가 절대 안 죽었다.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyContact_DamagesPlayer()
    {
        var player = NewPlayer(new Vector3(0f, 0f, 0f));
        var hp = player.GetComponent<PlayerHealth>();

        var enemy = new GameObject("TestEnemy");
        enemy.tag = "Enemy";
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) enemy.layer = enemyLayer; // Player×Enemy 물리 충돌이 꺼져야 겹친 채로 있을 수 있다
        enemy.transform.position = new Vector3(0.2f, 0f, 0f); // 겹쳐 있는 상태
        enemy.AddComponent<SpriteRenderer>();
        enemy.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = enemy.AddComponent<EnemyMove>();
        enemy.AddComponent<EnemyHealth>();
        enemy.GetComponent<Rigidbody2D>().gravityScale = 0f;
        move.moveSpeed = 0f;

        // 스폰 무적(2초)을 기다리는 대신 private 타이머를 직접 0으로 — 테스트를 빠르게.
        var timerField = typeof(EnemyMove).GetField("spawnProtectTimer",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        timerField.SetValue(move, 0f);

        float t = 0f;
        while (t < 0.5f && hp.CurrentHp >= hp.MaxHp) { yield return null; t += Time.deltaTime; }

        Assert.Less(hp.CurrentHp, hp.MaxHp, "적과 겹쳐 있는데 접촉 데미지가 안 들어갔다");

        Object.Destroy(enemy);
        Object.Destroy(player);
        yield return null;
    }
}

}
