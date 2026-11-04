using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    // 갈래 enum(`GunnerBranch`)은 세이브에 들어가야 해서 Core(`PlayerProfile.cs`)에 있다.

    /// <summary>
    /// 메카닉 스킬트리(전문화) 수치 전부 — 원본 `SPEC.gunner`(project_test.html:915-932)의 두 갈래 5티어와,
    /// 그 갈래들이 실제로 바꾸는 `gunnerFire`(:2188)·`updateGunnerBeam`(:2224)·`placeGunnerTurret`(:2349)·
    /// `summonLaserDrone`(:2363)·`tryGunnerSkill`(:2390)·설치기 장판(`updateZones` :3766)·
    /// 스택(`gunnerStackMax` :1321, `addGunnerStack` :1411) 공식을 그대로 옮겼다.
    /// 거리·속도 상수는 전부 100px=1유닛 축척(÷100)했다.
    ///
    /// ⚠️ **원본 SPEC의 설명문(ds)과 실제 코드가 다른 곳은 코드를 따랐다**(원본 게임이 실제로 그렇게 동작하므로):
    /// - 설치기 1층도 최대 3개·링크·내부 장판이 전부 된다(`gunnerTurretMax`가 1층부터 3) — 설명문은 "2층부터".
    /// - 충전 스택은 1층부터 드론 지속시간으로 바뀐다(`consumeGunnerStacks`가 티어 조건 없음) — 설명문은 "2층".
    ///
    /// 여기 있는 건 전부 순수 함수(상태 없음)다 — 실제 판정·피해는 <see cref="GunnerAttack"/>·
    /// <see cref="GunnerLaserBeam"/>·<see cref="GunnerDrone"/>·<see cref="GunnerTurret"/>·<see cref="GunnerField"/>에 있다.
    /// </summary>
    public static class GunnerSpecConfig
    {
        /// <summary>원본 `curTier('gunner', branch)` — 그 갈래를 고른 상태일 때만 티어가 잡힌다.</summary>
        public static int LaserTier(GunnerBranch branch, int tier) => branch == GunnerBranch.Laser ? tier : 0;
        public static int InstallerTier(GunnerBranch branch, int tier) => branch == GunnerBranch.Installer ? tier : 0;

        // ---- Z 총알 (gunnerFire, project_test.html:2188) — laserTier = 레이저 갈래 티어(아니면 0) ----
        /// <summary>원본 `(laserTier ? (laserTier >= 4 ? 0.18 : 0.22) : 0.24)` — 여기에 ÷statAs×cdMult가 붙는다.</summary>
        public static float BulletBaseCooldown(int laserTier) => laserTier > 0 ? (laserTier >= 4 ? 0.18f : 0.22f) : 0.24f;
        public static float BulletDamageMult(int laserTier) => laserTier > 0 ? 0.66f + laserTier * 0.08f : 0.82f;
        public static int BulletPierce(int laserTier) => laserTier > 0 ? 2 + laserTier / 2 : 1;
        public static float BulletLife(int laserTier) => laserTier > 0 ? 0.7f : 0.62f;
        public static float BulletSize(int laserTier) => laserTier > 0 ? 1.18f + laserTier * 0.05f : 1f;
        public static bool BulletHoming(int laserTier) => laserTier >= 5;
        /// <summary>원본 `430 + laserTier*45` px ÷100.</summary>
        public static float BulletSeekRange(int laserTier) => laserTier >= 5 ? (430f + laserTier * 45f) / 100f : 0f;
        public static float BulletTurnRate(int laserTier) => laserTier >= 5 ? 9f + laserTier * 1.4f : 0f;
        /// <summary>원본 `stackOnHit` 기본값 1(:2213) — 갈래가 없으면 <see cref="GunnerAttack.AddStack"/>가 무시한다.</summary>
        public const int BulletStackOnHit = 1;

        // ---- 레이저 빔 (updateGunnerBeam, project_test.html:2224) — Z를 누르고 있는 동안 ----
        public const float BeamOriginForward = 0.26f;   // 원본 ox = p.x + facing*26
        public const float BeamOriginHeight = 0.38f;    // 원본 oy = p.y - 38 — p.y는 발, Y+가 아래라 부호를 뒤집음
        /// <summary>원본 `(tier >= 5 ? 520 : 360 + tier*40) * 0.8` px ÷100.</summary>
        public static float BeamRange(int tier) => (tier >= 5 ? 520f : 360f + tier * 40f) * 0.8f / 100f;
        /// <summary>유도 탐지 거리 — 5층은 사거리의 1.5배(`seekR`).</summary>
        public static float BeamSeekRange(int tier) => tier >= 5 ? BeamRange(tier) * 1.5f : BeamRange(tier);
        /// <summary>유도 콘 반각(rad). 5층 0.6109(≈35°, 총 70°) · 4층 0.44 · 3층 0.26.</summary>
        public static float BeamConeHalfAngle(int tier) => tier >= 5 ? 0.6109f : tier >= 4 ? 0.44f : 0.26f;
        public static bool BeamHoming(int tier) => tier >= 3;
        public static bool BeamCurved(int tier) => tier >= 5;
        /// <summary>원본 `aheadDist = clamp(len*0.45, 60, 260)` px ÷100.</summary>
        public static float BeamCurveAhead(float len) => Mathf.Clamp(len * 0.45f, 0.60f, 2.60f);
        public const float BeamCurveOvershoot = 0.50f;  // 원본 overshoot 50
        public const float BeamCurveEaseRate = 16f;     // 원본 ease = min(1, dt*16)
        public const int BeamCurveSamples = 14;         // 원본 곡선 판정 분할 수
        /// <summary>콘 안에 적이 없을 때 빔이 짧아지는 비율(`range *= 0.55`).</summary>
        public const float BeamIdleRangeMult = 0.55f;
        public const float BeamIdleSwayFreq = 4.5f;     // 원본 sin(state.time*4.5)
        public const float BeamIdleSwayAmp = 0.06f;     // rad
        /// <summary>|dx| 가 이보다 크면 빔이 캐릭터 방향을 목표 쪽으로 돌린다(원본 `if (Math.abs(dx) > 0.15) p.facing = sign(dx)`).</summary>
        public const float BeamFacingTurnThreshold = 0.15f;
        /// <summary>원본 `width = 14 + tier*1.6 + (tier >= 5 ? 6 : 0)` px ÷100.</summary>
        public static float BeamWidth(int tier) => (14f + tier * 1.6f + (tier >= 5 ? 6f : 0f)) / 100f;
        /// <summary>판정 여유에 더하는 적 폭 비율(`width + e.w*0.35`).</summary>
        public const float BeamEnemyWidthFactor = 0.35f;
        /// <summary>원본 `(tier >= 5 ? 0.085 : tier >= 4 ? 0.105 : 0.13) / statAs()` — **cdMult는 안 곱한다**.</summary>
        public static float BeamBaseTick(int tier) => tier >= 5 ? 0.085f : tier >= 4 ? 0.105f : 0.13f;
        public static float BeamDamageMult(int tier) => (0.16f + tier * 0.028f) * (tier >= 5 ? 1.18f : 1f);

        // ---- 충전 스택 / 부품 (gunnerStackMax :1321, addGunnerStack :1411) ----
        /// <summary>
        /// 원본 `gunnerStackMax()` — 설치기 갈래면 동시 설치 한도(=부품 최대치), 아니면 `6 + tier*3 + (tier >= 4 ? 4 : 0)`.
        /// (갈래가 없으면 tier=0 → 6이지만, 그 상태에선 스택이 아예 안 쌓인다.)
        /// </summary>
        public static int StackMax(GunnerBranch branch, int tier)
        {
            int t = branch != GunnerBranch.None ? tier : 0;
            if (branch == GunnerBranch.Installer) return TurretMax(t);
            return 6 + t * 3 + (t >= 4 ? 4 : 0);
        }
        /// <summary>설치기 갈래: 공격 적중 10회마다 부품 1개(`gunnerPartHits >= 10`).</summary>
        public const int PartHitsPerPart = 10;

        // ---- X 스킬 쿨다운 (tryGunnerSkill, project_test.html:2390) ----
        /// <summary>원본 `CONFIG.ult.cd`(:686) — 마법사(`MageSpecConfig.UltBaseCooldown`)와 같은 30초 기준값.</summary>
        public const float UltBaseCooldown = 30f;
        /// <summary>레이저 드론: `CONFIG.ult.cd * 0.34 * (tier >= 4 ? 0.72 : 1)` — 여기에 cdMult가 붙는다.</summary>
        public static float DroneUltCooldown(int tier) => UltBaseCooldown * 0.34f * (tier >= 4 ? 0.72f : 1f);
        /// <summary>설치기: `0.55 - min(0.18, tier*0.03)` — 여기에 cdMult가 붙는다.</summary>
        public static float TurretUltCooldown(int tier) => 0.55f - Mathf.Min(0.18f, tier * 0.03f);

        // ---- 레이저 드론 (summonLaserDrone :2363, updateZones laserDrone :3872) ----
        public static int DroneCount(int tier) => tier >= 5 ? 2 : 1;
        /// <summary>원본 `4.0 + tier*0.45 + stackSpend*0.32 + (tier >= 4 ? 1.0 : 0)`.</summary>
        public static float DroneLife(int tier, int stackSpend) => 4.0f + tier * 0.45f + stackSpend * 0.32f + (tier >= 4 ? 1.0f : 0f);
        public static float DroneFireInterval(int tier) => tier >= 4 ? 0.32f : 0.42f;
        /// <summary>두 번째 드론은 사격 타이머를 0.12초 앞당겨 시작한다(`tickT: i*0.12`).</summary>
        public const float DroneFirePhaseStep = 0.12f;
        /// <summary>원본 `420 + tier*55` px ÷100.</summary>
        public static float DroneRange(int tier) => (420f + tier * 55f) / 100f;
        public static float DroneDamageMult(int tier) => 0.7f + tier * 0.13f;
        public const float DroneRescanInterval = 0.18f;          // 원본 scanT = 0.18
        // 소환 위치: x = p.x - facing*42 + side*28, y = p.y - 78 - i*10
        public const float DroneSpawnBack = 0.42f;
        public const float DroneSpawnSide = 0.28f;
        public const float DroneSpawnHeight = 0.78f;
        public const float DroneSpawnHeightStep = 0.10f;
        // 따라다니기: x = player.x - facing*56 + side*42, y = player.y - 86 + sin(time*5 + side)*9, lerp(dt*4.2)
        public const float DroneFollowBack = 0.56f;
        public const float DroneFollowSide = 0.42f;
        public const float DroneFollowHeight = 0.86f;
        public const float DroneBobFreq = 5f;
        public const float DroneBobAmp = 0.09f;
        public const float DroneFollowRate = 4.2f;
        // 드론 탄 (updateZones laserDrone :3897)
        /// <summary>원본 `1240 + tier*95` px/s ÷100.</summary>
        public static float DroneBulletSpeed(int tier) => (1240f + tier * 95f) / 100f;
        public const float DroneBulletLife = 0.62f;
        public static int DroneBulletPierce(int tier) => tier >= 5 ? 3 : 2;
        public static float DroneBulletSize(int tier) => tier >= 5 ? 1.45f : 1.12f;
        public static bool DroneBulletHoming(int tier) => tier >= 4;
        public static float DroneBulletTurnRate(int tier) => 11f + tier;

        // ---- 설치기 (placeGunnerTurret :2349, updateZones turret :3858) ----
        /// <summary>원본 `gunnerTurretMax()` — 1~3층 3개, 4층 4개, 5층 5개(0층 0).</summary>
        public static int TurretMax(int tier) => tier <= 0 ? 0 : tier >= 5 ? 5 : tier >= 4 ? 4 : 3;
        public const float TurretPlaceForward = 0.34f;   // 원본 x = p.x + facing*34
        public const float TurretEdgeMargin = 0.36f;     // 원본 clamp(x, 36, mapW - 36)
        /// <summary>5층은 사실상 영구(원본 9999초).</summary>
        public static float TurretLife(int tier) => tier >= 5 ? 9999f : 5.4f + tier * 0.65f + (tier >= 4 ? 2.4f : 0f);
        public static float TurretTick(int tier) => tier >= 5 ? 0.28f : tier >= 3 ? 0.38f : 0.48f;
        /// <summary>원본 `118 + tier*18` px ÷100.</summary>
        public static float TurretRange(int tier) => (118f + tier * 18f) / 100f;
        public static float TurretDamageMult(int tier) => 0.25f + tier * 0.07f;
        /// <summary>설치기 피해 판정 중심 높이 — 원본 `z.y - 18`.</summary>
        public const float TurretCenterHeight = 0.18f;

        // ---- 설치기 링크 · 내부 장판 (mechanicLinkMax :1327, mechanicFieldPoints :1392, updateZones :3766) ----
        /// <summary>원본 `300 + tier*70 + (tier >= 4 ? 90 : 0) + (tier >= 5 ? 120 : 0)` px ÷100.</summary>
        public static float LinkMax(int tier) =>
            tier <= 0 ? 0f : (300f + tier * 70f + (tier >= 4 ? 90f : 0f) + (tier >= 5 ? 120f : 0f)) / 100f;
        /// <summary>장판 꼭짓점 높이 — 원본 `z.y - 24`.</summary>
        public const float FieldPointHeight = 0.24f;
        /// <summary>앞 세 꼭짓점의 삼각형 넓이가 이보다 작으면(거의 일직선) 가운데 점을 들어 올린다. 원본 800px² ÷10000.</summary>
        public const float FieldFlatTriangleArea = 0.08f;
        /// <summary>들어 올리는 높이 — 원본 `min(pts[0].y, pts[2].y) - 120`.</summary>
        public const float FieldLiftHeight = 1.20f;
        public static float FieldTick(int tier) => tier >= 5 ? 0.28f : 0.38f;
        /// <summary>링크 판정 폭 — 원본 `30 + tier*3` px ÷100에 적 폭 × 0.25를 더한다.</summary>
        public static float LinkHitWidth(int tier) => (30f + tier * 3f) / 100f;
        public const float LinkEnemyWidthFactor = 0.25f;
        /// <summary>원본 `0.22 + tier*0.05 + (링크 위 0.08) + (내부 0.06)`.</summary>
        public static float FieldDamageMult(int tier, bool onLink, bool insideField) =>
            0.22f + tier * 0.05f + (onLink ? 0.08f : 0f) + (insideField ? 0.06f : 0f);
    }
}
