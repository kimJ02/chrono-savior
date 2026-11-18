using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Characters;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 2지역 보스 부서진 여신상(`Enemies/BrokenGoddessBoss`, 사용자 2026-11-18 요청서) — CLAUDE.md "테스트"
/// 규칙대로 Rigidbody2D/Collider2D가 얽힌 로직(잡기·휘두르기 판정)을 PlayMode에서 검증한다.
///
/// **핵심 패턴 로직(위 대부분의 테스트)은 1지역 `BlobLordBossTests`와 달리 프리팹을 쓰지 않는다** — 판정·
/// 상태머신은 `AddComponent`로 직접 세운 보스로도 충분히 검증되고, 프리팹 의존을 줄이면 그만큼 빨리 돈다.
/// `Enemy_BrokenGoddess` 프리팹 자체는 이미 있다(`isBoss` 표시·`warningPrefab` 연결 — 2026-11-17/18 커밋) —
/// 다만 **몸 그림 연결은 에디터 메뉴(`ChronoSavior/보스 그림 연결/부서진 여신상`)를 돌려야** 반영된다
/// (<see cref="BodyArt_IsWiredOnThePrefab"/>는 그 메뉴를 돌리기 전까진 실패하는 게 정상 — PROGRESS.md 참고).
/// </summary>
public class BrokenGoddessBossTests
{
    const string PrefabPath = "Assets/Prefabs/Enemies/Enemy_BrokenGoddess.prefab";

    static GameObject LoadPrefab()
    {
        var p = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.IsNotNull(p, "부서진 여신상 프리팹이 없다: " + PrefabPath);
        return p;
    }

    /// <summary>맞는 쪽 — 플레이어 대신(붙잡기가 필요 없는 테스트용, 휘두르기 등).</summary>
    class Dummy : MonoBehaviour, IDamageable, IHealthView
    {
        public float maxHp = 100f;
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
        savedTimeScale = Time.timeScale;
        Time.timeScale = 4f; // 패턴 한 바퀴가 몇 초쯤 — 빨리 감는다(물리 프레임 수는 같다, BlobLordBossTests와 같은 방식)

        // 이 보스는 BlobLordBoss와 달리 Y를 직접 정하지 않고(IEnemyVerticalMotion 미구현) EnemyMove의
        // 기본 중력·착지에 맡긴다 — 실제 게임에선 늘 발판/바닥 위에 선다. 이 테스트 씬엔 바닥이 없어서
        // 그냥 두면 보스가 계속 떨어져 몇 초 뒤 잡기 판정 높이·사거리를 완전히 벗어난다(테스트 의도와
        // 무관한 실패). 그래서 평평한 바닥 하나를 직접 만들어 둔다.
        var ground = new GameObject("TestGround", typeof(BoxCollider2D));
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.transform.position = new Vector3(13f, -0.1f, 0f);
        ground.GetComponent<BoxCollider2D>().size = new Vector2(60f, 0.2f);
    }

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = savedTimeScale;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Test"))
                Object.DestroyImmediate(go);
        Reset();
    }

    static void Reset()
    {
        GameInput.PlayerGrabbed = false;
    }

    /// <summary>
    /// 프리팹 없이 — 필요한 컴포넌트를 직접 붙여 세운다(위 클래스 설명 참고).
    /// **비활성 상태에서 전부 붙이고 마지막에 켠다** — `new GameObject(name, typeof(A), typeof(B), ...)`로
    /// 한 번에 붙이면(활성 오브젝트라) 각 타입이 붙는 순간 바로 `Awake()`가 돈다. `EnemyMove.Awake()`가
    /// `CacheTypeBehaviour()`에서 `GetComponent&lt;IEnemyMotion&gt;()`로 같은 오브젝트의 `BrokenGoddessBoss`를
    /// 찾는데, 목록에서 `EnemyMove`가 `BrokenGoddessBoss`보다 앞에 있으면 그 시점엔 아직 안 붙어 있어 null로
    /// 캐시되고 — 그러면 `BrokenGoddessBoss.GetHorizontalSpeed`가 영영 안 불려서 상태머신이 Idle에서
    /// 멈춘다(실제로 테스트 5개가 전부 이 이유로 타임아웃났다, 2026-11-18). 실제 게임의 프리팹 경로는
    /// 인스턴스화 때 컴포넌트가 전부 먼저 붙은 뒤에 Awake가 도므로 이 문제가 없다 — **테스트 전용 함정**.
    /// 비활성 상태로 다 붙인 뒤 `SetActive(true)`로 한꺼번에 깨우면, 그때는 전부 이미 붙어 있어 순서와
    /// 무관하게 안전하다.
    /// </summary>
    static BrokenGoddessBoss NewBoss(float x, Transform target)
    {
        var go = new GameObject("TestGoddess");
        go.SetActive(false);
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<CircleCollider2D>();
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<EnemyMove>();
        go.AddComponent<EnemyHealth>();
        var boss = go.AddComponent<BrokenGoddessBoss>();

        go.transform.position = new Vector3(x, 0.25f, 0f); // 바닥(y=0) 위, 반지름만큼 띄워서 시작(실제 스폰과 같은 방식)
        go.GetComponent<CircleCollider2D>().radius = 0.25f;
        var move = go.GetComponent<EnemyMove>();
        move.SetSpawnProtection(0f);
        move.Target = target;
        go.GetComponent<EnemyHealth>().SetMaxHp(1000f);

        go.SetActive(true); // 여기서 전부의 Awake가 한꺼번에 돈다 — 이미 서로 다 붙어 있어 순서 무관
        return boss;
    }

    static Dummy NewTarget(float x)
    {
        var go = new GameObject("TestTarget");
        go.tag = "Player";
        go.transform.position = new Vector3(x, 0f, 0f);
        var rb = go.AddComponent<Rigidbody2D>(); rb.bodyType = RigidbodyType2D.Kinematic;
        go.AddComponent<CircleCollider2D>().radius = 0.25f;
        return go.AddComponent<Dummy>();
    }

    /// <summary>
    /// 잡기 패턴을 실제로 받는 쪽 — IGrabbable까지 진짜 `CharacterMover2D`로 검증한다(가짜로 대체하지 않음).
    /// 몸은 Kinematic — `CharacterMover2D`는 중력·착지를 실제 발판이 있어야 다루는데 이 테스트엔 발판이
    /// 없다(BlobLordBossTests의 `NewTarget`과 같은 이유로 Kinematic을 쓴다 — 중력으로 계속 떨어지면
    /// 몇 초 뒤 잡기 판정 높이를 벗어나 테스트가 의도와 무관하게 깨진다).
    /// </summary>
    static CharacterMover2D NewGrabbablePlayer(float x, out Dummy damageable)
    {
        var go = new GameObject("TestGrabbablePlayer", typeof(Rigidbody2D), typeof(CircleCollider2D));
        go.tag = "Player";
        go.transform.position = new Vector3(x, 0f, 0f);
        go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        go.GetComponent<CircleCollider2D>().radius = 0.25f;
        damageable = go.AddComponent<Dummy>();
        return go.AddComponent<CharacterMover2D>();
    }

    static IEnumerator Until(System.Func<bool> cond, float maxSeconds = 10f)
    {
        float t = 0f;
        while (!cond() && t < maxSeconds) { yield return new WaitForFixedUpdate(); t += Time.fixedDeltaTime; }
        Assert.IsTrue(cond(), "시간 안에 조건이 안 됐다");
    }

    // ───────────────────────── 판정 기하(순수 함수) ─────────────────────────

    [Test]
    public void InGrabBox_FrontWithinRangeAndHeight_HitsOnlyThere()
    {
        Vector2 origin = Vector2.zero;
        Assert.IsTrue(BrokenGoddessBoss.InGrabBox(origin, 1, BrokenGoddessBoss.GrabRange, BrokenGoddessBoss.GrabHeight, new Vector2(2f, 0.5f)), "전방·사거리·높이 안");
        Assert.IsFalse(BrokenGoddessBoss.InGrabBox(origin, 1, BrokenGoddessBoss.GrabRange, BrokenGoddessBoss.GrabHeight, new Vector2(-1f, 0f)), "보는 방향 반대쪽(뒤)");
        Assert.IsFalse(BrokenGoddessBoss.InGrabBox(origin, 1, BrokenGoddessBoss.GrabRange, BrokenGoddessBoss.GrabHeight, new Vector2(BrokenGoddessBoss.GrabRange + 0.5f, 0f)), "사거리 밖");
        Assert.IsFalse(BrokenGoddessBoss.InGrabBox(origin, 1, BrokenGoddessBoss.GrabRange, BrokenGoddessBoss.GrabHeight, new Vector2(1f, BrokenGoddessBoss.GrabHeight)), "판정 높이 밖");
        // 반대로 보고 있으면(facing -1) 왼쪽이 전방이 된다.
        Assert.IsTrue(BrokenGoddessBoss.InGrabBox(origin, -1, BrokenGoddessBoss.GrabRange, BrokenGoddessBoss.GrabHeight, new Vector2(-2f, 0f)));
    }

    [Test]
    public void InSweepArea_FrontArcWithinRadius_HitsOnlyThere()
    {
        Vector2 origin = Vector2.zero;
        Assert.IsTrue(BrokenGoddessBoss.InSweepArea(origin, 1, BrokenGoddessBoss.SweepRadius, BrokenGoddessBoss.SweepHalfAngleDeg, new Vector2(2f, 1f)), "전방 부채꼴 안");
        Assert.IsFalse(BrokenGoddessBoss.InSweepArea(origin, 1, BrokenGoddessBoss.SweepRadius, BrokenGoddessBoss.SweepHalfAngleDeg, new Vector2(-2f, 0f)), "정반대 뒤쪽은 160도 밖(요청서: 보스 뒤로 빠지면 안 맞음)");
        Assert.IsFalse(BrokenGoddessBoss.InSweepArea(origin, 1, BrokenGoddessBoss.SweepRadius, BrokenGoddessBoss.SweepHalfAngleDeg, new Vector2(BrokenGoddessBoss.SweepRadius + 1f, 0f)), "반경 밖");
    }

    // ───────────────────────── 잡기 ─────────────────────────

    /// <summary>요청서 2절 전체 흐름 — 사거리 안에서 성공, 끌려오고, 한 번만 피해(최대 체력 20%), 끝나면 풀려난다.</summary>
    [UnityTest]
    public IEnumerator Grab_SuccessPullsAndDamagesOncePercentOfMaxHp()
    {
        var player = NewGrabbablePlayer(8f, out Dummy damageable);
        damageable.maxHp = 200f;
        var boss = NewBoss(9f, player.transform); // 사거리(3유닛) 안

        yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.GrabThrust || boss.CurrentState == BrokenGoddessBoss.State.SweepActive, 6f);
        // 둘 중 뭐가 먼저 골렸을 수도 있으니(랜덤) 잡기가 아니면 이번엔 건너뛰지 않고 다음 선택까지 기다린다.
        if (boss.CurrentState != BrokenGoddessBoss.State.GrabThrust)
            yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.GrabThrust, 8f);

        // 먼저 휘두르기가 골랐다 치면 그 한 번은 실제로 맞아서(사거리 안이라) damageable에 이미 타격이 쌓여 있을 수
        // 있다(2026-11-18 실제로 이 경우로 깨짐 — "한 번만 맞아야 한다"가 휘두르기 몫까지 세고 있었다). 그래서
        // 지금부터 보는 "이번 잡기 한 번"만 비교하려고 여기서 기준점을 찍는다 — 우연히 먼저 골린 휘두르기는
        // 이 테스트가 보는 범위 밖(그건 Sweep 쪽 테스트가 따로 검증).
        int hitsBefore = damageable.hits;
        float takenBefore = damageable.taken;

        yield return Until(() => player.IsGrabbed, 2f);
        Assert.IsTrue(GameInput.PlayerGrabbed, "붙잡힌 동안은 점프·공격·스킬도 잠긴다");

        yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.GrabRecover, 4f);
        Assert.AreEqual(1, damageable.hits - hitsBefore, "이번 잡기로는 한 번만 맞아야 한다");
        Assert.AreEqual(200f * BrokenGoddessBoss.GrabDamagePct, damageable.taken - takenBefore, 0.01f);
        Assert.IsFalse(player.IsGrabbed, "끝나면 풀려난다");
        Assert.IsFalse(GameInput.PlayerGrabbed, "풀려나면 입력 잠금도 같이 풀린다");
    }

    /// <summary>요청서 2절 "잡기에 실패하면 피해를 주지 않는다 / 바로 후딜레이로" — 판정이 열리기 전에 플레이어가 멀어지면 실패.</summary>
    [UnityTest]
    public IEnumerator Grab_FailsWhenTargetLeavesRangeBeforeThrust_NoDamageNoPull()
    {
        var player = NewGrabbablePlayer(8f, out Dummy damageable);
        var boss = NewBoss(9f, player.transform);

        bool wasGrabPrep = false;
        yield return Until(() =>
        {
            if (boss.CurrentState == BrokenGoddessBoss.State.GrabPrep) wasGrabPrep = true;
            return wasGrabPrep || boss.CurrentState == BrokenGoddessBoss.State.SweepActive;
        }, 6f);
        if (!wasGrabPrep)
        {
            // 처음에 휘두르기가 골렸으면 다음 번엔 쿨 때문에 반드시 잡기가 골린다(Choose의 쿨+연속제한 상호작용).
            yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.GrabPrep, 8f);
        }

        // 먼저 휘두르기가 골랐다면 사거리 안이라 실제로 맞았을 수 있다(위 성공 테스트와 같은 이유, 2026-11-18) —
        // 그건 이번에 보는 "이번 잡기 시도"와 무관하니 여기서 기준점을 찍어 그 이후 변화만 본다.
        int hitsBefore = damageable.hits;

        // 준비·경고가 끝나기 전에 플레이어를 사거리(3유닛) 밖으로 뺀다 — 팔을 뻗었을 때 아무도 없게.
        player.transform.position = new Vector3(player.transform.position.x + 10f, 0f, 0f);
        player.GetComponent<Rigidbody2D>().position = player.transform.position; // Kinematic도 rb.position을 직접 맞춰 둬야 바로 반영된다

        yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.GrabRecover, 4f);
        Assert.AreEqual(hitsBefore, damageable.hits, "이번 잡기 시도는 실패했으니 피해가 늘면 안 된다");
        Assert.IsFalse(player.IsGrabbed, "애초에 붙잡히지 않았다");
    }

    /// <summary>
    /// 2026-11-18, 사용자가 실제 플레이로 발견: 잡기는 성공(끌려옴·풀려남)했는데 체력이 안 깎이는 것처럼
    /// 보였던 버그 — 끌어당기기(0.5초)+붙잡아둠(0.2초) 동안 다른 몹에게 맞아 생긴 플레이어 무적(기본
    /// 0.9초)이 아직 안 끝났으면 `PlayerHealth.TakeDamage`가 조용히 무시했다. 이 테스트는 `Dummy` 대신
    /// 실제 `PlayerHealth`를 붙여서, 잡기 시작 전에 긴 무적을 미리 걸어 둬도 끝날 때 체력이 깎이는지
    /// (= `TakeDamageIgnoringInvuln` 경로를 타는지) 확인한다.
    /// </summary>
    [UnityTest]
    public IEnumerator Grab_DamageIgnoresPlayerInvulnerability()
    {
        var go = new GameObject("TestGrabbablePlayerWithRealHealth", typeof(Rigidbody2D), typeof(CircleCollider2D));
        go.tag = "Player";
        go.transform.position = new Vector3(8f, 0f, 0f);
        go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic; // NewGrabbablePlayer와 같은 이유 — 이 테스트엔 발판이 없다
        go.GetComponent<CircleCollider2D>().radius = 0.25f;
        var mover = go.AddComponent<CharacterMover2D>();
        var health = go.AddComponent<PlayerHealth>();

        var boss = NewBoss(9f, go.transform);

        // 근처 다른 몹에게 막 맞은 것처럼 — 테스트 전체 길이보다 긴 무적을 미리 걸어 둔다.
        health.GrantInvuln(999f);

        yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.GrabThrust || boss.CurrentState == BrokenGoddessBoss.State.SweepActive, 6f);
        if (boss.CurrentState != BrokenGoddessBoss.State.GrabThrust)
            yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.GrabThrust, 8f); // 먼저 휘두르기가 골랐으면(무적이라 안 맞음) 잡기를 한 번 더 기다린다

        float hpBefore = health.CurrentHp;
        yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.GrabRecover, 4f);
        Assert.Less(health.CurrentHp, hpBefore, "플레이어가 무적 중이어도 잡기는 피해를 줘야 한다");
        Assert.Greater(health.InvulnRemaining, 0f, "원래 걸려 있던 무적 자체를 없애는 게 아니다 — 무적 '검사'만 이 피해에서 건너뛴다");
    }

    // ───────────────────────── 휘두르기 ─────────────────────────

    /// <summary>요청서 3절 — 경고 범위(전방 160도) 밖이면(보스 뒤로 빠짐) 안 맞는다. 범위 안이면 1회 피해(최대 체력 15%).</summary>
    [UnityTest]
    public IEnumerator Sweep_MissesWhenTargetMovesBehindAfterDirectionLocked()
    {
        var target = NewTarget(8f);
        target.maxHp = 200f;
        var boss = NewBoss(9f, target.transform); // 보스가 왼쪽(target)을 보도록 방향이 고정된다

        bool wasSweepPrep = false;
        yield return Until(() =>
        {
            if (boss.CurrentState == BrokenGoddessBoss.State.SweepPrep) wasSweepPrep = true;
            return wasSweepPrep || boss.CurrentState == BrokenGoddessBoss.State.GrabThrust;
        }, 6f);
        if (!wasSweepPrep)
            yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.SweepPrep, 8f);

        // 방향이 고정된 뒤(준비 중) 플레이어를 보스 반대편(뒤쪽)으로 옮긴다 — "추적 판정을 추가하지 않는다".
        // +2(반경 3유닛 안 — 거리로는 맞을 수 있었지만 보스가 보는 방향 반대쪽이라 각도로 걸러져야 한다).
        yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.SweepWarn, 2f);
        target.transform.position = new Vector3(boss.transform.position.x + 2f, target.transform.position.y, 0f);

        yield return Until(() => boss.CurrentState == BrokenGoddessBoss.State.SweepRecover, 4f);
        Assert.AreEqual(0, target.hits, "보스 뒤로 빠졌으니 안 맞아야 한다");
    }

    // ───────────────────────── 고르기 ─────────────────────────

    /// <summary>요청서 4절 "같은 패턴이 지나치게 반복되지 않도록" — 쓴 패턴은 자기 쿨타임에 걸려 다음 번엔 반드시 다른 패턴이 골린다.</summary>
    [UnityTest]
    public IEnumerator Choose_SecondPickAlwaysDiffersFromFirst()
    {
        var target = NewTarget(8f);
        var boss = NewBoss(9f, target.transform);

        yield return Until(() => boss.LastPattern != BrokenGoddessBoss.Pattern.None, 6f);
        var first = boss.LastPattern;

        // 두 번째 선택이 일어나는 순간(=Idle을 벗어나는 순간) LastPattern이 바로 바뀐다 — Idle로 돌아올
        // 때까지 기다리면 그 변화의 순간을 놓친다.
        yield return Until(() => boss.LastPattern != first, 8f);
        Assert.AreNotEqual(first, boss.LastPattern, "먼저 쓴 패턴은 쿨타임에 걸려 바로 다음엔 못 쓴다");
    }

    // ───────────────────────── 예외 처리 ─────────────────────────

    /// <summary>요청서 "보스가 사망하거나 공격이 중단되면 잡힌 플레이어를 즉시 해제한다" — 비활성화만으로도 풀려야 한다.</summary>
    [UnityTest]
    public IEnumerator BossDisabled_ReleasesGrabbedTargetImmediately()
    {
        var player = NewGrabbablePlayer(8f, out Dummy damageable);
        var boss = NewBoss(9f, player.transform);

        bool grabStarted = false;
        yield return Until(() =>
        {
            if (player.IsGrabbed) grabStarted = true;
            return grabStarted || boss.CurrentState == BrokenGoddessBoss.State.SweepActive;
        }, 10f);

        if (!grabStarted)
        {
            // 먼저 휘두르기가 골렸으면 한 바퀴 더 기다려 잡기를 받는다(쿨타임 상호작용으로 다음 번엔 반드시 잡기).
            yield return Until(() => player.IsGrabbed, 10f);
        }

        Assert.IsTrue(player.IsGrabbed);
        boss.gameObject.SetActive(false); // 파괴 대신 비활성화만 — OnDisable 경로를 직접 검증

        Assert.IsFalse(player.IsGrabbed, "보스가 꺼지면 즉시 풀려나야 한다");
        Assert.IsFalse(GameInput.PlayerGrabbed);
    }

    // ───────────────────────── 그림/애니메이션(2026-11-18) ─────────────────────────

    /// <summary>
    /// 몸 그림은 프리팹 칸의 에셋(BlobLordBossTests.BodyArt_IsWiredOnThePrefab과 같은 패턴). 동작별 장 수는
    /// 실제로 받은 스프라이트 그대로(`BrokenGoddessBoss.cs` 헤더 주석). **메뉴
    /// `ChronoSavior/보스 그림 연결/부서진 여신상`을 아직 안 돌렸다면 이 테스트는 실패하는 게 정상이다**
    /// (이 세션은 Unity 에디터가 없어 그 메뉴를 대신 돌릴 수 없었다 — PROGRESS.md "확인 필요" 참고).
    /// </summary>
    [Test]
    public void BodyArt_IsWiredOnThePrefab()
    {
        var boss = LoadPrefab().GetComponent<BrokenGoddessBoss>();
        foreach (var (name, book, count) in new[]
                 { ("대기", boss.IdleArt, 4), ("이동", boss.MoveArt, 4), ("잡기", boss.GrabArt, 7),
                   ("휘두르기", boss.SweepArt, 5), ("피격", boss.HitArt, 1), ("사망", boss.DeathArt, 8) })
        {
            Assert.IsNotNull(book, name + " 칸이 비었다 — 메뉴 ChronoSavior/보스 그림 연결/부서진 여신상");
            Assert.AreEqual(count, book.Count, name + " 장 수");
            foreach (var f in book.frames) Assert.IsNotNull(f, name + " 빈 프레임");
        }
    }

    /// <summary>
    /// 사망 연출 동안 콜라이더를 끈다(BlobLordBoss.Death_TurnsCollidersOff_WhileDeathArtPlays와 같은 이유 —
    /// 레이저·유도 대상 고르기가 시체를 겨누지 않게). 그림 에셋 유무와 무관하게 지금 바로 통과해야 한다
    /// (deathArt가 비어 있어도 PlayDeathAndDestroy의 콜라이더 끄기 자체는 그림과 상관없는 로직).
    /// </summary>
    [UnityTest]
    public IEnumerator Death_TurnsCollidersOff_WhileDeathArtPlays()
    {
        var boss = NewBoss(10f, null);
        Assert.IsTrue(System.Array.TrueForAll(boss.GetComponents<Collider2D>(), c => c.enabled), "살아 있을 땐 켜져 있다");
        boss.PlayDeathAndDestroy();
        Assert.IsTrue(System.Array.TrueForAll(boss.GetComponents<Collider2D>(), c => !c.enabled), "죽는 순간 끈다");
        yield return null;
    }

    /// <summary>붙잡고 있는 도중 죽으면(사망 연출 시작) 즉시 풀려난다 — OnDisable이 아니라도(오브젝트는 아직 켜져 있다) 보장돼야 한다.</summary>
    [UnityTest]
    public IEnumerator Death_ReleasesGrabbedTargetImmediately()
    {
        var player = NewGrabbablePlayer(8f, out Dummy damageable);
        var boss = NewBoss(9f, player.transform);

        bool grabStarted = false;
        yield return Until(() =>
        {
            if (player.IsGrabbed) grabStarted = true;
            return grabStarted || boss.CurrentState == BrokenGoddessBoss.State.SweepActive;
        }, 10f);
        if (!grabStarted) yield return Until(() => player.IsGrabbed, 10f);

        Assert.IsTrue(player.IsGrabbed);
        boss.PlayDeathAndDestroy();
        Assert.IsFalse(player.IsGrabbed, "사망 연출이 시작되면 즉시 풀려나야 한다");
    }
}
}
