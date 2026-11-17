using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Combat;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 2지역 보스 **부서진 여신상** — 별도 명세서(`부서진_여신상_스킬_구현_명세서.docx`, 범위값) +
    /// 사용자 2026-11-18 요청서(확정 단일값, "첨부 문서를 참고하되 아래 요구사항을 우선 적용")대로.
    /// 1지역 <see cref="BlobLordBoss"/>와 같은 자리의 **다른 보스**다 — 2지역만 이걸 쓴다
    /// (`MonsterCatalog.regionBosses`, `Systems/EnemySetup.SetupBoss`).
    ///
    /// 공격은 둘, 둘 다 **준비 → 경고 → 공격 → 후딜레이** 순서다(요청서 2·3절, 명세서 2절과 같은 구조):
    /// - **잡기**: 방향 고정(<c>State.GrabPrep</c>) → 전방 직사각형 경고(<c>State.GrabWarn</c>) →
    ///   팔 뻗기(<c>State.GrabThrust</c>, 그 안에서 맞으면 즉시 성공) → 성공 시 끌어당김(<c>State.GrabPull</c>) →
    ///   짧게 붙잡아 둠(<c>State.GrabHold</c>, 끝날 때 피해 1회) → 후딜레이. 실패하면 끌어당기지 않고 바로 후딜레이
    ///   (요청서 2절 "잡기에 실패하면 플레이어를 끌어당기지 않고 바로 후딜레이 상태로 전환한다").
    /// - **휘두르기**: 방향 고정(<c>State.SweepPrep</c>) → 전방 부채꼴 경고(<c>State.SweepWarn</c>) →
    ///   판정(<c>State.SweepActive</c>, 범위 안이면 1회 피해) → 후딜레이.
    ///
    /// 고르기(요청서 4절): 둘 다 "쿨타임이 끝났고 사거리 안"이면 랜덤, 하나만 되면 그것, 둘 다 안 되면
    /// (너무 멀거나 둘 다 쿨) **접근**(기존 적 AI처럼 그냥 걸어간다, `Boss.MoveSpeed` 재사용) — 요청서 6절
    /// "기본 이동은 기존 적 AI 시스템을 우선 재사용한다"를 그대로 따른 것. 같은 패턴 연속 사용은 금지
    /// (<see cref="MaxConsecutive"/>).
    ///
    /// 방향은 **각 패턴의 준비 단계가 끝나는 시점에 한 번 고정**하고 공격이 끝날 때까지 바꾸지 않는다
    /// (요청서 "공격 시작 전 플레이어 위치 기준으로 방향 결정, 공격 중 과도한 방향 전환 지양" / 명세서
    /// 공통 규칙). 몸통 접촉 피해는 없다(<see cref="ContactDamageMultiplier"/> = 0) — 1지역 덩어리
    /// 지배자와 같은 이유로, 피해는 예고된 두 패턴으로만 들어간다(두 문서 모두 몸통 접촉을 언급하지
    /// 않는다 — "확인 필요": 팀장이 몸통 접촉 피해를 원하면 이 값을 조정할 것).
    ///
    /// 판정은 `Physics2D.OverlapBox` 대신 **수동 거리/각도 계산**으로 한다(<see cref="InGrabBox"/>·
    /// <see cref="InSweepArea"/>) — 요청서도 "OverlapBox 방식으로 구현할 수 있다"(허용, 강제 아님)라고
    /// 적어 두었고, 기존 보스(`Boss.DoSlam`·`BlobLordBoss.InImpactArea`)가 전부 이 방식이라 맞췄다.
    ///
    /// 이동은 `EnemyMove`에 수평 속도(<see cref="IEnemyMotion"/>)만 넘긴다 — Y는 기본 중력/착지를 그대로
    /// 쓴다(이 보스는 땅 위에서만 움직이는 근접형이라 1지역 보스처럼 Y를 직접 정할 필요가 없다).
    ///
    /// **애니메이션 제외**(사용자 2026-11-18 요청서 1절 "현재는 애니메이션을 사용하지 않으므로 Animator
    /// 및 애니메이션 상태 전환은 구현하지 마") — 그림은 아직 정식 아트를 안 입혔다(업로드된 스프라이트
    /// 프레임은 이 작업 범위 밖, `docs/asset-swap-points.md`에 등록되는 다음 작업에서 교체할 것). 지금은
    /// 공통 보스와 같은 임시 크기(<see cref="VisualSize"/>)로 선다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMove))]
    [RequireComponent(typeof(EnemyHealth))]
    public class BrokenGoddessBoss : MonoBehaviour, IEnemyMotion, IEnemyContactDamageModifier, IGrabExempt
    {
        // ── 몸(정식 그림 들어오기 전 — 공통 보스와 같은 임시 크기) ──
        public static readonly Vector2 VisualSize = new Vector2(EntitySizeConfig.BossWidth, EntitySizeConfig.BossHeight);
        public const float HitRadius = 0.65f; // Mathf.Min(BossWidth, BossHeight) / 2

        // ── 패턴 1 잡기(사용자 2026-11-18 요청서 2절 "초기 설정값" — 명세서 범위보다 이 값을 우선한다) ──
        public const float GrabPrepTime = 0.6f;     // 공격 준비 시간
        public const float GrabWarnTime = 0.5f;     // 경고 표시 시간
        public const float GrabRange = 3f;          // 잡기 사거리(유닛)
        public const float GrabHeight = 1.5f;       // 잡기 판정 높이(유닛)
        public const float GrabThrustTime = 0.25f;  // 팔 뻗기 시간(= 판정 지속)
        public const float GrabPullTime = 0.5f;     // 끌어당기기 시간
        public const float GrabHoldTime = 0.2f;     // 끌어당긴 후 유지 시간
        public const float GrabDamagePct = 0.20f;   // 공격 피해량 — 플레이어 최대 체력의 20%
        public const float GrabRecoverTime = 1.2f;  // 후딜레이
        public const float GrabCooldown = 5f;       // 쿨타임
        /// <summary>끌어당길 때 보스 앞 어디까지 끌어오는지 — 두 문서 모두 "보스 앞으로"만 말하고 정확한
        /// 거리는 안 정해서 보조로 둔 값(보스 몸 절반 + 팔 길이 느낌의 여유).</summary>
        public const float GrabAnchorOffset = 0.7f;

        // ── 패턴 2 휘두르기(사용자 2026-11-18 요청서 3절 "초기 설정값") ──
        public const float SweepPrepTime = 0.5f;    // 공격 준비 시간
        public const float SweepWarnTime = 0.6f;    // 경고 표시 시간
        public const float SweepRadius = 3f;        // 공격 범위(반경 유닛)
        public const float SweepHalfAngleDeg = 80f; // 전방 160도의 절반
        public const float SweepActiveTime = 0.3f;  // 실제 공격 판정 시간
        public const float SweepDamagePct = 0.15f;  // 공격 피해량 — 플레이어 최대 체력의 15%
        public const float SweepRecoverTime = 1f;   // 후딜레이
        public const float SweepCooldown = 4f;      // 쿨타임

        // ── 고르기(요청서 4절) ──
        public const int MaxConsecutive = 1;        // 같은 패턴 연속 금지
        public const float FirstDelay = 1f;         // 스폰 직후 첫 공격까지 — 플레이어가 상황을 볼 시간
        public const float IdleGap = 0.3f;          // 후딜레이 뒤 다음 선택까지 짧은 틈
        /// <summary>대상 최대 체력을 못 읽을 때(테스트 등) — 플레이어 기본 최대 체력.</summary>
        public const float FallbackTargetMaxHp = 100f;

        public enum Pattern { None, Grab, Sweep }
        public enum State
        {
            Idle,
            GrabPrep, GrabWarn, GrabThrust, GrabPull, GrabHold, GrabRecover,
            SweepPrep, SweepWarn, SweepActive, SweepRecover,
        }

        [Tooltip("공격 경고 표시 프리팹(Prefabs/Combat/GroundWarning) — 잡기·휘두르기가 하나씩 따로 인스턴스를 만든다(1지역 보스와 같은 방식).")]
        [SerializeField] GroundWarning warningPrefab;

        /// <summary>프리팹 검증·테스트가 읽고 넣는다.</summary>
        public GroundWarning WarningPrefab { get => warningPrefab; set => warningPrefab = value; }

        EnemyMove mover;
        EnemyHealth health;
        SpriteRenderer body;
        GroundWarning grabWarning, sweepWarning;

        State state = State.Idle;
        float stateT, stateDur = FirstDelay;
        Pattern last = Pattern.None;
        int consecutive;
        float clock, grabReadyAt, sweepReadyAt;

        /// <summary>공격 준비가 끝나는 순간 고정한 방향(1 또는 -1) — 공격이 끝날 때까지 안 바뀐다.</summary>
        int lockedFacing = 1;
        bool grabHitDone, sweepHitDone;

        // 붙잡은 대상 — 보스가 죽거나 꺼질 때 반드시 풀어 줘야 하므로 들고 있는다.
        GameObject grabbedTarget;
        float grabStartX, grabHeldY, grabAnchorX;

        public State CurrentState => state;
        public Pattern LastPattern => last;
        public GroundWarning GrabWarning => grabWarning;
        public GroundWarning SweepWarning => sweepWarning;
        public bool IsGrabbing => grabbedTarget != null;

        /// <summary>테스트·연출이 듣는다: 누구를 붙잡았는지.</summary>
        public event System.Action<GameObject> Grabbed;
        /// <summary>테스트·연출이 듣는다: 휘두르기가 맞았는지(중심점, 맞았는지).</summary>
        public event System.Action<Vector2, bool> SweptHit;

        /// <summary>몸통 접촉 피해 없음 — 피해는 예고된 두 패턴으로만(두 문서 모두 몸통 접촉을 요구하지 않는다).</summary>
        public float ContactDamageMultiplier => 0f;

        void Awake()
        {
            mover = GetComponent<EnemyMove>();
            health = GetComponent<EnemyHealth>();
            body = GetComponent<SpriteRenderer>();

            if (warningPrefab != null)
            {
                grabWarning = Instantiate(warningPrefab);
                sweepWarning = Instantiate(warningPrefab);
                grabWarning.name = "BrokenGoddessGrabWarning";
                sweepWarning.name = "BrokenGoddessSweepWarning";
                RunTransient.Mark(grabWarning.gameObject);
                RunTransient.Mark(sweepWarning.gameObject);
            }
        }

        void OnDestroy()
        {
            if (grabWarning != null) Destroy(grabWarning.gameObject);
            if (sweepWarning != null) Destroy(sweepWarning.gameObject);
            ReleaseGrabbedTarget();
        }

        void OnDisable()
        {
            // 보스가 비활성화돼도(파괴되기 전 포함) 붙잡고 있던 플레이어는 즉시 풀어준다
            // (요청서 "보스가 사망하거나 공격이 중단되면 잡힌 플레이어를 즉시 해제한다").
            ReleaseGrabbedTarget();
        }

        void ReleaseGrabbedTarget()
        {
            if (grabbedTarget == null) return;
            var g = grabbedTarget.GetComponent<IGrabbable>();
            g?.EndGrab();
            grabbedTarget = null;
        }

        // ───────────────────────── 매 물리 프레임 ─────────────────────────

        public float GetHorizontalSpeed(float dt, Transform target, float baseSpeed)
        {
            if (health != null && health.IsDead) return 0f;
            clock += dt;
            stateT += dt;

            bool targetGone = !TargetAlive(target);
            if (targetGone && state != State.Idle)
            {
                // 요청서 "플레이어가 사망하면 공격 실행을 중단한다" — 진행 중인 경고·붙잡기를 정리하고 대기로.
                AbortToIdle();
                return 0f;
            }

            float x = transform.position.x;
            float desiredX = x;

            switch (state)
            {
                case State.Idle:
                    desiredX = IdleApproach(target, x, dt);
                    if (stateT >= stateDur && !targetGone) TryStartNextPattern(target);
                    break;

                case State.GrabPrep:
                    if (stateT >= GrabPrepTime) BeginGrabWarn();
                    break;

                case State.GrabWarn:
                    if (stateT >= GrabWarnTime) BeginGrabThrust();
                    break;

                case State.GrabThrust:
                    if (!grabHitDone && !targetGone && InGrabBox(transform.position, lockedFacing, GrabRange, GrabHeight, target.position))
                        TryGrab(target);
                    if (stateT >= GrabThrustTime && !grabHitDone)
                        Enter(State.GrabRecover, GrabRecoverTime); // 실패 — 끌어당기지 않고 바로 후딜레이(요청서 2절)
                    break;

                case State.GrabPull:
                    UpdatePull();
                    if (stateT >= GrabPullTime) Enter(State.GrabHold, GrabHoldTime);
                    break;

                case State.GrabHold:
                    HoldGrabbed();
                    if (stateT >= GrabHoldTime) FinishGrab();
                    break;

                case State.GrabRecover:
                    if (stateT >= GrabRecoverTime) ToIdle();
                    break;

                case State.SweepPrep:
                    if (stateT >= SweepPrepTime) BeginSweepWarn();
                    break;

                case State.SweepWarn:
                    if (stateT >= SweepWarnTime) BeginSweepActive();
                    break;

                case State.SweepActive:
                    if (!sweepHitDone && !targetGone && InSweepArea(transform.position, lockedFacing, SweepRadius, SweepHalfAngleDeg, target.position))
                        ApplySweepHit(target);
                    if (stateT >= SweepActiveTime) Enter(State.SweepRecover, SweepRecoverTime);
                    break;

                case State.SweepRecover:
                    if (stateT >= SweepRecoverTime) ToIdle();
                    break;
            }

            return dt > 0f ? (desiredX - x) / dt : 0f;
        }

        /// <summary>
        /// 대기 중 접근 — 요청서 6절 "기본 이동은 기존 적 AI 시스템을 우선 재사용한다"를 따라 새 추적
        /// 로직을 만들지 않고 공통 보스의 걷기 속도(<see cref="Boss.MoveSpeed"/>)로 그냥 걸어간다.
        /// 두 패턴 중 더 먼 사거리 안에 들어오면 멈추고 고르기를 기다린다(요청서 "플레이어가 지나치게
        /// 멀 경우 무리하게 공격하지 않고 접근 상태로 전환한다"의 반대 방향 — 가까우면 접근을 멈춘다).
        /// </summary>
        float IdleApproach(Transform target, float x, float dt)
        {
            if (target == null) return x;
            float dx = target.position.x - x;
            float dist = Mathf.Abs(dx);
            float stopDist = Mathf.Max(GrabRange, SweepRadius);
            if (dist <= stopDist)
            {
                if (body != null) body.flipX = mover.Direction < 0;
                return x;
            }
            int dir = dx > 0f ? 1 : -1;
            mover.SetDirection(dir);
            if (body != null) body.flipX = dir < 0;
            return x + dir * Boss.MoveSpeed * dt;
        }

        // ───────────────────────── 고르기(요청서 4절) ─────────────────────────

        void TryStartNextPattern(Transform target)
        {
            var next = Choose(target);
            if (next == Pattern.None) return; // 둘 다 쿨이거나 너무 멀다 — 계속 접근/대기
            consecutive = next == last ? consecutive + 1 : 1;
            last = next;
            LockFacing(target);

            if (next == Pattern.Grab)
            {
                grabReadyAt = clock + GrabCooldown;
                grabHitDone = false;
                Enter(State.GrabPrep, GrabPrepTime);
            }
            else
            {
                sweepReadyAt = clock + SweepCooldown;
                sweepHitDone = false;
                Enter(State.SweepPrep, SweepPrepTime);
            }
        }

        /// <summary>
        /// 다음 패턴(요청서 4절). 쓸 수 있는 것 = 쿨이 끝났고 사거리 안인 것 − 연속 제한에 걸린 것.
        /// 둘 다 되면 랜덤, 하나만 되면 그것, 둘 다 안 되면 <see cref="Pattern.None"/>(접근을 계속한다).
        /// </summary>
        public Pattern Choose(Transform target)
        {
            if (target == null) return Pattern.None;
            float distance = Mathf.Abs(target.position.x - transform.position.x);
            bool grabOk = clock >= grabReadyAt && distance <= GrabRange
                          && !(last == Pattern.Grab && consecutive >= MaxConsecutive);
            bool sweepOk = clock >= sweepReadyAt && distance <= SweepRadius
                           && !(last == Pattern.Sweep && consecutive >= MaxConsecutive);
            if (!grabOk && !sweepOk) return Pattern.None;
            if (grabOk && !sweepOk) return Pattern.Grab;
            if (sweepOk && !grabOk) return Pattern.Sweep;
            return Random.value < 0.5f ? Pattern.Grab : Pattern.Sweep;
        }

        /// <summary>공격 시작 전 방향을 한 번 고정한다 — 공격이 끝날 때까지 다시 안 부른다.</summary>
        void LockFacing(Transform target)
        {
            float dx = target.position.x - transform.position.x;
            lockedFacing = dx >= 0f ? 1 : -1;
            mover.SetDirection(lockedFacing);
            if (body != null) body.flipX = lockedFacing < 0;
        }

        void AbortToIdle()
        {
            if (grabWarning != null) grabWarning.Hide();
            if (sweepWarning != null) sweepWarning.Hide();
            ReleaseGrabbedTarget();
            Enter(State.Idle, IdleGap);
        }

        void ToIdle() => Enter(State.Idle, IdleGap);

        void Enter(State s, float dur)
        {
            state = s; stateT = 0f; stateDur = dur;
        }

        // ───────────────────────── 패턴 1 잡기 ─────────────────────────

        void BeginGrabWarn()
        {
            if (grabWarning != null)
            {
                // 전방으로 GrabRange만큼 뻗는 직사각형 — 보스 위치에서 고정 방향으로 절반만큼 떨어진 중심.
                Vector2 center = (Vector2)transform.position + new Vector2(lockedFacing * GrabRange / 2f, 0f);
                grabWarning.ShowCustom(center, DotShapes.Rect(GrabRange, GrabHeight));
                grabWarning.Lock(); // 방향이 이미 고정됐으니 따라가는 단계 없이 바로 "고정" 표시(요청서 "방향 전환 지양")
            }
            Enter(State.GrabWarn, GrabWarnTime);
        }

        void BeginGrabThrust()
        {
            if (grabWarning != null) grabWarning.Flash(); // 경고가 사라지는 시점 = 판정 시작 시점(명세서 공통 규칙)
            Enter(State.GrabThrust, GrabThrustTime);
        }

        void TryGrab(Transform target)
        {
            var grabbable = target.GetComponent<IGrabbable>();
            // 못 잡는 대상이거나(인터페이스 없음) 이미 다른 데 붙잡혀 있으면 이번 시도는 실패로 둔다 —
            // GrabThrust 시간이 다 되면 위에서 실패 후딜레이로 넘어간다.
            if (grabbable == null || grabbable.IsGrabbed) return;

            grabHitDone = true;
            grabbedTarget = target.gameObject;
            grabStartX = target.position.x;
            grabHeldY = target.position.y; // 끌려오는 동안 높이는 바꾸지 않는다(요청서 "지형을 통과하지 않도록")
            grabAnchorX = Mathf.Clamp(transform.position.x + lockedFacing * GrabAnchorOffset,
                                       FieldBounds.MinX + 0.1f, FieldBounds.MaxX - 0.1f); // 맵 밖으로 안 나가게 보정

            grabbable.BeginGrab(gameObject);
            Grabbed?.Invoke(target.gameObject);
            Enter(State.GrabPull, GrabPullTime);
        }

        void UpdatePull()
        {
            if (grabbedTarget == null) return;
            float t = EaseOut(Mathf.Clamp01(stateT / GrabPullTime));
            float newX = Mathf.Lerp(grabStartX, grabAnchorX, t);
            var g = grabbedTarget.GetComponent<IGrabbable>();
            g?.MoveGrabbedTo(new Vector2(newX, grabHeldY));
        }

        void HoldGrabbed()
        {
            if (grabbedTarget == null) return;
            var g = grabbedTarget.GetComponent<IGrabbable>();
            g?.MoveGrabbedTo(new Vector2(grabAnchorX, grabHeldY)); // 유지 시간 동안 자리 그대로
        }

        /// <summary>끌어당기기가 끝난 시점 — 피해를 1회 주고 풀어준다(요청서 "끌어당기기가 끝나면 피해를 준다 → 이동 제한을 해제한다").</summary>
        void FinishGrab()
        {
            if (grabbedTarget != null)
            {
                var d = grabbedTarget.GetComponent<IDamageable>();
                if (d != null && !d.IsDead)
                    d.TakeDamage(TargetMaxHp(grabbedTarget.transform) * GrabDamagePct, gameObject);
            }
            ReleaseGrabbedTarget();
            Enter(State.GrabRecover, GrabRecoverTime);
        }

        /// <summary>잡기 판정 — 보스가 보는 방향(<paramref name="facing"/>) 전방, 사거리 <paramref name="range"/> 안·
        /// 높이 <paramref name="height"/> 범위 안이면 잡힌다. 경고 사각형과 정확히 같은 영역이다.</summary>
        public static bool InGrabBox(Vector2 origin, int facing, float range, float height, Vector2 targetPos)
        {
            float forward = (targetPos.x - origin.x) * facing; // 보는 방향 기준 전방 거리(음수면 뒤쪽)
            if (forward < 0f || forward > range) return false;
            return Mathf.Abs(targetPos.y - origin.y) <= height / 2f;
        }

        // ───────────────────────── 패턴 2 휘두르기 ─────────────────────────

        void BeginSweepWarn()
        {
            if (sweepWarning != null)
            {
                // 부채꼴 그림은 항상 +X로 열리게 굽는다 — 왼쪽을 보고 있으면 가로로 뒤집어 보여준다.
                var scale = new Vector3(lockedFacing, 1f, 1f);
                sweepWarning.ShowCustom(transform.position, DotShapes.Wedge(SweepRadius, SweepHalfAngleDeg * Mathf.Deg2Rad), scale);
                sweepWarning.Lock();
            }
            Enter(State.SweepWarn, SweepWarnTime);
        }

        void BeginSweepActive()
        {
            if (sweepWarning != null) sweepWarning.Flash(); // 경고 사라짐 = 판정 시작(명세서 공통 규칙)
            Enter(State.SweepActive, SweepActiveTime);
        }

        void ApplySweepHit(Transform target)
        {
            sweepHitDone = true; // 다단 피격 방지 — 이 공격에서는 한 번만
            var d = target.GetComponent<IDamageable>();
            bool hit = d != null && !d.IsDead;
            if (hit) d.TakeDamage(TargetMaxHp(target) * SweepDamagePct, gameObject);
            SweptHit?.Invoke(transform.position, hit);
        }

        /// <summary>휘두르기 판정 — 보스가 보는 방향을 중심으로 ±<paramref name="halfAngleDeg"/>도, 반지름
        /// <paramref name="radius"/> 안이면 맞는다. 경고 부채꼴과 정확히 같은 영역이다.</summary>
        public static bool InSweepArea(Vector2 origin, int facing, float radius, float halfAngleDeg, Vector2 targetPos)
        {
            Vector2 d = targetPos - origin;
            float dist = d.magnitude;
            if (dist > radius || dist < 0.0001f) return false;
            float angleToTarget = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float facingAngle = facing >= 0 ? 0f : 180f;
            return Mathf.Abs(Mathf.DeltaAngle(facingAngle, angleToTarget)) <= halfAngleDeg;
        }

        // ───────────────────────── 공통 ─────────────────────────

        static bool TargetAlive(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            var d = target.GetComponent<IDamageable>();
            return d == null || !d.IsDead;
        }

        static float TargetMaxHp(Transform target)
        {
            var h = target != null ? target.GetComponent<IHealthView>() : null;
            return h != null && h.MaxHp > 0f ? h.MaxHp : FallbackTargetMaxHp;
        }

        static float EaseOut(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t); }
    }
}
