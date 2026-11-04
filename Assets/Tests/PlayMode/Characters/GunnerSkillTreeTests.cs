using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
/// 메카닉 스킬트리(단계 2) 검증 — 원본 `SPEC.gunner`(project_test.html:915)의 레이저·설치기 두 갈래가
/// 실제로 `updateGunnerBeam`(:2224)·`summonLaserDrone`(:2363)·`placeGunnerTurret`(:2349)·
/// `tryGunnerSkill`(:2390)·`addGunnerStack`(:1411)·설치기 장판(`updateZones` :3766)대로 동작하는지 본다.
///
/// 수치만 보는 테스트가 아니라 **실제로 적이 맞는지**를 본다 — 값은 맞는데 호출부가 없어서 안 쓰이는
/// 실수(CLAUDE.md "정의만 하고 호출부를 안 만드는 실수")를 행동으로 잡으려는 것.
/// Z/X 실제 키 입력은 시뮬레이트할 수 없어 다른 키트 테스트와 같은 관례로 private 메서드를 리플렉션으로 부른다.
/// </summary>
public class GunnerSkillTreeTests
{
    static readonly string[] SpawnedNames =
    {
        "TestGunner", "TestEnemy", "GunnerBullet", "GunnerDrone", "GunnerTurret", "GunnerBeam", "GunnerFieldLinks",
    };

    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            foreach (var n in SpawnedNames)
                if (go.name == n) { Object.DestroyImmediate(go); break; }
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

    // ───────────────────────── 헬퍼 ─────────────────────────

    static GunnerAttack NewGunner(Vector3 pos, GunnerBranch branch, int tier)
    {
        var go = new GameObject("TestGunner");
        go.transform.position = pos;
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterMover2D>();
        // 이동기가 Awake에서 gravityScale을 1로 되돌리므로 그 뒤에 다시 고정한다 — 판정 기준점이 흔들리지 않게.
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        var g = go.AddComponent<GunnerAttack>();
        SetBuild(branch, tier);
        return g;
    }

    /// <summary>메카닉 빌드는 세이브(프로필)에 있다 — 원본 `meta.skills.gunner`. 키트는 매 프레임 읽기만 한다.</summary>
    static void SetBuild(GunnerBranch branch, int tier)
    {
        ProfileService.Current.character = CharacterId.Gunner;
        ProfileService.Current.gunnerBranch = branch;
        ProfileService.Current.gunnerTier = tier;
    }

    static EnemyHealth NewEnemy(Vector3 pos, float radius = 0.5f, float hp = 100000f)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = radius;
        var move = go.AddComponent<EnemyMove>();
        var health = go.AddComponent<EnemyHealth>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(move, 0f);
        var rb = go.GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        move.enabled = false; // 배회 AI가 판정 위치를 흔들지 않게
        health.SetMaxHp(hp);  // 여러 틱을 맞아도 죽지 않게
        return health;
    }

    static object Call(object obj, string method, params object[] args)
    {
        var m = obj.GetType().GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, method + "()가 없다");
        return m.Invoke(obj, args);
    }

    static void HoldBeam(GunnerAttack g, int frames, float dt = 0.05f)
    {
        for (int i = 0; i < frames; i++) Call(g, "UpdateBeam", dt, true);
    }

    static void SetUltReady(GunnerAttack g) =>
        typeof(GunnerAttack).GetField("ultCdTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(g, 0f);

    static bool Damaged(EnemyHealth h) => h.CurrentHp < h.MaxHp;

    // ───────────────────────── 레이저 빔 (updateGunnerBeam) ─────────────────────────

    [UnityTest]
    public IEnumerator LaserBeam_Tier1_HitsEnemyAhead_NotBehindOrBeyondRange_AndChargesStacks()
    {
        // 총구 = 발(5, 0.5-0.5) + (0.26, 0.38) — 원본 p.y는 발. 1층 사거리 = (360+40)×0.8 ÷100 = 3.2
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Laser, 1);
        var ahead = NewEnemy(new Vector3(7.26f, 0.38f, 0f));
        var behind = NewEnemy(new Vector3(3.0f, 0.38f, 0f));
        var beyond = NewEnemy(new Vector3(9.26f, 0.38f, 0f)); // 앞으로 4.0 > 3.2
        yield return new WaitForFixedUpdate();

        HoldBeam(g, 30); // 1.5초 — 틱 0.13초

        Assert.IsTrue(g.Beam.Active, "레이저 1층인데 Z를 누르고 있어도 광선이 안 켜졌다");
        Assert.IsTrue(Damaged(ahead), "광선 앞의 적이 안 맞았다");
        Assert.IsFalse(Damaged(behind), "등 뒤의 적이 맞았다 — 원본은 along < 0이면 제외");
        Assert.IsFalse(Damaged(beyond), "사거리(3.2) 밖의 적이 맞았다");
        Assert.AreEqual(9, g.Stacks, "적중마다 스택 +1, 1층 최대치는 6+1×3=9여야 한다");

        Call(g, "UpdateBeam", 0.05f, false);
        Assert.IsFalse(g.Beam.Active, "Z를 떼면 광선이 꺼져야 한다");
    }

    [UnityTest]
    public IEnumerator LaserBeam_Tier3_BendsToEnemyInsideCone_Tier1DoesNot()
    {
        // 총구에서 (2.5, 0.6) — 각도 0.2355rad(3층 콘 0.26 안), 직선 광선과의 거리 0.6 > 폭 0.156+적폭×0.35=0.506
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Laser, 1);
        var off = NewEnemy(new Vector3(7.76f, 0.98f, 0f));
        yield return new WaitForFixedUpdate();

        HoldBeam(g, 20);
        Assert.IsFalse(Damaged(off), "1층은 유도가 없는데 비스듬한 적이 맞았다");

        SetBuild(GunnerBranch.Laser, 3);
        HoldBeam(g, 20);
        Assert.AreEqual(off.GetComponent<Collider2D>(), g.Beam.Target, "3층 광선이 콘 안의 적을 목표로 잡지 않았다");
        Assert.IsTrue(Damaged(off), "3층 광선이 콘 안의 적 쪽으로 휘지 않았다");
    }

    [UnityTest]
    public IEnumerator LaserBeam_Tier5_BecomesCurvedAndHitsWideAngleTarget()
    {
        // 각도 atan(1.2/2.0)=0.54rad — 4층 콘(0.44) 밖, 5층 콘(0.6109) 안
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Laser, 4);
        var wide = NewEnemy(new Vector3(7.26f, 1.58f, 0f));
        yield return new WaitForFixedUpdate();

        HoldBeam(g, 20);
        Assert.IsNull(g.Beam.Target, "4층 콘(±0.44rad) 밖의 적을 목표로 잡았다");

        SetBuild(GunnerBranch.Laser, 5);
        HoldBeam(g, 20);
        Assert.IsTrue(g.Beam.Curved, "5층 광선이 곡선이 되지 않았다");
        Assert.IsTrue(Damaged(wide), "5층 곡선 광선이 넓은 콘 안의 적을 못 맞혔다");
    }

    [UnityTest]
    public IEnumerator InstallerBranch_ZStaysBullet_NoBeam()
    {
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Installer, 3);
        yield return null;
        Assert.IsFalse((bool)Call(g, "UpdateBeam", 0.05f, true), "설치기 빌드는 Z가 총알이어야 한다(광선은 레이저 빌드 전용)");
        Assert.AreEqual(0.24f, g.cooldown, 0.001f, "설치기 빌드 총알 쿨다운은 0차와 같은 0.24여야 한다");
    }

    // ───────────────────────── 총알 (gunnerFire) ─────────────────────────

    [UnityTest]
    public IEnumerator LaserBuildBullet_UsesLaserTierCooldownAndDamage()
    {
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Laser, 4);
        yield return null;
        float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
        Assert.AreEqual(0.18f, g.cooldown, 0.001f, "레이저 4층 총알 쿨다운은 0.18이어야 한다");
        Assert.AreEqual(atk * (0.66f + 4 * 0.08f), g.baseDamage, 0.001f, "레이저 총알 피해는 0.66+tier×0.08");
    }

    [UnityTest]
    public IEnumerator InstallerBullet_HitCountsTowardNextPart()
    {
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Installer, 1);
        var enemy = NewEnemy(new Vector3(6.2f, 0.5f, 0f));
        yield return new WaitForFixedUpdate();

        Call(g, "Fire");
        float t = 0f;
        while (t < 0.4f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }

        Assert.IsTrue(Damaged(enemy), "총알이 적을 못 맞혔다");
        Assert.AreEqual(1, g.PartHits, "설치기 빌드는 총알 적중마다 부품 적중 수가 1 올라야 한다");
    }

    /// <summary>
    /// 고블린 크기(키 0.5)의 적이 바닥에 서 있을 때, 같은 바닥에 선 메카닉의 정면 총알이 **맞아야** 한다.
    /// 총구를 몸 중심에서 재던 시절엔 총알이 고블린 머리 위로 지나갔다 — 원본 `p.y - 38`의 p.y는 발이다.
    /// </summary>
    [UnityTest]
    public IEnumerator Bullet_HitsOniSizedEnemyOnSameGround()
    {
        // 지금 플레이어 크기(EntitySizeConfig: 키 0.5)로 바닥(y=0)에 세운다.
        var g = NewGunner(new Vector3(5f, EntitySizeConfig.PlayerRadius, 0f), GunnerBranch.None, 0);
        g.GetComponent<CircleCollider2D>().radius = EntitySizeConfig.PlayerRadius;
        var oni = NewEnemy(new Vector3(6.5f, EntitySizeConfig.OniRadius, 0f), EntitySizeConfig.OniRadius);
        yield return new WaitForFixedUpdate();

        Call(g, "Fire");
        float t = 0f;
        while (t < 0.4f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }

        Assert.IsTrue(Damaged(oni), "바닥의 고블린 크기 적을 정면 총알이 못 맞혔다 — 총구 높이를 발에서 재는지 확인");
    }

    [UnityTest]
    public IEnumerator HomingBullet_SteersIntoOffAxisEnemy_PlainBulletMisses()
    {
        var target = NewEnemy(new Vector3(8f, 2.2f, 0f), 0.3f);
        yield return new WaitForFixedUpdate();

        GunnerBullet.Spawn(new Vector3(5f, 0.88f, 0f), new Vector2(13.2f, 0f), 10f, 1, 0.8f, 1f, null, Color.white);
        float t = 0f;
        while (t < 0.6f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }
        Assert.IsFalse(Damaged(target), "유도 없는 총알이 비스듬한 적을 맞혔다 — 기준 조건이 틀렸다");

        GunnerBullet.Spawn(new Vector3(5f, 0.88f, 0f), new Vector2(13.2f, 0f), 10f, 1, 0.8f, 1f, null, Color.white,
            homing: true, seekRange: 4.2f, turnRate: 12f);
        t = 0f;
        while (t < 0.6f) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }
        Assert.IsTrue(Damaged(target), "유도탄이 탐지 거리 안의 적 쪽으로 휘지 않았다");
    }

    // ───────────────────────── 충전 스택 / 부품 (addGunnerStack) ─────────────────────────

    [UnityTest]
    public IEnumerator Stacks_NoBranchIgnored_LaserCapsAtMax_InstallerOnePartPerTenHits()
    {
        // 빌드는 프로필 하나라 키트 하나로 빌드를 바꿔 가며 본다.
        var g = NewGunner(new Vector3(4f, 0.5f, 0f), GunnerBranch.None, 0);
        yield return null;

        Assert.AreEqual(0, g.AddStack(5), "0차는 스택이 쌓이면 안 된다(원본 addGunnerStack의 `if (!s.branch) return 0`)");

        SetBuild(GunnerBranch.Laser, 4);
        g.AddStack(100);
        Assert.AreEqual(6 + 4 * 3 + 4, g.Stacks, "레이저 4층 최대치는 6+tier×3+4=22");

        SetBuild(GunnerBranch.Installer, 1);
        var inst = g;
        inst.ResetForRun();
        Assert.AreEqual(1, inst.Stacks, "설치기 빌드는 부품 1개를 쥐고 시작한다");
        for (int i = 0; i < 9; i++) inst.AddStack(1);
        Assert.AreEqual(1, inst.Stacks, "적중 9회로는 부품이 안 생긴다");
        Assert.AreEqual(9, inst.PartHits);
        inst.AddStack(1);
        Assert.AreEqual(2, inst.Stacks, "적중 10회마다 부품 +1");
        Assert.AreEqual(0, inst.PartHits);
        inst.AddStack(10);
        Assert.AreEqual(3, inst.Stacks, "1층 부품 최대치(=설치기 한도)는 3");
        inst.AddStack(5);
        Assert.AreEqual(3, inst.Stacks);
        Assert.AreEqual(0, inst.PartHits, "부품이 가득 차면 적중 수를 세지 않는다");
    }

    // ───────────────────────── X 스킬 (tryGunnerSkill) ─────────────────────────

    [UnityTest]
    public IEnumerator Ult_WithoutBuild_DoesNothing()
    {
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.None, 0);
        yield return null;
        Call(g, "TryUseSkill");
        Assert.AreEqual(0, GunnerDrone.Active.Count);
        Assert.AreEqual(0, GunnerTurret.Active.Count);
        Assert.AreEqual(0f, g.UltCooldownRemaining, 0.0001f, "빌드가 없으면 쿨다운도 돌면 안 된다");
    }

    [UnityTest]
    public IEnumerator DroneUlt_SpendsAllStacks_IntoDroneLife_WithOriginalCooldown()
    {
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Laser, 1);
        yield return null;
        g.AddStack(5);

        Call(g, "TryUseSkill");
        Assert.AreEqual(0, g.Stacks, "드론 소환은 충전 스택을 전부 쓴다");
        Assert.AreEqual(1, GunnerDrone.Active.Count, "1층은 드론 1기");
        Assert.AreEqual(4.0f + 1 * 0.45f + 5 * 0.32f, GunnerDrone.Active[0].Life, 0.001f, "드론 지속 = 4.0+tier×0.45+스택×0.32");
        Assert.AreEqual(30f * 0.34f, g.UltCooldownRemaining, 0.01f, "드론 쿨다운 = 30×0.34");

        var first = GunnerDrone.Active[0];
        Call(g, "TryUseSkill"); // 쿨다운 중
        Assert.AreEqual(1, GunnerDrone.Active.Count);
        Assert.AreSame(first, GunnerDrone.Active[0], "쿨다운 중에 드론이 교체됐다");
    }

    [UnityTest]
    public IEnumerator DroneUlt_Tier5_SummonsTwo_AndResummonReplacesOld()
    {
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Laser, 5);
        yield return null;

        Call(g, "TryUseSkill");
        Assert.AreEqual(2, GunnerDrone.Active.Count, "5층은 드론 2기");
        Assert.AreEqual(30f * 0.34f * 0.72f, g.UltCooldownRemaining, 0.01f, "4층부터 드론 쿨다운 ×0.72");
        var old = new List<GunnerDrone>(GunnerDrone.Active);

        SetUltReady(g);
        Call(g, "TryUseSkill");
        Assert.AreEqual(2, GunnerDrone.Active.Count, "다시 소환해도 2기 — 기존 드론은 교체된다");
        foreach (var d in old) Assert.IsFalse(GunnerDrone.Active.Contains(d), "기존 드론이 남아 있다");
    }

    [UnityTest]
    public IEnumerator Drone_FiresOnlyAtEnemiesWithinRange()
    {
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Laser, 1);
        // 드론은 발(5, 0) 기준 (5-0.56-0.42, 0+0.86) 근처를 따라다닌다. 사거리 1층 = (420+55)÷100 = 4.75
        var far = NewEnemy(new Vector3(-1.2f, 1.36f, 0f)); // 약 5.2 떨어짐
        yield return new WaitForFixedUpdate();

        Call(g, "TryUseSkill");
        yield return new WaitForSeconds(1.0f);
        Assert.IsNull(GameObject.Find("GunnerBullet"), "사거리 밖의 적에게 드론이 쐈다");
        Assert.IsFalse(Damaged(far));

        var near = NewEnemy(new Vector3(7.2f, 0.9f, 0f));
        yield return new WaitForSeconds(1.2f);
        Assert.IsTrue(Damaged(near), "드론이 사거리 안의 적을 쏘지 않았다");
    }

    [UnityTest]
    public IEnumerator TurretUlt_NeedsPart_PlacesInFront_WithOriginalCooldown()
    {
        var g = NewGunner(new Vector3(5f, 0.5f, 0f), GunnerBranch.Installer, 1);
        yield return null;

        Call(g, "TryUseSkill");
        Assert.AreEqual(0, GunnerTurret.Active.Count, "부품이 없는데 설치기가 놓였다");
        Assert.AreEqual(0f, g.UltCooldownRemaining, 0.0001f, "부품이 없으면 쿨다운도 돌면 안 된다");

        g.ResetForRun(); // 부품 1개
        Call(g, "TryUseSkill");
        Assert.AreEqual(1, GunnerTurret.Active.Count);
        Assert.AreEqual(0, g.Stacks, "설치에 부품 1개를 써야 한다");
        var tp = GunnerTurret.Active[0].transform.position;
        Assert.AreEqual(5f + 0.34f, tp.x, 0.001f, "설치기는 캐릭터 앞 0.34에 놓인다");
        Assert.AreEqual(0f, tp.y, 0.001f, "설치기는 캐릭터 발 높이(원본 y: p.y)에 놓인다");
        Assert.AreEqual(0.55f - 0.03f, g.UltCooldownRemaining, 0.001f, "설치기 쿨다운 = 0.55-min(0.18, tier×0.03)");
    }

    [UnityTest]
    public IEnumerator TurretUlt_OverLimit_RemovesOldest()
    {
        var g = NewGunner(new Vector3(12f, 0.5f, 0f), GunnerBranch.Installer, 1);
        var first = GunnerTurret.Spawn(new Vector2(4f, 0.5f), 1, null);
        yield return new WaitForSeconds(0.1f);
        GunnerTurret.Spawn(new Vector2(6f, 0.5f), 1, null);
        yield return new WaitForSeconds(0.1f);
        GunnerTurret.Spawn(new Vector2(8f, 0.5f), 1, null);
        yield return null;

        g.AddStack(10); // 부품 +1
        Call(g, "TryUseSkill");
        Assert.AreEqual(3, GunnerTurret.Active.Count, "1층 한도는 3개");
        Assert.IsFalse(GunnerTurret.Active.Contains(first), "가장 오래된 설치기가 교체되지 않았다");
    }

    [UnityTest]
    public IEnumerator Turret_DamagesOnlyEnemiesWithinRange()
    {
        GunnerTurret.Spawn(new Vector2(10f, 0.5f), 1, null); // 판정 중심 (10, 0.68), 1층 반경 1.36
        var inside = NewEnemy(new Vector3(11.2f, 0.68f, 0f));   // 1.2 < 1.36+0.5
        var outside = NewEnemy(new Vector3(12.2f, 0.68f, 0f));  // 2.2 > 1.86
        yield return new WaitForSeconds(1.2f);                   // 1층 틱 0.48초

        Assert.IsTrue(Damaged(inside), "설치기 반경 안의 적이 안 맞았다");
        Assert.IsFalse(Damaged(outside), "설치기 반경 밖의 적이 맞았다");
    }

    // ───────────────────────── 링크 · 내부 장판 ─────────────────────────

    [UnityTest]
    public IEnumerator Field_TwoLinkedTurrets_DamageEnemyOnLink()
    {
        NewGunner(new Vector3(20f, 0.5f, 0f), GunnerBranch.Installer, 1);
        GunnerTurret.Spawn(new Vector2(5f, 0.5f), 1, null);
        GunnerTurret.Spawn(new Vector2(8.6f, 0.5f), 1, null); // 3.6 ≤ 링크 한도 3.70
        // 링크 선(y=0.74) 위, 두 설치기 반경(1.36+0.2) 밖
        var onLink = NewEnemy(new Vector3(6.8f, 0.74f, 0f), 0.2f);
        yield return new WaitForSeconds(1.0f); // 장판 틱 0.38초

        Assert.IsTrue(Damaged(onLink), "연결선 위의 적이 장판 피해를 안 받았다");
    }

    [UnityTest]
    public IEnumerator Field_TurretsTooFarApart_NoLinkDamage()
    {
        NewGunner(new Vector3(20f, 0.5f, 0f), GunnerBranch.Installer, 1);
        GunnerTurret.Spawn(new Vector2(5f, 0.5f), 1, null);
        GunnerTurret.Spawn(new Vector2(9.0f, 0.5f), 1, null); // 4.0 > 링크 한도 3.70
        var mid = NewEnemy(new Vector3(7.0f, 0.74f, 0f), 0.2f);
        yield return new WaitForSeconds(1.0f);

        Assert.IsFalse(Damaged(mid), "링크 한도보다 먼 설치기끼리 연결됐다");
    }

    [UnityTest]
    public IEnumerator Field_ClosedTriangle_DamagesEnemyInside()
    {
        NewGunner(new Vector3(20f, 0.5f, 0f), GunnerBranch.Installer, 1);
        GunnerTurret.Spawn(new Vector2(5f, 0.5f), 1, null);
        GunnerTurret.Spawn(new Vector2(8.6f, 0.5f), 1, null);
        GunnerTurret.Spawn(new Vector2(6.8f, 3.62f), 1, null); // 세 변 모두 ≈3.6 ≤ 3.70 → 닫힌 삼각형
        // 무게중심 — 링크에서 1.04, 설치기 판정 중심에서 2.0 이상 떨어져 장판 "내부"로만 맞는다
        var inside = NewEnemy(new Vector3(6.8f, 1.78f, 0f), 0.2f);
        yield return new WaitForSeconds(1.0f);

        Assert.IsTrue(Damaged(inside), "닫힌 장판 안쪽의 적이 피해를 안 받았다");
    }

    [UnityTest]
    public IEnumerator FieldPoints_FlatRowOfThree_LiftsMiddlePoint()
    {
        var a = GunnerTurret.Spawn(new Vector2(5f, 0.5f), 1, null);
        var b = GunnerTurret.Spawn(new Vector2(6.5f, 0.5f), 1, null);
        var c = GunnerTurret.Spawn(new Vector2(8f, 0.5f), 1, null);
        yield return null;

        var pts = GunnerField.FieldPoints(new List<GunnerTurret> { a, b, c });
        Assert.AreEqual(3, pts.Count);
        // 원본 `pts[1].y = min(pts[0].y, pts[2].y) - 120`(Y+ 아래) → Unity에선 위로 1.20
        Assert.AreEqual(0.5f + 0.24f + 1.20f, pts[1].y, 0.001f, "일직선 설치기 셋의 가운데 꼭짓점을 들어 올려야 한다");
        Assert.AreEqual(0.74f, pts[0].y, 0.001f);
        Assert.AreEqual(0.74f, pts[2].y, 0.001f);
    }

    [Test]
    public void FieldGeometry_MatchesOriginalHelpers()
    {
        var square = new List<Vector2> { new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2) };
        Assert.IsTrue(GunnerField.PointInPolygon(new Vector2(1, 1), square));
        Assert.IsFalse(GunnerField.PointInPolygon(new Vector2(3, 1), square));
        Assert.AreEqual(1f, GunnerField.DistToSegment(new Vector2(1, 1), new Vector2(0, 0), new Vector2(2, 0)), 1e-4f);

        // 볼록 껍질 변 위에 놓인 점도 다시 끼워 넣는다(원본 convexHullPoints의 onEdge 처리)
        var withEdgePoint = new List<Vector2>
            { new Vector2(0, 0), new Vector2(1, 0), new Vector2(2, 0), new Vector2(2, 2), new Vector2(0, 2) };
        Assert.AreEqual(5, GunnerField.ConvexHull(withEdgePoint).Count, "변 위의 점이 껍질에서 빠졌다");

        var tri = new List<Vector2> { new Vector2(0, 0), new Vector2(3, 0), new Vector2(1.5f, 2) };
        Assert.AreEqual(3, GunnerField.LinkSegments(tri, 4f).Count, "세 변이 다 연결돼야 닫힌 도형");
        Assert.AreEqual(2, GunnerField.LinkSegments(tri, 2.6f).Count, "한도보다 긴 변(3.0)은 연결되면 안 된다");
    }

    // ───────────────────────── 빌드 습득 · 시간 회귀 (PlayerProfile) ─────────────────────────

    /// <summary>원본 `learnSkill('gunner', …)`(:7011) — 갈래 고정, 1층부터 순서대로, 층 비용 1~5, SP 부족 시 실패.</summary>
    [Test]
    public void LearnGunnerTier_FollowsOriginalLearnSkillRules()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner; // SP는 캐릭터마다 따로 — 원본처럼 메카닉을 고른 채로 배운다
        p.level = 3; // SP 2
        Assert.IsTrue(p.TryLearnGunnerTier(GunnerBranch.Laser), "SP 2로 1층(비용 1)을 못 배웠다");
        Assert.AreEqual(GunnerBranch.Laser, p.gunnerBranch);
        Assert.AreEqual(1, p.gunnerTier);
        Assert.IsFalse(p.TryLearnGunnerTier(GunnerBranch.Installer), "갈래는 처음 고른 뒤 바꿀 수 없다");
        Assert.IsFalse(p.TryLearnGunnerTier(GunnerBranch.Laser), "남은 SP 1로 2층(비용 2)을 배웠다");

        p.level = 16; // SP 15 = 1+2+3+4+5
        for (int t = 2; t <= 5; t++) Assert.IsTrue(p.TryLearnGunnerTier(GunnerBranch.Laser), $"{t}층을 못 배웠다");
        Assert.AreEqual(5, p.gunnerTier);
        Assert.AreEqual(15, p.spUsed);
        Assert.IsFalse(p.TryLearnGunnerTier(GunnerBranch.Laser), "5층 위로 올라갔다");
    }

    /// <summary>원본 doRebirth(:6509~) — 모든 무기의 빌드를 비우고, '각인의 봉인'이 있으면 그대로 둔다.</summary>
    [Test]
    public void Regression_ClearsGunnerBuild_UnlessKeepSpec()
    {
        var p = ProfileService.Current;
        p.MarkBossCleared(1); // 정복 지역이 없으면 회귀 자체가 안 된다
        p.gunnerBranch = GunnerBranch.Installer;
        p.gunnerTier = 3;
        Assert.Greater(p.DoRegression(), 0);
        Assert.AreEqual(GunnerBranch.None, p.gunnerBranch, "회귀했는데 메카닉 갈래가 남았다");
        Assert.AreEqual(0, p.gunnerTier);

        p.MarkBossCleared(1);
        p.gunnerBranch = GunnerBranch.Laser;
        p.gunnerTier = 2;
        p.items.Add("keepSpec");
        Assert.Greater(p.DoRegression(), 0);
        Assert.AreEqual(GunnerBranch.Laser, p.gunnerBranch, "각인의 봉인이 있는데 메카닉 빌드가 지워졌다");
        Assert.AreEqual(2, p.gunnerTier);
    }

    [Test]
    public void SpecValues_MatchOriginalFormulas()
    {
        Assert.AreEqual(3.2f, GunnerSpecConfig.BeamRange(1), 1e-4f);
        Assert.AreEqual(4.16f, GunnerSpecConfig.BeamRange(5), 1e-4f);
        Assert.AreEqual(0.13f, GunnerSpecConfig.BeamBaseTick(3), 1e-4f);
        Assert.AreEqual(0.085f, GunnerSpecConfig.BeamBaseTick(5), 1e-4f);
        Assert.AreEqual((0.16f + 5 * 0.028f) * 1.18f, GunnerSpecConfig.BeamDamageMult(5), 1e-4f);
        Assert.AreEqual(3, GunnerSpecConfig.TurretMax(1));
        Assert.AreEqual(4, GunnerSpecConfig.TurretMax(4));
        Assert.AreEqual(5, GunnerSpecConfig.TurretMax(5));
        Assert.AreEqual(3.70f, GunnerSpecConfig.LinkMax(1), 1e-4f);
        Assert.AreEqual(8.60f, GunnerSpecConfig.LinkMax(5), 1e-4f);
        Assert.AreEqual(9999f, GunnerSpecConfig.TurretLife(5), 1e-4f);
        Assert.AreEqual(3, GunnerSpecConfig.BulletPierce(2));
        Assert.AreEqual(4, GunnerSpecConfig.BulletPierce(5));
    }
}
}
