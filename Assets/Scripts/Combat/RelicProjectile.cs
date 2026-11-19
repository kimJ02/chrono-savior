using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 유물이 쏘는 탄(칼날·유도탄·파편) — 2026-11-19 테스트 유물. 물리 몸 없이 매 프레임 움직이고 반경 안 적을 맞힌다.
    /// 직선 / 유도(가까운 적으로 휜다) / 낙하(아래로 떨어져 땅이나 적에 닿으면 폭발) / 부메랑(갔다가 돌아온다).
    /// </summary>
    public class RelicProjectile : MonoBehaviour
    {
        public enum Mode { Straight, Homing, Falling, Boomerang }

        Mode mode;
        Vector2 vel;
        float speed, life, radius, atkMult, turnRate;
        int pierceLeft;
        float explodeRadius;
        Color ringColor;
        string explodeKey;
        Transform owner;
        float age;
        bool returning;
        readonly HashSet<Collider2D> hit = new HashSet<Collider2D>();
        VfxPlayer art;
        SpriteRenderer fallback;

        static Sprite dot;

        /// <summary>탄 하나를 쏜다.</summary>
        /// <param name="artKey">그림(반복 재생). 없으면 색 점으로.</param>
        /// <param name="pierce">관통 횟수(-1 = 무한).</param>
        /// <param name="explodeRadius">0보다 크면 첫 적중·착지에서 폭발(범위 피해)하고 사라진다.</param>
        public static RelicProjectile Spawn(Vector2 pos, Vector2 dir, float speed, float life, float atkMult, Mode mode,
                                            string artKey, Color tint, float hitRadius = 0.35f, int pierce = 0,
                                            float explodeRadius = 0f, string explodeKey = null, Transform owner = null, float artScale = 1f)
        {
            var go = new GameObject("RelicProjectile");
            go.transform.position = pos;
            RunTransient.Mark(go);
            var p = go.AddComponent<RelicProjectile>();
            p.mode = mode;
            p.speed = speed;
            p.vel = (dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right) * speed;
            p.life = life;
            p.atkMult = atkMult;
            p.radius = hitRadius;
            p.pierceLeft = pierce;
            p.explodeRadius = explodeRadius;
            p.explodeKey = explodeKey;
            p.ringColor = tint;
            p.owner = owner;
            p.turnRate = 9f;

            if (artKey != null)
            {
                p.art = VfxPlayer.Spawn(artKey, pos, DotScale.Value * artScale, 15f, RelicFx.SortingOrder, loop: true, tint: tint);
                if (p.art != null) p.art.transform.SetParent(go.transform, true);
            }
            if (p.art == null)
            {
                if (dot == null)
                {
                    var tex = new Texture2D(4, 4) { filterMode = FilterMode.Point };
                    var px = new Color[16];
                    for (int i = 0; i < 16; i++) px[i] = Color.white;
                    tex.SetPixels(px); tex.Apply();
                    dot = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 32f);
                }
                p.fallback = go.AddComponent<SpriteRenderer>();
                p.fallback.sprite = dot;
                p.fallback.color = tint;
                p.fallback.sortingOrder = RelicFx.SortingOrder;
                go.transform.localScale = Vector3.one * Mathf.Max(0.5f, hitRadius * 2.5f);
            }
            p.Face();
            return p;
        }

        void Face()
        {
            if (vel.sqrMagnitude < 0.0001f) return;
            float a = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, a);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= life) { if (explodeRadius > 0f) Explode(); Destroy(gameObject); return; }

            switch (mode)
            {
                case Mode.Homing:
                {
                    var t = RelicFx.Nearest(transform.position, 1, 8f, hit);
                    if (t.Count > 0)
                    {
                        Vector2 want = (RelicFx.Center(t[0]) - (Vector2)transform.position).normalized * speed;
                        vel = Vector2.Lerp(vel, want, Mathf.Clamp01(turnRate * dt));
                        vel = vel.normalized * speed;
                    }
                    break;
                }
                case Mode.Falling:
                    vel += new Vector2(0f, -18f) * dt;
                    break;
                case Mode.Boomerang:
                    if (!returning && age >= life * 0.45f) { returning = true; hit.Clear(); }
                    if (returning && owner != null)
                        vel = ((Vector2)owner.position - (Vector2)transform.position).normalized * speed;
                    break;
            }
            transform.position += (Vector3)(vel * dt);
            Face();

            if (mode == Mode.Falling && GroundBelow()) { Explode(); Destroy(gameObject); return; }

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
            {
                if (hit.Contains(col) || !RelicFx.IsTarget(col, out _)) continue;
                hit.Add(col);
                if (explodeRadius > 0f) { Explode(); Destroy(gameObject); return; }
                RelicFx.Hit(col, atkMult, Mathf.Sign(vel.x == 0f ? 1f : vel.x), 1.2f);
                if (pierceLeft == 0) { Destroy(gameObject); return; }
                if (pierceLeft > 0) pierceLeft--;
            }
        }

        bool GroundBelow()
        {
            if (vel.y >= 0f) return false;
            foreach (var col in Physics2D.OverlapCircleAll(transform.position, 0.1f))
                if (!col.isTrigger && !col.CompareTag("Enemy") && !col.CompareTag("Player")) return true;
            return false;
        }

        void Explode()
        {
            RelicFx.Circle(transform.position, explodeRadius, atkMult, ringColor, explodeKey);
        }
    }

    /// <summary>
    /// 잠깐 남아 적을 끌어당기다가 폭발하는 장(중력 태엽·종이새 떼) — 보스는 끌려오지 않는다(<see cref="ICrowdControlImmune"/>).
    /// </summary>
    public class RelicPullZone : MonoBehaviour
    {
        float t, life, radius, pull, atkMult;
        Color color;
        string burstKey;
        VfxPlayer art;

        public static RelicPullZone Spawn(Vector2 pos, float radius, float life, float pull, float atkMult, Color color,
                                          string loopKey, string burstKey)
        {
            var go = new GameObject("RelicPullZone");
            go.transform.position = pos;
            RunTransient.Mark(go);
            var z = go.AddComponent<RelicPullZone>();
            z.radius = radius; z.life = life; z.pull = pull; z.atkMult = atkMult; z.color = color; z.burstKey = burstKey;
            if (loopKey != null)
            {
                z.art = VfxPlayer.Spawn(loopKey, pos, DotScale.Value, 15f, RelicFx.SortingOrder, loop: true);
                if (z.art != null) z.art.transform.SetParent(go.transform, true);
            }
            RangeRing.Show(pos, radius, color, life);
            return z;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            t += dt;
            Vector2 c = transform.position;
            foreach (var col in Physics2D.OverlapCircleAll(c, radius * 1.4f))
            {
                if (!RelicFx.IsTarget(col, out _)) continue;
                if (col.GetComponent<ICrowdControlImmune>() != null) continue;
                var g = col.GetComponent<IGravityAffectable>();
                if (g == null) continue;
                Vector2 to = c - (Vector2)col.transform.position;
                float d = Mathf.Max(to.magnitude, 0.05f);
                g.ApplyGravityWellPull(to / d * pull * dt * new Vector2(1f, 0.3f));
            }
            if (t >= life)
            {
                RelicFx.Circle(c, radius, atkMult, color, burstKey);
                Destroy(gameObject);
            }
        }
    }
}
