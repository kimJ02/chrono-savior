using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 전용 유물 6종 — **2026-11-22 재설계**(사용자 "레이저를 안 썼는데 다 죽는다, 레이저로 쓸어버려야 재미 / 설치기가 사기가 돼야지
    /// 왜 기본공격으로 하게 만드냐" — GPT와 정함). 시그니처(레이저 광선·설치 장)를 키우고, Z에는 피해를 더하지 않는다.
    /// - 잔탄 포켓(기본공격): Z 적중 n번마다 레이저는 드론 충전, 설치는 부품 적중을 더 준다(피해 없음).
    /// - 프리즘 구경(레이저): 광선 사거리·굵기·틱 피해(<see cref="BeamRangeBonus"/> 등 — <see cref="GunnerLaserBeam"/>이 읽는다).
    /// - 자율 포좌(레이저 전설): **광선으로** n마리 처치할 때마다 가까운 적들에게 광선이 튄다(드론 처치는 안 센다).
    /// - 삼각 방전(설치): 설치 한도 +1/2/3(<see cref="TurretBonus"/> — <see cref="GunnerSpecConfig.TurretMax"/>가 더한다).
    /// - 교차 전류(설치 영웅): 연결 거리·선 굵기·장판 피해·틱 간격(<see cref="GunnerSpecConfig"/>의 장판 수치가 읽는다).
    /// - 공성 연결망(설치 전설, 새로): 장판이 맞힌 적들에게 일제 사격 추가 피해.
    /// 세트 3: X 뒤 다음 시그니처 틱 피해 ×2. 세트 5: X 뒤 5초 과부하(광선 굵기 ×1.35·피해 ×1.5 / 장판 틱 ×0.6·피해 ×1.5).
    /// </summary>
    public static class GunnerRelics
    {
        static int zHits, beamKills;
        static bool setArmed;
        static float overloadUntil = -1f;

        public static void ResetForRun() { zHits = beamKills = 0; setArmed = false; overloadUntil = -1f; }

        // ── 시그니처 수치(킷·설정이 읽는다, 꺼져 있으면 0·1) ──
        public static float BeamRangeBonus => Relics.V("prismBore", 0);
        public static float BeamWidthBonus => Relics.V("prismBore", 1);
        public static float BeamDamageBonus => Relics.V("prismBore", 2);
        public static int TurretBonus => Relics.VI("triangleDischarge", 0);
        public static float LinkMaxMult => 1f + Relics.V("crossCurrent", 0);
        public static float LinkWidthBonus => Relics.V("crossCurrent", 1);
        public static float FieldDamageBonus => Relics.V("crossCurrent", 2);
        public static float FieldTickMult => (1f - Relics.V("crossCurrent", 3)) * (Overloaded ? 0.6f : 1f);

        /// <summary>세트 5 과부하 중(X 뒤 5초).</summary>
        public static bool Overloaded => Time.time < overloadUntil;
        public static float BeamWidthMult => Overloaded ? 1.35f : 1f;

        /// <summary>시그니처 피해 배수(과부하 ×1.5) — 매 틱 읽는다.</summary>
        public static float OverloadDamageMult => Overloaded ? 1.5f : 1f;

        /// <summary>세트 3 장전을 쓴다 — 시그니처가 **실제로 적을 맞힌** 틱에서 한 번만 부른다(빗나가면 남는다). 장전이면 2, 아니면 1.</summary>
        public static float ConsumeArmedMult()
        {
            if (!setArmed) return 1f;
            setArmed = false;
            return 2f;
        }

        /// <summary>X를 썼다 — 세트 3: 다음 시그니처 틱 ×2 장전. 세트 5: 5초 과부하.</summary>
        public static void OnSkillUsed(Transform player, int facing)
        {
            if (Relics.CharacterSet3) setArmed = true;
            if (Relics.CharacterSet5)
            {
                overloadUntil = Time.time + 5f;
                if (player != null) VfxPlayer.Spawn(VfxKeys.MechanicRelayAura, (Vector2)player.position + Vector2.up * 0.5f, DotScale.Value, 15f, RelicFx.SortingOrder);
            }
        }

        /// <summary>Z(총알·광선)가 적을 맞혔다 — 잔탄 포켓: 레이저는 드론 충전, 설치는 부품 적중을 더 준다.</summary>
        public static void OnZHit(GunnerAttack kit, Vector2 at)
        {
            if (Relics.InRelicDamage || kit == null || Relics.Rank("spareRounds") <= 0) return;
            if (++zHits < Relics.VI("spareRounds", 0)) return;
            zHits = 0;
            Relics.Proc("spareRounds");
            bool laser = ProfileService.Current.gunnerBranch == GunnerBranch.Laser;
            kit.AddStack(laser ? Relics.VI("spareRounds", 1) : Relics.VI("spareRounds", 2));
            VfxPlayer.Spawn(VfxKeys.MechanicHitSpark, at, DotScale.Value, 15f, RelicFx.SortingOrder);
        }

        /// <summary>레이저 광선이 적을 쓰러뜨렸다 — 자율 포좌: n마리마다 가까운 적들에게 광선이 튄다.</summary>
        public static void OnBeamKill(Vector2 at)
        {
            if (Relics.InRelicDamage || Relics.Rank("autoTurret") <= 0) return;
            if (++beamKills < Relics.VI("autoTurret", 0)) return;
            beamKills = 0;
            Relics.Proc("autoTurret");
            int n = Relics.VI("autoTurret", 1);
            bool gold = Relics.Awakened("autoTurret");
            float mult = Relics.V("autoTurret", 2);
            ChainFrom(at, n, mult, gold);
            if (gold) { var p = at; RelicFx.After(0.2f, () => ChainFrom(p, n, mult * 0.35f, true)); }
        }

        /// <summary>설치 장 한 틱이 적들을 맞혔다 — 공성 연결망: 맞은 적들에게 일제 사격.</summary>
        public static void OnFieldTick(List<Collider2D> hits, List<Vector2> pts)
        {
            if (hits.Count == 0 || Relics.InRelicDamage || Relics.Rank("siegeNetwork") <= 0) return;
            float mult = Relics.V("siegeNetwork", 0);
            Relics.Proc("siegeNetwork");
            int sparks = 0;
            using (Relics.Signature()) // 설치기 일제 사격도 설치 시그니처 — 공용 처치 유물이 센다
                foreach (var c in hits)
                {
                    if (c == null) continue;
                    Vector2 at = RelicFx.Center(c);
                    RelicFx.Hit(c, mult, 0f, 0.3f);
                    if (sparks++ < 6) VfxPlayer.Spawn(VfxKeys.MechanicZap, at, DotScale.Value, 15f, RelicFx.SortingOrder);
                }
        }

        /// <summary>at에서 가까운 적으로 n번 튀는 광선(첫 대상은 at 근처).</summary>
        static void ChainFrom(Vector2 at, int n, float mult, bool gold)
        {
            var visited = new List<Collider2D>();
            Vector2 from = at;
            using (Relics.Signature()) // 광선에서 튄 것 — 레이저 시그니처
                for (int i = 0; i < n; i++)
                {
                    var next = RelicFx.NearestOne(from, 4.5f, visited);
                    if (next == null) break;
                    Vector2 to = RelicFx.Center(next);
                    RelicLine.Show(from, to, 0.22f, gold ? RelicFx.Gold : RelicFx.Cyan, 0.22f);
                    RelicFx.Hit(next, mult, Mathf.Sign(to.x - from.x), 0.5f);
                    VfxPlayer.Spawn(VfxKeys.MechanicHitSpark, to, DotScale.Value, 15f, RelicFx.SortingOrder);
                    visited.Add(next);
                    from = to;
                }
        }
    }
}
