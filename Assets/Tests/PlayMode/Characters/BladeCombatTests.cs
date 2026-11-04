using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 섬영 0차 기본공격 검증 — 원본 `bladeSpinTick`/`bladeSpinHit`/`bladeLifesteal`/`bladeDmgMult`/
/// `bladeDirInvuln`(project_test.html:2420~2517)을 그대로 옮겼는지 확인한다.
///
/// `GameInput`이 실제 키보드만 읽어서 시뮬레이트할 수 없는 부분(공격 입력, 방향키 홀드)은
/// `GunnerAttackTests`/`PlayerAttackTests`와 같은 관례대로 private 메서드를 리플렉션으로 직접
/// 호출해서 검증한다.
/// </summary>
public class BladeCombatTests
{
    [SetUp]
    public void Setup()
    {
        ProfileService.Reset();
        CombatModifiers.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        ProfileService.Reset();
        CombatModifiers.Reset();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        }
    }

    static GameObject NewBlade(Vector3 pos, bool withHealth = true)
    {
        var go = new GameObject("TestBlade");
        go.transform.position = pos;
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        if (withHealth) go.AddComponent<PlayerHealth>();
        go.AddComponent<BladeCombat>();
        return go;
    }

    static GameObject NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        // 실제 배치와 같은 반지름 0.5(Enemy_Base.prefab 기준) — GunnerAttackTests와 동일한 관례.
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        go.AddComponent<EnemyMove>();
        go.AddComponent<EnemyHealth>();
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        return go;
    }

    /// <summary>스폰 무적(2초) 대기 대신 private 타이머를 직접 0으로 — GunnerAttackTests와 동일한 관례.</summary>
    static void ClearSpawnProtection(GameObject enemy)
    {
        var move = enemy.GetComponent<EnemyMove>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(move, 0f);
    }

    static float InvokeSpeedDamageMultiplier(BladeCombat blade, float speedRatio)
    {
        var m = typeof(BladeCombat).GetMethod("SpeedDamageMultiplier", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "SpeedDamageMultiplier(float)가 없다");
        return (float)m.Invoke(blade, new object[] { speedRatio });
    }

    static void SetMsSurplus(BladeCombat blade, float value)
    {
        var f = typeof(BladeCombat).GetField("msSurplus", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(f, "msSurplus 필드가 없다");
        f.SetValue(blade, value);
    }

    static void InvokeSpinAttack(BladeCombat blade, float dmgMultiplier)
    {
        var m = typeof(BladeCombat).GetMethod("SpinAttack", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "SpinAttack(float)가 없다");
        m.Invoke(blade, new object[] { dmgMultiplier });
    }

    static void InvokeApplyDirectionalInvuln(BladeCombat blade, bool held)
    {
        var m = typeof(BladeCombat).GetMethod("ApplyDirectionalInvuln", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "ApplyDirectionalInvuln(bool)이 없다");
        m.Invoke(blade, new object[] { held });
    }

    [UnityTest]
    public IEnumerator BladeKit_DeclaresInertialMoveMode()
    {
        var go = NewBlade(Vector3.zero, withHealth: false);
        yield return null;

        var kit = (ICharacterKit)go.GetComponent<BladeCombat>();
        Assert.AreEqual(CharacterId.Blade, kit.Character);
        Assert.AreEqual(CharacterMover2D.MoveMode.Inertial, kit.RequiredMoveMode,
            "섬영은 관성 가속 이동이어야 한다(원본 bladeMove) — 마법사·메카닉과 달리 Instant가 아니다");
    }

    [UnityTest]
    public IEnumerator Cooldown_ScalesWithAttackSpeedUpgrade()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f), withHealth: false);
        var blade = go.GetComponent<BladeCombat>();
        yield return null;

        Assert.AreEqual(0.13f, blade.cooldown, 0.001f, "기본 쿨다운이 원본(CONFIG.blade.spin.cd 0.13)과 다르다");

        // 공격속도 강화 5단계 → statAs = 1 + 5*0.04 = 1.2 (원본 :2514 max(0.35, statAs()) 분모)
        ProfileService.Current.upgrades.atkSpeed = 5;
        yield return null;

        Assert.AreEqual(0.13f / 1.2f, blade.cooldown, 0.001f, "공격속도 강화가 회전베기 쿨다운에 반영되지 않았다");
    }

    [UnityTest]
    public IEnumerator SpeedDamageMultiplier_AtRest_Is_One()
    {
        var go = NewBlade(Vector3.zero, withHealth: false);
        var blade = go.GetComponent<BladeCombat>();
        yield return null;

        SetMsSurplus(blade, 0f);
        float mult = InvokeSpeedDamageMultiplier(blade, 0f);
        Assert.AreEqual(1f, mult, 0.001f, "정지 상태 피해 배수가 원본(bladeDmgMult ×1)과 다르다");
    }

    [UnityTest]
    public IEnumerator SpeedDamageMultiplier_AtTopSpeed_Is_2Point4x()
    {
        var go = NewBlade(Vector3.zero, withHealth: false);
        var blade = go.GetComponent<BladeCombat>();
        yield return null;

        SetMsSurplus(blade, 0f);
        float mult = InvokeSpeedDamageMultiplier(blade, 1f);
        // 원본 1 + spdDmg(1.4) = 2.4 (project_test.html:619, :2439~2442) — HANDOFF.md 스펙과 일치.
        Assert.AreEqual(2.4f, mult, 0.001f, "최고 속도 피해 배수가 원본(정지×1→최고속×2.4)과 다르다");
    }

    [UnityTest]
    public IEnumerator SpinAttack_HitsEnemyInRadius_SkipsSpawnProtectedAndOutOfRange()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f), withHealth: false);
        var blade = go.GetComponent<BladeCombat>();

        var near = NewEnemy(new Vector3(2.6f, 0.5f, 0f));          // 반경(1.05) 안, 무적 해제
        var spawnProtected = NewEnemy(new Vector3(2.5f, 0.5f, 0f)); // 반경 안이지만 스폰 무적 유지
        var farAway = NewEnemy(new Vector3(6f, 0.5f, 0f));          // 반경 밖, 무적 해제
        ClearSpawnProtection(near);
        ClearSpawnProtection(farAway);
        // spawnProtected는 일부러 그대로 둔다(기본값이 스폰 무적 상태).
        yield return null;

        InvokeSpinAttack(blade, 1f);
        yield return null;

        var nearHp = near.GetComponent<EnemyHealth>();
        var protHp = spawnProtected.GetComponent<EnemyHealth>();
        var farHp = farAway.GetComponent<EnemyHealth>();

        Assert.Less(nearHp.CurrentHp, nearHp.MaxHp, "반경 안의 적이 회전베기에 안 맞았다");
        Assert.AreEqual(protHp.MaxHp, protHp.CurrentHp, 0.001f, "스폰 무적 중인 적이 맞았다(원본 e.spawnInvuln>0 스킵 위반)");
        Assert.AreEqual(farHp.MaxHp, farHp.CurrentHp, 0.001f, "반경 밖의 적이 맞았다(오탐)");
    }

    [UnityTest]
    public IEnumerator SpinAttack_HealsPlayerByLifestealRatio_OfTotalDamageDealt()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var health = go.GetComponent<PlayerHealth>();
        yield return null;

        // 풀피에서는 회복량이 클램프에 가려 보이지 않으니 먼저 깎아 여지를 만든다.
        health.TakeDamage(50f, null);
        yield return null;
        float hpBeforeSpin = health.CurrentHp;

        var enemy = NewEnemy(new Vector3(2.6f, 0.5f, 0f));
        ClearSpawnProtection(enemy);
        yield return null;

        InvokeSpinAttack(blade, 1f);
        yield return null;

        var enemyHp = enemy.GetComponent<EnemyHealth>();
        float dealt = enemyHp.MaxHp - enemyHp.CurrentHp;
        Assert.Greater(dealt, 0f, "흡혈 검증 전에 피해가 먼저 들어가야 한다");

        // 원본 bladeLifesteal: heal = round(totalDmg * 0.15) (project_test.html:2498~2508)
        float expectedHeal = Mathf.Round(dealt * 0.15f);
        Assert.AreEqual(hpBeforeSpin + expectedHeal, health.CurrentHp, 0.001f,
            "흡혈량이 원본(회전베기 피해 합계의 15%)과 다르다");
    }

    [UnityTest]
    public IEnumerator ApplyDirectionalInvuln_GrantsInvulnWhileAnyDirectionHeld()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var health = go.GetComponent<PlayerHealth>();
        yield return null;

        Assert.AreEqual(0f, health.InvulnRemaining, 0.001f, "시작부터 무적이 걸려 있으면 안 된다");

        InvokeApplyDirectionalInvuln(blade, true);

        Assert.Greater(health.InvulnRemaining, 0f,
            "방향키를 누르고 있는데 무적이 안 걸렸다(원본 bladeDirInvuln — 티어·스킬 무관 상시 적용)");
    }
}

}
