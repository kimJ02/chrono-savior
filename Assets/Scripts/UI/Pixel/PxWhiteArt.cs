using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 흰색으로 칠한 그림(<see cref="PxArt.White"/>)을 프리팹에서 쓰게 하는 표시(2026-11-23 프리팹 UI 11호, <see cref="PxSlicedArt"/>와 같은 이유).
    /// `PxArt.White`는 실행 중에 만든 그림이라 프리팹에 저장되지 않는다 — 프리팹엔 **원본 그림 경로만** 남기고, 불러올 때 다시 만들어 넣는다.
    /// 색은 `Image.color`로 입힌다(예: 유물 등급 색). 에디터(플레이 전)에선 저장되지 않는 `overrideSprite`로 미리 보여 준다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Image))]
    public class PxWhiteArt : MonoBehaviour
    {
        [Tooltip("Resources/Art/Figma/UI/ 아래 원본 그림 경로(예: Common/cursor) — 이 모양을 흰색으로 칠해 쓴다")]
        public string artPath = "Common/cursor";

        void Awake() => Apply();
        void OnEnable() => Apply();

        /// <summary>그림을 다시 만들어 넣는다 — 실행 중엔 진짜 그림 칸에, 에디터에선 미리보기(저장 안 됨)로.</summary>
        public void Apply()
        {
            var img = GetComponent<Image>();
            if (img == null || string.IsNullOrEmpty(artPath)) return;
            var s = PxArt.White(artPath);
            if (Application.isPlaying)
            {
                img.overrideSprite = null;
                if (img.sprite == null) img.sprite = s;
            }
            else img.overrideSprite = s;
        }
    }
}
