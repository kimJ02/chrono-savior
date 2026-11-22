using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 환산 전투력(메이플 "환산 주스탯"처럼 강함을 숫자 하나로) + 스테이지 권장 전투력 — 2026-11-22 사용자 요청, GPT와 정함.
    ///
    /// - 전투력 = 10,000 × √(기대 DPS 비 × 유효 체력 비) × √레벨. 기준(레벨 1·강화·아이템 없음) = 10,000.
    ///   기대 DPS = 공격력 × 공속 × 기대 치명 배율 × 피해 배율 × 쿨감 보정(스킬 몫 30%만 쿨감을 받는다고 본다).
    ///   유효 체력 = 최대 체력 ÷ 받는 피해 배율 × √이동속도(0.5~2배로 자름).
    ///   **사냥 중에만 바뀌는 것(살기·콤보·유리 서약·성소)은 넣지 않는다**, 스탯 아이템(items.Add/Mul)은 넣는다. 유물 고유 효과(발동형)는 숫자로 못 바꿔 빠진다.
    /// - 권장 전투력 = 10,000 × max(그 스테이지 몹 체력 배율, 몹 공격력 배율) — "기준 몹을 10대에 잡고 6대를 버티는" 힘을 그 스테이지에서도.
    ///   5스테이지는 그 스테이지 몹 기준(중간 보스는 따로 — 넣으면 1-5가 2-1보다 훨씬 높아졌다). 보스 스테이지는 다음 챕터 첫 단계 × 보스 한 단계.
    /// - 색: 내 전투력 ÷ 권장이 1 이상 초록, 0.7 이상 노랑, 그 아래 빨강(<see cref="Grade"/>).
    /// </summary>
    public static class CombatPower
    {
        public const int ReferencePower = 10_000;
        const float SkillDamageShare = 0.30f;

        public static int Of(PlayerProfile p)
        {
            if (p == null) return 0;
            // 강화 미리보기와 같은 계산(사냥 중 배수 없음) — 지금 강화 단계 그대로
            float atk = PlayerStatCalculator.StatAtUpgradeLevel(p, UpgradeStat.Atk, p.upgrades.atk);
            float hp = PlayerStatCalculator.StatAtUpgradeLevel(p, UpgradeStat.Hp, p.upgrades.hp);
            float ms = PlayerStatCalculator.StatAtUpgradeLevel(p, UpgradeStat.Ms, p.upgrades.ms);
            // 공속 계산에는 사냥 중 살기 배수가 들어 있다 — 덜어 낸다
            float aspd = PlayerStatCalculator.StatAtUpgradeLevel(p, UpgradeStat.AtkSpeed, p.upgrades.atkSpeed)
                         / Mathf.Max(0.01f, CombatModifiers.AttackSpeedMultiplier);
            float crit = Mathf.Clamp01(PlayerStatCalculator.StatAtUpgradeLevel(p, UpgradeStat.Crit, p.upgrades.crit));
            float critMult = PlayerStatCalculator.ComputeCritMultiplier(p);
            float cd = PlayerStatCalculator.ComputeCooldownMultiplier(p);
            float taken = PlayerStatCalculator.ComputeDamageTakenMultiplier(p);
            float dmgMult = p.items.Mul(ItemStat.Dmg);

            float cdBenefit = 1f - SkillDamageShare + SkillDamageShare / Mathf.Max(0.01f, cd);
            float dps = atk * aspd * (1f + crit * (critMult - 1f)) * dmgMult * cdBenefit;
            float ehp = hp / Mathf.Max(0.01f, taken) * Mathf.Sqrt(Mathf.Clamp(ms, 0.5f, 2f));

            const float refDps = PlayerStatCalculator.BaseAtk
                * (1f + PlayerStatCalculator.BaseCritChance * (PlayerStatCalculator.BaseCritMultiplier - 1f));
            float ratio = Mathf.Sqrt(Mathf.Max(0f, dps / refDps) * Mathf.Max(0f, ehp / PlayerStatCalculator.BaseMaxHp));
            float levelFactor = Mathf.Sqrt(Mathf.Max(1, p.level)); // 레벨 = 스킬 성장을 대신하는 완만한 보정
            return Mathf.Max(0, Mathf.RoundToInt(ReferencePower * ratio * levelFactor));
        }

        /// <summary>그 스테이지 권장 전투력(잘못된 번호면 0).</summary>
        public static int RecommendedFor(int chapter, int stage)
        {
            if (chapter < 1 || chapter > ChapterConfig.ChapterCount || stage < 1 || stage > ChapterConfig.BossStage) return 0;
            float hpP = DifficultyScalingConfig.ScaledHp(1f, chapter) * ChapterConfig.HpStageMult(stage);
            float dmgP = DifficultyScalingConfig.ScaledDmg(1f, chapter) * ChapterConfig.DmgStageMult(stage);
            if (stage == ChapterConfig.BossStage) { hpP *= ChapterConfig.BossHpMult; dmgP *= ChapterConfig.BossDmgMult; }
            return Mathf.Max(1, Mathf.RoundToInt(ReferencePower * Mathf.Max(hpP, dmgP)));
        }

        /// <summary>내 전투력 ÷ 권장 — 2 = 충분(초록), 1 = 빠듯(노랑), 0 = 부족(빨강).</summary>
        public static int Grade(int mine, int recommended)
        {
            if (recommended <= 0) return 2;
            float r = mine / (float)recommended;
            return r >= 1f ? 2 : r >= 0.7f ? 1 : 0;
        }
    }
}
