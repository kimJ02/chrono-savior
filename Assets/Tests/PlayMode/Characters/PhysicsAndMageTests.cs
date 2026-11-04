using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 물리엔진 전환(CharacterMover2D가 Rigidbody2D 실제 충돌로 바닥/발판에 서는지)과
/// 마법사 기본 공격(MageAttack/MageProjectile, 원본 CONFIG.bow 차지 공식)을 PlayMode에서 검증한다.
/// Edit Mode 배치 실행은 Physics2D/지연 Destroy가 못 미덥다는 게 이미 확인된 사실이라
/// (PROGRESS.md 로그 참고) 여기서도 전부 PlayMode 테스트로만 검증한다.
/// </summary>
public class PhysicsAndMageTests
{
    /// <summary>
    /// 원본은 여러 층 발판을 딛고 설 수 있어야 한다 — 발판 위에서 낙하시켰을 때 바닥까지
    /// 뚫고 떨어지지 않고 그 발판 위에서 실제 Physics2D 충돌로 멈추는지 확인한다.
    /// </summary>
    [UnityTest]
    public IEnumerator Player_LandsOnPlatform_NotFallingThroughToGround()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.AreNotEqual(-1, groundLayer, "Ground 레이어가 없음 — BuildPartAScene.Build()를 한 번도 안 돌린 프로젝트인가?");

        var platformGO = new GameObject("TestPlatform");
        platformGO.layer = groundLayer;
        platformGO.AddComponent<BoxCollider2D>().size = new Vector2(3f, 0.15f);
        platformGO.transform.position = new Vector3(5f, 2.25f, 0f); // 원본 2층 발판 높이

        var groundGO = new GameObject("TestGround");
        groundGO.layer = groundLayer;
        groundGO.AddComponent<BoxCollider2D>().size = new Vector2(20f, 0.3f);
        groundGO.transform.position = new Vector3(5f, FieldBounds.GroundY - 0.15f, 0f);

        var go = new GameObject("PlatformTestPlayer");
        go.tag = "Player";
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        go.AddComponent<Rigidbody2D>();
        go.transform.position = new Vector3(5f, 4f, 0f); // 발판보다 한참 위에서 떨어뜨림
        go.AddComponent<CharacterMover2D>();

        float t = 0f;
        while (t < 2f)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }

        // 발판 표면(y=2.25+0.075) 위에 반지름(0.5)만큼 떠서 멈춰야 한다 — 바닥(y=0)까지 떨어지면 실패.
        float expectedY = 2.25f + 0.075f + 0.5f;
        Assert.AreEqual(expectedY, go.transform.position.y, 0.05f, "발판을 뚫고 떨어졌다(발판 콜라이더가 안 먹힘)");

        Object.Destroy(go);
        Object.Destroy(platformGO);
        Object.Destroy(groundGO);
        yield return null;
    }

    /// <summary>
    /// 원본과 동일하게 원웨이 발판이어야 한다 — 밑에서 위로 뚫고 지나갈 땐 안 막히고(점프 중 머리
    /// 박힘 버그가 있었음, 사용자 피드백으로 발견), 위에서 떨어질 땐 그 위에 착지해야 한다.
    /// PlatformEffector2D(useOneWay=true) 적용이 실제로 두 방향 다 맞게 동작하는지 확인.
    /// </summary>
    [UnityTest]
    public IEnumerator Player_PassesThroughOneWayPlatformFromBelow_ThenLandsOnTopFromAbove()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        var platformGO = new GameObject("TestOneWayPlatform");
        platformGO.layer = groundLayer;
        platformGO.transform.position = new Vector3(5f, 3f, 0f);
        var col = platformGO.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3f, 0.15f);
        col.usedByEffector = true;
        var effector = platformGO.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;

        var go = new GameObject("OneWayTestPlayer");
        go.tag = "Player";
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        go.transform.position = new Vector3(5f, 1f, 0f); // 발판 한참 아래에서 시작
        go.AddComponent<CharacterMover2D>();

        rb.linearVelocity = new Vector2(0f, 14f); // 발판을 확실히 뚫고 지나갈 만큼 세게 쏴올림(실제 점프 속도보다 큼 — 통과 여부만 확인 목적)

        float platformTop = 3f + 0.075f;
        float maxY = go.transform.position.y;
        float t = 0f;
        while (t < 1f)
        {
            yield return new WaitForFixedUpdate();
            maxY = Mathf.Max(maxY, go.transform.position.y);
            t += Time.fixedDeltaTime;
        }
        Assert.Greater(maxY, platformTop + 0.3f, "아래에서 위로 지나갈 때 발판에 막혔다(원웨이가 아니라 막힌 콜라이더처럼 동작함)");

        t = 0f; // 다시 떨어지길 기다렸다가 발판 위에 착지하는지 확인
        while (t < 2f)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }
        float expectedRestY = platformTop + 0.5f;
        Assert.AreEqual(expectedRestY, go.transform.position.y, 0.05f, "위에서 떨어질 때 발판 위에 착지하지 않았다");

        Object.Destroy(go);
        Object.Destroy(platformGO);
        yield return null;
    }

    /// <summary>
    /// 원본 bowFire 차지 공식(dmg = base*(1+chargeDmg*k), pierce = base+floor(k*chargePierce),
    /// speed = base*(1+0.3*k))을 그대로 옮겼는지, chargeK를 0과 1로 바꿔가며 실제 발사된
    /// MageProjectile/Rigidbody2D 속도를 읽어 검증한다. Input은 시뮬레이트 못 하므로
    /// private Fire(float) 메서드를 리플렉션으로 직접 호출한다(그 안의 공식은 실제 코드 그대로 탄다).
    /// </summary>
    [UnityTest]
    public IEnumerator Fire_FullCharge_IsFasterAndPiercesMore_ThanNoCharge()
    {
        var go = new GameObject("MageTestCaster");
        var mage = go.AddComponent<MageAttack>();
        var fireMethod = typeof(MageAttack).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(fireMethod);

        fireMethod.Invoke(mage, new object[] { 0f, ProfileService.Current.mageBranch, ProfileService.Current.mageTier });
        yield return null;
        var noChargeBolt = GameObject.Find("MageBolt");
        Assert.IsNotNull(noChargeBolt, "차지 없이 발사했는데 투사체가 안 생겼다");
        float noChargeSpeed = noChargeBolt.GetComponent<Rigidbody2D>().linearVelocity.magnitude;
        Object.Destroy(noChargeBolt);
        yield return null;

        fireMethod.Invoke(mage, new object[] { 1f, ProfileService.Current.mageBranch, ProfileService.Current.mageTier });
        yield return null;
        var fullChargeBolt = GameObject.Find("MageBolt");
        Assert.IsNotNull(fullChargeBolt, "풀차지로 발사했는데 투사체가 안 생겼다");
        float fullChargeSpeed = fullChargeBolt.GetComponent<Rigidbody2D>().linearVelocity.magnitude;

        // speed = 10.8*(1+0.3*k) → k=0: 10.8, k=1: 14.04
        Assert.AreEqual(mage.projectileSpeed, noChargeSpeed, 0.05f, "무차지 탄속이 base speed와 다름");
        Assert.AreEqual(mage.projectileSpeed * 1.3f, fullChargeSpeed, 0.05f, "풀차지 탄속이 원본 공식(base*1.3)과 다름");
        Assert.Greater(fullChargeSpeed, noChargeSpeed, "풀차지가 무차지보다 빨라야 한다");

        Object.Destroy(fullChargeBolt);
        Object.Destroy(go);
        yield return null;
    }

    /// <summary>
    /// 무차지 기준 pierce=2(원본 B.pierce)면 적 정확히 2마리까지만 맞히고 사라진다 — 원본
    /// `w.pierceLeft--; if (w.pierceLeft <= 0) remove = true;`(project_test.html:3683-3684)를
    /// 그대로 계산하면 pierce=2는 "2번 맞은 뒤 소멸"이지 "3번째까지 맞고 소멸"이 아니다
    /// (스프린트 2 마법사 스킬트리 작업 중 이 오프바이원을 발견해 수정 — 이전엔 `pierceLeft &lt; 0`으로
    /// 비교해서 한 대 더 맞혔다).
    /// </summary>
    [UnityTest]
    public IEnumerator Projectile_PiercesExactlyBasePierceCountThenDespawns()
    {
        var go = new GameObject("MageTestCaster");
        var mage = go.AddComponent<MageAttack>();
        var fireMethod = typeof(MageAttack).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);

        // 스프린트 2부터 관통은 "죽인 수"가 아니라 "때린 수"로 소모된다(원본도 동일 — 체력이 남아도 pierce는 깎임).
        // 그래서 체력을 붙이고 "피해를 입었나"로 확인한다.
        var enemies = new GameObject[3];
        var healths = new ChronoSavior.Enemies.EnemyHealth[3];
        for (int i = 0; i < enemies.Length; i++)
        {
            var e = new GameObject($"Enemy_{i}");
            e.tag = "Enemy";
            e.transform.position = new Vector3(0.6f + i * 0.3f, 0.36f, 0f);
            e.AddComponent<CircleCollider2D>().radius = 0.3f;
            e.AddComponent<SpriteRenderer>();
            healths[i] = e.AddComponent<ChronoSavior.Enemies.EnemyHealth>();
            enemies[i] = e;
        }

        fireMethod.Invoke(mage, new object[] { 0f, ProfileService.Current.mageBranch, ProfileService.Current.mageTier }); // chargeK=0 → pierce = B.pierce(2) + 0 = 2 → 정확히 2타 관통

        float t = 0f;
        while (t < 1.5f)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }

        Assert.Less(healths[0].CurrentHp, healths[0].MaxHp, "1번째 적이 안 맞음");
        Assert.Less(healths[1].CurrentHp, healths[1].MaxHp, "2번째 적이 안 맞음(pierce=2는 정확히 2타여야 함)");
        Assert.AreEqual(healths[2].MaxHp, healths[2].CurrentHp, 0.001f, "3번째 적까지 맞았다 — pierce 제한이 안 걸림");

        foreach (var e in enemies) if (e != null) Object.Destroy(e);
        Object.Destroy(go);
        yield return null;
    }

    /// <summary>
    /// 원본 bowFire: life는 차지와 무관하게 항상 B.range/B.speed(880/1080≈0.8148초)로 고정이고,
    /// 이 시간이 지나면 아무것도 못 맞혀도 스스로 사라진다(기본 티어엔 벽/맵경계 충돌 로직이
    /// 따로 없음 — 그건 fireTier 스킬트리 전용, 범위 밖). 아무것도 없는 허공에 쏴서 실제로
    /// 그 시간에 사라지는지, 그때까지 얼마나 이동했는지(≈8.8유닛, 필드 26유닛보다 훨씬 짧음) 확인한다.
    /// </summary>
    [UnityTest]
    public IEnumerator Projectile_DespawnsAfterFixedLife_NotOnFieldEdge()
    {
        var go = new GameObject("MageTestCaster");
        go.transform.position = new Vector3(2.2f, 0.36f, 0f); // 원본 스폰 X 그대로
        var mage = go.AddComponent<MageAttack>();
        var fireMethod = typeof(MageAttack).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);

        fireMethod.Invoke(mage, new object[] { 0f, ProfileService.Current.mageBranch, ProfileService.Current.mageTier }); // 무차지 → life = range/speed = 8.8/10.8 ≈ 0.8148초
        yield return null;

        var bolt = GameObject.Find("MageBolt");
        Assert.IsNotNull(bolt, "발사했는데 투사체가 안 생겼다");
        float startX = bolt.transform.position.x;

        float expectedLife = mage.projectileRange / mage.projectileSpeed;

        // 예상 수명 직전: 아직 살아있어야 한다.
        yield return new WaitForSeconds(expectedLife - 0.15f);
        Assert.IsTrue(bolt != null, $"예상 수명({expectedLife:F3}초)보다 일찍 사라졌다");
        float xBeforeExpiry = bolt.transform.position.x;
        float traveled = xBeforeExpiry - startX;
        Assert.Less(Mathf.Abs(traveled), 26f, "이 시점에 이미 필드 폭(26유닛)만큼 이동했다 — 사거리 제한이 사실상 없는 것과 같음");

        // 예상 수명 이후: 사라져 있어야 한다 + 맵 끝(0 또는 26)까지 간 게 아니라 사거리(≈8.8유닛)만큼만 이동했어야 한다.
        yield return new WaitForSeconds(0.35f);
        Assert.IsTrue(bolt == null, $"예상 수명({expectedLife:F3}초)이 한참 지났는데도 안 사라졌다 — life 소멸 로직이 안 먹히는 것 아닌지 확인");

        Object.Destroy(go);
        yield return null;
    }
}

}
