using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 판정 범위 고리 — 광역 공격이 터질 때 **판정 반경**에 1px 도트 원을 잠깐 보였다 지운다(그림만, 2026-11-09 Codex 합의).
    /// 도트 배율을 하나로 맞추면서(<see cref="DotScale"/>) 폭발 그림이 판정보다 작아져, 어디까지 맞는지는 이 고리가 보인다.
    /// 원은 실행 중에 도트로 그린 텍스처(그림 1px = 격자 <see cref="DotScale.Value"/>칸)라 다른 그림과 도트 크기가 같다 — 반경(px)마다 한 번만 만든다.
    /// </summary>
    public class RangeRing : MonoBehaviour
    {
        /// <summary>보이는 시간(초)·정렬(공격 판정 표시 줄 5 — CLAUDE.md).</summary>
        public const float Life = 0.3f;
        public const int SortingOrder = 5;

        SpriteRenderer sr;
        Color color;
        float t, life;

        /// <summary>고리 하나를 띄운다(<paramref name="life"/>초에 걸쳐 흐려진다). 반경이 0 이하면 아무것도 안 한다.</summary>
        public static RangeRing Show(Vector2 center, float radius, Color color, float life = Life)
        {
            if (radius <= 0f) return null;
            var go = new GameObject("RangeRing", typeof(SpriteRenderer));
            RunTransient.Mark(go);
            go.transform.position = center;
            var r = go.AddComponent<RangeRing>();
            r.sr = go.GetComponent<SpriteRenderer>();
            r.sr.sprite = RingSprite(radius);
            r.sr.sortingOrder = SortingOrder;
            r.color = color;
            r.life = Mathf.Max(0.01f, life);
            r.sr.color = color;
            return r;
        }

        void Update()
        {
            t += Time.deltaTime;
            sr.color = new Color(color.r, color.g, color.b, color.a * (1f - Mathf.Clamp01(t / life)));
            if (t >= life) Destroy(gameObject);
        }

        /// <summary>반경(유닛)의 1px 도트 원 — 차지에 따라 반경이 연속으로 바뀌어 그림 4px 단위로 맞춘다(눈으로는 구별 안 되는 차이, 그림 수를 작게).</summary>
        public static Sprite RingSprite(float radius)
        {
            float step = 4f / DotShapes.Ppu;
            return DotShapes.Ring(Mathf.Max(step, Mathf.Round(radius / step) * step));
        }
    }
}
