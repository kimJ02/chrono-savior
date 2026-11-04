using System.Collections.Generic;
using UnityEngine;

namespace ChronoSavior.UI
{
    /// <summary>
    /// Figma UI 그림(`Resources/Art/Figma/UI/`, 디자이너가 올린 원본 PNG 그대로)을 불러오고, 버튼 상태 그림을 만든다.
    ///
    /// Figma엔 버튼의 기본 상태만 있다 — 마우스 올림·눌림·비활성은 사용자 허락(2026-10-23 "버튼 눌림·비활성은 만들어도 된다")으로
    /// **원본 그림의 밝기만 바꿔** 만든다. 테두리(아주 어두운 픽셀)는 그대로 둬서 모양이 안 변한다.
    /// </summary>
    public static class PxArt
    {
        const string Root = "Art/Figma/UI/";

        public enum Tone { Normal, Hover, Pressed, Disabled }

        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        static readonly Dictionary<(Sprite, Tone), Sprite> toned = new Dictionary<(Sprite, Tone), Sprite>();

        /// <summary>`UI/` 아래 경로(확장자 빼고), 예: "Common/button".</summary>
        public static Sprite Get(string path)
        {
            if (cache.TryGetValue(path, out var s) && s != null) return s;
            s = Resources.Load<Sprite>(Root + path);
            if (s == null) Debug.LogWarning("[PxArt] Figma 그림이 없다: " + Root + path);
            cache[path] = s;
            return s;
        }

        /// <summary>마우스 올림은 밝게, 눌림은 어둡게, 비활성은 회색으로.</summary>
        public const float HoverLighten = 0.16f;
        public const float PressedDarken = 0.18f;
        // 이보다 어두운 픽셀은 테두리로 보고 안 건드린다(Figma 테두리 #0e071b·#1b1b3a 등).
        const float OutlineLuma = 0.16f;

        public static Sprite Toned(Sprite s, Tone tone)
        {
            if (s == null || tone == Tone.Normal) return s;
            if (toned.TryGetValue((s, tone), out var t) && t != null) return t;
            if (!s.texture.isReadable) return s; // 가져오기 설정이 안 맞으면 기본 그림이라도

            Rect r = s.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            var px = s.texture.GetPixels(Mathf.RoundToInt(r.x), Mathf.RoundToInt(r.y), w, h);
            for (int i = 0; i < px.Length; i++) px[i] = Apply(px[i], tone);

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = s.name + "_" + tone,
            };
            tex.SetPixels(px);
            tex.Apply();
            var pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
            t = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, s.pixelsPerUnit, 0, SpriteMeshType.FullRect, s.border);
            t.name = tex.name;
            toned[(s, tone)] = t;
            return t;
        }

        static readonly Dictionary<(Sprite, string), Sprite> derived = new Dictionary<(Sprite, string), Sprite>();

        /// <summary>
        /// 원본 그림의 픽셀 색만 바꾼 그림(원본 파일은 그대로) — 예: HUD 틀에서 빨간 체력 칸만 남긴 그림, 칸을 비운 그림.
        /// 막대처럼 값에 따라 달라져야 하는 부분을 원본 그림 모양 그대로 그리려고 쓴다. <paramref name="key"/>로 캐시한다.
        /// </summary>
        public static Sprite Derive(Sprite s, string key, System.Func<Color32, Color32> map)
        {
            if (s == null || !s.texture.isReadable) return s;
            if (derived.TryGetValue((s, key), out var d) && d != null) return d;
            Rect r = s.textureRect;
            int w = Mathf.RoundToInt(r.width), h = Mathf.RoundToInt(r.height);
            var src = s.texture.GetPixels32();
            int texW = s.texture.width;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    px[y * w + x] = map(src[(y + Mathf.RoundToInt(r.y)) * texW + x + Mathf.RoundToInt(r.x)]);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = s.name + "_" + key,
            };
            tex.SetPixels32(px);
            tex.Apply();
            var pivot = new Vector2(s.pivot.x / s.rect.width, s.pivot.y / s.rect.height);
            d = Sprite.Create(tex, new Rect(0, 0, w, h), pivot, s.pixelsPerUnit, 0, SpriteMeshType.FullRect, s.border);
            d.name = tex.name;
            derived[(s, key)] = d;
            return d;
        }

        public static bool Same(Color32 a, Color32 b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a > 0;

        /// <summary>그림에서 그 색이 칠해진 가로 범위(그림 왼쪽 기준 도트, 없으면 0·0) — 막대 채움 칸을 그림에서 재려고.</summary>
        public static Vector2Int ColorSpanX(Sprite s, Color32 c)
        {
            if (s == null || !s.texture.isReadable) return Vector2Int.zero;
            Rect r = s.textureRect;
            var px = s.texture.GetPixels32();
            int texW = s.texture.width, x0 = Mathf.RoundToInt(r.x), y0 = Mathf.RoundToInt(r.y);
            int min = int.MaxValue, max = -1;
            for (int y = 0; y < (int)r.height; y++)
                for (int x = 0; x < (int)r.width; x++)
                    if (Same(px[(y + y0) * texW + x + x0], c)) { min = Mathf.Min(min, x); max = Mathf.Max(max, x); }
            return max < 0 ? Vector2Int.zero : new Vector2Int(min, max);
        }

        static readonly Dictionary<(string, int), Sprite> sliced = new Dictionary<(string, int), Sprite>();

        /// <summary>
        /// 9-슬라이스 그림 — 가장자리 <paramref name="border"/>도트는 그대로 두고 가운데만 늘린다(Figma 버튼을 다른 크기로 쓸 때,
        /// 사용자 10/23 "9-slice 창은 만들어도 된다"). `Image.type = Sliced`로 그린다.
        /// </summary>
        public static Sprite Sliced(string path, int border)
        {
            if (sliced.TryGetValue((path, border), out var s) && s != null) return s;
            var src = Get(path);
            if (src == null) return null;
            s = Sprite.Create(src.texture, src.textureRect, new Vector2(0.5f, 0.5f), src.pixelsPerUnit, 0,
                              SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            s.name = src.name + "_sliced";
            sliced[(path, border)] = s;
            return s;
        }

        public static Color Apply(Color c, Tone tone)
        {
            if (c.a <= 0f) return c;
            float luma = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            switch (tone)
            {
                case Tone.Hover:
                    if (luma < OutlineLuma) return c;
                    return new Color(c.r + (1f - c.r) * HoverLighten, c.g + (1f - c.g) * HoverLighten,
                                     c.b + (1f - c.b) * HoverLighten, c.a);
                case Tone.Pressed:
                    if (luma < OutlineLuma) return c;
                    float k = 1f - PressedDarken;
                    return new Color(c.r * k, c.g * k, c.b * k, c.a);
                case Tone.Disabled:
                    float g = Mathf.Lerp(luma, 0.5f, 0.15f) * 0.8f;
                    return new Color(Mathf.Lerp(c.r, g, 0.75f), Mathf.Lerp(c.g, g, 0.75f), Mathf.Lerp(c.b, g, 0.75f), c.a);
                default:
                    return c;
            }
        }
    }
}
