using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 피해를 받을 수 있는 대상(플레이어·적). 원본 `dealDamage()`(적)/`damagePlayer()`(플레이어)에 대응한다.
    ///
    /// 이 인터페이스가 Core에 있는 이유는 `ISpawnProtectable`과 같다 — 공격 스크립트(`Combat`/`Characters`)가
    /// 맞는 대상의 구체 타입(`Enemies`의 클래스 등)을 몰라도 되게 하려는 것이다. asmdef 계층 규칙상
    /// `Characters`↔`Enemies`는 서로 참조할 수 없으므로, 서로 때리는 건 태그(`CompareTag`)로 대상을 고르고
    /// 이 인터페이스로 피해만 넘기는 방식으로만 가능하다(CLAUDE.md asmdef 계층표 참고).
    ///
    /// 원본과 달리 "누가 때렸는지"를 `source`로 넘기는 이유: 넉백/피격 반동 방향이 가해자 위치 기준이다
    /// (원본은 전역 `player`를 직접 참조했지만, 우리는 전역이 없으므로 명시적으로 전달한다).
    /// </summary>
    public interface IDamageable
    {
        /// <summary>
        /// 피해를 적용한다. 이미 계산이 끝난 최종 피해량을 받는다(치명타·난수 변동은 공격 쪽에서 이미 적용됨 —
        /// 원본 `dealDamage`가 공격자 스탯으로 먼저 계산한 뒤 `e.hp -= dmg`를 하는 것과 같은 순서).
        /// </summary>
        /// <param name="amount">최종 피해량(1 이상 정수 권장).</param>
        /// <param name="source">가해자 오브젝트. 넉백 방향 계산에 쓰인다. 없으면 null 허용.</param>
        void TakeDamage(float amount, GameObject source);

        /// <summary>이미 죽었는지. 원본 `e.dead` — 죽은 대상에 중복으로 피해가 들어가지 않게 공격 쪽에서 확인한다.</summary>
        bool IsDead { get; }

        /// <summary>
        /// 넉백 방향·세기를 공격 쪽이 직접 지정하는 확장 경로. 원본 `dealDamage`의 `kb`/`kbDir` 인자
        /// (project_test.html:1678-1679)에 대응한다 — 스킬마다 넉백 세기가 다르고(예: 마법탄 직격 140,
        /// 폭발 260, 중력 충돌 70~280), 방향 기준도 "가해자 위치"가 아닐 때가 있다(예: 마법탄은
        /// 자기 이동 방향 `sign(vx)`, 폭발/중력은 폭발 중심 기준). `Combat`은 `Enemies`를 참조할 수
        /// 없어(asmdef 계층 규칙) 가해자 GameObject 위치로 방향을 대신 추론시킬 수 없는 경우가 많아서
        /// 방향을 이미 계산된 부호로 직접 넘긴다.
        ///
        /// 기본 구현은 무시하고 2-인자 <see cref="TakeDamage(float, GameObject)"/>로 위임한다 —
        /// 커스텀 넉백이 의미 없는 구현체(예: 아직 피격 넉백이 없는 <c>PlayerHealth</c>)는 아무것도
        /// 바꿀 필요가 없다.
        /// </summary>
        /// <param name="knockbackDirSign">넉백 방향 부호(-1 또는 1). 0을 넘기면 안 된다(원본도 0이면 `||`로 대체 기준을 쓴다 — 호출자가 미리 대체해서 넘긴다).</param>
        /// <param name="knockbackSpeed">넉백 속도. 0이면 넉백이 없다(원본 `kb: 0` — 화상 틱·중력 틱 등).</param>
        /// <param name="hitstop">맞힌 순간 멈춤(초) — 원본 `dealDamage`의 `hitstop`/`noHitstop`(:1683). 공격마다 다르다:
        /// 기본 <see cref="HitFeel.HitstopHit"/>, 스킬 <see cref="HitFeel.HitstopSkill"/>, 없음 <see cref="HitFeel.HitstopNone"/>(2026-10-24).</param>
        /// <returns>실제로 준 피해(원본 `dealDamage`의 반환값 — 대상 배수·반올림 뒤, 못 줬으면 0). 흡혈처럼 '준 만큼'을 쓰는 쪽이 받는다(2026-11-07).
        /// **이 경로로 피해를 받는 건 적(`Enemies/EnemyHealth`)뿐이고 그쪽이 직접 구현한다** — 공격은 `Enemy` 태그로 대상을 고른다.
        /// 아래 기본 구현(요청량을 그대로 돌려줌)은 피해를 깎거나 거부하지 않는 구현체용이다 — 깎거나 거부한다면 이 메서드를 직접 구현할 것.</returns>
        float TakeDamageWithKnockback(float amount, GameObject source, float knockbackDirSign, float knockbackSpeed,
                                      float hitstop = HitFeel.HitstopHit)
        {
            TakeDamage(amount, source);
            return amount;
        }

        /// <summary>
        /// **지속 피해 틱**(화상, 불길 장판, 중력점). 넉백이 없고 **히트스톱도 걸리지 않는다** —
        /// 원본이 `dealDamage`에서 `!opts.isBurnTick`일 때만 히트스톱을 걸기 때문이다(project_test.html:1686).
        /// 콤보는 틱에서도 쌓인다(원본 `addCombo()`는 조건 없이 불린다).
        ///
        /// 틱마다 0.045초씩 화면이 멈추면 장판 위에 적이 여럿일 때 게임이 사실상 정지한다 —
        /// 원본이 굳이 구분해 둔 이유가 그것이고, 일반 피해 경로로 보내면 그 상태가 재현된다.
        /// </summary>
        void TakeTickDamage(float amount) => TakeDamage(amount, null);

        /// <summary>
        /// **무적시간을 무시하고** 피해를 강제로 적용한다 — 2지역 보스 "부서진 여신상" 잡기처럼
        /// 공격 자체가 여러 단계(경고→판정→끌어당기기→붙잡아둠)로 길게 이어지는 패턴에서 쓴다.
        /// 끌어당기기+붙잡아두기만으로도 0.7초가 걸리는데, 플레이어의 기본 피격 무적시간은 0.9초라
        /// (<c>PlayerHealth.invulnTime</c>) — 근처 다른 몹에게 막 맞은 채로 잡히면 이 공격이 주는
        /// 피해가 그 무적에 조용히 묻혀버린다(2026-11-18, 사용자가 실제 플레이로 발견한 버그).
        /// 잡는 행위 자체는 성공했는데 피해만 안 들어가는 게 더 혼란스러우므로, 이 패턴만큼은
        /// 무적을 무시하고 반드시 피해가 들어가게 한다.
        ///
        /// 기본 구현은 평범한 <see cref="TakeDamage"/>로 위임한다 — 무적 개념이 없거나(적 쪽)
        /// 무시할 필요가 없는 구현체는 아무것도 바꾸지 않아도 된다. **무적시간이 있는 구현체
        /// (`PlayerHealth`)만 이 메서드를 오버라이드해 그 검사를 건너뛴다.**
        /// </summary>
        void TakeDamageIgnoringInvuln(float amount, GameObject source) => TakeDamage(amount, source);
    }
}
