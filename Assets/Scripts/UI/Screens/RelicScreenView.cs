using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 유물 소환(가챠) 창의 **모양**(2026-11-23 프리팹 UI 11호, 목록형 — 7호 업적 창과 같은 견본 복제) — 바탕·큰 창·제목판·닫기,
    /// 왼쪽 칸 격자(`Slots` 안의 견본 칸 `Template`을 30칸으로 복제)·쪽 넘김, 오른쪽 보유 파편/보유 유물 수 막대·유물 확률 판(4줄)·[1]·[10] 소환,
    /// 에디터 전용 "유물 전부 각성" 버튼.
    /// 모양은 프리팹 `Assets/Resources/UI/Views/RelicScreenView.prefab`이 원본이고, 위치·크기·그림·글꼴·색은 에디터에서 고친다.
    /// 무엇을 보여 줄지(칸마다 유물·개수·자물쇠, 쪽 번호, 파편·유물 수, 확률 값·등급 색·등급 이름), 버튼 동작·키보드 격자·설명 상자는
    /// <see cref="RelicScreen"/>가 코드로 한다.
    ///
    /// - 칸은 `Slots` 왼쪽 위부터 <see cref="columns"/>열로, 칸 간격 <see cref="cellStep"/>으로 놓인다(키보드 격자도 이 열 수). 한 쪽 칸 수는 코드(30).
    ///   `Slots`와 버튼·막대는 판(Board)의 직접 자식, 왼쪽 위 기준으로 둘 것 — 설명 상자 자리를 그 자리로 계산한다.
    /// - 견본 칸 이름은 `Template`(이름이 "Slot"으로 시작하면 안 된다 — 칸 세기 테스트가 헷갈린다), **꺼 둔 채** 둔다.
    /// - 9-슬라이스 그림(쪽 넘김·테스트 버튼·확률 이름표)은 <see cref="PxSlicedArt"/>, 흰 ◆는 <see cref="PxWhiteArt"/>가 불러올 때 다시 만든다(실행 중 만든 그림은 프리팹에 안 남는다).
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/유물 소환 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `RelicScreen.Build`와 같은 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class RelicScreenView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/RelicScreenView";
        public const string AssetPath = "Assets/Resources/" + ResourcePath + ".prefab";

        /// <summary>버튼 하나 — 버튼과 눌릴 때 1도트 내려갈 안쪽.</summary>
        [System.Serializable]
        public class ButtonRef
        {
            public PxButton button;
            public RectTransform inner;

            public PxButton Wire(System.Action onClick)
            {
                if (button == null) return null;
                var sliced = button.GetComponent<PxSlicedArt>(); // 9-슬라이스 그림은 프리팹에 안 남는다 — 연결 전에 다시 만든다
                if (sliced != null) sliced.Apply();
                button.Init(button.GetComponent<Image>(), inner);
                button.OnClick = onClick;
                return button;
            }
        }

        /// <summary>확률 판 한 줄 — 등급 ◆·이름·값.</summary>
        [System.Serializable]
        public class RateRow
        {
            public Image gem;
            public Text name;
            public Text value;
        }

        [Header("판 — 설명 상자를 띄울 곳")]
        public RectTransform board;

        [Header("칸 격자 — Slots 왼쪽 위부터 열 수·간격으로")]
        public RectTransform slots;
        [Tooltip("견본 칸(꺼 둔 채) — 한 쪽 칸 수만큼 복제된다")]
        public RelicSlotView slotTemplate;
        [Tooltip("열 수 — 키보드 격자도 이 열 수를 따른다")]
        public int columns = 5;
        [Tooltip("칸 간격(px) — 다음 칸 왼쪽 위까지")]
        public Vector2 cellStep = new Vector2(37f, 37f);

        [Header("쪽 넘김")]
        public ButtonRef prevPage = new ButtonRef();
        public ButtonRef nextPage = new ButtonRef();
        public Text pageText;

        [Header("오른쪽 — 보유 파편·유물 수, 확률 판(노말·레어·에픽·레전드), 소환")]
        public Text shardText;
        public Text countText;
        public RateRow[] rateRows = new RateRow[0];
        public ButtonRef pull1 = new ButtonRef();
        public ButtonRef pull10 = new ButtonRef();

        [Header("닫기 · 에디터 전용 테스트 버튼(빌드한 게임에선 꺼진다)")]
        public ButtonRef close = new ButtonRef();
        public ButtonRef testAllRelics = new ButtonRef();

        public int Columns => Mathf.Max(1, columns);

        /// <summary>견본 칸을 <paramref name="count"/>개 복제해 격자로 놓는다(견본은 꺼 둔 채). Build에서 한 번만 부른다.</summary>
        public RelicSlotView[] BuildSlots(int count)
        {
            var list = new RelicSlotView[count];
            if (slotTemplate == null || slots == null) return list;
            slotTemplate.gameObject.SetActive(false);
            for (int i = 0; i < count; i++)
            {
                var s = Instantiate(slotTemplate, slots, false);
                s.name = "Slot" + i;
                var cell = CellOffset(i);
                ((RectTransform)s.transform).anchoredPosition = new Vector2(cell.x, -cell.y);
                s.gameObject.SetActive(true);
                list[i] = s;
            }
            return list;
        }

        Vector2 CellOffset(int i) => new Vector2((i % Columns) * cellStep.x, (i / Columns) * cellStep.y);

        /// <summary>i번째 칸 자리(판 좌표, 왼쪽 위 기준, y는 아래로) — 설명 상자 기준.</summary>
        public Rect SlotRect(int i)
        {
            var cell = CellOffset(i);
            Vector2 size = slotTemplate != null ? ((RectTransform)slotTemplate.transform).sizeDelta : new Vector2(32f, 32f);
            return new Rect(slots.anchoredPosition.x + cell.x, -slots.anchoredPosition.y + cell.y, size.x, size.y);
        }

        /// <summary>버튼 자리(판 좌표, 왼쪽 위 기준, y는 아래로) — 판의 직접 자식인 버튼만.</summary>
        public static Rect RectOf(PxButton b)
        {
            if (b == null) return new Rect();
            var rt = (RectTransform)b.transform;
            return new Rect(rt.anchoredPosition.x, -rt.anchoredPosition.y, rt.sizeDelta.x, rt.sizeDelta.y);
        }

        // ── 처음 모양 — 드라이브 새 그림 + 사용자 스크린샷(2026-11-22, 640판 환산 ±1도트), 창 왼쪽 위(W) 기준 상대 좌표(예전 RelicScreen에서 옮김) ──
        static readonly Color Bg = Px.Hex(0x414141);
        static readonly Vector2 W = new Vector2(64f, 36f);                   // 창 512×304(Common/window_tall)
        static readonly Rect Plate = new Rect(192f, -15f, 128f, 32f);        // 제목판(Common/title_plate)
        static readonly Rect CloseBox = new Rect(478f, -4f, 40f, 40f);       // 나가기 버튼(Common/close_frame + close_x)
        static readonly Rect GridPanel = new Rect(23f, 44f, 220f, 236f);     // 유물칸 백그라운드
        const float SlotX = 43f, SlotY = 52f, SlotStep = 37f, SlotSize = 32f; // 칸 32, 간격 5
        const int DefaultCols = 5, DefaultRows = 6;
        const float RightX = 257f, RightW = 240f;
        static readonly Rect ShardBar = new Rect(RightX, 44f, RightW, 32f);  // 유물 설명 막대 1
        static readonly Rect CountBar = new Rect(RightX, 84f, RightW, 32f);  // 유물 설명 막대 2
        static readonly Rect RatePanel = new Rect(RightX, 131f, RightW, 120f);
        static readonly Rect Pull1Box = new Rect(259f, 255f, 96f, 32f);
        static readonly Rect Pull10Box = new Rect(398f, 255f, 96f, 32f);
        static readonly float[] RateRowY = { 16f, 44f, 73f, 103f };           // 확률 줄 가운데 y(판 안쪽 기준)
        static readonly float[] RateLineY = { 30f, 58f, 88f };                // 줄 사이 가는 선
        static readonly Color RateValue = Px.Hex(0xfff0c0);
        static readonly Color RateLine = Px.Hex(0x30364e);
        const float PageY = 282f, PageBtnW = 18f, PageBtnH = 14f;            // 스크린샷에 없는 것 — 쪽 넘김(격자 밑 빈틈)

        static ButtonRef Ref(PxButton b) => new ButtonRef { button = b, inner = b.Inner };

        static PxButton SlicedTextButton(Transform parent, string name, float x, float y, float w, float h, string label, int size)
        {
            var b = Px.TextButton(parent, name, PxArt.Sliced("Common/button", 4), x, y, w, h, label, size, Color.white, null);
            b.Art.type = Image.Type.Sliced;
            var sa = b.gameObject.AddComponent<PxSlicedArt>(); // 실행 중 만든 9-슬라이스 그림은 프리팹에 안 남는다 — 경로만 남긴다
            sa.artPath = "Common/button";
            sa.border = 4;
            return b;
        }

        static Image WhiteGem(Transform parent, string name, float x, float y, float size)
        {
            var img = Px.Image(parent, name, PxArt.White("Common/cursor"), x, y, size, size);
            img.gameObject.AddComponent<PxWhiteArt>().artPath = "Common/cursor"; // 실행 중 만든 흰 그림도 프리팹에 안 남는다
            return img;
        }

        /// <summary>
        /// 예전 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 글자 중 데이터(확률 값·등급 이름·쪽 번호 등)는 비워 두고 화면 코드가 채운다. 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다.
        /// </summary>
        public static RelicScreenView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "RelicScreenView");
            var v = root.gameObject.AddComponent<RelicScreenView>();

            Px.FullFill(root, "Background", Bg); // 판 밖까지(예전엔 PxScreen.Back 층)
            var board = v.board = Px.Board(root);
            Px.Image(board, "Window", PxArt.Get("Common/window_tall"), W.x, W.y, 512f, 304f);
            Px.Image(board, "TitlePlate", PxArt.Get("Common/title_plate"), W.x + Plate.x, W.y + Plate.y, Plate.width, Plate.height);
            Px.Text(board, "Title", "유물 소환", W.x + Plate.x, W.y + Plate.y, Plate.width, Plate.height, 20, Color.white);
            Px.Image(board, "GridPanel", PxArt.Get("Relic/grid_panel"), W.x + GridPanel.x, W.y + GridPanel.y, GridPanel.width, GridPanel.height);

            // 칸 격자 — 견본 칸 하나(꺼 둔 채)
            v.slots = Px.Node(board, "Slots", W.x + SlotX, W.y + SlotY,
                              (DefaultCols - 1) * SlotStep + SlotSize, (DefaultRows - 1) * SlotStep + SlotSize);
            v.columns = DefaultCols;
            v.cellStep = new Vector2(SlotStep, SlotStep);
            var b = Px.Button(v.slots, "Template", PxArt.Get("Relic/slot"), 0f, 0f, SlotSize, SlotSize, null, out var inner);
            var slot = b.gameObject.AddComponent<RelicSlotView>();
            slot.button = b;
            slot.inner = inner;
            slot.gemEdge = WhiteGem(inner, "GemEdge", 7f, 6f, 18f);
            slot.gemEdge.color = Px.Ink;
            slot.gem = WhiteGem(inner, "Gem", 8f, 7f, 16f);
            slot.lockIcon = Px.Image(inner, "Lock", PxArt.Get("Relic/lock"), 0f, 0f, SlotSize, SlotSize); // 32×32 그대로(1:1)
            slot.count = Px.Text(inner, "Count", "", 0f, 21f, SlotSize - 3f, 10f, 8, Color.white, TextAnchor.MiddleRight).WithOutline(Px.Ink);
            v.slotTemplate = slot;
            b.gameObject.SetActive(false);

            float px0 = W.x + GridPanel.x + 76f, py = W.y + PageY;
            v.prevPage = Ref(SlicedTextButton(board, "PrevPage", px0, py, PageBtnW, PageBtnH, "<", 8));
            v.pageText = Px.Text(board, "Page", "", px0 + PageBtnW, py, 48f, PageBtnH, 8, Color.white);
            v.nextPage = Ref(SlicedTextButton(board, "NextPage", px0 + PageBtnW + 48f, py, PageBtnW, PageBtnH, ">", 8));

            // 오른쪽: 보유 시간의 파편 · 보유 유물 수
            Px.Image(board, "ShardBar", PxArt.Get("Relic/info_bar"), W.x + ShardBar.x, W.y + ShardBar.y, ShardBar.width, ShardBar.height);
            Px.Image(board, "ShardIcon", PxArt.Get("Icons/shard"), W.x + ShardBar.x + 40f, W.y + ShardBar.y + 8f, 16f, 16f);
            v.shardText = Px.Text(board, "Shards", "", W.x + ShardBar.x + 60f, W.y + ShardBar.y, 170f, ShardBar.height, 13, Color.white, TextAnchor.MiddleLeft);
            Px.Image(board, "CountBar", PxArt.Get("Relic/info_bar"), W.x + CountBar.x, W.y + CountBar.y, CountBar.width, CountBar.height);
            v.countText = Px.Text(board, "OwnedCount", "", W.x + CountBar.x, W.y + CountBar.y, CountBar.width, CountBar.height, 13, Color.white);

            // 유물 확률 판 — 판 위에 걸친 작은 이름표
            float rx = W.x + RatePanel.x, ry = W.y + RatePanel.y;
            Px.Image(board, "RatePanel", PxArt.Get("Relic/rate_panel"), rx, ry, RatePanel.width, RatePanel.height);
            v.rateRows = new RateRow[RateRowY.Length];
            for (int g = 0; g < RateRowY.Length; g++)
            {
                float cy = ry + RateRowY[g];
                v.rateRows[g] = new RateRow
                {
                    gem = WhiteGem(board, "RateGem" + g, rx + 7f, cy - 6f, 12f),
                    name = Px.Text(board, "RateName" + g, "", rx + 32f, cy - 10f, 120f, 20f, 16, Color.white, TextAnchor.MiddleLeft),
                    value = Px.Text(board, "RateValue" + g, "", rx + 116f, cy - 10f, 100f, 20f, 16, RateValue, TextAnchor.MiddleRight),
                };
            }
            for (int k = 0; k < RateLineY.Length; k++)
                Px.Fill(board, "RateLine" + k, RateLine, rx + 20f, ry + RateLineY[k], 198f, 1f);
            var plate = Px.Image(board, "RatePlate", PxArt.Sliced("Common/title_plate", 4), rx + 89f, ry - 10f, 62f, 19f);
            plate.type = Image.Type.Sliced;
            var psa = plate.gameObject.AddComponent<PxSlicedArt>();
            psa.artPath = "Common/title_plate";
            psa.border = 4;
            Px.Text(board, "RatePlateText", "유물 확률", rx + 89f, ry - 10f, 62f, 19f, 11, Color.white).WithOutline(Px.Ink);

            v.pull1 = Ref(Px.TextButton(board, "Pull1", PxArt.Get("Relic/pull1"), W.x + Pull1Box.x, W.y + Pull1Box.y, Pull1Box.width,
                                        Pull1Box.height, "1", 16, Color.white, null));
            v.pull10 = Ref(Px.TextButton(board, "Pull10", PxArt.Get("Relic/pull10"), W.x + Pull10Box.x, W.y + Pull10Box.y,
                                         Pull10Box.width, Pull10Box.height, "10", 16, Color.white, null));
            v.close = Ref(Px.IconButton(board, "Close", PxArt.Get("Common/close_frame"), PxArt.Get("Icons/close_x"),
                                        W.x + CloseBox.x, W.y + CloseBox.y, CloseBox.width, CloseBox.height, 0f, 0f, CloseBox.width, CloseBox.height, null));
            // 에디터 전용 "유물 전부 각성"(2026-11-19 테스트 유물 확인용) — 빌드한 게임에선 화면 코드가 끈다
            v.testAllRelics = Ref(SlicedTextButton(board, "TestAllRelics", 352f, 272f, 120f, 16f, "유물 전부 각성 (테스트)", 8));
            return v;
        }
    }
}
