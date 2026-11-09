using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 실행 중에 그리는 **도트 도형**(원·타원·점선 원·호) — 원본이 캔버스에 선·원으로 그리던 연출(판정 고리·엘리트 오라·드론 빛·설치기 범위)을
    /// 다른 그림과 같은 도트 크기로 옮기려고(그림 1px = 격자 <see cref="DotScale.Value"/>칸, 2026-11-09). 흰색으로 그려 두고 색은 렌더러가 칠한다.
    /// 같은 모양은 한 번만 만든다(캐시). 종류가 적고 정해져 있어(고리는 그림 4px 단위, 호는 16각, 타원은 몹·설치기 크기별) 지우지 않는다 —
    /// 지우면 이미 그 그림을 쓰는 설치기 범위·엘리트 오라가 사라진다(Codex 리뷰 2026-11-09).
    /// </summary>
    public static class DotShapes
    {
        /// <summary>도트 도형의 PPU — 그림 1px이 격자 도트 배율 칸.</summary>
        public static float Ppu => DotScale.GridPixelsPerUnit / DotScale.Value;
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        /// <summary>원 둘레(1px). <paramref name="dash"/> &gt; 0이면 그 길이(px)만큼 칠하고 같은 만큼 비우는 점선.</summary>
        public static Sprite Ring(float radius, int dash = 0)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(radius * Ppu));
            return Get($"ring{r}_{dash}", r * 2 + 3, r * 2 + 3, (x, y, c) =>
            {
                float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y));
                if (Mathf.Abs(d - r) >= 0.5f) return false;
                if (dash <= 0) return true;
                float along = (Mathf.Atan2(y - c.y, x - c.x) + Mathf.PI) * r; // 둘레 위 거리(px)
                return ((int)(along / dash)) % 2 == 0;
            });
        }

        /// <summary>속이 찬 타원(반지름 rx·ry 유닛).</summary>
        public static Sprite Ellipse(float rx, float ry)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(rx * 2f * Ppu)), h = Mathf.Max(2, Mathf.RoundToInt(ry * 2f * Ppu));
            return Get($"ell{w}x{h}", w, h, (x, y, c) =>
            {
                float dx = (x + 0.5f - w / 2f) / (w / 2f), dy = (y + 0.5f - h / 2f) / (h / 2f);
                return dx * dx + dy * dy <= 1f;
            }, center: false);
        }

        /// <summary>타원 둘레(1px).</summary>
        public static Sprite EllipseOutline(float rx, float ry)
        {
            int w = Mathf.Max(2, Mathf.RoundToInt(rx * 2f * Ppu)), h = Mathf.Max(2, Mathf.RoundToInt(ry * 2f * Ppu));
            return Get($"ello{w}x{h}", w, h, (x, y, c) =>
            {
                bool In(int px, int py)
                {
                    float dx = (px + 0.5f - w / 2f) / (w / 2f), dy = (py + 0.5f - h / 2f) / (h / 2f);
                    return dx * dx + dy * dy <= 1f;
                }
                return In(x, y) && (!In(x - 1, y) || !In(x + 1, y) || !In(x, y - 1) || !In(x, y + 1));
            }, center: false);
        }

        /// <summary>
        /// 원호(1px) — <paramref name="startStep"/>/<paramref name="steps"/> 바퀴에서 시작해 <paramref name="sweep"/>(라디안)만큼. 돌리는 대신 각도마다 다른 그림을 쓴다
        /// (그림을 회전시키면 도트가 비스듬히 깨진다).
        /// </summary>
        public static Sprite Arc(float radius, int startStep, int steps, float sweep)
        {
            int r = Mathf.Max(1, Mathf.RoundToInt(radius * Ppu));
            startStep = ((startStep % steps) + steps) % steps;
            float a0 = startStep * Mathf.PI * 2f / steps;
            return Get($"arc{r}_{startStep}_{steps}_{Mathf.RoundToInt(sweep * 100f)}", r * 2 + 3, r * 2 + 3, (x, y, c) =>
            {
                float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y));
                if (Mathf.Abs(d - r) >= 0.5f) return false;
                float a = Mathf.Repeat(Mathf.Atan2(y - c.y, x - c.x) - a0, Mathf.PI * 2f);
                return a <= sweep;
            });
        }

        static Sprite Get(string key, int w, int h, System.Func<int, int, Vector2, bool> on, bool center = true)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[w * h];
            var c = new Vector2(w / 2, h / 2);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = on(x, y, c) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
            tex.SetPixels32(px);
            tex.Apply();
            s = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), Ppu, 0, SpriteMeshType.FullRect);
            s.name = "DotShape_" + key;
            cache[key] = s;
            return s;
        }

        /// <summary>만들어 둔 도형을 모두 지운다(텍스처까지).</summary>
        public static void Clear()
        {
            foreach (var sp in cache.Values)
                if (sp != null) { Object.Destroy(sp.texture); Object.Destroy(sp); }
            cache.Clear();
        }
    }
}
