using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 유물 칸 하나의 **모양**(2026-11-23 프리팹 UI 11호) — 칸 버튼·등급 ◆(뒤 테두리 ◆)·자물쇠·개수 글자.
    /// 유물 창 프리팹 안의 견본 칸(`Slots/Template`, 꺼 둔 채)이 이 컴포넌트를 갖고, <see cref="RelicScreenView.BuildSlots"/>가 30칸으로 복제한다.
    /// 무엇을 보여 줄지(칸 그림 밝음/어두움, ◆ 색, 자물쇠, 개수·각성)는 <see cref="RelicScreen"/>가 정한다.
    /// </summary>
    public class RelicSlotView : MonoBehaviour
    {
        public PxButton button;
        public RectTransform inner;
        [Tooltip("◆ 뒤 한 도트 큰 어두운 ◆ — 밝은 칸 바탕에서 노말(연회색)도 보이게")]
        public Image gemEdge;
        public Image gem;
        public Image lockIcon;
        public Text count;

        /// <summary>버튼 연결 — 흰 ◆ 그림(실행 중 만든 그림)을 먼저 다시 넣고 버튼을 잇는다.</summary>
        public PxButton Wire()
        {
            foreach (var w in GetComponentsInChildren<PxWhiteArt>(true)) w.Apply();
            if (button == null) return null;
            button.Init(button.GetComponent<Image>(), inner);
            return button;
        }
    }
}
