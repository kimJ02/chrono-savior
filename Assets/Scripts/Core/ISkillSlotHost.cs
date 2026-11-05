namespace ChronoSavior.Core
{
    /// <summary>
    /// 지금 캐릭터의 스킬 칸을 내주는 쪽 — 키트를 고르는 `Characters/PlayerRig`가 구현한다(지금 켜진 키트의 <see cref="ISkillSlotSource"/>).
    /// 전투 담당(`Systems/CombatDirector`)이 전투 화면 창구에 Z·X 칸과 버프 줄을 내줄 때 읽는다(2026-11-05 2b). `Systems`는 `Characters`를 모르므로 `Core`에 둔다.
    /// </summary>
    public interface ISkillSlotHost
    {
        /// <summary>지금 켜진 키트의 칸 — 그 키트가 칸을 안 내주면 null.</summary>
        ISkillSlotSource CurrentSkillSlots { get; }
    }
}
