namespace ChronoSavior.Core
{
    /// <summary>
    /// 지금 플레이어 프로필(세이브 데이터)에 대한 정적 접근점. 어느 도메인에서든 `ProfileService.Current`로 읽고,
    /// 프로필 **안쪽 값**(골드·레벨 등)은 각 기능이 바로 바꾼다.
    ///
    /// **프로필을 통째로 바꾸는 건 세이브 시스템(`Systems/SaveService`)만 한다**(2026-11-05, `HANDOFF.md` "상태 소유권") —
    /// 불러오기(<see cref="Replace"/>)와 전체 초기화(<see cref="Reset"/>). 예전엔 `Current`가 공개 필드라 아무 데서나 갈아끼울 수 있었고,
    /// 타이틀 화면이 직접 초기화하고 있었다. `Core`는 `Systems`를 몰라서 컴파일러로는 막을 수 없으니
    /// `Tests/PlayMode/Systems/SaveServiceTests`가 소스를 훑어 다른 곳에서 부르지 않는지 검사한다(테스트 코드는 예외 — 준비·정리에 쓴다).
    /// </summary>
    public static class ProfileService
    {
        public static PlayerProfile Current { get; private set; } = new PlayerProfile();

        /// <summary>
        /// <see cref="Current"/>가 다른 인스턴스로 바뀐 직후. 프로필 **인스턴스의** 이벤트(`PlayerProfile.LeveledUp`)를 구독한 쪽은
        /// 이걸 듣고 새 프로필로 갈아타야 한다 — 안 그러면 옛 프로필을 계속 듣는다(2026-11-05 발견: '새 원정' 뒤 그 사냥에서 레벨업해도
        /// 최대 체력이 안 늘었다. 런을 새로 시작할 땐 `PlayerHealth.ResetForRun`이 다시 계산해서 가려져 있었다).
        /// </summary>
        public static event System.Action Replaced;

        /// <summary>불러온 프로필로 교체한다 — `SaveService.Load`만 부른다.</summary>
        public static void Replace(PlayerProfile profile)
        {
            Current = profile ?? throw new System.ArgumentNullException(nameof(profile));
            Replaced?.Invoke();
        }

        /// <summary>
        /// 새 프로필로 교체한다 — 실행 코드에선 `SaveService.DeleteSave`(새 원정)만 부른다. `PlayerHealth`가 `Current.LeveledUp`을
        /// 구독하는데, 정적 인스턴스라 테스트마다 새로 만든 플레이어가 같은 이벤트에 쌓이면 이전 테스트의 파괴된 오브젝트를 향한 호출이 남는다
        /// — PlayMode 테스트는 `[SetUp]`/`[TearDown]`에서 이걸 불러 격리한다.
        /// </summary>
        public static void Reset()
        {
            Current = new PlayerProfile();
            Replaced?.Invoke();
        }
    }
}
