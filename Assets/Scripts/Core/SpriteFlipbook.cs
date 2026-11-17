using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 프레임 묶음 하나 — 그림 몇 장을 몇 fps로, 되풀이할지(2026-11-17 애니메이션 플립북 전환, `docs/animation-flipbook-proposal.md` 3절).
    /// 그림은 **직렬화 참조**다(경로 문자열 로드 아님 — 월드 그림 직렬화 원칙). 재생 시간은 쓰는 쪽이 들고 있고 여기선 "t초면 몇 번째"만 답한다
    /// — 그래서 같은 에셋을 플레이어·로비·보스가 동시에 써도 서로 안 엉킨다.
    /// 캐릭터 몸 프레임 에셋은 에디터 메뉴가 원본 프레임 폴더에서 만든다(`Editor/BuildCharacterFlipbooks` — 손으로 고치지 않는다).
    /// </summary>
    [CreateAssetMenu(menuName = "ChronoSavior/Sprite Flipbook", fileName = "Flipbook_")]
    public class SpriteFlipbook : ScriptableObject
    {
        public Sprite[] frames = new Sprite[0];
        [Min(0f)] public float fps = 12f;
        public bool loop = true;

        public int Count => frames != null ? frames.Length : 0;

        /// <summary>한 바퀴 길이(초). 그림이 없거나 fps가 0이면 0.</summary>
        public float Duration => Count == 0 || fps <= 0f ? 0f : Count / fps;

        /// <summary>t초(재생 시작부터)에 보일 프레임 번호 — 되풀이면 감고, 아니면 마지막에서 멈춘다. 그림이 없으면 -1.</summary>
        public int IndexAt(float t)
        {
            int n = Count;
            if (n == 0) return -1;
            if (fps <= 0f) return 0;
            int i = Mathf.FloorToInt(Mathf.Max(0f, t) * fps);
            return loop ? i % n : Mathf.Min(i, n - 1);
        }

        /// <summary>t초에 보일 그림(없으면 null).</summary>
        public Sprite FrameAt(float t)
        {
            int i = IndexAt(t);
            return i < 0 ? null : frames[i];
        }

        /// <summary>되풀이 여부와 상관없이 한 번 돌고 마지막 프레임에서 멈춘 그림(누르는 동안 자세를 붙잡을 때).</summary>
        public Sprite FrameAtHeld(float t)
        {
            int n = Count;
            if (n == 0) return null;
            int i = fps <= 0f ? 0 : Mathf.Min(Mathf.FloorToInt(Mathf.Max(0f, t) * fps), n - 1);
            return frames[i];
        }

        /// <summary>한 번만 도는 묶음이 다 돌았나(되풀이는 늘 false).</summary>
        public bool IsFinished(float t) => !loop && Count > 0 && t >= Duration;
    }
}
