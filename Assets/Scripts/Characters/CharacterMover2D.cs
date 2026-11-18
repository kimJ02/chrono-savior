using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
/// <summary>
/// 이동 컨트롤러. 실제 Physics2D(Rigidbody2D 동적 바디 + 콜라이더 충돌 해석) 기반 — 전역 중력은
/// `ProjectSettings/Physics2DSettings`의 Gravity (0, -26)(원본 2600px/s² → 26, 100px=1유닛 축척).
///
/// X는 화살표 키로 직접 속도를 넣어 자유 이동(원본 `updatePlayerCommon`의 bow/gunner 분기가
/// `p.vx = mx * CONFIG.player.moveSpeed`로 가속 없는 즉시 속도라 그대로 이식 — 참고로 블레이드
/// 캐릭터만 별도의 관성 가속 이동(`bladeMove`)을 쓰는데 이번 스프린트 범위(마법사)엔 해당 없음).
/// Y는 중력 + 발판/바닥 콜라이더와의 실제 충돌로 정지한다. 접지 판정은 발밑에서 Ground 레이어로
/// OverlapCircle.
///
/// 원본에서 이식한 디테일(2026-08-26, 전체 재대조):
/// - **코요테 타임(0.10초)·점프 입력 버퍼(0.13초)**: 원본 `COYOTE_T`/`INPUT_BUF_T`. 발판을 살짝
///   벗어난 직후에도 점프가 되고(코요테), 착지 직전에 미리 눌러둔 점프도 착지 즉시 나간다(버퍼).
///   이전엔 "grounded인 바로 그 프레임에 keydown"만 인정해서 원본보다 훨씬 빡빡했다.
/// - **낙하 종단속도 15유닛/s**(원본 `p.vy = Math.min(p.vy, 1500)` → 1500px/s÷100). 실제 물리
///   중력을 쓰면서 높은 발판(최고 5.4유닛)에서 떨어지면 이론상 ≈16.8유닛/s까지 나서 원본의 상한을
///   넘는다 — 그대로 두면 원본보다 더 세게 떨어진다.
/// - **필드 X 경계 여백 0.24유닛**(원본 `clamp(nx, 24, mapW-24)`). 이전엔 여백 없이 [0,26]에
///   딱 붙여서 clamp했다.
///
/// 점프 키는 원본 KEYMAP엔 C와 Space 둘 다 있지만, 사용자 지시로 이번 프로토타입은
/// C만 쓴다 — 원본과의 의도적 차이. 나중에 세션이 이걸 "원본과 다르다"며 되돌리지 말 것.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CharacterMover2D : MonoBehaviour, Core.IRunResettable, Core.IGrabbable
{
    public float jumpSpeed = 9.6f;
    public float groundCheckRadius = 0.12f;
    public LayerMask groundMask;

    [Header("원본 상수 그대로 이식")]
    public float coyoteTime = 0.10f;      // 원본 COYOTE_T
    public float jumpBufferTime = 0.13f;  // 원본 INPUT_BUF_T
    public float terminalFallSpeed = 15f; // 원본 1500px/s ÷100

    [Header("아래키 발판 관통 (원본 :3460)")]
    [Tooltip("발판을 통과하는 시간. 원본 dropTimer 0.22.")]
    public float dropThroughTime = 0.22f;
    [Tooltip("통과 시작할 때 아래로 밀어주는 속도. 원본 vy 140 ÷100(원본은 Y+가 아래라 부호가 반대).")]
    public float dropThroughSpeed = 1.4f;
    public float edgeMargin = 0.24f;      // 원본 clamp(nx, 24, mapW-24)의 24px ÷100

    /// <summary>
    /// 수평 이동 방식. 원본은 캐릭터마다 갱신 함수가 갈린다(`updatePlayer` :3509).
    /// <c>Instant</c> = 마법사·메카닉(입력 즉시 목표 속도, `p.vx = mx * moveSpeed`),
    /// <c>Inertial</c> = 섬영(관성 가속, `bladeMove` :2454).
    /// </summary>
    public enum MoveMode { Instant, Inertial }

    [Header("이동 방식 (캐릭터별)")]
    public MoveMode moveMode = MoveMode.Instant;

    [Header("관성 모드 파라미터 (원본 CONFIG.blade :613, 100px=1유닛)")]
    [Tooltip("입력 즉시 붙는 최저 속도. 원본 baseSpeed 300.")]
    public float inertialBaseSpeed = 3.0f;
    [Tooltip("최고 속도. 원본 maxSpeed 900.")]
    public float inertialMaxSpeed = 9.0f;
    [Tooltip("같은 방향 유지 시 초당 가속량. 원본 accel 520.")]
    public float inertialAccel = 5.2f;
    [Tooltip("무입력 시 초당 자연 감속. 원본 decel 900.")]
    public float inertialDecel = 9.0f;

    /// <summary>마지막으로 이동한 좌우 방향(1 또는 -1). 조준 입력이 없을 때 MageAttack의 기본 발사 방향으로 쓰인다.</summary>
    public int Facing { get; private set; } = 1;

    /// <summary>
    /// 마지막으로 "달리던" 방향(원본 `p.runDir`). 관성 모드에서 가속(같은 방향 유지)과
    /// 반전(방향 전환)을 가르는 기준이다. <see cref="Facing"/>과 달리 조준으로는 안 바뀐다.
    /// </summary>
    public int RunDir { get; private set; } = 1;

    /// <summary>다른 스크립트가 프레임마다 갱신하는 이동속도 배율(예: MageAttack의 차지 중 50% 감속). 기본 1.</summary>
    public float SpeedMultiplier = 1f;

    /// <summary>
    /// 관성 모드에서 이동 파라미터 전체(baseSpeed/maxSpeed/accel/decel)에 곱해지는 배율.
    /// 섬영 키트가 매 프레임 `min(1, statMs)`를 넣는다 — 원본 `bladeMsK()`(:2426)는 이속 배수를
    /// **100%에서 자르고**, 넘친 분(`bladeMsSurplus`)은 속도가 아니라 피해로 환산한다.
    /// 그래서 관성 모드는 <see cref="SpeedMultiplier"/>(Instant 모드용, 상한 없음)를 쓰지 않는다.
    /// </summary>
    public float MoveScale = 1f;

    /// <summary>
    /// 관성 모드에서 **가속에만** 추가로 곱해지는 배율. 원본에서 집중(focus) 중 가속이 커지는 항
    /// (`:2462` `accelMul`의 focus 부분)에 대응한다 — 0차엔 집중이 없으니 1로 두면 된다.
    ///
    /// 이속의 **제곱**으로 가속이 커지는 부분(`bladeAccelK()` :2434, 그래야 최고속 도달 '거리'가
    /// 이속과 무관하게 일정해진다)은 <see cref="MoveScale"/>로부터 **내부에서 자동 적용**되므로
    /// 여기에 다시 넣지 말 것. 원본은 감속에도 같은 제곱을 곱하지만 focus 배수는 안 곱한다(`:2464`).
    /// </summary>
    public float AccelMultiplier = 1f;

    /// <summary>공격 중 이동 잠금 종류(<see cref="AttackLock"/>).</summary>
    public enum AttackMoveLock
    {
        /// <summary>잠금 없음(원본 — 마법사·메카닉도 공격 중 이동 자유, `attackRapid`).</summary>
        None,
        /// <summary>땅에선 좌우 이동·점프 불가, 공중에선 원본대로 자유(마법사 차지·발사 자세).</summary>
        RootOnGround,
        /// <summary>땅에선 좌우 이동·점프 불가, 공중에선 새 좌우 입력만 무시하고 지금 가로 속도·중력 유지(메카닉 레이저).</summary>
        RootOnGroundNoSteerInAir,
    }

    /// <summary>
    /// 공격 중 이동 잠금 — 무기 키트가 <see cref="SpeedMultiplier"/>처럼 **매 프레임** 넣는다(2026-11-16 사용자 결정, 의도적 편차:
    /// 공격 그림이 제자리에 선 자세라 달리며 쏘면 미끄러져 보인다). 방향 전환(뒤돌기)은 잠금 중에도 된다 — 입력이 <see cref="Facing"/>을 계속 바꾼다.
    /// 버튼을 떼면 키트가 None으로 돌려 곧바로 움직인다. 캐릭터 전환·런 리셋(<see cref="ResetMotion"/>)에서 None.
    /// </summary>
    public AttackMoveLock AttackLock = AttackMoveLock.None;

    /// <summary>잠금 중 점프가 막히는가 — 땅(코요테 구간 포함)이면 막는다. 막힌 점프 입력은 버린다(버튼을 뗀 뒤 저절로 뛰지 않게).</summary>
    public static bool JumpBlockedByAttack(AttackMoveLock lk, bool groundedOrCoyote) => lk != AttackMoveLock.None && groundedOrCoyote;

    /// <summary>
    /// 입력으로 낸 가로 속도에 잠금을 적용한다 — 땅이면 0, 공중 레이저면 지금 가로 속도 그대로(넉백·관성 포함, 저장값을 다시 넣지 않는다), 그 외 입력 그대로.
    /// </summary>
    public static float ApplyAttackLock(AttackMoveLock lk, bool grounded, float inputVx, float currentVx)
    {
        if (lk == AttackMoveLock.None) return inputVx;
        if (grounded) return 0f;
        return lk == AttackMoveLock.RootOnGroundNoSteerInAir ? currentVx : inputVx;
    }

    Rigidbody2D rb;
    Collider2D col;
    bool grounded;

    /// <summary>지금 접지 여부 — `PlayerRig`가 Animator의 Grounded 파라미터로 그대로 넘긴다(2026-11-16 애니메이션 연결).</summary>
    public bool Grounded => grounded;

    /// <summary>지금 몸 속도 — `PlayerRig`가 수평 성분만 보고 Animator의 Moving 파라미터를 결정한다(2026-11-16).</summary>
    public Vector2 Velocity => rb != null ? rb.linearVelocity : Vector2.zero;
    // 아래키 발판 관통 — 통과 중인 동안 무시한 발판 콜라이더들과 남은 시간.
    readonly System.Collections.Generic.List<Collider2D> ignoredPlatforms = new System.Collections.Generic.List<Collider2D>();
    float dropTimer;
    float coyoteTimer;
    float jumpBufferTimer;
    float inertialVx; // 관성 모드가 프레임을 넘겨 들고 가는 수평 속도(원본 p.vx)
    bool grabbed; // IGrabbable — 2지역 보스 "부서진 여신상" 잡기 패턴에 붙잡힌 동안

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 1f;
        if (groundMask.value == 0) groundMask = LayerMask.GetMask("Ground");
    }

    void Update()
    {
        // 붙잡힌 동안은 점프 버퍼·코요테를 포함해 통째로 멈춘다 — 보스가 MoveGrabbedTo로 위치를 직접 넣는다
        // (2지역 보스 "부서진 여신상" 잡기 패턴, IGrabbable 아래 참고).
        if (grabbed) return;

        UpdateDropThrough(Time.deltaTime);

        if (GameInput.JumpDown) jumpBufferTimer = jumpBufferTime;
        else jumpBufferTimer -= Time.deltaTime;

        coyoteTimer = grounded ? coyoteTime : coyoteTimer - Time.deltaTime;
        if (JumpBlockedByAttack(AttackLock, grounded || coyoteTimer > 0f)) jumpBufferTimer = 0f; // 공격 중 점프 금지(땅)

        if (jumpBufferTimer > 0f && (grounded || coyoteTimer > 0f))
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpSpeed);
            jumpBufferTimer = 0f;
            coyoteTimer = 0f; // 코요테를 소모해 공중에서 두 번 점프하는 걸 막는다(원본 동일)
            SfxEvents.RaiseJumped(ProfileService.Current.character); // 2026-11-10 사운드 — 캐릭터별 점프음
        }
    }

    void FixedUpdate()
    {
        // 붙잡힌 동안은 수평 이동·경계 clamp·접지 갱신도 멈춘다 — 중력도 꺼 둔 상태라(BeginGrab)
        // 보스가 MoveGrabbedTo로 넣는 위치가 다음 프레임까지 그대로 유지된다.
        if (grabbed) return;

        float h = 0f;
        if (GameInput.Left) h -= 1f;
        if (GameInput.Right) h += 1f;
        if (h != 0f) Facing = h > 0 ? 1 : -1;

        float vx = moveMode == MoveMode.Inertial
            ? ComputeInertialVx(h, Time.fixedDeltaTime)
            // 원본 statMs()(project_test.html:1286) — 골드 강화(ms)가 반영된 배수. 기본 속도(`Core/PlayerMoveConfig.BaseSpeed`, 로비 아바타와 같은 값)는
            // 안 바꾸고 매 프레임 곱해서 적용(다른 배수들과 같은 자리에서, 누적 곱 버그 없이).
            : h * PlayerMoveConfig.BaseSpeed * SpeedMultiplier * PlayerStatCalculator.ComputeMoveSpeedMultiplier(ProfileService.Current);
        // 공격 중 이동 잠금 — 땅 판정은 지금 다시 잰다(직전 물리 틱 값이면 착지 직후 한 틱 미끄러진다, Codex 2026-11-16).
        // 수직 속도(중력·발판 내려가기·넉백 튕김)는 건드리지 않는다.
        if (AttackLock != AttackMoveLock.None)
            vx = ApplyAttackLock(AttackLock, CheckGrounded(), vx, rb.linearVelocity.x);

        float minX = FieldBounds.MinX + edgeMargin;
        float maxX = FieldBounds.MaxX - edgeMargin;
        if ((rb.position.x <= minX && vx < 0f) || (rb.position.x >= maxX && vx > 0f))
        {
            vx = 0f;
            // 관성 모드는 속도를 프레임 너머로 들고 다니므로, 벽에 막혔으면 쌓인 속도도 같이 버린다
            // — 원본 설계 주석 그대로 "같은 방향 유지 = 가속, 입력 해제 = 감속, **벽 = 정지**"(:612).
            // 안 버리면 벽에 붙어 있는 동안 최고 속도가 유지돼서, 떨어지자마자 최고속으로 튀어나간다.
            inertialVx = 0f;
        }

        float vy = Mathf.Max(rb.linearVelocity.y, -terminalFallSpeed); // 원본 종단속도 상한
        rb.linearVelocity = new Vector2(vx, vy);

        if (rb.position.x < minX || rb.position.x > maxX)
            rb.position = new Vector2(Mathf.Clamp(rb.position.x, minX, maxX), rb.position.y);

        grounded = CheckGrounded();
    }

    /// <summary>
    /// 관성 이동. 원본 `bladeMove(dt, mx)`(project_test.html:2454) 그대로 옮겼다.
    /// - **무입력**: `decel`만큼 감속하되 부호는 유지(0에서 멈춤)
    /// - **같은 방향 유지**: `max(현재속도, baseSpeed)`에서 `accel`만큼 가속, `maxSpeed`에서 상한
    /// - **방향 전환**: **감속이 아니라 즉시 반전** — 갖고 있던 속도를 그대로 반대로 돌린다(`:2469`~`:2474`).
    ///   원본 주석 그대로 "baseS로 깎으면 순간이지만 여전히 감속으로 느껴지므로".
    ///   ⚠️ `CONFIG.blade.brake`(2200)는 정의만 있고 `bladeMove`가 **실제로 안 쓰는 죽은 값**이다
    ///   (`CONFIG.souls`와 같은 종류) — "브레이크 감속"을 만들어 넣지 말 것.
    ///
    /// 속도(base/max)에는 <see cref="MoveScale"/>을, 가속·감속에는 그 **제곱**을 곱한다
    /// (원본 `bladeMsK()`와 `bladeAccelK()` :2426·:2434). 가속에만 <see cref="AccelMultiplier"/>가 더 곱해진다.
    /// </summary>
    float ComputeInertialVx(float h, float dt)
    {
        float k = Mathf.Max(0f, MoveScale);
        float kSq = k * k;
        float baseS = inertialBaseSpeed * k;
        float maxS = inertialMaxSpeed * k;

        if (h == 0f)
        {
            float s = Mathf.Max(0f, Mathf.Abs(inertialVx) - inertialDecel * kSq * dt);
            inertialVx = Mathf.Sign(inertialVx) * s;
        }
        else
        {
            int mx = h > 0f ? 1 : -1;
            // 원본 `mx === p.runDir && Math.sign(p.vx) === mx` — 둘 다 맞아야 "계속 달리는 중"이다.
            //
            // ⚠️ `Mathf.Sign`을 쓰면 안 된다: JS `Math.sign(0)`은 **0**인데 Unity `Mathf.Sign(0f)`은 **1**이다.
            // 그 차이 때문에 정지 상태에서 첫 입력이 "이미 달리던 중"으로 오판돼 baseSpeed에 한 프레임
            // 가속이 더 붙는다(원본은 정확히 baseSpeed에서 시작). 실제로 테스트가 이 차이를 잡아냈다.
            int vxSign = inertialVx > 0f ? 1 : inertialVx < 0f ? -1 : 0;
            if (mx == RunDir && vxSign == mx)
            {
                float s = Mathf.Max(Mathf.Abs(inertialVx), baseS);
                inertialVx = mx * Mathf.Min(s + inertialAccel * kSq * AccelMultiplier * dt, maxS);
            }
            else
            {
                RunDir = mx;
                inertialVx = mx * Mathf.Max(Mathf.Abs(inertialVx), baseS);
            }
        }
        return inertialVx;
    }

    /// <summary>조준 방향으로 바라보게 한다(원본 `if (Math.abs(aimX) > 0.1) p.facing = sign(aimX)`,
    /// project_test.html:1959) — 이동이 없어도 마법사가 쏘는/스킬 쓰는 방향으로 몸을 돌린다.
    /// FixedUpdate의 이동 입력 기반 갱신은 h==0(가만히 서서 조준)일 땐 이 값을 건드리지 않는다.</summary>
    public void SetFacing(int dir) => Facing = dir;

    /// <summary>즉시 순간이동(원본 불길 이동 X 스킬, `p.x=toX;p.y=toY;p.vx=0;p.vy=0`, project_test.html:2148).</summary>
    public void Teleport(Vector3 position)
    {
        CancelDropThrough(); // 옛 자리에서 통과 중이던 발판이 새 자리에서도 무시되지 않게(2026-11-07 최종 점검)
        // `rb.position`만 바꾸면 `transform`은 **다음 물리 스텝에야** 따라온다 — 그 사이에 위치를 읽는
        // 쪽(카메라, 같은 프레임의 다른 스크립트, 런 시작 직후 판정)은 옛 좌표를 본다. 실제로
        // 런 시작 리셋에서 플레이어가 한 프레임 동안 엉뚱한 자리에 남아 있는 걸로 드러났다.
        transform.position = position;
        rb.position = position;
        rb.linearVelocity = Vector2.zero;
        inertialVx = 0f; // 원본도 vx를 0으로 만든다 — 관성 모드가 들고 있던 속도도 같이 버려야 한다
    }

    /// <summary>
    /// 캐릭터를 바꿀 때 이동 상태를 초기화한다(<see cref="PlayerRig"/>가 호출).
    /// 관성 속도가 남아 있으면 다른 캐릭터로 바꾼 직후에도 그 속도로 미끄러진다.
    /// </summary>
    /// <summary>
    /// 새 사냥 시작 시 — 원본 `resetPlayerForRun()`의 이동 부분
    /// (`p.x = 220; p.y = groundY; p.vx = 0; p.vy = 0; p.facing = 1; p.runDir = 1`, project_test.html:1513).
    /// **시작 위치 2.2유닛은 원본 220px 그대로다**(100px=1유닛).
    /// </summary>
    public void ResetForRun()
    {
        if (grabbed) EndGrab(); // 방어적 정리 — 보스 사망/비활성화 경로가 먼저 돌았어야 하지만, 혹시 남아 있으면 새 런에 안 넘어가게
        CancelDropThrough();
        ResetMotion();
        Facing = 1;
        // 덩치가 바뀌면 띄울 높이도 같이 바뀌어야 한다 — 상수로 박아두면 커진 캐릭터가
        // 지면에 파묻힌 채 물리에 밀려 튀어오른다.
        var col = GetComponent<CircleCollider2D>();
        float lift = col != null ? col.radius * Mathf.Abs(transform.localScale.x) : StartHeightAboveGround;
        Teleport(new Vector3(RunStartX, Core.FieldBounds.GroundY + lift, 0f));
    }

    /// <summary>원본 `p.x = 220`(project_test.html:1513) ÷100.</summary>
    public const float RunStartX = 2.2f;
    /// <summary>
    /// 콜라이더를 못 찾았을 때 쓰는 기본 여유. 평소엔 실제 반지름을 쓴다(<see cref="ResetForRun"/>).
    /// 원본은 y=groundY에 바로 놓는다 — 원본 엔티티는 발밑이 기준점이라 띄울 필요가 없다.
    /// </summary>
    public const float StartHeightAboveGround = 0.5f;

    public void ResetMotion()
    {
        inertialVx = 0f;
        RunDir = 1;
        SpeedMultiplier = 1f;
        MoveScale = 1f;
        AccelMultiplier = 1f;
        AttackLock = AttackMoveLock.None;
        if (rb != null) rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
    }

    /// <summary>
    /// 아래키로 발판을 통과해 내려간다 — 원본 `:3460`:
    /// <code>if (down &amp;&amp; onGround &amp;&amp; y &lt; groundY - 2) { dropTimer = 0.22; vy = 140; onGround = false; }</code>
    ///
    /// **바닥(맨 아래 지면)에서는 안 된다** — 원본의 `y &lt; groundY - 2` 조건이 그것이다.
    /// 4층 수직 맵인데 이게 없으면 내려오는 수단이 "가장자리로 걸어서 떨어지기"뿐이라 이동이 답답해진다.
    ///
    /// 구현은 **콜라이더 쌍 무시**(`Physics2D.IgnoreCollision`)다. 레이어를 통째로 끄면 같은 레이어의
    /// 적까지 발판을 통과하고, 플레이어 콜라이더를 끄면 맨 바닥까지 빠진다.
    /// </summary>
    void UpdateDropThrough(float dt)
    {
        if (dropTimer > 0f)
        {
            dropTimer -= dt;
            if (dropTimer <= 0f) RestoreDroppedPlatforms();
            return;
        }

        if (GameInput.Down) TryStartDropThrough();
    }

    /// <summary>
    /// 지금 밟고 있는 원웨이 발판을 통과하기 시작한다 — 아래키를 누른 것과 같은 동작(테스트도 이걸 부른다, 2026-10-24).
    /// 공중·맨 바닥·이미 통과 중이면 아무것도 안 한다. 시작했으면 true.
    /// </summary>
    public bool TryStartDropThrough()
    {
        if (dropTimer > 0f || !grounded) return false;
        // 맨 바닥에 서 있으면 통과할 발판이 없다(원본 `y < groundY - 2`).
        if (transform.position.y <= FieldBounds.GroundY + col.bounds.extents.y + 0.05f) return false;

        // 지금 밟고 있는 발판(원웨이 이펙터가 붙은 것)만 골라 무시한다.
        var hits = Physics2D.OverlapCircleAll(
            (Vector2)transform.position + Vector2.down * col.bounds.extents.y, groundCheckRadius * 3f, groundMask);
        foreach (var hit in hits)
        {
            if (hit == null || hit.GetComponent<PlatformEffector2D>() == null) continue;
            Physics2D.IgnoreCollision(col, hit, true);
            ignoredPlatforms.Add(hit);
        }
        if (ignoredPlatforms.Count == 0) return false;

        dropTimer = dropThroughTime;
        rb.linearVelocity = new Vector2(rb.linearVelocity.x, -dropThroughSpeed);
        grounded = false;
        return true;
    }

    void RestoreDroppedPlatforms()
    {
        foreach (var c in ignoredPlatforms)
            if (c != null) Physics2D.IgnoreCollision(col, c, false);
        ignoredPlatforms.Clear();
    }

    /// <summary>런이 끝나거나 순간이동할 때 통과 상태가 남지 않게 한다.</summary>
    public void CancelDropThrough()
    {
        dropTimer = 0f;
        RestoreDroppedPlatforms();
    }

    bool CheckGrounded()
    {
        Vector2 feet = (Vector2)transform.position + Vector2.down * col.bounds.extents.y;
        return Physics2D.OverlapCircle(feet, groundCheckRadius, groundMask);
    }

    // ───────────────────────── IGrabbable (2지역 보스 "부서진 여신상" 잡기 패턴) ─────────────────────────
    // 보스가 끌어당기는 동안 일반 이동·중력을 꺼서 두 이동 방식이 충돌하지 않게 한다(사용자 2026-11-18
    // 요청서 6절 "Rigidbody2D가 있다면 기존 이동 방식과 충돌하지 않도록 처리"). 위치는 보스가
    // MoveGrabbedTo로 직접 넣고, Update/FixedUpdate는 맨 위에서 grabbed면 통째로 건너뛴다.

    public bool IsGrabbed => grabbed;

    public void BeginGrab(GameObject grabber)
    {
        if (grabbed) return;
        grabbed = true;
        CancelDropThrough(); // 끌려가는 동안 발판 통과 상태가 남아 있지 않게
        rb.gravityScale = 0f;
        rb.linearVelocity = Vector2.zero;
        inertialVx = 0f;
        GameInput.PlayerGrabbed = true; // 점프·공격·스킬 입력도 같이 잠긴다(GameInput.CombatKey)
    }

    public void MoveGrabbedTo(Vector2 position)
    {
        if (!grabbed) return;
        // rb.position만 바꾸면 transform은 다음 물리 스텝에야 따라온다(Teleport 주석과 같은 함정) —
        // 보스가 매 프레임 호출하므로 둘 다 바로 맞춰 둔다.
        transform.position = position;
        rb.position = position;
    }

    public void EndGrab()
    {
        if (!grabbed) return;
        grabbed = false;
        rb.gravityScale = 1f;
        GameInput.PlayerGrabbed = false;
    }

    void OnDisable()
    {
        // 플레이어 오브젝트가 꺼지거나 파괴될 때 — 붙잡은 쪽(보스)의 정리가 먼저 돌았어야 하지만,
        // 여기서도 방어적으로 풀어 둔다(사용자 2026-11-18 요청서 "플레이어가 잡힌 도중 사망하면
        // 이동 제한 및 잡기 상태를 정리한다").
        if (grabbed) EndGrab();
    }
}
}
