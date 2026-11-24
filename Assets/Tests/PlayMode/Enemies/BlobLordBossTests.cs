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

    /// <summary>1지역은 덩어리 지배자, 2지역은 부서진 여신상(2026-11-18), 전용이 없는 지역(3~)은 원본 공통 보스. 공통 보스 체력 곡선(1지역 800)은 그대로.</summary>
    [Test]
    public void Catalog_Region1UsesBlobLord_OthersKeepCommonBoss()
    {
        var cat = UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterCatalog>(CatalogPath);
        Assert.IsNotNull(cat.BossPrefabFor(1).GetComponent<BlobLordBoss>());
        Assert.IsNotNull(cat.BossPrefabFor(2).GetComponent<BrokenGoddessBoss>(), "2지역 전용 보스 = 부서진 여신상");
        Assert.IsNotNull(cat.BossPrefabFor(3).GetComponent<Boss>(), "전용 보스가 없는 지역은 공통 보스");
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
        Assert.Greater(boss.ContactDamageMultiplier, 0f, "땅에선 몸통 접촉 피해가 있다(2026-11-24 보스 강화 — 예전엔 0)");
    }

    // ───────────────────────── 보스 강화(2026-11-24) ─────────────────────────

    /// <summary>몸통 접촉 피해 = 대상 최대 체력 8%(`attackPower × 배수`가 그 값이 되게), 공중에 뜬 동안은 0.</summary>
    [UnityTest]
    public IEnumerator Contact_IsPercentOfTargetMaxHp_AndZeroInAir()
    {
        var target = NewTarget(9f, FieldBounds.GroundY);
        target.maxHp = 200f;
        var boss = NewBoss(10f, target.transform);
        var move = boss.GetComponent<EnemyMove>();
        move.attackPower = 30f;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        Assert.AreEqual(200f * BlobLordBoss.ContactDamagePct, move.attackPower * boss.ContactDamageMultiplier, 0.01f, "땅 = 최대 체력 8%");
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpTrack);
        Assert.AreEqual(0f, boss.ContactDamageMultiplier, "공중에선 몸통 피해 없음");
    }

    /// <summary>체력 50% 이하면 추적 점프 강하가 3번 연속(사이에 땅에 잠깐) — 마지막 착지 뒤에만 후딜. 50% 위면 한 번.</summary>
    [UnityTest]
    public IEnumerator Enraged_JumpBecomesThreeChainedJumps()
    {
        var target = NewTarget(9f, FieldBounds.GroundY);
        var boss = NewBoss(10f, target.transform);
        var health = boss.GetComponent<EnemyHealth>();
        health.TakeDamageWithKnockback(health.MaxHp * 0.6f, null, 1f, 0f); // 체력 40%
        Assert.IsTrue(boss.IsEnraged, "체력 50% 이하 = 분노");

        int impacts = 0;
        bool sawGap = false;
        boss.Impacted += (p, hit) => impacts++;
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpPrep, 6f);
        Assert.AreEqual(BlobLordBoss.ChainJumps, boss.JumpsLeft, "저체력 → 점프 강하를 3번 이어서");
        yield return Until(() =>
        {
            if (boss.CurrentState == BlobLordBoss.State.ChainGap) sawGap = true;
            return boss.CurrentState == BlobLordBoss.State.JumpRecover;
        }, 12f);
        Assert.AreEqual(BlobLordBoss.ChainJumps, impacts, "후딜 전에 착지 3번");
        Assert.IsTrue(sawGap, "착지 사이엔 땅에서 잠깐(맞을 수 있는 틈)");
        Assert.AreEqual(0, boss.JumpsLeft);
    }

    /// <summary>체력 50% 위에선 예전처럼 한 번 뛰고 바로 후딜.</summary>
    [UnityTest]
    public IEnumerator NotEnraged_SingleJump()
    {
        var target = NewTarget(9f, FieldBounds.GroundY);
        var boss = NewBoss(10f, target.transform);
        Assert.IsFalse(boss.IsEnraged);
        int impacts = 0;
        boss.Impacted += (p, hit) => impacts++;
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpPrep, 6f);
        Assert.AreEqual(1, boss.JumpsLeft);
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpRecover, 8f);
        Assert.AreEqual(1, impacts, "한 번만 착지");
    }

    /// <summary>
    /// 조약돌 연속 투척(2026-11-24 사용자) — 돌 3개(분노면 5개)를 0.4초 간격으로 보스 근처에 만들어 차례로 던진다.
    /// 돌은 보스 몸 근처에서 출발하고, 던지는 간격은 만드는 간격과 같다.
    /// </summary>
    [UnityTest]
    public IEnumerator Throw_Volley_ThreeStonesStaggeredFromNearBoss()
    {
        var target = NewTarget(3f, FieldBounds.GroundY);
        var boss = NewBoss(11f, target.transform);
        var times = new System.Collections.Generic.List<float>();
        var starts = new System.Collections.Generic.List<Vector2>();
        boss.Thrown += b => { times.Add(Time.time); starts.Add(b.transform.position); };

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.ThrowWarn);
        Assert.AreEqual(BlobLordBoss.VolleyCount, boss.VolleySize);
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.ThrowRecover, 6f);
        Assert.AreEqual(BlobLordBoss.VolleyCount, times.Count, "돌 3개");
        for (int i = 1; i < times.Count; i++)
            Assert.AreEqual(BlobLordBoss.VolleyInterval, times[i] - times[i - 1], 0.08f, "시간차로 하나씩");
        foreach (var p in starts)
            Assert.Less(Mathf.Abs(p.x - boss.transform.position.x), 2f, "보스 근처에서 출발");
    }

    /// <summary>분노(체력 50% 이하)면 돌이 5개.</summary>
    [UnityTest]
    public IEnumerator Throw_Volley_EnragedThrowsFive()
    {
        var target = NewTarget(3f, FieldBounds.GroundY);
        var boss = NewBoss(11f, target.transform);
        var health = boss.GetComponent<EnemyHealth>();
        health.TakeDamageWithKnockback(health.MaxHp * 0.6f, null, 1f, 0f);
        int thrown = 0;
        boss.Thrown += b => thrown++;
        // 분노면 점프가 우선이라 투척이 나올 때까지 기다린다
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.ThrowWarn, 16f);
        Assert.AreEqual(BlobLordBoss.VolleyCountEnraged, boss.VolleySize);
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.ThrowRecover, 6f);
        Assert.AreEqual(BlobLordBoss.VolleyCountEnraged, thrown);
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
        int hitsBefore = 0, hitsAtImpact = 0;
        float takenBefore = 0f, takenAtImpact = 0f;
        boss.Impacted += (p, h) => { impacted = true; hitsAtImpact = target.hits; takenAtImpact = target.taken; };

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.JumpTrack);
        Assert.AreEqual(BlobLordBoss.ImpactRadius, boss.JumpWarning.Radius, 1e-4f);
        Assert.AreEqual(BlobLordBoss.ImpactRadius * 2f, boss.JumpWarning.GetComponent<SpriteRenderer>().bounds.size.x, 0.1f, "그림 폭 = 판정 지름");
        // 2026-11-24 보스 강화 — 땅에선 몸통 접촉 피해가 있다. 이 가짜 대상은 맞은 뒤 무적이 없어 착지 뒤 접촉이 매 프레임 쌓이므로,
        // 내려찍기 한 번의 몫만 보려고 착지 순간(Impacted, 피해 직후에 불린다)에 센다. 뜬 동안엔 접촉 피해가 없어 그 전 값이 기준.
        hitsBefore = target.hits;
        takenBefore = target.taken;
        yield return Until(() => impacted);
        Assert.AreEqual(1, hitsAtImpact - hitsBefore, "내려찍기는 한 번");
        Assert.AreEqual(200f * BlobLordBoss.ImpactDamagePct, takenAtImpact - takenBefore, 0.01f);
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
        Assert.AreEqual(top + BlobLordBoss.BodyCenterHeight, boss.transform.position.y, 0.05f, "발판 위에 선다(그림 바닥 투명 여백 6도트를 뺀 높이 — 안 빼면 공중에 떠 보인다, 2026-11-22)");
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
        Assert.IsFalse(boss.ThrowPathLine.enabled, "첫 돌의 길은 던지면 지워진다");
        // 2026-11-24 연속 투척 — 서 있는 대상에겐 돌 3개가 하나씩 다 맞는다(돌마다 최대 체력 10%)
        yield return Until(() => boss.CurrentState == BlobLordBoss.State.ThrowRecover, 6f);
        yield return Until(() => target.hits >= BlobLordBoss.VolleyCount, 4f);
        Assert.AreEqual(BlobLordBoss.VolleyCount, target.hits, "돌마다 한 번");
        Assert.AreEqual(200f * BlobLordBoss.BoulderDamagePct * BlobLordBoss.VolleyCount, target.taken, 0.01f);
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
        BoulderProjectile first = null;
        boss.Thrown += b => { if (first == null) first = b; };
        yield return Until(() => rock != null);
        target.transform.position = new Vector3(15f, target.transform.position.y, 0f); // 피함
        Vector2 planned = boss.ThrowTarget; // 첫 돌의 끝점(만들어질 때 고정)
        // (2026-11-24 연속 투척 — 뒤에 만들어지는 돌은 그 순간의 플레이어를 새로 노리므로, 여기선 첫 돌만 본다)
        bool firstLanded = false; Vector2 firstAt = default;
        first.Landed += r => { firstLanded = true; firstAt = r.transform.position; };
        yield return Until(() => firstLanded, 4f);
        Assert.AreEqual(planned.x, firstAt.x, 0.01f, "유도 없이 처음 정한 끝점에 떨어진다");

        yield return Until(() => boss.CurrentState == BlobLordBoss.State.Idle);
        yield return Until(() => boss.CurrentState != BlobLordBoss.State.Idle);
        Assert.AreEqual(BlobLordBoss.Pattern.Jump, boss.LastPattern, "같은 패턴 연속 금지");


        // 근거리 보정 — 보스 바로 옆(1u)의 플레이어를 노려도 최소 거리 밖에 떨어진다(같은 쪽)
        Vector2 aim = BlobLordBoss.CorrectAim(10.5f, 1, new Vector2(9.5f, FieldBounds.GroundY));
        Assert.AreEqual(10.5f - BlobLordBoss.MinThrowDistance, aim.x, 1e-3f);
    }

    /// <summary>몸 그림은 프리팹 칸의 에셋(2026-11-17 — 예전엔 경로 문자열 로드). 동작별 장 수는 팀원 그림 그대로.</summary>
    [Test]
    public void BodyArt_IsWiredOnThePrefab()
    {
        var boss = LoadPrefab().GetComponent<BlobLordBoss>();
        foreach (var (name, book, count) in new[] { ("튀어오르기", boss.BounceArt, 6), ("투척", boss.ThrowArt, 4), ("이동", boss.MoveArt, 4), ("피격", boss.HitArt, 1), ("사망", boss.DeathArt, 8) })
        {
            Assert.IsNotNull(book, name + " 칸이 비었다 — 메뉴 ChronoSavior/몬스터 지역 그림 연결");
            Assert.AreEqual(count, book.Count, name + " 장 수");
            foreach (var f in book.frames) Assert.IsNotNull(f, name + " 빈 프레임");
        }
    }

    /// <summary>사망 연출(1초) 동안 콜라이더를 끈다 — 레이저·유도 같은 대상 고르기가 시체를 겨누지 않게(2026-11-17).</summary>
    [Test]
    public void Death_TurnsCollidersOff_WhileDeathArtPlays()
    {
        var boss = NewBoss(10f, null);
        Assert.IsTrue(System.Array.TrueForAll(boss.GetComponents<Collider2D>(), c => c.enabled), "살아 있을 땐 켜져 있다");
        boss.PlayDeathAndDestroy();
        Assert.IsTrue(System.Array.TrueForAll(boss.GetComponents<Collider2D>(), c => !c.enabled), "죽는 순간 끈다");
        Assert.IsNotNull(boss, "사망 그림이 도는 동안은 아직 남아 있다");
    }
}
}
