using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.World;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 전투 씬(`20_Combat`)의 진행 담당 — 필드 준비·정리, 런 타이머, 보스 필드 전환, 처치·피격 연출, 런을 끝낼 사건 감지.
    /// 원본 `startRun`의 필드 부분(:4313~:4319)·`resetPlayerForRun`(:1511)·`update`의 런 타이머(:4412)·`killEnemy` 연출(:1847)·
    /// 보스 격파 지연(:4289)에 해당한다. 2026-11-05 씬 분할 2차 1단계에서 `GameFlow`(옛 `RunController`)에서 떼어 냈다.
    ///
    /// ## `GameFlow`와 나눈 기준 (`HANDOFF.md` "씬 분할 2차")
    /// - **`GameFlow`(상주 씬)**: 화면 흐름(타이틀·로비·사냥·일시정지·결과), `GameState.Set`, `Time.timeScale`(히트스톱 포함). 씬이 바뀌어도 남는 것.
    /// - **`CombatDirector`(전투 씬)**: 전투가 벌어지는 동안의 일. 전투 씬과 함께 생기고 사라진다.
    /// - 연결은 한 방향만 직접 안다 — `GameFlow`가 <see cref="Current"/>로 이 컴포넌트를 부르고(<see cref="BeginField"/>),
    ///   이쪽은 `GameFlow`를 모른 채 이벤트로만 알린다(런 종료 <see cref="RunEvents.RequestRunEnd"/>, 히트스톱 <see cref="CombatEvents.RequestHitstop"/>).
    ///   둘은 씬이 달라 직렬화로 이을 수 없다.
    ///
    /// 전투 화면이 읽는 창구(<see cref="ICombatView"/>, `WorldView.Combat`)도 이 담당이다 — 같은 씬 참조(Player·포탈)와 적 등록 목록으로 **물을 때** 계산한다
    /// (2026-11-05 2b). 화면은 이 타입을 모르고 계약만 본다.
    ///
    /// 시간이 멈춘 동안(히트스톱·일시정지, `timeScale` 0)은 `Time.deltaTime`이 0이라 런 타이머·콤보 창이 저절로 멈춘다 — 원본도 `dt = 0`이다.
    /// 0을 빼는 호출은 결과를 바꾸지 않는다: 런 타이머는 0이 되는 순간 이미 종료를 요청하고(`RunState.Over` 뒤엔 무시),
    /// 콤보·연쇄 창은 0 이하가 되는 순간 이미 초기화된다(`CombatModifiers.Tick`). `CombatDirectorTests.WhileFrozen_TimersAndComboDoNotMove`.
    /// </summary>
    [DisallowMultipleComponent]
    public class CombatDirector : MonoBehaviour, ICombatView
    {
        /// <summary>지금 떠 있는 전투 씬의 담당. 전투 씬이 없으면(타이틀 부팅 직후 등) null.</summary>
        public static CombatDirector Current { get; private set; }

        [Tooltip("같은 씬(20_Combat)의 플레이어 — 사냥 시작·보스 필드 진입 때 위치·체력·스킬 상태를 되돌린다(IRunResettable).")]
        [SerializeField] GameObject player;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다.</summary>
        public GameObject Player { get => player; set => player = value; }

        [Tooltip("같은 씬(20_Combat)의 일반 무대 발판 세트.")]
        [SerializeField] PlatformSet normalPlatforms;
        [Tooltip("같은 씬(20_Combat)의 보스 무대 발판 세트.")]
        [SerializeField] PlatformSet bossPlatforms;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public PlatformSet NormalPlatforms { get => normalPlatforms; set => normalPlatforms = value; }
        public PlatformSet BossPlatforms { get => bossPlatforms; set => bossPlatforms = value; }

        [Tooltip("같은 씬(20_Combat)의 보스 포탈 — 전투 화면이 범위 안내·미니맵 점에 쓴다.")]
        [SerializeField] BossPortal portal;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public BossPortal BossPortal { get => portal; set => portal = value; }

        /// <summary>원본 `setTimeout(() => endRun('bossdead'), 1600)`(project_test.html:4289).</summary>
        public const float BossDeadEndDelay = 1.6f;
        float bossDeadTimer = -1f;

        float nextHitShakeTime, nextTickShakeTime;

        void OnEnable()
        {
            // 전투 담당은 하나뿐이어야 한다 — 둘이면 처치·사망 이벤트를 두 번 처리하고, 하나가 꺼질 때 등록이 엉킨다.
            // 전투 씬이 두 번 켜진 채 겹치는 건 전환 버그라 조용히 넘기지 않는다(재도전은 이전 전투 씬을 멈춰(꺼) 둔 뒤 새로 부른다 — `GameFlow` 전환).
            if (Current != null && Current != this)
            {
                Debug.LogError($"[CombatDirector] 전투 담당이 둘이다 — {name}은 꺼 둔다(전투 씬이 두 번 떴는지 확인)");
                enabled = false;
                return;
            }
            Current = this;
            WorldView.Attach(this);
            CombatEvents.PlayerDied += HandlePlayerDied;
            CombatEvents.EnemyKilled += HandleEnemyKilled;
            CombatEvents.EnemyDamaged += HandleEnemyHit;
            CombatEvents.EnemyTickDamaged += HandleEnemyTick;
            CombatEvents.ShrineBuffGranted += CombatModifiers.GrantShrineBuff;
            RunEvents.BossPortalRequested += HandleBossPortalRequested;
            RunEvents.BossDefeated += HandleBossDefeated;
            // 켜지면 카메라가 이 씬의 플레이어를 본다 — 꺼질 때(OnDisable) 놓으므로, 전환이 실패해 멈췄던 전투 씬이 다시 켜질 때도 되돌아온다.
            if (player != null) CameraFollow2D.Track(player.transform);
        }

        void OnDisable()
        {
            if (Current == this) Current = null;
            WorldView.Detach(this);
            if (player != null) CameraFollow2D.Untrack(player.transform);
            CombatEvents.PlayerDied -= HandlePlayerDied;
            CombatEvents.EnemyKilled -= HandleEnemyKilled;
            CombatEvents.EnemyDamaged -= HandleEnemyHit;
            CombatEvents.EnemyTickDamaged -= HandleEnemyTick;
            CombatEvents.ShrineBuffGranted -= CombatModifiers.GrantShrineBuff;
            RunEvents.BossPortalRequested -= HandleBossPortalRequested;
            RunEvents.BossDefeated -= HandleBossDefeated;
        }

        void Update()
        {
            if (GameState.Current != GameScene.Run) return;

            // 보스 격파 뒤 결과 화면까지의 지연. **멈춘 시간(unscaled)으로 잰다** — 히트스톱이 걸려 있으면 `Time.deltaTime`이 0이라
            // 타이머가 안 줄어 결과 화면이 영영 안 뜬다.
            if (bossDeadTimer >= 0f)
            {
                bossDeadTimer -= Time.unscaledDeltaTime;
                if (bossDeadTimer <= 0f)
                {
                    bossDeadTimer = -1f;
                    RunEvents.RequestRunEnd(RunEndReason.BossDead);
                }
                return; // 격파 뒤엔 런 타이머가 더 흐르지 않는다(timeout으로 뒤집히면 안 된다)
            }

            HitFeel.TickRedFlash(Time.deltaTime); // 원본 `state.redFlash -= dt * 2.2`(:4426) — 게임 시간

            // 원본 `run.timeLeft -= dt; if (<= 0) endRun('timeout')`(:4412).
            if (RunState.Tick(Time.deltaTime)) RunEvents.RequestRunEnd(RunEndReason.Timeout);

            // 콤보 창·연쇄 처치 창·성소 버프 감소(원본 `updateCombo` :1630 등).
            CombatModifiers.Tick(Time.deltaTime);
        }

        // ───────────────────────── 전투 화면이 읽는 것 (ICombatView) ─────────────────────────

        // 플레이어 쪽은 `Core` 계약으로만 읽는다 — `Systems`는 `Characters`(PlayerHealth·PlayerRig)를 모른다(IRunResettable과 같은 이유).
        IHealthView playerHealth;
        ISkillSlotHost playerSkills;
        GameObject cachedFor;

        void CachePlayer()
        {
            if (cachedFor == player) return;
            cachedFor = player;
            playerHealth = player != null ? player.GetComponent<IHealthView>() : null;
            playerSkills = player != null ? player.GetComponent<ISkillSlotHost>() : null;
        }

        float ICombatView.PlayerHp { get { CachePlayer(); return playerHealth != null ? playerHealth.CurrentHp : 0f; } }
        float ICombatView.PlayerMaxHp { get { CachePlayer(); return playerHealth != null ? playerHealth.MaxHp : 0f; } }
        Vector2 ICombatView.PlayerPosition => player != null ? (Vector2)player.transform.position : Vector2.zero;
        ISkillSlotSource ICombatView.PlayerKit { get { CachePlayer(); return playerSkills?.CurrentSkillSlots; } }

        /// <summary>포탈은 일반 필드에서만 보인다(보스 필드엔 없다) — 지금 HUD와 같은 조건.</summary>
        PortalView ICombatView.Portal => portal != null && RunState.Mode == RunMode.Normal
            ? new PortalView(true, portal.PlayerInRange, portal.IsUnlocked, portal.transform.position)
            : default;

        float? ICombatView.BossHpRatio
        {
            get
            {
                foreach (var e in EnemyHealth.Active)
                    if (e != null && e.isBoss && !e.IsDead && e.MaxHp > 0f) return e.CurrentHp / e.MaxHp;
                return null;
            }
        }

        /// <summary>살아 있는 적 전부 — 예전 화면의 `FindObjectsByType&lt;EnemyHealth&gt;` + `isActiveAndEnabled && CurrentHp > 0`과 같은 범위.</summary>
        void ICombatView.CopyEnemyPositions(List<Vector2> into)
        {
            into.Clear();
            foreach (var e in EnemyHealth.Active)
                if (e != null && e.isActiveAndEnabled && e.CurrentHp > 0f) into.Add(e.transform.position);
        }

        // ───────────────────────── GameFlow가 부르는 것 ─────────────────────────

        /// <summary>
        /// 사냥 시작 — 원본 `startRun`의 필드 부분. `RunState.Begin`은 `GameFlow`가 먼저 끝낸 상태다.
        /// 무대를 먼저 바꾼다 — 발판 배치와 맵 폭(`FieldBounds.MaxX`)이 여기서 정해지고, 뒤의 필드 정리·플레이어 리셋이 그 값으로 좌표를 잡는다
        /// (원본도 `startRun`에서 `run.mapW`와 `platforms`를 먼저 갈아끼운다, :4313·:4316).
        /// </summary>
        public void BeginField(RunMode mode)
        {
            ShowArena(mode == RunMode.Boss);
            RunTransient.DestroyAll();      // 원본 `enemies = []; projectiles = []; zones = []; ...`(:4318) — 지난 런의 잔해
            CombatModifiers.ResetForRun();  // 원본 `combo.n = 0; run.chainN = 0`(:4307·:4319)
            HitFeel.Reset();
            bossDeadTimer = -1f;
            ResetPlayer();
            // 사냥 화면의 카메라 대상 = 플레이어(로비에선 로비 기준점이었다). 부드럽게 따라간다(예전처럼).
            if (player != null) CameraFollow2D.Track(player.transform);
        }

        /// <summary>
        /// 일반 필드 → 보스 필드. 무대·타이머·필드 정리·플레이어 위치를 한 번에 바꾼다. <see cref="BeginField"/>와 같은 순서다
        /// (무대를 먼저 바꿔야 `FieldBounds.MaxX`가 갱신되고 그 뒤 좌표 계산이 맞는다). **원본에 없는 전환**(사용자 지시 2026-09-16).
        /// 실행 코드에선 포털 요청(<see cref="HandleBossPortalRequested"/>, 해금 확인 뒤)만 부른다 — 공개인 건 테스트가 해금 없이 보스 필드를 세우려고.
        /// </summary>
        /// <returns>실제로 전환됐으면 true.</returns>
        public bool EnterBossField()
        {
            if (!RunState.EnterBossField()) return false;

            ShowArena(true);
            // 일반 필드의 잡몹·투사체·장판을 지운다. 안 지우면 좁아진 보스 무대에 이전 필드의 몹이 맵 밖 좌표로 남아,
            // 플레이어가 닿을 수 없는 곳에서 계속 살아 있다.
            RunTransient.DestroyAll();
            ResetPlayer(); // 좁아진 무대의 시작 지점으로
            RunEvents.RaiseBossFieldEntered(); // 스포너가 보스를 세운다
            return true;
        }

        /// <summary>
        /// 무대를 바꾼다 — 좌표 데이터(<see cref="FieldLayout.SetBossArena"/>)와 **이 씬의** 발판 세트를 같이(따로 두면 스포너는 좁은 맵을 보는데 발판은
        /// 넓은 배치로 남는 식의 조용한 불일치가 생긴다). 다른 씬의 발판은 건드리지 않는다.
        /// </summary>
        void ShowArena(bool boss)
        {
            FieldLayout.SetBossArena(boss);
            if (normalPlatforms != null) normalPlatforms.gameObject.SetActive(!boss);
            if (bossPlatforms != null) bossPlatforms.gameObject.SetActive(boss);
        }

        // ───────────────────────── 전투 사건 ─────────────────────────

        /// <summary>
        /// 보스 포탈 입장 요청 — 판단은 여기서 한다(포탈은 요청만 던진다, <see cref="RunEvents"/> 주석):
        /// 사냥 중인지 · 그 지역 보스가 해금됐는지. 이미 보스 필드인지는 `RunState.EnterBossField`가 본다.
        /// </summary>
        void HandleBossPortalRequested()
        {
            if (GameState.Current != GameScene.Run) return;
            if (!ProfileService.Current.IsBossUnlocked(RunState.Region)) return;
            EnterBossField();
        }

        /// <summary>
        /// 보스 격파. 원본은 **1.6초 뒤에** 런을 끝낸다(`:4289`) — 즉사 연출과 보상 표시를 볼 시간을 준다.
        /// **의도적 편차(사용자 2026-11-05 "지금대로")**: 원본은 실제 시간으로 재서(`setTimeout`) 그 순간 일시정지 중이면 런을 아예 안 끝내고,
        /// 대기 중에도 런 시간이 흘러 시간 초과가 먼저 날 수 있다. 우리는 사냥 중일 때만 재고(일시정지 동안 멈췄다 이어 감) 대기 중엔 런 시간을 안 흘린다.
        /// **보스 격파 → 런 종료 예약은 이 한 곳뿐이다**(<see cref="HandleEnemyKilled"/> 주석 참고).
        /// </summary>
        void HandleBossDefeated() => bossDeadTimer = BossDeadEndDelay;

        void HandlePlayerDied() => RunEvents.RequestRunEnd(RunEndReason.Dead); // 원본 :1927

        void HandleEnemyKilled(GameObject enemy)
        {
            var (stop, shakeMag, shakeDur) = KillFeel(
                enemy != null && enemy.GetComponent<ChronoSavior.Enemies.Boss>() != null,
                enemy != null && enemy.GetComponent<ChronoSavior.Enemies.Shrine>() != null,
                enemy != null && enemy.GetComponent<ChronoSavior.Enemies.EnemyElite>() != null);
            CombatEvents.RequestHitstop(stop);
            if (shakeMag > 0f) HitFeel.Shake(shakeMag, shakeDur);

            // ⚠️ 보스 격파 뒤 결과 화면 예약은 **여기서 하지 않는다** — `RunEvents.BossDefeated` → <see cref="HandleBossDefeated"/>
            // (멈춘 시간 기준)가 한다. 예전엔 여기서도 `Invoke(EndRunAfterBoss, 1.6)`를 걸어 **두 번** 예약했는데, `Invoke`는 게임 시간
            // 기준이라 결과·로비의 `timeScale = 0` 동안 멈춘 채 남아 있다가 **다음 사냥 시작 직후** 터져서 방금 들어온 런을 "보스 격파"로
            // 끝냈다(2026-09-28 사용자 발견). 취소도 안 되는 경로라 없앴다.
        }

        /// <summary>
        /// 처치 연출 — (멈춤 초, 흔들림 세기 px, 흔들림 초). 원본 `killEnemy`(:1847~:1848), 성소는 따로 :1804~:1805
        /// (성소는 일반 처치 멈춤을 안 거치고 0.12만).
        /// </summary>
        public static (float hitstop, float shakeMag, float shakeDuration) KillFeel(bool boss, bool shrine, bool elite)
        {
            if (shrine) return (HitFeel.HitstopShrine, 9f, 0.35f);
            if (boss) return (HitFeel.HitstopBossKill, 14f, 0.5f);
            if (elite) return (HitFeel.HitstopKill, 6f, 0.22f);
            return (HitFeel.HitstopKill, 0f, 0f);
        }

        /// <summary>
        /// 평타·무기 틱으로 몹을 맞혔을 때 약하게 흔든다(`HitFeel.HitShakePx` — 원본엔 없는 연출, 사용자 2026-10-28).
        /// 마지막 흔들림에서 `HitShakeGap`초가 안 지났으면 건너뛴다. 멈춘 시간(unscaled)으로 잰다 — 히트스톱 중에도 간격이 흐르게.
        /// </summary>
        void HandleEnemyHit(GameObject enemy) => GappedShake(ref nextHitShakeTime, HitFeel.HitShakePx, HitFeel.HitShakeTime);

        /// <summary>지속 피해 틱(레이저·깔린 블랙홀 등)은 평타보다 약하게, 간격은 따로(`HitFeel.TickShakePx`, 사용자 2026-10-28).</summary>
        void HandleEnemyTick(GameObject enemy) => GappedShake(ref nextTickShakeTime, HitFeel.TickShakePx, HitFeel.TickShakeTime);

        void GappedShake(ref float nextTime, float px, float time)
        {
            if (GameState.Current != GameScene.Run || RunState.Over) return;
            if (Time.unscaledTime < nextTime) return;
            nextTime = Time.unscaledTime + HitFeel.HitShakeGap;
            HitFeel.Shake(px, time);
        }

        /// <summary>
        /// 원본 `resetPlayerForRun()`(:1511). 구체 타입을 모르고 <see cref="IRunResettable"/>만 보고 부른다 — `Systems`(3층)가
        /// `Characters`(2층)의 키트 클래스를 하나하나 알 필요가 없다. 키트가 이 인터페이스만 구현하면 자동으로 같이 초기화된다.
        /// </summary>
        void ResetPlayer()
        {
            if (player == null) return; // 씬엔 늘 있다(씬 검증이 본다) — 플레이어 없이 도는 테스트용
            foreach (var resettable in player.GetComponents<IRunResettable>())
                resettable.ResetForRun();
        }
    }
}
