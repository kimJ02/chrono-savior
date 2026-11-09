using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 설치기 빌드의 **링크·내부 장판** — 설치기 2개 이상이 서로 연결되면, 연결선 위와 (닫힌 도형이면) 그 안쪽에
    /// 주기적으로 피해를 준다. 원본 `mechanicFieldTurrets`(project_test.html:1388)·`mechanicFieldPoints`(:1392)·
    /// `mechanicLinkSegments`(:1399)·`convexHullPoints`/`pointInPolygon`/`distToSegment`(:1331~:1387)와
    /// `updateZones()` 앞부분(:3766~:3797)을 그대로 옮겼다.
    ///
    /// 좌표 주의: 원본은 Y+가 아래라 "위로 들어 올린다"가 `y - 120`이다 — Unity(Y+ 위)에선 `+1.20`.
    /// 볼록 껍질·점-다각형 판정은 방향(시계/반시계)과 무관해서 Y 반전 영향을 받지 않는다.
    /// </summary>
    public static class GunnerField
    {
        public struct Segment
        {
            public Vector2 a, b;
            public Segment(Vector2 a, Vector2 b) { this.a = a; this.b = b; }
        }

        // 원본 볼록 껍질의 픽셀 단위 허용오차 ÷100
        const float HullDuplicateEps = 0.005f;   // pushUnique: hypot < 0.5px
        const float HullEdgeEps = 0.025f;        // onEdge: distToSegment < 2.5px

        /// <summary>
        /// 장판을 이루는 설치기 — 원본 `zones.filter(turret && !dead).slice(0, gunnerTurretMax())`(설치한 순서).
        /// 빌드가 설치기 1층 미만이면 없다(호출하는 쪽이 판단).
        /// </summary>
        public static List<GunnerTurret> FieldTurrets(int installerTier)
        {
            var list = new List<GunnerTurret>();
            if (installerTier < 1) return list;
            int max = GunnerSpecConfig.TurretMax(installerTier);
            foreach (var t in GunnerTurret.Active)
            {
                if (list.Count >= max) break;
                list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// 원본 `mechanicFieldPoints` — 꼭짓점(설치기 위 0.24)을 x순으로 정렬하고, 앞 세 점이 거의 일직선이면
        /// (원본 식 그대로의 넓이×2 값이 0.08 미만) 두 번째 점을 위로 1.20 들어 올린 뒤 볼록 껍질로 정리한다.
        /// 바닥에 나란히 깐 설치기 셋도 삼각형 장판이 되게 하려는 원본 장치다.
        /// </summary>
        public static List<Vector2> FieldPoints(IReadOnlyList<GunnerTurret> fieldTurrets)
        {
            // JS Array.sort는 안정 정렬이라 x가 같을 때 원래 순서를 지킨다 — LINQ OrderBy도 안정 정렬이다.
            var pts = fieldTurrets.Select(z => z.FieldPoint).OrderBy(p => p.x).ToList();
            if (pts.Count < 3) return pts;
            float triArea = Mathf.Abs(pts[0].x * (pts[1].y - pts[2].y) + pts[1].x * (pts[2].y - pts[0].y)
                                      + pts[2].x * (pts[0].y - pts[1].y));
            if (triArea < GunnerSpecConfig.FieldFlatTriangleArea)
                pts[1] = new Vector2(pts[1].x, Mathf.Max(pts[0].y, pts[2].y) + GunnerSpecConfig.FieldLiftHeight);
            return ConvexHull(pts);
        }

        /// <summary>
        /// 원본 `convexHullPoints` — 단조 사슬로 볼록 껍질을 구한 뒤, **껍질 변 위에 놓인 점도 다시 끼워 넣는다**
        /// (일직선으로 깐 설치기가 껍질 계산에서 빠져 연결이 끊기지 않게). 3개 이하면 그대로 둔다.
        /// </summary>
        public static List<Vector2> ConvexHull(List<Vector2> pts)
        {
            if (pts.Count <= 3) return pts;
            var sorted = pts.OrderBy(p => p.x).ThenBy(p => p.y).ToList();

            var lower = new List<Vector2>();
            foreach (var p in sorted)
            {
                while (lower.Count >= 2 && Cross(lower[lower.Count - 2], lower[lower.Count - 1], p) <= 0f)
                    lower.RemoveAt(lower.Count - 1);
                lower.Add(p);
            }
            var upper = new List<Vector2>();
            for (int i = sorted.Count - 1; i >= 0; i--)
            {
                var p = sorted[i];
                while (upper.Count >= 2 && Cross(upper[upper.Count - 2], upper[upper.Count - 1], p) <= 0f)
                    upper.RemoveAt(upper.Count - 1);
                upper.Add(p);
            }
            var hull = new List<Vector2>();
            hull.AddRange(lower.GetRange(0, lower.Count - 1));
            hull.AddRange(upper.GetRange(0, upper.Count - 1));
            if (hull.Count < 3) return sorted;

            var result = new List<Vector2>();
            for (int i = 0; i < hull.Count; i++)
            {
                Vector2 a = hull[i], b = hull[(i + 1) % hull.Count];
                float vx = b.x - a.x, vy = b.y - a.y;
                float len2 = vx * vx + vy * vy;
                if (len2 == 0f) len2 = 1f;
                var onEdge = pts
                    .Select(p => (p, t: ((p.x - a.x) * vx + (p.y - a.y) * vy) / len2))
                    .Where(o => o.t >= -0.01f && o.t <= 1.01f && DistToSegment(o.p, a, b) < HullEdgeEps)
                    .OrderBy(o => o.t);
                foreach (var o in onEdge)
                {
                    bool dup = false;
                    foreach (var q in result) if (Vector2.Distance(q, o.p) < HullDuplicateEps) { dup = true; break; }
                    if (!dup) result.Add(o.p);
                }
            }
            return result;
        }

        static float Cross(Vector2 o, Vector2 a, Vector2 b) => (a.x - o.x) * (b.y - o.y) - (a.y - o.y) * (b.x - o.x);

        /// <summary>
        /// 원본 `mechanicLinkSegments` — 이웃한 꼭짓점끼리 거리가 링크 한도 이하면 연결한다. 3개 이상이면
        /// 마지막↔처음도 잇는다(그래야 닫힌 도형).
        /// </summary>
        public static List<Segment> LinkSegments(List<Vector2> pts, float maxD)
        {
            var segs = new List<Segment>();
            if (pts.Count < 2) return segs;
            for (int i = 0; i < pts.Count - 1; i++)
                if (Vector2.Distance(pts[i], pts[i + 1]) <= maxD) segs.Add(new Segment(pts[i], pts[i + 1]));
            if (pts.Count >= 3 && Vector2.Distance(pts[pts.Count - 1], pts[0]) <= maxD)
                segs.Add(new Segment(pts[pts.Count - 1], pts[0]));
            return segs;
        }

        /// <summary>원본 `pointInPolygon` — 반직선 교차 판정. 점이 3개 미만이면 false.</summary>
        public static bool PointInPolygon(Vector2 p, List<Vector2> pts)
        {
            if (pts.Count < 3) return false;
            bool inside = false;
            for (int i = 0, j = pts.Count - 1; i < pts.Count; j = i++)
            {
                Vector2 a = pts[i], b = pts[j];
                float dy = b.y - a.y;
                if (dy == 0f) dy = 1f; // 원본 `((b.y - a.y) || 1)`
                bool cross = ((a.y > p.y) != (b.y > p.y)) && (p.x < (b.x - a.x) * (p.y - a.y) / dy + a.x);
                if (cross) inside = !inside;
            }
            return inside;
        }

        /// <summary>원본 `distToSegment` — 점과 선분 사이 최단거리.</summary>
        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            float vx = b.x - a.x, vy = b.y - a.y;
            float len2 = vx * vx + vy * vy;
            if (len2 == 0f) len2 = 1f;
            float t = Mathf.Clamp01(((p.x - a.x) * vx + (p.y - a.y) * vy) / len2);
            return Vector2.Distance(p, new Vector2(a.x + vx * t, a.y + vy * t));
        }

        /// <summary>
        /// 장판 한 틱 — 원본 `updateZones()` :3775~:3790. 링크 위(선분까지 `30+tier*3`px + 적 폭×0.25 이내)이거나
        /// **모든 변이 연결된 닫힌 도형** 안이면 피해. 치명타 없음·넉백 없음·히트스톱 없음(`noCrit·kb:0·noHitstop`)
        /// 이라 <see cref="IDamageable.TakeTickDamage"/>로 보낸다.
        /// </summary>
        /// <returns>이번 틱에 맞은 적이 있었는지(원본 `hit`).</returns>
        public static bool DamageTick(int tier, List<Vector2> pts, List<Segment> segs)
        {
            if (pts.Count == 0) return false;
            bool closedField = pts.Count >= 3 && segs.Count >= pts.Count;

            Vector2 c = Vector2.zero;
            foreach (var p in pts) c += p;
            c /= pts.Count;
            float r = 0f;
            foreach (var p in pts) r = Mathf.Max(r, Vector2.Distance(p, c));
            r += GunnerSpecConfig.LinkHitWidth(tier) + GunnerTargets.QueryMargin;

            float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
            bool hit = false;
            foreach (var e in GunnerTargets.Query(c, r))
            {
                float linkW = GunnerSpecConfig.LinkHitWidth(tier) + e.width * GunnerSpecConfig.LinkEnemyWidthFactor;
                bool onLink = false;
                foreach (var s in segs)
                    if (DistToSegment(e.center, s.a, s.b) < linkW) { onLink = true; break; }
                bool insideField = closedField && PointInPolygon(e.center, pts);
                if (!onLink && !insideField) continue;

                hit = true;
                float mult = GunnerSpecConfig.FieldDamageMult(tier, onLink, insideField && pts.Count >= 3);
                float dmg = DamageCalculator.Roll(atk * mult, 0f, out _); // 원본 noCrit
                e.damageable.TakeTickDamage(dmg);                       // 원본 kb:0 + noHitstop
                GunnerZap.At(e.collider, e.center);                      // 그림만 — 맞은 적 자리 전기(적마다 0.25초에 한 번, 2026-11-10 I-B)
            }
            return hit;
        }
    }
}
