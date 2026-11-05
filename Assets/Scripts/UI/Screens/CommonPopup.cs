using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 공용 팝업 — Figma "공용 팝업 업데이트"(53:32, 640×360): 창 틀(512×288) · 제목 · 내용 · 취소/확인 버튼(96×32).
    /// 되돌릴 수 없는 일(새 원정으로 기록 지우기 등)을 한 번 더 묻거나 알릴 때 쓴다 — 예전 IMGUI는 "정말? 기록 삭제"처럼
    /// 버튼 글자를 바꿔서 물었다. 키보드: ←/→로 고르고 Enter·Space·Z, ESC는 취소.
    /// </summary>
    public class CommonPopup : PxScreen
    {
        static CommonPopup instance;

        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Popup;
        protected override bool Modal => true;
        protected override bool WantsVisible => open;
        protected override int DefaultNav => 1; // 확인

        // Figma 좌표(배경 -961·-954를 원점으로 뺀 값)
        static readonly Rect Frame = new Rect(64f, 36f, 512f, 288f);      // 288:19 "창 프레임 1"
        static readonly Rect TitleBox = new Rect(280f, 58f, 80f, 20f);    // 290:26 "팝업창" 16px
        static readonly Rect BodyBox = new Rect(104f, 104f, 432f, 136f);  // 290:24 "내용" 16px (280·119 자리에서 여러 줄이 들어가게 넓힘)
        static readonly Rect CancelBox = new Rect(137f, 259f, 96f, 32f);  // 289:21 + 290:23 "취소"
        static readonly Rect OkBox = new Rect(419f, 259f, 96f, 32f);      // 290:27 + 290:28 "확인"
        static readonly Rect OkCenter = new Rect(272f, 259f, 96f, 32f);   // 알림(버튼 하나)일 때는 가운데

        Text title, body, okLabel, cancelLabel;
        PxButton ok, cancel;
        bool open;
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

        protected override void Build()
        {
            if (instance == null) instance = this;
            Px.FullFill(Back, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, 0.5f));
            Px.Image(Content, "Frame", PxArt.Get("Common/window_frame"), Frame.x, Frame.y, Frame.width, Frame.height)
              .raycastTarget = true;
            title = Px.Text(Content, "Title", "", TitleBox.x - 120f, TitleBox.y, TitleBox.width + 240f, TitleBox.height, 16, Color.white);
            body = Px.Text(Content, "Body", "", BodyBox.x, BodyBox.y, BodyBox.width, BodyBox.height, 16, Color.white);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;

            cancel = AddNav(Px.TextButton(Content, "Cancel", PxArt.Get("Common/button"), CancelBox.x, CancelBox.y,
                                          CancelBox.width, CancelBox.height, "취소", 16, Color.white, () => Close(false)));
            ok = AddNav(Px.TextButton(Content, "Ok", PxArt.Get("Common/button"), OkBox.x, OkBox.y,
                                      OkBox.width, OkBox.height, "확인", 16, Color.white, () => Close(true)));
            cancelLabel = cancel.Inner.GetComponentInChildren<Text>();
            okLabel = ok.Inner.GetComponentInChildren<Text>();
        }

        void Show(string t, string b, System.Action yes, string okText, string cancelText, System.Action no, bool twoButtons)
        {
            EnsureBuilt();
            title.text = t;
            body.text = b;
            onOk = yes;
            onCancel = no;
            okLabel.text = okText;
            cancel.gameObject.SetActive(twoButtons);
            if (twoButtons) cancelLabel.text = cancelText;
            var okRt = (RectTransform)ok.transform;
            var r = twoButtons ? OkBox : OkCenter;
            okRt.anchoredPosition = new Vector2(r.x, -r.y);
            open = true;
        }

        void Close(bool confirmed)
        {
            open = false;
            var action = confirmed ? onOk : onCancel;
            onOk = onCancel = null;
            action?.Invoke();
        }

        protected override void OnShown()
        {
            if (!cancel.gameObject.activeSelf) SelectNav(1);
        }

        protected override void OnEscape() => Close(false);
    }
}
