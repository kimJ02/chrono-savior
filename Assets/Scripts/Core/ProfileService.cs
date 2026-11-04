namespace ChronoSavior.Core
{
    /// <summary>
    /// 현재 플레이어 프로필에 대한 정적 접근점 — `FieldBounds`와 같은 패턴. 씬에서 오브젝트를
    /// 찾아다니지 않고 어느 도메인에서든 `ProfileService.Current`로 바로 읽고 쓸 수 있다.
    ///
    /// 세이브/로드(파일 입출력)는 이번 스프린트 범위 밖(`docs/sprints/03-growth-curve.md` "범위 밖" 참고,
    /// "인메모리 상태로 충분") — 그래서 지금은 시작할 때 기본값 하나만 만들어두는 게 전부다.
    /// 나중에 세이브 시스템(`Systems/SaveSystem`)이 생기면 여기서 로드한 인스턴스로 교체한다.
    /// </summary>
    public static class ProfileService
    {
        public static PlayerProfile Current = new PlayerProfile();

        /// <summary>
        /// 새 프로필로 완전히 교체한다. `PlayerHealth`가 `Current.LeveledUp`을 구독하는데,
        /// 정적 인스턴스라 테스트마다 새로 만든 플레이어 오브젝트들이 전부 같은 이벤트에 계속
        /// 쌓여 구독하면 이전 테스트의 파괴된 오브젝트를 향한 호출이 남는다(이 프로젝트에서
        /// `EnemyHealth` 테스트 오염을 이미 한 번 겪은 것과 같은 종류) — PlayMode 테스트는
        /// `[TearDown]`에서 반드시 이걸 불러 격리할 것.
        /// </summary>
        public static void Reset() => Current = new PlayerProfile();
    }
}
