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
    /// **보스 강화(2026-11-24 사용자 — 요청서 밖, "너무 쉽다", 접촉 피해는 없는 대신 더 빠르게)**: 접근 걷기 <see cref="ApproachSpeed"/>
    /// (공통 보스의 1.5배), 준비 ×0.8·후딜 ×0.7·쿨타임 ×0.75(경고 시간은 그대로 — 피할 수는 있게), 그리고 패턴 3 **돌진 잡기**:
    /// 거리 <see cref="DashMinDistance"/>~<see cref="DashMaxDistance"/>이면 방향 고정(<c>State.DashPrep</c>) → 바닥 직선 경고(<c>State.DashWarn</c>) →
    /// 초당 <see cref="DashSpeed"/>로 최대 <see cref="DashLength"/> 미끄러짐(<c>State.DashRun</c>, 몸 앞 <see cref="DashHitRange"/> 안이면 기존 잡기의
    /// 끌어당김·붙잡아 둠·피해로) → 빗나가면 <c>State.DashRecover</c>. 그림은 잡기 그림을 빌려 쓴다.
    ///
    /// **2차 조정(2026-11-24 사용자 플레이 뒤)**: 잡기·휘두르기는 사거리 끝이 아니라 **사거리의 <see cref="EngageRatio"/>(60%) 안**에 들어와야 시작한다
    /// (판정 범위는 그대로), 같은 패턴 2번 연속 허용(<see cref="MaxConsecutive"/>), 휘두르기 직후 <see cref="ComboChance"/> 확률로 **바로 잡기**(연계).
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
    /// 않는다 — "확인 필요": 팀장이 몸통 접촉 피해를 원하면 이 값을 조정할 것). 끌어당김·스턴 등
    /// 이동 제한 디버프는 안 받는다(<see cref="ICrowdControlImmune"/>, 2026-11-18 사용자 요청 —
    /// 다른 보스와 공통).
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
    /// `ChronoSavior/보스 그림 연결/부서진 여신상`). 그림은 **왼쪽**을 본다(BlobLord의 "오른쪽" 관례와
    /// 반대 — 2026-11-18 사용자가 좌우 반전을 지적해 직접 그림을 열어 확인하고 <see cref="IdleApproach"/>·
    /// <see cref="LockFacing"/>의 flipX 조건을 뒤집었다).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMove))]
    [RequireComponent(typeof(EnemyHealth))]
    public class BrokenGoddessBoss : MonoBehaviour, IEnemyMotion, IEnemyContactDamageModifier, IGrabExempt, IDeathAnimated, ICrowdControlImmune
    {
        // ── 몸 — 실제 그림 80×64 캔버스 비율(대기·이동·피격·사망) 그대로, 세로는 공통 보스 기준
        // (EntitySizeConfig.BossHeight) 유지(2026-11-18, 그림 들어오기 전 임시 크기였던 1.30×1.50에서 가로만 보정 —
        // 세로가 같은 그림을 그대로 써서 비율이 안 틀어지게). 잡기·휘두르기(100×64)는 같은 세로 스케일을 쓰므로
        // 가로만 더 넓게 나온다(그림대로, 늘이지 않음).
        public static readonly Vector2 VisualSize = new Vector2(EntitySizeConfig.BossHeight * 80f / 64f, EntitySizeConfig.BossHeight);
        public const float HitRadius = 0.75f; // Mathf.Min(VisualSize.x, VisualSize.y) / 2

        // ── 패턴 1 잡기(사용자 2026-11-18 요청서 2절 "초기 설정값" — 명세서 범위보다 이 값을 우선한다) ──
        public const float GrabPrepTime = 0.48f;    // 공격 준비 시간 — 2026-11-24 보스 강화 ×0.8(예전 0.6)
        public const float GrabWarnTime = 0.5f;     // 경고 표시 시간
        public const float GrabRange = 3f;          // 잡기 사거리(유닛)
        public const float GrabHeight = 1.5f;       // 잡기 판정 높이(유닛)
        public const float GrabThrustTime = 0.25f;  // 팔 뻗기 시간(= 판정 지속)
        public const float GrabPullTime = 0.5f;     // 끌어당기기 시간
        public const float GrabHoldTime = 0.8f;     // 끌어당긴 후 유지 시간 — 2026-11-24 0.2→0.4(손에 들린 모습이 보이게)→0.8(사용자 요청)
        public const float GrabDamagePct = 0.20f;   // 공격 피해량 — 플레이어 최대 체력의 20%
        public const float GrabRecoverTime = 0.84f; // 후딜레이 — 2026-11-24 ×0.7(예전 1.2)
        public const float GrabCooldown = 3.75f;    // 쿨타임 — 2026-11-24 ×0.75(예전 5)
        // ── 잡힌 대상이 따라가는 손 위치(2026-11-24, 사용자 "끌려와서 여신상 앞에 서 있기만 한다 — 손에
        // 들려 있게") — 잡기 그림(100×64, 가운데 기준, 왼쪽을 봄)에서 주먹 가운데를 직접 재서 옮긴 값.
        // x = 보스 가운데에서 **보는 방향 앞으로**, y = 위로. 그림 1px = BossHeight/64 유닛.
        // **2차(같은 날, 사용자 "손 위로 캐릭터 머리가 다 보이게, 보이는 건 손이 우선")**: 주먹 가운데가 아니라
        // **주먹 윗변에 캐릭터의 턱**을 맞춘다 — 머리는 주먹 위로 다 보이고, 몸은 주먹(과 여신상 몸) 뒤로 가려진다
        // (잡는 동안 여신상 몸을 캐릭터보다 앞에 그린다, 아래 GrabFrontOrder).
        //   팔 뻗은 주먹(Grab_04·05) — 주먹 가로 가운데 20px, 윗변 40px → 앞 30px · 아래 8px
        //   들어 올린 주먹(Grab_06·07) — 주먹 가로 가운데 43px, 윗변 20px → 앞 7px · 위 12px
        //   들어 올린 주먹 크기 ≈ 10×10px(0.23×0.23유닛) — 캐릭터 턱 아래 몸(0.22유닛 높이·폭 0.23)이 거의 꼭 들어간다.
        /// <summary>잡기 그림 1픽셀의 월드 크기.</summary>
        public const float GrabArtPixel = EntitySizeConfig.BossHeight / 64f;
        /// <summary>캐릭터 턱이 캐릭터 기준점보다 얼마나 아래인지(유닛) — 캐릭터 그림(32×48, 기준점 위에서 16px,
        /// 턱 ≈ 위에서 21px, `CharacterArtSet` 높이 0.375 = 128px/유닛 기준)에서 잰 값. 캐릭터 그림 크기가 바뀌면 다시 잴 것.</summary>
        public const float GrabChinBelowPivot = 5f / 128f;
        /// <summary>팔을 뻗은 주먹 — 끌어당기기 앞 2/3(그림 3·4번) 동안 대상을 여기까지 끌고 온다(캐릭터 기준점 자리).</summary>
        public static readonly Vector2 GrabReachHand = new Vector2(30f, -8f) * GrabArtPixel + new Vector2(0f, GrabChinBelowPivot);
        /// <summary>들어 올린 주먹 — 끌어당기기 마지막 1/3(그림 5번)에 여기로 들어 올리고, 붙잡아둠(그림 6번) 동안 유지(캐릭터 기준점 자리).</summary>
        public static readonly Vector2 GrabHeldHand = new Vector2(7f, 12f) * GrabArtPixel + new Vector2(0f, GrabChinBelowPivot);
        /// <summary>붙잡아둠 동안 손아귀에서 버둥대는 흔들림(좌우, 유닛) — 연출용 임의 값. 주먹과 몸 폭이 거의 같아서
        /// 크게 흔들면 몸 옆이 삐져나온다 — 1px 남짓.</summary>
        public const float GrabHoldShake = 0.01f;
        /// <summary>잡고 있는 동안(끌어당기기·붙잡아둠) 여신상 몸 정렬을 이만큼 올린다 — 잡힌 캐릭터(0)와 그 피격
        /// 깜빡임(+1)보다 앞에 그려 손이 우선으로 보이게(사용자 2026-11-24). 놓아 주면 원래대로.</summary>
        public const int GrabFrontOrder = 2;

        /// <summary>손 위치(보스 기준 앞·위)를 월드 좌표로 — 보는 방향을 곱하고 맵 밖으로 안 나가게 자른다.
        /// 테스트도 이걸로 기대 위치를 계산한다.</summary>
        public static Vector2 HandPoint(Vector2 bossPos, int facing, Vector2 hand)
            => new Vector2(Mathf.Clamp(bossPos.x + facing * hand.x, FieldBounds.MinX + 0.1f, FieldBounds.MaxX - 0.1f),
                           bossPos.y + hand.y);

        // ── 패턴 2 휘두르기(사용자 2026-11-18 요청서 3절 "초기 설정값") ──
        public const float SweepPrepTime = 0.4f;    // 공격 준비 시간 — 2026-11-24 ×0.8(예전 0.5)
        public const float SweepWarnTime = 0.6f;    // 경고 표시 시간
        public const float SweepRadius = 3f;        // 공격 범위(반경 유닛)
        public const float SweepHalfAngleDeg = 80f; // 전방 160도의 절반
        public const float SweepActiveTime = 0.3f;  // 실제 공격 판정 시간
        public const float SweepDamagePct = 0.15f;  // 공격 피해량 — 플레이어 최대 체력의 15%
        public const float SweepRecoverTime = 0.7f; // 후딜레이 — 2026-11-24 ×0.7(예전 1.0)
        public const float SweepCooldown = 3f;      // 쿨타임 — 2026-11-24 ×0.75(예전 4)

        // ── 고르기(요청서 4절) ──
        public const int MaxConsecutive = 2;        // 같은 패턴은 2번까지 연속 — 2026-11-24 사용자 "번갈아 나와 예측이 쉽다"(예전 1 = 연속 금지)

        // ── 보스 강화(2026-11-24 사용자, `HANDOFF.md` "1·2지역 보스 강화") — 접촉 피해는 없는 대신 더 빠르게 ──
        /// <summary>접근 걷기 속도 — 공통 보스(<see cref="Boss.MoveSpeed"/> 0.9)의 1.5배(예전엔 그대로 0.9).</summary>
        public const float ApproachSpeed = 1.35f;
        /// <summary>
        /// 잡기·휘두르기를 **시작하는** 거리 = 사거리 × 이 비율(2026-11-24 사용자 "사거리 끝자락에서 공격해 피하기 쉽다" → 60%).
        /// 판정 범위(<see cref="GrabRange"/>·<see cref="SweepRadius"/>)는 그대로 — 그보다 멀면 더 걸어 들어온다.
        /// </summary>
        public const float EngageRatio = 0.6f;
        public const float GrabEngage = GrabRange * EngageRatio, SweepEngage = SweepRadius * EngageRatio;
        /// <summary>연계 공격 — 휘두르기가 끝나면 이 확률로 후딜 없이 바로 잡기로 잇는다(잡기 쿨이 돌았고 사거리 안일 때).</summary>
        public const float ComboChance = 0.4f;
        /// <summary>연계 잡기의 준비 시간 — 평소(<see cref="GrabPrepTime"/>)보다 짧다. 경고는 평소대로 뜬다.</summary>
        public const float ComboGrabPrep = 0.25f;

        // 패턴 3 돌진 잡기 — 멀리 있으면(잡기 사거리 밖 ~ DashMaxDistance) 미끄러지며 잡는다
        public const float DashMinDistance = GrabRange;  // 이보다 가까우면 그냥 잡기·휘두르기
        public const float DashMaxDistance = 8f;
        public const float DashPrepTime = 0.5f;     // 방향 고정
        public const float DashWarnTime = 0.5f;     // 바닥 직선 경고
        public const float DashSpeed = 8f;          // 유닛/초
        public const float DashLength = 6f;         // 최대 돌진 거리
        public const float DashHitRange = 1.0f;     // 몸 앞 이 거리 안에 들어오면 잡힘(높이는 GrabHeight)
        public const float DashRecoverTime = 1.2f;  // 빗나가면 멈춰서 후딜레이
        public const float DashCooldown = 6f;
        public const float FirstDelay = 1f;         // 스폰 직후 첫 공격까지 — 플레이어가 상황을 볼 시간
        public const float IdleGap = 0.3f;          // 후딜레이 뒤 다음 선택까지 짧은 틈
        /// <summary>대상 최대 체력을 못 읽을 때(테스트 등) — 플레이어 기본 최대 체력.</summary>
        public const float FallbackTargetMaxHp = 100f;

        public enum Pattern { None, Grab, Sweep, DashGrab }
        public enum State
        {
            Idle,
            GrabPrep, GrabWarn, GrabThrust, GrabPull, GrabHold, GrabRecover,
            SweepPrep, SweepWarn, SweepActive, SweepRecover,
            DashPrep, DashWarn, DashRun, DashRecover,
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

        State state = State.Idle;
        float stateT, stateDur = FirstDelay;
        Pattern last = Pattern.None;
        int consecutive;
        float clock, grabReadyAt, sweepReadyAt, dashReadyAt;
        // 돌진 잡기 — 출발점(달린 거리 재기)
        float dashStartX;

        /// <summary>연계 공격 확률 — 테스트가 0/1로 고정할 수 있게(기본 <see cref="ComboChance"/>).</summary>
        public float ComboChanceRuntime { get; set; } = ComboChance;

        /// <summary>공격 준비가 끝나는 순간 고정한 방향(1 또는 -1) — 공격이 끝날 때까지 안 바뀐다.</summary>
        int lockedFacing = 1;
        bool grabHitDone, sweepHitDone;

        // 붙잡은 대상 — 보스가 죽거나 꺼질 때 반드시 풀어 줘야 하므로 들고 있는다.
        GameObject grabbedTarget;
        Vector2 grabStart;
        int bodyOrderBeforeGrab;
        bool bodyOrderRaised;

        /// <summary>대기 중 걷고 있는지 — 그림이 대기 4장/이동 4장 중 뭘 돌릴지 고른다(<see cref="UpdateBodySprite"/>,
        /// <see cref="IdleApproach"/>가 매긴다).</summary>
        bool isMoving;

        // 그림(2026-11-18) — 피격은 직접 타격마다 짧게 덮어쓴다(BlobLordBoss.HandleDamaged와 같은 이유).
        public const float HitFlashDuration = 0.15f;
        float hitTimer;
        float moveCycleT, idleCycleT;

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

        void OnEnable() => CombatEvents.EnemyDamaged += HandleDamaged;

        void OnDisable()
        {
            CombatEvents.EnemyDamaged -= HandleDamaged;
            // 보스가 비활성화돼도(파괴되기 전 포함) 붙잡고 있던 플레이어는 즉시 풀어준다
            // (요청서 "보스가 사망하거나 공격이 중단되면 잡힌 플레이어를 즉시 해제한다").
            ReleaseGrabbedTarget();
        }

        /// <summary>직접 타격마다(지속 피해 제외) 피격 그림을 잠깐 띄운다 — `CombatEvents.EnemyDamaged`는
        /// 적 전체가 같이 올리는 전역 이벤트라 이 보스 걸 걸러야 한다(BlobLordBoss.HandleDamaged와 동일).</summary>
        void HandleDamaged(GameObject go)
        {
            if (go != gameObject) return;
            hitTimer = HitFlashDuration;
        }

        // 그림은 그리기 직전에(BlobLordBoss와 같은 방식). 죽은 뒤엔 사망 연출(DeathSequence)이 그린다.
        void LateUpdate()
        {
            if (health != null && health.IsDead) return;
            UpdateBodySprite(Time.deltaTime);
        }

        void ReleaseGrabbedTarget()
        {
            if (bodyOrderRaised)
            {
                if (body != null) body.sortingOrder = bodyOrderBeforeGrab;
                bodyOrderRaised = false;
            }
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
                    if (stateT >= stateDur) BeginGrabWarn(); // 평소 GrabPrepTime, 연계면 ComboGrabPrep
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
                    if (stateT >= SweepActiveTime)
                    {
                        if (!TryComboGrab(target)) Enter(State.SweepRecover, SweepRecoverTime);
                    }
                    break;

                case State.SweepRecover:
                    if (stateT >= SweepRecoverTime) ToIdle();
                    break;

                case State.DashPrep:
                    if (stateT >= DashPrepTime) BeginDashWarn();
                    break;

                case State.DashWarn:
                    if (stateT >= DashWarnTime) BeginDashRun();
                    break;

                case State.DashRun:
                {
                    // 고정한 방향으로 미끄러진다 — 몸 앞에 닿으면 기존 잡기(끌어당김·붙잡아 둠·피해)로 넘어간다
                    float step = DashSpeed * dt;
                    float nextX = Mathf.Clamp(x + lockedFacing * step, FieldBounds.MinX + 0.3f, FieldBounds.MaxX - 0.3f);
                    desiredX = nextX;
                    if (!grabHitDone && !targetGone && InGrabBox(transform.position, lockedFacing, DashHitRange, GrabHeight, target.position))
                    {
                        TryGrab(target); // 성공하면 GrabPull로 넘어간다
                        if (grabHitDone) { desiredX = x; break; }
                    }
                    bool wall = Mathf.Abs(nextX - x) < step * 0.5f; // 맵 끝에 막혔다
                    if (Mathf.Abs(nextX - dashStartX) >= DashLength || wall || stateT >= stateDur)
                    {
                        if (grabWarning != null) grabWarning.Hide();
                        Enter(State.DashRecover, DashRecoverTime); // 빗나감 — 멈춰서 후딜레이
                    }
                    break;
                }

                case State.DashRecover:
                    if (stateT >= DashRecoverTime) ToIdle();
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
            float stopDist = Mathf.Max(GrabEngage, SweepEngage); // 2026-11-24 — 사거리 끝이 아니라 안쪽까지 들어온다
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
            return x + dir * ApproachSpeed * dt; // 2026-11-24 보스 강화 — 공통 보스 0.9의 1.5배
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
            else if (next == Pattern.Sweep)
            {
                sweepReadyAt = clock + SweepCooldown;
                sweepHitDone = false;
                Enter(State.SweepPrep, SweepPrepTime);
            }
            else
            {
                dashReadyAt = clock + DashCooldown;
                grabHitDone = false;
                Enter(State.DashPrep, DashPrepTime);
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
            bool grabOk = clock >= grabReadyAt && distance <= GrabEngage
                          && !(last == Pattern.Grab && consecutive >= MaxConsecutive);
            bool sweepOk = clock >= sweepReadyAt && distance <= SweepEngage
                           && !(last == Pattern.Sweep && consecutive >= MaxConsecutive);
            // 돌진 잡기(2026-11-24 보스 강화) — 잡기 사거리 밖 ~ DashMaxDistance면 걸어오지 않고 미끄러지며 잡는다
            bool dashOk = clock >= dashReadyAt && distance > DashMinDistance && distance <= DashMaxDistance
                          && !(last == Pattern.DashGrab && consecutive >= MaxConsecutive);
            if (dashOk && !grabOk && !sweepOk) return Pattern.DashGrab;
            if (!grabOk && !sweepOk) return Pattern.None;
            if (grabOk && !sweepOk) return Pattern.Grab;
            if (sweepOk && !grabOk) return Pattern.Sweep;
            return Random.value < 0.5f ? Pattern.Grab : Pattern.Sweep;
        }

        /// <summary>공격 시작 전 방향을 한 번 고정한다 — 공격이 끝날 때까지 다시 안 부른다.</summary>
        /// <summary>
        /// 연계 공격(2026-11-24 사용자 "휘두르기·잡기 반복이라 예측이 쉽다 → 변주") — 휘두르기 직후 확률적으로 후딜 없이 잡기로 잇는다.
        /// 잡기 쿨이 돌았고 대상이 잡기 사거리 안일 때만. 방향은 다시 고정하고(뒤로 빠진 플레이어를 향해), 경고는 평소대로 뜬다.
        /// </summary>
        bool TryComboGrab(Transform target)
        {
            if (!TargetAlive(target) || clock < grabReadyAt) return false;
            if (Mathf.Abs(target.position.x - transform.position.x) > GrabRange) return false;
            if (Random.value >= ComboChanceRuntime) return false;
            consecutive = last == Pattern.Grab ? consecutive + 1 : 1;
            last = Pattern.Grab;
            LockFacing(target);
            grabReadyAt = clock + GrabCooldown;
            grabHitDone = false;
            Enter(State.GrabPrep, ComboGrabPrep);
            return true;
        }

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

        // ───────────────────────── 패턴 3 돌진 잡기(2026-11-24) ─────────────────────────

        void BeginDashWarn()
        {
            if (grabWarning != null)
            {
                // 돌진할 길 전체 — 보스 앞으로 DashLength만큼(맵 끝에서 잘린다) 바닥 직선
                float endX = Mathf.Clamp(transform.position.x + lockedFacing * DashLength, FieldBounds.MinX + 0.3f, FieldBounds.MaxX - 0.3f);
                float len = Mathf.Max(0.5f, Mathf.Abs(endX - transform.position.x));
                Vector2 center = (Vector2)transform.position + new Vector2(lockedFacing * len / 2f, 0f);
                grabWarning.ShowCustom(center, DotShapes.Rect(len, GrabHeight));
                grabWarning.Lock();
            }
            Enter(State.DashWarn, DashWarnTime);
        }

        void BeginDashRun()
        {
            if (grabWarning != null) grabWarning.Flash(); // 경고 사라짐 = 판정 시작(명세서 공통 규칙)
            dashStartX = transform.position.x;
            Enter(State.DashRun, DashLength / DashSpeed + 0.1f);
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
            // 2026-11-24: 예전엔 높이를 그대로 두고 보스 앞 0.7유닛 땅으로만 끌어와서 "앞에 서 있는" 것처럼
            // 보였다 — 이제 그림의 주먹을 따라간다(위 GrabReachHand/GrabHeldHand). 놓아 주는 자리(들어 올린
            // 주먹)는 보스 몸 안쪽이라 보스가 서 있는 빈 공간이다 — 풀려나면 중력이 돌아와 그 자리에서
            // 떨어지므로 지형 속에 끼지 않는다(요청서 "지형을 통과하지 않도록"의 의도는 그대로).
            grabStart = target.position;
            if (body != null && !bodyOrderRaised)
            {
                bodyOrderBeforeGrab = body.sortingOrder;
                body.sortingOrder = bodyOrderBeforeGrab + GrabFrontOrder; // 손이 캐릭터보다 앞에
                bodyOrderRaised = true;
            }

            grabbable.BeginGrab(gameObject);
            HitFeel.Shake(8f, 0.2f); // 잡기 성공(스냇치) 순간 — PlayerHurt 기본 흔들림(7/0.22) 위로 덮어씀(임의 값, 확인 필요)
            Grabbed?.Invoke(target.gameObject);
            Enter(State.GrabPull, GrabPullTime);
        }

        void UpdatePull()
        {
            if (grabbedTarget == null) return;
            var g = grabbedTarget.GetComponent<IGrabbable>();
            g?.MoveGrabbedTo(PullPoint(stateT));
        }

        void HoldGrabbed()
        {
            if (grabbedTarget == null) return;
            var g = grabbedTarget.GetComponent<IGrabbable>();
            Vector2 held = HandPoint(transform.position, lockedFacing, GrabHeldHand);
            held.x += Mathf.Sin(stateT * 55f) * GrabHoldShake; // 손아귀에서 버둥댐
            g?.MoveGrabbedTo(held); // 유지 시간 동안 들어 올린 주먹 자리
        }

        /// <summary>끌어당기기 t초 시점의 대상 위치 — 그림 3장(<see cref="GrabSprite"/>)과 같은 3등분:
        /// 앞 2/3은 처음 자리 → 뻗은 주먹, 마지막 1/3은 뻗은 주먹 → 들어 올린 주먹(살짝 위로 휘는 길).</summary>
        Vector2 PullPoint(float t)
        {
            Vector2 reach = HandPoint(transform.position, lockedFacing, GrabReachHand);
            Vector2 held = HandPoint(transform.position, lockedFacing, GrabHeldHand);
            float u = Mathf.Clamp01(t / GrabPullTime);
            const float Split = 2f / 3f;
            if (u < Split) return Vector2.Lerp(grabStart, reach, EaseOut(u / Split));
            float k = EaseOut((u - Split) / (1f - Split));
            Vector2 p = Vector2.Lerp(reach, held, k);
            p.y += Mathf.Sin(k * Mathf.PI) * 0.15f; // 위로 휘어 들어 올리는 느낌(연출용 임의 값)
            return p;
        }

        /// <summary>
        /// 끌어당기기가 끝난 시점 — 피해를 1회 주고 풀어준다(요청서 "끌어당기기가 끝나면 피해를 준다 →
        /// 이동 제한을 해제한다"). **무적을 무시하고 준다**(<see cref="IDamageable.TakeDamageIgnoringInvuln"/>,
        /// 2026-11-18 사용자가 실제 플레이로 발견) — 끌어당기기(0.5초)+붙잡아둠(0.8초, 2026-11-24 이전 0.2) = 1.3초 동안
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
                HitFeel.Shake(6f, 0.18f); // 휘두르기 명중 순간(임의 값, 확인 필요)
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

        // ───────────────────────── 그림/애니메이션(2026-11-18) ─────────────────────────

        /// <summary>
        /// 몸 그림 — 피격이 최우선(맞은 순간은 어떤 상태든 짧게 덮어쓴다, BlobLordBoss와 같은 이유). 잡기·
        /// 휘두르기는 상태 진행도로 몇 번째 장인지 직접 계산(<see cref="GrabSprite"/>·<see cref="SweepSprite"/>,
        /// fps 안 씀). 후딜레이(<c>GrabRecover</c>/<c>SweepRecover</c>)는 대기 그림의 첫 자세로 돌아가 다음
        /// 대기 되풀이가 자연스럽게 이어지게 한다. 대기·이동은 시간 기반 되풀이(<see cref="isMoving"/>은
        /// <see cref="IdleApproach"/>가 매긴다).
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
            Sprite s;
            if (state == State.GrabRecover || state == State.SweepRecover || state == State.DashRecover)
            {
                s = At(idleArt, 0); // 후딜레이 — 다음 대기 되풀이가 시작되기 전까지 대기 그림의 첫 자세로
            }
            else if (state == State.Idle)
            {
                if (isMoving)
                {
                    moveCycleT += dt;
                    s = moveArt != null ? moveArt.FrameAt(moveCycleT) : null;
                }
                else
                {
                    idleCycleT += dt;
                    s = idleArt != null ? idleArt.FrameAt(idleCycleT) : null;
                }
            }
            else
            {
                s = GrabSprite() ?? SweepSprite();
            }
            if (s != null) body.sprite = s;
        }

        /// <summary>잡기 7장 — 준비 1(0) · 경고 1(1) · 팔 뻗기 1(2) · 끌어당기기 3(3~5) · 붙잡아둠 1(6).</summary>
        Sprite GrabSprite()
        {
            switch (state)
            {
                case State.GrabPrep: return At(grabArt, 0);
                case State.GrabWarn: return At(grabArt, 1);
                case State.GrabThrust: return At(grabArt, 2);
                case State.GrabPull: return At(grabArt, 3 + Mathf.Clamp(Mathf.FloorToInt(stateT / GrabPullTime * 3f), 0, 2));
                case State.GrabHold: return At(grabArt, 6);
                // 돌진 잡기(2026-11-24) — 전용 그림이 없어 잡기 그림을 쓴다: 준비 0 · 경고 1 · 돌진 2(팔 뻗기)
                case State.DashPrep: return At(grabArt, 0);
                case State.DashWarn: return At(grabArt, 1);
                case State.DashRun: return At(grabArt, 2);
                default: return null;
            }
        }

        /// <summary>휘두르기 5장 — 준비 1(0) · 경고 2(1~2) · 판정 2(3~4).</summary>
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

        static Sprite At(SpriteFlipbook b, int idx) => b != null && idx >= 0 && idx < b.Count ? b.frames[idx] : null;

        /// <summary>
        /// <see cref="IDeathAnimated"/> — BlobLordBoss.PlayDeathAndDestroy와 같은 방식. <see cref="EnemyHealth.Die"/>가
        /// 즉시 Destroy하는 대신 이걸 부른다. 사망 그림 8장을 재생한 뒤 스스로 Destroy한다. 죽는 순간 경고 표시를
        /// 치우고 콜라이더를 꺼서(레이저·유도 대상 고르기가 시체를 겨누지 않게) **붙잡고 있던 대상은 즉시
        /// 풀어준다**(죽는 순간까지 잡혀 있으면 영구히 안 풀리는 버그를 막기 위해 — OnDisable을 기다리지 않는다).
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
                yield return new WaitForSeconds(step);
            }
            Destroy(gameObject);
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
