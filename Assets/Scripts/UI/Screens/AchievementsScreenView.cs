using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 업적 창의 **모양**(2026-11-23 프리팹 UI 7호 — 첫 목록형) — 어두운 막·창 틀·제목·요약 줄·닫기 버튼·업적 목록 자리(`List`)와 그 안의 견본 줄(`RowTemplate`).
    /// 모양은 프리팹 `Assets/Resources/UI/Views/AchievementsScreenView.prefab`이 원본이고, 위치·크기·그림·글꼴·색·격자 칸 크기는 에디터에서 고친다.
    ///
    /// - **목록은 데이터 길이만큼 실행 중에 만든다**: 견본 줄(꺼 둔 `List/RowTemplate`, <see cref="AchievementRowView"/>)을 업적 수만큼 복제해
    ///   `List` 왼쪽 위부터 <see cref="columns"/>열 격자로 놓는다(칸 크기 <see cref="cellSize"/>). 견본 하나를 고치면 모든 줄이 바뀐다.
    /// - 무엇을 쓰고(★/☆ 이름·제목·요약), 어떤 색으로 칠하고, 고른 줄의 설명 상자를 띄우는 건 <see cref="AchievementsScreen"/>가 코드로 한다.
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/업적 창 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `AchievementsScreen.Build`와 같은 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class AchievementsScreenView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/AchievementsScreenView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        [Header("글자 — 코드가 채운다")]
        [SerializeField] Text title;
        [SerializeField] Text summary;

        [Header("닫기")]
        [SerializeField] PxButton closeButton;
        [SerializeField] RectTransform closeInner;

        [Header("목록 — List 왼쪽 위부터 격자로 줄을 늘어놓는다")]
        [Tooltip("판(Board)의 직접 자식, 왼쪽 위 기준(앵커·피벗 0,1)으로 둘 것 — 설명 상자 자리를 이 자리로 계산한다")]
        [SerializeField] RectTransform list;
        [Tooltip("견본 줄(꺼 둔 채로 둔다) — 업적 수만큼 복제된다")]
        [SerializeField] AchievementRowView rowTemplate;
        [Tooltip("열 수 — 키보드 ←/→/↑/↓ 격자도 이 열 수를 따른다")]
        [SerializeField] int columns = 2;
        [Tooltip("격자 칸 크기(px) — 줄은 칸 왼쪽 위에 놓인다")]
        [SerializeField] Vector2 cellSize = new Vector2(224f, 11f);
        [Tooltip("설명 상자 기준 폭 = 칸 폭 − 이 값(상자는 그 오른쪽에 뜬다)")]
        [SerializeField] float tooltipInset = 60f;

        [Header("줄 글자 색 — 달성 #fac70e, 아직 흰색 70%")]
        [SerializeField] Color gotColor = new Color32(0xfa, 0xc7, 0x0e, 0xff);
        [SerializeField] Color notColor = new Color(1f, 1f, 1f, 0.7f);

        [Header("설명 상자를 띄울 판")]
        [SerializeField] RectTransform board;

        public Text Title => title;
        public Text Summary => summary;
        public int Columns => Mathf.Max(1, columns);
        public Color GotColor => gotColor;
        public Color NotColor => notColor;
        public RectTransform Board => board;

        public PxButton WireClose(System.Action onClick)
        {
            if (closeButton == null) return null;
            closeButton.Init(closeButton.GetComponent<Image>(), closeInner);
            closeButton.OnClick = onClick;
            return closeButton;
        }

        /// <summary>
        /// 견본 줄을 <paramref name="count"/>개 복제해 격자로 놓고 돌려준다(견본은 꺼 둔 채 그대로). 화면마다 한 번(Build에서)만 부른다.
        /// </summary>
        public AchievementRowView[] BuildRows(int count)
        {
            var rows = new AchievementRowView[count];
            if (rowTemplate == null || list == null) return rows;
            rowTemplate.gameObject.SetActive(false);
            for (int i = 0; i < count; i++)
            {
                var row = Instantiate(rowTemplate, list, false);
                row.name = "Row" + i;
                var rt = (RectTransform)row.transform;
                var cell = CellOffset(i);
                rt.anchoredPosition = new Vector2(cell.x, -cell.y);
                row.gameObject.SetActive(true);
                rows[i] = row;
            }
            return rows;
        }

        /// <summary>i번째 줄의 자리 — <see cref="list"/> 왼쪽 위 기준(px, y는 아래로).</summary>
        Vector2 CellOffset(int i) => new Vector2((i % Columns) * cellSize.x, (i / Columns) * cellSize.y);

        /// <summary>i번째 줄의 설명 상자 기준 자리(판 좌표, 왼쪽 위 기준) — 줄 바로 아래, 폭은 칸 폭 − <see cref="tooltipInset"/>.</summary>
        public Rect TooltipAnchor(int i)
        {
            var cell = CellOffset(i);
            float x = list.anchoredPosition.x + cell.x, y = -list.anchoredPosition.y + cell.y;
            return new Rect(x, y + cellSize.y, cellSize.x - tooltipInset, 0f);
        }

        // ── 처음 모양 — Figma에 화면이 없다(예전 로비 "업적" 탭을 공용 창 틀에 옮긴 것). 예전 AchievementsScreen에서 옮김 ──
        static readonly Rect Frame = new Rect(64f, 36f, 512f, 288f);
        const float ListX = 96f, ListY = 112f, ColW = 224f, RowH = 11f;

        /// <summary>
        /// 예전 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static AchievementsScreenView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "AchievementsScreenView");
            var view = root.gameObject.AddComponent<AchievementsScreenView>();

            // 판 밖까지 덮는 반투명 막(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content) — 그리는 순서가 같다
            Px.FullFill(root, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, 0.5f));
            var board = Px.Board(root);
            view.board = board;
            Px.Image(board, "Frame", PxArt.Get("Common/window_frame"), Frame.x, Frame.y, Frame.width, Frame.height).raycastTarget = true;
            view.title = Px.Text(board, "Title", "업적", 220f, 56f, 200f, 22f, 16, Color.white);
            view.summary = Px.Text(board, "Summary", "", 96f, 96f, 448f, 12f, 8, Color.white, TextAnchor.MiddleLeft);

            // 목록 자리 + 견본 줄(꺼 둔다) — 줄은 실행 중에 업적 수만큼 복제된다
            int rowsShown = (ChronoSavior.Core.Achievements.All.Length + 1) / 2;
            view.list = Px.Node(board, "List", ListX, ListY, ColW * 2f, RowH * Mathf.Max(1, rowsShown));
            var plate = Px.Fill(view.list, "RowTemplate", Color.clear, 0f, 0f, ColW - 8f, RowH);
            var inner = Px.Stretch(plate.transform, "Inner");
            var b = plate.gameObject.AddComponent<PxButton>();
            b.Init(plate, inner);
            var label = Px.Text(inner, "Name", "", 0f, 0f, ColW - 8f, RowH, 8, Color.white, TextAnchor.MiddleLeft);
            view.rowTemplate = plate.gameObject.AddComponent<AchievementRowView>();
            view.rowTemplate.Assign(b, inner, label);
            plate.gameObject.SetActive(false);

            var close = Px.IconButton(board, "Close", PxArt.Get("Common/button_small"), PxArt.Get("Icons/close"), 536f, 54f, 24f, 24f,
                                      2f, 2f, 19f, 19f, null);
            view.closeButton = close;
            view.closeInner = close.Inner;
            return view;
        }
    }
}
