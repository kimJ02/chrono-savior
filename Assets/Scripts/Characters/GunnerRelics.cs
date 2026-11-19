using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 전용 유물 5종(2026-11-19 테스트 유물, <see cref="ItemDatabase"/>) — 공격 코드가 적중·처치 때 여기를 부른다.
    /// - 잔탄 포켓(기본공격): Z가 n번 맞힐 때마다 유도 파편탄.
    /// - 프리즘 구경(레이저): 광선이 n번 맞힐 때마다 옆으로 갈라지는 광선.
    /// - 자율 포좌(레이저 전설): 드론이 처치할 때마다 유도탄 여러 발.
    /// - 삼각 방전(설치): 설치 장 안 적중 n번마다 번개가 여러 적에게 튄다.
    /// - 교차 전류(설치 영웅): 설치 장 안 적중 n번마다 설치기 사이를 잇는 교차 광선.
    /// </summary>
    public static class GunnerRelics
    {
        static int zHits, beamHits, fieldHitsDischarge, fieldHitsCross;

        public static void ResetForRun() { zHits = beamHits = fieldHitsDischarge = fieldHitsCross = 0; }

        /// <summary>Z(총알·광선)가 적을 맞혔다 — 잔탄 포켓.</summary>
        public static void OnZHit(Vector2 from, Vector2 at)
        {
            if (Relics.InRelicDamage || Relics.Rank("spareRounds") <= 0) return;
            if (++zHits < Relics.VI("spareRounds", 0)) return;
            zHits = 0;
            float mult = Relics.V("spareRounds", 1);
            bool gold = Relics.Awakened("spareRounds");
            Missile(from, at - from, mult, gold ? RelicFx.Gold : RelicFx.Blue);
            if (gold) RelicFx.After(0.2f, () => Missile(from, at - from, mult * 0.35f, RelicFx.Gold));
        }

        /// <summary>레이저 광선 한 틱이 적들을 맞혔다 — 프리즘 구경 + 잔탄 포켓(광선도 Z라서).</summary>
        public static void OnBeamTick(Vector2 origin, Vector2 dir, List<Vector2> hitCenters)
        {
            if (hitCenters.Count == 0 || Relics.InRelicDamage) return;
            OnZHit(origin, hitCenters[0]);
            if (Relics.Rank("prismBore") <= 0) return;
            if (++beamHits < Relics.VI("prismBore", 0)) return;
            beamHits = 0;
            int n = Relics.VI("prismBore", 1);
            float mult = Relics.V("prismBore", 2);
            Vector2 at = hitCenters[0];
            Vector2 d = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
            bool gold = Relics.Awakened("prismBore");
            for (int i = 0; i < n; i++)
            {
                float deg = n == 3 && i == 2 ? 0f : (i % 2 == 0 ? 28f : -28f);
                Vector2 side = Quaternion.Euler(0f, 0f, deg) * d;
                RelicFx.Line(at, at + side * 5f, 0.35f, mult, RelicFx.Cyan, 0.2f);
                if (gold) RelicFx.After(0.2f, () => RelicFx.Line(at, at + side * 5f, 0.35f, mult * 0.35f, RelicFx.Gold, 0.2f));
            }
            VfxPlayer.Spawn(VfxKeys.MechanicHitSpark, at, DotScale.Value, 15f, RelicFx.SortingOrder);
        }

        /// <summary>드론 탄이 적을 쓰러뜨렸다 — 자율 포좌.</summary>
        public static void OnDroneKill(Vector2 at)
        {
            if (Relics.InRelicDamage || Relics.Rank("autoTurret") <= 0) return;
            int n = Relics.VI("autoTurret", 0);
            float mult = Relics.V("autoTurret", 1);
            bool gold = Relics.Awakened("autoTurret");
            for (int i = 0; i < n; i++)
            {
                float deg = 90f + (i - (n - 1) * 0.5f) * 35f;
                Vector2 dir = Quaternion.Euler(0f, 0f, deg) * Vector2.right;
                Missile(at, dir, mult, gold ? RelicFx.Gold : RelicFx.Blue);
            }
            VfxPlayer.Spawn(VfxKeys.MechanicDroneMuzzle, at, DotScale.Value, 15f, RelicFx.SortingOrder);
        }

        /// <summary>설치 장 한 틱이 적들을 맞혔다 — 삼각 방전·교차 전류.</summary>
        public static void OnFieldTick(List<Collider2D> hits, List<Vector2> pts)
        {
            if (hits.Count == 0 || Relics.InRelicDamage) return;

            if (Relics.Rank("triangleDischarge") > 0)
            {
                fieldHitsDischarge += hits.Count;
                int every = Relics.VI("triangleDischarge", 0);
                if (fieldHitsDischarge >= every)
                {
                    fieldHitsDischarge = 0;
                    Chain(hits[0], Relics.VI("triangleDischarge", 1), Relics.V("triangleDischarge", 2),
                          Relics.Awakened("triangleDischarge"));
                }
            }

            if (Relics.Rank("crossCurrent") > 0 && pts.Count >= 2)
            {
                fieldHitsCross += hits.Count;
                if (fieldHitsCross >= Relics.VI("crossCurrent", 0))
                {
                    fieldHitsCross = 0;
                    float mult = Relics.V("crossCurrent", 1);
                    bool gold = Relics.Awakened("crossCurrent");
                    var copy = new List<Vector2>(pts);
                    Cross(copy, mult, RelicFx.Cyan);
                    if (gold) RelicFx.After(0.2f, () => Cross(copy, mult * 0.35f, RelicFx.Gold));
                }
            }
        }

        static void Cross(List<Vector2> pts, float mult, Color color)
        {
            // 설치기 모든 쌍을 잇는다(삼각형이면 변 셋 + 가운데를 가로지르는 선)
            var hitOnce = new HashSet<Collider2D>();
            for (int i = 0; i < pts.Count; i++)
                for (int j = i + 1; j < pts.Count; j++)
                    RelicFx.Line(pts[i], pts[j], 0.5f, mult, color, 0.3f, hitOnce);
            if (pts.Count >= 3)
            {
                Vector2 c = Vector2.zero;
                foreach (var p in pts) c += p;
                c /= pts.Count;
                foreach (var p in pts) RelicFx.Line(p, c + (c - p), 0.4f, mult * 0.5f, color, 0.3f, hitOnce);
            }
        }

        /// <summary>번개 사슬 — 첫 적에서 가까운 적으로 n번 튄다.</summary>
        static void Chain(Collider2D first, int n, float mult, bool gold)
        {
            var visited = new List<Collider2D> { first };
            Vector2 from = RelicFx.Center(first);
            for (int i = 0; i < n; i++)
            {
                var next = RelicFx.Nearest(from, 1, 4f, visited);
                if (next.Count == 0) break;
                var c = next[0];
                Vector2 to = RelicFx.Center(c);
                RelicLine.Show(from, to, 0.18f, gold ? RelicFx.Gold : RelicFx.Cyan, 0.2f);
                RelicFx.Hit(c, mult * (gold ? 1.35f : 1f), Mathf.Sign(to.x - from.x), 0.5f);
                VfxPlayer.Spawn(VfxKeys.MechanicZap, to, DotScale.Value, 15f, RelicFx.SortingOrder);
                visited.Add(c);
                from = to;
            }
        }

        static void Missile(Vector2 from, Vector2 dir, float mult, Color tint) =>
            RelicProjectile.Spawn(from, dir, 9f, 1.8f, mult, RelicProjectile.Mode.Homing, VfxKeys.MechanicPellet, tint, 0.3f, 0);
    }
}
