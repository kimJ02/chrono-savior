namespace ChronoSavior.Core
{
    /// <summary>
    /// 캐릭터 몸 그림의 자세(2026-11-17 애니메이션 플립북 전환). 우선순위는 위가 이긴다:
    /// <see cref="Hit"/> &gt; <see cref="Attack"/> &gt; <see cref="Charge"/> &gt; <see cref="Rise"/>/<see cref="Fall"/> &gt; <see cref="Move"/> &gt; <see cref="Idle"/>.
    /// 루프 자세(Idle·Move·Rise·Fall·Charge)는 매 프레임 상태에서 정하고, Hit·Attack은 한 번 재생(또는 유지)이다.
    /// </summary>
    public enum CharacterPose { Idle, Move, Rise, Fall, Charge, Attack, Hit }
}
