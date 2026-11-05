using System.Collections;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.World;

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

        // ───────────────────────── 월드 씬 전환 (2026-11-05 씬 분할 2차 3-4) ─────────────────────────

        /// <summary>로비 월드 씬(빌드 1번).</summary>
        public const string LobbyScene = "10_Lobby";
        /// <summary>전투 월드 씬(빌드 2번).</summary>
        public const string CombatScene = "20_Combat";

        /// <summary>
        /// 월드 씬을 불러오고 내리는 일(`IWorldScenes`). 실제 게임은 `SceneWorldScenes`, 씬 없이 도는 테스트는 가짜 로더를 넣는다 —
        /// 가짜 로더는 즉시 끝나서 전환이 그 자리에서 끝난다(아래 <see cref="Transition"/>은 끝난 작업을 기다리며 yield하지 않는다).
        /// </summary>
        public IWorldScenes Scenes { get; set; } = new SceneWorldScenes();

        /// <summary>지금 떠 있는 월드 씬(로비 또는 전투) — 타이틀·부팅 직후엔 없음.</summary>
        public WorldHandle CurrentWorld { get; private set; } = WorldHandle.None;

        /// <summary>화면을 옮기는 중인지 — 그동안 시작·로비·타이틀·새 원정·일시정지·재개·종료 요청을 받지 않는다(<see cref="GameState.IsTransitioning"/>).</summary>
        public bool IsTransitioning => GameState.IsTransitioning;

        /// <summary>
        /// 에디터 Play 진입 씬을 넘겨받는 열쇠(`SessionState`) — `Editor/PlayFromPersistent`가 Play 직전에 열어 둔 게임 씬 이름을 쓰고, 여기서 한 번 읽고 지운다
        /// (2026-11-05 씬 분할 2차 3-5). 빌드에는 없다.
        /// </summary>
        public const string EditorEntryKey = "ChronoSavior.EditorEntryScene";

        /// <summary>실제 게임의 흐름이 사는 상주 씬 — 에디터 진입은 이 씬의 흐름만 따른다(테스트가 빈 씬에 만든 흐름은 무시).</summary>
        public const string PersistentScene = "00_Persistent";

        void Start()
        {
#if UNITY_EDITOR
            // 에디터에서 `10_Lobby`/`20_Combat`을 열고 Play하면 그 화면으로 바로(타이틀을 거치지 않게). 사냥은 날짜를 쓰지 않는다(디버그 진입).
            if (gameObject.scene.name == PersistentScene)
            {
                string entry = UnityEditor.SessionState.GetString(EditorEntryKey, "");
                UnityEditor.SessionState.EraseString(EditorEntryKey); // 한 번만
                if (entry == LobbyScene) { EnterLobby(); return; }
                if (entry == CombatScene) { StartRun(defaultRegion, RunMode.Normal, null, spendDay: false); return; }
            }
#endif
            // 원본은 로비에서 시작하지만(`state.scene` 초기값 'lobby', :1466), 디자인에 타이틀
            // 화면이 생겨서(Figma 35:2) 그쪽이 먼저다. 로비로는 타이틀 메뉴를 거쳐 들어간다. 타이틀엔 월드가 없다.
            EnterTitle();
        }

        /// <summary>
        /// 타이틀 화면으로. **원본에 없는 화면**이다(2026-09-16, Figma 적용). 떠 있는 월드를 내린다 — 타이틀엔 월드가 없다.
        /// </summary>
        public bool EnterTitle(System.Action<bool> done = null) =>
            BeginTransition(null, false, () => { GameState.Set(GameScene.Title); Time.timeScale = 0f; return true; }, done);

        /// <summary>
        /// 원본 `startRun(region, mode)`(project_test.html:4293). 전투 씬을 새로 불러와(재도전도 — 지금 전투 씬은 멈췄다가 새 씬이 뜨면 내린다)
        /// **불러오기가 성공한 뒤에만** 날짜 차감 → 런 상태 → 필드 준비(<see cref="CombatDirector.BeginField"/>) → 사냥 화면 → 시간 흐름.
        /// 실패하면 이전 화면·월드로 되돌리고 <paramref name="done"/>에 false를 준다. 받아들이지 않으면(전환 중) false를 돌려준다.
        /// </summary>
        /// <param name="spendDay">스테이지에 들어갈 때마다 멸망까지 하루가 준다(사용자 결정 2026-10-24) — 에디터 디버그 진입만 false.</param>
        public bool StartRun(int region, RunMode mode, System.Action<bool> done = null, bool spendDay = true) =>
            BeginTransition(CombatScene, false, () =>
            {
                var director = CombatDirector.Current;
                if (director == null)
                {
                    Debug.LogError("[GameFlow] 전투 씬을 불러왔는데 전투 담당(CombatDirector)이 없다");
                    return false;
                }
                var profile = ProfileService.Current;
                int daysBefore = profile.doomDays;
                try
                {
                    // 보스 포탈로 보스 필드에 넘어가는 건 같은 원정이라 안 준다(그건 이 함수를 안 거친다).
                    if (spendDay && mode == RunMode.Normal) profile.SpendDay();
                    RunState.Begin(region, mode);
                    hitstopLeft = 0f;
                    hitstopCooldown = 0f;
                    director.BeginField(mode);
                }
                catch
                {
                    // 필드 준비가 예외로 실패하면 깎은 날짜를 돌려준다 — 전환이 이전 화면으로 되돌린다(들어가지 못한 사냥에 하루를 쓰지 않게).
                    profile.doomDays = daysBefore;
                    throw;
                }
                GameState.Set(GameScene.Run);
                Time.timeScale = 1f;
                return true;
            }, done);

        /// <summary>로비에서 기본 지역으로 바로 시작(임시 진입점 — 정식 지역 선택은 로비 스테이지 탭).</summary>
        public bool StartRun() => StartRun(defaultRegion, RunMode.Normal);

        /// <summary>원본 `toLobby()`(:4382) — 로비 씬을 불러오고 지금 월드(전투)를 내린다.</summary>
        public bool EnterLobby(System.Action<bool> done = null) => EnterLobbyThen(null, done);

        /// <summary>
        /// 타이틀의 "새 원정" — 로비 씬을 **불러온 뒤에** 세이브를 지우고(새 프로필) 로비로(불러오기가 실패하면 진행을 잃지 않게).
        /// 프로필 교체는 `SaveService`가, 화면 이동은 여기서 한다(예전엔 `TitleMenu`가 직접 했다).
        /// </summary>
        public bool StartNewExpedition(System.Action<bool> done = null) => EnterLobbyThen(SaveService.DeleteSave, done);

        // 로비는 일반 무대 기준(가운데 자리·카메라 범위) — 로비 씬이 뜨며 준비하기 전에 바꾼다(normalArena). 실패하면 전환이 되돌린다.
        bool EnterLobbyThen(System.Action beforeLobby, System.Action<bool> done) =>
            BeginTransition(LobbyScene, true, () =>
            {
                beforeLobby?.Invoke();
                GameState.Set(GameScene.Lobby);
                Time.timeScale = 0f; // 로비는 시간이 멈춘 채(로비 걷기는 실제 시간) — 3단계에선 그대로(4단계에서 판단)
                return true;
            }, done);

        /// <summary>전환을 시작한다 — 이미 옮기는 중이면 받지 않는다(false).</summary>
        bool BeginTransition(string nextScene, bool normalArena, System.Func<bool> enter, System.Action<bool> done)
        {
            if (IsTransitioning) return false;
            StartCoroutine(Transition(nextScene, normalArena, enter, done));
            return true;
        }

        /// <summary>
        /// 화면 전환 — 잠금 → 이전 월드 멈춤 → 새 월드 불러오기 → 활성 씬 → 들어가기(<paramref name="enter"/>) → 이전 월드 내리기 → 잠금 해제.
        /// 불러오기·들어가기가 실패하면 **바꾸기 전 상태**(게임 화면·시간·보스 무대 값·이전 월드와 활성 씬)로 되돌린다. 그래서 들어가기 전엔 아무것도 지우지 않는다.
        /// 끝난 작업은 기다리지 않아서(yield 없음) 즉시 끝나는 로더면 전환 전체가 이 호출 안에서 끝난다.
        /// </summary>
        IEnumerator Transition(string nextScene, bool normalArena, System.Func<bool> enter, System.Action<bool> done)
        {
            GameState.SetTransitioning(true);
            var old = CurrentWorld;
            float timeScaleBefore = Time.timeScale;
            bool bossArenaBefore = FieldLayout.IsBossArena;
            string failure = null;
            if (!old.IsNone) failure = Safe(() => Scenes.SetRunning(old, false), "이전 월드 멈추기");
            if (normalArena) FieldLayout.SetBossArena(false);

            var fresh = WorldHandle.None;
            if (failure == null && nextScene != null)
            {
                WorldSceneOp load = null;
                // 예외도 실패로 — 잠금이 풀리지 않거나 이전 월드가 멈춘 채 남지 않게(yield는 try 밖에서).
                failure = Safe(() => load = Scenes.Load(nextScene), nextScene + " 불러오기");
                if (failure == null)
                {
                    while (!load.IsDone) yield return null;
                    if (load.Succeeded) { fresh = load.World; failure = Safe(() => Scenes.Activate(fresh), "새 월드 활성화"); }
                    else failure = load.Error;
                }
            }
            if (failure == null)
            {
                try { if (!enter()) failure = $"{nextScene ?? "타이틀"}에 들어가지 못했다"; }
                catch (System.Exception e) { Debug.LogException(e); failure = $"{nextScene ?? "타이틀"}에 들어가다 예외: {e.Message}"; }
            }

            if (failure != null)
            {
                Debug.LogError("[GameFlow] 화면 전환 실패 — 이전 화면으로 되돌린다: " + failure);
                if (!fresh.IsNone)
                {
                    // 새 월드부터 멈춘다 — 내리기가 실패해도 두 월드가 같이 켜져 있지 않게(전투 담당이 둘이 되지 않게).
                    Safe(() => Scenes.SetRunning(fresh, false), "새 월드 멈추기");
                    var drop = StartUnload(fresh);
                    while (!drop.IsDone) yield return null;
                    ReportUnload(fresh, drop);
                }
                // 값부터 되돌리고 이전 월드를 켠다 — 다시 켜질 때(OnEnable) 이전 무대 값을 보게.
                Time.timeScale = timeScaleBefore;
                FieldLayout.SetBossArena(bossArenaBefore);
                if (!old.IsNone) Safe(() => { Scenes.Activate(old); Scenes.SetRunning(old, true); }, "이전 월드 되살리기");
                GameState.SetTransitioning(false);
                done?.Invoke(false);
                yield break;
            }

            CurrentWorld = fresh;
            // 예전 전환에서 못 내린 월드(꺼진 채 남은 것)가 있으면 이번에 다시 내려 본다 — 이번에 못 내린 건 다음 전환에.
            var earlier = stranded.ToArray();
            stranded.Clear();
            foreach (var w in earlier)
            {
                var retry = StartUnload(w);
                while (!retry.IsDone) yield return null;
                ReportUnload(w, retry);
            }
            if (!old.IsNone)
            {
                var unload = StartUnload(old);
                while (!unload.IsDone) yield return null;
                ReportUnload(old, unload);
            }
            GameState.SetTransitioning(false);
            done?.Invoke(true);
        }

        /// <summary>내리지 못해 꺼진 채 남은 월드 — 다음 전환이 성공할 때 다시 내려 본다.</summary>
        readonly System.Collections.Generic.List<WorldHandle> stranded = new System.Collections.Generic.List<WorldHandle>();

        /// <summary>그 일을 하고, 예외가 나면 실패 문구를 돌려준다(성공이면 null) — 전환 중 어디서 던져도 잠금이 풀리고 되돌리게.</summary>
        static string Safe(System.Action action, string what)
        {
            try { action(); return null; }
            catch (System.Exception e) { Debug.LogException(e); return $"{what} 중 예외: {e.Message}"; }
        }

        /// <summary>월드 내리기를 시작한다(예외는 실패로). 내리기가 실패해도 전환 자체는 되돌리지 않는다 — 그 월드는 꺼진 채 남고 <see cref="stranded"/>에 넣어 다음 전환에 다시 내린다.</summary>
        /// <remarks>기다리는 건 부르는 쪽(<see cref="Transition"/>)이 직접 한다 — 코루틴 안에서 다른 IEnumerator를 yield하면 끝난 작업이어도 한 프레임을 쉬어서, 즉시 끝나는 로더의 전환이 그 자리에서 안 끝난다.</remarks>
        WorldSceneOp StartUnload(WorldHandle world)
        {
            try { return Scenes.Unload(world); }
            catch (System.Exception e) { return WorldSceneOp.Failed($"내리기 예외: {e.Message}"); }
        }

        void ReportUnload(WorldHandle world, WorldSceneOp op)
        {
            if (op.Succeeded) return;
            Debug.LogError($"[GameFlow] 월드를 내리지 못했다(꺼진 채 남음, 다음 전환에 다시 시도): {world} — {op.Error}");
            if (!stranded.Contains(world)) stranded.Add(world);
        }

        void Update()
        {
            if (IsTransitioning) return; // 옮기는 중엔 ESC를 받지 않는다(일시정지 메뉴 '로비로' 전환 중 ESC → 재개 → 꺼진 월드에서 시간이 흐르던 구멍)

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

        /// <summary>원본 `pauseRun()`(:4332) — 사냥 중에만, 그리고 이미 끝난 런에선 안 걸린다.</summary>
        public void Pause()
        {
            if (IsTransitioning || RunState.Over || GameState.Current != GameScene.Run) return;
            hitstopLeft = 0f; // 히트스톱 중에 멈추면 그게 timeScale을 다시 1로 되돌려버린다
            GameState.Set(GameScene.Pause);
            Time.timeScale = 0f;
        }

        /// <summary>원본 `resumeRun()`(:4346).</summary>
        public void Resume()
        {
            if (IsTransitioning || GameState.Current != GameScene.Pause) return;
            GameState.Set(GameScene.Run);
            Time.timeScale = 1f;
            hitstopLeft = 0f;
        }

        /// <summary>
        /// 원본 `endRun(reason)`(:4354). 이미 끝난 런은 두 번 끝나지 않는다(`if (run.over) return`).
        /// 전투 쪽의 종료 사건(사망·시간 초과·보스 격파 뒤 지연)은 `RunEvents.RunEndRequested`로 여기 온다. 옮기는 중엔 받지 않는다.
        /// </summary>
        public void EndRun(RunEndReason reason)
        {
            if (IsTransitioning || RunState.Over) return;
            RunState.MarkOver();
            LastEndReason = reason;
            hitstopLeft = 0f; // 히트스톱 중에 멈추면 그게 timeScale을 다시 1로 되돌려버린다
            GameState.Set(GameScene.Result);
            Time.timeScale = 0f;
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
            if (IsTransitioning || GameState.Current != GameScene.Run || RunState.Over) return;
            // 쉬는 중이면 건너뛴다(HitstopGap) — 이미 멈춰 있는 중이거나 아주 긴 멈춤이면 예외.
            if (hitstopLeft <= 0f && hitstopCooldown > 0f && duration < HitstopGapBypass) return;
            hitstopLeft = Mathf.Max(hitstopLeft, duration); // 원본도 max로 겹친다
            Time.timeScale = 0f;
        }
    }
}
