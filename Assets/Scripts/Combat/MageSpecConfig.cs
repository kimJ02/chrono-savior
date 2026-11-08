using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 마법사 스킬트리(전문화) 수치 전부 — 원본 `SPEC.bow`(project_test.html:897-914)의 두 갈래
    /// ("move"=폭발, "conv"=중력) 5티어와, 그 갈래들이 실제로 바꾸는 `bowFire`/`spawnFireExplosion`/
    /// `spawnFlameTrail`/`gravityImpactRadius`/`gravityImpactDamage`/`spawnGravityWell`/`gravityCollapse`
    /// 공식(1932~2179줄)을 그대로 옮겼다. 거리·속도 상수는 전부 100px=1유닛 축척(÷100)했다.
    ///
    /// 여기 있는 건 전부 순수 함수(상태 없음) — 실제로 Physics2D 질의를 하거나 `IDamageable`을
    /// 호출하는 부분은 <see cref="MageSkillEffects"/>에 있다. 이 클래스는 "숫자만" 담당한다.
    /// </summary>
    public static class MageSpecConfig
    {
        public static int FireTier(MageBranch branch, int tier) => branch == MageBranch.Explosion ? tier : 0;
        public static int FrostTier(MageBranch branch, int tier) => branch == MageBranch.Gravity ? tier : 0;
        public static bool IsGravityOrb(MageBranch branch, int tier) => FrostTier(branch, tier) > 0;

        /// <summary>원본 `cdBonus`(project_test.html:1941) — 두 갈래 중 하나라도 4티어 이상이면 쿨감 22%.</summary>
        public static float BowCooldownBonus(MageBranch branch, int tier) =>
            (FireTier(branch, tier) >= 4 || FrostTier(branch, tier) >= 4) ? 0.78f : 1f;

        /// <summary>원본 `dmg`(project_test.html:1946). `weaponBaseDamage`는 이미 statAtk×B.dmg가 반영된 값.</summary>
        public static float BowDamage(MageBranch branch, int tier, float chargeK, float chargeDmgMult, float weaponBaseDamage)
        {
            int fireTier = FireTier(branch, tier);
            int frostTier = FrostTier(branch, tier);
            bool gravityOrb = frostTier > 0;
            int tierBoost = Mathf.Max(fireTier, frostTier);
            float baseMult = gravityOrb ? 0.18f : (1f + chargeDmgMult * chargeK);
            return weaponBaseDamage * baseMult * (1f + tierBoost * 0.12f + (fireTier >= 4 ? 0.18f : 0f));
        }

        /// <summary>원본 `pierce`(project_test.html:1947).</summary>
        public static int BowPierce(MageBranch branch, int tier, float chargeK, int basePierce, int chargePierce)
        {
            int fireTier = FireTier(branch, tier);
            int frostTier = FrostTier(branch, tier);
            bool gravityOrb = frostTier > 0;
            bool inf = !gravityOrb && chargeK >= 0.99f && frostTier >= 5;
            if (gravityOrb) return 1;
            if (inf) return 9999;
            return basePierce + Mathf.FloorToInt(chargeK * chargePierce) + (fireTier >= 5 ? 2 : 0);
        }

        public static bool BowInfinitePierce(MageBranch branch, int tier, float chargeK) =>
            !IsGravityOrb(branch, tier) && chargeK >= 0.99f && FrostTier(branch, tier) >= 5;

        /// <summary>원본 `size`(project_test.html:1968) — 판정/시각 크기 배율.</summary>
        public static float BowSize(MageBranch branch, int tier, float chargeK) =>
            1f + chargeK * 0.9f + (FireTier(branch, tier) >= 5 ? 0.35f : 0f);

        /// <summary>원본 `life`(project_test.html:1967) — 중력탄은 차지에 따라 짧게, 무한관통이면 사실상 무한.</summary>
        public static float BowLife(MageBranch branch, int tier, float chargeK, float projectileRange, float projectileSpeed)
        {
            if (IsGravityOrb(branch, tier)) return 0.86f + chargeK * 0.22f;
            if (BowInfinitePierce(branch, tier, chargeK)) return 99f;
            return projectileRange / projectileSpeed;
        }

        public static bool BowExplosive(MageBranch branch, int tier) => !IsGravityOrb(branch, tier) && FireTier(branch, tier) > 0;

        /// <summary>원본 `explosionPower`(project_test.html:1973).</summary>
        public static float BowExplosionPower(float chargeK) => chargeK >= 0.99f ? 0.62f : chargeK >= 0.5f ? 0.42f : 0.26f;

        // ---- 화염 폭발 (spawnFireExplosion, project_test.html:1978) ----
        public static float FireExplosionRadius(int tier) => 0.96f + tier * 0.15f + (tier >= 5 ? 0.40f : 0f);
        public static float FireExplosionDamageMult(int tier, float power) => (0.95f + tier * 0.24f) * power;
        public static int FireExplosionBurnStacks(int tier) => tier >= 5 ? 4 : 2;

        // ---- 불길 이동 궤적 (spawnFlameTrail, project_test.html:1995) ----
        public static float FlameTrailRadius(int tier) => 0.58f + tier * 0.08f;
        public static float FlameTrailLife(int tier) => 1.6f + tier * 0.22f + (tier >= 4 ? 0.8f : 0f);
        public const float FlameTrailTickInterval = 0.32f;
        public static float FlameTrailDamageMult(int tier) => 0.065f + tier * 0.018f;
        public static int FlameTrailBurnStacks(int tier) => tier >= 5 ? 2 : 1;

        // ---- 불길 이동(X 스킬, mageFireTeleport, project_test.html:2132) ----
        public const float TeleportDistanceX = 3.3f;
        public const float TeleportDistanceY = 2.1f;
        public const float TeleportInvuln = 0.22f;
        public static int TeleportTrailPoints(int tier) => tier >= 4 ? 6 : 4;
        public const float TeleportTrailPower = 1.05f;
        /// <summary>도착 X는 맵 좌우 끝에서 이만큼 안쪽까지 — 원본 `clamp(toX, 28, mapW - 28)`.</summary>
        public const float TeleportWallInset = 0.28f;
        /// <summary>불길 줄 높이 — 원본 `spawnFlameLine(p.x, p.y - 28, …, p.y - 28)`, 출발·도착 모두 발에서 0.28 위.</summary>
        public const float TeleportFlameLineHeight = 0.28f;

        // ---- 중력 충돌(폭발 지점 판정, gravityImpactRadius/Damage, project_test.html:2017) ----
        public static float GravityImpactRadius(int tier, float chargeK) => 0.86f + tier * 0.10f + (tier >= 3 ? 0.18f : 0f) + chargeK * 0.34f;
        public static float GravityImpactDamageMult(int tier, float chargeK) => 0.32f + tier * 0.055f + chargeK * 0.18f;
        public const float GravityImpactKnockback = 0.70f; // 원본 kb:70(project_test.html:2036)

        // ---- 중력점 (spawnGravityWell, project_test.html:2044) ----
        public static int GravityWellLimit(int tier) => tier >= 5 ? 5 : tier >= 3 ? 3 : 1;
        public static int GravityStackGain(int tier, float chargeK) =>
            tier < 2 ? 1 : (chargeK >= 0.99f ? 3 : chargeK >= 0.5f ? 2 : 1);
        public const float GravityWellMergeRadiusBase = 0.86f;
        public const float GravityWellMergeRadiusPerStack = 0.12f;
        public static float GravityWellLife(int tier, float chargeK) => 2.0f + tier * 0.18f + (tier >= 4 ? 0.7f : 0f) + chargeK * 0.4f;
        public static float GravityWellRadius(int tier, int stack, float chargeK) => 1.18f + tier * 0.14f + stack * 0.22f + chargeK * 0.28f;
        public static float GravityWellPull(int tier, int stack, float chargeK) => 2.10f + tier * 0.45f + stack * 0.80f + chargeK * 0.90f;
        public const float GravityGroundClearance = 0.20f; // 원본 y = min(y, groundY-20px)

        // ---- 중력점 지속 효과(끌어당김·틱 피해, project_test.html:3806-3843) ----
        public static float GravityPullRangeExtra(int stack) => stack * 0.18f; // pullR = z.r + stack*18px
        public static float GravityDamageTickInterval(int stack) => stack >= 4 ? 0.28f : 0.34f;
        public static float GravityTickDamageMult(int tier, int stack, float nearBonus) => 0.045f + tier * 0.012f + stack * 0.012f + nearBonus;

        // ---- 중력 취약 (gravityExpose → vulnT, project_test.html:3817-3819) ----
        /// <summary>노출 누적 속도. 원본 `dt * (1 + stack * 0.16)` — 중첩이 클수록 빨리 취약해진다.</summary>
        public const float GravityExposureRatePerStack = 0.16f;
        /// <summary>이 누적치를 넘으면 취약이 걸린다. 원본 `gravityExpose >= 1.25`.</summary>
        public const float GravityVulnerableThreshold = 1.25f;
        /// <summary>노출은 초당 이만큼 빠진다. 원본 `e.gravityExpose = Math.max(0, e.gravityExpose - dt * 0.35)`(:1727).</summary>
        public const float GravityExposureDecayPerSecond = 0.35f;
        /// <summary>취약 지속시간. 원본 `Math.max(e.vulnT, 3.0)`.</summary>
        public const float VulnerableDuration = 3.0f;

        // ---- 대붕괴 (gravityCollapse, X 스킬, project_test.html:2081) ----
        public const float CollapseSearchHalfWidth = 7.6f;
        public const float CollapseSearchHalfHeight = 4.2f;
        public const float CollapseFallbackDistance = 1.2f;
        public static float CollapseRadius(int tier, int stack) => 1.50f + tier * 0.22f + stack * 0.18f;
        public static float CollapseTightAvgDist(int stack) => 0.86f + stack * 0.10f;
        public static bool CollapseIsTight(int count, float avgDist, int stack) => count >= 3 && avgDist < CollapseTightAvgDist(stack);
        public static bool CollapseIsCritical(int tier, int count, bool tight, int stack) =>
            tier >= 2 && (count >= 5 || tight || (tier >= 5 && stack >= 3 && count >= 2));
        public static float CollapseMassBonus(int count, int stack, bool critical) =>
            Mathf.Max(0, count - 1) * 0.18f + stack * 0.22f + (critical ? 0.75f : 0f);
        public static float CollapseDamageMult(int tier, float massBonus, int burstIndex) =>
            (0.78f + tier * 0.16f) * (1f + massBonus) * (burstIndex == 0 ? 1f : 0.72f);
        public static float CollapseKnockback(bool critical) => critical ? 2.80f : 1.70f;
        public static float CollapseCriticalRingMult(int tier) => tier >= 5 ? 1.38f : 1.18f;
        public const float CollapseCriticalRingDamageMult = 0.58f;
        public const float CollapseCriticalRingKnockback = 2.40f;
        /// <summary>폭발 고리 그림 수명(원본 `life: 0.42 + idx * 0.04` :2114, 바깥 고리 `life: 0.58` :2123).</summary>

        // ---- X 스킬 쿨다운 (tryMageSkill, project_test.html:2177) ----
        public const float UltBaseCooldown = 30f; // 원본 CONFIG.ult.cd
        public static float UltCooldown(MageBranch branch, int tier) =>
            UltBaseCooldown * (branch == MageBranch.Explosion ? 0.34f : 0.28f) * (tier >= 4 ? 0.68f : 1f);
    }
}
