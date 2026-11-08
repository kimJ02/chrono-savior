using System.Collections.Generic;
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

        static readonly Dictionary<int, Sprite> cache = new Dictionary<int, Sprite>();
        const int CacheStepPx = 4, CacheLimit = 32;

        /// <summary>그려 둔 고리 그림을 모두 지운다(텍스처까지).</summary>
        public static void ClearCache()
        {
            foreach (var sp in cache.Values)
                if (sp != null) { Destroy(sp.texture); Destroy(sp); }
            cache.Clear();
        }
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

        /// <summary>반경(유닛)의 1px 도트 원 — 중점 원 그리기, 그림 PPU = 128 ÷ 도트 배율.</summary>
        public static Sprite RingSprite(float radius)
        {
            float ppu = DotScale.GridPixelsPerUnit / DotScale.Value;
            // 반경은 차지에 따라 연속으로 바뀐다 — 그림 4px 단위로 맞춰 캐시를 작게(눈으로는 구별 안 되는 차이), 너무 많아지면 비운다
            int rp = Mathf.Max(CacheStepPx, Mathf.RoundToInt(radius * ppu / CacheStepPx) * CacheStepPx);
            if (cache.TryGetValue(rp, out var s) && s != null) return s;
            if (cache.Count >= CacheLimit) ClearCache();
            int size = rp * 2 + 3;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            int c = size / 2;
            void Put(int x, int y) { if (x >= 0 && y >= 0 && x < size && y < size) px[y * size + x] = new Color32(255, 255, 255, 255); }
            for (int x = 0, y = rp, d = 1 - rp; x <= y; x++)
            {
                Put(c + x, c + y); Put(c - x, c + y); Put(c + x, c - y); Put(c - x, c - y);
                Put(c + y, c + x); Put(c - y, c + x); Put(c + y, c - x); Put(c - y, c - x);
                if (d < 0) d += 2 * x + 3; else { d += 2 * (x - y) + 5; y--; }
            }
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            s.name = "RangeRing_" + rp;
            cache[rp] = s;
            return s;
        }
    }
}
