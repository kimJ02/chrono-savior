namespace ChronoSavior.Core
{
    /// <summary>
    /// 원본 `state.scene`(project_test.html:1466)은 로비/사냥/일시정지/결과 4종이다.
    /// <see cref="Title"/>은 **원본에 없다** — 디자인(Figma "타이틀" 화면)에 새 원정·불러오기·
    /// 설정·나가기 메뉴가 생기면서 추가됐다(2026-09-16).
    /// </summary>
    public enum GameScene { Lobby, Run, Pause, Result, Title }

    /// <summary>
    /// 게임이 지금 어느 화면에 있는지. 원본 `state.scene`과 그 게임루프 분기
    /// (`frame()`: `if (state.scene !== 'run' || run.over) dt = 0`, project_test.html:7169)를 옮긴 것이다.
    ///
    /// **모든 런타임 갱신은 <see cref="IsRunning"/>이 true일 때만 돈다.** 원본은 게임 전체가 한 개의
    /// `frame()` 루프라 `dt=0`으로 한 번에 멈출 수 있지만, 우리는 컴포넌트마다 `Update()`가 따로 돌기 때문에
    /// 각 컴포넌트가 스스로 확인해야 한다 — 적·스폰·투사체·장판 전부 `Update()` 첫 줄에
    /// `if (!GameState.IsRunning) return;`을 넣는다(`docs/worksplit.md` 공유 계약).
    ///
    /// `FieldBounds`/`ProfileService`와 같은 정적 클래스 — 씬에서 오브젝트를 찾아다닐 필요가 없다.
    /// </summary>
    public static class GameState
    {
        public static GameScene Current { get; private set; } = GameScene.Lobby;

        /// <summary>런이 실제로 굴러가는 중인지(일시정지·결과·로비면 false).</summary>
        public static bool IsRunning => Current == GameScene.Run;

        /// <summary>바로 전 화면 — 일시정지에서 돌아온 건지, 로비·결과에서 새로 들어온 건지 가른다(<see cref="GameInput"/>).</summary>
        public static GameScene Previous { get; private set; } = GameScene.Lobby;

        /// <summary>화면이 바뀔 때마다 1씩 는다 — "화면이 바뀌었나"를 이벤트 구독 없이 알아보는 값(<see cref="GameInput"/>).</summary>
        public static int Version { get; private set; }

        /// <summary>화면이 바뀔 때 1회. HUD/로비 UI가 구독해서 표시를 전환한다.</summary>
        public static event System.Action<GameScene> Changed;

        public static void Set(GameScene scene)
        {
            if (Current == scene) return;
            Previous = Current;
            Current = scene;
            Version++;
            Changed?.Invoke(scene);
        }

        /// <summary>
        /// 테스트 격리용. 정적 상태 + 정적 이벤트라 리셋하지 않으면 테스트끼리 오염된다
        /// (`ProfileService.Reset`과 같은 이유 — 이 프로젝트에서 이미 겪은 함정).
        /// 구독자까지 끊어야 파괴된 오브젝트로 콜백이 날아가지 않는다.
        /// </summary>
        public static void Reset()
        {
            Current = GameScene.Lobby;
            Previous = GameScene.Lobby;
            Version++;
            Changed = null;
        }
    }
}
