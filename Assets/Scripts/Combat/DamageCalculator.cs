using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 최종 피해량 계산. 원본 `dealDamage()`(`project_test.html:1664`)의 피해 계산식에서
    /// **이번 스프린트 범위에 해당하는 항만** 옮긴 것이다.
    ///
    /// 원본 전체 식:
    ///   max(1, round( statAtk() × dmgMultAll() × mult × vulnMult × lvF × itemDmgVs(e)
    ///                 × rebirthWallPlayerDmgMult() × rand(0.9,1.1) × (crit ? critMultOf() : 1) ))
    ///
    /// 지금 구현한 항: `statAtk × mult`(= 인자로 받는 baseDamage) · `dmgMultAll()`(콤보·살기·성소,
    /// <see cref="CombatModifiers"/>) · `rand(0.9,1.1)` · 치명타 · `max(1, round())`.
    ///
    /// **대상에 달린 항은 여기가 아니라 맞는 쪽(`Enemies/EnemyHealth`)이 곱한다** — 이 클래스는 대상
    /// 참조가 없는 순수 계산기라서다: `vulnMult`(취약) · `lvF`(레벨 페널티) 두 개가 그렇다.
    ///
    /// 아직 빠진 항(해당 시스템이 없어서): `itemMul('dmg')`·`itemDmgVs`(아이템) ·
    /// `rebirthWallPlayerDmgMult`(회귀 장벽). 생기면 `CombatModifiers.DamageMultiplier`에 곱하면 된다.
    ///
    /// 이 클래스가 `Combat`에 있는 이유: 무기에 종속되지 않는 전투 공용 인프라(CLAUDE.md 폴더 구조 규칙의
    /// "피해 판정 헬퍼")이고, `Characters`(2층)가 `Combat`(1층)을 참조할 수 있어 마법사 무기에서도 쓸 수 있다.
    /// </summary>
    public static class DamageCalculator
    {
        // 원본 CONFIG.player (`:605`) — 스탯 시스템(골드 강화·아이템)이 아직 없어 기본값 고정.
        // 스탯이 생기면 statCrit()/critMultOf()에 해당하는 값을 인자로 받도록 바꾼다.
        public const float BaseCritChance = 0.10f;   // 원본 baseCrit: 0.10
        /// <summary>기준값은 `Core.PlayerStatCalculator`가 갖고 있다 — 아이템('처형인의 각인')이
        /// 이 값을 올리는데, 그 계산이 Core에 있어서 상수도 그쪽이 원본 자리다.</summary>
        public const float CritMultiplier = PlayerStatCalculator.BaseCritMultiplier;

        // 원본 `rand(0.9, 1.1)` (`:983`, `:1664`) — 같은 공격이라도 피해가 ±10% 흔들린다.
        public const float VarianceMin = 0.9f;
        public const float VarianceMax = 1.1f;

        /// <summary>
        /// 취약 상태의 피해 배수. 원본 `vulnMult = e.vulnT > 0 ? 1.2 + itemPow('vuln') : 1`(`:1663`) —
        /// 아이템 항(`itemPow`)은 아이템 시스템이 아직 없어서 빠져 있고 기본값 1.2만 쓴다.
        ///
        /// 이 배수는 **대상이 알고 있는 값**이라 여기서 곱하지 않고 `EnemyHealth`가 피해를 받을 때 곱한다
        /// (`DamageCalculator`는 대상 참조가 없는 순수 계산기다). 원본은 곱셈을 전부 모은 뒤 한 번만
        /// 반올림하는데 우리는 굴림 → 반올림 → 배수 → 반올림이라 **드물게 1 차이가 날 수 있다** —
        /// 대상별로 굴림을 다시 하려면 10곳의 호출부가 전부 대상을 알아야 해서 그쪽이 더 나빴다.
        /// </summary>
        public const float VulnerableMultiplier = 1.2f;

        /// <summary>
        /// 최종 피해량을 굴린다. `baseDamage`는 무기 배수·차지 배수까지 이미 반영된 값을 넘긴다
        /// (예: `MageAttack`이 `baseDamage × (1 + chargeDmgMult × chargeK)`를 계산해서 전달).
        /// `critChance`를 생략하면 <see cref="BaseCritChance"/>(골드 강화 미적용 기본값)를 쓴다 —
        /// 스탯이 있는 호출자(`MageProjectile` 등)는 `Core.PlayerStatCalculator.ComputeCritChance()`를
        /// 넘겨서 강화가 반영되게 한다.
        /// </summary>
        public static int Roll(float baseDamage, float critChance, out bool isCrit)
        {
            isCrit = Random.value < critChance;
            float variance = Random.Range(VarianceMin, VarianceMax);
            // 원본 `dmgMultAll()`(project_test.html:1296) — 콤보 × 살기 × 성소.
            // 대상과 무관한 항이라 여기서 곱한다(대상에 달린 취약·레벨 페널티는 EnemyHealth가 맡는다).
            float raw = baseDamage * CombatModifiers.DamageMultiplier * variance * (isCrit ? CritMultiplier : 1f);
            return Mathf.Max(1, Mathf.RoundToInt(raw)); // 원본 max(1, round(...))
        }

        public static int Roll(float baseDamage, out bool isCrit) => Roll(baseDamage, BaseCritChance, out isCrit);

        /// <summary>치명타 여부가 필요 없을 때 쓰는 간편 버전.</summary>
        public static int Roll(float baseDamage) => Roll(baseDamage, out _);
    }
}
