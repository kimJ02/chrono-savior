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

        // ── 비할당 범위 질의(2026-11-20 최적화) ──
        // OverlapCircleAll은 부를 때마다 새 배열을 만든다. 탄·당김 장이 매 프레임 부르면 몬스터 4배에서 쓰레기가 쌓인다.
        // 피해 → 처치 → 다른 유물 발동으로 질의 안에서 질의가 또 불릴 수 있어(재진입), 버퍼 하나를 돌려쓰지 않고 빌려 쓰고 돌려준다.
        static readonly Stack<List<Collider2D>> pool = new Stack<List<Collider2D>>();

        static ContactFilter2D Filter => new ContactFilter2D
        {
            useTriggers = Physics2D.queriesHitTriggers, // OverlapCircleAll 기본과 같게
            useLayerMask = true,
            layerMask = Physics2D.DefaultRaycastLayers,
        };

        /// <summary>원 안의 콜라이더 — 다 쓰면 <see cref="Release"/>로 돌려준다.</summary>
        public static List<Collider2D> QueryCircle(Vector2 pos, float radius)
        {
            var l = pool.Count > 0 ? pool.Pop() : new List<Collider2D>(64);
            Physics2D.OverlapCircle(pos, radius, Filter, l);
            return l;
        }

        /// <summary>돌린 막대 안의 콜라이더 — 다 쓰면 <see cref="Release"/>.</summary>
        public static List<Collider2D> QueryBox(Vector2 center, Vector2 size, float angle)
        {
            var l = pool.Count > 0 ? pool.Pop() : new List<Collider2D>(64);
            Physics2D.OverlapBox(center, size, angle, Filter, l);
            return l;
        }

        public static void Release(List<Collider2D> l)
        {
            if (l == null) return;
            l.Clear();
            pool.Push(l);
        }

        /// <summary>맞을 수 있는 적인지(태그·스폰 보호·생존).</summary>
        public static bool IsTarget(Collider2D col, out IDamageable target)
        {
            target = null;
            if (col == null || !col.CompareTag("Enemy")) return false;
            if (!Relics.RelicCanHit(col)) return false; // 레이저 갈래 — 레이저가 닿은 적만(2026-11-22)
            var protectable = col.GetComponent<ISpawnProtectable>();
            if (protectable != null && protectable.IsSpawnProtected) return false;
            target = col.GetComponent<IDamageable>();
            return target != null && !target.IsDead;
        }

        /// <summary>적 하나에 공격력 × 배수 피해.</summary>
        public static float Hit(Collider2D col, float atkMult, float dirSign, float knockback = 1.6f)
        {
            if (!IsTarget(col, out var target)) return 0f;
            return HitTarget(target, atkMult, dirSign, knockback);
        }

        /// <summary>이미 대상 검사를 마친 적에게(같은 컴포넌트를 두 번 찾지 않게).</summary>
        static float HitTarget(IDamageable target, float atkMult, float dirSign, float knockback)
        {
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
            var cols = QueryCircle(pos, radius);
            try
            {
                foreach (var col in cols)
                {
                    if (col == null || col == except || !IsTarget(col, out var target)) continue;
                    float dir = Mathf.Sign(col.transform.position.x - pos.x);
                    if (dir == 0f) dir = 1f;
                    if (HitTarget(target, atkMult, dir, knockback) > 0f) { n++; onHit?.Invoke(col); }
                }
            }
            finally { Release(cols); }
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
            var cols = QueryBox((a + b) * 0.5f, new Vector2(len, width), angle);
            try
            {
                foreach (var col in cols)
                {
                    if (col == null || (skip != null && skip.Contains(col))) continue;
                    if (!IsTarget(col, out var target)) continue;
                    if (HitTarget(target, atkMult, Mathf.Sign(d.x == 0f ? 1f : d.x), 0.8f) > 0f) n++;
                }
            }
            finally { Release(cols); }
            RelicLine.Show(a, b, width, color, showSeconds);
            return n;
        }

        /// <summary>from에서 가까운 적 n마리(범위 안). except는 뺀다.</summary>
        public static List<Collider2D> Nearest(Vector2 from, int n, float range, ICollection<Collider2D> except = null)
        {
            var list = new List<Collider2D>();
            var cols = QueryCircle(from, range);
            foreach (var col in cols)
            {
                if (col == null || (except != null && except.Contains(col))) continue;
                if (IsTarget(col, out _) && !list.Contains(col)) list.Add(col);
            }
            Release(cols);
            list.Sort((x, y) => ((Vector2)x.transform.position - from).sqrMagnitude
                         .CompareTo(((Vector2)y.transform.position - from).sqrMagnitude));
            if (list.Count > n) list.RemoveRange(n, list.Count - n);
            return list;
        }

        /// <summary>from에서 가장 가까운 적 하나(범위 안, except 빼고) — 매 프레임 부르는 유도탄용(목록·정렬 없이).</summary>
        public static Collider2D NearestOne(Vector2 from, float range, ICollection<Collider2D> except = null)
        {
            Collider2D best = null;
            float bestD = float.MaxValue;
            var cols = QueryCircle(from, range);
            foreach (var col in cols)
            {
                if (col == null || (except != null && except.Contains(col))) continue;
                if (!IsTarget(col, out _)) continue;
                float dd = ((Vector2)col.transform.position - from).sqrMagnitude;
                if (dd < bestD) { bestD = dd; best = col; }
            }
            Release(cols);
            return best;
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
            if (action == null) return;
            RelicDelayed.Add(delay, action);
        }
    }

    /// <summary>
    /// 늦게 터뜨리기용 — <see cref="RelicFx.After"/>. 2026-11-20 최적화: 예약마다 오브젝트를 만들던 것을 **런마다 하나**의 대기열로.
    /// 이 오브젝트는 런 한정(<see cref="RunTransient"/>)이라 런이 끝나면 대기열째 사라진다(예전과 같다).
    /// </summary>
    public class RelicDelayed : MonoBehaviour
    {
        struct Entry { public float t; public int frame; public System.Action a; }

        static RelicDelayed runner;
        int generation;
        readonly List<Entry> entries = new List<Entry>();
        readonly List<System.Action> due = new List<System.Action>();

        /// <summary>테스트용 — 기다리는 예약 수.</summary>
        public static int Pending => runner != null ? runner.entries.Count : 0;

        public static void Add(float delay, System.Action a)
        {
            // 정리(런 시작·재도전)된 뒤면 옛 대기열은 곧 지워진다 — 새로 만든다(옛 것은 지워질 때까지 자기 예약만 돌린다)
            if (runner == null || runner.generation != RunTransient.Generation)
            {
                var go = new GameObject("RelicDelayed");
                RunTransient.Mark(go);
                runner = go.AddComponent<RelicDelayed>();
                runner.generation = RunTransient.Generation;
            }
            runner.entries.Add(new Entry { t = delay, frame = Time.frameCount, a = a });
        }

        void OnDestroy() { if (runner == this) runner = null; }

        void Update()
        {
            // 정리(런 끝·시작)된 뒤의 옛 대기열은 아무것도 실행하지 않는다 — 곧 지워진다(2026-11-20 GPT 리뷰)
            if (generation != RunTransient.Generation) { entries.Clear(); due.Clear(); return; }
            float dt = Time.deltaTime;
            int now = Time.frameCount;
            due.Clear();
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.frame == now) continue; // 예전처럼 만든 프레임엔 줄이지 않는다(새 오브젝트의 Update는 다음 프레임부터였다)
                e.t -= dt;
                if (e.t > 0f) { entries[i] = e; continue; }
                due.Add(e.a);
                entries.RemoveAt(i);
                i--;
            }
            // 실행 중 새 예약이 들어와도 위 목록은 이미 정리돼 있어 안전하다(새 예약은 다음 프레임부터 센다)
            for (int i = 0; i < due.Count; i++)
            {
                if (generation != RunTransient.Generation) break; // 실행 중 포탈·런 끝으로 정리됐으면 나머지는 버린다
                try { due[i]?.Invoke(); }
                catch (System.Exception e) { Debug.LogException(e); } // 하나가 터져도 나머지 예약은 돈다
            }
            due.Clear();
        }
    }

    /// <summary>잠깐 보였다 사라지는 선(광선·베기 자국).</summary>
    public class RelicLine : MonoBehaviour
    {
        LineRenderer lr;
        float life, t;
        int generation;
        Color color;
        static Material mat;

        // 2026-11-20 최적화 — 다 보인 선은 지우지 않고 꺼 두었다가 다시 쓴다(런이 끝나 지워진 것은 건너뛴다)
        static readonly Stack<RelicLine> free = new Stack<RelicLine>();

        public static RelicLine Show(Vector2 a, Vector2 b, float width, Color color, float seconds)
        {
            RelicLine l = null;
            while (free.Count > 0 && l == null)
            {
                l = free.Pop();
                if (l != null && l.generation != RunTransient.Generation) l = null; // 정리 뒤 곧 지워질 것
            }
            if (l == null)
            {
                var go = new GameObject("RelicLine");
                RunTransient.Mark(go);
                l = go.AddComponent<RelicLine>();
                l.lr = go.AddComponent<LineRenderer>();
                if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
                l.lr.sharedMaterial = mat;
                l.generation = RunTransient.Generation;
            }
            l.gameObject.SetActive(true);
            l.t = 0f;
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
            if (t >= life) { gameObject.SetActive(false); free.Push(this); }
        }
    }
}
