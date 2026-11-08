using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 프레임 애니메이션 하나를 재생하는 **단순한 재생기** — 그림만 있는 효과(판정 없음, 생성 책임 D 분류). 위치·크기·언제 띄울지는 그 효과의 담당
    /// (예: 마법사 착탄은 `MageSkillEffects`)이 정하고, 이건 프레임을 넘기고 끝나면 사라지는 것만 한다(2026-11-07 VFX 스프린트).
    /// 시간은 게임 시간(`Time.deltaTime`)이라 일시정지·히트스톱 땐 같이 멈춘다. 런 오브젝트(`RunTransient`)라 필드를 떠나면 같이 지워진다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class VfxPlayer : MonoBehaviour
    {
        Sprite[] frames;
        float fps;
        bool loop;
        float fadeOut;
        float t;
        SpriteRenderer sr;
        Color baseColor = Color.white;
        float alpha = 1f;

        /// <summary>지금 보여 주는 프레임 번호(테스트용).</summary>
        public int Frame { get; private set; }
        /// <summary>한 번 재생이 끝났는지(반복 재생은 늘 false).</summary>
        public bool Finished { get; private set; }

        /// <summary>
        /// 바깥에서 곱하는 투명도 0~1 — 수명을 그 효과의 담당이 들고 있는 반복 재생(예: 불길 장판이 끝나 갈 때 흐려짐)용. 기본 1.
        /// </summary>
        public float Alpha
        {
            get => alpha;
            set { alpha = Mathf.Clamp01(value); if (frames != null && frames.Length > 0) Show(Frame, CurrentFade()); }
        }

        /// <summary>
        /// 그림을 `targetWidth`(유닛)에 가장 가깝게 보이게 하는 **정수** 배율(최소 1) — 도트 배율을 정수로 지키려고(`docs/pixel-standard.md`).
        /// 판정 크기는 그대로 두고 그림만 이 배율로 그린다.
        /// </summary>
        public static int WholeScale(float targetWidth, Sprite frame) =>
            frame == null ? 1 : Mathf.Max(1, Mathf.RoundToInt(targetWidth / frame.bounds.size.x));

        /// <summary>
        /// 그 키의 효과를 세운다. 그림이 없으면 null — 부르는 쪽은 지금 임시 그림으로 그린다.
        /// </summary>
        /// <param name="scale">배율 — 그림은 PPU 128이라 1이면 그림 한 픽셀 = 월드 격자 한 칸(도트 배율 1, `docs/pixel-standard.md`). 정수가 좋다.</param>
        /// <param name="fadeOutSeconds">끝나기 전 이만큼 동안 투명해진다(0이면 바로 사라짐).</param>
        public static VfxPlayer Spawn(string key, Vector3 position, float scale, float fps, int sortingOrder,
                                      bool loop = false, float fadeOutSeconds = 0f, Color? tint = null)
        {
            var frames = VfxFrames.Get(key);
            if (frames.Length == 0) return null;
            var go = new GameObject("Vfx_" + key.Replace('/', '_'));
            go.transform.position = position;
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            RunTransient.Mark(go);
            var player = go.AddComponent<VfxPlayer>();
            player.Play(frames, fps, loop, fadeOutSeconds, tint ?? Color.white);
            return player;
        }

        /// <summary>프레임을 처음부터 재생한다. 프레임이 없으면 아무것도 보이지 않고 곧바로 끝난다(사라진다).</summary>
        public void Play(Sprite[] sequence, float framesPerSecond, bool repeat, float fadeOutSeconds, Color tint)
        {
            sr = GetComponent<SpriteRenderer>();
            if (sequence == null || sequence.Length == 0)
            {
                frames = null;
                Finished = true;
                Destroy(gameObject);
                return;
            }
            frames = sequence;
            fps = Mathf.Max(0.01f, framesPerSecond);
            loop = repeat;
            fadeOut = Mathf.Max(0f, fadeOutSeconds);
            baseColor = tint;
            t = 0f;
            Finished = false;
            Show(0, 1f);
        }

        /// <summary>재생 중인 위치를 옮긴다(초) — 여러 개를 같이 세울 때 서로 다른 프레임에서 시작하게(예: 불길 장판).</summary>
        public void Skip(float seconds)
        {
            if (frames == null || frames.Length == 0) return;
            t = Mathf.Max(0f, seconds);
            int i = Mathf.FloorToInt(t * fps);
            Show(loop ? i % frames.Length : Mathf.Min(i, frames.Length - 1), CurrentFade());
        }

        /// <summary>한 번 재생에 걸리는 시간(초).</summary>
        public float Duration => frames == null ? 0f : frames.Length / fps;

        void Update()
        {
            if (frames == null || frames.Length == 0 || Finished) return;
            t += Time.deltaTime;
            int i = Mathf.FloorToInt(t * fps);
            if (loop) { Show(i % frames.Length, 1f); return; }
            if (i >= frames.Length)
            {
                Finished = true;
                Destroy(gameObject);
                return;
            }
            Show(i, CurrentFade());
        }

        /// <summary>한 번 재생이 끝나 갈 때의 투명도(fadeOut) — 반복 재생은 1.</summary>
        float CurrentFade() => !loop && fadeOut > 0f ? Mathf.Clamp01((Duration - t) / fadeOut) : 1f;

        void Show(int index, float fade)
        {
            Frame = index;
            sr.sprite = frames[index];
            sr.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade * alpha);
        }
    }
}
