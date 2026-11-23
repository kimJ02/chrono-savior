using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 업적 목록의 **한 줄 모양**(2026-11-23 프리팹 UI 7호 — 첫 목록형). 업적 창 프리팹(`AchievementsScreenView`) 안의 `List/RowTemplate`가
    /// 이 컴포넌트를 단 견본 줄이고, 실행하면 업적 수만큼 복제해 격자로 늘어놓는다(<see cref="AchievementsScreenView.BuildRows"/>).
    /// 견본 줄 하나를 에디터에서 고치면(글꼴·크기·배경) 모든 줄이 따라 바뀐다. 글자 내용·색은 <see cref="AchievementsScreen"/>가 채운다.
    /// </summary>
    public class AchievementRowView : MonoBehaviour
    {
        [SerializeField] PxButton button;
        [Tooltip("눌릴 때 1도트 내려갈 안쪽")]
        [SerializeField] RectTransform inner;
        [SerializeField] Text label;

        public PxButton Button => button;
        public Text Label => label;

        /// <summary>코드로 지은 견본 줄에 참조를 채운다 — `AchievementsScreenView.CreateFromCode` 전용.</summary>
        public void Assign(PxButton b, RectTransform innerRect, Text text)
        {
            button = b;
            inner = innerRect;
            label = text;
        }

        /// <summary>
        /// 줄 버튼을 쓸 수 있게 한다 — 버튼 상태(마우스 올림·눌림)는 실행 중에 만들므로 복제한 뒤 여기서 다시 준비한다.
        /// </summary>
        public PxButton Wire()
        {
            if (button == null) return null;
            button.Init(button.GetComponent<Image>(), inner);
            return button;
        }

        public void Show(string text, Color color)
        {
            if (label == null) return;
            label.text = text;
            label.color = color;
        }
    }
}
