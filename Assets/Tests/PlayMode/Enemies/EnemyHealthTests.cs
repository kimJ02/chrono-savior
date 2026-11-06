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
/// 스프린트 2(체력 & 데미지) — 적 쪽 검증. 원본 `dealDamage()`(project_test.html:1657)의
/// 대상 처리(체력 감소·넉백·사망)와 스폰 무적(:1659)이 실제로 맞물려 도는지 확인한다.
///
/// 주의: `AddComponent`는 `Awake`를 동기 실행하므로 그 뒤에 `maxHp`를 바꿔도 이미 초기화된
/// `CurrentHp`엔 반영되지 않는다(이 프로젝트에서 이미 두 번 겪은 함정). 그래서 테스트는
/// 프리팹 기본값(고블린 38)을 그대로 쓰고 기대값을 `MaxHp`에서 역산한다.
/// </summary>
public class EnemyHealthTests
{
    /// <summary>
    /// 테스트용 적. 두 가지를 반드시 처리한다:
    /// 1. **스폰 무적 해제** — `EnemyMove.Awake`가 2초 무적을 걸어서(원본 spawnProtect) 그대로 두면
    ///    모든 피해가 무시된다. public 필드를 바꿔도 Awake는 이미 돌았으므로 private 타이머를 직접 0으로.
    /// 2. **중력 off** — 테스트끼리 좌표가 겹칠 때 낙하하는 적이 다른 테스트의 오브젝트를 물리로 밀어내
    ///    엉뚱한 테스트를 깨뜨린다(실제로 겪음). 낙하가 필요한 테스트가 아니면 꺼둔다.
    /// </summary>
    static GameObject NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();          // EnemyHealth가 RequireComponent로 요구
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = go.AddComponent<EnemyMove>();    // Rigidbody2D도 같이 붙음
        go.AddComponent<EnemyHealth>();

        var timerField = typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        return go;
    }

    /// <summary>
    /// 테스트가 중간에 assert로 중단돼도 오브젝트가 남지 않게 한다 — 남으면 다음 테스트의 물리에 끼어든다.
    /// </summary>
    [SetUp]
    public void ResetStatics()
    {
        // 콤보가 쌓이면 피해 배수가 올라가 "몇 방에 죽는가" 기대값이 어긋난다(정적 상태 오염).
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunState.Reset();
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            if (go.name.StartsWith("Test") || go.name == "MageBolt") Object.DestroyImmediate(go);
        }
    }

    /// <summary>
    /// 핵심 회귀 방지: 예전엔 맞으면 **한 방에 파괴**됐다. 이제 체력 38을 9씩 깎아야 하므로
    /// 두 방으론 절대 안 죽고(최대 굴림이어도 9×1.1×1.7×2 = 33.7 < 38), 다섯 방이면 반드시 죽는다
    /// (최소 굴림이어도 9×0.9×5 = 40.5 > 38).
    /// </summary>
    [UnityTest]
    public IEnumerator TakeDamage_ReducesHp_AndDoesNotDieInOneHit()
    {
        var attacker = new GameObject("TestAttacker");
        attacker.transform.position = Vector3.zero;
        var enemy = NewEnemy(new Vector3(2f, 0f, 0f));
        var health = enemy.GetComponent<EnemyHealth>();
        Assert.AreEqual(38f, health.MaxHp, 0.01f, "고블린 기본 체력이 원본(38)과 다르다");

        health.TakeDamage(9f, attacker);
        Assert.Less(health.CurrentHp, health.MaxHp, "피해를 줬는데 체력이 안 깎였다");
        Assert.IsFalse(health.IsDead, "한 방에 죽었다 — 체력 시스템이 안 걸린 것");

        health.TakeDamage(9f, attacker);
        yield return null;
        Assert.IsTrue(enemy != null, "두 방(최대 33.7)으로 체력 38이 소진돼 파괴됐다");

        for (int i = 0; i < 3; i++)
        {
            if (enemy == null) break;
            enemy.GetComponent<EnemyHealth>().TakeDamage(9f, attacker);
        }
        yield return null;
        Assert.IsTrue(enemy == null, "다섯 방(최소 40.5)을 맞았는데도 안 죽었다");

        Object.Destroy(attacker);
        yield return null;
    }

    /// <summary>
    /// 원본은 `dealDamage` 진입 즉시 스폰 무적을 확인하고 피해 0을 반환한다(project_test.html:1659).
    /// 무적이 풀리면 정상적으로 피해가 들어가야 한다.
    /// </summary>
    [UnityTest]
    public IEnumerator SpawnProtected_TakesNoDamage_ThenTakesDamageAfterExpiry()
    {
        var attacker = new GameObject("TestAttacker");
        var enemy = NewEnemy(new Vector3(2f, 0f, 0f));
        var health = enemy.GetComponent<EnemyHealth>();
        var move = enemy.GetComponent<EnemyMove>();

        // AddComponent가 Awake를 이미 돌렸으므로 public 필드가 아니라 private 타이머를 직접 세팅한다.
        var timerField = typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(timerField);
        timerField.SetValue(move, 0.15f);
        Assert.IsTrue(move.IsSpawnProtected, "스폰 무적 상태를 만들지 못했다");

        float before = health.CurrentHp;
        health.TakeDamage(9f, attacker);
        Assert.AreEqual(before, health.CurrentHp, 0.001f, "스폰 무적 중인데 체력이 깎였다");

        yield return new WaitForSeconds(0.3f); // 무적 만료 대기

        health.TakeDamage(9f, attacker);
        Assert.Less(health.CurrentHp, before, "무적이 풀렸는데도 피해가 안 들어갔다");

        Object.Destroy(attacker);
        Object.Destroy(enemy);
        yield return null;
    }

    /// <summary>
    /// 원본 `e.kbx = sign(e.x - player.x) * 240`(:1679) + 매 프레임 지수 감쇠(:4021).
    /// 넉백은 AI 이동을 덮어쓰는 게 아니라 더해지는 별도 성분이라, 가해자 반대쪽으로 밀렸다가
    /// 감쇠 후 원래 이동으로 돌아와야 한다. `EnemyMove`가 매 FixedUpdate에 속도를 통째로
    /// 덮어쓰기 때문에, 넉백을 그냥 속도에 넣었다면 이 테스트가 실패한다.
    /// </summary>
    [UnityTest]
    public IEnumerator Knockback_PushesAwayFromAttacker_ThenDecays()
    {
        var attacker = new GameObject("TestAttacker");
        attacker.transform.position = new Vector3(0f, 0f, 0f);
        var enemy = NewEnemy(new Vector3(3f, 0f, 0f)); // 가해자보다 오른쪽 → 오른쪽으로 밀려야 함
        var move = enemy.GetComponent<EnemyMove>();
        var rb = enemy.GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;      // 낙하와 분리해서 수평 넉백만 본다
        move.moveSpeed = 0f;       // AI 이동을 꺼서 넉백 성분만 측정

        enemy.GetComponent<EnemyHealth>().TakeDamage(1f, attacker);
        yield return new WaitForFixedUpdate();

        float vxRightAfterHit = rb.linearVelocity.x;
        Assert.Greater(vxRightAfterHit, 0.5f, "가해자 반대(오른쪽) 방향으로 밀려나지 않았다");

        // 감쇠 확인: 초당 9배율이면 0.5초 뒤엔 거의 사라져야 한다
        float t = 0f;
        while (t < 0.5f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }
        Assert.Less(Mathf.Abs(rb.linearVelocity.x), Mathf.Abs(vxRightAfterHit) * 0.2f, "넉백이 감쇠하지 않고 계속 남아있다");

        Object.Destroy(attacker);
        Object.Destroy(enemy);
        yield return null;
    }

    /// <summary>
    /// 관통 투사체가 실제로 체력을 깎는지(= `Destroy()` 직접 호출에서 `TakeDamage()`로 전환됐는지)
    /// 통합 경로로 확인한다. 무차지 마법탄 한 발로는 고블린이 죽지 않아야 한다.
    /// </summary>
    [UnityTest]
    public IEnumerator MageProjectile_DamagesInsteadOfInstantKill()
    {
        var enemy = NewEnemy(new Vector3(1f, 0.36f, 0f));
        enemy.GetComponent<Rigidbody2D>().gravityScale = 0f;
        var health = enemy.GetComponent<EnemyHealth>();

        ChronoSavior.Characters.MageProjectile.Spawn(TestPrefabs.MageBolt,
            new Vector3(0.5f, 0.36f, 0f), new Vector2(6f, 0f),
            damage: 9f, pierce: 2, life: 1f, sizeMul: 1f, color: Color.white,
            infinitePierce: false, charged: false, explosive: false, explosionPower: 0f,
            gravityOrb: false, gravityCharge: 0f, tier: 0,
            casterPos: new Vector3(0f, 0.36f, 0f), casterFacing: 1);

        float t = 0f;
        while (t < 0.5f && enemy != null) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }

        Assert.IsTrue(enemy != null, "마법탄 한 발에 즉사했다 — 체력을 거치지 않고 파괴되는 것");
        Assert.Less(health.CurrentHp, health.MaxHp, "마법탄에 맞았는데 체력이 안 깎였다");

        if (enemy != null) Object.Destroy(enemy);
        yield return null;
    }

    /// <summary>
    /// 데미지 굴림(`Combat.DamageCalculator`) — 원본 `base × rand(0.9,1.1) × crit`(반올림 전 값 — 반올림과 최소 1은 맞는 쪽이 대상 배수까지 곱한 뒤 한 번, 아래 테스트).
    /// 난수라 개별 값이 아니라 범위·치명타 발생을 통계로 확인한다.
    /// </summary>
    [Test]
    public void DamageCalculator_StaysInVarianceRange_AndCritsOccur()
    {
        const float baseDmg = 100f;
        int critCount = 0;
        for (int i = 0; i < 2000; i++)
        {
            float dmg = DamageCalculator.Roll(baseDmg, out bool crit);
            if (crit) critCount++;
            // 비치명타 하한 90, 치명타 상한 100×1.1×1.7 = 187
            Assert.GreaterOrEqual(dmg, 90f - 0.001f, "피해가 -10% 하한보다 낮다");
            Assert.LessOrEqual(dmg, 187f + 0.001f, "피해가 치명타 상한보다 높다");
        }
        // 기대 10%(2000회 중 200회). 난수 편차를 넉넉히 잡아 5~17% 범위면 통과.
        Assert.Greater(critCount, 100, $"치명타가 거의 안 나온다({critCount}/2000) — 확률 10% 적용 확인 필요");
        Assert.Less(critCount, 340, $"치명타가 너무 자주 나온다({critCount}/2000)");

    }

    /// <summary>
    /// 피해는 맞는 쪽에서 **대상 배수까지 곱한 뒤 한 번** 반올림하고 최소 1(원본 `max(1, round(... × vulnMult × lvF ...))` :1664,
    /// 반올림은 JS `Math.round` — .5는 위로). 2026-11-07 최종 점검 전엔 굴림에서 한 번, 배수 뒤 한 번 두 번 반올림했다.
    /// </summary>
    [UnityTest]
    public IEnumerator Damage_IsRoundedOnceAtTheEnd_AndAtLeastOne()
    {
        var enemy = new GameObject("TestRoundEnemy");
        enemy.AddComponent<SpriteRenderer>();
        var health = enemy.AddComponent<EnemyHealth>();
        health.SetMaxHp(1000f);
        health.SetLevel(1);
        yield return null;

        health.TakeTickDamage(10.5f);                        // .5는 위로(짝수 쪽이 아니라) → 11
        Assert.AreEqual(1000f - 11f, health.CurrentHp, 1e-4f);
        health.TakeTickDamage(0.01f);                        // 최소 1
        Assert.AreEqual(1000f - 12f, health.CurrentHp, 1e-4f);
        Object.Destroy(enemy);
    }
}

}
