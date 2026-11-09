namespace ChronoSavior.Combat
{
    /// <summary>
    /// 라이선스 이펙트 그림의 **키** — `Resources/Vfx/<키>/frame_000…`(git 밖, `HANDOFF.md` 0-3).
    /// 규칙 `<캐릭터>/<효과>[_<부위·상태>][_<크기>]`, 소문자 snake_case. 키 폴더 하나 = 프레임 시퀀스 하나.
    /// 어느 팩의 어느 프레임을 이 키로 쓰는지는 에디터 도구 `Editor/VfxPackImporter`의 목록이 정한다(그 목록은 이 상수를 쓴다 — 키가 어긋나면 컴파일이 안 된다).
    /// 그림이 없으면(팩을 안 가져온 클론) <see cref="VfxFrames.Get"/>가 빈 배열을 주고, 쓰는 쪽은 지금 임시 그림으로 그린다.
    /// </summary>
    public static class VfxKeys
    {
        // ── 마법사 ──
        public const string MageChargeLarge = "mage/charge_large";
        public const string MageChargeSmall = "mage/charge_small";
        /// <summary>차지 보라(중력 갈래) — 가져오기 도구가 노랑을 색상 회전으로 만든 파생 그림.</summary>
        public const string MageChargeLargeViolet = "mage/charge_large_violet";
        public const string MageChargeSmallViolet = "mage/charge_small_violet";
        public const string MageFireProjectile = "mage/fire_projectile";
        public const string MageFireTrail = "mage/fire_trail";
        public const string MageFireTrailSmall = "mage/fire_trail_small";
        /// <summary>화상 걸린 적에 붙는 불꽃(Pixel Fire).</summary>
        public const string MageBurn = "mage/burn";
        /// <summary>화염돌진 바닥 불꽃 모양 더(넓은·낮은·큰) — 기존 fire_trail(가늘고 긴)·fire_trail_small과 섞는다.</summary>
        public const string MageFireTrailWide = "mage/fire_trail_wide";
        public const string MageFireTrailLow = "mage/fire_trail_low";
        public const string MageFireTrailBig = "mage/fire_trail_big";
        /// <summary>화염돌진: 바닥 불길 사이 작은 연기·출발 퍼프·도착 불꽃.</summary>
        public const string MageFireSmoke = "mage/fire_smoke";
        public const string MageDashPuff = "mage/dash_puff";
        public const string MageDashLand = "mage/dash_land";
        /// <summary>중력 공격이 적에게 맞을 때 튀는 보라 충격(작은 것 = 착탄, 큰 것 = 대붕괴 임계).</summary>
        public const string MageGravitySpark = "mage/gravity_spark";
        public const string MageGravitySparkLarge = "mage/gravity_spark_large";
        public const string MageGravityOrbSmall = "mage/gravity_orb_small";
        public const string MageGravityOrbLarge = "mage/gravity_orb_large";
        public const string MageFireExplosion = "mage/fire_explosion";
        public const string MageGravityExplosion = "mage/gravity_explosion";
        /// <summary>대붕괴 폭발(가시 충격 고리)과 임계 붕괴에 겹치는 큰 충격 — 둘 다 가져오기 도구가 보라로 돌린 파생 그림.</summary>
        public const string MageCollapseExplosion = "mage/collapse_explosion";
        public const string MageCollapseCritical = "mage/collapse_critical";

        // ── 메카닉 ──
        /// <summary>레이저 한 토막: <c>mechanic/laser_{origin|center|impact}_{start|loop|end}_{small|large}</c>.</summary>
        /// <summary>메카닉 레이저(팩 B형 small 파랑) — 두께는 셰이더가 층마다 잘라 정한다(2026-11-10).</summary>
        public static string MechanicLaser(string part, string phase) => $"mechanic/laser_b_{part}_{phase}";
        public static readonly string[] LaserParts = { "origin", "center", "impact" };
        public static readonly string[] LaserPhases = { "start", "loop", "end" };
        public const string MechanicPellet = "mechanic/pellet";
        /// <summary>레이저 차징(맞을 적이 없을 때 총구) — 가져오기 도구가 노랑 차지를 파랑으로 돌린 파생 그림.</summary>
        public const string MechanicCharge = "mechanic/charge";
        /// <summary>레이저에 맞은 적 자리의 작은 파란 충격(기가팩 directional_impact_001 파랑).</summary>
        public const string MechanicHitSpark = "mechanic/hit_spark";
        /// <summary>레이저 드론 몸통 — 팀원 문서의 드론 그림(Projectiles Pack 4 `scrap_metal` 회색).</summary>
        public const string MechanicDrone = "mechanic/drone";
        public const string MechanicRelayHead = "mechanic/relay_head";
        /// <summary>드론 발사 섬광 — 기가팩 scifi_muzzle_flash_003을 파랑으로(2026-11-10 사용자 D-B '발사 강조형').</summary>
        public const string MechanicDroneMuzzle = "mechanic/drone_muzzle";
        /// <summary>드론 탄 적중 불꽃 — 기가팩 scifi_spark_burst_001을 파랑으로(D-B).</summary>
        public const string MechanicDroneHit = "mechanic/drone_hit";
        /// <summary>설치기 피해 순간 적 자리의 전기 — 기가팩 lightning_burst_001을 파랑으로(2026-11-10 사용자 I-B '전기 아크형').</summary>
        public const string MechanicZap = "mechanic/zap";
        /// <summary>중계기 머리에 붙는 전기 오라 — 기가팩 lightning_aura_002 파랑(I-B).</summary>
        public const string MechanicRelayAura = "mechanic/relay_aura";

        // ── 살수 ──
        public const string AssassinSlash1 = "assassin/slash_1";
        public const string AssassinSlash2 = "assassin/slash_2";
        public const string AssassinSlash3 = "assassin/slash_3";
        /// <summary>칼날폭풍 회전베기(Slash Vol 2 업데이트 시트).</summary>
        public const string AssassinSpin = "assassin/spin";
        public const string AssassinHeartbeat = "assassin/heartbeat";
        public const string AssassinSplatter = "assassin/splatter";
        public const string AssassinSmoke = "assassin/smoke";

        /// <summary>모든 키(검사용).</summary>
        public static string[] All
        {
            get
            {
                var keys = new System.Collections.Generic.List<string>
                {
                    MageChargeLarge, MageChargeSmall, MageChargeLargeViolet, MageChargeSmallViolet, MageFireProjectile, MageFireTrail, MageFireTrailSmall, MageBurn, MageFireTrailWide, MageFireTrailLow, MageFireTrailBig,
                    MageFireSmoke, MageDashPuff, MageDashLand, MageGravitySpark, MageGravitySparkLarge,
                    MageGravityOrbSmall, MageGravityOrbLarge, MageFireExplosion, MageGravityExplosion, MageCollapseExplosion, MageCollapseCritical,
                    MechanicPellet, MechanicCharge, MechanicHitSpark, MechanicDrone, MechanicRelayHead,
                    MechanicDroneMuzzle, MechanicDroneHit, MechanicZap, MechanicRelayAura,
                    AssassinSlash1, AssassinSlash2, AssassinSlash3, AssassinSpin, AssassinHeartbeat, AssassinSplatter, AssassinSmoke,
                };
                foreach (var part in LaserParts)
                    foreach (var phase in LaserPhases)
                    {
                        keys.Add(MechanicLaser(part, phase));
                    }
                return keys.ToArray();
            }
        }
    }
}
