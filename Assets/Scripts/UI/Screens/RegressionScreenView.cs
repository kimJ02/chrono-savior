using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 시간 회귀("회귀 결과") 창의 **모양**(2026-11-23 프리팹 UI 4호) — 어두운 막·창 틀·제목판·부제·큰 막대 둘·작은 막대 둘(아이콘)·버튼 둘(취소·회귀).
    /// 모양은 프리팹 `Assets/Resources/UI/Views/RegressionScreenView.prefab`이 원본이고, 위치·크기·그림·글자는 에디터에서 고친다.
    /// 값(스테이지·파편·레벨·처치 수)과 회귀 버튼 글자·눌림 가능 여부, 버튼 동작은 <see cref="RegressionScreen"/>가 코드로 채운다.
    ///
    /// 창 안 요소는 전부 `Window`(창 512×304) 아래에 창 왼쪽 위 기준으로 놓았다 — `Window` 하나를 옮기면 창 전체가 같이 움직인다.
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/시간 회귀 창 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `RegressionScreen.Build`와 같은 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class RegressionScreenView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/RegressionScreenView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        public enum Slot { Cancel, Ok }

        [Header("값 글자 — 코드가 채운다: 0 이전 최고 스테이지 · 1 회귀 후 스테이지 · 2 획득한 파편 · 3 최고 레벨 · 4 총 처치 수")]
        [SerializeField] Text[] values = new Text[0];

        [Header("버튼 — Cancel(취소) · Ok(회귀). 키보드 ←/→ 순서도 이 순서")]
        [SerializeField] PxButton[] buttons = new PxButton[0];
        [Tooltip("버튼마다 눌릴 때 1도트 내려갈 안쪽 — buttons와 같은 순서")]
        [SerializeField] RectTransform[] buttonInners = new RectTransform[0];
        [Tooltip("회귀 버튼 글자 — 코드가 '회귀 (+N)' 등으로 채운다")]
        [SerializeField] Text okLabel;

        public int ValueCount => values.Length;
        public Text Value(int i) => i < values.Length ? values[i] : null;
        public int ButtonCount => buttons.Length;
        public Text OkLabel => okLabel;

        /// <summary>
        /// 버튼을 쓸 수 있게 한다 — 버튼 상태 그림(마우스 올림·눌림)은 실행 중에 만들므로 프리팹에 저장되지 않아 여기서 다시 준비한다.
        /// </summary>
        public PxButton WireRegressionButton(Slot s, System.Action onClick)
        {
            int i = (int)s;
            var b = buttons[i];
            b.Init(b.GetComponent<Image>(), i < buttonInners.Length ? buttonInners[i] : null);
            b.OnClick = onClick;
            return b;
        }

        // ── 처음 모양 — 사용자 스크린샷에서 잰 값(640판 환산, ±1도트, 2026-11-22). 창 왼쪽 위 기준(예전 RegressionScreen에서 옮김) ──
        static readonly Rect Win = new Rect(64f, 36f, 512f, 304f);       // 창 512×304(Common/window_tall)
        static readonly Rect Plate = new Rect(192f, -15f, 128f, 32f);    // 제목판(Common/title_plate)
        static readonly Rect StageBar = new Rect(54f, 52f, 400f, 48f);   // 정보창 large
        static readonly Rect ShardBar = new Rect(54f, 110f, 400f, 48f);
        static readonly Rect LevelBar = new Rect(54f, 167f, 184f, 48f);  // 정보창 small
        static readonly Rect KillBar = new Rect(270f, 167f, 184f, 48f);
        static readonly Rect CancelBox = new Rect(95f, 236f, 96f, 32f);  // 회귀창 버튼
        static readonly Rect OkBox = new Rect(321f, 236f, 96f, 32f);
        const float LabelH = 14f, ValueH = 24f;

        /// <summary>막대 안(막대 왼쪽 위 기준) 가운데 (cx, cy)에 글자 한 줄 — 창 왼쪽 위 기준 자리로 바꿔 놓는다.</summary>
        static Text Line(RectTransform win, Rect bar, string name, string text, float cx, float cy, float w, float h, int size) =>
            Px.Text(win, name, text, bar.x + cx - w / 2f, bar.y + cy - h / 2f, w, h, size, Color.white);

        /// <summary>
        /// 예전 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static RegressionScreenView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "RegressionScreenView");
            var view = root.gameObject.AddComponent<RegressionScreenView>();

            // 판 밖까지 덮는 어두운 막(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content) — 그리는 순서가 같다
            Px.FullFill(root, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, Px.BackdropAlpha));
            var board = Px.Board(root);
            var win = Px.Node(board, "Window", Win.x, Win.y, Win.width, Win.height);

            Px.Image(win, "Frame", PxArt.Get("Common/window_tall"), 0f, 0f, Win.width, Win.height).raycastTarget = true;
            Px.Image(win, "TitlePlate", PxArt.Get("Common/title_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            Px.Text(win, "Title", "회귀 결과", Plate.x, Plate.y, Plate.width, Plate.height, 24, Color.white);
            Px.Text(win, "Subtitle", "시간을 되감아 더 강한 회차로 넘어 갑니다", 0f, 22f, Win.width, 20f, 13, Color.white);

            view.values = new Text[5];

            // 큰 막대 — 이전 최고 스테이지 ⟫ 회귀 후 스테이지
            Px.Image(win, "StageBar", PxArt.Get("Regression/info_large"), StageBar.x, StageBar.y, StageBar.width, StageBar.height);
            Line(win, StageBar, "LabelFrom", "이전 최고 스테이지", 82f, 8f, 160f, LabelH, 11);
            view.values[0] = Line(win, StageBar, "ValueFrom", "", 82f, 33f, 160f, ValueH, 20);
            Line(win, StageBar, "Arrow", ">>>", 200f, 24f, 60f, 24f, 20);
            Line(win, StageBar, "LabelTo", "회귀 후 스테이지", 320f, 8f, 160f, LabelH, 11);
            view.values[1] = Line(win, StageBar, "ValueTo", "", 320f, 33f, 160f, ValueH, 20);

            // 큰 막대 — 획득한 파편
            Px.Image(win, "ShardBar", PxArt.Get("Regression/info_large"), ShardBar.x, ShardBar.y, ShardBar.width, ShardBar.height);
            Line(win, ShardBar, "LabelShard", "획득한 파편", 200f, 9f, 160f, LabelH, 11);
            view.values[2] = Line(win, ShardBar, "ValueShard", "", 200f, 31f, 200f, ValueH, 20);

            // 작은 막대 둘 — 왼쪽 아이콘(32×32 1:1), 위 이름, 아래 값
            view.values[3] = SmallBar(win, LevelBar, "Level", "Icons/star", "최고 레벨");
            view.values[4] = SmallBar(win, KillBar, "Kill", "Icons/skull", "총 처치 수");

            var cancel = Px.TextButton(win, "Cancel", PxArt.Get("Regression/button"), CancelBox.x, CancelBox.y, CancelBox.width,
                                       CancelBox.height, "취소", 13, Color.white, null);
            var ok = Px.TextButton(win, "Ok", PxArt.Get("Regression/button"), OkBox.x, OkBox.y, OkBox.width,
                                   OkBox.height, "회귀", 13, Color.white, null);
            view.buttons = new[] { cancel, ok };
            view.buttonInners = new[] { cancel.Inner, ok.Inner };
            view.okLabel = ok.Inner.GetComponentInChildren<Text>();
            return view;
        }

        static Text SmallBar(RectTransform win, Rect bar, string key, string icon, string label)
        {
            Px.Image(win, key + "Bar", PxArt.Get("Regression/info_small"), bar.x, bar.y, bar.width, bar.height);
            Px.Image(win, key + "Icon", PxArt.Get(icon), bar.x + 6f, bar.y + 8f, 32f, 32f);
            Line(win, bar, key + "Label", label, 108f, 9f, 130f, LabelH + 2f, 13);
            return Line(win, bar, key + "Value", "", 112f, 31f, 130f, ValueH, 20);
        }
    }
}
