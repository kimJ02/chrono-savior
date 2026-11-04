namespace ChronoSavior.Core
{
    /// <summary>
    /// **취약** 상태가 될 수 있는 대상. 원본 `e.vulnT`(project_test.html:1663·:1726·:3818)에 대응한다.
    ///
    /// 취약해진 적은 **받는 피해가 1.2배**가 된다(`vulnMult`, `:1663`). 거는 경로는 두 가지인데
    /// 지금 구현된 건 중력점 노출 쪽 하나다:
    /// - **중력점에 오래 노출**(`gravityExpose >= 1.25`) → 취약 3초 (`:3817`~`:3819`)
    /// - 아이템 "균열의 각인"(`itemPow('vuln')`, `:1677`) — 아이템 시스템이 아직 없어 범위 밖
    ///
    /// `Combat`(1층)이 `Enemies`(2층)를 직접 참조할 수 없어(asmdef 계층 규칙) 중력점 스크립트는
    /// 이 Core 인터페이스로만 취약을 걸 수 있다 — <see cref="IDamageable"/>·<see cref="IElementAfflictable"/>와 같은 이유.
    /// </summary>
    public interface IVulnerable
    {
        /// <summary>지금 취약 상태인지. 피해 계산이 1.2배를 곱할지 판단하는 데 쓴다.</summary>
        bool IsVulnerable { get; }

        /// <summary>
        /// 중력점에 노출된 시간을 누적한다. 임계치를 넘으면 **구현체가** 취약을 건다.
        ///
        /// ⚠️ 원본의 `gravityExpose`는 **줄어들지 않는다** — 스폰 시 0으로 시작해서 중력점 안에 있는
        /// 동안 계속 더해지기만 한다(`:3817`). 즉 한 번 임계치를 넘긴 적은 그 뒤로 중력점에 닿을
        /// 때마다 곧바로 다시 취약해진다. 감쇠를 넣으면 중력 빌드의 후반 체감이 달라진다.
        /// </summary>
        void AddGravityExposure(float exposure);

        /// <summary>취약을 직접 건다(남은 시간을 줄이지는 않는다 — 원본 `Math.max(vulnT, 3.0)`).</summary>
        void ApplyVulnerable(float duration);
    }
}
