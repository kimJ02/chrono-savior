using UnityEngine;
using UnityEngine.EventSystems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 눌러 끌어서 값을 고르는 칸(설정창 음량 막대, 2026-11-16 Figma 새 설정창) — 막대 위 가로 위치를 0~<see cref="Steps"/> 정수로 바꿔 <see cref="OnChange"/>로 알린다.
    /// 그림(바탕·채움·손잡이)은 화면이 따로 그린다 — 이 칸은 입력만 받는다.
    /// </summary>
    public class PxSlider : MonoBehaviour, IPointerDownHandler, IDragHandler
    {
        public int Steps = 10;
        public System.Action<int> OnChange;

        public void OnPointerDown(PointerEventData e) => Pick(e);
        public void OnDrag(PointerEventData e) => Pick(e);

        void Pick(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out var local)) return;
            OnChange?.Invoke(ValueAt(local.x - rt.rect.xMin, rt.rect.width, Steps));
        }

        /// <summary>막대 왼쪽 끝에서 <paramref name="x"/>만큼 떨어진 곳의 값(0~steps, 반올림).</summary>
        public static int ValueAt(float x, float width, int steps) =>
            width <= 0f ? 0 : Mathf.Clamp(Mathf.RoundToInt(x / width * steps), 0, steps);
    }
}
