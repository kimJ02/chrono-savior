using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 유물 효과가 같이 쓰는 도구(2026-11-19 테스트 유물) — 범위 피해, 선 피해, 가까운 적 찾기, 늦게 터뜨리기.
    /// 피해는 전부 **플레이어 공격력 × 배수**를 굴려(<see cref="DamageCalculator.Roll(float,float,out bool)"/>) 주고,
    /// <see cref="Relics.Damaging"/>으로 감싸 유물 피해가 다른 유물을 다시 발동시키지 않게 한다.
    /// </summary>
    public static class RelicFx
    {
        public static readonly Color Gold = new Color(1f, 0.82f, 0.35f, 0.95f);
        public static readonly Color Fire = new Color(1f, 0.55f, 0.2f, 0.9f);
        public static readonly Color Violet = new Color(0.8f, 0.55f, 1f, 0.9f);
        public static readonly Color Cyan = new Color(0.45f, 0.95f, 1f, 0.9f);
        public static readonly Color Blue = new Color(0.45f, 0.7f, 1f, 0.9f);
        public static readonly Color Light = new Color(1f, 0.95f, 0.7f, 0.9f);

        public const int SortingOrder = 4;

        static float Atk => PlayerStatCalculator.ComputeAtk(ProfileService.Current);
        static float Crit => PlayerStatCalculator.ComputeCritChance(ProfileService.Current);

        /// <summary>맞을 수 있는 적인지(태그·스폰 보호·생존).</summary>
        public static bool IsTarget(Collider2D col, out IDamageable target)
        {
            target = null;
            if (col == null || !col.CompareTag("Enemy")) return false;
            var protectable = col.GetComponent<ISpawnProtectable>();
            if (protectable != null && protectable.IsSpawnProtected) return false;
            target = col.GetComponent<IDamageable>();
            return target != null && !target.IsDead;
        }

        /// <summary>적 하나에 공격력 × 배수 피해.</summary>
        public static float Hit(Collider2D col, float atkMult, float dirSign, float knockback = 1.6f)
        {
            if (!IsTarget(col, out var target)) return 0f;
            using (Relics.Damaging())
            {
                float dmg = DamageCalculator.Roll(Atk * atkMult, Crit, out _);
                return target.TakeDamageWithKnockback(dmg, null, dirSign, knockback, HitFeel.HitstopNone);
            }
        }

        /// <summary>원 범위 피해. 맞힌 수를 돌려준다. 그림 키가 있으면 터지는 그림, 늘 범위 고리.</summary>
        public static int Circle(Vector2 pos, float radius, float atkMult, Color ring, string vfxKey = null,
                                 float knockback = 1.6f, Collider2D except = null, System.Action<Collider2D> onHit = null)
        {
            int n = 0;
            foreach (var col in Physics2D.OverlapCircleAll(pos, radius))
            {
                if (col == except || !IsTarget(col, out _)) continue;
                float dir = Mathf.Sign(col.transform.position.x - pos.x);
                if (dir == 0f) dir = 1f;
                if (Hit(col, atkMult, dir, knockback) > 0f) { n++; onHit?.Invoke(col); }
            }
            RangeRing.Show(pos, radius, ring);
            if (vfxKey != null) VfxPlayer.Spawn(vfxKey, pos, DotScale.Value, 15f, SortingOrder, tint: null);
            if (n > 0) HitFeel.Shake(3f, 0.12f);
            return n;
        }

        /// <summary>선(두께 있는 막대) 범위 피해 + 선 그림. 맞힌 수.</summary>
        public static int Line(Vector2 a, Vector2 b, float width, float atkMult, Color color, float showSeconds = 0.25f,
                               HashSet<Collider2D> skip = null)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 0.01f) return 0;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            int n = 0;
            foreach (var col in Physics2D.OverlapBoxAll((a + b) * 0.5f, new Vector2(len, width), angle))
            {
                if (skip != null && skip.Contains(col)) continue;
                if (!IsTarget(col, out _)) continue;
                if (Hit(col, atkMult, Mathf.Sign(d.x == 0f ? 1f : d.x), 0.8f) > 0f) n++;
            }
            RelicLine.Show(a, b, width, color, showSeconds);
            return n;
        }

        /// <summary>from에서 가까운 적 n마리(범위 안). except는 뺀다.</summary>
        public static List<Collider2D> Nearest(Vector2 from, int n, float range, ICollection<Collider2D> except = null)
        {
            var list = new List<Collider2D>();
            foreach (var col in Physics2D.OverlapCircleAll(from, range))
            {
                if (except != null && except.Contains(col)) continue;
                if (IsTarget(col, out _) && !list.Contains(col)) list.Add(col);
            }
            list.Sort((x, y) => ((Vector2)x.transform.position - from).sqrMagnitude
                         .CompareTo(((Vector2)y.transform.position - from).sqrMagnitude));
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        /// <summary>가장 밀집한 곳(반경 r 안에 적이 제일 많은 적의 자리). 없으면 null.</summary>
        public static Vector2? Densest(Vector2 around, float searchRange, float r)
        {
            var all = Nearest(around, 64, searchRange);
            if (all.Count == 0) return null;
            Vector2 best = all[0].transform.position;
            int bestN = 0;
            foreach (var c in all)
            {
                Vector2 p = c.transform.position;
                int k = 0;
                foreach (var o in all) if (((Vector2)o.transform.position - p).sqrMagnitude <= r * r) k++;
                if (k > bestN) { bestN = k; best = p; }
            }
            return best;
        }

        /// <summary>몸 중심(콜라이더 가운데).</summary>
        public static Vector2 Center(Collider2D col) => col != null ? (Vector2)col.bounds.center : Vector2.zero;

        /// <summary>delay초 뒤 실행(런이 끝나면 같이 사라진다).</summary>
        public static void After(float delay, System.Action action)
        {
            var go = new GameObject("RelicDelayed");
            RunTransient.Mark(go);
            go.AddComponent<RelicDelayed>().Init(delay, action);
        }
    }

    /// <summary>늦게 터뜨리기용 — <see cref="RelicFx.After"/>.</summary>
    public class RelicDelayed : MonoBehaviour
    {
        float t;
        System.Action action;
        public void Init(float delay, System.Action a) { t = delay; action = a; }
        void Update()
        {
            t -= Time.deltaTime;
            if (t > 0f) return;
            var a = action;
            action = null;
            Destroy(gameObject);
            a?.Invoke();
        }
    }

    /// <summary>잠깐 보였다 사라지는 선(광선·베기 자국).</summary>
    public class RelicLine : MonoBehaviour
    {
        LineRenderer lr;
        float life, t;
        Color color;
        static Material mat;

        public static RelicLine Show(Vector2 a, Vector2 b, float width, Color color, float seconds)
        {
            var go = new GameObject("RelicLine");
            RunTransient.Mark(go);
            var l = go.AddComponent<RelicLine>();
            l.lr = go.AddComponent<LineRenderer>();
            if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
            l.lr.sharedMaterial = mat;
            l.lr.positionCount = 2;
            l.lr.SetPosition(0, a);
            l.lr.SetPosition(1, b);
            l.lr.startWidth = l.lr.endWidth = Mathf.Max(0.05f, width * 0.6f);
            l.lr.sortingOrder = RelicFx.SortingOrder;
            l.lr.numCapVertices = 0;
            l.color = color;
            l.life = Mathf.Max(0.05f, seconds);
            l.Apply(1f);
            return l;
        }

        void Apply(float a)
        {
            var c = color; c.a *= a;
            lr.startColor = lr.endColor = c;
        }

        void Update()
        {
            t += Time.deltaTime;
            Apply(1f - t / life);
            if (t >= life) Destroy(gameObject);
        }
    }
}
