namespace ChronoSavior.Core
{
    /// <summary>
    /// 원소 상태이상(화상)을 받을 수 있는 대상. 원본 `applyElement()`(project_test.html:1699)의
    /// burn 분기에 대응한다. 출혈(bleed)은 아직 이 상태이상을 실제로 부여하는 스킬(섬영 계열)이
    /// 구현되지 않아 뺐다 — 그쪽을 구현할 때 이 인터페이스에 추가한다.
    ///
    /// `Combat`(1층)이 `Enemies`(2층)를 직접 참조할 수 없어(asmdef 계층 규칙, `IDamageable`과 같은 이유)
    /// 화상 스택 적용도 이 Core 인터페이스로만 가능하다.
    /// </summary>
    public interface IElementAfflictable
    {
        /// <summary>
        /// 화상 스택을 더한다(최대치는 구현체가 원본 `CONFIG.elem.burn.maxStacks`=10으로 캡한다).
        /// 지속시간은 매 적용마다 원본처럼 갱신된다(`e.burnT = E.life`, 누적이 아니라 리필).
        /// </summary>
        void ApplyBurn(int stacks);
    }
}
