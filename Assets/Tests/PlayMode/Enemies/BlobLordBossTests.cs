using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 1지역 보스 덩어리 지배자(`Enemies/BlobLordBoss`, 2026-11-10 팀원 명세) — 실제 프리팹(`Prefabs/Enemies/Enemy_BlobLord`)을 세워 물리 프레임을 흘린다.
/// 보스 무대(좁은 맵·발판 3개)에서 돈다.
/// </summary>
public class BlobLordBossTests
{
    const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_BlobLord.prefab";
    const string CatalogPath = "Assets/Data/Enemies/MonsterCatalog.asset";

    /// <summary>맞는 쪽 — 플레이어 대신. 최대 체력 200(기본 100이 아니어야 % 피해가 진짜 최대 체력을 읽는지 보인다).</summary>
    class Dummy : MonoBehaviour, IDamageable, IHealthView
    {
        public float maxHp = 200f;
        public float taken; public int hits; public bool dead;
        public float CurrentHp => maxHp - taken;
        public float MaxHp => maxHp;
        public bool IsDead => dead;
        public void TakeDamage(float amount, GameObject source) { taken += amount; hits++; }
    }

    float savedTimeScale;

    [SetUp]
    public void Setup()
    {
        Reset();
        FieldLayout.SetBossArena(true);
        savedTimeScale = Time.timeScale;
        Time.timeScale = 3f; // 패턴 한 바퀴가 3초쯤 — 빨리 감는다(물리 프레임 수는 같다)
    }

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = savedTimeScale;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && (go.name.StartsWith("Test") || go.name.StartsWith("BlobLord") || go.name.StartsWith("Enemy_BlobLord") || go.name == "RangeRing"))
                Object.DestroyImmediate(go);
        FieldLayout.SetBossArena(false);
        Reset();
    }

    static void Reset()
    {
        CombatModifiers.Reset(); CombatEvents.Reset(); RunEvents.Reset(); RunState.Reset();
        RunTransient.Reset(); EnemySpawnRequestBus.Reset(); ProfileService.Reset();
    }

    static GameObject LoadPrefab()
    {
        var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(p, "덩어리 지배자 프리팹이 없다: " + PrefabPath);
        return p;
    }

    static BlobLordBoss NewBoss(float x, Transform target)
    {
        var go = Object.Instantiate(LoadPrefab(), new Vector3(x, FieldBounds.GroundY + 1f, 0f), Quaternion.identity);
        go.name = "Enemy_BlobLord";
        var move = go.GetComponent<EnemyMove>();
        move.SetSpawnProtection(0f);
        move.Target = target;
        go.GetComponent<EnemyHealth>().SetMaxHp(1000f);
        return go.GetComponent<BlobLordBoss>();
    }

    static Dummy NewTarget(float x, float surfaceY)
    {
        var go = new GameObject("TestTarget");
        go.tag = "Player";
        go.layer = LayerMask.NameToLayer("Player"); // 게임처럼 몹과 물리로 안 밀어낸다(레이어 충돌표)
        go.transform.position = new Vector3(x, surfaceY + 0.25f, 0f);
        var rb = go.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<CircleCollider2D>().radius = 0.25f;
        return go.AddComponent<Dummy>();
    }

    static IEnumerator Until(System.Func<bool> cond, float maxSeconds = 12f)
    {
        float t = 0f;
        while (!cond() && t < maxSeconds) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }
        Assert.IsTrue(cond(), "시간 안에 조건이 안 됐다");
    }

    // ───────────────────────── 연결 ─────────────────────────

    /// <summary>1지역은 덩어리 지배자, 다른 지역은 원본 공통 보스(사용자 2026-11-10 "1지역 보스만"). 공통 보스 체력 곡선(1지역 800)은 그대로.</summary>
    [Test]
    public void Catalog_Region1UsesBlobLord_OthersKeepCommonBoss()
    {
        var cat = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
        Assert.IsNotNull(cat.BossPrefabFor(1).GetComponent<BlobLordBoss>());
        Assert.IsNotNull(cat.BossPrefabFor(2).GetComponent<Boss>());
        Assert.IsTrue(cat.BossPrefabFor(1).GetComponent<EnemyHealth>().isBoss, "보스 표시(체력바·처치 연출·보상 분기)");

        var go = Object.Instantiate(cat.BossPrefabFor(1));
        go.name = "Enemy_BlobLord";
        ChronoSavior.Systems.EnemySetup.SetupBoss(go, 1, cat);
        Assert.AreEqual(Boss.HpForRegion(1), go.GetComponent<EnemyHealth>().MaxHp, 0.5f);
        Assert.AreEqual(BlobLordBoss.VisualSize.x, go.GetComponent<SpriteRenderer>().bounds.size.x, 0.02f, "그림 크기");
        Assert.AreEqual(cat.BossPrefabFor(1).GetComponent<SpriteRenderer>().sprite, go.GetComponent<SpriteRenderer>().sprite, "공통 보스 그림으로 바뀌면 안 된다");
    }

    // ───────────────────────── 점프 ─────────────────────────

    /// <summary>명세 2절: 뜬 동안 무적, 후딜에는 맞는다. 몸통 접촉 피해는 없다(피해는 예고된 공격으로만).</summary>
    [UnityTest]
    public IEnumerator Jump_InvulnerableInAir_VulnerableInRecovery()
    {
        var target = NewTarget(9f, FieldBounds.GroundY);
        var boss = NewBoss(10f, target.transform);
        var health = boss.GetComponent<EnemyHealth>();
        var move = boss.GetComponent<EnemyMove>();

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpTrack);
        Assert.IsTrue(move.IsSpawnProtected, "공격 스크립트가 보는 '못 때림' 값");
        Assert.AreEqual(0f, health.TakeDamageWithKnockback(50f, null, 1f, 0f), 0.001f);
        Assert.Greater(boss.transform.position.y, FieldBounds.GroundY + 3f, "높이 떠 있다");

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpRecover);
        Assert.IsFalse(move.IsSpawnProtected);
        Assert.Greater(health.TakeDamageWithKnockback(50f, null, 1f, 0f), 0f, "후딜엔 반격이 들어간다");
        Assert.AreEqual(0f, boss.ContactDamageMultiplier);
    }

    /// <summary>경고가 플레이어 X를 따라가다 착지 0.3초 전에 고정 — 고정 뒤엔 플레이어가 움직여도 그 자리에 떨어진다(명세 3-1).</summary>
    [UnityTest]
    public IEnumerator Jump_MarkerFollowsThenLocks_AndBossLandsOnLockedSpot()
    {
        var target = NewTarget(9f, FieldBounds.GroundY);
        var boss = NewBoss(11f, target.transform);
        Vector2? impact = null; bool? hit = null;
        boss.Impacted += (p, h) => { impact = p; hit = h; };

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpTrack);
        target.transform.position = new Vector3(5f, target.transform.position.y, 0f);
        yield return Until(() => boss.JumpWarning.IsLocked);
        float lockedX = boss.JumpWarning.Point.x;
        Assert.AreEqual(5f, lockedX, 0.6f, "고정 전에 플레이어 쪽으로 따라왔다");

        target.transform.position = new Vector3(14f, target.transform.position.y, 0f); // 고정 뒤 피함
        yield return Until(() => impact.HasValue);
        Assert.AreEqual(lockedX, impact.Value.x, 0.01f, "고정된 자리에 떨어진다");
        Assert.AreEqual(lockedX, boss.transform.position.x, 0.1f);
        Assert.IsFalse(hit.Value, "피했으면 안 맞는다");
        Assert.AreEqual(0, target.hits);
    }

    /// <summary>경고 반경 안이면 한 번, 최대 체력의 20%(명세 15~25%). 경고 그림 반경 = 판정 반경.</summary>
    [UnityTest]
    public IEnumerator Jump_ImpactHitsOnceForPercentOfMaxHp()
    {
        var target = NewTarget(8f, FieldBounds.GroundY);
        var boss = NewBoss(11f, target.transform);
        bool impacted = false;
        boss.Impacted += (p, h) => impacted = true;

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpTrack);
        Assert.AreEqual(BlobLordBoss.ImpactRadius, boss.JumpWarning.Radius, 1e-4f);
        Assert.AreEqual(BlobLordBoss.ImpactRadius * 2f, boss.JumpWarning.GetComponent<SpriteRenderer>().bounds.size.x, 0.1f, "그림 폭 = 판정 지름");
        yield return Until(() => impacted);
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(1, target.hits);
        Assert.AreEqual(200f * BlobLordBoss.ImpactDamagePct, target.taken, 0.01f);
    }

    /// <summary>플레이어가 발판 위면 경고·착지도 발판 윗면 — 발판이 안전지대가 되지 않는다(Codex 상의).</summary>
    [UnityTest]
    public IEnumerator Jump_TargetOnPlatform_WarningAndLandingOnPlatformTop()
    {
        int i = 0; // 아래층 왼쪽 발판
        float top = FieldLayout.PlatformTopY(i);
        var target = NewTarget(FieldLayout.Platforms[i, 0], top);
        var boss = NewBoss(10f, target.transform);
        Vector2? impact = null;
        boss.Impacted += (p, h) => impact = p;

        yield return Until(() => impact.HasValue);
        Assert.AreEqual(top, impact.Value.y, 1e-3f);
        Assert.AreEqual(1, target.hits);
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(top + BlobLordBoss.HalfHeight, boss.transform.position.y, 0.05f, "발판 위에 선다");
    }

    /// <summary>맵 끝에서도 경고·착지가 맵 밖으로 안 나간다(명세 3-4 Clamp). 죽은 플레이어는 더 안 따라간다.</summary>
    [UnityTest]
    public IEnumerator Jump_ClampsAtEdge_AndStopsTrackingDeadTarget()
    {
        var target = NewTarget(FieldBounds.MinX + 0.2f, FieldBounds.GroundY);
        var boss = NewBoss(4f, target.transform);
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpTrack);
        Assert.GreaterOrEqual(boss.JumpWarning.Point.x, FieldBounds.MinX + BlobLordBoss.VisualSize.x / 2f - 1e-3f);

        target.dead = true;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.IsTrue(boss.JumpWarning.IsLocked, "죽으면 즉시 고정");
    }

    // ───────────────────────── 투척 ─────────────────────────

    /// <summary>멀면 투척(명세 5절). 조준은 1회, 경고 자리 = 돌이 떨어지는 자리, 맞으면 최대 체력 14% 한 번.</summary>
    [UnityTest]
    public IEnumerator Throw_FarTarget_WarnsLandingSpot_HitsOnceForPercent()
    {
        var target = NewTarget(3f, FieldBounds.GroundY);
        var boss = NewBoss(11f, target.transform);
        BoulderProjectile rock = null;
        boss.Thrown += b => rock = b;

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.ThrowWarn);
        Assert.AreEqual(BlobLordBoss.Pattern.Throw, boss.LastPattern);
        Assert.IsTrue(boss.ThrowWarning.Visible);
        Assert.IsTrue(boss.ThrowPathLine.enabled, "날아갈 길도 보인다");
        Assert.AreEqual(boss.ThrowTarget.x, boss.ThrowWarning.Point.x, 1e-3f);
        Assert.AreEqual(FieldBounds.GroundY, boss.ThrowTarget.y, 1e-3f);
        Assert.AreEqual(3f, boss.ThrowTarget.x, 0.4f, "플레이어 발밑 쪽");

        yield return Until(() => rock != null);
        Assert.IsFalse(boss.ThrowPathLine.enabled);
        yield return Until(() => target.hits > 0, 4f);
        Assert.AreEqual(1, target.hits);
        Assert.AreEqual(200f * BlobLordBoss.BoulderDamagePct, target.taken, 0.01f);
        yield return new WaitForSeconds(GroundWarning.FlashTime * 1.5f);
        Assert.IsFalse(boss.ThrowWarning.Visible, "맞혀도 경고가 지워진다");
    }

    /// <summary>보스가 발판 밑에 서 있어도 돌은 머리 위 발판이 아니라 플레이어 발밑까지 간다(2026-11-10 캡처에서 발견).</summary>
    [UnityTest]
    public IEnumerator Throw_FromUnderPlatform_StillTargetsPlayerFeet()
    {
        var target = NewTarget(12.5f, FieldBounds.GroundY);
        var boss = NewBoss(FieldLayout.Platforms[0, 0], target.transform);
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.ThrowWarn);
        Assert.AreEqual(FieldBounds.GroundY, boss.ThrowTarget.y, 1e-3f);
        Assert.AreEqual(12.5f, boss.ThrowTarget.x, 1e-3f);
    }

    /// <summary>
    /// 경고를 벗어나면 돌은 원래 끝점까지 가서 부서지고(유도 없음, 명세 4-4), 바로 앞의 플레이어에겐 최소 거리만큼 떨어진 곳을 노린다.
    /// 같은 패턴은 연속으로 안 쓴다(투척 → 다음은 점프).
    /// </summary>
    [UnityTest]
    public IEnumerator Throw_NoHoming_MinDistance_AndNoRepeat()
    {
        var target = NewTarget(3f, FieldBounds.GroundY);
        var boss = NewBoss(11f, target.transform);
        BoulderProjectile rock = null; bool landed = false;
        Vector2 landAt = default;
        boss.Thrown += b => { rock = b; b.Landed += r => { landed = true; landAt = r.transform.position; }; };
        yield return Until(() => rock != null);
        target.transform.position = new Vector3(15f, target.transform.position.y, 0f); // 피함
        Vector2 planned = boss.ThrowTarget;
        yield return Until(() => landed, 4f);
        Assert.AreEqual(0, target.hits);
        Assert.AreEqual(planned.x, landAt.x, 0.01f, "유도 없이 처음 정한 끝점에 떨어진다");

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.Idle);
        yield return Until(() => boss.CurrentState != BlobLordBoss.State.Idle);
        Assert.AreEqual(BlobLordBoss.Pattern.Jump, boss.LastPattern, "같은 패턴 연속 금지");


        // 근거리 보정 — 보스 바로 옆(1u)의 플레이어를 노려도 최소 거리 밖에 떨어진다(같은 쪽)
        Vector2 aim = BlobLordBoss.CorrectAim(10.5f, 1, new Vector2(9.5f, FieldBounds.GroundY));
        Assert.AreEqual(10.5f - BlobLordBoss.MinThrowDistance, aim.x, 1e-3f);
    }
}
}
