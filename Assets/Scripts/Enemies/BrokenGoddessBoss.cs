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
    /// **그림/애니메이션(2026-11-18, 사용자 "여신상 완성" — 실제 스프라이트 수령)**: 요청서 1절의 "현재는
    /// 애니메이션을 사용하지 않으므로 Animator 및 애니메이션 상태 전환은 구현하지 마"는 **그때 정식 아트가
    /// 없던 시점의 범위 제한**이었다 — 이제 실제 그림이 들어왔으므로 그 제한은 해소됐다(Animator 컴포넌트는
    /// 여전히 안 쓴다 — 1지역 <see cref="BlobLordBoss"/>와 같은 방식으로 상태머신이 직접 스프라이트를 고른다).
    /// 대기 4장·이동 4장·피격 1장·사망 8장은 80×64 캔버스, 잡기 7장·휘두르기 5장은 100×64 캔버스(둘 다 높이
    /// 64 공통 — <see cref="VisualSize"/> 하나로 일관되게 늘어난다). <see cref="UpdateBodySprite"/> 참고.
    /// **에셋 가져오기·플립북 생성·프리팹 연결은 에디터 메뉴가 한다**(`Editor/BuildBrokenGoddessArt`,
    /// `ChronoSavior/보스 그림 연결/부서진 여신상` — 이 세션은 Unity 에디터가 없어 직접 임포트할 수 없다).
    /// 메뉴를 돌리기 전까진 프리팹이 기본 도형 그림 그대로다 — "확인 필요": 메뉴 실행 + 인게임 확인(`HANDOFF.md`).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMove))]
    [RequireComponent(typeof(EnemyHealth))]
    public class BrokenGoddessBoss : MonoBehaviour, IEnemyMotion, IEnemyContactDamageModifier, IGrabExempt, IDeathAnimated
    {
        // ── 몸 — 실제 그림 80×64 캔버스 비율(대기·이동·피격·사망) 그대로, 세로는 공통 보스 기준
        // (EntitySizeConfig.BossHeight) 유지(2026-11-18, 그림 들어오기 전 임시 크기였던 1.30×1.50에서 가로만 보정 —
        // 세로가 같은 그림을 그대로 써서 비율이 안 틀어지게). 잡기·휘두르기(100×64)는 같은 세로 스케일을 쓰므로
        // 가로만 더 넓게 나온다(그림대로, 늘이지 않음).
        public static readonly Vector2 VisualSize = new Vector2(EntitySizeConfig.BossHeight * 80f / 64f, EntitySizeConfig.BossHeight);
        public const float HitRadius = 0.75f; // Mathf.Min(VisualSize.x, VisualSize.y) / 2

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

        [Header("몸 그림(2026-11-18 — 실제 아트 적용, 메뉴 ChronoSavior/보스 그림 연결/부서진 여신상이 채운다)")]
        [Tooltip("대기 4장 — 접근하지 않고 가만히 서 있을 때(사거리 안 또는 대상 없음) 되풀이.")]
        [SerializeField] SpriteFlipbook idleArt;
        [Tooltip("이동 4장 — 사거리 밖이라 접근(IdleApproach로 걸어감) 중일 때 되풀이.")]
        [SerializeField] SpriteFlipbook moveArt;
        [Tooltip("잡기 7장 — 상태 진행도로 고른다(fps는 안 쓴다): 준비 1(0) · 경고 1(1) · 팔 뻗기 1(2) · 끌어당기기 3(3~5) · 붙잡아둠 1(6).")]
        [SerializeField] SpriteFlipbook grabArt;
        [Tooltip("휘두르기 5장 — 상태 진행도로 고른다(fps는 안 쓴다): 준비 1(0) · 경고 2(1~2) · 판정 2(3~4).")]
        [SerializeField] SpriteFlipbook sweepArt;
        [Tooltip("피격 1장 — 직접 타격마다 잠깐(피격 그림이 도는 동안은 다른 모든 그림보다 우선).")]
        [SerializeField] SpriteFlipbook hitArt;
        [Tooltip("사망 8장 — 다 돌고 사라진다(fps는 에셋).")]
        [SerializeField] SpriteFlipbook deathArt;

        /// <summary>씬 검증·테스트·에디터 도구가 읽고 넣는다.</summary>
        public SpriteFlipbook IdleArt { get => idleArt; set => idleArt = value; }
        public SpriteFlipbook MoveArt { get => moveArt; set => moveArt = value; }
        public SpriteFlipbook GrabArt { get => grabArt; set => grabArt = value; }
        public SpriteFlipbook SweepArt { get => sweepArt; set => sweepArt = value; }
        public SpriteFlipbook HitArt { get => hitArt; set => hitArt = value; }
        public SpriteFlipbook DeathArt { get => deathArt; set => deathArt = value; }

        EnemyMove mover;
        EnemyHealth health;
        SpriteRenderer body;
        GroundWarning grabWarning, sweepWarning;

        // 몸 기본 그림(프리팹의 m_Sprite 오버라이드를 그대로 캐시) — BlobLordBoss와 같은 이유로, 대기·이동·
        // 상태별 그림이 전부 없는 경우(에셋 연결 전)의 안전한 대비값이다.
        Sprite defaultSprite;
        // 피격 그림(직접 타격마다 짧게) — BlobLordBoss와 같은 지속시간.
        public const float HitFlashDuration = 0.15f;
        float hitTimer;
        // 대기/이동 되풀이 재생 시각(상태가 바뀌면 처음부터 다시 돌게 리셋).
        float idleCycleT, moveCycleT;
        // 대기 중 실제로 접근해 걷고 있는지(IdleApproach가 매 프레임 갱신) — 이동 그림 분기에 쓴다.
        bool isMoving;

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
            defaultSprite = body != null ? body.sprite : null;

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
            CombatEvents.EnemyDamaged -= HandleDamaged;
            // 보스가 비활성화돼도(파괴되기 전 포함) 붙잡고 있던 플레이어는 즉시 풀어준다
            // (요청서 "보스가 사망하거나 공격이 중단되면 잡힌 플레이어를 즉시 해제한다").
            ReleaseGrabbedTarget();
        }

        void OnEnable() => CombatEvents.EnemyDamaged += HandleDamaged;

        /// <summary>
        /// 직접 타격마다(지속 피해 제외) 피격 그림을 잠깐 띄운다 — BlobLordBoss.HandleDamaged와 같은 이유·같은 필터링
        /// (`CombatEvents.EnemyDamaged`는 적 전체가 같이 올리는 전역 이벤트라 이 보스 걸 걸러야 한다).
        /// </summary>
        void HandleDamaged(GameObject go)
        {
            if (go != gameObject) return;
            hitTimer = HitFlashDuration;
        }

        void ReleaseGrabbedTarget()
        {
            if (grabbedTarget == null) return;
            var g = grabbedTarget.GetComponent<IGrabbable>();
            g?.EndGrab();
            grabbedTarget = null;
        }

        // 그림은 그리기 직전에(BlobLordBoss와 같은 이유) — 죽은 뒤엔 사망 연출(DeathSequence)이 그린다.
        void LateUpdate()
        {
            if (health != null && health.IsDead) return;
            UpdateBodySprite(Time.deltaTime);
        }

        /// <summary>
        /// 몸 그림 — 피격이 최우선(맞은 순간은 어떤 상태든 짧게 덮어쓴다), 그다음 상태머신(잡기·휘두르기는
        /// 상태 진행도로 몇 번째 장인지 고른다 — <see cref="GrabSprite"/>·<see cref="SweepSprite"/>, BlobLordBoss의
        /// 튀어오르기·투척과 같은 방식), 그 밖(대기/후딜레이)은 접근 중인지로 대기·이동을 가른다. 기본 그림(대기 01)은
        /// 에셋이 아직 안 꽂혔을 때의 안전한 대비값이다.
        /// </summary>
        void UpdateBodySprite(float dt)
        {
            if (body == null) return;
            if (hitTimer > 0f)
            {
                hitTimer = Mathf.Max(0f, hitTimer - dt);
                var hit = At(hitArt, 0);
                if (hit != null) { body.sprite = hit; return; }
            }

            Sprite s = null;
            switch (state)
            {
                case State.GrabPrep: case State.GrabWarn: case State.GrabThrust: case State.GrabPull: case State.GrabHold:
                    s = GrabSprite();
                    break;
                case State.SweepPrep: case State.SweepWarn: case State.SweepActive:
                    s = SweepSprite();
                    break;
                case State.GrabRecover: case State.SweepRecover:
                    s = At(idleArt, 0); // 후딜레이 — 다음 대기 되풀이가 시작되기 전까지 대기 그림의 첫 자세로
                    break;
                case State.Idle:
                    if (isMoving)
                    {
                        idleCycleT = 0f;
                        moveCycleT += dt;
                        s = moveArt != null ? moveArt.FrameAt(moveCycleT) : null;
                    }
                    else
                    {
                        moveCycleT = 0f;
                        idleCycleT += dt;
                        s = idleArt != null ? idleArt.FrameAt(idleCycleT) : null;
                    }
                    break;
            }
            body.sprite = s != null ? s : defaultSprite;
        }

        static Sprite At(SpriteFlipbook b, int idx) => b != null && idx >= 0 && idx < b.Count ? b.frames[idx] : null;

        /// <summary>잡기 7장 — 준비 1(0)·경고 1(1)·팔 뻗기 1(2)·끌어당기기 3(3~5, 가장 역동적인 구간이라 세 장)·붙잡아둠 1(6).</summary>
        Sprite GrabSprite()
        {
            switch (state)
            {
                case State.GrabPrep: return At(grabArt, 0);
                case State.GrabWarn: return At(grabArt, 1);
                case State.GrabThrust: return At(grabArt, 2);
                case State.GrabPull: return At(grabArt, 3 + Mathf.Clamp(Mathf.FloorToInt(stateT / GrabPullTime * 3f), 0, 2));
                case State.GrabHold: return At(grabArt, 6);
                default: return null;
            }
        }

        /// <summary>휘두르기 5장 — 준비 1(0)·경고 2(1~2, 휘두를 팔을 들어 올리는 구간)·판정 2(3~4, 휘두름+따라치기).</summary>
        Sprite SweepSprite()
        {
            switch (state)
            {
                case State.SweepPrep: return At(sweepArt, 0);
                case State.SweepWarn: return At(sweepArt, 1 + Mathf.Clamp(Mathf.FloorToInt(stateT / SweepWarnTime * 2f), 0, 1));
                case State.SweepActive: return At(sweepArt, 3 + Mathf.Clamp(Mathf.FloorToInt(stateT / SweepActiveTime * 2f), 0, 1));
                default: return null;
            }
        }

        /// <summary>
        /// <see cref="IDeathAnimated"/> — BlobLordBoss.PlayDeathAndDestroy와 같은 방식. <see cref="EnemyHealth.Die"/>가
        /// 즉시 Destroy하는 대신 이걸 부른다. 사망 그림 8장을 재생한 뒤 스스로 Destroy — 그동안 경고 표시를 치우고
        /// 붙잡고 있던 대상을 풀어주고(이미 OnDisable이 하지만 Destroy 전에 명시적으로 한 번 더 — 죽는 순간 바로
        /// 반영돼야 함) 콜라이더를 꺼서(시체를 겨누거나 맞히지 않게) 상태머신이 끼어들지 않게 한다
        /// (<see cref="GetHorizontalSpeed"/>가 맨 위에서 health.IsDead를 보고 멈춘다).
        /// </summary>
        public void PlayDeathAndDestroy()
        {
            if (grabWarning != null) grabWarning.gameObject.SetActive(false);
            if (sweepWarning != null) sweepWarning.gameObject.SetActive(false);
            ReleaseGrabbedTarget();
            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
            StartCoroutine(DeathSequence());
        }

        System.Collections.IEnumerator DeathSequence()
        {
            int n = deathArt != null ? deathArt.Count : 0;
            float step = deathArt != null && deathArt.fps > 0f ? 1f / deathArt.fps : 0.125f;
            for (int i = 0; i < n; i++)
            {
                if (body != null && deathArt.frames[i] != null) body.sprite = deathArt.frames[i];
                yield return new WaitForSeconds(step); // 게임 시간 — 히트스톱·일시정지 땐 같이 멈춘다
            }
            Destroy(gameObject);
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
            if (target == null) { isMoving = false; return x; }
            float dx = target.position.x - x;
            float dist = Mathf.Abs(dx);
            float stopDist = Mathf.Max(GrabRange, SweepRadius);
            if (dist <= stopDist)
            {
                isMoving = false;
                // 그림은 왼쪽을 본다(BlobLord 관례 "오른쪽"과 반대 — 2026-11-18 사용자가 좌우 반전을
                // 지적해 Grab/Sweep 원본 그림을 직접 확인하고 뒤집었다).
                if (body != null) body.flipX = mover.Direction >= 0;
                return x;
            }
            isMoving = true;
            int dir = dx > 0f ? 1 : -1;
            mover.SetDirection(dir);
            if (body != null) body.flipX = dir >= 0;
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
            if (body != null) body.flipX = lockedFacing >= 0; // 그림이 왼쪽을 보는 것 기준(위 IdleApproach 참고)
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
            // 타격감(2026-11-18, 사용자 "여신상 완성" 범위) — 붙잡는 순간의 무게감. 실제 피해는 FinishGrab에서
            // PlayerHealth.ApplyDamage가 이미 HitFeel.PlayerHurt()(붉어짐+흔들림 7/0.22)를 걸므로, 여기는 그보다
            // 큰 쪽으로 덮어써 "잡혔다"는 순간을 더 강조한다(BlobLordBoss.Impact의 착지 흔들림과 같은 역할).
            HitFeel.Shake(8f, 0.2f);
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

        /// <summary>
        /// 끌어당기기가 끝난 시점 — 피해를 1회 주고 풀어준다(요청서 "끌어당기기가 끝나면 피해를 준다 →
        /// 이동 제한을 해제한다"). **무적을 무시하고 준다**(<see cref="IDamageable.TakeDamageIgnoringInvuln"/>,
        /// 2026-11-18 사용자가 실제 플레이로 발견) — 끌어당기기(0.5초)+붙잡아둠(0.2초) = 0.7초 동안
        /// 다른 몹에게 맞아 생긴 무적(기본 0.9초)이 아직 안 끝났으면, 잡기는 분명히 성공(붙잡혀 끌려옴)
        /// 했는데 정작 이 공격의 피해만 조용히 무시되는 게 더 혼란스럽기 때문이다.
        /// </summary>
        void FinishGrab()
        {
            if (grabbedTarget != null)
            {
                var d = grabbedTarget.GetComponent<IDamageable>();
                if (d != null && !d.IsDead)
                    d.TakeDamageIgnoringInvuln(TargetMaxHp(grabbedTarget.transform) * GrabDamagePct, gameObject);
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
            if (hit)
            {
                d.TakeDamage(TargetMaxHp(target) * SweepDamagePct, gameObject);
                HitFeel.Shake(6f, 0.18f); // 타격감 — 그랩 스냇치(8/0.2)보다 조금 작게, PlayerHurt 기본 흔들림(7/0.22) 위로 덮어쓴다
            }
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
