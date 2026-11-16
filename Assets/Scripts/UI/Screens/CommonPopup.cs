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
    /// </summary>
    public class CommonPopup : PxScreen
    {
        static CommonPopup instance;

        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Popup;
        protected override bool Modal => true;
        protected override bool WantsVisible => open;
        protected override int DefaultNav => 2; // 확인

        // Figma 좌표(어두운 배경 -963·-952를 원점으로 뺀 값)
        static readonly Rect Frame = new Rect(64f, 28f, 512f, 304f);      // 447:18 "설정 배경 2"(창 틀 512×304)
        static readonly Rect CloseBox = new Rect(541f, 23f, 40f, 40f);    // 597:144 "나가기_버튼 1" + 469:14 "x 2"
        static readonly Rect TitleBox = new Rect(160f, 52f, 320f, 20f);   // 글자 자리는 Figma에 없다 — 옛 "팝업창"(290:26) 16px 자리
        static readonly Rect BodyBox = new Rect(104f, 92f, 432f, 156f);   // 옛 "내용"(290:24) 자리를 새 틀·버튼 높이에 맞춰
        static readonly Rect CancelBox = new Rect(113f, 268f, 96f, 32f);  // 289:21 작은 버튼(왼쪽)
        static readonly Rect WideBox = new Rect(232f, 268f, 176f, 32f);   // 590:19 "버튼long 1"(가운데)
        static readonly Rect OkBox = new Rect(445f, 268f, 96f, 32f);      // 290:27 작은 버튼(오른쪽)

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

        protected override void Build()
        {
            if (instance == null) instance = this;
            Px.FullFill(Back, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, 0.5f));
            Px.Image(Content, "Frame", PxArt.Get("Common/window_tall"), Frame.x, Frame.y, Frame.width, Frame.height)
              .raycastTarget = true;
            Px.IconButton(Content, "Close", PxArt.Get("Common/close_frame"), PxArt.Get("Icons/close_x"),
                          CloseBox.x, CloseBox.y, CloseBox.width, CloseBox.height, 0f, 0f, CloseBox.width, CloseBox.height, Dismiss);
            title = Px.Text(Content, "Title", "", TitleBox.x - 120f, TitleBox.y, TitleBox.width + 240f, TitleBox.height, 16, Color.white);
            body = Px.Text(Content, "Body", "", BodyBox.x, BodyBox.y, BodyBox.width, BodyBox.height, 16, Color.white);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.lineSpacing = 1.15f;

            cancel = AddNav(Px.TextButton(Content, "Cancel", PxArt.Get("Common/button"), CancelBox.x, CancelBox.y,
                                          CancelBox.width, CancelBox.height, "취소", 16, Color.white, () => Close(false)));
            wide = AddNav(Px.TextButton(Content, "OkWide", PxArt.Get("Common/button_wide"), WideBox.x, WideBox.y,
                                        WideBox.width, WideBox.height, "확인", 16, Color.white, () => Close(true)));
            ok = AddNav(Px.TextButton(Content, "Ok", PxArt.Get("Common/button"), OkBox.x, OkBox.y,
                                      OkBox.width, OkBox.height, "확인", 16, Color.white, () => Close(true)));
            cancelLabel = cancel.Inner.GetComponentInChildren<Text>();
            wideLabel = wide.Inner.GetComponentInChildren<Text>();
            okLabel = ok.Inner.GetComponentInChildren<Text>();
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
