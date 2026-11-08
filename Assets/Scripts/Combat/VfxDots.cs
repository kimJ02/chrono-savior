using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 그림 없이 작은 점(4px 정사각형)으로 만드는 단순 입자 — 그림만이다(2026-11-09 VFX). 세 가지 움직임만:
    /// - <see cref="Motion.Rise"/>: 둘레에서 생겨 위로 떠오르며 사라진다(화상 불씨).
    /// - <see cref="Motion.Inward"/>: 둘레(반지름)에서 생겨 가운데로 빨려 들며 사라진다(블랙홀 끌어당김).
    /// - <see cref="Motion.Burst"/>: 가운데서 한 번 사방으로 튀고 끝난다(중첩 반짝임). 다 사라지면 오브젝트도 지운다.
    /// 붙인 오브젝트를 따라다닌다(자식). 시간은 게임 시간이라 일시정지·히트스톱 땐 같이 멈춘다.
    /// </summary>
    public class VfxDots : MonoBehaviour
    {
        public enum Motion { Rise, Inward, Burst }

        struct Dot { public SpriteRenderer sr; public Vector2 pos, vel; public float t, life; }

        Motion motion;
        float radius, life, speed;
        Color color;
        readonly List<Dot> dots = new List<Dot>();
        static Sprite dotSprite;

        /// <summary>끊임없이 다시 생기는지(Rise·Inward). 끄면 남은 점만 사라지고 더 안 생긴다.</summary>
        public bool Emitting { get; set; } = true;

        /// <summary>점 무리를 붙인다 — <paramref name="radius"/>는 생기는 둘레(유닛), <paramref name="life"/>는 점 하나의 수명(초).</summary>
        public static VfxDots Attach(Transform parent, Motion motion, int count, Color color, float radius, float life, float speed, int sortingOrder)
        {
            var go = new GameObject("VfxDots_" + motion);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            var s = parent.lossyScale; // 부모 배율과 무관하게 점 크기·거리는 월드 기준(그림 4px = 격자 4칸)
            go.transform.localScale = new Vector3(1f / Mathf.Max(0.01f, Mathf.Abs(s.x)), 1f / Mathf.Max(0.01f, Mathf.Abs(s.y)), 1f);
            var d = go.AddComponent<VfxDots>();
            d.motion = motion;
            d.radius = radius;
            d.life = Mathf.Max(0.05f, life);
            d.speed = speed;
            d.color = color;
            d.Emitting = motion != Motion.Burst;
            for (int i = 0; i < count; i++)
            {
                var dgo = new GameObject("Dot", typeof(SpriteRenderer));
                dgo.transform.SetParent(go.transform, false);
                var sr = dgo.GetComponent<SpriteRenderer>();
                sr.sprite = DotSprite();
                sr.sortingOrder = sortingOrder;
                var dot = new Dot { sr = sr };
                d.Respawn(ref dot);
                if (motion != Motion.Burst) dot.t = Random.value * dot.life; // 제각각 다른 때에 시작
                d.dots.Add(dot);
            }
            return d;
        }

        /// <summary>점이 지금 몇 개 보이나(테스트용).</summary>
        public int VisibleCount { get { int n = 0; foreach (var d in dots) if (d.sr.enabled) n++; return n; } }

        public void SetRadius(float r) => radius = r;

        void Respawn(ref Dot d)
        {
            d.t = 0f;
            d.life = life * Random.Range(0.7f, 1.3f);
            float a = Random.value * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            switch (motion)
            {
                case Motion.Rise:
                    d.pos = new Vector2(Random.Range(-radius, radius), Random.Range(-radius * 0.5f, radius * 0.5f));
                    d.vel = new Vector2(Random.Range(-0.15f, 0.15f), speed * Random.Range(0.7f, 1.3f));
                    break;
                case Motion.Inward:
                    d.pos = dir * radius * Random.Range(0.8f, 1.1f);
                    d.vel = -dir * (radius / d.life) * Random.Range(0.8f, 1.1f);
                    break;
                default: // Burst
                    d.pos = Vector2.zero;
                    d.vel = dir * speed * Random.Range(0.5f, 1.2f);
                    break;
            }
            d.sr.enabled = true;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool any = false;
            for (int i = 0; i < dots.Count; i++)
            {
                var d = dots[i];
                if (!d.sr.enabled) continue;
                d.t += dt;
                if (d.t >= d.life)
                {
                    if (Emitting) Respawn(ref d);
                    else { d.sr.enabled = false; dots[i] = d; continue; }
                }
                d.pos += d.vel * dt;
                if (motion == Motion.Burst) d.vel *= Mathf.Max(0f, 1f - 3f * dt); // 튀었다가 멈춘다
                float k = d.t / d.life;
                float a = motion == Motion.Inward ? Mathf.Sin(Mathf.PI * k) : 1f - k; // 빨려 드는 점은 나타났다 사라지고, 나머지는 서서히 사라진다
                d.sr.transform.localPosition = d.pos;
                d.sr.color = new Color(color.r, color.g, color.b, color.a * a);
                dots[i] = d;
                any = true;
            }
            if (!any && !Emitting) Destroy(gameObject);
        }

        static Sprite DotSprite()
        {
            if (dotSprite == null)
                dotSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 128f);
            return dotSprite;
        }
    }
}
