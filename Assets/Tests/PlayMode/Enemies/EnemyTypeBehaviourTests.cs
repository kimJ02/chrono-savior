using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 단계 1 "몬스터 6종" — 종류별 행동이 원본과 같은지 검증한다.
///
/// 이동 계산이 전부 `GetHorizontalSpeed(dt, target, baseSpeed)` / `GetVerticalPosition(...)`처럼
/// **dt를 인자로 받는 순수 함수**라, 실제로 프레임을 흘려보내지 않고 원하는 시점을 바로 찍을 수 있다.
/// 상태 전이(예고 0.5초 → 질주 0.7초)를 실시간으로 기다리면 테스트가 느려지고 프레임 타이밍에
/// 흔들리기 때문에 이 구조를 택했다.
/// </summary>
public class EnemyTypeBehaviourTests
{
    [TearDown]
    public void Cleanup()
    {
        EnemySpawnRequestBus.Reset();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            if (go.name.StartsWith("Test") || go.name == "EnemyBolt") Object.DestroyImmediate(go);
        }
    }

    /// <summary>몹 본체(EnemyMove + 콜라이더). 스폰 무적은 꺼둔다 — 안 그러면 이동이 통째로 멈춘다.</summary>
    static GameObject NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var move = go.AddComponent<EnemyMove>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
                         .SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        return go;
    }

    static GameObject NewTarget(Vector3 pos)
    {
        var go = new GameObject("TestTarget");
        go.transform.position = pos;
        return go;
    }

    // ────────────────────────── 지역별 해금표 (원본 rollSpawnType :3933) ──────────────────────────

    /// <summary>
    /// 1지역은 박쥐 45% / 고블린 55% 딱 두 종뿐이다. 경계값(0.45)이 고블린 쪽에 붙는지까지 본다 —
    /// 부등호를 헷갈리면 확률이 조용히 밀린다.
    /// </summary>
    [Test]
    public void SpawnTable_Region1_OnlyWispAndOni()
    {
        Assert.AreEqual(EnemyType.Flyer, EnemySpawnTable.RollBase(1, 0f));
        Assert.AreEqual(EnemyType.Flyer, EnemySpawnTable.RollBase(1, 0.449f));
        Assert.AreEqual(EnemyType.Grunt, EnemySpawnTable.RollBase(1, 0.45f));
        Assert.AreEqual(EnemyType.Grunt, EnemySpawnTable.RollBase(1, 0.999f));
    }

    /// <summary>지역이 오를수록 강한 종이 "추가"된다 — 2지역 멧돼지, 3지역 해골 궁수, 4지역 슬라임.</summary>
    [Test]
    public void SpawnTable_UnlocksStrongerTypesByRegion()
    {
        Assert.AreEqual(EnemyType.Charger, EnemySpawnTable.RollBase(2, 0.99f));
        Assert.AreEqual(EnemyType.Shooter, EnemySpawnTable.RollBase(3, 0.99f));
        Assert.AreEqual(EnemyType.Splitter, EnemySpawnTable.RollBase(4, 0.99f));
        // 3지역에서 아직 슬라임은 안 나온다(해골 궁수가 상한).
        Assert.AreEqual(EnemyType.Shooter, EnemySpawnTable.RollBase(3, 0.85f));
    }

    /// <summary>
    /// 꼬마 슬라임은 **웨이브로 절대 안 나온다** — 슬라임이 죽을 때만 생긴다(원본 :1842). 표에 섞여 들어가면
    /// 체력 14짜리가 그냥 굴러다니는 전혀 다른 게임이 된다.
    /// </summary>
    [Test]
    public void SpawnTable_NeverRollsSplitlet()
    {
        for (int region = 1; region <= 9; region++)
            for (int i = 0; i <= 100; i++)
                Assert.AreNotEqual(EnemyType.Splitlet, EnemySpawnTable.RollBase(region, i / 100f),
                    "region " + region + ", roll " + (i / 100f));
    }

    /// <summary>오우거는 2지역부터만 나온다(원본 `run.region >= 2` :3944).</summary>
    [Test]
    public void SpawnTable_BigOni_NeverInRegion1()
    {
        for (int i = 0; i < 400; i++)
            Assert.AreNotEqual(EnemyType.Brute, EnemySpawnTable.Roll(1));
    }

    // ────────────────────────── 멧돼지 (원본 :4061) ──────────────────────────

    /// <summary>
    /// 접근 → 예고(정지) → 질주 → 지침(정지) 순환. **예고와 지침 동안 멈추는 게 이 몹의 핵심**이라
    /// (피할 틈을 주는 구간) 속도가 정확히 0인지 본다.
    /// </summary>
    [Test]
    public void Charger_TelegraphThenChargeThenTired()
    {
        // 맵 좌우 끝(0.6유닛 안쪽)에서는 원본이 질주를 즉시 끝내므로(`e.x < 60`, :4078) 필드 한가운데에 둔다.
        var enemy = NewEnemy(new Vector3(10f, 0f, 0f));
        var charger = enemy.AddComponent<EnemyChargerMotion>();
        var target = NewTarget(new Vector3(12f, 0f, 0f)); // aggroX 3 안, aggroY 0.9 안

        // 스폰 직후 쿨다운(rand 0.5~1.5)을 먼저 태운다 — 대상 없이 부르면 돌진 조건을 안 본다.
        charger.GetHorizontalSpeed(1.6f, null, 1f);

        // 예고에 "들어가는" 프레임은 원본에서 아직 걷는다(속도를 정한 뒤에 상태를 바꾸기 때문).
        Assert.AreEqual(1f, Mathf.Abs(charger.GetHorizontalSpeed(0.016f, target.transform, 1f)), 1e-4f,
            "예고 진입 프레임은 아직 보행 속도여야 한다(원본 :4067~4071 순서)");
        Assert.IsFalse(charger.IsCharging);

        // 그 다음 프레임부터가 진짜 예고 — 완전 정지.
        Assert.AreEqual(0f, charger.GetHorizontalSpeed(0.016f, target.transform, 1f), 1e-4f);

        // 예고 0.5초가 끝나는 프레임은 아직 0이고(원본 tele 분기는 mvx=0 뒤에 전이), 그 다음부터 질주.
        Assert.AreEqual(0f, charger.GetHorizontalSpeed(0.5f, target.transform, 1f), 1e-4f);
        Assert.IsTrue(charger.IsCharging);

        // 질주 속도는 이동속도가 아니라 chargeSpeed(5)다.
        float v = charger.GetHorizontalSpeed(0.016f, target.transform, 1f);
        Assert.AreEqual(charger.chargeSpeed, Mathf.Abs(v), 1e-4f);
        Assert.AreEqual(1, Mathf.RoundToInt(Mathf.Sign(v)), "오른쪽 대상으로 돌진해야 한다");

        // 질주 0.7초가 끝나는 프레임도 원본에선 마지막으로 한 번 더 질주한다 — 여기서 0이 되면
        // 원본은 끝까지 밀고 들어오는데 우리 쪽만 마지막에 멈칫하게 된다.
        Assert.AreEqual(charger.chargeSpeed, Mathf.Abs(charger.GetHorizontalSpeed(0.7f, target.transform, 1f)), 1e-4f);
        Assert.IsFalse(charger.IsCharging, "질주 시간이 끝났으면 상태는 이미 지침이다");

        // 그 다음 프레임부터 지침 — 완전 정지.
        Assert.AreEqual(0f, charger.GetHorizontalSpeed(0.016f, target.transform, 1f), 1e-4f);
    }

    /// <summary>
    /// 원본 `chargeMul`(:4147) — 접촉 피해 ×1.4는 **질주 중일 때만**이다. 상시 1.4면 서 있는
    /// 멧돼지도 아프게 때리는 다른 몹이 된다.
    /// </summary>
    [Test]
    public void Charger_ContactDamageBoostOnlyWhileCharging()
    {
        var enemy = NewEnemy(new Vector3(10f, 0f, 0f));
        var charger = enemy.AddComponent<EnemyChargerMotion>();
        var target = NewTarget(new Vector3(12f, 0f, 0f));

        Assert.AreEqual(1f, charger.ContactDamageMultiplier, 1e-4f, "걷는 중엔 배수가 없다");

        charger.GetHorizontalSpeed(1.6f, null, 1f);
        charger.GetHorizontalSpeed(0.016f, target.transform, 1f); // 예고 진입
        charger.GetHorizontalSpeed(0.5f, target.transform, 1f);   // 예고 종료 → 질주
        Assert.AreEqual(1.4f, charger.ContactDamageMultiplier, 1e-4f);
    }

    /// <summary>Y가 0.9유닛 넘게 차이나면(다른 층) 돌진하지 않는다 — 원본 aggroY.</summary>
    [Test]
    public void Charger_DoesNotChargeAcrossFloors()
    {
        var enemy = NewEnemy(new Vector3(10f, 0f, 0f));
        var charger = enemy.AddComponent<EnemyChargerMotion>();
        var target = NewTarget(new Vector3(11f, 2.5f, 0f)); // X는 가깝지만 Y가 멀다

        charger.GetHorizontalSpeed(1.6f, null, 1f);
        for (int i = 0; i < 60; i++)
        {
            charger.GetHorizontalSpeed(0.05f, target.transform, 1f);
            Assert.IsFalse(charger.IsCharging, "다른 층의 플레이어에겐 돌진하면 안 된다");
        }
    }

    /// <summary>
    /// 맵 좌우 끝에 닿으면 시간이 남아도 질주가 끝난다(원본 `e.x &lt; 60 || e.x &gt; mapW - 60`, :4078).
    /// 없으면 멧돼지가 벽에 처박힌 채 질주 판정(접촉 피해 ×1.4)을 계속 유지한다.
    /// </summary>
    [Test]
    public void Charger_EndsChargeAtFieldEdge()
    {
        var enemy = NewEnemy(new Vector3(FieldBounds.MinX + 0.3f, 0f, 0f)); // 이미 끝자락
        var charger = enemy.AddComponent<EnemyChargerMotion>();
        var target = NewTarget(new Vector3(FieldBounds.MinX + 1.5f, 0f, 0f));

        charger.GetHorizontalSpeed(1.6f, null, 1f);
        charger.GetHorizontalSpeed(0.016f, target.transform, 1f); // 예고 진입
        charger.GetHorizontalSpeed(0.5f, target.transform, 1f);   // 질주 진입
        Assert.IsTrue(charger.IsCharging);

        // 질주 시간(0.7)이 한참 남았는데도 끝자락이라 바로 지침으로 넘어간다.
        charger.GetHorizontalSpeed(0.016f, target.transform, 1f);
        Assert.IsFalse(charger.IsCharging, "맵 끝에서는 질주 시간이 남아도 끝나야 한다");
    }

    // ────────────────────────── 해골 궁수 (원본 :4084) ──────────────────────────

    /// <summary>너무 가까우면 물러나고, 너무 멀면 다가가고, 사거리(3~5) 안이면 멈춘다.</summary>
    [Test]
    public void Shooter_KeepsDistanceBand()
    {
        var enemy = NewEnemy(Vector3.zero);
        var shooter = enemy.AddComponent<EnemyShooterMotion>();
        var target = NewTarget(Vector3.zero);

        target.transform.position = new Vector3(1f, 0f, 0f);  // keepMin(3)보다 가깝다
        Assert.Less(shooter.GetHorizontalSpeed(0.016f, target.transform, 0.55f), 0f, "물러나야 한다");

        target.transform.position = new Vector3(8f, 0f, 0f);  // keepMax(5)보다 멀다
        Assert.Greater(shooter.GetHorizontalSpeed(0.016f, target.transform, 0.55f), 0f, "다가가야 한다");

        target.transform.position = new Vector3(4f, 0f, 0f);  // 사거리 안
        Assert.AreEqual(0f, shooter.GetHorizontalSpeed(0.016f, target.transform, 0.55f), 1e-4f, "멈춰서 쏜다");
    }

    /// <summary>
    /// 사격은 **사거리 안에 멈춰 있을 때만** 일어난다. 이동 중에도 쏘면 원본보다 훨씬 강해진다.
    /// </summary>
    [Test]
    public void Shooter_FiresOnlyWhileHoldingRange()
    {
        var enemy = NewEnemy(Vector3.zero);
        var shooter = enemy.AddComponent<EnemyShooterMotion>();
        var target = NewTarget(new Vector3(1f, 0f, 0f)); // 너무 가까워서 물러나는 중

        for (int i = 0; i < 40; i++) shooter.GetHorizontalSpeed(0.1f, target.transform, 0.55f);
        Assert.AreEqual(0, CountBolts(), "이동 중엔 쏘지 않는다");

        target.transform.position = new Vector3(4f, 0f, 0f); // 사거리 안으로
        shooter.GetHorizontalSpeed(0.1f, target.transform, 0.55f);
        Assert.AreEqual(1, CountBolts(), "사거리 안에서 쿨다운이 다 찼으면 한 발 나간다");
    }

    static int CountBolts() => Object.FindObjectsByType<EnemyBolt>(FindObjectsSortMode.None).Length;

    // ────────────────────────── 박쥐 (원본 :4042) ──────────────────────────

    /// <summary>
    /// 비행형은 중력을 안 받는다 — 박쥐 프리팹(Enemy_Flyer)처럼 **행동 부품이 처음부터 같이 있으면** `EnemyMove.Awake`가 알아보고 중력을 끈다
    /// (2026-11-05 생성 책임 2-2 — 예전엔 스포너가 나중에 붙이고 `RefreshTypeBehaviour()`로 다시 찾게 했다). 프리팹을 세우는 것처럼 꺼 둔 채 부품을 다 붙이고 켠다.
    /// </summary>
    [Test]
    public void Flyer_WithItsBehaviourFromTheStart_HasNoGravity()
    {
        var go = new GameObject("TestFlyer");
        go.SetActive(false);
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        go.AddComponent<EnemyMove>();
        go.AddComponent<EnemyFlyerMotion>();
        var rb = go.GetComponent<Rigidbody2D>();
        rb.gravityScale = 1f;
        go.SetActive(true); // 프리팹 Instantiate와 같다 — 부품이 다 있는 상태에서 Awake
        Assert.AreEqual(0f, rb.gravityScale, 1e-4f);
        Object.DestroyImmediate(go);
    }

    /// <summary>감지 범위(3.5) 밖이면 쫓아오지 않고 제자리에서 흔들린다 — 진폭 0.22를 넘지 않는다.</summary>
    [Test]
    public void Wisp_DoesNotChaseOutsideDetectRange()
    {
        var enemy = NewEnemy(new Vector3(0f, 2f, 0f));
        var wisp = enemy.AddComponent<EnemyFlyerMotion>();
        var target = NewTarget(new Vector3(10f, 2f, 0f));

        float vx = wisp.GetHorizontalSpeed(0.016f, target.transform, 0.66f);
        Assert.LessOrEqual(Mathf.Abs(vx), wisp.idleSwayX + 1e-4f, "추적 속도(0.66)로 붙으면 안 된다");
    }

    /// <summary>
    /// **Y 추적은 거리와 무관하게 일정 속도(0.46)** — 원본 주석이 콕 집어 말하는 부분이다.
    /// 거리에 비례하게 만들면 멀리 있을 때 순간이동하듯 내려꽂힌다.
    /// </summary>
    [Test]
    public void Wisp_VerticalSpeedIsConstantRegardlessOfDistance()
    {
        var enemy = NewEnemy(new Vector3(0f, 1f, 0f));
        var wisp = enemy.AddComponent<EnemyFlyerMotion>();
        const float dt = 0.1f;
        float expected = wisp.verticalSpeed * dt;

        var near = NewTarget(new Vector3(0.5f, 2.5f, 0f)); // 위로 조금
        float dNear = wisp.GetVerticalPosition(dt, near.transform, 1f) - 1f;

        var far = NewTarget(new Vector3(0.5f, 4.5f, 0f));  // 위로 한참
        float dFar = wisp.GetVerticalPosition(dt, far.transform, 1f) - 1f;

        Assert.AreEqual(expected, dNear, 1e-4f);
        Assert.AreEqual(expected, dFar, 1e-4f);
    }

    // ────────────────────────── 슬라임 (원본 :1840) ──────────────────────────

    /// <summary>
    /// 죽으면 꼬마 슬라임 2마리를 **좌우로** 요청한다. 스포너를 직접 부르지 않고 버스에 요청만 던지는
    /// 구조라(asmdef 순환참조 회피) 여기선 버스에 들어온 요청을 본다.
    /// </summary>
    [UnityTest]
    public IEnumerator Splitter_RequestsTwoSplitletsOnDeath()
    {
        var requests = new List<KeyValuePair<Vector2, EnemyType>>();
        EnemySpawnRequestBus.Requested += (p, t, _) => requests.Add(new KeyValuePair<Vector2, EnemyType>(p, t));

        var enemy = NewEnemy(new Vector3(5f, 0.5f, 0f));
        enemy.AddComponent<EnemyHealth>();
        enemy.AddComponent<EnemySplitOnDeath>();

        var attacker = new GameObject("TestAttacker");
        enemy.GetComponent<EnemyHealth>().TakeDamage(9999f, attacker);
        yield return null;

        Assert.AreEqual(2, requests.Count);
        Assert.AreEqual(EnemyType.Splitlet, requests[0].Value);
        Assert.AreEqual(EnemyType.Splitlet, requests[1].Value);
        Assert.AreEqual(4.76f, requests[0].Key.x, 1e-3f, "왼쪽 꼬마 슬라임은 -0.24");
        Assert.AreEqual(5.24f, requests[1].Key.x, 1e-3f, "오른쪽 꼬마 슬라임은 +0.24");
    }

    /// <summary>꼬마 슬라임은 지면 아래에서 나오지 않는다 — 원본 `Math.min(e.y, groundY)`.</summary>
    [UnityTest]
    public IEnumerator Splitter_ClampsChildSpawnToGround()
    {
        var requests = new List<Vector2>();
        EnemySpawnRequestBus.Requested += (p, _, __) => requests.Add(p);

        var enemy = NewEnemy(new Vector3(5f, FieldBounds.GroundY - 3f, 0f)); // 지면보다 한참 아래
        enemy.AddComponent<EnemyHealth>();
        enemy.AddComponent<EnemySplitOnDeath>();
        enemy.GetComponent<EnemyHealth>().TakeDamage(9999f, new GameObject("TestAttacker"));
        yield return null;

        Assert.AreEqual(2, requests.Count);
        foreach (var p in requests)
            Assert.GreaterOrEqual(p.y, FieldBounds.GroundY - 1e-4f);
    }
}

}
