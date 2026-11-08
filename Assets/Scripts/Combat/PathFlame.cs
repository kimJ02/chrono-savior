using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 화염돌진이 지나간 바닥에 붙는 불꽃 하나 — **그림만이다**(피해·화상은 <see cref="FireTrailZone"/> 장판, 원본 자리·개수 그대로).
    /// 2026-11-09 사용자 "바닥 화염이 인위적" → 장판 지점마다 기둥 하나씩 서던 것을 바꿨다(Codex 합의): 지나간 바닥을 따라 무작위 간격으로
    /// 여러 모양(Pixel Fire 5종)·크기·속도의 불꽃이 **지나가는 순서대로 점화**(아래에서 솟으며 커짐)하고, 끝날 땐 **사그라든다**(세로로 줄며 흐려짐).
    /// 밑동은 바닥(발 높이)에 맞춘다. 그림이 없으면(팩을 안 가져온 클론) 아무것도 안 생긴다.
    /// </summary>
    public class PathFlame : MonoBehaviour
    {
        /// <summary>섞어 쓰는 불꽃 모양(가늘고 긴·넓은·낮은·큰·작은).</summary>
        public static readonly string[] Shapes =
            { VfxKeys.MageFireTrail, VfxKeys.MageFireTrailWide, VfxKeys.MageFireTrailLow, VfxKeys.MageFireTrailBig, VfxKeys.MageFireTrailSmall };
        public const float IgniteTime = 0.15f, DieTime = 0.35f;
        /// <summary>정렬은 불길 장판과 같은 줄(캐릭터·적 뒤, 땅 위).</summary>
        public const int SortingOrder = -2;

        VfxPlayer player;
        SpriteRenderer sr;
        Vector2 feet;
        float delay, life, t, height;

        /// <summary>
        /// 불꽃 하나 — <paramref name="feetPos"/>(밑동, 바닥 높이)에 <paramref name="delay"/>초 뒤 점화해 <paramref name="life"/>초 타고(사그라드는 시간 포함) 사라진다.
        /// 모양·배율(1~2, 정수)·속도·뒤집기·시작 프레임은 무작위. 그림이 없으면 null.
        /// </summary>
        public static PathFlame Ignite(Vector2 feetPos, float delay, float life)
        {
            var frames = VfxFrames.Get(Shapes[Random.Range(0, Shapes.Length)]);
            if (frames.Length == 0) frames = VfxFrames.Get(VfxKeys.MageFireTrail);
            if (frames.Length == 0) return null;
            var go = new GameObject("PathFlame", typeof(SpriteRenderer));
            RunTransient.Mark(go);
            var f = go.AddComponent<PathFlame>();
            f.sr = go.GetComponent<SpriteRenderer>();
            f.sr.sortingOrder = SortingOrder;
            f.sr.flipX = Random.value < 0.5f;
            int scale = Random.value < 0.65f ? 2 : 1;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            f.player = go.AddComponent<VfxPlayer>();
            float fps = 12f * Random.Range(0.75f, 1.3f);
            f.player.Play(frames, fps, true, 0f, Color.white);
            f.player.Skip(Random.value * frames.Length / fps);
            f.feet = feetPos;
            f.delay = Mathf.Max(0f, delay);
            f.life = Mathf.Max(IgniteTime + DieTime, life);
            f.height = frames[0].bounds.size.y * scale;
            f.Apply();
            return f;
        }

        /// <summary>지금 세로 크기 비율 0~1(점화 중·사그라드는 중) — 테스트용.</summary>
        public float Grow { get; private set; }

        void Update()
        {
            t += Time.deltaTime;
            Apply();
            if (t >= delay + life) Destroy(gameObject);
        }

        void Apply()
        {
            float lt = t - delay;
            if (lt < 0f) { sr.enabled = false; Grow = 0f; return; }
            sr.enabled = true;
            float grow = Mathf.Clamp01(lt / IgniteTime);
            float die = Mathf.Clamp01((life - lt) / DieTime);
            Grow = Mathf.Min(grow, die);
            var s = transform.localScale;
            float baseScale = Mathf.Abs(s.x);
            transform.localScale = new Vector3(s.x, baseScale * Mathf.Max(0.05f, Grow), 1f);
            transform.position = feet + new Vector2(0f, height * Grow * 0.5f); // 밑동은 바닥에 붙어 있다
            player.Alpha = die;
        }
    }
}
