using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 게임의 뼈대 — **로비 ↔ 사냥 ↔ 일시정지 ↔ 결과**를 오가게 하는 주체.
    /// 원본 `startRun`(:4293) / `pauseRun`(:4332) / `resumeRun`(:4346) / `endRun`(:4354) /
    /// `toLobby`(:4382)를 한 컴포넌트로 옮긴 것이다.
    ///
    /// 상태 자체는 <see cref="GameState"/>·<see cref="RunState"/>가 이미 들고 있고, 이 클래스는
    /// **전이를 실행**한다(필드 정리·플레이어 리셋·시간 정지).
    ///
    /// ## 왜 `Time.timeScale`로 멈추는가 (확정 설계, docs/sprints/04-run-cycle.md 3번)
    /// 원본은 게임 전체가 한 개의 `frame()` 루프라 `state.scene !== 'run'`이면 `dt = 0`을 줘서
    /// **한 곳에서** 전부 멈춘다(`:7169`). 우리는 컴포넌트마다 `Update()`가 따로 돌아 그게 안 되므로
    /// `Time.timeScale = 0`으로 물리와 `Time.deltaTime`을 통째로 멈춘다 — 같은 효과다.
    ///
    /// **덕분에 적·스폰너·투사체·장판에 `IsRunning` 가드를 하나도 안 넣어도 된다.**
    /// (`EnemySpawner.waveTimer -= Time.deltaTime`이 0을 빼게 되므로 로비에서 몹이 안 나온다.)
    /// 대신 이 컨트롤러와 UI만 `timeScale`에 영향받지 않아야 한다 — `Update()`는 `timeScale`이 0이어도
    /// 계속 돌기 때문에 여기서 ESC를 읽는 건 문제없다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameFlow : MonoBehaviour
    {
        [Header("시작 설정")]
        [Tooltip("로비에서 '사냥 시작'을 눌렀을 때 들어갈 지역. 정식 지역 선택은 로비 스테이지 탭이 맡는다.")]
        public int defaultRegion = 1;

        /// <summary>마지막 런이 끝난 이유. 결과 화면이 제목을 고르는 데 쓴다(원본 `endRun(reason)`).</summary>
        public RunEndReason LastEndReason { get; private set; } = RunEndReason.Timeout;

        // 히트스톱 길이는 `Core/HitFeel`(원본 값, 공격·대상마다 다르다 — 2026-10-24). 예전 인스펙터 칸 두 개(맞을 때 0.045·
        // 처치 0.085)는 없앴다.

        float hitstopLeft;

        /// <summary>
        /// 히트스톱 사이에 두는 쉬는 시간(초) — 2026-10-25 사용자 "히트스톱을 모든 히트마다 걸지 말고 어느 정도 시간을 두고".
        /// 원본은 맞힐 때마다 `freeze = max(freeze, …)`(:1683)라, 섬영처럼 0.13초마다 휘두르면 시간의 4분의 1쯤이 멈춰 있었다(의도적 편차).
        /// 한 번 멈췄다 풀리면 이 시간 동안은 새로 멈추지 않는다. 이미 멈춰 있는 동안 들어온 더 긴 멈춤(처치 등)은 원본처럼 늘린다.
        /// </summary>
        public const float HitstopGap = 0.25f;
        /// <summary>이 길이 이상의 멈춤(성소 0.12·보스 처치 0.25)은 쉬는 시간과 상관없이 건다 — 드물고, 그 순간 연출의 핵심이라.</summary>
        public const float HitstopGapBypass = HitFeel.HitstopShrine;
        float hitstopCooldown;

        /// <summary>원본 `setTimeout(() => endRun('bossdead'), 1600)`(project_test.html:4289).</summary>
        public const float BossDeadEndDelay = 1.6f;
        float bossDeadTimer = -1f;

        void OnEnable()
        {
            CombatEvents.PlayerDied += HandlePlayerDied;
            CombatEvents.EnemyKilled += HandleEnemyKilled;
            CombatEvents.HitstopRequested += Hitstop;
            CombatEvents.EnemyDamaged += HandleEnemyHit;
            CombatEvents.EnemyTickDamaged += HandleEnemyTick;
            CombatEvents.ShrineBuffGranted += CombatModifiers.GrantShrineBuff;
            RunEvents.BossPortalRequested += HandleBossPortalRequested;
            RunEvents.BossDefeated += HandleBossDefeated;
        }

        void OnDisable()
        {
            CombatEvents.PlayerDied -= HandlePlayerDied;
            CombatEvents.EnemyKilled -= HandleEnemyKilled;
            CombatEvents.HitstopRequested -= Hitstop;
            CombatEvents.EnemyDamaged -= HandleEnemyHit;
            CombatEvents.EnemyTickDamaged -= HandleEnemyTick;
            CombatEvents.ShrineBuffGranted -= CombatModifiers.GrantShrineBuff;
            RunEvents.BossPortalRequested -= HandleBossPortalRequested;
            RunEvents.BossDefeated -= HandleBossDefeated;
        }

        void Start()
        {
            // 원본은 로비에서 시작하지만(`state.scene` 초기값 'lobby', :1466), 디자인에 타이틀
            // 화면이 생겨서(Figma 35:2) 그쪽이 먼저다. 로비로는 타이틀 메뉴를 거쳐 들어간다.
            EnterTitle();
        }

        /// <summary>
        /// 타이틀 화면으로. **원본에 없는 화면**이다(2026-09-16, Figma 적용).
        /// 로비와 같은 정리를 한다 — 타이틀에서 "새 원정"을 고르면 바로 로비로 가므로
        /// 필드에 지난 런의 잔해가 남아 있으면 안 된다.
        /// </summary>
        public void EnterTitle()
        {
            ChronoSavior.World.PlatformSet.Activate(false);
            RunTransient.DestroyAll();
            bossDeadTimer = -1f;
            GameState.Set(GameScene.Title);
            Time.timeScale = 0f;
        }

        void Update()
        {
            // ⚠️ `timeScale = 0`이어도 `Update`는 계속 돈다 — 멈추는 건 `Time.deltaTime`과 물리다.
            // 그래서 일시정지 중에도 ESC를 읽을 수 있다.
            // 설정 창 같은 창이 떠 있으면 ESC는 창 몫이다(창 닫기) — 일시정지까지 같이 풀리지 않게(UiModal, 2026-10-24).
            if (GameInput.PauseDown && !UiModal.OwnsEscape)
            {
                if (GameState.Current == GameScene.Run) Pause();
                else if (GameState.Current == GameScene.Pause) Resume();
            }

            if (GameState.Current != GameScene.Run) return;

            // 보스 격파 후 결과 화면까지의 지연. **멈춘 시간(unscaled)으로 잰다** — 히트스톱이
            // 걸려 있으면 `Time.deltaTime`이 0이라 타이머가 안 줄고 결과 화면이 영영 안 뜬다.
            if (bossDeadTimer >= 0f)
            {
                bossDeadTimer -= Time.unscaledDeltaTime;
                if (bossDeadTimer <= 0f)
                {
                    bossDeadTimer = -1f;
                    EndRun(RunEndReason.BossDead);
                }
                return; // 격파 뒤엔 런 타이머가 더 흐르지 않는다(timeout으로 뒤집히면 안 된다)
            }

            // 히트스톱은 게임을 멈추는 것이므로 **멈춘 시간(unscaled)으로 재야 한다** —
            // `Time.deltaTime`으로 재면 timeScale이 0인 동안 타이머가 안 줄어 영원히 안 풀린다.
            HitFeel.TickRedFlash(Time.deltaTime); // 원본 `state.redFlash -= dt * 2.2`(:4426) — 게임 시간
            if (hitstopCooldown > 0f) hitstopCooldown -= Time.unscaledDeltaTime;
            if (hitstopLeft > 0f)
            {
                hitstopLeft -= Time.unscaledDeltaTime;
                if (hitstopLeft <= 0f)
                {
                    Time.timeScale = 1f;
                    hitstopCooldown = HitstopGap; // 풀린 뒤 잠깐은 새로 안 멈춘다
                }
                return; // 멈춘 동안엔 런 타이머도 안 간다(원본도 dt=0이라 같다)
            }

            // 원본 `run.timeLeft -= dt; if (<= 0) endRun('timeout')`(:4412).
            if (RunState.Tick(Time.deltaTime)) EndRun(RunEndReason.Timeout);

            // 콤보 창·연쇄 처치 창·성소 버프 감소(원본 `updateCombo` :1630 등).
            CombatModifiers.Tick(Time.deltaTime);
        }

        /// <summary>원본 `startRun(region, mode)`(project_test.html:4293).</summary>
        public void StartRun(int region, RunMode mode)
        {
            // 스테이지에 들어갈 때마다 멸망까지 하루가 준다(사용자 결정 2026-10-24, PlayerProfile.doomDays).
            // 보스 포탈로 보스 필드에 넘어가는 건 같은 원정이라 안 준다(그건 이 함수를 안 거친다).
            if (mode == RunMode.Normal) ProfileService.Current.SpendDay();
            RunState.Begin(region, mode);

            // 무대를 먼저 바꾼다 — 발판 배치와 맵 폭(`FieldBounds.MaxX`)이 여기서 정해지고,
            // 아래의 플레이어 리셋·필드 정리가 그 값을 보고 좌표를 잡는다.
            // 원본도 `startRun`에서 `run.mapW`와 `platforms`를 먼저 갈아끼운다(:4313·:4316).
            ChronoSavior.World.PlatformSet.Activate(mode == RunMode.Boss);

            // 원본 `enemies = []; projectiles = []; zones = []; ...`(:4318) — 지난 런의 잔해를 지운다.
            RunTransient.DestroyAll();
            CombatModifiers.ResetForRun(); // 원본 `combo.n = 0; run.chainN = 0`(:4307·:4319)
            hitstopLeft = 0f;
            hitstopCooldown = 0f;
            HitFeel.Reset();
            bossDeadTimer = -1f;

            ResetPlayer();

            GameState.Set(GameScene.Run);
            Time.timeScale = 1f;
        }

        /// <summary>로비에서 기본 지역으로 바로 시작(임시 진입점 — 정식 지역 선택은 로비 스테이지 탭).</summary>
        public void StartRun() => StartRun(defaultRegion, RunMode.Normal);

        /// <summary>원본 `pauseRun()`(:4332) — 사냥 중에만, 그리고 이미 끝난 런에선 안 걸린다.</summary>
        public void Pause()
        {
            if (RunState.Over || GameState.Current != GameScene.Run) return;
            hitstopLeft = 0f; // 히트스톱 중에 멈추면 그게 timeScale을 다시 1로 되돌려버린다
            GameState.Set(GameScene.Pause);
            Time.timeScale = 0f;
        }

        /// <summary>원본 `resumeRun()`(:4346).</summary>
        public void Resume()
        {
            if (GameState.Current != GameScene.Pause) return;
            GameState.Set(GameScene.Run);
            Time.timeScale = 1f;
            hitstopLeft = 0f;
        }

        /// <summary>원본 `endRun(reason)`(:4354). 이미 끝난 런은 두 번 끝나지 않는다(`if (run.over) return`).</summary>
        public void EndRun(RunEndReason reason)
        {
            if (RunState.Over) return;
            RunState.MarkOver();
            LastEndReason = reason;
            hitstopLeft = 0f; // 히트스톱 중에 멈추면 그게 timeScale을 다시 1로 되돌려버린다
            GameState.Set(GameScene.Result);
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 보스 포탈 입장 요청을 처리한다 — **원본에 없는 전환**(사용자 지시 2026-09-16).
        ///
        /// 판단은 전부 여기서 한다(포탈은 요청만 던진다, <see cref="RunEvents"/> 주석 참고):
        /// 사냥 중인지 · 이미 보스 필드가 아닌지 · 그 지역 보스가 해금됐는지.
        /// </summary>
        void HandleBossPortalRequested()
        {
            if (GameState.Current != GameScene.Run) return;
            if (!ProfileService.Current.IsBossUnlocked(RunState.Region)) return;
            EnterBossField();
        }

        /// <summary>
        /// 일반 필드 → 보스 필드. 무대·타이머·필드 정리·플레이어 위치를 한 번에 바꾼다.
        /// <see cref="StartRun"/>과 같은 순서를 쓰는 이유는 거기 주석에 있다(무대를 먼저 바꿔야
        /// `FieldBounds.MaxX`가 갱신되고, 그 뒤 좌표 계산이 맞는다).
        /// </summary>
        /// <returns>실제로 전환됐으면 true.</returns>
        public bool EnterBossField()
        {
            if (!RunState.EnterBossField()) return false;

            ChronoSavior.World.PlatformSet.Activate(true);

            // 일반 필드의 잡몹·투사체·장판을 지운다. 안 지우면 좁아진 보스 무대에 이전 필드의
            // 몹이 맵 밖 좌표로 남아, 플레이어가 닿을 수 없는 곳에서 계속 살아 있다.
            RunTransient.DestroyAll();

            ResetPlayer(); // 좁아진 무대의 시작 지점으로
            RunEvents.RaiseBossFieldEntered(); // 스포너가 보스를 세운다
            return true;
        }

        /// <summary>
        /// 보스 격파. 원본은 **1.6초 뒤에** 런을 끝낸다(`:4289`) — 즉사 연출과 보상 표시를
        /// 볼 시간을 준다. 즉시 결과 화면을 띄우면 무엇을 잡았는지 보이지 않는다.
        /// **보스 격파 → 런 종료 예약은 이 한 곳뿐이다**(<see cref="HandleEnemyKilled"/> 주석 참고) —
        /// 타이머는 사냥 시작·로비 진입 때 -1로 초기화되므로 다음 런으로 새지 않는다.
        /// </summary>
        void HandleBossDefeated() => bossDeadTimer = BossDeadEndDelay;

        /// <summary>
        /// 타이틀의 "새 원정" — 세이브를 지우고(새 프로필) 로비로. 프로필 교체는 `SaveService`가, 화면 이동은 여기서 한다
        /// (2026-11-05 — 예전엔 `TitleMenu`가 직접 세이브를 지우고 프로필을 초기화했다).
        /// </summary>
        public void StartNewExpedition()
        {
            SaveService.DeleteSave();
            EnterLobby();
        }

        /// <summary>원본 `toLobby()`(:4382).</summary>
        public void EnterLobby()
        {
            ChronoSavior.World.PlatformSet.Activate(false); // 로비에선 일반 무대로 되돌린다
            RunTransient.DestroyAll(); // 결과 화면을 거치지 않고 나가도 필드가 남지 않게
            bossDeadTimer = -1f;
            GameState.Set(GameScene.Lobby);
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 원본 `resetPlayerForRun()`(:1511). 구체 타입을 모르고 <see cref="IRunResettable"/>만 보고 부른다
        /// — `Systems`(3층)가 `Characters`(2층)의 클래스를 직접 알 필요가 없다(CLAUDE.md 계층 규칙).
        /// 섬영·드루이드 키트도 이 인터페이스만 구현하면 자동으로 같이 초기화된다.
        /// </summary>
        void ResetPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            foreach (var resettable in player.GetComponents<IRunResettable>())
                resettable.ResetForRun();
        }

        void HandlePlayerDied() => EndRun(RunEndReason.Dead); // 원본 :1927

        float nextHitShakeTime, nextTickShakeTime;

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

        void HandleEnemyKilled(GameObject enemy)
        {
            var (stop, shakeMag, shakeDur) = KillFeel(
                enemy != null && enemy.GetComponent<ChronoSavior.Enemies.Boss>() != null,
                enemy != null && enemy.GetComponent<ChronoSavior.Enemies.Shrine>() != null,
                enemy != null && enemy.GetComponent<ChronoSavior.Enemies.EnemyElite>() != null);
            Hitstop(stop);
            if (shakeMag > 0f) HitFeel.Shake(shakeMag, shakeDur);

            // ⚠️ 보스 격파 뒤 결과 화면 예약은 **여기서 하지 않는다** — `RunEvents.BossDefeated` →
            // <see cref="HandleBossDefeated"/>(`bossDeadTimer`, 멈춘 시간 기준)가 한다.
            // 예전엔 여기서도 `Invoke(EndRunAfterBoss, 1.6)`를 걸어 **두 번** 예약했는데, `Invoke`는 게임 시간
            // 기준이라 결과·로비의 `timeScale = 0` 동안 멈춘 채 남아 있다가 **다음 사냥 시작 직후** 터져서
            // 방금 들어온 런을 "보스 격파"로 끝냈다(2026-09-28 사용자 발견). 취소도 안 되는 경로라 없앴다.
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
        /// 타격감용 순간 정지 — 원본 `state.freeze = Math.max(state.freeze, ...)`(project_test.html:1685).
        /// **`timeScale`을 만지는 유일한 주체가 이 컨트롤러**라서 여기 둔다. 다른 데서 `timeScale`을
        /// 건드리면 일시정지·결과 화면과 서로 덮어써서 게임이 안 멈추거나 안 풀린다.
        ///
        /// 사냥 중이 아닐 땐 무시한다 — 로비/결과에서 걸리면 그 화면이 `timeScale = 1`로 풀려버린다.
        /// </summary>
        public void Hitstop(float duration)
        {
            if (GameState.Current != GameScene.Run || RunState.Over) return;
            // 쉬는 중이면 건너뛴다(HitstopGap) — 이미 멈춰 있는 중이거나 아주 긴 멈춤이면 예외.
            if (hitstopLeft <= 0f && hitstopCooldown > 0f && duration < HitstopGapBypass) return;
            hitstopLeft = Mathf.Max(hitstopLeft, duration); // 원본도 max로 겹친다
            Time.timeScale = 0f;
        }
    }
}
