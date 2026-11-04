namespace ChronoSavior.Core
{
    /// <summary>
    /// 새 사냥(런)이 시작될 때 초기 상태로 되돌아가야 하는 것. 원본 `resetPlayerForRun()`
    /// (project_test.html:1511 — 위치·속도·체력·무적·쿨다운을 전부 되돌린다)에 대응한다.
    ///
    /// `Systems`(3층)가 `Characters`(2층)의 구체 타입을 몰라도 리셋을 시킬 수 있게 Core에 둔다
    /// — `IDamageable`/`ISpawnProtectable`과 같은 이유(CLAUDE.md asmdef 계층표).
    /// 런 시작 시 `RunController`가 플레이어 오브젝트의 모든 구현체를 찾아 호출한다.
    /// </summary>
    public interface IRunResettable
    {
        void ResetForRun();
    }
}
