using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// Figma 버튼 — 그림 한 장(기본 상태)에 마우스 올림·눌림·비활성을 입힌다(<see cref="PxArt.Toned"/>).
    /// 눌리는 동안 안의 글자·아이콘(<see cref="Inner"/>)이 1도트 내려간다 — 도트 게임 버튼의 흔한 눌림 표현.
    /// 키보드로 고른 버튼(<see cref="Selected"/>)은 마우스를 올린 것처럼 보인다. 고르는 건 <see cref="PxScreen"/>이 한다.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class PxButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler,
        IPointerUpHandler, IPointerClickHandler
    {
        public Image Art { get; private set; }
        /// <summary>눌릴 때 1도트 내려갈 안쪽(글자·아이콘). 없어도 된다.</summary>
        public RectTransform Inner { get; private set; }
        public Action OnClick;
        /// <summary>마우스가 올라왔을 때 — 화면이 키보드 선택을 이 버튼으로 옮긴다.</summary>
        public Action OnHover;

        Sprite baseSprite;
        Vector2 innerBase;
        CanvasGroup innerGroup;
        bool interactable = true, hovered, pressed, selected;

        public void Init(Image art, RectTransform inner)
        {
            Art = art;
            Art.raycastTarget = true;
            baseSprite = art.sprite;
            Inner = inner;
            if (inner != null)
            {
                innerBase = inner.anchoredPosition;
                if (!inner.TryGetComponent(out innerGroup)) innerGroup = inner.gameObject.AddComponent<CanvasGroup>();
                innerGroup.blocksRaycasts = false;
            }
            Apply();
        }

        public bool Interactable
        {
            get => interactable;
            set { if (interactable == value) return; interactable = value; Apply(); }
        }

        public bool Selected
        {
            get => selected;
            set { if (selected == value) return; selected = value; Apply(); }
        }

        /// <summary>바탕 그림을 바꾼다(켜짐/꺼짐 그림이 따로 있는 버튼).</summary>
        public void SetSprite(Sprite s)
        {
            baseSprite = s;
            Apply();
        }

        public void Click()
        {
            if (!interactable) return;
            SfxEvents.RaiseUiClick(); // 2026-11-11 공용 버튼 클릭음 — 버튼 종류 안 가리고 전부 여기서 한 번
            OnClick?.Invoke();
        }

        public void OnPointerEnter(PointerEventData e)
        {
            hovered = true;
            if (interactable) OnHover?.Invoke();
            Apply();
        }

        public void OnPointerExit(PointerEventData e)
        {
            hovered = false;
            pressed = false;
            Apply();
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left) return;
            pressed = true;
            Apply();
        }

        public void OnPointerUp(PointerEventData e)
        {
            pressed = false;
            Apply();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) Click();
        }

        void OnDisable()
        {
            hovered = pressed = false;
            Apply();
        }

        /// <summary>지금 보이는 상태(테스트용).</summary>
        public PxArt.Tone CurrentTone =>
            !interactable ? PxArt.Tone.Disabled
            : pressed ? PxArt.Tone.Pressed
            : hovered || selected ? PxArt.Tone.Hover
            : PxArt.Tone.Normal;

        void Apply()
        {
            if (Art == null) return;
            var tone = CurrentTone;
            Art.sprite = PxArt.Toned(baseSprite, tone);
            if (Inner != null)
            {
                Inner.anchoredPosition = innerBase + (tone == PxArt.Tone.Pressed ? new Vector2(0f, -1f) : Vector2.zero);
                if (innerGroup != null) innerGroup.alpha = interactable ? 1f : 0.55f;
            }
        }
    }
}
