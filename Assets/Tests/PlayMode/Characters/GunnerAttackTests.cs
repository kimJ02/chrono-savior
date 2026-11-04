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
/// 메카닉 0차 기본공격 검증 — 원본 `gunnerFire()`(project_test.html:2188)의 `laserTier == 0` 분기와
/// 총알 갱신(`:3695`~`:3730`)을 그대로 옮겼는지 확인한다.
/// </summary>
public class GunnerAttackTests
{
    [SetUp]
    public void Setup() => ProfileService.Reset();

    [TearDown]
    public void Teardown()
    {
        ProfileService.Reset();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test") || go.name == "GunnerBullet")) Object.DestroyImmediate(go);
        }
    }

    static GameObject NewGunner(Vector3 pos)
    {
        var go = new GameObject("TestGunner");
        go.transform.position = pos;
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        go.AddComponent<GunnerAttack>();
        return go;
    }

    static GameObject NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        // 실제 `Enemy_Base.prefab`과 같은 반지름 0.5를 쓴다 — 0.3으로 줄이면 총구 높이(중심+0.38)가
        // 적 몸통 위로 지나가 버려서, 실제 게임에선 맞는 공격이 테스트에서만 빗나간다.
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var move = go.AddComponent<EnemyMove>();
        go.AddComponent<EnemyHealth>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        return go;
    }

    static void Fire(GunnerAttack gunner)
    {
        var m = typeof(GunnerAttack).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "Fire()가 없다");
        m.Invoke(gunner, null);
    }

    [UnityTest]
    public IEnumerator Fire_SpawnsBulletTravellingHorizontallyAtOriginalSpeed()
    {
        var go = NewGunner(new Vector3(2f, 0.36f, 0f));
        var gunner = go.GetComponent<GunnerAttack>();
        yield return null;

        Fire(gunner);
        yield return null;

        var bullet = GameObject.Find("GunnerBullet");
        Assert.IsNotNull(bullet, "총알이 안 생겼다");

        var v = bullet.GetComponent<Rigidbody2D>().linearVelocity;
        // 원본 spd 1320 ÷100 = 13.2, vy는 0(메카닉은 상하 조준이 없다)
        Assert.AreEqual(13.2f, v.x, 0.05f, "탄속이 원본(1320px/s ÷100)과 다르다");
        Assert.AreEqual(0f, v.y, 0.001f, "메카닉 총알에 수직 속도가 생겼다 — 원본은 정면으로만 나간다");
    }

    [UnityTest]
    public IEnumerator Bullet_DamagesOneEnemyThenDespawns_BecausePierceIsOne()
    {
        // 플레이어·적 둘 다 반지름 0.5 원이 지면(GroundY=0) 위에 서므로 중심이 y=0.5다.
        // 총구는 원본처럼 **발**에서 0.38 위(발 0 + 0.38 = 0.38)라 적 몸통(0~1.0) 안을 지난다
        // (실제 크기인 키 0.5 고블린은 GunnerSkillTreeTests.Bullet_HitsOniSizedEnemyOnSameGround가 본다).
        // 두 적은 서로 반지름 합(1.0)보다 멀리 떼야 물리로 밀어내지 않는다.
        var go = NewGunner(new Vector3(2f, 0.5f, 0f));
        var gunner = go.GetComponent<GunnerAttack>();
        var first = NewEnemy(new Vector3(3f, 0.5f, 0f));
        var second = NewEnemy(new Vector3(4.5f, 0.5f, 0f));
        yield return null;

        Fire(gunner);

        float t = 0f;
        while (t < 0.6f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }

        var h1 = first.GetComponent<EnemyHealth>();
        var h2 = second.GetComponent<EnemyHealth>();
        Assert.Less(h1.CurrentHp, h1.MaxHp, "첫 적이 안 맞았다");
        // 원본 pierceLeft = 1 → 한 번 맞히면 사라진다(:2204, :3723~3725)
        Assert.AreEqual(h2.MaxHp, h2.CurrentHp, 0.001f, "관통 1인데 두 번째 적까지 맞았다");
    }

    [UnityTest]
    public IEnumerator Cooldown_ScalesWithAttackSpeedUpgrade()
    {
        var go = NewGunner(new Vector3(2f, 0.36f, 0f));
        var gunner = go.GetComponent<GunnerAttack>();
        yield return null;

        float baseCd = gunner.cooldown;
        Assert.AreEqual(0.24f, baseCd, 0.001f, "기본 쿨다운이 원본(0.24)과 다르다");

        // 공격속도 강화 5단계 → statAs = 1 + 5×0.04 = 1.2
        ProfileService.Current.upgrades.atkSpeed = 5;
        yield return null;

        Assert.AreEqual(0.24f / 1.2f, gunner.cooldown, 0.001f, "공격속도 강화가 쿨다운에 반영되지 않았다");
    }

    [UnityTest]
    public IEnumerator BaseDamage_IsAtkTimesOriginalWeaponMultiplier()
    {
        var go = NewGunner(new Vector3(2f, 0.36f, 0f));
        var gunner = go.GetComponent<GunnerAttack>();
        yield return null;

        float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
        // 원본 dmg: laserTier 0이면 0.82(:2203)
        Assert.AreEqual(atk * 0.82f, gunner.baseDamage, 0.001f, "기본 피해량이 statAtk × 0.82가 아니다");
    }

    [UnityTest]
    public IEnumerator GunnerKit_DeclaresInstantMoveMode()
    {
        var go = NewGunner(Vector3.zero);
        yield return null;

        var kit = (ICharacterKit)go.GetComponent<GunnerAttack>();
        Assert.AreEqual(CharacterId.Gunner, kit.Character);
        Assert.AreEqual(CharacterMover2D.MoveMode.Instant, kit.RequiredMoveMode,
            "메카닉은 마법사와 같은 즉시-속도 이동이다(관성은 섬영 전용)");
    }
}
}
