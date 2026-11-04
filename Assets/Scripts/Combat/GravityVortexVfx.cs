using UnityEngine;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 중력장이 남아 있는 동안 계속 보이는 "끌어당기는" 소용돌이 — 2026-10-25 사용자 "중력장이 남아 있는데 이펙트가 끝난다, 블랙홀이
    /// 끌어당기는 효과를 보여줘야 한다". 착탄 폭발(<see cref="GravityImpactVfx"/>)과 같은 Gigapack 그림 중 **소용돌이가 도는 가운데 프레임
    /// (5~7번째)** 을 되풀이해서 돌리고, 바깥에서 안으로 빨려드는 것처럼 크기가 반복해서 줄어든다(회전은 안 시킨다 — 사용자 요청).
    /// 처음엔 착탄 폭발에 가려 안 보이다가 0.35~0.6초에 걸쳐 나타나고, 중력장이 끝나기 0.5초 전부터 흐려진다.
    /// **그림만이다** — 끌어당기는 힘·피해는 <see cref="GravityWellZone"/>이 그대로 한다. 중력장 오브젝트의 자식이라 같이 사라진다.
    /// </summary>
    public class GravityVortexVfx : MonoBehaviour
    {
        /// <summary>되풀이할 프레임 번호(0부터) — 소용돌이 줄기와 바깥 고리가 보이는 5~7번째 그림을 4→5→6→5로 오간다.</summary>
        static readonly int[] LoopOrder = { 4, 5, 6, 5 };
        public const float Fps = 15f;
        /// <summary>빨려드는 주기(초)와 크기 변화 — 1.0배로 시작해 이만큼까지 줄었다가 다시 처음으로.</summary>
        public const float PullPeriod = 0.9f, PullMinScale = 0.78f;
        public const float FadeInStart = 0.35f, FadeInEnd = 0.6f, FadeOutTime = 0.5f;
        const int SortingOrder = 4; // 중력점 고리(3) 위, 착탄 폭발(5) 아래
        const float MaxAlpha = 0.85f;

        GravityWellZone well;
        SpriteRenderer sr;
        Sprite[] frames;
        float radius = 1f;
        float clock;

        public static GravityVortexVfx Attach(GravityWellZone well)
        {
            var frames = GravityImpactVfx.Frames();
            if (frames.Length == 0) return null;
            var go = new GameObject("GravityVortex");
            go.transform.SetParent(well.transform, false);
            var v = go.AddComponent<GravityVortexVfx>();
            v.well = well;
            v.frames = frames;
            v.sr = go.AddComponent<SpriteRenderer>();
            v.sr.sortingOrder = SortingOrder;
            v.Apply(0f);
            return v;
        }

        public void SetRadius(float r) => radius = r;

        /// <summary>시간 t(초)에 보일 되풀이 프레임.</summary>
        public static int LoopFrameAt(float t, int count) =>
            Mathf.Min(LoopOrder[Mathf.FloorToInt(t * Fps) % LoopOrder.Length], count - 1);

        /// <summary>시간 t에 크기 배율 — 1 → <see cref="PullMinScale"/>로 줄었다 처음으로(톱니).</summary>
        public static float PullScale(float t)
        {
            float k = (t % PullPeriod) / PullPeriod;
            return Mathf.Lerp(1f, PullMinScale, k);
        }

        /// <summary>시간 t에 투명도 — 착탄 폭발이 끝날 즈음 나타나고, 중력장이 끝나기 직전에 사라진다.</summary>
        public static float AlphaAt(float t, float remaining)
        {
            float fadeIn = Mathf.InverseLerp(FadeInStart, FadeInEnd, t);
            float fadeOut = Mathf.Clamp01(remaining / FadeOutTime);
            return MaxAlpha * Mathf.Min(fadeIn, fadeOut);
        }

        void Update()
        {
            clock += Time.deltaTime; // 히트스톱(timeScale 0) 동안엔 같이 멈춘다
            Apply(clock);
        }

        /// <summary>캡처용 — 시간을 직접 정해 그린다.</summary>
        public void ApplyAt(float t, float remaining) => Apply(t, remaining);

        void Apply(float t) => Apply(t, well != null ? well.Remaining : 99f);

        void Apply(float t, float remaining)
        {
            if (sr == null) return;
            sr.sprite = frames[LoopFrameAt(t, frames.Length)];
            float size = radius * 2f / sr.sprite.bounds.size.x * PullScale(t);
            transform.localScale = Vector3.one * size;
            var c = sr.color;
            c.a = AlphaAt(t, remaining);
            sr.color = c;
        }
    }
}
