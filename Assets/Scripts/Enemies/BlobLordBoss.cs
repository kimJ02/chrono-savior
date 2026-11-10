using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.World;
using ChronoSavior.Combat;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 1지역 보스 **덩어리 지배자** — 팀원 명세(`덩어리_지배자_스킬_구현_명세서.docx`, 2026-11-10)대로. 냇물·이끼·조약돌을 흡수해 비대해진 덩어리.
    /// 원본 HTML 게임의 공통 보스(<see cref="Boss"/>)와 **다른 보스**다 — 1지역만 이걸 쓴다(`MonsterCatalog.regionBosses`, 사용자 2026-11-10 "1지역 보스만").
    ///
    /// 공격은 둘, 둘 다 **예고(빨간 바닥 경고) → 공격 → 후딜** 순서다(명세 2절):
    /// - **추적 점프 강하**: 몸을 낮춤(<see cref="JumpPrep"/>) → 뛰어오름(<see cref="JumpRise"/>) → 공중에서 경고가 플레이어 X를 따라감 →
    ///   착지 <see cref="LockBeforeImpact"/>초 전에 고정 → 내려찍기(반경 <see cref="ImpactRadius"/>, 최대 체력 <see cref="ImpactDamagePct"/>) → 후딜.
    ///   **뜬 동안 무적**(<see cref="IDamageProtection"/>, 후딜 시작에 풀림 — 명세 권장).
    /// - **대형 조약돌 투척**: 돌을 들어 올림(<see cref="ThrowPrep"/>) → 조준 1회 고정 + 날아갈 길·떨어질 자리에 빨간 경고(<see cref="ThrowWarn"/>) →
    ///   직선으로 던짐(<see cref="BoulderSpeed"/>, 최대 체력 <see cref="BoulderDamagePct"/>) → 후딜.
    ///
    /// 고르기(명세 5절, Codex 상의로 우선순위 확정): 같은 패턴 연속 금지(<see cref="MaxConsecutive"/>) 안에서
    /// ① 플레이어가 <see cref="StationaryTime"/>초 동안 <see cref="StationaryRange"/> 안에 머묾 → 점프 ② 체력 <see cref="LowHpRatio"/> 이하 → 점프
    /// ③ 멀다(<see cref="FarDistance"/>) → 투척 ④ 직전과 반대.
    ///
    /// Codex 상의로 정한 것(명세 밖): 몸통 접촉 피해 없음(피해는 예고된 공격으로만), 걷기·졸개 소환 없음(제자리), 경고와 착지는
    /// 플레이어 발밑 **표면**(바닥 또는 발판 윗면 — `FieldLayout.SurfaceBelow`)에 — 발판 위가 안전지대가 되지 않게.
    ///
    /// 이동은 `EnemyMove`에 수평 속도(<see cref="IEnemyMotion"/>)와 Y(<see cref="IEnemyVerticalMotion"/>)를 넘겨서 한다 — Y는 언제나
    /// 이 스크립트가 정한다(서 있을 땐 표면 위, 뛸 땐 포물선). 그래서 땅과의 물리 충돌을 끈다(<see cref="Awake"/>).
    /// 구성은 프리팹(`Prefabs/Enemies/Enemy_BlobLord` — 경고·조약돌 프리팹, 들어 올린 돌 그림, 투척 경로 선).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMove))]
    [RequireComponent(typeof(EnemyHealth))]
    public class BlobLordBoss : MonoBehaviour, IEnemyMotion, IEnemyVerticalMotion, IEnemyContactDamageModifier,
                                IDamageProtection, IGrabExempt
    {
        // ── 몸 ──
        /// <summary>그림 크기(가로·세로, 유닛) — 중대형(명세 1절). 그림 128×104px를 도트 배율 2로.</summary>
        public static readonly Vector2 VisualSize = new Vector2(2.0f, 1.625f);
        /// <summary>피격 판정 원 반지름.</summary>
        public const float HitRadius = 0.78f;
        /// <summary>몸 중심이 서 있는 표면보다 위에 있는 높이(그림 세로 절반).</summary>
        public static float HalfHeight => VisualSize.y / 2f;

        // ── 패턴 1 추적 점프 강하(명세 3-3, 범위 중 값은 Codex 상의로 확정) ──
        public const float JumpPrep = 0.6f;          // 준비 0.5~0.7
        public const float JumpRise = 0.35f;         // 뛰어오르는 시간
        public const float JumpTrack = 0.9f;         // 공중 추적 0.8~1.2 (고정 구간 포함)
        public const float JumpFall = 0.2f;          // 빠른 낙하
        public const float LockBeforeImpact = 0.3f;  // 착지 0.3초 전 고정
        public const float JumpRecover = 1.2f;       // 후딜 1.0~1.5
        public const float JumpCooldown = 5f;        // 4~6
        public const float ImpactRadius = 1.8f;      // 1.5~2.2
        public const float ImpactDamagePct = 0.20f;  // 최대 체력 15~25%
        /// <summary>떠 있는 높이 — 바닥 기준. 위층 발판(2.70)보다 높아야 화면 위로 '뛰어올랐다'가 읽힌다.</summary>
        public const float ApexHeight = 4.6f;
        /// <summary>경고가 플레이어를 따라가는 최고 속도(유닛/초) — 순간이동하면 '따라온다'가 안 보인다.</summary>
        public const float MarkerFollowSpeed = 7f;

        // ── 패턴 2 대형 조약돌 투척(명세 4-3) ──
        public const float ThrowPrep = 0.7f;         // 0.6~0.8
        public const float ThrowWarn = 0.6f;         // 0.5~0.8
        public const float BoulderSpeed = 8f;        // 6~10
        public const float BoulderDamagePct = 0.14f; // 10~18%
        public const float ThrowRecover = 1.0f;      // 0.8~1.2
        public const float ThrowCooldown = 4f;       // 3~5
        public const float MinThrowDistance = 3.5f;  // 근거리 최소 투척 거리 보정
        /// <summary>떨어질 자리 경고 반경 — 돌 크기(0.70×0.60)보다 조금 크게.</summary>
        public const float BoulderWarnRadius = 0.5f;
        /// <summary>들어 올린 돌의 위치 — 몸 꼭대기 위.</summary>
        public const float HoldAbove = 0.35f;

        // ── 고르기(명세 5절) ──
        public const float FarDistance = 6f;
        public const float StationaryTime = 1.5f;
        public const float StationaryRange = 0.5f;
        public const float LowHpRatio = 0.4f;
        public const int MaxConsecutive = 1;         // 같은 패턴 연속 금지
        public const float FirstDelay = 1.0f;
        public const float IdleGap = 0.4f;
        /// <summary>대상 최대 체력을 못 읽을 때(테스트 등) — 플레이어 기본 최대 체력.</summary>
        public const float FallbackTargetMaxHp = 100f;

        public enum Pattern { None, Jump, Throw }
        public enum State { Idle, JumpPrep, JumpRise, JumpTrack, JumpFall, JumpRecover, ThrowPrep, ThrowWarn, ThrowRecover }

        [SerializeField] GroundWarning warningPrefab;
        [SerializeField] BoulderProjectile boulderPrefab;
        [Tooltip("들어 올린 돌(투척 준비 중에만 보임) — 프리팹 자식.")]
        [SerializeField] SpriteRenderer heldBoulder;
        [Tooltip("투척 경로 경고 선 — 프리팹 자식(VfxUnlit 머티리얼).")]
        [SerializeField] LineRenderer throwPathLine;

        public GroundWarning WarningPrefab { get => warningPrefab; set => warningPrefab = value; }
        public BoulderProjectile BoulderPrefab { get => boulderPrefab; set => boulderPrefab = value; }

        EnemyMove mover;
        EnemyHealth health;
        SpriteRenderer body;
        GroundWarning jumpWarning, throwWarning;

        State state = State.Idle;
        float stateT, stateDur = FirstDelay;
        Pattern last = Pattern.None;
        int consecutive;
        float clock, jumpReadyAt, throwReadyAt;

        // 점프
        float launchX, launchY, markerX, markerSurfaceY, apexY;
        bool locked;
        // 투척
        Vector2 throwFrom, throwTo;
        // 정지 감지
        float anchorX, anchorSince;
        bool hasAnchor;
        // 서 있는 표면(몸 중심 Y = 이것 + HalfHeight)
        float standSurfaceY = float.NaN;
        Vector3 baseScale;
        bool baseScaleKnown;

        public State CurrentState => state;
        public Pattern LastPattern => last;
        public bool IsAirborne => state == State.JumpRise || state == State.JumpTrack || state == State.JumpFall;
        public GroundWarning JumpWarning => jumpWarning;
        public GroundWarning ThrowWarning => throwWarning;
        public Vector2 ThrowTarget => throwTo;
        public LineRenderer ThrowPathLine => throwPathLine;

        /// <summary>도약~낙하 동안 무적(명세 2절 "도약 중 무적 / 후딜 중 피격 가능").</summary>
        public bool IsDamageProtected => IsAirborne;
        /// <summary>몸통 접촉 피해 없음 — 피해는 예고된 공격으로만(Codex 상의, `EnemyMove.TryAttack`이 0이면 건너뜀).</summary>
        public float ContactDamageMultiplier => 0f;

        /// <summary>테스트·착지 효과가 듣는다: 내려찍기가 떨어진 자리, 맞았는지.</summary>
        public event System.Action<Vector2, bool> Impacted;
        public event System.Action<BoulderProjectile> Thrown;

        void Awake()
        {
            mover = GetComponent<EnemyMove>();
            health = GetComponent<EnemyHealth>();
            body = GetComponent<SpriteRenderer>();
            // Y는 이 스크립트가 정한다 — 땅·발판 콜라이더와 부딪히면 내려찍기가 발판에 걸리거나 밀려난다.
            var rb = GetComponent<Rigidbody2D>();
            int ground = LayerMask.NameToLayer("Ground");
            if (rb != null && ground >= 0) rb.excludeLayers |= 1 << ground;

            if (warningPrefab != null)
            {
                jumpWarning = Instantiate(warningPrefab);
                throwWarning = Instantiate(warningPrefab);
                jumpWarning.name = "BlobLordJumpWarning";
                throwWarning.name = "BlobLordThrowWarning";
                RunTransient.Mark(jumpWarning.gameObject);
                RunTransient.Mark(throwWarning.gameObject);
            }
            if (heldBoulder != null) heldBoulder.enabled = false;
            if (throwPathLine != null) throwPathLine.enabled = false;
        }

        void Start()
        {
            // 스포너가 크기를 맞춘 뒤 — 발을 서 있는 표면에 붙인다.
            Vector3 p = transform.position;
            standSurfaceY = FieldLayout.SurfaceBelow(p.x, p.y);
            transform.position = new Vector3(p.x, standSurfaceY + HalfHeight, p.z);
        }

        void OnDestroy()
        {
            if (jumpWarning != null) Destroy(jumpWarning.gameObject);
            if (throwWarning != null) Destroy(throwWarning.gameObject);
        }

        // ───────────────────────── 매 물리 프레임 ─────────────────────────

        public float GetHorizontalSpeed(float dt, Transform target, float baseSpeed)
        {
            if (!baseScaleKnown) { baseScale = transform.localScale; baseScaleKnown = true; }
            if (float.IsNaN(standSurfaceY)) standSurfaceY = FieldLayout.SurfaceBelow(transform.position.x, transform.position.y);
            clock += dt;
            stateT += dt;
            TrackStationary(target);

            bool targetGone = !TargetAlive(target);
            float x = transform.position.x;
            float desiredX = x;

            switch (state)
            {
                case State.Idle:
                    Face(target);
                    if (stateT >= stateDur && !targetGone) StartNextPattern(target);
                    break;

                case State.JumpPrep:
                    Face(target);
                    Squash(Mathf.Clamp01(stateT / JumpPrep) * -0.18f);
                    if (stateT >= JumpPrep) BeginRise(target);
                    break;

                case State.JumpRise:
                    UpdateMarker(target, dt);
                    desiredX = Mathf.Lerp(launchX, markerX, EaseOut(stateT / JumpRise));
                    Squash(0.12f);
                    if (stateT >= JumpRise) Enter(State.JumpTrack, JumpTrack);
                    break;

                case State.JumpTrack:
                    // 착지(= 추적 끝 + 낙하)까지 LockBeforeImpact가 남으면 고정
                    if (!locked && (JumpTrack - stateT) + JumpFall <= LockBeforeImpact) LockMarker();
                    if (!locked) UpdateMarker(target, dt);
                    desiredX = markerX;
                    Squash(0f);
                    if (stateT >= JumpTrack) { if (!locked) LockMarker(); Enter(State.JumpFall, JumpFall); }
                    break;

                case State.JumpFall:
                    desiredX = markerX;
                    Squash(0.15f);
                    if (stateT >= JumpFall) Impact(target);
                    break;

                case State.JumpRecover:
                    Squash(-0.2f * (1f - Mathf.Clamp01(stateT / 0.25f)));
                    if (stateT >= JumpRecover) ToIdle();
                    break;

                case State.ThrowPrep:
                    Face(target);
                    ShowHeld(Mathf.Clamp01(stateT / ThrowPrep));
                    if (stateT >= ThrowPrep) BeginThrowWarn(target);
                    break;

                case State.ThrowWarn:
                    ShowHeld(1f);
                    if (stateT >= ThrowWarn) Throw(target);
                    break;

                case State.ThrowRecover:
                    if (stateT >= ThrowRecover) ToIdle();
                    break;
            }

            // EnemyMove가 넉백을 더하므로 공중에선 그만큼 빼서 정확히 표식 위로 간다(넉백 배수 0.08이라 작다).
            return dt > 0f ? (desiredX - x) / dt : 0f;
        }

        public float GetVerticalPosition(float dt, Transform target, float currentY)
        {
            switch (state)
            {
                case State.JumpRise:
                    return Mathf.Lerp(launchY, apexY, EaseOut(Mathf.Clamp01(stateT / JumpRise)));
                case State.JumpTrack:
                    return apexY + 0.08f * Mathf.Sin(stateT * 9f);
                case State.JumpFall:
                {
                    float t = Mathf.Clamp01(stateT / JumpFall);
                    return Mathf.Lerp(apexY, markerSurfaceY + HalfHeight, t * t);
                }
                default:
                    return (float.IsNaN(standSurfaceY) ? FieldBoundsGround() : standSurfaceY) + HalfHeight;
            }
        }

        static float FieldBoundsGround() => FieldBounds.GroundY;

        // ───────────────────────── 고르기 ─────────────────────────

        void StartNextPattern(Transform target)
        {
            var next = Choose(target);
            if (next == Pattern.None) return; // 둘 다 쿨 — 기다린다
            consecutive = next == last ? consecutive + 1 : 1;
            last = next;
            if (next == Pattern.Jump) { jumpReadyAt = clock + JumpCooldown; Enter(State.JumpPrep, JumpPrep); }
            else { throwReadyAt = clock + ThrowCooldown; Enter(State.ThrowPrep, ThrowPrep); }
        }

        /// <summary>
        /// 다음 패턴(명세 5절). 쓸 수 있는 것 = 쿨이 돈 것 − 연속 제한에 걸린 것. 둘 다 되면 우선순위 ①정지 ②저체력 ③멀면 투척 ④직전 반대.
        /// </summary>
        public Pattern Choose(Transform target)
        {
            bool jumpOk = clock >= jumpReadyAt && !(last == Pattern.Jump && consecutive >= MaxConsecutive);
            bool throwOk = clock >= throwReadyAt && !(last == Pattern.Throw && consecutive >= MaxConsecutive);
            if (!jumpOk && !throwOk) return Pattern.None;
            if (jumpOk && !throwOk) return Pattern.Jump;
            if (throwOk && !jumpOk) return Pattern.Throw;

            if (IsTargetStationary) return Pattern.Jump;
            if (health != null && health.MaxHp > 0f && health.CurrentHp / health.MaxHp <= LowHpRatio) return Pattern.Jump;
            if (target != null && Mathf.Abs(target.position.x - transform.position.x) >= FarDistance) return Pattern.Throw;
            return last == Pattern.Jump ? Pattern.Throw : Pattern.Jump;
        }

        public bool IsTargetStationary => hasAnchor && clock - anchorSince >= StationaryTime;

        void TrackStationary(Transform target)
        {
            if (target == null) { hasAnchor = false; return; }
            float tx = target.position.x;
            if (!hasAnchor || Mathf.Abs(tx - anchorX) > StationaryRange)
            {
                anchorX = tx; anchorSince = clock; hasAnchor = true;
            }
        }

        // ───────────────────────── 점프 ─────────────────────────

        void BeginRise(Transform target)
        {
            launchX = transform.position.x;
            launchY = transform.position.y;
            apexY = FieldBounds.GroundY + ApexHeight;
            locked = false;
            markerX = ClampMarkerX(target != null ? target.position.x : launchX);
            markerSurfaceY = SurfaceUnder(markerX, target);
            if (jumpWarning != null) jumpWarning.Show(new Vector2(markerX, markerSurfaceY), ImpactRadius);
            Enter(State.JumpRise, JumpRise);
        }

        void UpdateMarker(Transform target, float dt)
        {
            if (locked) return;
            if (!TargetAlive(target)) { LockMarker(); return; } // 명세 3-4: 플레이어가 죽으면 추적을 멈추고 마지막 자리에
            float goal = ClampMarkerX(target.position.x);
            markerX = Mathf.MoveTowards(markerX, goal, MarkerFollowSpeed * dt);
            markerSurfaceY = SurfaceUnder(markerX, target);
            if (jumpWarning != null) jumpWarning.MoveTo(new Vector2(markerX, markerSurfaceY));
        }

        void LockMarker()
        {
            locked = true;
            if (jumpWarning != null) jumpWarning.Lock();
        }

        void Impact(Transform target)
        {
            standSurfaceY = markerSurfaceY;
            var point = new Vector2(markerX, markerSurfaceY);
            bool hit = false;
            if (TargetAlive(target) && InImpactArea(point, target.position))
            {
                var d = target.GetComponent<IDamageable>();
                if (d != null) { d.TakeDamage(TargetMaxHp(target) * ImpactDamagePct, gameObject); hit = true; }
            }
            if (jumpWarning != null) jumpWarning.Flash();
            HitFeel.Shake(12f, 0.3f);
            RangeRing.Show(point, ImpactRadius, new Color(1f, 0.85f, 0.7f, 0.9f), 0.25f);
            Impacted?.Invoke(point, hit);
            Enter(State.JumpRecover, JumpRecover);
        }

        /// <summary>
        /// 내려찍기 판정 — 착지점에서 반경 <see cref="ImpactRadius"/> 안, 그리고 그 표면보다 아래로 떨어진 대상은 제외(발판 밑에 숨은 건 안 맞음).
        /// 경고 그림과 같은 반경이다(명세 "판정과 최대한 같은 크기").
        /// </summary>
        public static bool InImpactArea(Vector2 point, Vector2 targetPos)
            => targetPos.y >= point.y - 0.3f && Vector2.Distance(point, targetPos) <= ImpactRadius;

        float ClampMarkerX(float x)
            => Mathf.Clamp(x, FieldBounds.MinX + VisualSize.x / 2f, FieldBounds.MaxX - VisualSize.x / 2f);

        // ───────────────────────── 투척 ─────────────────────────

        void BeginThrowWarn(Transform target)
        {
            throwFrom = HeldPosition();
            // 플레이어 **발밑 표면**을 노린다(명세 4-2 "플레이어 현재 위치 바닥에 경고, 그 위치로 던진다") — 몸 중심을 노리면 위에서 비스듬히
            // 내려오는 돌이 플레이어 뒤쪽 바닥까지 날아가 경고가 엉뚱한 데 뜬다. 돌은 내려오면서 플레이어 몸을 지나므로 서 있으면 맞는다.
            Vector2 aim = target != null
                ? new Vector2(target.position.x, SurfaceUnder(target.position.x, target))
                : new Vector2(transform.position.x + mover.Direction * MinThrowDistance, FieldBounds.GroundY);
            aim = CorrectAim(transform.position.x, mover.Direction, aim);
            throwTo = aim; // 노린 표면 점까지 직선 — 사이의 발판은 지나간다(한쪽 방향 발판)

            if (throwWarning != null)
            {
                throwWarning.Show(throwTo, BoulderWarnRadius);
                throwWarning.Lock();
            }
            if (throwPathLine != null)
            {
                throwPathLine.enabled = true;
                throwPathLine.positionCount = 2;
                throwPathLine.SetPosition(0, throwFrom);
                throwPathLine.SetPosition(1, throwTo);
            }
            Enter(State.ThrowWarn, ThrowWarn);
        }

        void Throw(Transform target)
        {
            if (heldBoulder != null) heldBoulder.enabled = false;
            if (throwPathLine != null) throwPathLine.enabled = false;
            float dmg = TargetMaxHp(target) * BoulderDamagePct;
            var b = BoulderProjectile.Spawn(boulderPrefab, throwFrom, throwTo, BoulderSpeed, dmg);
            if (b != null)
            {
                var warn = throwWarning;
                b.Finished += _ => { if (warn != null) warn.Flash(); };
                b.Landed += _ => HitFeel.Shake(5f, 0.15f);
                Thrown?.Invoke(b);
            }
            Enter(State.ThrowRecover, ThrowRecover);
        }

        /// <summary>
        /// 근거리 보정(명세 4-4) — 바로 앞에 던지면 못 피한다. 플레이어가 <see cref="MinThrowDistance"/>보다 가까우면 같은 쪽으로
        /// 그 거리만큼 떨어진 자리의 표면을 노린다(맵 밖이면 반대쪽).
        /// </summary>
        public static Vector2 CorrectAim(float bossX, int facing, Vector2 aim)
        {
            float dx = aim.x - bossX;
            if (Mathf.Abs(dx) >= MinThrowDistance) return aim;
            float side = Mathf.Abs(dx) > 0.01f ? Mathf.Sign(dx) : (facing >= 0 ? 1f : -1f);
            float ax = bossX + side * MinThrowDistance;
            if (ax <= FieldBounds.MinX || ax >= FieldBounds.MaxX) ax = bossX - side * MinThrowDistance;
            return new Vector2(ax, FieldLayout.SurfaceBelow(ax, aim.y + 0.05f));
        }

        Vector2 HeldPosition() => (Vector2)transform.position + new Vector2(0f, HalfHeight + HoldAbove);

        void ShowHeld(float lift)
        {
            if (heldBoulder == null) return;
            heldBoulder.enabled = true;
            // 몸 꼭대기에서 위로 들어 올린다. 부모 배율과 상관없이 월드 위치로 놓는다.
            Vector2 top = (Vector2)transform.position + new Vector2(0f, HalfHeight * 0.6f);
            heldBoulder.transform.position = Vector2.Lerp(top, HeldPosition(), EaseOut(lift));
        }

        // ───────────────────────── 공통 ─────────────────────────

        void ToIdle()
        {
            Squash(0f);
            Enter(State.Idle, IdleGap);
        }

        void Enter(State s, float dur)
        {
            state = s; stateT = 0f; stateDur = dur;
        }

        void Face(Transform target)
        {
            if (target == null) return;
            float dx = target.position.x - transform.position.x;
            if (Mathf.Abs(dx) > 0.05f) mover.SetDirection(dx > 0f ? 1 : -1);
            if (body != null) body.flipX = mover.Direction < 0; // 그림은 오른쪽을 본다
        }

        /// <summary>몸 눌림(-)·늘어남(+) — 준비 때 낮추고, 뛸 때 늘이고, 착지 때 찌그러진다(명세 "몸을 낮추며 점프 준비").</summary>
        void Squash(float amount)
        {
            if (!baseScaleKnown) return;
            transform.localScale = new Vector3(baseScale.x * (1f - amount * 0.6f), baseScale.y * (1f + amount), baseScale.z);
        }

        float SurfaceUnder(float x, Transform target)
        {
            float feet = target != null ? Feet(target) : FieldBounds.GroundY;
            return FieldLayout.SurfaceBelow(x, feet);
        }

        static float Feet(Transform target)
        {
            var c = target.GetComponent<Collider2D>();
            return c != null ? c.bounds.min.y : target.position.y;
        }

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
