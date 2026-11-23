using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 타이틀 화면의 **모양**(2026-11-23 프리팹 UI 3호) — 검은 여백·배경 그림·로고·메뉴 판 넷(새 원정 · 불러오기 · 설정 · 나가기)·◆ 커서.
    /// 모양은 프리팹 `Assets/Resources/UI/Views/TitleMenuView.prefab`이 원본이고, 위치·크기·그림·글자·색은 에디터에서 고친다.
    /// 무엇을 고르면 무슨 일이 생기는지, 어떤 메뉴가 골라져 있는지, ◆를 어디에 둘지는 <see cref="TitleMenu"/>가 코드로 한다.
    ///
    /// - 메뉴 판은 판(Board)의 직접 자식으로, 왼쪽 위 기준(앵커·피벗 0,1)으로 둔다 — ◆ 커서가 판 자리를 읽어 글자 왼쪽에 붙는다.
    /// - 메뉴 글자 색(고른 것·나머지)은 아래 색 칸에서 고친다(글자 자체의 색은 실행 중에 이 값으로 덮인다).
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/타이틀 화면 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `TitleMenu.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class TitleMenuView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/TitleMenuView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        [Header("메뉴 — 키보드(↑/↓) 순서: 새 원정 · 불러오기 · 설정 · 나가기")]
        [Tooltip("메뉴 판(누르는 자리). 순서를 바꾸면 TitleMenu가 연결하는 동작도 같이 바뀐다.")]
        [SerializeField] PxButton[] buttons = new PxButton[0];
        [Tooltip("판마다 눌릴 때 1도트 내려갈 안쪽 — buttons와 같은 순서")]
        [SerializeField] RectTransform[] buttonInners = new RectTransform[0];
        [Tooltip("메뉴 글자 — buttons와 같은 순서")]
        [SerializeField] Text[] labels = new Text[0];

        [Header("◆ 커서")]
        [SerializeField] RectTransform cursor;
        [Tooltip("고른 메뉴 글자 왼쪽 끝과 ◆ 사이(px) — Figma 404:15")]
        [SerializeField] float cursorGap = 6f;

        [Header("메뉴 글자 색 — Figma: 고른 것 #fac70e, 나머지 #d4ae88")]
        [SerializeField] Color selectedColor = new Color32(0xfa, 0xc7, 0x0e, 0xff);
        [SerializeField] Color normalColor = new Color32(0xd4, 0xae, 0x88, 0xff);

        public int MenuCount => buttons.Length;
        public Text Label(int i) => i < labels.Length ? labels[i] : null;
        public Color SelectedColor => selectedColor;
        public Color NormalColor => normalColor;

        /// <summary>
        /// i번째 메뉴 판을 쓸 수 있게 한다 — 버튼 상태(마우스 올림·눌림)는 실행 중에 만들므로 프리팹에 저장되지 않아 여기서 다시 준비한다.
        /// </summary>
        public PxButton WireTitleButton(int i, System.Action onClick)
        {
            var b = buttons[i];
            b.Init(b.GetComponent<Image>(), i < buttonInners.Length ? buttonInners[i] : null);
            b.OnClick = onClick;
            return b;
        }

        /// <summary>
        /// ◆를 i번째 메뉴 글자 왼쪽에 붙인다 — 판 자리(앵커 왼쪽 위)·글자 폭·<see cref="cursorGap"/>로 계산하고 정수 도트로 맞춘다.
        /// 판을 에디터에서 옮기거나 키워도 ◆가 따라간다.
        /// </summary>
        public void PlaceCursor(int i)
        {
            if (cursor == null || i < 0 || i >= buttons.Length || i >= labels.Length) return;
            var plate = (RectTransform)buttons[i].transform;
            float plateX = plate.anchoredPosition.x, plateY = -plate.anchoredPosition.y;
            float plateW = plate.sizeDelta.x, plateH = plate.sizeDelta.y;
            float textW = labels[i].preferredWidth;
            float x = plateX + (plateW - textW) / 2f - cursorGap - cursor.sizeDelta.x;
            float y = plateY + (plateH - cursor.sizeDelta.y) / 2f;
            cursor.anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        }

        // ── 처음 모양 — Figma "타이틀"(35:2, 640판 원점 0·0). 예전 TitleMenu에서 옮김 ──
        static readonly Rect Logo = new Rect(155f, 20f, 329f, 185f);   // 375:2 (그림이 칸 높이의 94.47%)
        const float PlateX = 256f, PlateW = 128f, PlateH = 32f;          // 404:5 · 404:9 · 404:10 · 404:11
        static readonly float[] PlateY = { 200f, 238f, 276f, 314f };
        const int MenuSize = 12;                                         // 319:2~319:5 12px
        const float CursorSize = 16f;                                    // ◆(404:15) — "새 원정" 글자 왼쪽 6px 앞(278 + 16 = 294 … 글자 300)
        static readonly Color Plate = new Color(0f, 0f, 0f, 0.33f);      // 반투명 검은 판 rgba(0,0,0,.33)
        static readonly string[] Labels = { "새 원정", "불러오기", "설정", "나가기" };

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static TitleMenuView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "TitleMenuView");
            var view = root.gameObject.AddComponent<TitleMenuView>();

            // 16:9가 아닌 화면의 남는 곳은 검게(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content)엔 배경 그림
            Px.FullFill(root, "Letterbox", Color.black);
            var board = Px.Board(root);
            Px.Image(board, "Background", PxArt.Get("Title/background"), 0f, 0f, PixelCanvas.Width, PixelCanvas.Height);
            Px.Image(board, "Logo", PxArt.Get("Title/logo"), Logo.x, Logo.y, Logo.width, Logo.height);

            view.buttons = new PxButton[Labels.Length];
            view.buttonInners = new RectTransform[Labels.Length];
            view.labels = new Text[Labels.Length];
            for (int i = 0; i < Labels.Length; i++)
            {
                var plate = Px.Fill(board, "Menu" + i, Plate, PlateX, PlateY[i], PlateW, PlateH);
                var inner = Px.Stretch(plate.transform, "Inner");
                var b = plate.gameObject.AddComponent<PxButton>();
                b.Init(plate, inner);
                view.buttons[i] = b;
                view.buttonInners[i] = inner;
                view.labels[i] = Px.Text(inner, "Label", Labels[i], 0f, 0f, PlateW, PlateH, MenuSize, view.normalColor);
            }
            view.cursor = (RectTransform)Px.Image(board, "Cursor", PxArt.Get("Common/cursor"), 0f, 0f, CursorSize, CursorSize).transform;
            return view;
        }
    }
}
