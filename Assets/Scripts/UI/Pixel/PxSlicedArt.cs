using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 9-슬라이스 그림(<see cref="PxArt.Sliced"/>)을 프리팹에서 쓰게 하는 표시(2026-11-23, 프리팹 UI 10호에서 발견).
    /// `PxArt.Sliced`는 실행 중에 만든 그림이라 프리팹에 저장되지 않는다 — 저장하면 그림 칸이 비어 흰 네모가 된다.
    /// 그래서 프리팹엔 **그림 경로·테두리 두께만** 남기고, 불러올 때(<see cref="Awake"/>) 이 컴포넌트가 그림을 다시 만들어 넣는다.
    /// 화면 코드가 버튼을 연결(<see cref="PxButton.Init"/>)하기 전에 불러야 버튼 상태 그림도 이 그림을 바탕으로 한다 — 뷰의 버튼 연결(`Wire`)이 먼저 <see cref="Apply"/>를 부른다(꺼진 채 불러와 Awake가 아직 안 돌았을 때를 위해).
    /// 에디터(플레이 전)에선 저장되지 않는 `overrideSprite`로 미리 보여 준다 — 프리팹 파일엔 아무것도 안 바뀐다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class PxSlicedArt : MonoBehaviour
    {
        [Tooltip("Resources/Art/Figma/UI/ 아래 그림 경로(예: Common/button)")]
        public string artPath = "Common/button";
        [Tooltip("늘리지 않을 가장자리 두께(도트)")]
        [Min(0)] public int border = 4;

        void Awake() => Apply();
        void OnEnable() => Apply();

        /// <summary>그림을 다시 만들어 넣는다 — 실행 중엔 진짜 그림 칸에, 에디터에선 미리보기(저장 안 됨)로.</summary>
        public void Apply()
        {
            var img = GetComponent<Image>();
            if (img == null || string.IsNullOrEmpty(artPath)) return;
            var s = PxArt.Sliced(artPath, border);
            img.type = Image.Type.Sliced;
            if (Application.isPlaying)
            {
                img.overrideSprite = null;
                if (img.sprite == null) img.sprite = s; // 이미 버튼이 상태 그림을 넣었으면 건드리지 않는다
            }
            else img.overrideSprite = s;
        }
    }
}
