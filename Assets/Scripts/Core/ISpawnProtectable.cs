namespace ChronoSavior.Core
{
    /// <summary>
    /// 스폰 직후 무적 상태를 노출하는 대상(원본 `spawnInvuln`). 공격 스크립트(`PlayerAttack`,
    /// `MageProjectile`)가 파괴 전에 확인한다.
    ///
    /// 이 인터페이스가 Core에 있는 이유: `Combat`/`Characters` 도메인은 `Enemies`를 직접
    /// 참조하면 안 된다(asmdef 계층 규칙, CLAUDE.md 참고) — 그런데 예전엔 `MageProjectile`/
    /// `PlayerAttack`이 `MonsterMove` 타입을 직접 `GetComponent`해서 계층을 어겼다. `EnemyMove`가
    /// 이 인터페이스만 구현하고, 공격 스크립트는 `GetComponent&lt;ISpawnProtectable&gt;()`로
    /// 구체 타입을 몰라도 되게 했다 — 태그(`Enemy`)로 대상을 고르고 이 인터페이스로 상태만 묻는
    /// 것과 같은 원칙(`IDamageable`도 나중에 이 자리에 추가될 것).
    /// </summary>
    public interface ISpawnProtectable
    {
        bool IsSpawnProtected { get; }
    }
}
