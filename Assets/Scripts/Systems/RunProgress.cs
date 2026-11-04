using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 난이도 스케일링 대리 지표 "가상 지역 레벨"(`docs/sprints/03-growth-curve-worksplit.md` 트랙 A 1번,
    /// `docs/sprints/03-growth-curve.md` 5번). 실제 9지역(진행 UI·경계·입장료)은 이번 스프린트 범위 밖이라,
    /// 원본 `regionKillTarget`(project_test.html:699)만 그대로 가져와 누적 처치 수로 대체한다.
    /// 트리거를 "누적 처치 수"로 할지 "생존 시간"으로 할지는 팀 논의로 확정된 사항(2026-09-08,
    /// PROGRESS.md 로그) — 시간/레벨 기준은 원본에 없는 설계라 기각됐다.
    ///
    /// 기준 상수(`KillsPerRegionLevel`)는 여기 두지 않고 `Core/DifficultyScalingConfig`에 모아둔다
    /// (2026-09-08 "0. 착수 전 필독" 추가 — 숫자는 전부 그 파일 하나로).
    ///
    /// `FieldBounds`와 같은 패턴의 정적 클래스 — 씬에서 오브젝트를 찾아다닐 필요 없이
    /// `RunProgress.RegionLv`로 어디서든 바로 읽는다.
    /// </summary>
    public static class RunProgress
    {
        public static int TotalKills { get; private set; }

        /// <summary>
        /// 난이도에 쓰이는 지역 번호. **이제 진짜 지역(<see cref="RunState.Region"/>)을 그대로 돌려준다** —
        /// 런 사이클이 생기기 전엔 "누적 처치 100 = 가상 지역 레벨"로 대신했었다(그때 이 클래스가 생긴 이유).
        ///
        /// 이 프로퍼티를 없애지 않고 위임으로 바꾼 건 `EnemySpawner`가 스폰·보상 양쪽에서 참조하고 있어서다 —
        /// 호출부를 건드리지 않고 기준만 진짜 지역으로 바꾸는 게 이번 변경의 요점이다.
        /// <see cref="TotalKills"/>는 계속 세지만 이제 난이도를 정하지는 않는다(결과 화면·업적용).
        /// </summary>
        public static int RegionLv => RunState.Region;

        /// <summary>몹 1마리 처치 시 호출(`EnemySpawner.HandleEnemyDied` 참고).</summary>
        public static void RegisterKill() => TotalKills++;

        /// <summary>
        /// 새 런 시작 시 초기화용. 세이브/로드가 이번 스프린트 범위 밖이라 지금은 호출하는 곳이
        /// 없지만(인메모리 상태로 충분, `docs/sprints/03-growth-curve.md` "범위 밖" 참고), PlayMode 테스트가
        /// 정적 상태를 테스트 간에 격리하는 데도 쓴다.
        /// </summary>
        public static void Reset() => TotalKills = 0;
    }
}
