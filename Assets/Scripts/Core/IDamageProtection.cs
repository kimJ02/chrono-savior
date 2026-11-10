namespace ChronoSavior.Core
{
    /// <summary>
    /// 지금 **피해를 받지 않는** 상태인지 알려 주는 몹 부품(스폰 보호와 다른, 패턴 중 무적) — 예: 덩어리 지배자가 공중에 뜬 동안
    /// (`Enemies/BlobLordBoss`, 2026-11-10 명세 "도약 중 무적").
    ///
    /// `EnemyMove.IsSpawnProtected`(공격 스크립트 10곳과 `EnemyHealth`가 확인하는 "지금 못 때림" 값)가 이 값을 함께 본다 —
    /// 그래서 공격 쪽을 고치지 않아도 피해도 안 들어가고 관통 횟수도 안 닳는다. 스폰 보호의 반투명 연출·행동 정지와는 무관하다
    /// (그건 스폰 타이머만 본다).
    /// </summary>
    public interface IDamageProtection
    {
        bool IsDamageProtected { get; }
    }
}
