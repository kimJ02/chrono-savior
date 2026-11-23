using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 공용 팝업 — Figma "공용 팝업 업데이트"(53:32, 640×360). 2026-11-16 새 판(팀원 zip + Figma): 창 틀 512×304(64,28) · 오른쪽 위 X 닫기(40×40) ·
    /// 버튼 셋(작은 96×32 왼쪽 · 큰 176×32 가운데 · 작은 96×32 오른쪽). 묻기(<see cref="Confirm"/>)는 왼쪽 취소·오른쪽 확인, 알림(<see cref="Alert"/>)은
    /// 가운데 큰 확인 하나(Codex 2026-11-16). X·ESC는 묻기면 취소, 알림이면 확인과 같다(콜백은 한 번만). Figma에 글자 자리가 없어 제목·내용은 예전 자리.
    /// 되돌릴 수 없는 일(새 원정으로 기록 지우기 등)을 한 번 더 묻거나 알릴 때 쓴다 — 예전 IMGUI는 "정말? 기록 삭제"처럼
    /// 버튼 글자를 바꿔서 물었다. 키보드: ←/→로 고르고 Enter·Space·Z, ESC는 취소.
    ///
    /// **모양은 프리팹이 원본이다(2026-11-23 프리팹 UI 2호)** — 위치·크기·그림은 <see cref="CommonPopupView"/> 프리팹에서 고친다.
    /// </summary>
    public class CommonPopup : PxScreen
    {
        static CommonPopup instance;

        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Popup;
        protected override bool Modal => true;
        protected override bool WantsVisible => open;
        protected override int DefaultNav => 2; // 확인

        Text title, body, okLabel, cancelLabel, wideLabel;
        PxButton ok, cancel, wide;
        bool open, asking;
        System.Action onOk, onCancel;

        public static bool IsOpen => instance != null && instance.open;

        /// <summary>확인/취소를 묻는다. 확인을 누르면 <paramref name="onOk"/>.</summary>
        public static void Confirm(string title, string body, System.Action onOk, string okText = "확인",
                                   string cancelText = "취소", System.Action onCancel = null)
        {
            if (instance == null) { onOk?.Invoke(); return; } // 판이 없으면(테스트 등) 묻지 않고 그대로 진행
            instance.Show(title, body, onOk, okText, cancelText, onCancel, twoButtons: true);
        }

        /// <summary>알리기만 한다(확인 버튼 하나).</summary>
        public static void Alert(string title, string body, System.Action onOk = null)
        {
            if (instance == null) { onOk?.Invoke(); return; }
            instance.Show(title, body, onOk, "확인", null, null, twoButtons: false);
        }

        /// <summary>
        /// 화면 전환(`GameFlow`)이 실패하면 알린다 — 전환 함수의 완료 콜백으로 넘긴다. 실패하면 흐름이 이전 화면으로 되돌려 놓았다(2026-11-05 씬 분할 2차 3-4).
        /// 성공하면 <paramref name="onSuccess"/>.
        /// </summary>
        public static System.Action<bool> AlertIfTransitionFails(string title, System.Action onSuccess = null) => ok =>
        {
            if (ok) onSuccess?.Invoke();
            else Alert(title, "화면을 불러오지 못했다.\n다시 시도해 줘.");
        };

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (instance == this) instance = null;
        }

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

        /// <summary>
        /// 모양은 프리팹(<see cref="CommonPopupView"/>, `Resources/UI/Views/CommonPopupView`)이 원본이다(2026-11-23 프리팹 UI 2호).
        /// 여기선 글자 자리·버튼을 받아 동작과 키보드 순서(취소 → 큰 확인 → 확인, X는 순서 밖)만 연결한다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            if (instance == null) instance = this;
            var view = LoadView<CommonPopupView>(CommonPopupView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[CommonPopup] 화면 프리팹(Resources/" + CommonPopupView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/공용 팝업 만들기", this);
                view = CommonPopupView.CreateFromCode(Root);
            }
            if (view.ButtonCount != 4)
                Debug.LogError($"[CommonPopup] 화면 프리팹 버튼이 {view.ButtonCount}개다 — 4개(X·취소·큰 확인·확인)여야 한다", view);

            title = view.Title;
            body = view.Body;
            view.WirePopupButton(CommonPopupView.Slot.Close, Dismiss); // X — 키보드 순서 밖(예전과 같다)
            cancel = AddNav(view.WirePopupButton(CommonPopupView.Slot.Cancel, () => Close(false)));
            wide = AddNav(view.WirePopupButton(CommonPopupView.Slot.Wide, () => Close(true)));
            ok = AddNav(view.WirePopupButton(CommonPopupView.Slot.Ok, () => Close(true)));
            cancelLabel = view.Label(CommonPopupView.Slot.Cancel);
            wideLabel = view.Label(CommonPopupView.Slot.Wide);
            okLabel = view.Label(CommonPopupView.Slot.Ok);
        }

        void Show(string t, string b, System.Action yes, string okText, string cancelText, System.Action no, bool twoButtons)
        {
            EnsureBuilt();
            title.text = t;
            body.text = b;
            onOk = yes;
            onCancel = no;
            asking = twoButtons;
            okLabel.text = wideLabel.text = okText;
            cancel.gameObject.SetActive(twoButtons);
            ok.gameObject.SetActive(twoButtons);
            wide.gameObject.SetActive(!twoButtons);
            if (twoButtons) cancelLabel.text = cancelText;
            open = true;
        }

        /// <summary>X·ESC — 묻기면 취소, 알림이면 확인(알림의 콜백이 빠지지 않게).</summary>
        void Dismiss() { if (open) Close(!asking); }

        /// <summary>캡처·테스트용 — 지금 보이는 버튼(왼쪽 취소, 가운데 큰 확인, 오른쪽 확인).</summary>
        public bool CancelShown => cancel != null && cancel.gameObject.activeSelf;
        public bool WideShown => wide != null && wide.gameObject.activeSelf;
        public bool OkShown => ok != null && ok.gameObject.activeSelf;
        public void DismissForTest() => Dismiss();

        void Close(bool confirmed)
        {
            if (!open) return; // 같은 프레임에 두 번 눌려도 콜백은 한 번
            open = false;
            var action = confirmed ? onOk : onCancel;
            onOk = onCancel = null;
            action?.Invoke();
        }

        protected override void OnShown() => SelectNav(asking ? 2 : 1);

        protected override void OnEscape() => Dismiss();
    }
}
