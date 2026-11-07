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
        public const string MageFireProjectile = "mage/fire_projectile";
        public const string MageFireTrail = "mage/fire_trail";
        public const string MageFireTrailSmall = "mage/fire_trail_small";
        public const string MageGravityOrbSmall = "mage/gravity_orb_small";
        public const string MageGravityOrbLarge = "mage/gravity_orb_large";
        public const string MageFireExplosion = "mage/fire_explosion";
        public const string MageGravityExplosion = "mage/gravity_explosion";

        // ── 메카닉 ──
        /// <summary>레이저 한 토막: <c>mechanic/laser_{origin|center|impact}_{start|loop|end}_{small|large}</c>.</summary>
        public static string MechanicLaser(string part, string phase, bool large) => $"mechanic/laser_{part}_{phase}_{(large ? "large" : "small")}";
        public static readonly string[] LaserParts = { "origin", "center", "impact" };
        public static readonly string[] LaserPhases = { "start", "loop", "end" };
        public const string MechanicPellet = "mechanic/pellet";
        public const string MechanicRelayHead = "mechanic/relay_head";

        // ── 살수 ──
        public const string AssassinSlash1 = "assassin/slash_1";
        public const string AssassinSlash2 = "assassin/slash_2";
        public const string AssassinSlash3 = "assassin/slash_3";
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
                    MageChargeLarge, MageChargeSmall, MageChargeLargeViolet, MageFireProjectile, MageFireTrail, MageFireTrailSmall,
                    MageGravityOrbSmall, MageGravityOrbLarge, MageFireExplosion, MageGravityExplosion,
                    MechanicPellet, MechanicRelayHead,
                    AssassinSlash1, AssassinSlash2, AssassinSlash3, AssassinHeartbeat, AssassinSplatter, AssassinSmoke,
                };
                foreach (var part in LaserParts)
                    foreach (var phase in LaserPhases)
                    {
                        keys.Add(MechanicLaser(part, phase, false));
                        keys.Add(MechanicLaser(part, phase, true));
                    }
                return keys.ToArray();
            }
        }
    }
}
