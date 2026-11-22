using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 전용 유물 6종 — **2026-11-22 재설계 2차**(사용자 "레이저는 Z·X에 골고루 특별한 효과, 설치는 X에 특별한 효과" — GPT와 정함).
    /// 수치만 올리지 않고 그 버튼의 플레이를 바꾼다.
    /// - 잔탄 포켓(기본공격, 두 갈래): Z 적중으로 잔탄을 모아 다음 X 때 전기 연쇄로 터뜨린다.
    /// - 프리즘 구경(레이저, Z): 광선 처치 n번마다 광선이 갈라져 주변 적에게 연쇄.
    /// - 자율 포좌(레이저, X): 드론이 n번 쏠 때마다 부채꼴 추가 탄.
    /// - 삼각 방전(설치, X): 설치 한도 +, X 한 번에 중계기를 더 놓는다(<see cref="ExtraRelays"/> — GunnerAttack이 놓는다).
    /// - 교차 전류(설치, X): 설치하는 순간 그 자리에서 전류 방출.
    /// - 공성 연결망(설치, X): 장판이 맞힌 틱마다 적에서 적으로 전류가 이어진다.
    /// 세트 3: X 뒤 첫 시그니처 적중 틱 ×2. 세트 5: X 뒤 5초 과부하(광선 굵기 ×1.35·피해 ×1.5 / 장판 틱 ×0.6·피해 ×1.5).
    /// </summary>
    public static class GunnerRelics
    {
        const int MaxSpareCharges = 3;
        static int zHits, spareCharges, beamKills, droneShots;
        static bool setArmed;
        static float overloadUntil = -1f;

        public static void ResetForRun() { zHits = spareCharges = beamKills = droneShots = 0; setArmed = false; overloadUntil = -1f; Relics.ClearLaserTouched(); }

        // ── 킷·설정이 읽는 값 ──
        public static int TurretBonus => Relics.VI("triangleDischarge", 0);
        /// <summary>X 한 번에 더 놓는 중계기 수(삼각 방전).</summary>
        public static int ExtraRelays => Relics.VI("triangleDischarge", 1);
        public static bool Overloaded => Time.time < overloadUntil;
        public static float BeamWidthMult => Overloaded ? 1.35f : 1f;
        public static float FieldTickMult => Overloaded ? 0.6f : 1f;
        public static float OverloadDamageMult => Overloaded ? 1.5f : 1f;

        /// <summary>세트 3 장전을 쓴다 — 시그니처가 실제로 적을 맞힌 틱에서 한 번만(빗나가면 남는다). 장전이면 2, 아니면 1.</summary>
        public static float ConsumeArmedMult()
        {
            if (!setArmed) return 1f;
            setArmed = false;
            return 2f;
        }

        /// <summary>
        /// X를 썼다(<paramref name="at"/> = 드론이 뜨는·설치기를 놓은 자리) — 세트, 잔탄 포켓(모은 잔탄을 전기 연쇄로), 교차 전류(설치 순간 방출).
        /// </summary>
        public static void OnSkillUsed(Vector2 at, bool installer)
        {
            if (Relics.CharacterSet3) setArmed = true;
            if (Relics.CharacterSet5)
            {
                overloadUntil = Time.time + 5f;
                VfxPlayer.Spawn(VfxKeys.MechanicRelayAura, at + Vector2.up * 0.5f, DotScale.Value, 15f, RelicFx.SortingOrder);
            }

            // 잔탄 포켓 — 잔탄마다 0.1초 간격으로 전기 연쇄
            if (spareCharges > 0 && Relics.Rank("spareRounds") > 0)
            {
                Relics.Proc("spareRounds");
                int n = spareCharges, targets = Relics.VI("spareRounds", 1);
                float mult = Relics.V("spareRounds", 2);
                spareCharges = 0;
                Vector2 o = at + Vector2.up * 0.6f;
                for (int i = 0; i < n; i++) RelicFx.After(0.1f * i, () => ChainFrom(o, targets, mult, false));
            }

            // 교차 전류 — 설치하는 순간 그 자리에서 방출
            if (installer && Relics.Rank("crossCurrent") > 0)
            {
                Relics.Proc("crossCurrent");
                bool gold = Relics.Awakened("crossCurrent");
                int targets = Relics.VI("crossCurrent", 0);
                float mult = Relics.V("crossCurrent", 1);
                Vector2 o = at + new Vector2(0f, GunnerSpecConfig.TurretCenterHeight);
                VfxPlayer.Spawn(VfxKeys.MechanicRelayAura, o, DotScale.Value, 15f, RelicFx.SortingOrder);
                ChainFrom(o, targets, mult, gold);
                if (gold) RelicFx.After(0.2f, () => ChainFrom(o, targets, mult * 0.35f, true));
            }
        }

        /// <summary>Z(총알·광선)가 적 하나를 맞혔다 — 잔탄 포켓: n번마다 잔탄 1(최대 3).</summary>
        public static void OnZHit()
        {
            if (Relics.InRelicDamage || Relics.Rank("spareRounds") <= 0 || spareCharges >= MaxSpareCharges) return;
            if (++zHits < Relics.VI("spareRounds", 0)) return;
            zHits = 0;
            spareCharges++;
        }

        /// <summary>지금 모인 잔탄(HUD·테스트용).</summary>
        public static int SpareCharges => spareCharges;

        /// <summary>레이저 광선이 적을 쓰러뜨렸다 — 프리즘 구경: n번마다 광선이 갈라져 주변 적에게 연쇄.</summary>
        public static void OnBeamKill(Vector2 at)
        {
            if (Relics.InRelicDamage || Relics.Rank("prismBore") <= 0) return;
            if (++beamKills < Relics.VI("prismBore", 0)) return;
            beamKills = 0;
            Relics.Proc("prismBore");
            bool gold = Relics.Awakened("prismBore");
            int n = Relics.VI("prismBore", 1);
            float mult = Relics.V("prismBore", 2);
            ChainFrom(at, n, mult, gold);
            if (gold) { var p = at; RelicFx.After(0.2f, () => ChainFrom(p, n, mult * 0.35f, true)); }
        }

        /// <summary>드론이 한 발 쐈다 — 자율 포좌: n발마다 부채꼴 추가 탄.</summary>
        public static void OnDroneShot(Vector2 pos, Vector2 dir)
        {
            if (Relics.Rank("autoTurret") <= 0) return;
            if (++droneShots < Relics.VI("autoTurret", 0)) return;
            droneShots = 0;
            Relics.Proc("autoTurret");
            int n = Relics.VI("autoTurret", 1);
            float mult = Relics.V("autoTurret", 2);
            bool gold = Relics.Awakened("autoTurret");
            for (int i = 0; i < n; i++)
            {
                float deg = n == 1 ? 14f : (i == 0 ? 16f : -16f);
                Vector2 d = Quaternion.Euler(0f, 0f, deg) * dir;
                RelicProjectile.Spawn(pos, d, 12f, 0.8f, mult, RelicProjectile.Mode.Homing, VfxKeys.MechanicPellet,
                                      gold ? RelicFx.Gold : GunnerAttack.LaserColor, 0.3f, 0);
            }
        }

        /// <summary>설치 장 한 틱이 적들을 맞혔다 — 공성 연결망: 맞은 적에서 다른 적으로 전류가 이어진다.</summary>
        public static void OnFieldTick(List<Collider2D> hits, List<Vector2> pts)
        {
            if (hits.Count == 0 || Relics.InRelicDamage || Relics.Rank("siegeNetwork") <= 0) return;
            Relics.Proc("siegeNetwork");
            var first = hits[0];
            if (first == null) return;
            ChainFrom(RelicFx.Center(first), Relics.VI("siegeNetwork", 0), Relics.V("siegeNetwork", 1), Relics.Awakened("siegeNetwork"), first); // 맞은 적은 빼고 다음 적부터
        }

        /// <summary>at에서 가까운 적으로 n번 튀는 전류·광선.</summary>
        static void ChainFrom(Vector2 at, int n, float mult, bool gold, Collider2D exclude = null)
        {
            var visited = new List<Collider2D>();
            if (exclude != null) visited.Add(exclude);
            Vector2 from = at;
            for (int i = 0; i < n; i++)
            {
                var next = RelicFx.NearestOne(from, 4.5f, visited);
                if (next == null) break;
                Vector2 to = RelicFx.Center(next);
                RelicLine.Show(from, to, 0.2f, gold ? RelicFx.Gold : RelicFx.Cyan, 0.22f);
                RelicFx.Hit(next, mult, Mathf.Sign(to.x - from.x), 0.5f);
                VfxPlayer.Spawn(VfxKeys.MechanicZap, to, DotScale.Value, 15f, RelicFx.SortingOrder);
                visited.Add(next);
                from = to;
            }
        }
    }
}
