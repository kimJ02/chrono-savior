using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Enemies
{
/// <summary>
/// Part B (feature/monster-combat) - 몬스터 이동 AI. docs/sprints/01-combat-core.md 2번 스탯 범위(몹 1종=고블린 기준)를
/// 원본 updateEnemies()의 "고블린·슬라임·꼬마 슬라임" 분기(기본 보행 AI)를 그대로 이식해서 구현한다 —
/// 이건 프로토타입이 아니라 실제 구현이므로 세부 동작까지 원본과 동일하게 맞춘다. 아래 각 동작은
/// 전부 원본 코드의 특정 줄에 대응한다(주석에 표시).
///
/// - **추적/배회**: `Math.abs(dx) < 300`(3유닛) 안이면 플레이어 쪽으로 방향만 맞추고 계속 걷는다.
///   범위 밖이면 무작위 1.5~3.5초마다 방향을 뒤집으며 배회한다(`e.wanderT`). "접촉하면 멈춘다"는
///   동작이 원본엔 없다 — 계속 걷다가 겹치면 매 프레임 접촉 판정만 별도로 일어난다.
/// - **중력/착지**: `e.vy += gravity*dt; e.y += e.vy*dt` — 플레이어와 동일한 진짜 물리(Rigidbody2D).
/// - **발판 가장자리 반전**: 발판 위(바닥이 아님)에 서 있고 가장자리 14px 안쪽에 닿으면 그 자리에서
///   멈추고 안쪽으로 방향을 뒤집는다 — **발판 위 몹은 걸어서 떨어지지 않는다**(원본 주석 그대로:
///   "발판 위 몹은 가장자리에서 되돌아간다 — 내려오지 않는다"). 이전 버전은 이 로직이 없어서
///   그냥 물리로 굴러떨어지게만 뒀는데, 원본과 다른 동작이었다.
/// - **스폰 무적**: 원본 `CONFIG.run.spawnProtect: 2.0`(`spawnInvuln`) — 스폰 후 2초간
///   모든 피해 판정 함수가 대상을 건너뛴다. `IsSpawnProtected`로 노출, 공격 스크립트가 확인한다.
/// - **접촉 데미지**: 원본은 매 프레임 `rectsOverlap` 판정 후 `damagePlayer()` 호출 — 별도
///   쿨다운 없이 겹치는 동안 계속 불린다(실제 반복 피해 방지는 플레이어 쪽 무적시간이 담당).
///   `TryAttack()`이 `Core.IDamageable`로 실제 피해를 준다(스프린트 2에서 스텁 → 실구현으로 전환됨).
/// - **낙하 종단속도 15유닛/s**(원본 `e.vy`엔 명시적 상한이 없지만 플레이어와 같은 `updateEnemies`
///   중력 루프를 쓰고 원본 전체가 이 상한을 공유함 — `CharacterMover2D` 참고).
/// - **필드 X 경계 여백 0.3유닛**(원본 `e.x = clamp(e.x, 30, mapW-30)`, 매 프레임). 이전엔 배회
///   중 경계 clamp가 아예 없어서 몹이 필드 밖으로 나갈 수 있었다.
/// - **몹끼리 물리로 안 부딪힌다**(2026-10-25 사용자) — 원본처럼 겹쳐 지나간다(Awake의 `excludeLayers`).
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(CircleCollider2D))]
public class EnemyMove : MonoBehaviour, ISpawnProtectable, IGravityAffectable
{
    [Header("스탯 (docs/sprints/01-combat-core.md 2번 - 고블린 기준 시작값, 원본 76px/s를 100px=1유닛로 축척)")]
    public float moveSpeed = 0.76f;
    public float attackPower = 13f;

    [Header("AI (원본 고블린 분기 그대로)")]
    [Tooltip("이 거리 안에 플레이어가 들어오면 그 방향으로 계속 걷는다. 원본 300px → 3유닛.")]
    public float aggroRangeX = 3f;
    [Tooltip("범위 밖일 때 배회 방향을 바꾸는 간격(최소~최대, 초). 원본 rand(1.5, 3.5).")]
    public Vector2 wanderIntervalRange = new Vector2(1.5f, 3.5f);

    [Header("발판 가장자리 반전 (원본: 발판 위 몹은 걸어서 안 떨어짐)")]
    [Tooltip("발판 가장자리에서 이만큼 안쪽에서 멈추고 방향을 반전한다. 원본 14px → 0.14유닛.")]
    public float platformEdgeMargin = 0.14f;
    [Tooltip("발판에 '올라서 있다'고 볼 X축 근접 여유. 원본 12px → 0.12유닛.")]
    public float platformProximityMargin = 0.12f;
    [Tooltip("발판에 '올라서 있다'고 볼 Y축 오차 허용. 원본 3px → 0.03유닛.")]
    public float onPlatformYTolerance = 0.03f;

    [Tooltip("스폰 직후 무적 시간(초) — 원본 CONFIG.run.spawnProtect 그대로.")]
    public float spawnProtectDuration = 2f;

    [Tooltip("성소처럼 움직이지도 때리지도 않는 구조물인지. 원본 `if (e.shrine) continue;`(:4024).\n" +
             "**컴포넌트를 끄면 안 된다** — 원본은 그 한 줄 앞에서 spawnInvuln을 깎기 때문에(:4023), " +
             "Update가 멈추면 스폰 보호가 영영 안 풀린다.")]
    public bool isStructure;

    [Header("원본 상수 그대로 이식")]
    public float terminalFallSpeed = 15f; // 원본 1500px/s ÷100(CharacterMover2D와 동일 상한)
    public float edgeMargin = 0.3f;       // 원본 clamp(e.x, 30, mapW-30)의 30px ÷100

    /// <summary>스폰 직후 무적 상태인지. 공격 스크립트는 이 몹을 파괴하기 전에 반드시 확인한다.</summary>
    public bool IsSpawnProtected => spawnProtectTimer > 0f;

    [Header("넉백 (원본 e.kbx)")]
    [Tooltip("넉백 감쇠 계수. 원본 `e.kbx *= max(0, 1 - 9*dt)`(project_test.html:4021) 그대로.")]
    public float knockbackDecay = 9f;

    Rigidbody2D rb;
    CircleCollider2D col;
    float spawnProtectTimer;
    SpriteRenderer body;   // 스폰 보호 중 반투명으로 그릴 몸(원본 "반투명 유령 상태")
    float bodyAlpha = -1f; // 스포너가 칠한 원래 알파 — 보호 중 첫 Update에서 잡는다(Awake 땐 아직 안 칠했다)
    Transform target;

    /// <summary>
    /// 쫓고 때릴 플레이어 — **만드는 쪽(`Systems/EnemySpawner`)이 넣는다**(2026-11-05 씬 분할 2차 2b, `HANDOFF.md` "연결 규칙").
    /// 예전엔 Player 태그로 씬을 뒤져 찾았다. 비어 있으면 배회만 하고 아무도 안 때린다.
    /// </summary>
    public Transform Target { get => target; set => target = value; }

    // 종류 전용 이동 스크립트(있으면 기본 보행 대신 이쪽에 물어본다). 원본 updateEnemies의 타입 분기.
    IEnemyMotion motion;
    IEnemyVerticalMotion verticalMotion;
    IEnemyContactDamageModifier contactDamageModifier;
    int dir = 1; // 원본 e.dir(1 또는 -1) — 추적/배회/가장자리반전이 전부 이 값을 공유
    float wanderTimer;
    // 원본 `e.kbx` — AI 이동 속도를 대체하는 게 아니라 **거기에 더해지는 별도 성분**이다
    // (`e.x += (e.vx + e.kbx) * dt`, project_test.html:4056). 매 프레임 지수 감쇠한다.
    // 그냥 rb.linearVelocity에 넣으면 아래 FixedUpdate가 다음 프레임에 통째로 덮어써서 사라진다.
    float knockbackX;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        // 몹끼리는 물리로 안 부딪힌다 — 2026-10-25 사용자 "몬스터끼리 충돌이 일어나서 문제였다, 겹쳐도 된다". 예전엔 레이어 충돌표에서
        // Enemy↔Enemy가 켜져 있어 원형 콜라이더끼리 밀리다 서로 위로 타고 올라가 쌓였다(특히 중력장이 끌어모을 때 — 측정: 중력장 셋을 깐
        // 3스테이지에서 올라탄 쌍 최대 6). 원본도 몹끼리 밀어내기가 없어 그냥 겹쳐 지나간다(updateEnemies :4015) — 원본대로 돌아간 것.
        // 레이어 충돌표(프로젝트 설정)를 안 고치고 몹 몸마다 끈다 — 편집기가 켜진 채 설정 파일을 바꾸면 편집기가 덮어쓴다.
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) rb.excludeLayers |= 1 << enemyLayer;
        col = GetComponent<CircleCollider2D>();
        body = GetComponent<SpriteRenderer>();
        CacheTypeBehaviour();
        spawnProtectTimer = spawnProtectDuration;
        wanderTimer = Random.Range(wanderIntervalRange.x, wanderIntervalRange.y);
        dir = Random.value < 0.5f ? 1 : -1; // 원본 `dir: Math.random() < 0.5 ? 1 : -1`(project_test.html:3959)
    }

    /// <summary>
    /// 종류 전용 이동 스크립트(<see cref="IEnemyMotion"/> 등)를 찾아 둔다 — `Awake`에서 한 번.
    /// 행동 부품은 **종류별 프리팹에 이미 들어 있어서**(`Assets/Prefabs/Enemies/`, 2026-11-05 생성 책임 2-2) 세우는 순간 같이 보인다.
    /// 예전엔 스포너가 `Instantiate` 뒤에 부품을 붙이고 이걸 다시 부르게 했다(빠뜨리면 박쥐가 떨어지고 멧돼지가 걷기만 하는, 에러 없이 틀리는 상태) —
    /// 실행 중에 행동 부품을 붙이지 말고 프리팹에 넣을 것. 테스트도 꺼 둔 오브젝트에 부품을 다 붙인 뒤 켠다.
    /// </summary>
    void CacheTypeBehaviour()
    {
        // 종류에 맞는 이동 스크립트가 붙어 있으면 그쪽을 쓴다. 없으면(고블린·오우거·슬라임·꼬마 슬라임) 기본 보행.
        motion = GetComponent<IEnemyMotion>();
        verticalMotion = GetComponent<IEnemyVerticalMotion>();
        contactDamageModifier = GetComponent<IEnemyContactDamageModifier>();
        if (verticalMotion != null) rb.gravityScale = 0f; // 비행형은 중력을 안 받는다(원본 wisp 분기)
    }

    /// <summary>
    /// 스폰 보호 시간을 덮어쓴다. 원본 `spawnEnemyAt(..., { protect: 0.35 })`(project_test.html:3968) —
    /// 슬라임이 낳는 꼬마 슬라임만 일반 스폰(2초)보다 훨씬 짧은 보호를 받는다. 죽은 자리에서 바로 나오는데
    /// 2초나 무적이면 플레이어가 손을 못 대기 때문이다.
    /// </summary>
    public void SetSpawnProtection(float seconds)
    {
        spawnProtectDuration = seconds;
        spawnProtectTimer = seconds;
    }

    /// <summary>
    /// 스폰 보호 중 몸의 불투명도 — 원본 "스폰 보호 중: 반투명 유령 상태"
    /// `if (e.spawnInvuln > 0) ctx.globalAlpha = 0.4 + 0.1 * Math.sin(state.time * 10)`(project_test.html:4772~4773).
    /// 이 연출이 빠져 있어서 무적인 몹이 그냥 서 있는 것처럼 보였다(2026-10-24 사용자 "적 스폰 중일 때 효과").
    /// </summary>
    public static float SpawnGhostAlpha(float time) => 0.4f + 0.1f * Mathf.Sin(time * 10f);

    // 색(흰색·종류색)은 스포너가 칠한 그대로 두고 알파만 바꾼다.
    void ApplySpawnGhost(bool ghost)
    {
        if (body == null) return;
        Color c = body.color;
        if (bodyAlpha < 0f) bodyAlpha = c.a;
        float a = ghost ? bodyAlpha * SpawnGhostAlpha(Time.time) : bodyAlpha;
        if (Mathf.Approximately(c.a, a)) return;
        c.a = a;
        body.color = c;
    }

    void Update()
    {
        // 원본은 스폰 보호 중이면 적 갱신을 통째로 건너뛴다 — 주석 그대로 "상호 무적: 움직이지도,
        // 때리지도, 맞지도 않는다"(project_test.html:4022~4023). 예전엔 타이머만 깎고 이동·접촉은
        // 그대로 돌아서, 무적인 몹이 플레이어를 때릴 수 있었다(원본과 다름 — 스프린트 2에서 수정).
        if (spawnProtectTimer > 0f)
        {
            spawnProtectTimer -= Time.deltaTime;
            ApplySpawnGhost(spawnProtectTimer > 0f);
            return;
        }

        // 원본은 스폰 보호를 깎은 **뒤에** 성소를 건너뛴다(:4024) — 순서가 뒤집히면 무적이 안 풀린다.
        if (isStructure) return;

        // 기본 보행의 방향 판단(추적/배회)은 종류 전용 스크립트가 없을 때만 돈다 —
        // 멧돼지·해골 궁수는 자기 상태기계 안에서 방향을 직접 정한다(원본도 타입별 분기 안에서 e.dir을 정함).
        if (motion == null) UpdateDirection();
        // 발판 가장자리 반전은 지상형만 — 비행형은 발판과 무관하다.
        if (verticalMotion == null) TryTurnBackAtPlatformEdge();

        if (target != null && OverlapsTarget())
            TryAttack(target); // 원본: 쿨다운 없이 겹치는 동안 매 프레임 호출(실제 반복 피해 방지는 플레이어 무적시간 담당)
    }

    /// <summary>
    /// 피격 넉백을 준다(원본 `e.kbx = sign(e.x - player.x) * kbBase`, project_test.html:1679).
    /// `EnemyHealth`가 피해를 입힐 때 호출한다 — 이동 속도를 덮어쓰지 않고 더해지는 성분이라
    /// 걷던 방향과 무관하게 밀려난 뒤 자연스럽게 원래 이동으로 돌아온다.
    /// </summary>
    public void ApplyKnockback(float velocityX) => knockbackX = velocityX;

    /// <summary>
    /// 실제 월드 반지름. 몸집이 다른 종류(오우거는 고블린의 1.74배)와 엘리트(×1.35)는 콜라이더 값을
    /// 고치는 대신 <c>transform.localScale</c>로 키운다 — 그래야 스프라이트와 판정이 같이 커진다.
    /// 그래서 `col.radius`(로컬 값)를 그대로 쓰면 큰 몹일수록 판정이 실제보다 작아진다.
    /// </summary>
    public float WorldRadius => col.radius * Mathf.Abs(transform.lossyScale.x);

    /// <summary>중력점(gravityWell)에 노출된 동안의 위치. Combat 도메인이 끌어당길 대상까지의 거리를 재는 데 쓴다.</summary>
    public Vector2 WorldPosition => rb.position;

    /// <summary>
    /// 원본 `e.x += dx/d*force*dt; e.y += ...*0.28; e.kbx *= 0.85`(project_test.html:3823-3825).
    /// 물리 속도가 아니라 위치를 직접 더하고, 기존 넉백을 추가로 감쇠시킨다.
    /// </summary>
    public void ApplyGravityWellPull(Vector2 positionDelta)
    {
        rb.position += positionDelta;
        knockbackX *= 0.85f;
    }

    void FixedUpdate()
    {
        // 원본은 AI 판단보다 먼저 넉백을 감쇠시킨다(project_test.html:4021).
        knockbackX *= Mathf.Max(0f, 1f - knockbackDecay * Time.fixedDeltaTime);

        if (isStructure) return; // 구조물은 밀리지도 걷지도 않는다

        // 스폰 보호 중엔 수평 이동을 하지 않는다(위 Update 주석 참고). 중력·착지는 Physics2D에 그대로 맡긴다
        // — 원본은 스폰 지점이 이미 착지 높이라 낙하가 없지만, 우리 쪽은 안전하게 물리에 맡겨 둔다.
        //
        // 종류 전용 이동 스크립트가 있으면 수평 속도를 그쪽이 정한다(원본 updateEnemies의 타입별 mvx).
        // 성소가 살아 있는 동안 적은 더 빨라진다(원본 `e.speed * (buffed ? eMs : 1)`, :4040).
        float buffedSpeed = moveSpeed * CombatModifiers.EnemyMoveSpeedMultiplier;
        float ownSpeed = motion != null
            ? motion.GetHorizontalSpeed(Time.fixedDeltaTime, target, buffedSpeed)
            : dir * buffedSpeed;
        float vx = IsSpawnProtected ? 0f : ownSpeed + knockbackX; // 원본 `e.vx + e.kbx`

        float minX = FieldBounds.MinX + edgeMargin;
        float maxX = FieldBounds.MaxX - edgeMargin;
        if (rb.position.x <= minX && vx < 0f) vx = 0f;
        if (rb.position.x >= maxX && vx > 0f) vx = 0f;

        if (verticalMotion != null)
        {
            // 비행형: 중력·착지 없이 Y를 직접 놓는다(원본 wisp는 `e.y +=`로 좌표를 직접 옮긴다).
            float y = IsSpawnProtected
                ? rb.position.y
                : verticalMotion.GetVerticalPosition(Time.fixedDeltaTime, target, rb.position.y);
            rb.linearVelocity = new Vector2(vx, 0f);
            rb.position = new Vector2(Mathf.Clamp(rb.position.x, minX, maxX), y);
            return;
        }

        float vy = Mathf.Max(rb.linearVelocity.y, -terminalFallSpeed); // 원본 종단속도 상한
        rb.linearVelocity = new Vector2(vx, vy); // Y(중력·착지)는 Physics2D에 맡김

        if (rb.position.x < minX || rb.position.x > maxX)
            rb.position = new Vector2(Mathf.Clamp(rb.position.x, minX, maxX), rb.position.y);
    }

    /// <summary>
    /// 종류 전용 스크립트가 방향을 직접 정할 때 쓴다(원본은 타입별 분기 안에서 `e.dir`을 바꾼다).
    /// `EnemyMove`가 들고 있는 `dir`은 스프라이트 방향·발판 반전과도 얽혀 있어 한 곳에서만 바꾼다.
    /// </summary>
    public void SetDirection(int newDir) => dir = newDir >= 0 ? 1 : -1;

    /// <summary>현재 바라보는 방향(1/-1). 종류 전용 스크립트가 읽는다.</summary>
    public int Direction => dir;

    /// <summary>원본: `if (Math.abs(dx) < 300) dir = sign(dx) || dir; else { wander }`.</summary>
    void UpdateDirection()
    {
        if (target != null)
        {
            float dx = target.position.x - transform.position.x;
            if (Mathf.Abs(dx) < aggroRangeX)
            {
                if (dx != 0f) dir = dx > 0f ? 1 : -1; // Math.sign(0)이 falsy라 dx==0이면 dir 유지
                return;
            }
        }
        Wander();
    }

    void Wander()
    {
        wanderTimer -= Time.deltaTime;
        if (wanderTimer <= 0f)
        {
            dir = -dir;
            wanderTimer = Random.Range(wanderIntervalRange.x, wanderIntervalRange.y);
        }
    }

    /// <summary>
    /// 원본: `e.vy===0 && e.y < groundY-2`(=우리 좌표계에서 "발판 위에 정지해 있음")일 때,
    /// 지금 서 있는 발판을 찾아 가장자리 14px 안쪽이면 그 자리에 고정하고 안쪽으로 dir 반전.
    /// </summary>
    void TryTurnBackAtPlatformEdge()
    {
        if (Mathf.Abs(rb.linearVelocity.y) > 0.01f) return; // 낙하/착지 중이면 스킵(원본 e.vy===0)
        if (transform.position.y <= FieldBounds.GroundY + WorldRadius + 0.02f) return; // 바닥이면 발판 로직 불필요

        for (int i = 0; i < FieldLayout.Platforms.GetLength(0); i++)
        {
            float landingY = FieldLayout.PlatformLandingY(i, WorldRadius);
            if (Mathf.Abs(transform.position.y - landingY) >= onPlatformYTolerance) continue;

            float left = FieldLayout.PlatformLeftX(i);
            float right = FieldLayout.PlatformRightX(i);
            if (transform.position.x < left - platformProximityMargin || transform.position.x > right + platformProximityMargin)
                continue;

            float leftEdge = left + platformEdgeMargin;
            float rightEdge = right - platformEdgeMargin;
            if (transform.position.x < leftEdge)
            {
                transform.position = new Vector3(leftEdge, transform.position.y, transform.position.z);
                dir = 1;
            }
            else if (transform.position.x > rightEdge)
            {
                transform.position = new Vector3(rightEdge, transform.position.y, transform.position.z);
                dir = -1;
            }
            break;
        }
    }

    bool OverlapsTarget()
    {
        var targetCol = target.GetComponent<Collider2D>();
        float targetRadius = targetCol != null ? targetCol.bounds.extents.x : 0.5f;
        float dist = Vector2.Distance(transform.position, target.position);
        return dist < WorldRadius + targetRadius;
    }

    /// <summary>
    /// 접촉 데미지. 원본은 겹치는 동안 **매 프레임 쿨다운 없이** `damagePlayer(e.dmg, e.x)`를 부른다
    /// (project_test.html:4148) — 반복 피해를 막는 건 전적으로 플레이어 쪽 무적시간(0.9초)이다.
    /// 그래서 여기에 쿨다운을 넣으면 안 된다(원본과 달라진다).
    ///
    /// `Enemies`는 `Characters`를 참조할 수 없으므로(asmdef 같은 층) `Core.IDamageable`로만 접근한다.
    /// 무적 여부도 여기서 못 보지만, 받는 쪽이 무적이면 스스로 무시하므로 결과는 원본과 같다.
    /// </summary>
    void TryAttack(Transform playerTransform)
    {
        var damageable = playerTransform.GetComponent<IDamageable>();
        if (damageable == null || damageable.IsDead) return;
        // 원본은 멧돼지가 질주 중일 때만 접촉 피해 ×1.4다(`chargeMul`, project_test.html:4147).
        float mul = contactDamageModifier != null ? contactDamageModifier.ContactDamageMultiplier : 1f;
        // 성소가 살아 있으면 여기에 ×1.3이 더 붙는다(원본 `buffed ? eDmg : 1`, :4148).
        mul *= CombatModifiers.EnemyDamageMultiplier;
        // 피해 난수(±10%)는 원본이 피격자 쪽(damagePlayer)에서 굴리므로 여기선 원본 스탯 그대로 넘긴다.
        damageable.TakeDamage(attackPower * mul, gameObject);
    }
}
}
