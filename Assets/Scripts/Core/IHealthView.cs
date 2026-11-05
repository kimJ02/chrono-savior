namespace ChronoSavior.Core
{
    /// <summary>
    /// 체력 읽기 — 지금 체력과 최대 체력. 플레이어 체력(`Characters/PlayerHealth`)이 구현하고, 전투 담당(`Systems/CombatDirector`)이 전투 화면 창구
    /// (`Systems/WorldView`)에 체력을 내줄 때 읽는다(2026-11-05 씬 분할 2차 2b). `Systems`는 `Characters`를 모르므로 계약은 `Core`에 둔다
    /// — `IRunResettable`·`CombatEvents.PlayerDied`와 같은 이유.
    /// </summary>
    public interface IHealthView
    {
        float CurrentHp { get; }
        float MaxHp { get; }
    }
}
