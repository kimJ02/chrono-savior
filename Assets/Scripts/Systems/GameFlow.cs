using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 게임의 흐름 — **타이틀 ↔ 로비 ↔ 사냥 ↔ 일시정지 ↔ 결과**를 오가게 하는 주체(상주 씬 `00_Persistent`).
    /// 원본 `startRun`(:4293) / `pauseRun`(:4332) / `resumeRun`(:4346) / `endRun`(:4354) / `toLobby`(:4382)의 화면·시간 부분이다.
    ///
    /// 상태 자체는 <see cref="GameState"/>·<see cref="RunState"/>가 들고 있고, 이 클래스는 **전이를 실행**한다.
    /// **`GameState.Set`과 `Time.timeScale` 대입은 여기서만 한다**(`HANDOFF.md` "상태 소유권").
    /// 전투가 벌어지는 동안의 일(필드 준비·정리, 런 타이머, 보스 필드, 처치 연출, 런을 끝낼 사건 감지)은 전투 씬의
    /// <see cref="CombatDirector"/>가 한다 — 2026-11-05 씬 분할 2차 1단계에서 이 클래스(옛 `RunController`)에서 떼어 냈다.
    /// 여기서는 <see cref="CombatDirector.Current"/>를 직접 부르고, 그쪽은 이벤트(`RunEvents.RunEndRequested`·`CombatEvents.HitstopRequested`)로만 알린다.
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

        // 히트스톱 길이는 `Core/HitFeel`(원본 값, 공격·대상마다 다르다 — 2026-10-24).

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

        void OnEnable()
        {
            CombatEvents.HitstopRequested += Hitstop;
            RunEvents.RunEndRequested += EndRun;
        }

        void OnDisable()
        {
            CombatEvents.HitstopRequested -= Hitstop;
            RunEvents.RunEndRequested -= EndRun;
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
            ClearCombatField();
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

            // 히트스톱은 게임을 멈추는 것이므로 **멈춘 시간(unscaled)으로 재야 한다** —
            // `Time.deltaTime`으로 재면 timeScale이 0인 동안 타이머가 안 줄어 영원히 안 풀린다.
            if (hitstopCooldown > 0f) hitstopCooldown -= Time.unscaledDeltaTime;
            if (hitstopLeft > 0f)
            {
                hitstopLeft -= Time.unscaledDeltaTime;
                if (hitstopLeft <= 0f)
                {
                    Time.timeScale = 1f;
                    hitstopCooldown = HitstopGap; // 풀린 뒤 잠깐은 새로 안 멈춘다
                }
            }
        }

        /// <summary>
        /// 원본 `startRun(region, mode)`(project_test.html:4293). 순서: 날짜·런 상태 → 전투 필드 준비(<see cref="CombatDirector.BeginField"/>) →
        /// 사냥 화면 → 시간 흐름.
        /// </summary>
        public void StartRun(int region, RunMode mode)
        {
            var director = CombatDirector.Current;
            if (director == null)
            {
                Debug.LogError("[GameFlow] 전투 씬(CombatDirector)이 없어 사냥을 시작할 수 없다");
                return;
            }

            // 스테이지에 들어갈 때마다 멸망까지 하루가 준다(사용자 결정 2026-10-24, PlayerProfile.doomDays).
            // 보스 포탈로 보스 필드에 넘어가는 건 같은 원정이라 안 준다(그건 이 함수를 안 거친다).
            if (mode == RunMode.Normal) ProfileService.Current.SpendDay();
            RunState.Begin(region, mode);
            hitstopLeft = 0f;
            hitstopCooldown = 0f;

            director.BeginField(mode);

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

        /// <summary>
        /// 원본 `endRun(reason)`(:4354). 이미 끝난 런은 두 번 끝나지 않는다(`if (run.over) return`).
        /// 전투 쪽의 종료 사건(사망·시간 초과·보스 격파 뒤 지연)은 `RunEvents.RunEndRequested`로 여기 온다.
        /// </summary>
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
            ClearCombatField(); // 결과 화면을 거치지 않고 나가도 필드가 남지 않게
            GameState.Set(GameScene.Lobby);
            Time.timeScale = 0f;
        }

        /// <summary>
        /// 전투 필드를 비운다. 전투 씬이 아직 없으면(부팅 첫 프레임 — 월드 씬은 `GameBoot`가 겹쳐 부르는 중) 비울 것도 없다.
        /// </summary>
        static void ClearCombatField()
        {
            if (CombatDirector.Current != null) CombatDirector.Current.ClearField();
        }

        /// <summary>
        /// 타격감용 순간 정지 — 원본 `state.freeze = Math.max(state.freeze, ...)`(project_test.html:1685).
        /// **`timeScale`을 만지는 유일한 주체가 이 컨트롤러**라서 여기 둔다. 다른 데서 `timeScale`을
        /// 건드리면 일시정지·결과 화면과 서로 덮어써서 게임이 안 멈추거나 안 풀린다.
        /// 전투 쪽은 `CombatEvents.RequestHitstop`으로 요청만 한다.
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
