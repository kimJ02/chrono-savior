using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 설정 창 같은 창이 떠 있는 동안 ESC를 창이 먼저 쓴다(2026-10-24, Figma UI). `RunController`의 일시정지 토글은
    /// <see cref="OwnsEscape"/>가 false일 때만 돈다 — 그래야 설정 창을 닫으려던 ESC가 일시정지까지 풀지 않는다.
    /// 창을 닫은 그 프레임에도 ESC는 창 몫이다(Update 순서가 정해져 있지 않아서 — 창이 먼저 닫혀도 같은 ESC로 일시정지가 안 풀리게).
    /// UI 어셈블리는 Systems보다 위라 Systems가 UI를 직접 못 본다 — 그래서 Core에 둔다.
    /// </summary>
    public static class UiModal
    {
        static int depth;
        static int closedFrame = -1;

        public static bool Open => depth > 0;
        public static bool OwnsEscape => depth > 0 || closedFrame == Time.frameCount;

        public static void Push() => depth++;

        public static void Pop()
        {
            if (depth > 0) depth--;
            closedFrame = Time.frameCount;
        }

        public static void Reset()
        {
            depth = 0;
            closedFrame = -1;
        }
    }
}
