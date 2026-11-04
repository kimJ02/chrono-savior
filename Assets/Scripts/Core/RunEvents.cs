using System;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 런 진행 중에 일어나는 "장면 전환급" 사건을 전달하는 이벤트 버스.
    ///
    /// ## 왜 이벤트인가
    /// 보스 포탈은 필드에 서 있는 구조물이라 `World`(1층)에 있는데, 실제로 무대를 갈아끼우고
    /// 타이머를 바꾸는 주체는 `Systems.CombatDirector`(3층)다. 낮은 층이 높은 층을 직접 참조하면
    /// 두 asmdef가 서로를 참조해 **컴파일 자체가 거부된다**(CLAUDE.md "낮은 층이 높은 층의 기능을
    /// 요청" 항목). 그래서 방향을 뒤집어 `Core`의 이벤트만 아래층이 부르게 한다 —
    /// <see cref="EnemySpawnRequestBus"/>와 같은 구조다.
    ///
    /// ## 요청과 통보를 나눈 이유
    /// <see cref="BossPortalRequested"/>는 "보내 달라"는 **요청**이고
    /// <see cref="BossFieldEntered"/>는 "실제로 옮겨졌다"는 **통보**다. 둘을 하나로 합치면
    /// 포탈이 직접 스포너를 깨우는 셈이 되어, 입장료·잠금 확인 같은 판단이 여러 곳으로 흩어진다.
    /// 판단은 `CombatDirector` 한 곳에서만 하고, 결과만 퍼뜨린다.
    ///
    /// ## 런 종료 요청(2026-11-05)
    /// 런을 끝낼 사건(사망·시간 초과·보스 격파 뒤 지연)은 전투 씬의 `CombatDirector`가 알아채지만, 화면을 결과로 넘기고 시간을 멈추는 건
    /// 상주 씬의 `GameFlow`다. 둘은 씬이 달라 서로를 직렬화로 못 잇고, **위(흐름)가 아래(전투)를 직접 부르고 아래는 이벤트로만 알린다**
    /// (`HANDOFF.md` "씬 분할 2차" 1단계) — 그래서 <see cref="RunEndRequested"/>.
    ///
    /// 구독자는 **반드시 `OnDisable`에서 해제**할 것. 정적 이벤트라 해제를 빠뜨리면 파괴된
    /// 오브젝트를 계속 참조해 `MissingReferenceException`이 난다.
    /// </summary>
    public static class RunEvents
    {
        /// <summary>보스 포탈에 입장을 시도했다(`World.BossPortal` → `Systems.CombatDirector`).</summary>
        public static event Action BossPortalRequested;

        /// <summary>보스 필드로 실제 전환됐다(`CombatDirector` → 스포너·HUD 등).</summary>
        public static event Action BossFieldEntered;

        /// <summary>
        /// 지역 보스를 격파했다(`Systems.EnemySpawner` → `CombatDirector`).
        /// 원본은 격파 1.6초 뒤에 `endRun('bossdead')`을 부른다(project_test.html:4289) —
        /// 그 지연을 주는 주체가 `CombatDirector`라 이벤트로 넘긴다.
        /// </summary>
        public static event Action BossDefeated;

        /// <summary>런을 끝내 달라(`CombatDirector` → `GameFlow.EndRun`). 원본 `endRun(reason)`을 부르던 자리들.</summary>
        public static event Action<RunEndReason> RunEndRequested;

        public static void RequestBossPortal() => BossPortalRequested?.Invoke();
        public static void RaiseBossFieldEntered() => BossFieldEntered?.Invoke();
        public static void RaiseBossDefeated() => BossDefeated?.Invoke();
        public static void RequestRunEnd(RunEndReason reason) => RunEndRequested?.Invoke(reason);

        /// <summary>테스트 격리용 — 정적 이벤트라 구독이 남으면 테스트끼리 오염된다.</summary>
        public static void Reset()
        {
            BossPortalRequested = null;
            BossFieldEntered = null;
            BossDefeated = null;
            RunEndRequested = null;
        }
    }
}
