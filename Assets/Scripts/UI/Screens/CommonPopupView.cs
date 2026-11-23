using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 공용 팝업의 **모양**(2026-11-23 프리팹 UI 2호) — 어두운 막·창 틀·X 닫기·제목/내용 자리·버튼 셋(왼쪽 취소 · 가운데 큰 확인 · 오른쪽 확인).
    /// 모양은 프리팹 `Assets/Resources/UI/Views/CommonPopupView.prefab`이 원본이고, 위치·크기·그림·글꼴은 에디터에서 고친다.
    /// 무엇을 쓰고(제목·내용·버튼 글자), 어떤 버튼을 켜고, 누르면 무엇을 하는지는 <see cref="CommonPopup"/>가 코드로 한다.
    ///
    /// ⚠️ 버튼 오브젝트 이름(`Close`·`Cancel`·`OkWide`·`Ok`)은 바꾸지 말 것 — 테스트가 이름으로 찾는다.
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/공용 팝업 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `CommonPopup.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class CommonPopupView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/CommonPopupView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        public enum Slot { Close, Cancel, Wide, Ok }

        [Header("글자 자리 — 내용은 코드가 채운다")]
        [SerializeField] Text title;
        [SerializeField] Text body;

        [Header("버튼 — Close(X) · Cancel(왼쪽) · Wide(가운데 큰 확인) · Ok(오른쪽)")]
        [Tooltip("Slot 순서대로: Close · Cancel · Wide · Ok")]
        [SerializeField] PxButton[] buttons = new PxButton[0];
        [Tooltip("버튼마다 눌릴 때 1도트 내려갈 안쪽 — buttons와 같은 순서")]
        [SerializeField] RectTransform[] buttonInners = new RectTransform[0];
        [Tooltip("버튼 글자 — Close는 글자가 없어 비워 둔다. buttons와 같은 순서")]
        [SerializeField] Text[] buttonLabels = new Text[0];

        public Text Title => title;
        public Text Body => body;
        public int ButtonCount => buttons.Length;
        public PxButton Button(Slot s) => (int)s < buttons.Length ? buttons[(int)s] : null;
        public Text Label(Slot s) => (int)s < buttonLabels.Length ? buttonLabels[(int)s] : null;

        /// <summary>
        /// 버튼을 쓸 수 있게 한다 — 버튼 상태 그림(마우스 올림·눌림)은 실행 중에 만들므로 프리팹에 저장되지 않아 여기서 다시 준비한다.
        /// </summary>
        public PxButton WirePopupButton(Slot s, System.Action onClick)
        {
            int i = (int)s;
            var b = buttons[i];
            b.Init(b.GetComponent<Image>(), i < buttonInners.Length ? buttonInners[i] : null);
            b.OnClick = onClick;
            return b;
        }

        // ── 처음 모양 — Figma "공용 팝업 업데이트"(53:32, 640×360). 어두운 배경 -963·-952를 원점으로 뺀 값(예전 CommonPopup에서 옮김) ──
        static readonly Rect Frame = new Rect(64f, 28f, 512f, 304f);      // 447:18 "설정 배경 2"(창 틀 512×304)
        static readonly Rect CloseBox = new Rect(541f, 23f, 40f, 40f);    // 597:144 "나가기_버튼 1" + 469:14 "x 2"
        static readonly Rect TitleBox = new Rect(160f, 52f, 320f, 20f);   // 글자 자리는 Figma에 없다 — 옛 "팝업창"(290:26) 16px 자리
        static readonly Rect BodyBox = new Rect(104f, 92f, 432f, 156f);   // 옛 "내용"(290:24) 자리를 새 틀·버튼 높이에 맞춰
        static readonly Rect CancelBox = new Rect(113f, 268f, 96f, 32f);  // 289:21 작은 버튼(왼쪽)
        static readonly Rect WideBox = new Rect(232f, 268f, 176f, 32f);   // 590:19 "버튼long 1"(가운데)
        static readonly Rect OkBox = new Rect(445f, 268f, 96f, 32f);      // 290:27 작은 버튼(오른쪽)

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static CommonPopupView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "CommonPopupView");
            var view = root.gameObject.AddComponent<CommonPopupView>();

            // 판 밖까지 덮는 반투명 막(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content) — 그리는 순서가 같다
            Px.FullFill(root, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, 0.5f));
            var board = Px.Board(root);
            Px.Image(board, "Frame", PxArt.Get("Common/window_tall"), Frame.x, Frame.y, Frame.width, Frame.height)
              .raycastTarget = true; // 창 틀 뒤로 클릭이 새지 않게
            var close = Px.IconButton(board, "Close", PxArt.Get("Common/close_frame"), PxArt.Get("Icons/close_x"),
                                      CloseBox.x, CloseBox.y, CloseBox.width, CloseBox.height, 0f, 0f, CloseBox.width, CloseBox.height, null);
            view.title = Px.Text(board, "Title", "", TitleBox.x - 120f, TitleBox.y, TitleBox.width + 240f, TitleBox.height, 16, Color.white);
            view.body = Px.Text(board, "Body", "", BodyBox.x, BodyBox.y, BodyBox.width, BodyBox.height, 16, Color.white);
            view.body.horizontalOverflow = HorizontalWrapMode.Wrap;
            view.body.lineSpacing = 1.15f;

            var cancel = Px.TextButton(board, "Cancel", PxArt.Get("Common/button"), CancelBox.x, CancelBox.y,
                                       CancelBox.width, CancelBox.height, "취소", 16, Color.white, null);
            var wide = Px.TextButton(board, "OkWide", PxArt.Get("Common/button_wide"), WideBox.x, WideBox.y,
                                     WideBox.width, WideBox.height, "확인", 16, Color.white, null);
            var ok = Px.TextButton(board, "Ok", PxArt.Get("Common/button"), OkBox.x, OkBox.y,
                                   OkBox.width, OkBox.height, "확인", 16, Color.white, null);

            view.buttons = new[] { close, cancel, wide, ok };
            view.buttonInners = new[] { close.Inner, cancel.Inner, wide.Inner, ok.Inner };
            view.buttonLabels = new Text[]
            {
                null,
                cancel.Inner.GetComponentInChildren<Text>(),
                wide.Inner.GetComponentInChildren<Text>(),
                ok.Inner.GetComponentInChildren<Text>(),
            };
            return view;
        }
    }
}
