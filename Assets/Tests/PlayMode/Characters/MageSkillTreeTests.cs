using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 마법사 스킬트리(전문화) — 폭발/중력 두 갈래 전부(project_test.html:897-914, 1932-2179)를
/// 검증한다. `ProfileService.Current`와 `GravityWellZone.Active`가 둘 다 정적 상태라 테스트끼리
/// 오염되지 않게 매 테스트 전후로 리셋한다(이 프로젝트에서 이미 겪은 함정과 같은 종류).
/// </summary>
public class MageSkillTreeTests
{
    [SetUp]
    public void Reset()
    {
        ProfileService.Reset();
        GravityWellZone.ClearAllForTests();
        // 콤보가 남으면 피해 배수가 올라가 기대 피해량이 어긋난다(정적 상태 오염).
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
    }

    [TearDown]
    public void Cleanup()
    {
        ProfileService.Reset();
        GravityWellZone.ClearAllForTests();
        // 콤보가 남으면 피해 배수가 올라가 기대 피해량이 어긋난다(정적 상태 오염).
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            if (go.name.StartsWith("Test") || go.name == "MageBolt" || go.name == "FireTrailZone" || go.name == "GravityWellZone")
                Object.DestroyImmediate(go);
        }
    }

    static GameObject NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = go.AddComponent<EnemyMove>();
        go.AddComponent<EnemyHealth>();

        var timerField = typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        return go;
    }

    // ---- 티어 습득(SP 경제, project_test.html:1258-1259, 7011) ----

    [Test]
    public void TryLearnMageTier_LocksBranchAndRequiresSequentialOrder()
    {
        var p = new PlayerProfile { level = 20 }; // SP 넉넉히
        Assert.IsTrue(p.TryLearnMageTier(MageBranch.Explosion), "1티어(SP1)를 못 배웠다");
        Assert.AreEqual(MageBranch.Explosion, p.mageBranch);
        Assert.AreEqual(1, p.mageTier);

        Assert.IsFalse(p.TryLearnMageTier(MageBranch.Gravity), "이미 폭발 갈래를 골랐는데 중력 갈래도 배워짐 — 갈래는 고정이어야 한다");
        Assert.AreEqual(1, p.mageTier, "다른 갈래 시도가 실패해도 되는데 티어가 바뀜");

        Assert.IsTrue(p.TryLearnMageTier(MageBranch.Explosion));
        Assert.AreEqual(2, p.mageTier);
        Assert.AreEqual(1 + 2, p.spUsed, "1+2티어 비용 합이 안 맞음");
    }

    [Test]
    public void TryLearnMageTier_FailsWithoutEnoughSp()
    {
        var p = new PlayerProfile { level = 2 }; // SP = 1
        Assert.IsTrue(p.TryLearnMageTier(MageBranch.Gravity), "SP1로 1티어(비용1)를 못 배웠다");
        Assert.IsFalse(p.TryLearnMageTier(MageBranch.Gravity), "SP가 부족한데 2티어(비용2)가 배워졌다");
        Assert.AreEqual(1, p.mageTier);
    }

    // ---- 화염 폭발 (MageSkillEffects.SpawnFireExplosion, project_test.html:1978) ----

    [UnityTest]
    public IEnumerator SpawnFireExplosion_DamagesAllEnemiesInRadius_NotJustDirectHit()
    {
        var center = NewEnemy(new Vector3(5f, 0.36f, 0f));
        var nearby = NewEnemy(new Vector3(5.5f, 0.36f, 0f)); // 폭발 반경(3티어 ≈1.41유닛) 안
        var far = NewEnemy(new Vector3(9f, 0.36f, 0f));      // 반경 훨씬 밖

        bool hit = MageSkillEffects.SpawnFireExplosion(new Vector3(5f, 0.36f, 0f), tier: 3, power: 0.62f,
            playerPos: new Vector3(4f, 0.36f, 0f), playerFacing: 1);
        yield return null;

        Assert.IsTrue(hit, "범위 안에 적이 있는데 hit=false");
        Assert.Less(center.GetComponent<EnemyHealth>().CurrentHp, center.GetComponent<EnemyHealth>().MaxHp, "직격 지점 적이 안 맞음");
        Assert.Less(nearby.GetComponent<EnemyHealth>().CurrentHp, nearby.GetComponent<EnemyHealth>().MaxHp, "반경 안 옆 적이 안 맞음(광역 판정 안 됨)");
        Assert.AreEqual(far.GetComponent<EnemyHealth>().MaxHp, far.GetComponent<EnemyHealth>().CurrentHp, 0.001f, "반경 밖 적까지 맞았다");
    }

    // ---- 중력 충돌 + 중력점 생성 (MageSkillEffects.DetonateGravityOrb, project_test.html:2023) ----

    [UnityTest]
    public IEnumerator DetonateGravityOrb_DamagesEnemiesAndSpawnsWell()
    {
        var enemy = NewEnemy(new Vector3(5f, 0.36f, 0f));
        Assert.AreEqual(0, GravityWellZone.Active.Count);

        int hitCount = MageSkillEffects.DetonateGravityOrb(new Vector3(5f, 0.36f, 0f), tier: 1, chargeK: 0f, playerFacing: 1);
        yield return null;

        Assert.AreEqual(1, hitCount, "충돌 반경 안 적을 못 맞힘");
        Assert.Less(enemy.GetComponent<EnemyHealth>().CurrentHp, enemy.GetComponent<EnemyHealth>().MaxHp);
        Assert.AreEqual(1, GravityWellZone.Active.Count, "충돌 후 중력점이 하나 생겨야 한다");
    }

    [UnityTest]
    public IEnumerator GravityWellZone_MergesInsteadOfSpawningSecond_WhenTierAtLeast2AndClose()
    {
        var well1 = GravityWellZone.SpawnOrMerge(new Vector3(5f, 0.36f, 0f), tier: 2, chargeK: 0f);
        yield return null;
        Assert.AreEqual(1, GravityWellZone.Active.Count);
        int stackAfterFirst = well1.Stack;

        var well2 = GravityWellZone.SpawnOrMerge(new Vector3(5.05f, 0.36f, 0f), tier: 2, chargeK: 0f); // 병합 반경 안
        yield return null;

        Assert.AreEqual(1, GravityWellZone.Active.Count, "가까이 또 만들었는데 새 중력점이 따로 생김 — 중첩이 아니라 새로 만들어짐");
        Assert.AreSame(well1, well2, "같은 인스턴스를 돌려줘야 한다(중첩)");
        Assert.Greater(well1.Stack, stackAfterFirst, "중첩 스택이 안 늘어남");
    }

    [UnityTest]
    public IEnumerator GravityWellZone_SpawnsAtHitFloorHeight_NotPulledToBottom()
    {
        // 회귀 테스트(2026-10-07) — MageSkillEffects.DetonateGravityOrb(2026-09-28)와 똑같은
        // "Y+가 위인데 Math.min을 그대로 써서 바닥 아래로 끌려감" 버그가 GravityWellZone.SpawnOrMerge에
        // 따로 복사돼 있었다. 3층 발판(y≈2.7) 높이에서 터뜨렸는데 결과 중력점이 맨 밑으로 끌려가면
        // 이 버그가 되돌아온 것이다.
        const float platformY = 2.7f;
        var well = GravityWellZone.SpawnOrMerge(new Vector3(5f, platformY, 0f), tier: 2, chargeK: 0f);
        yield return null;

        Assert.AreEqual(platformY, well.transform.position.y, 0.01f,
            "중력점이 맞은 높이가 아니라 다른 Y에 생겼다 — 바닥 클램프 방향이 또 뒤집힌 것으로 의심된다");
    }

    [UnityTest]
    public IEnumerator GravityWellZone_PullsNearbyEnemyCloser()
    {
        var enemy = NewEnemy(new Vector3(6.5f, 0.36f, 0f)); // 중력점 반경 안, 충분히 떨어진 위치
        var well = GravityWellZone.SpawnOrMerge(new Vector3(5f, 0.36f, 0f), tier: 3, chargeK: 0f);
        float startDist = Vector3.Distance(enemy.transform.position, well.transform.position);

        float t = 0f;
        while (t < 0.5f)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }

        float endDist = Vector3.Distance(enemy.transform.position, well.transform.position);
        Assert.Less(endDist, startDist, "중력점이 근처 적을 끌어당기지 않음");
    }

    // ---- 불길 이동 장판 (FireTrailZone, project_test.html:1995, 3844-3857) ----

    [UnityTest]
    public IEnumerator FireTrailZone_DamagesAndBurnsEnemyInRadius()
    {
        var enemy = NewEnemy(new Vector3(5f, 0.36f, 0f));
        var health = enemy.GetComponent<EnemyHealth>();
        FireTrailZone.Spawn(new Vector3(5f, 0.36f, 0f), tier: 3, power: 1f);

        float t = 0f;
        while (t < MageSpecConfig.FlameTrailTickInterval + 0.05f)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }

        Assert.Less(health.CurrentHp, health.MaxHp, "화염 장판 틱 피해가 안 들어감");
    }

    // ---- 대붕괴 (MageSkillEffects.GravityCollapse, project_test.html:2081) ----

    [UnityTest]
    public IEnumerator GravityCollapse_DetonatesNearbyWells_DamagesEnemies_AndClearsWells()
    {
        Vector3 playerPos = new Vector3(5f, 0.36f, 0f);
        GravityWellZone.SpawnOrMerge(playerPos, tier: 3, chargeK: 0.99f); // 최대 스택으로 한 번에 생성
        var enemy = NewEnemy(playerPos);
        yield return null;

        var (count, _, bursts) = MageSkillEffects.GravityCollapse(tier: 3, playerPos: playerPos, playerFacing: 1, nearestEnemyPos: null);
        yield return null;

        Assert.AreEqual(1, bursts, "중력점 1개가 있는데 폭발 지점 수가 다름");
        Assert.GreaterOrEqual(count, 1, "대붕괴가 적을 못 맞힘");
        // 3중첩 대붕괴는 오니(38)를 한 방에 죽일 수도 있다(치명타가 뜨면 피해가 38을 넘는다) —
        // 죽으면 오브젝트가 파괴되므로 "죽었거나, 살아있다면 체력이 깎였거나" 둘 다 통과로 본다.
        // (이 전제를 안 두면 난수에 따라 가끔 깨지는 플래키 테스트가 된다 — 실제로 그렇게 깨졌다.)
        if (enemy != null)
        {
            var h = enemy.GetComponent<EnemyHealth>();
            Assert.Less(h.CurrentHp, h.MaxHp, "대붕괴 범위 안인데 피해가 안 들어갔다");
        }
        Assert.AreEqual(0, GravityWellZone.Active.Count, "터뜨린 중력점이 안 사라짐");
    }

    // ---- MageAttack 배선 통합 검증 ----

    [UnityTest]
    public IEnumerator MageAttack_ExplosionTier3_ChainExplodesOnHit_WithoutNeedingCharge()
    {
        ProfileService.Current.level = 20;
        ProfileService.Current.TryLearnMageTier(MageBranch.Explosion);
        ProfileService.Current.TryLearnMageTier(MageBranch.Explosion);
        ProfileService.Current.TryLearnMageTier(MageBranch.Explosion); // 3티어

        var go = new GameObject("TestMageCaster");
        go.transform.position = new Vector3(2f, 0.36f, 0f);
        var mage = go.AddComponent<MageAttack>();
        var fireMethod = typeof(MageAttack).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);

        var direct = NewEnemy(new Vector3(3f, 0.36f, 0f));
        // 마법탄은 수평으로만 날아가므로(vy=0) 물리적으로 절대 못 지나가는 위치(수직으로 0.9유닛 위)에
        // 두면, 이 적이 맞는다면 그건 관통이 아니라 반드시 폭발 판정 때문이다 — 두 메커니즘을 확실히 분리.
        var splash = NewEnemy(new Vector3(3f, 1.26f, 0f)); // 직격 지점에서 수직으로 0.9유닛(폭발 반경 1.41 안)

        fireMethod.Invoke(mage, new object[] { 0f, ProfileService.Current.mageBranch, ProfileService.Current.mageTier });

        float t = 0f;
        while (t < 1f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }

        Assert.Less(direct.GetComponent<EnemyHealth>().CurrentHp, direct.GetComponent<EnemyHealth>().MaxHp, "직격 적이 안 맞음");
        Assert.Less(splash.GetComponent<EnemyHealth>().CurrentHp, splash.GetComponent<EnemyHealth>().MaxHp,
            "3티어는 무차지여도 착탄마다 연쇄 폭발해야 하는데(project_test.html:3680) 옆 적이 안 맞음");

        Object.Destroy(go);
    }

    [UnityTest]
    public IEnumerator MageAttack_GravityBranch_HitDoesNotDealDirectDamage_ButSpawnsWell()
    {
        ProfileService.Current.level = 5;
        ProfileService.Current.TryLearnMageTier(MageBranch.Gravity); // 1티어

        var go = new GameObject("TestMageCaster");
        go.transform.position = new Vector3(2f, 0.36f, 0f);
        var mage = go.AddComponent<MageAttack>();
        var fireMethod = typeof(MageAttack).GetMethod("Fire", BindingFlags.NonPublic | BindingFlags.Instance);

        var enemy = NewEnemy(new Vector3(3f, 0.36f, 0f));

        fireMethod.Invoke(mage, new object[] { 0f, ProfileService.Current.mageBranch, ProfileService.Current.mageTier });

        float t = 0f;
        while (t < 1f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }

        // 중력탄 직격 자체는 dealDamage를 안 부른다(project_test.html:3673-3677) — 대신 착탄 즉시
        // detonateGravityOrb의 충돌 반경 AoE로만 피해가 들어가고, 중력점이 하나 생겨야 한다.
        var health = enemy.GetComponent<EnemyHealth>();
        Assert.Less(health.CurrentHp, health.MaxHp, "중력탄 충돌 AoE로도 적이 안 맞음");
        Assert.AreEqual(1, GravityWellZone.Active.Count, "중력탄이 맞았는데 중력점이 안 생김");

        Object.Destroy(go);
    }

    // ────────────────────────── 중력 취약 (원본 vulnT, :1663·:1726·:3818) ──────────────────────────

    /// <summary>
    /// 중력점에 오래(누적 1.25) 노출되면 취약이 걸린다. 원본은 노출량이 **중첩에 비례해 빨리** 쌓인다
    /// (`dt * (1 + stack*0.16)`)는 것까지 확인한다 — 여기선 계산이 결정적이라 인터페이스로 직접 먹인다.
    /// </summary>
    [Test]
    public void GravityExposure_TurnsEnemyVulnerable_AtThreshold()
    {
        var enemy = NewEnemy(new Vector3(5f, 0.36f, 0f));
        var vuln = enemy.GetComponent<IVulnerable>();
        Assert.IsNotNull(vuln, "EnemyHealth가 IVulnerable을 구현해야 중력점이 취약을 걸 수 있다");

        vuln.AddGravityExposure(MageSpecConfig.GravityVulnerableThreshold - 0.01f);
        Assert.IsFalse(vuln.IsVulnerable, "임계치 직전엔 아직 취약이 아니다");

        vuln.AddGravityExposure(0.02f);
        Assert.IsTrue(vuln.IsVulnerable, "누적 1.25를 넘으면 취약해져야 한다");

        Object.DestroyImmediate(enemy);
    }

    /// <summary>취약한 적은 받는 피해가 1.2배다(원본 `vulnMult`, `:1663`).</summary>
    [Test]
    public void Vulnerable_IncreasesDamageTaken()
    {
        var plain = NewEnemy(new Vector3(5f, 0.36f, 0f));
        var weak = NewEnemy(new Vector3(8f, 0.36f, 0f));
        weak.GetComponent<IVulnerable>().ApplyVulnerable(MageSpecConfig.VulnerableDuration);

        const float hit = 10f;
        var plainHealth = plain.GetComponent<EnemyHealth>();
        var weakHealth = weak.GetComponent<EnemyHealth>();
        float plainBefore = plainHealth.CurrentHp, weakBefore = weakHealth.CurrentHp;

        // 넉백 0으로 넣어 물리가 개입하지 않게 한다(피해량만 보는 테스트).
        plainHealth.TakeDamageWithKnockback(hit, null, 1f, 0f);
        weakHealth.TakeDamageWithKnockback(hit, null, 1f, 0f);

        Assert.AreEqual(hit, plainBefore - plainHealth.CurrentHp, 0.01f);
        Assert.AreEqual(hit * DamageCalculator.VulnerableMultiplier, weakBefore - weakHealth.CurrentHp, 0.01f);

        Object.DestroyImmediate(plain);
        Object.DestroyImmediate(weak);
    }

    /// <summary>
    /// 취약은 시간이 지나면 풀린다(원본 `vulnT = max(0, vulnT - dt)`, `:1726`).
    /// 안 풀리면 한 번 중력점에 닿은 적이 **영구히** 1.2배로 맞는 전혀 다른 밸런스가 된다.
    /// </summary>
    [UnityTest]
    public IEnumerator Vulnerable_ExpiresOverTime()
    {
        var enemy = NewEnemy(new Vector3(5f, 0.36f, 0f));
        var vuln = enemy.GetComponent<IVulnerable>();
        vuln.ApplyVulnerable(0.1f);
        Assert.IsTrue(vuln.IsVulnerable);

        float waited = 0f;
        while (waited < 0.4f) { waited += Time.deltaTime; yield return null; }
        Assert.IsFalse(vuln.IsVulnerable, "지속시간이 지났는데도 취약이 안 풀렸다");

        Object.DestroyImmediate(enemy);
    }
}
}
