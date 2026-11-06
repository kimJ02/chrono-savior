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
    /// **시간은 `Time.timeScale`로 멈춘다**(바꾸는 곳은 `Systems/GameFlow` 하나 — 일시정지·결과·로비·타이틀·히트스톱에 0).
    /// 원본은 게임 전체가 한 개의 `frame()` 루프라 `dt=0`으로 한 번에 멈추는데, 우리 쪽은 `Time.deltaTime`·물리를 쓰는
    /// 갱신(이동·타이머·투사체·장판)이 같은 방식으로 저절로 멈춘다. 다만 `Update()` 자체는 계속 돌므로
    /// **시간과 상관없이 일을 하는 갱신(키 입력·겹침 판정)은 <see cref="IsRunning"/>을 스스로 확인한다**(예: `GameInput`의 전투 키).
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

        /// <summary>
        /// 화면 사이를 옮기는 중인지(월드 씬을 내리고 불러오는 동안, 2026-11-05 씬 분할 2차 3-4). 이동 중엔 입력·전환 요청을 받지 않는다 —
        /// `Systems/GameFlow`만 바꾸고, 다른 씬의 입력(로비 걷기 등)이 이걸 본다(그쪽은 GameFlow를 모른다).
        /// </summary>
        public static bool IsTransitioning { get; private set; }

        /// <summary>`GameFlow`만 부른다.</summary>
        public static void SetTransitioning(bool transitioning) => IsTransitioning = transitioning;

        public static void Set(GameScene scene)
        {
            if (Current == scene) return;
            Previous = Current;
            Current = scene;
            Version++;
            // 구독자 하나가 던져도 다른 구독자와 화면 전환은 계속된다 — 던지면 화면 값은 이미 바뀐 채 전환만 실패로 되돌아가
            // 상태가 어긋난다(2026-11-07 최종 점검). 예외는 그대로 기록한다(테스트는 기록된 예외로 실패한다).
            var handlers = Changed;
            if (handlers == null) return;
            foreach (System.Action<GameScene> h in handlers.GetInvocationList())
            {
                try { h(scene); }
                catch (System.Exception e) { UnityEngine.Debug.LogException(e); }
            }
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
            IsTransitioning = false;
            Version++;
            Changed = null;
        }
    }
}
