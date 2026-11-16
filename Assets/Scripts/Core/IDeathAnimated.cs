namespace ChronoSavior.Core
{
    /// <summary>
    /// 죽는 순간 바로 Destroy되지 않고 자기 사망 그림을 먼저 보여주겠다는 표시
    /// (2026-11-16, 보스 '덩어리 지배자' 사망 그림 8장 — `EnemyHealth.Die()`가 그냥
    /// `Destroy(gameObject)`만 하면 그림이 한 프레임도 안 보인다).
    ///
    /// `EnemyHealth.Die()`가 `GetComponent&lt;IDeathAnimated&gt;()`로 이 인터페이스를 찾으면
    /// Destroy를 구현체에 맡기고 자신은 더 이상 손대지 않는다 — 구현하지 않는 보통 몹(고블린 등)은
    /// 지금처럼 즉시 Destroy된다(동작 변화 없음).
    /// </summary>
    public interface IDeathAnimated
    {
        /// <summary>
        /// 사망 연출을 시작한다. 호출 시점 이후 이 오브젝트의 파괴는 구현체 책임이다
        /// (연출이 끝나면 반드시 스스로 Destroy(gameObject)할 것).
        /// </summary>
        void PlayDeathAndDestroy();
    }
}
