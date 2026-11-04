using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 섬영(閃影) — 관성 이동(가속) + 회전베기(기본공격, Z) + 흡혈 + 방향키 피격무적(0차) +
    /// 집중/칼날폭풍 1층(전문화 X 스킬, 2026-09-28 추가) + 2층(예리한 감각·낙하 충격, 2026-09-29 추가) +
    /// 3층(짙은 잔영·벽 반사, 2026-09-30 추가) + 4층(궤적 파열·즉시 가속·강제 연행, 2026-09-30 추가) +
    /// 5층(재사용 즉시 파열·쿨타임 환급·곡예비행, 2026-09-30 추가).
    /// 원본 `bladeMove`/`bladeSpinTick`/`bladeLifesteal`/`bladeDirInvuln`/`bladeFocusStart`/
    /// `bladeTrailUpdate`/`bladeRipTrail`/`bladeStormStart`/`bladeStormUpdate`
    /// (project_test.html:2420~2738)을 옮겼다.
    ///
    /// 이동 자체(가속/감속/방향전환)는 <see cref="CharacterMover2D"/>의 Inertial 모드가 맡는다 —
    /// 파일 소유권 규칙상(`docs/worksplit.md`) 이 키트는 그 파일을 고치지 않고, 매 프레임
    /// <see cref="CharacterMover2D.MoveScale"/>·<see cref="CharacterMover2D.AccelMultiplier"/>(둘 다
    /// 그 파일이 이미 노출해 둔 공개 필드)만 넣어준다. 칼날폭풍의 "충돌 판정 없이 활공"은
    /// `CharacterMover2D.FixedUpdate`를 건드리지 않고, 매 프레임 <see cref="LateUpdate"/>에서
    /// `Rigidbody2D.position`을 직접 덮어써서(원본 `MageAttack.Teleport`와 같은 기법을 매 프레임
    /// 반복) 구현했다 — 물리 충돌 자체가 안 걸리므로 발판을 실제로 통과한다.
    ///
    /// ⚠️ **스킬트리(갈래 선택·SP 소비) 자체는 아직 로비 UI가 없다.** `Core/PlayerProfile.
    /// TryLearnBladeTier`(팀원 추가, 팀장 확인 필요 — 그 파일 주석 참고)로 데이터는 있지만,
    /// `UI/LobbyScreen.DrawSpecTab`은 여전히 마법사 전용이라 실제 플레이에서 갈래를 고를 방법이
    /// 없다(UI는 이 키트 소유가 아니다). 지금은 테스트에서 `TryLearnBladeTier`를 직접 불러
    /// 검증한다 — 로비 화면 연결은 별도 작업(팀장 담당 또는 추후 요청).
    ///
    /// **1층 범위**(`SPEC.blade.move/conv.tiers[0]`, project_test.html:937·944): 집중은 가속 증가 +
    /// 궤적 피해, 칼날폭풍은 공중 활공 + 회전베기 유지만.
    /// **2층 범위**(`tiers[1]`, :938·945) 추가: 집중은 가속 배율이 한 번 더 커지고(`accelMul2`),
    /// 칼날폭풍은 착지 시 광역 낙하 충격(`bladeStormLand`)이 붙는다. 최고 속도 충돌 무적
    /// (`bladeCrashImmune` :2444)도 "집중 2층 또는 칼날폭풍 3층" 조건 그대로 같이 넣어 뒀었다.
    /// **3층 범위**(`tiers[2]`, :939·946) 추가: 집중 "짙은 잔영"은 궤적 지속시간·피해·반경이
    /// `t3Life`(1.45)·`t3Dmg`(1.8)·`t3R`(1.3)배로 커진다(:2537~2539, 캐스팅 순간 티어로 지속시간은
    /// 고정되고 피해·반경은 원본처럼 매 틱 현재 티어로 재계산). 칼날폭풍은 맵 가장자리에 닿으면
    /// 종료 대신 반대 방향으로 반사한다(`st.dir *= -1`, :2683~2686) — 이제서야 칼날폭풍 3층이
    /// 실전에서 열리므로, 2층 때 미리 넣어 둔 `bladeCrashImmune`의 "칼날폭풍≥3" 절반도 드디어
    /// 실제로 걸리게 됐다. 다만 그러려면 칼날폭풍 중 속도 비율을 알아야 하는데 `ComputeSpeedRatio()`는
    /// 물리 속도(칼날폭풍 중엔 일부러 0으로 비움)를 보므로 못 쓴다 — `UpdateStorm`이 매 프레임 직접
    /// 계산해 <see cref="stormSpeedRatio"/>에 담아 두면 다음 프레임 `Update()`가 그 값을 읽는 식으로
    /// 이었다(한 프레임 지연이 있지만 `ApplyCrashImmunity` 자체가 이미 근사 기법이라 무시할 오차).
    /// **4층 범위**(`tiers[3]`, :940·947, project_test.html:2570~2589·2691~2735) 추가 — 집중은
    /// "궤적 파열"(<see cref="RipTrail"/>이 남은 궤적 위 적을 한 번 더 베는 폭발)과 시전 즉시
    /// 최고 속도로 가속(<see cref="TryFocusStart"/>·<see cref="UpdateFocus"/> 주석의 근사 기법 참고),
    /// 칼날폭풍은 스치는 적을 붙잡아 끌고 다니다가(<see cref="UpdateStorm"/>의 강제 연행 구간) 종료
    /// 시 낙하시켜 착지 피해를 준다(<see cref="UpdateFalling"/>). 붙잡기는 보스·성소를 제외해야
    /// 하는데(원본 `e.boss || e.shrine`, :2694) `Characters`가 `Enemies` 타입을 직접 못 봐서
    /// <see cref="IGrabExempt"/> 마커 인터페이스를 새로 추가했다(사용자 확인, `Core/IGrabExempt.cs`
    /// 주석 참고 — 팀장 확인 필요).
    /// **5층 범위**(`tiers[4]`, :941·948, project_test.html:2526·2594~2608·2662~2673) 추가 — 집중은
    /// 궤적이 아직 남아 있는 동안 재사용하면 새 궤적을 깔기 전에 남은 궤적을 먼저 찢고(<see
    /// cref="TryFocusStart"/>), 최고 속도로 적을 스쳐 지나갈 때마다(적당 0.5초에 한 번) 집중
    /// 쿨타임을 깎는다(<see cref="UpdatePassTick"/> — Focus 갈래 전용, Storm 갈래에선 원본도
    /// `bladeTier('move')`가 0이라 아무 효과가 없다). 칼날폭풍은 활공 중 점프키를 누르면 종료 대신
    /// 위로 튀어 오르며 칼날폭풍 쿨타임을 즉시 돌려준다(<see cref="UpdateStorm"/>의 곡예비행 분기).
    ///
    /// **0차에 없는 것**(전부 `docs/worksplit.md`/`HANDOFF.md` "0차" 정의 밖 — 전문화 티어·X스킬 필요):
    /// - `p.bladeSpin` 회전 연출 타이머(:2512, 순수 시각효과) — 프로젝트 관례상 연출은 범위 밖.
    /// - `CONFIG.blade.brake`(:615, 2200) — 원본 `bladeMove`도 실제로는 안 쓰는 죽은 값이다
    ///   (`CharacterMover2D` 쪽 주석 참고). 여기서도 새로 만들어 넣지 않는다.
    ///
    /// **상시 적용**(티어 무관 — 그래서 0차에도 있다): 속도→피해 배수(`bladeDmgMult`)와 방향키
    /// 피격무적(`bladeDirInvuln`) — 원본이 조건절에 티어 체크를 안 넣은 것만 확인하고 그대로 옮겼다.
    /// </summary>
    public class BladeCombat : MonoBehaviour, ICharacterKit, IRunResettable
    {
        [Header("원본 CONFIG.blade.spin 그대로 (거리는 100px=1유닛, project_test.html:624)")]
        [Tooltip("표시용 현재 쿨다운. 매 프레임 SpinCd ÷ 공격속도 배수로 다시 계산된다.")]
        public float cooldown = SpinCd;
        [Tooltip("회전베기 판정 반경. 원본 spin.r 105px ÷100.")]
        public float spinRadius = SpinRadius;
        [Tooltip("적 판정용 레이어. 기본값은 전체 — Enemy 전용 레이어를 쓰기 전까지는 태그로 한 번 더 거른다(PlayerAttack과 동일한 관례).")]
        public LayerMask enemyMask = ~0;

        const float SpinCd = 0.13f;             // CONFIG.blade.spin.cd (:624)
        const float SpinDmgMult = 0.42f;        // CONFIG.blade.spin.dmg (:624)
        const float SpinRadius = 1.05f;         // CONFIG.blade.spin.r 105px÷100 (:624)
        const float SpinKnockback = 1.10f;      // bladeSpinHit 호출부 kb:110 (:2489) ÷100
        const float SpeedDmgAtTop = 1.4f;       // CONFIG.blade.spdDmg — 최고 속도에서 피해 +140% (:619)
        const float SpeedDmgPerMsSurplus = 1.4f;// CONFIG.blade.spdDmgPerMs — 100% 넘긴 이속 1.0당 추가 (:620)
        const float LifestealRatio = 0.15f;     // CONFIG.blade.lifesteal (:625)
        const float DirInvulnRefresh = 2f;      // 방향키 유지 중 매 프레임 갱신하는 무적 여유분(근사 — 아래 ApplyDirectionalInvuln 참고)
        const float TopAtRatio = 0.92f;         // CONFIG.blade.topAt (:621) — bladeAtTop() 판정 기준.

        [Header("집중(1~5층) — CONFIG.blade.focus (project_test.html:627)")]
        const float FocusDur = 2.0f;            // F.dur
        const float FocusCd = 11f;              // F.cd
        const float FocusAccelMul = 1.7f;       // F.accelMul (1층)
        const float FocusAccelMul2 = 1.4f;      // F.accelMul2 — 2층부터 accelMul에 추가로 곱한다(:2462).
        const float TrailLife = 3.2f;           // F.trailLife
        const float TrailTickInterval = 0.22f;  // F.trailTick
        const float TrailDmgMult = 0.09f;       // F.trailDmg
        const float TrailRadius = 0.34f;        // F.trailR 34px÷100
        const float TrailPointSpacing = 0.24f;  // 원본 24px÷100 (:2548)
        const int TrailPointCap = 240;          // 원본 240 (:2550)
        const float FocusT3LifeMult = 1.45f;    // F.t3Life — 3층 "짙은 잔영"(:2537, 캐스팅 순간 고정).
        const float FocusT3DmgMult = 1.8f;      // F.t3Dmg — 3층부터(:2538, 매 틱 현재 티어로 재계산).
        const float FocusT3RadiusMult = 1.3f;   // F.t3R — 3층부터(:2539, 매 틱 현재 티어로 재계산).
        const float FocusT4RipRadiusMult = 1.7f;// 궤적 파열 반경 = bladeTrailR()*1.7 (:2576).
        const float FocusT4RipDmgMult = 1.35f;  // F.ripDmg (:610).
        const float FocusT4RipKnockback = 2.8f; // bladeRipTrail 호출부 kb:280 (:2582) ÷100.
        [Tooltip("4층 '즉시 최고 속도' 근사에 쓰는 1프레임 가속 배율. 아래 TryFocusStart/UpdateFocus 주석 참고.")]
        const float FocusT4InstantAccelMultiplier = 2000f;
        const float PassCooldownReduction = 0.55f; // F.cdRedPerPass — 패스당 집중 쿨타임 감소량(:610).
        const float PassTickInterval = 0.5f;        // bladePassTick의 "적당 재적용 간격" 리터럴(:2603).

        [Header("칼날폭풍(1~5층) — CONFIG.blade.storm (project_test.html:636)")]
        const float StormCd = 8f;               // S.cd
        const float StormAngleDeg = 30f;        // S.angle
        const float StormAccel = 15f;           // S.accel 1500px/s²÷100
        const float StormMaxDur = 3.2f;         // S.maxDur
        const float StormMaxSpeedMult = 1.4f;   // bladeStormUpdate의 속도 상한 배수(:2675)
        const float StormDmgMult = 0.5f;        // S.dmg
        const float StormTickInterval = 0.11f;  // bladeStormUpdate의 회전베기 유지 간격(:2710)
        const float StormSpinRadiusMult = 1.15f;// CONFIG.blade.spin.r × 1.15 (:2712)
        const float StormLandRadius = 2.1f;     // S.landR 210px÷100 (:2645)
        const float StormLandDmgMult = 1.6f;    // S.landDmg (:2646)
        const float StormLandKnockback = 3.2f;  // bladeStormLand 호출부 kb:320 (:2647) ÷100
        const float StormLandYOffset = 0.24f;   // 원본 `p.y - 24`(:2645) — 발밑 기준 위로 24px÷100.
        const float StormGrabRadius = 0.74f;    // 강제 연행 판정 반경 74px÷100 (:2701, 리터럴).
        const int StormGrabMax = 6;             // S.grabMax — 동시 최대 연행 수(:2696).
        const float StormGrabOffsetXMin = -0.3f;// 원본 rand(-30,30)px÷100 (:2704).
        const float StormGrabOffsetXMax = 0.3f;
        // 원본 rand(-24,12)px÷100(:2704) — Y축 반전(클래스 주석 "좌표계 주의")이라 부호도 뒤집는다:
        // 원본 -24(위쪽) → 여기 +0.24, 원본 +12(아래쪽) → 여기 -0.12.
        const float StormGrabOffsetYMin = -0.12f;
        const float StormGrabOffsetYMax = 0.24f;
        const float StormFallDmgMult = 1.1f;    // S.fallDmg (:637)
        const float StormFallKnockback = 1.0f;  // bladeFallingUpdate 호출부 kb:100 (:2735) ÷100
        const float StormFallGravity = 26f;     // W.gravity 2600px/s²÷100 — 전역 Physics2D.gravity와 동일 크기.
        // 5층 곡예비행(project_test.html:2662~2673) — hopVy는 원본이 Y+아래라 음수(위로 솟구침)인데,
        // 이 포팅은 Y+가 위라 부호를 뒤집는다: 원본 -720px/s÷100 → 여기 +7.2.
        const float StormHopVy = 7.2f;          // S.hopVy -720px/s÷100, 부호 반전.
        const float StormHopSpeedRatio = 0.55f; // S.hopSpdK — stormSpd(이미 포트 단위)에 곱해서 더한다.

        CharacterMover2D mover;
        Rigidbody2D rb;
        Collider2D col;
        PlayerHealth playerHealth;
        float cdTimer;

        /// <summary>bladeMsSurplus — 100%를 넘겨 속도로 못 쓰는 이속 초과분(project_test.html:2428). Update에서 매 프레임 갱신.</summary>
        float msSurplus;

        // ---- 집중(move 갈래) 상태 ----
        float focusT;
        float focusCd;
        float trailT;
        float trailTickTimer;
        readonly List<Vector2> trailPts = new List<Vector2>();
        readonly HashSet<Collider2D> trailHitBuffer = new HashSet<Collider2D>();

        /// <summary>5층 쿨타임 환급 — 원본 `p.spinHits`(project_test.html:2597)와 같은 자리. 적별로
        /// 마지막으로 패스 판정을 받은 시각(`Time.time`)을 기억해 <see cref="PassTickInterval"/>
        /// 안에는 중복 적용을 막는다.</summary>
        readonly Dictionary<Collider2D, float> passHitTimestamps = new Dictionary<Collider2D, float>();

        // ---- 칼날폭풍(conv 갈래) 상태 ----
        bool storming;
        int stormDir;
        float stormSpd;
        float stormT;
        float stormHitT;
        float stormCd;

        /// <summary>
        /// bladeSpeedRatio를 칼날폭풍 중에도 알 수 있게 <see cref="UpdateStorm"/>이 매 프레임 자기
        /// 계산값을 담아 두는 자리 — `Update()`의 <see cref="ApplyCrashImmunity"/> 게이팅(칼날폭풍
        /// 3층)이 다음 프레임에 읽는다. storming이 아닐 때는 무의미한 값이라도 상관없다(그때는
        /// `Update()`가 이 필드 대신 <see cref="ComputeSpeedRatio"/>를 쓴다).
        /// </summary>
        float stormSpeedRatio;

        /// <summary>
        /// 4층 "즉시 최고 속도" 캐스팅 요청을 다음 <see cref="UpdateFocus"/> 호출(=다음 물리 스텝 이전)
        /// 까지만 들고 있는 1회성 플래그. <see cref="TryFocusStart"/>/<see cref="UpdateFocus"/> 주석 참고.
        /// </summary>
        bool pendingInstantAccel;

        /// <summary>붙잡아 끌고 다니는 적 하나의 기록 — 원본 `p.stormGrab`의 원소(`{e, ox, oy}`, :2705).</summary>
        struct GrabRecord
        {
            public Collider2D col;
            public float offsetX;
            public float offsetY;
        }
        /// <summary>4층 강제 연행 중인 적 목록. 칼날폭풍 하나가 지속되는 동안만 채워진다(원본 `p.stormGrab`).</summary>
        readonly List<GrabRecord> stormGrab = new List<GrabRecord>();

        /// <summary>붙잡혔다 놓여서 추락 중인 적 하나의 기록 — 원본 `e.stormFall`/`e.fallVy`(:2727~2734).</summary>
        struct FallRecord
        {
            public Collider2D col;
            public float fallVy;
        }
        /// <summary>
        /// 연행이 끝나 낙하 중인 적 목록 — <see cref="storming"/>과 무관하게 매 프레임(<see cref="LateUpdate"/>)
        /// 갱신된다(원본 `bladeFallingUpdate`가 `bladeStormUpdate`와 별도로 항상 도는 것과 동일, :2724~2737).
        /// </summary>
        readonly List<FallRecord> stormFalling = new List<FallRecord>();

        public CharacterId Character => CharacterId.Blade;
        /// <summary>섬영만 관성 가속 이동을 쓴다(원본 `bladeMove`, 나머지 셋은 즉시-속도).</summary>
        public CharacterMover2D.MoveMode RequiredMoveMode => CharacterMover2D.MoveMode.Inertial;

        public void OnSelected()
        {
            cdTimer = 0f;
            focusT = 0f;
            focusCd = 0f;
            trailT = 0f;
            trailTickTimer = 0f;
            trailPts.Clear();
            storming = false;
            stormDir = 1;
            stormSpd = 0f;
            stormT = 0f;
            stormHitT = 0f;
            stormCd = 0f;
            stormSpeedRatio = 0f;
            pendingInstantAccel = false;
            stormGrab.Clear();
            stormFalling.Clear();
            passHitTimestamps.Clear();
            if (mover != null) mover.AccelMultiplier = 1f;
        }

        public void OnDeselected() { }

        /// <summary>새 사냥 시작 시 쿨다운/스킬 상태 초기화 — 원본 `resetPlayerForRun()`의 `p.atkCds`·
        /// `p.focusT`·`p.storm` 등(:1516 언저리, 런 전환 시 전부 비운다).</summary>
        public void ResetForRun() => OnSelected();

        void Awake()
        {
            mover = GetComponent<CharacterMover2D>();
            rb = GetComponent<Rigidbody2D>();
            col = GetComponent<Collider2D>();
            playerHealth = GetComponent<PlayerHealth>();
        }

        /// <summary>
        /// Run을 벗어나는 순간 집중·칼날폭풍 상태를 전부 정리한다 — 아래 Update/LateUpdate 가드만으로는
        /// "스킬을 쓰는 중에 죽어서 Run을 벗어난" 경우 궤적·연행·추락 중인 적 목록이 그대로 남는 걸
        /// 못 막는다(그 콜백들 자체가 멈추니 정리할 코드도 같이 멈춘다). <see cref="OnSelected"/>가
        /// 이미 하는 전체 초기화를 그대로 재사용한다.
        /// </summary>
        void OnEnable() => GameState.Changed += HandleGameStateChanged;
        void OnDisable() => GameState.Changed -= HandleGameStateChanged;
        void HandleGameStateChanged(GameScene scene)
        {
            if (scene != GameScene.Run) OnSelected();
        }

        void Update()
        {
            var profile = ProfileService.Current;

            // bladeMsRaw() = statMs() * 성소 버프(project_test.html:2422~2423).
            // MoveScale은 100%에서 잘리고(bladeMsK, :2426) — CharacterMover2D가 실제 속도·가속에 반영한다.
            // 넘친 분(bladeMsSurplus, :2428)은 여기서 들고 있다가 SpeedDamageMultiplier에서 피해로 환산된다.
            float msRaw = PlayerStatCalculator.ComputeMoveSpeedMultiplier(profile) * CombatModifiers.MoveSpeedMultiplier;
            if (mover != null) mover.MoveScale = Mathf.Min(1f, msRaw);
            msSurplus = Mathf.Max(0f, msRaw - 1f);

            // 원본 `p.atkCds.blade = S.cd / Math.max(0.35, statAs())`(:2514) — 표시용으로 매 프레임 재계산.
            cooldown = SpinCd / Mathf.Max(0.35f, PlayerStatCalculator.ComputeAttackSpeedMultiplier(profile));

            cdTimer -= Time.deltaTime;
            focusCd -= Time.deltaTime;
            stormCd -= Time.deltaTime;

            // 로비 등 Run 밖에서는 "새 입력을 받아 액션을 거는" 이 두 블록만 막는다 — 위 쿨다운·이동
            // 배율 재계산과 아래 UpdateFocus/ApplyDirectionalInvuln/ApplyCrashImmunity/UpdatePassTick은
            // 그대로 둬야 한다(PlayMode 테스트가 TryFocusStart/TryStormStart를 리플렉션으로 직접 불러
            // 놓고 이 콜백들이 자연스럽게 매 프레임 도는 것에 의존한다 — 테스트의 GameState는 기본
            // Lobby라 Update() 전체를 막으면 그 테스트들이 깨진다). 막아야 하는 건 GameInput.AttackHeld/
            // UltDown을 "새로" 읽어 공격·스킬을 거는 지점뿐이다 — GameInput.MenuConfirmDown이 같은
            // Z를 메뉴 확인에도 쓰기 때문에(10-24), 로비 진입 직후 메뉴를 확인하려는 Z가 그대로
            // 회전베기로 읽히는 게 이번 버그였다.
            if (GameState.IsRunning)
            {
                // 칼날폭풍 중엔 원본도 회전베기(Z)를 별도로 안 받는다 — bladeStormUpdate 자체가 이미
                // 매 0.11초 회전베기를 유지시킨다(:2708~2713). 여기 Z는 storm이 아닐 때만 반응한다.
                if (!storming && GameInput.AttackHeld && cdTimer <= 0f)
                {
                    cdTimer = cooldown;
                    float dmgMult = SpinDmgMult * SpeedDamageMultiplier(ComputeSpeedRatio());
                    SpinHit(dmgMult, spinRadius, false, true);
                }

                // 원본 tryBladeSkill(:2739~2745) — 전문화(갈래)를 아직 안 골랐으면 X는 아무 일도 안 한다.
                if (GameInput.UltDown && profile.bladeBranch != BladeBranch.None)
                {
                    if (profile.bladeBranch == BladeBranch.Focus) TryFocusStart(profile.bladeTier);
                    else TryStormStart(profile.bladeTier);
                }
            }

            UpdateFocus(Time.deltaTime);

            // bladeDirInvuln(:2448~2451) — 방향키를 누르고 있는 동안(어느 방향이든) 상시 피격 무적.
            ApplyDirectionalInvuln(GameInput.Left || GameInput.Right || GameInput.Up || GameInput.Down);

            // bladeCrashImmune(:2444~2446) — 최고 속도(topAt 92%) + (집중 2층 또는 칼날폭풍 3층)면
            // 상시 충돌 무적. 칼날폭풍 중엔 ComputeSpeedRatio가 물리 속도(일부러 0으로 비움)를 보므로
            // 못 쓰고, UpdateStorm이 매 프레임 직접 계산해 둔 stormSpeedRatio를 대신 읽는다(클래스
            // 주석 "3층 범위" 참고 — 한 프레임 지연 근사).
            bool atTop = CurrentSpeedRatio() >= TopAtRatio;
            bool crashImmune = atTop &&
                ((profile.bladeBranch == BladeBranch.Focus && profile.bladeTier >= 2) ||
                 (profile.bladeBranch == BladeBranch.Storm && profile.bladeTier >= 3));
            ApplyCrashImmunity(crashImmune);

            // bladePassTick(:2594~2608) — 원본은 storming 여부와 무관하게 매 프레임 부른다(:2811·2876).
            UpdatePassTick(atTop);
        }

        /// <summary>
        /// 칼날폭풍의 위치 갱신만 물리 스텝 이후(LateUpdate)로 미룬다 — 원본 "충돌 판정 없음"
        /// (활공 중 발판을 그냥 통과)을 재현하려면 `CharacterMover2D.FixedUpdate`가 이번 프레임에
        /// 계산한 물리 결과 위에 우리가 마지막으로 덮어써야 한다(그 파일은 못 고치므로 순서로 이긴다).
        /// </summary>
        void LateUpdate()
        {
            // 여기는 안 막는다 — PlayMode 테스트가 TryStormStart를 리플렉션으로 직접 불러 storming을
            // 켜 놓고 이 콜백이 매 프레임 자연스럽게 도는 것(이동·벽 반사·연행·낙하 판정)에 의존한다.
            // Run을 벗어나면서 storming이 그대로 true로 남는 위험은 위 HandleGameStateChanged가
            // OnSelected()로 즉시 storming=false를 포함해 전부 정리해 주므로, 이 콜백을 막을 필요가 없다.
            if (storming) UpdateStorm(Time.deltaTime);
            // bladeFallingUpdate(project_test.html:2724~2737) — storming 여부와 무관하게 항상 돈다.
            UpdateFalling(Time.deltaTime);
        }

        /// <summary>
        /// bladeSpeedRatio (project_test.html:2435) — 실제 수평 속도 ÷ 현재 최고 속도.
        /// `CharacterMover2D`는 관성 속도(`inertialVx`)를 캡슐화해서 밖으로 안 내보낸다(그 파일을
        /// 고치는 건 팀장 담당이라 여기서 필드를 늘려달라고 하지 않는다) — 대신 같은 오브젝트의
        /// `Rigidbody2D`에서 실제로 적용된 속도를 그대로 읽는다. `CharacterMover2D.FixedUpdate`가
        /// 매 스텝 그 값을 `rb.linearVelocity.x`에 쓰기 때문에 결과는 동일하다.
        ///
        /// ⚠️ 칼날폭풍 중에는 안 쓴다 — 그 동안은 물리 속도를 일부러 0으로 비워 두므로(아래
        /// <see cref="UpdateStorm"/>) 대신 그 자리에서 직접 계산한 비율을 넘긴다.
        /// </summary>
        float ComputeSpeedRatio()
        {
            if (mover == null || rb == null) return 0f;
            float maxSpeed = mover.inertialMaxSpeed * mover.MoveScale; // bladeMaxSpeed (:2429)
            if (maxSpeed <= 0f) return 0f;
            return Mathf.Clamp01(Mathf.Abs(rb.linearVelocity.x) / maxSpeed);
        }

        /// <summary>
        /// bladeSpeedRatio를 "지금 상태에 맞게" 읽는 자리 — 칼날폭풍 중엔 <see cref="stormSpeedRatio"/>
        /// (물리 속도를 일부러 비워 둔 동안의 대체값), 아니면 <see cref="ComputeSpeedRatio"/>. 원본은
        /// 어디서 호출하든 `p.vx` 기준의 같은 함수 하나뿐이라, 4층 궤적 파열(<see cref="RipTrail"/>)·
        /// 낙하 충격(<see cref="UpdateFalling"/>)의 `bladeDmgMult()`도 이 값을 그대로 쓴다.
        /// </summary>
        float CurrentSpeedRatio() => storming ? stormSpeedRatio : ComputeSpeedRatio();

        /// <summary>붙잡기 목록에 이미 들어 있는 적인지 — 회전베기(<see cref="SpinHit"/>의 skipGrabbed,
        /// 원본 :2486)와 붙잡기 재획득 중복 방지에 같이 쓴다.</summary>
        bool IsGrabbed(Collider2D c)
        {
            for (int i = 0; i < stormGrab.Count; i++)
                if (stormGrab[i].col == c) return true;
            return false;
        }

        /// <summary>
        /// bladeDmgMult (project_test.html:2439~2442) — 정지 ×1 → 최고 속도 ×(1+spdDmg)=×2.4.
        /// 100%를 넘긴 이속(<see cref="msSurplus"/>)은 속도로 못 나가는 대신 여기서 추가로 곱해진다.
        /// </summary>
        float SpeedDamageMultiplier(float speedRatio)
        {
            return 1f + speedRatio * (SpeedDmgAtTop + msSurplus * SpeedDmgPerMsSurplus);
        }

        /// <summary>
        /// 회전베기 판정 — 원본 `bladeSpinHit(dmgMult, r, skipGrabbed, lifesteal)`(project_test.html:
        /// 2480~2496). Z 기본공격(<see cref="Update"/>)과 칼날폭풍 유지 틱(<see cref="UpdateStorm"/>)이
        /// 이 한 함수를 공유한다 — 원본도 같은 함수를 두 곳에서 부른다.
        /// `dmgMultiplier`는 이미 각 호출부에서 (기본 배율 × SpeedDamageMultiplier)까지 곱해서 넘긴다
        /// (= 원본 `dmgMult * bladeDmgMult()`).
        /// </summary>
        void SpinHit(float dmgMultiplier, float radius, bool skipGrabbed, bool lifesteal)
        {
            var profile = ProfileService.Current;
            float baseDamage = PlayerStatCalculator.ComputeAtk(profile) * dmgMultiplier;
            float critChance = PlayerStatCalculator.ComputeCritChance(profile);

            var hits = Physics2D.OverlapCircleAll(transform.position, radius, enemyMask);
            float totalDealt = 0f;
            bool anyHit = false;

            foreach (var col in hits)
            {
                if (col == null || !col.CompareTag("Enemy")) continue;
                // 4층 붙잡기 제외(:2486) — 강제 연행 중인 적은 회전베기(유지 틱 포함)에서 빠진다.
                if (skipGrabbed && IsGrabbed(col)) continue;

                // 원본 `e.spawnInvuln > 0` 스킵(:2485) — Characters는 Enemies를 직접 참조할 수 없어
                // (asmdef 계층 규칙) Core의 ISpawnProtectable로만 상태를 묻는다.
                var protectable = col.GetComponent<ISpawnProtectable>();
                if (protectable != null && protectable.IsSpawnProtected) continue;

                // 원본 `e.dead` 스킵(:2485).
                var target = col.GetComponent<IDamageable>();
                if (target == null || target.IsDead) continue;

                int dmg = DamageCalculator.Roll(baseDamage, critChance, out _);

                // 원본 kbDir: sign(e.x - p.x) || p.facing (:2489) — 정확히 겹친 경우만 facing으로 대체.
                float dirSign = Mathf.Sign(col.transform.position.x - transform.position.x);
                if (Mathf.Approximately(dirSign, 0f)) dirSign = mover != null ? mover.Facing : 1f;

                target.TakeDamageWithKnockback(dmg, gameObject, dirSign, SpinKnockback);

                totalDealt += dmg;
                anyHit = true;
            }

            // bladeLifesteal(:2498~2508) — 이번 회전베기로 입힌 피해 "합계"의 15%를 한 번에 회복한다
            // (적중마다가 아니라 틱 전체 총합 기준 — 원본 `totalDmg` 누적과 동일). 칼날폭풍 유지 틱은
            // lifesteal=false로 불러 대상에서 제외한다(:2479 주석 그대로).
            if (lifesteal && anyHit && totalDealt > 0f && playerHealth != null)
            {
                float heal = Mathf.Round(totalDealt * LifestealRatio);
                if (heal > 0f) playerHealth.Heal(heal);
            }
        }

        /// <summary>
        /// 기존 PlayMode 테스트(`BladeCombatTests.cs`, 0차분)가 리플렉션으로 부르는 이름·시그니처를
        /// 그대로 유지하기 위한 래퍼 — 0차 회전베기(Z)는 항상 `spinRadius`·`skipGrabbed=false`·
        /// `lifesteal=true`였다. 실제 로직은 <see cref="SpinHit"/>로 옮겼다(칼날폭풍 유지 틱과
        /// 매개변수를 공유하려고 시그니처를 늘렸다) — 이 메서드가 없어지면 기존 테스트가 리플렉션
        /// 단계에서 못 찾아 깨진다.
        /// </summary>
        void SpinAttack(float dmgMultiplier) => SpinHit(dmgMultiplier, spinRadius, false, true);

        /// <summary>
        /// 집중 진입 — 원본 `bladeFocusStart()`(project_test.html:2521~2535)의 1~5층 범위.
        ///
        /// **5층**: 재사용(취소) 분기를 통과한 뒤에도 궤적이 아직 남아 있으면(`trailPts.Count>1`)
        /// 새 궤적을 깔기 전에 <see cref="RipTrail"/>로 먼저 찢는다(:2526) — 쿨다운이 돌아 다시
        /// 캐스팅할 수 있게 된 시점엔 이전 궤적이 `trailT` 자연 소멸 전일 수 있어서(집중 지속시간
        /// 2초 &lt; 궤적 지속시간 3.2초+) 생기는 상황이다.
        ///
        /// **4층 "즉시 최고 속도"**(`p.vx = (p.runDir||p.facing) * bladeMaxSpeed()`, :2529)는 여기서
        /// 바로 적용하지 못한다 — `CharacterMover2D.inertialVx`가 private이라(그 파일은 팀장 소유,
        /// 고치지 않기로 함) 속도를 순간이동시키듯 못 박을 방법이 없다. 대신 <see cref="pendingInstantAccel"/>
        /// 플래그만 올려 두고, 실제 가속은 <see cref="UpdateFocus"/>가 다음 물리 스텝 전에 초강력
        /// `AccelMultiplier`로 근사한다 — 자세한 한계는 그쪽 주석 참고.
        /// </summary>
        void TryFocusStart(int tier)
        {
            if (tier <= 0) return; // 방어적 체크 — branch!=None이면 항상 tier>=1이라 실제로는 안 걸린다.
            if (focusT > 0f) { focusT = 0f; return; } // 재사용 즉시 중단(:2524) — 트레일은 안 건드린다.
            if (focusCd > 0f) return; // 쿨다운 중(:2525)

            if (tier >= 5 && trailPts.Count > 1) RipTrail(); // 남은 궤적부터 먼저 찢는다(:2526).

            focusT = FocusDur;
            focusCd = FocusCd;
            // bladeTrailLife()(:2537) — 3층 "짙은 잔영"부터 지속시간이 늘어난다. 원본처럼 캐스팅
            // 순간의 티어로 한 번만 계산해 담아 둔다(지속시간은 focus와 별개로 그냥 줄어드는 타이머라
            // 매 틱 재계산할 대상이 아니다 — 매 틱 재계산하는 건 아래 피해·반경 쪽).
            trailT = TrailLife * (tier >= 3 ? FocusT3LifeMult : 1f);
            trailPts.Clear();

            if (tier >= 4) pendingInstantAccel = true;
        }

        /// <summary>
        /// 집중 가속 배율 + 궤적 갱신/틱 피해 — 원본 `bladeMove`의 accelMul 항(:2462)과
        /// `bladeTrailUpdate`(:2541~2569)를 합쳤다. 2층부터 `accelMul2`가 추가로 곱해진다
        /// (원본 `accelMul * (t2 ? accelMul2 : 1)`).
        ///
        /// **4층 "즉시 최고 속도" 근사**: <see cref="pendingInstantAccel"/>이 서 있으면 이번 한 번만
        /// `AccelMultiplier`를 <see cref="FocusT4InstantAccelMultiplier"/>(2000배)로 올린다.
        /// `CharacterMover2D.ComputeInertialVx`의 "같은 방향 유지" 가속식은 `min(s + accel*k²*Mul*dt,
        /// maxS)`라 이 정도 배율이면 baseSpeed~maxSpeed 격차(최대 6유닛, `MoveScale` 0.3까지 낮아져도
        /// 여유 있게)를 물리 스텝 **한 번**(`Time.fixedDeltaTime`) 안에 다 메운다 — 이미 상한이
        /// `maxS`라 배율이 과해도 오버슈트는 없다.
        ///
        /// **한계(받아들인 근사 오차)**: `ComputeInertialVx`는 이 배율을 "같은 방향으로 계속 달리는
        /// 중"(`h!=0 && mx==RunDir`) 가지일 때만 본다 — 무입력(`h==0`, 감속만) 또는 반대 방향 입력
        /// (방향 전환 분기, 배율 무시하고 baseSpeed로 즉시 반전)일 때는 이 배율이 아무 효과가 없다.
        /// 즉, 이동 방향키를 누른 채로 4층 집중을 켤 때만 원본처럼 즉시 최고 속도가 나가고, 무입력·
        /// 반대 입력 중에 켜면 원본과 달리 순간 가속이 안 걸린다. `CharacterMover2D`를 고치지 않고
        /// 공개 필드(`AccelMultiplier`/`MoveScale`)만으로 낼 수 있는 최선이라 받아들였다.
        /// </summary>
        void UpdateFocus(float dt)
        {
            int tier = ProfileService.Current.bladeTier;
            float accelMul = FocusAccelMul * (tier >= 2 ? FocusAccelMul2 : 1f);
            if (pendingInstantAccel)
            {
                accelMul = FocusT4InstantAccelMultiplier;
                pendingInstantAccel = false;
            }
            if (mover != null) mover.AccelMultiplier = focusT > 0f ? accelMul : 1f;
            if (focusT > 0f) focusT -= dt;

            UpdateTrail(dt);
        }

        void UpdateTrail(float dt)
        {
            if (trailT > 0f)
            {
                trailT -= dt;
                Vector2 cur = transform.position;
                if (trailPts.Count == 0 || Vector2.Distance(cur, trailPts[trailPts.Count - 1]) > TrailPointSpacing)
                {
                    trailPts.Add(cur);
                    if (trailPts.Count > TrailPointCap) trailPts.RemoveAt(0);
                }
                if (trailT <= 0f) { RipTrail(); return; } // 지속시간이 다하면 즉시 찢는다(:2552).
            }
            if (trailPts.Count == 0) return;

            trailTickTimer += dt;
            if (trailTickTimer < TrailTickInterval) return;
            trailTickTimer -= TrailTickInterval;

            // 원본: 적 하나당 궤적 점 하나에라도 닿으면 그 틱에 한 번만 피해(:2559~2567, break).
            // Physics2D 질의는 점 기준으로 돌기 때문에 이번 틱에 이미 맞은 적은 HashSet으로 거른다.
            // bladeTrailDmg()/bladeTrailR()(:2538~2539) — 원본처럼 "지금" 티어로 매 틱 다시 계산한다
            // (trailT처럼 캐스팅 순간에 고정하는 게 아니다 — 위 TryFocusStart 주석 참고).
            int currentTier = ProfileService.Current.bladeTier;
            float radius = TrailRadius * (currentTier >= 3 ? FocusT3RadiusMult : 1f);
            float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
            float dmg = atk * TrailDmgMult * (currentTier >= 3 ? FocusT3DmgMult : 1f);
            trailHitBuffer.Clear();
            foreach (var pt in trailPts)
            {
                foreach (var c in Physics2D.OverlapCircleAll(pt, radius, enemyMask))
                {
                    if (c == null || !c.CompareTag("Enemy")) continue;
                    if (!trailHitBuffer.Add(c)) continue;

                    var protectable = c.GetComponent<ISpawnProtectable>();
                    if (protectable != null && protectable.IsSpawnProtected) continue;
                    var target = c.GetComponent<IDamageable>();
                    if (target == null || target.IsDead) continue;

                    int rolled = DamageCalculator.Roll(dmg, 0f, out _); // 원본 noCrit(:2564)
                    target.TakeTickDamage(rolled); // 원본 kb:0 + noHitstop(:2564)
                }
            }
        }

        /// <summary>
        /// 궤적 파열 — 원본 `bladeRipTrail()`(project_test.html:2570~2592)의 1~4층 범위. 1~3층은
        /// 궤적만 비우고(:2591~2592), 4층부터 비우기 전에 궤적 위 적을 한 번씩 더 베는 폭발이
        /// 붙는다(:2573~2589) — 반경은 <see cref="TrailRadius"/>(3층 배수 포함) × 1.7, 피해는
        /// `F.ripDmg * bladeDmgMult()`로 <see cref="CurrentSpeedRatio"/> 기준 속도 피해 배수가 그대로
        /// 곱해진다(원본과 동일하게 crit 적용 — trail 틱 피해와 달리 `noCrit`이 없다, :2582).
        /// </summary>
        void RipTrail()
        {
            int tier = ProfileService.Current.bladeTier;
            if (tier >= 4 && trailPts.Count > 0)
            {
                var profile = ProfileService.Current;
                float baseRadius = TrailRadius * (tier >= 3 ? FocusT3RadiusMult : 1f); // bladeTrailR()
                float ripRadius = baseRadius * FocusT4RipRadiusMult; // ripR (:2576)
                float dmgMult = FocusT4RipDmgMult * SpeedDamageMultiplier(CurrentSpeedRatio()); // F.ripDmg*bladeDmgMult() (:2582)
                float baseDamage = PlayerStatCalculator.ComputeAtk(profile) * dmgMult;
                float critChance = PlayerStatCalculator.ComputeCritChance(profile);

                trailHitBuffer.Clear();
                foreach (var pt in trailPts)
                {
                    foreach (var c in Physics2D.OverlapCircleAll(pt, ripRadius, enemyMask))
                    {
                        if (c == null || !c.CompareTag("Enemy")) continue;
                        if (!trailHitBuffer.Add(c)) continue; // 원본 적당 한 번만(:2578~2586, break)

                        var protectable = c.GetComponent<ISpawnProtectable>();
                        if (protectable != null && protectable.IsSpawnProtected) continue;
                        var target = c.GetComponent<IDamageable>();
                        if (target == null || target.IsDead) continue;

                        int dmg = DamageCalculator.Roll(baseDamage, critChance, out _);
                        float dirSign = Mathf.Sign(c.transform.position.x - transform.position.x);
                        if (Mathf.Approximately(dirSign, 0f)) dirSign = mover != null ? mover.Facing : 1f;
                        target.TakeDamageWithKnockback(dmg, gameObject, dirSign, FocusT4RipKnockback);
                    }
                }
            }

            trailPts.Clear();
            trailT = 0f;
        }

        bool IsGrounded()
        {
            if (col == null || mover == null) return false;
            // CharacterMover2D.CheckGrounded()와 동일한 판정을 그 파일을 안 고치고 재현한다 —
            // groundCheckRadius/groundMask는 이미 공개 필드다.
            Vector2 feet = (Vector2)transform.position + Vector2.down * col.bounds.extents.y;
            return Physics2D.OverlapCircle(feet, mover.groundCheckRadius, mover.groundMask);
        }

        /// <summary>
        /// 칼날폭풍 진입 — 원본 `bladeStormStart()`(project_test.html:2612~2629)의 1층 범위.
        /// TODO(tier1 한계): 원본은 지상에서 누르면 실패 대신 'retry'(선입력 유지 — 곧 점프하면
        /// 그 즉시 발동)로 돌려주지만, 여기서는 단순화해 지상이면 그냥 무시한다. 나중에 입력 버퍼를
        /// 붙일 수 있으면 보완한다.
        /// </summary>
        void TryStormStart(int tier)
        {
            if (tier <= 0) return; // 방어적 체크 — 위와 동일한 이유.
            if (storming) { EndStorm(); return; } // 재사용 취소(:2615) — 지상/공중 무관.
            if (IsGrounded()) return; // 원본은 'retry', 여기선 단순 무시(위 설명 참고).
            if (stormCd > 0f) return;

            stormDir = mover != null ? mover.RunDir : 1;
            float baseSpeed = mover != null ? mover.inertialBaseSpeed * mover.MoveScale : 0f;
            float curSpeedAbs = rb != null ? Mathf.Abs(rb.linearVelocity.x) : 0f;
            stormSpd = Mathf.Max(curSpeedAbs, baseSpeed);
            stormT = 0f;
            stormHitT = 0f;
            stormCd = StormCd;
            stormGrab.Clear(); // 원본 `p.stormGrab = []`(:2626).
            storming = true;
        }

        /// <summary>
        /// 원본 `bladeStormEnd()`(project_test.html:2630~2637) — 붙잡고 있던 적을 낙하 상태로
        /// 풀어준다(죽은 적은 그대로 버린다, 원본 `!e.dead` 조건).
        /// </summary>
        /// <summary>
        /// 5층 곡예비행 실행부 — 원본 :2662~2673. `jumpPressed`를 매개변수로 받아 `UpdateStorm`에서
        /// `GameInput.JumpDown`을 넘기는 자리와 분리했다 — 실제 키 입력은 `GameInput`(=Unity `Input`)을
        /// 통해서만 들어와서 PlayMode 테스트가 키를 흉내 낼 방법이 없다(이 프로젝트 다른 스킬 진입도
        /// 전부 `Update()`를 안 거치고 `TryFocusStart`/`TryStormStart`를 리플렉션으로 직접 불러 검증하는
        /// 것과 같은 이유) — 이렇게 분리해야 테스트가 `jumpPressed=true`를 직접 넘겨 도약 자체(쿨타임
        /// 반환·상승 속도 계산·해제)를 검증할 수 있다.
        /// </summary>
        bool TryStormHop(bool jumpPressed)
        {
            if (!jumpPressed || ProfileService.Current.bladeTier < 5) return false;

            float hopVy = StormHopVy + stormSpd * StormHopSpeedRatio;
            EndStorm(); // 붙잡은 적 낙하 전환 등 공통 정리부터(:2664 bladeStormEnd()).
            stormCd = 0f; // 쿨타임 즉시 반환(:2669 p.stormCd = 0).
            if (rb != null) rb.linearVelocity = new Vector2(rb.linearVelocity.x, hopVy);
            return true;
        }

        void EndStorm()
        {
            foreach (var g in stormGrab)
            {
                if (g.col == null) continue;
                var target = g.col.GetComponent<IDamageable>();
                if (target != null && target.IsDead) continue;
                stormFalling.Add(new FallRecord { col = g.col, fallVy = 0f });
            }
            stormGrab.Clear();
            storming = false;
        }

        /// <summary>
        /// 칼날폭풍 유지 — 원본 `bladeStormUpdate(dt)`(project_test.html:2658~2723)의 1~5층 범위.
        ///
        /// **5층 곡예비행**(:2662~2673): 점프키를 누르면 종료 대신 위로 튀어 오르며 칼날폭풍
        /// 쿨타임을 즉시 돌려준다 — 도약 속도는 `hopVy + stormSpd*hopSpdK`라 활공 속도가 빠를수록
        /// 더 높이 솟는다. 수평 속도는 손대지 않는다(원본도 `p.vx`는 안 건드림) — 종료 이후엔 다른
        /// 칼날폭풍 종료 경로와 똑같이 `CharacterMover2D`의 독립 관성이 이어받는다(1층 로그의
        /// "알려진 단순화" 참고).
        ///
        /// **좌표계 주의**: 원본은 Y+가 아래라 "하강"이 `p.y += vy*dt`(양수 증가)다. 이 포팅은
        /// Y+가 위라(`FieldBounds.GroundY`가 아래쪽 0) 부호를 뒤집어 `y -= vy*dt`로 내려간다 —
        /// 이번 세션에서 발견한 중력장 버그(`MageSkillEffects.DetonateGravityOrb`)와 같은 종류의
        /// 함정이라 특히 주의해서 뒤집었다.
        ///
        /// **순서 주의(3층에서 고쳤음)**: 원본은 `p.y`를 벽 충돌 체크보다 먼저, 무조건 갱신한다
        /// (:2680 `p.y = p.y + vy*dt` 다음에야 :2681 벽 체크). 2층까지는 `pos.y` 갱신을 벽에 안
        /// 부딪힌 경우로만 묶어 놔도 티어<3이 벽에서 항상 그냥 끝나 버려 차이가 안 보였지만, 3층
        /// 반사부터는 "벽에 부딪힌 바로 그 프레임에도 하강·회전베기 유지·착지 판정이 계속 이어져야"
        /// 원본과 같아서, `pos.y` 갱신을 벽 체크보다 앞으로 옮기고 반사 후에도 아래 로직이 계속
        /// 흐르게 고쳤다.
        /// </summary>
        void UpdateStorm(float dt)
        {
            stormT += dt;

            // 5층 곡예비행(:2662~2673) — 다른 모든 계산보다 먼저 검사한다(원본도 `st.t += dt` 바로
            // 다음, 벽/착지 판정보다 앞).
            if (TryStormHop(GameInput.JumpDown)) return;

            float maxSpeed = mover != null ? mover.inertialMaxSpeed * mover.MoveScale : 0f; // bladeMaxSpeed(:2429)
            float accelK = mover != null ? mover.MoveScale * mover.MoveScale : 0f;           // bladeAccelK(:2434)
            stormSpd = Mathf.Min(stormSpd + StormAccel * accelK * dt, maxSpeed * StormMaxSpeedMult);

            float angleRad = StormAngleDeg * Mathf.Deg2Rad;
            float vx = stormDir * stormSpd * Mathf.Cos(angleRad);
            float vyDown = stormSpd * Mathf.Sin(angleRad); // 하강 속력(원본 좌표계 기준 크기, 부호는 아래서 뒤집음)

            // bladeCrashImmune(:2444) 게이팅용 — Update()가 다음 프레임에 읽는다(클래스 주석 "3층
            // 범위" 참고). 회전베기 유지 틱과 같은 값을 쓰므로 여기서 한 번만 계산해 둔다.
            stormSpeedRatio = maxSpeed > 0f ? Mathf.Clamp01(Mathf.Abs(vx) / maxSpeed) : 0f; // bladeSpeedRatio(:2435)

            Vector3 pos = transform.position;
            float edgeMargin = mover != null ? mover.edgeMargin : 0f;
            float minX = FieldBounds.MinX + edgeMargin;
            float maxX = FieldBounds.MaxX - edgeMargin;
            float nx = pos.x + vx * dt;
            pos.y -= vyDown * dt; // 위 요약 참고 — Y축 부호 반전 + 벽 체크보다 먼저(순서 주의 참고).

            if (nx <= minX || nx >= maxX)
            {
                pos.x = Mathf.Clamp(nx, minX, maxX);
                if (ProfileService.Current.bladeTier >= 3)
                {
                    // 벽 반사(:2683~2686) — 방향만 뒤집고 이번 프레임 나머지(회전베기 유지·착지
                    // 판정)는 그대로 이어간다. `p.runDir = st.dir`는 원본이 같이 하지만
                    // `CharacterMover2D.RunDir`은 그 파일 소유라 밖에서 못 바꾼다(주석대로 방향 전환
                    // 애니메이션 등에만 쓰이고 칼날폭풍 이동 자체는 stormDir로만 도는 값이라 게임
                    // 플레이에는 영향이 없다) — 안 건드리고 넘어간다.
                    stormDir = -stormDir;
                }
                else
                {
                    WritePosition(pos);
                    EndStorm(); // 1~2층은 그냥 종료(:2687).
                    return;
                }
            }
            else
            {
                pos.x = nx;
            }

            // 4층 강제 연행 — 원본 :2691~2707. 원본은 이 구간에서 이미 갱신된 `p.x`/`p.y`(바로 위
            // 벽 체크까지 반영된 값)를 기준으로 붙잡는다 — 여기서도 `transform.position`(아직 안 써진
            // 지난 프레임 값)이 아니라 이번 프레임 계산 중인 `pos`를 써야 한다(3층 "순서 주의"와 같은
            // 함정). `Physics2D.OverlapCircleAll`은 실제 콜라이더 형태로 겹침을 판정하므로, 원본이
            // 직접 `hypot(...) < 74 + e.w/2`로 계산하던 "쿼리 반지름 + 대상 반지름" 판정을 별도 계산
            // 없이 그대로 대응한다(SpinHit/StormLand와 같은 방식).
            if (ProfileService.Current.bladeTier >= 4)
            {
                if (stormGrab.Count < StormGrabMax)
                {
                    foreach (var c in Physics2D.OverlapCircleAll(pos, StormGrabRadius, enemyMask))
                    {
                        if (stormGrab.Count >= StormGrabMax) break;
                        if (c == null || !c.CompareTag("Enemy")) continue;
                        if (IsGrabbed(c)) continue;
                        if (c.GetComponent<IGrabExempt>() != null) continue; // 보스·성소 제외(:2694)

                        var protectable = c.GetComponent<ISpawnProtectable>();
                        if (protectable != null && protectable.IsSpawnProtected) continue;
                        var target = c.GetComponent<IDamageable>();
                        if (target == null || target.IsDead) continue;

                        stormGrab.Add(new GrabRecord
                        {
                            col = c,
                            offsetX = Random.Range(StormGrabOffsetXMin, StormGrabOffsetXMax),
                            offsetY = Random.Range(StormGrabOffsetYMin, StormGrabOffsetYMax),
                        });
                    }
                }

                for (int i = 0; i < stormGrab.Count; i++)
                {
                    var g = stormGrab[i];
                    if (g.col == null) continue;
                    var target = g.col.GetComponent<IDamageable>();
                    if (target != null && target.IsDead) continue; // 원본 `if (!e || e.dead) continue`(:2705)

                    // 원본 clamp(p.y+oy, 60, groundY)의 위쪽(60) 한도는 이 포트에 대응하는 천장
                    // 좌표가 없어(FieldBounds에 MaxY 없음) 뺐다 — 바닥(GroundY) 하한만 유지한다.
                    float gx = FieldBounds.ClampX(pos.x + g.offsetX);
                    float gy = Mathf.Max(FieldBounds.GroundY, pos.y + g.offsetY);
                    WriteEnemyPosition(g.col, new Vector3(gx, gy, g.col.transform.position.z));
                    // 원본 `e.kbx = 0`(:2707)은 EnemyMove 넉백 상태(private, Enemies 소유)를 직접 못
                    // 지운다 — 매 프레임 위치·속도를 강제로 덮어쓰므로(WriteEnemyPosition) 잔여
                    // 넉백이 있어도 화면에 드러나지 않아 받아들인 단순화다.
                }
            }

            // 회전베기 유지 — 원본 :2708~2713.
            stormHitT += dt;
            if (stormHitT >= StormTickInterval)
            {
                stormHitT -= StormTickInterval;
                float dmgMult = StormDmgMult * SpeedDamageMultiplier(stormSpeedRatio);
                SpinHit(dmgMult, spinRadius * StormSpinRadiusMult, true, false);
            }

            if (pos.y <= FieldBounds.GroundY)
            {
                pos.y = FieldBounds.GroundY;
                WritePosition(pos);
                if (ProfileService.Current.bladeTier >= 2)
                {
                    StormLand(stormSpeedRatio);
                }
                EndStorm();
                return;
            }

            WritePosition(pos);
            if (stormT >= StormMaxDur) EndStorm();
        }

        /// <summary>
        /// 낙하 충격 — 원본 `bladeStormLand()`(project_test.html:2640~2657)의 2층 전용 광역 피해.
        /// 착지 지점(발밑에서 위로 <see cref="StormLandYOffset"/>만큼)을 중심으로 한 번 터진다.
        /// 시각효과(`zones.push gravityBurst`/`burst`/`shake`/`sfx`/`showSkillMsg`)는 프로젝트 관례상
        /// 범위 밖이라 안 옮겼다 — 피해·넉백만.
        /// </summary>
        void StormLand(float speedRatio)
        {
            var profile = ProfileService.Current;
            float dmgMult = StormLandDmgMult * SpeedDamageMultiplier(speedRatio);
            float baseDamage = PlayerStatCalculator.ComputeAtk(profile) * dmgMult;
            float critChance = PlayerStatCalculator.ComputeCritChance(profile);
            Vector3 center = transform.position + new Vector3(0f, StormLandYOffset, 0f);

            foreach (var c in Physics2D.OverlapCircleAll(center, StormLandRadius, enemyMask))
            {
                if (c == null || !c.CompareTag("Enemy")) continue;
                var protectable = c.GetComponent<ISpawnProtectable>();
                if (protectable != null && protectable.IsSpawnProtected) continue;
                var target = c.GetComponent<IDamageable>();
                if (target == null || target.IsDead) continue;

                int dmg = DamageCalculator.Roll(baseDamage, critChance, out _);
                float dirSign = Mathf.Sign(c.transform.position.x - transform.position.x);
                if (Mathf.Approximately(dirSign, 0f)) dirSign = mover != null ? mover.Facing : 1f;
                target.TakeDamageWithKnockback(dmg, gameObject, dirSign, StormLandKnockback);
            }
        }

        /// <summary>
        /// bladeCrashImmune (project_test.html:2444~2446) — 게이팅 조건(최고 속도 + 티어)은
        /// <see cref="Update"/>가 계산해서 넘긴다. `ApplyDirectionalInvuln`과 같은 근사 기법
        /// (감소 타이머라 "상시"를 표현할 다른 방법이 없어 매 프레임 짧게 갱신)을 그대로 재사용한다.
        /// </summary>
        void ApplyCrashImmunity(bool active)
        {
            if (!active || playerHealth == null) return;
            playerHealth.GrantInvuln(Time.deltaTime * DirInvulnRefresh);
        }

        /// <summary>
        /// 5층 "쿨타임 환급" — 원본 `bladePassTick()`(project_test.html:2594~2608). 최고 속도로
        /// 적과 겹쳐 지나갈 때마다(적당 <see cref="PassTickInterval"/>초에 한 번) 집중 쿨타임을
        /// <see cref="PassCooldownReduction"/>만큼 깎는다. Focus 갈래 5층 전용 — 원본도
        /// `bladeTier('move')`가 Storm 갈래에선 0이라 이 함수가 아무 일도 안 한다.
        ///
        /// 원본의 겹침 판정(`hypot` 없이 x·y 절반폭 비교, :2601)은 실제 콜라이더 형태가 아니라
        /// 사각형 AABB지만, 이 포트는 원형 콜라이더 기반이라 다른 근접 판정(SpinHit/StormLand 등)과
        /// 같은 방식 — `Physics2D.OverlapCircleAll`에 자기 콜라이더 반지름을 넘겨 실제 겹침을 묻는다.
        /// </summary>
        void UpdatePassTick(bool atTop)
        {
            var profile = ProfileService.Current;
            if (profile.bladeBranch != BladeBranch.Focus || profile.bladeTier < 5 || !atTop) return;
            if (col == null) return;

            float now = Time.time;
            foreach (var c in Physics2D.OverlapCircleAll(transform.position, col.bounds.extents.x, enemyMask))
            {
                if (c == null || !c.CompareTag("Enemy")) continue;

                var protectable = c.GetComponent<ISpawnProtectable>();
                if (protectable != null && protectable.IsSpawnProtected) continue;
                var target = c.GetComponent<IDamageable>();
                if (target == null || target.IsDead) continue;

                if (passHitTimestamps.TryGetValue(c, out float last) && now - last <= PassTickInterval) continue;
                passHitTimestamps[c] = now;
                focusCd = Mathf.Max(0f, focusCd - PassCooldownReduction);
            }
        }

        /// <summary>
        /// 칼날폭풍 활공 중 매 프레임 위치를 직접 덮어쓴다 — 원본 "충돌 판정 없음"(플랫폼 통과)을
        /// 재현하는 방법. `Rigidbody2D.position`까지 같이 맞추고 속도는 0으로 비워서, 다음
        /// `CharacterMover2D.FixedUpdate`가 남은 관성 속도로 우리 위치를 밀어내지 않게 한다
        /// (원본 `MageAttack`의 `CharacterMover2D.Teleport`와 같은 기법).
        /// </summary>
        void WritePosition(Vector3 pos)
        {
            transform.position = pos;
            if (rb != null)
            {
                rb.position = pos;
                rb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// <see cref="WritePosition"/>과 같은 기법을 붙잡힌/추락 중인 "적" 쪽에 적용한다 — 그 적의
        /// `EnemyMove.FixedUpdate`(팀장 소유, 못 고침)가 이번 프레임 계산해 둔 위치·속도를, 우리
        /// `LateUpdate`가 마지막에 덮어써서 이긴다. 대상은 `Enemies` 구체 타입이 아니라 그 오브젝트의
        /// `Collider2D`/`Rigidbody2D`만 다루므로 asmdef 경계를 넘지 않는다.
        /// </summary>
        void WriteEnemyPosition(Collider2D target, Vector3 pos)
        {
            if (target == null) return;
            target.transform.position = pos;
            var erb = target.attachedRigidbody;
            if (erb != null)
            {
                erb.position = pos;
                erb.linearVelocity = Vector2.zero;
            }
        }

        /// <summary>
        /// 붙잡혔다 놓인 뒤 추락 — 원본 `bladeFallingUpdate(dt)`(project_test.html:2724~2737).
        /// 원본 주석 그대로 "플랫폼 무시하고 추락" — `EnemyMove`의 실제 발판 충돌(원웨이 플랫폼 포함)에
        /// 맡기면 도중 발판에 걸려 멈추므로, 칼날폭풍 활공(<see cref="UpdateStorm"/>)과 같은 방식으로
        /// 매 프레임 위치를 직접 계산해 덮어쓴다(<see cref="WriteEnemyPosition"/>). 착지(`FieldBounds.
        /// GroundY` 도달)하면 한 번 피해를 주고 목록에서 뺀다. <see cref="storming"/>과 무관하게
        /// <see cref="LateUpdate"/>에서 항상 불린다.
        /// </summary>
        void UpdateFalling(float dt)
        {
            for (int i = stormFalling.Count - 1; i >= 0; i--)
            {
                var f = stormFalling[i];
                if (f.col == null) { stormFalling.RemoveAt(i); continue; }

                var target = f.col.GetComponent<IDamageable>();
                if (target == null || target.IsDead) { stormFalling.RemoveAt(i); continue; }

                // W.gravity(:602) 크기 그대로 하강 속력을 키운다 — 좌표계는 Y+가 위라(클래스 주석
                // "좌표계 주의") 아래로는 뺀다.
                f.fallVy += StormFallGravity * dt;
                Vector3 pos = f.col.transform.position;
                pos.y -= f.fallVy * dt;

                if (pos.y <= FieldBounds.GroundY)
                {
                    pos.y = FieldBounds.GroundY;
                    WriteEnemyPosition(f.col, pos);

                    var profile = ProfileService.Current;
                    float dmgMult = StormFallDmgMult * SpeedDamageMultiplier(CurrentSpeedRatio()); // S.fallDmg*bladeDmgMult() (:2732)
                    float baseDamage = PlayerStatCalculator.ComputeAtk(profile) * dmgMult;
                    float critChance = PlayerStatCalculator.ComputeCritChance(profile);
                    int dmg = DamageCalculator.Roll(baseDamage, critChance, out _);
                    float dirSign = Mathf.Sign(f.col.transform.position.x - transform.position.x);
                    if (Mathf.Approximately(dirSign, 0f)) dirSign = mover != null ? mover.Facing : 1f;
                    target.TakeDamageWithKnockback(dmg, gameObject, dirSign, StormFallKnockback);

                    stormFalling.RemoveAt(i);
                    continue;
                }

                WriteEnemyPosition(f.col, pos);
                stormFalling[i] = f; // struct라 fallVy 갱신분을 리스트에 다시 써 넣어야 한다.
            }
        }

        /// <summary>
        /// bladeDirInvuln (project_test.html:2448~2451) — 원본은 "이 프레임에 방향키가 눌려
        /// 있는가"를 매 프레임 새로 평가하는 상태 없는(stateless) 조건이다. Unity 쪽 무적은
        /// 감소하는 타이머(<see cref="PlayerHealth.InvulnRemaining"/>)라 외부에서 "이번 프레임만"
        /// 무적을 끼워 넣을 지점이 없다 — 그래서 방향키가 눌려 있는 동안 매 프레임 "최소 이번
        /// 프레임 두 배만큼"을 계속 갱신하는 방식으로 근사했다. `GrantInvuln`은 값을 줄이지 않고
        /// 올리기만 하므로 이미 더 긴 무적(피격 직후 0.9초 등)을 갖고 있으면 그대로 둔다.
        ///
        /// 한계: 키를 뗀 직후 최대 한 프레임 분량의 잔여 무적이 남을 수 있다 — 원본의 프레임 단위
        /// 판정과 사실상 구분되지 않는 오차라 범위 안에서 받아들였다.
        /// </summary>
        void ApplyDirectionalInvuln(bool anyDirectionHeld)
        {
            if (!anyDirectionHeld || playerHealth == null) return;
            playerHealth.GrantInvuln(Time.deltaTime * DirInvulnRefresh);
        }
    }
}
