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
/// 원본 좌표의 높이 기준점(발)을 Unity(몸 중심)로 옮길 때의 회귀 검사 — <see cref="PlayerBody"/>.
///
/// 원본 `p.y - N`의 p.y는 발인데 몸 중심에서 재 버리면, 캐릭터 키가 몬스터와 같아진 지금(0.5) 공격이
/// 고블린(키 0.5) 머리 위로 지나간다. 캐릭터 크기를 줄이면서(2026-09-28) 드러났다.
/// 메카닉 총알은 <c>GunnerSkillTreeTests.Bullet_HitsOniSizedEnemyOnSameGround</c>가 본다.
/// </summary>
public class PlayerBodyAnchorTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test") || go.name == "MageBolt")) Object.DestroyImmediate(go);
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        ProfileService.Reset();
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunTransient.Reset();
    }

    [Test]
    public void Feet_IsBodyCenterMinusWorldRadius()
    {
        var go = new GameObject("TestBody");
        go.transform.position = new Vector3(3f, 2f, 0f);
        go.transform.localScale = new Vector3(2f, 2f, 1f);
        var col = go.AddComponent<CircleCollider2D>();
        col.radius = 0.25f; // 월드 반지름 0.5

        var feet = PlayerBody.Feet(go.transform, col);
        Assert.AreEqual(3f, feet.x, 1e-4f);
        Assert.AreEqual(1.5f, feet.y, 1e-4f, "발은 몸 중심에서 월드 반지름만큼 아래여야 한다");
    }

    [UnityTest]
    public IEnumerator MageBolt_HitsOniSizedEnemyOnSameGround()
    {
        // 지금 플레이어 크기(키 0.5)로 바닥(y=0)에 세운다.
        var go = new GameObject("TestMage");
        go.transform.position = new Vector3(5f, EntitySizeConfig.PlayerRadius, 0f);
        go.AddComponent<CircleCollider2D>().radius = EntitySizeConfig.PlayerRadius;
        var rb = go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterMover2D>();
        rb.gravityScale = 0f; // 이동기가 Awake에서 1로 되돌리므로 그 뒤에 다시 고정
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        var mage = go.AddComponent<MageAttack>();

        var oni = new GameObject("TestEnemy");
        oni.tag = "Enemy";
        oni.transform.position = new Vector3(6.5f, EntitySizeConfig.OniRadius, 0f);
        oni.AddComponent<SpriteRenderer>();
        oni.AddComponent<CircleCollider2D>().radius = EntitySizeConfig.OniRadius;
        var move = oni.AddComponent<EnemyMove>();
        var health = oni.AddComponent<EnemyHealth>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(move, 0f);
        var erb = oni.GetComponent<Rigidbody2D>();
        erb.gravityScale = 0f;
        erb.constraints = RigidbodyConstraints2D.FreezeAll;
        move.enabled = false;
        health.SetMaxHp(100000f);
        yield return new WaitForFixedUpdate();

        var fire = typeof(MageAttack).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(fire, "Fire()가 없다");
        fire.Invoke(mage, new object[] { 0f, MageBranch.None, 0 }); // 무차지 정면 발사

        float t = 0f;
        while (t < 0.5f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }

        Assert.Less(health.CurrentHp, health.MaxHp,
            "바닥의 고블린 크기 적을 정면 마법탄이 못 맞혔다 — 발사 높이를 발에서 재는지 확인(원본 p.y - 36)");
    }
}
}
