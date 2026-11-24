using UnityEngine;
using UnityEngine.UI;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 각성창의 **모양**(2026-11-23 프리팹 UI 12호) — 바탕·왼쪽 전신 그림·위 패널, 위 패널의 두 얼굴(`SkillDetail` 스킬 상세 / `StatDetail` 스탯 5줄),
    /// [각성]·[스탯] 탭, 아래 창, 아래 창의 두 얼굴(`Tree` 스킬 칸 2줄×5 / `Upgrades` 강화 목록·스크롤 표시·강화 상세), 닫기, 에디터 전용 "SP +1".
    /// 모양은 프리팹 `Assets/Resources/UI/Views/AwakenScreenView.prefab`이 원본이고, 위치·크기·그림·글꼴·색은 에디터에서 고친다.
    /// 무엇을 보여 줄지(스킬 아이콘·설명·상태, 칸 틀 그림(배움/열림/막힘), 스탯 값, 강화 목록·상세, 고른 줄/구매 단위 그림), 탭 전환, 버튼 동작·키보드 순서,
    /// 목록 스크롤 자리는 <see cref="AwakenScreen"/>가 코드로 한다.
    ///
    /// - 스킬 칸은 <see cref="nodes"/>에 1줄 1~5층, 2줄 1~5층 순서(줄 × 5 + 층 − 1)로 둔다.
    /// - 강화 목록 줄은 `Upgrades/List` 안에서 코드가 세로 자리만 정한다(맨 위에 보이는 줄 기준 × <see cref="rowStep"/>) — `List`의 가림막(RectMask2D) 밖은 잘린다.
    /// - 스크롤 표시(`Thumb`)는 가로 자리·크기는 프리팹 그대로, 세로만 <see cref="scrollTrackY"/>~+<see cref="scrollTrackHeight"/> 사이를 코드가 옮긴다.
    /// - 에디터 전용 "SP +1"의 9-슬라이스 그림은 <see cref="PxSlicedArt"/>가 불러올 때 다시 만든다(빌드한 게임에선 화면 코드가 끈다).
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/각성창 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `AwakenScreen.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class AwakenScreenView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/AwakenScreenView";
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

            /// <summary>버튼 안 글자(없으면 null).</summary>
            public Text Label => inner != null ? inner.GetComponentInChildren<Text>(true) : null;
        }

        /// <summary>스킬 칸 하나 — 틀 버튼·아이콘·층 숫자(아이콘이 없을 때)·배움 점·금테 커서.</summary>
        [System.Serializable]
        public class NodeRef
        {
            public ButtonRef frame = new ButtonRef();
            public Image icon;
            public Text number;
            public Image badge;
            public Image cursor;
        }

        /// <summary>강화 목록 줄 하나 — 줄 버튼·이름·단계.</summary>
        [System.Serializable]
        public class UpgradeRowRef
        {
            public ButtonRef row = new ButtonRef();
            public Text name;
            public Text level;
        }

        [Header("위 패널 — [각성] 탭 얼굴(스킬 상세)")]
        public RectTransform skillDetail;
        public Image bigIcon;
        public Text descText;
        public Text stateText;
        public Text spText;
        public ButtonRef learn = new ButtonRef();
        [Tooltip("에디터 전용 \"SP +1\" — 빌드한 게임에선 화면 코드가 끈다")]
        public ButtonRef testSp = new ButtonRef();

        [Header("위 패널 — [스탯] 탭 얼굴(스탯 5줄: 레벨·최대체력·공격력·이동속도·공격속도)")]
        public RectTransform statDetail;
        public Text[] statValues = new Text[0];

        [Header("탭")]
        public ButtonRef tabAwaken = new ButtonRef();
        public ButtonRef tabStat = new ButtonRef();

        [Header("아래 창 — [각성] 얼굴(스킬 칸, 줄 × 5 + 층 − 1)")]
        public RectTransform tree;
        public NodeRef[] nodes = new NodeRef[0];

        [Header("아래 창 — [스탯] 얼굴(강화 목록·스크롤·상세)")]
        public RectTransform upgrades;
        public UpgradeRowRef[] upgradeRows = new UpgradeRowRef[0];
        [Tooltip("목록 줄 간격(px) — 맨 위에 보이는 줄부터 이 간격으로 아래로")]
        public float rowStep = 28f;
        [Tooltip("목록에 한 번에 보이는 줄 수 — 휠·키보드로 미는 범위")]
        public int rowsVisible = 3;
        public RectTransform thumb;
        [Tooltip("스크롤 표시가 움직이는 세로 범위 — 위 끝 y(판 좌표, 아래로)")]
        public float scrollTrackY = 230f;
        [Tooltip("스크롤 표시가 움직이는 세로 범위 — 길이(px)")]
        public float scrollTrackHeight = 108f;
        public Text detailName;
        public Text beforeLv, beforeVal, afterLv, afterVal;
        public ButtonRef[] qtyButtons = new ButtonRef[0];
        public ButtonRef buy = new ButtonRef();

        [Header("닫기")]
        public ButtonRef close = new ButtonRef();

        [Header("색 — 지금 탭 글자·막힌 칸")]
        public Color gold = new Color32(0xfa, 0xc7, 0x0e, 0xff);
        public Color dim = new Color(0.55f, 0.55f, 0.6f, 1f);

        /// <summary>스크롤 표시를 비율(0 = 맨 위, 1 = 맨 아래)에 놓는다 — 가로는 그대로.</summary>
        public void SetScroll(float t)
        {
            if (thumb == null) return;
            float range = Mathf.Max(0f, scrollTrackHeight - thumb.sizeDelta.y);
            thumb.anchoredPosition = new Vector2(thumb.anchoredPosition.x, -Mathf.Round(scrollTrackY + Mathf.Clamp01(t) * range));
        }

        // ── 처음 모양 — Figma "각성창"(16:2) 2026-11-18판 + [스탯] 탭 2026-11-22 스크린샷, 640판 원점 = 배경 324:2의 -959·-534(예전 AwakenScreen에서 옮김) ──
        static readonly Color Bg = Px.Hex(0x414141);
        static readonly Rect Portrait = new Rect(6f, 43f, 192f, 288f);     // 501:2 전신 32×48을 6배
        static readonly Rect TopPanel = new Rect(214f, 27f, 384f, 160f);   // 348:5
        static readonly Rect BigFrame = new Rect(228f, 43f, 64f, 64f);     // 532:13 (금테 64칸)
        static readonly Rect BigIcon = new Rect(230f, 45f, 60f, 60f);      // 629:14 (30px 아이콘 2배)
        static readonly Rect DescBox = new Rect(308f, 43f, 168f, 128f);    // 532:21
        static readonly Rect SpBox = new Rect(492f, 56f, 96f, 32f);        // 532:18
        static readonly Rect LearnBtn = new Rect(492f, 126f, 96f, 32f);    // 532:15
        static readonly Rect TabAwaken = new Rect(214f, 197f, 64f, 64f);   // 348:2 + 348:6 "각성" 20px
        static readonly Rect TabStat = new Rect(214f, 277f, 64f, 64f);     // 348:4 + 348:7 "스탯"
        static readonly Rect SkillWindow = new Rect(294f, 197f, 304f, 144f); // 348:9
        static readonly float[] NodeX = { 306f, 368f, 430f, 492f, 554f };  // 미획득 칸 x(62 간격)
        static readonly float[] NodeY = { 246f, 293f };
        const int Cols = 5;
        const float NodeSize = 32f, IconInset = 1f, IconSize = 30f;         // 2026-11-18 Figma 도트 아이콘 30×30 — 1:1로(32칸 가운데)
        static readonly Rect CloseBox = new Rect(608f, 8f, 24f, 24f);       // Figma에 없음
        const float StatBarX = 226f, StatBarY = 36f, StatBarStep = 29f, StatBarW = 360f, StatBarH = 24f;
        const float StatLabelX = 306f, StatValueRight = 576f;
        const float ListX = 300f, ListY = 234f, ListW = 128f, ListH = 104f, RowH = 24f, RowStep = 28f;
        const float ScrollX = 434f, ScrollY = 230f, ScrollH = 108f, ThumbH = 8f;
        const float DetailX = 445f, DetailY = 236f;
        static readonly string[] StatLabels = { "레벨", "최대체력", "공격력", "이동속도", "공격속도" };
        static readonly string[] QtyLabels = { "X1", "X10", "최대" };
        static readonly Color Lime = Px.Hex(0xeeff00);                  // 산 뒤 값(스크린샷의 노랑)
        static readonly Color Gold = new Color32(0xfa, 0xc7, 0x0e, 0xff);

        static ButtonRef Ref(PxButton b) => new ButtonRef { button = b, inner = b.Inner };

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static AwakenScreenView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "AwakenScreenView");
            var v = root.gameObject.AddComponent<AwakenScreenView>();

            Px.FullFill(root, "Background", Bg); // 판 밖까지(예전엔 PxScreen.Back 층)
            var board = Px.Board(root);
            Px.Image(board, "Portrait", PxArt.Get("Awaken/portrait_full"), Portrait.x, Portrait.y, Portrait.width, Portrait.height);
            Px.Image(board, "TopPanel", PxArt.Get("Awaken/stat_window"), TopPanel.x, TopPanel.y, TopPanel.width, TopPanel.height);

            // 스킬 상세(각성 탭) — 큰 아이콘 + 설명 + SP + 배우기
            var sd = v.skillDetail = Px.Stretch(board, "SkillDetail");
            Px.Image(sd, "DescBox", PxArt.Get("Awaken/desc_window"), DescBox.x, DescBox.y, DescBox.width, DescBox.height);
            Px.Image(sd, "SpBox", PxArt.Get("Awaken/sp_box"), SpBox.x, SpBox.y, SpBox.width, SpBox.height);
            v.spText = Px.Text(sd, "Sp", "", SpBox.x + 5f, SpBox.y, SpBox.width - 10f, SpBox.height, 13, Gold, TextAnchor.MiddleLeft);
            Px.Image(sd, "BigFrame", PxArt.Get("Awaken/big_frame"), BigFrame.x, BigFrame.y, BigFrame.width, BigFrame.height);
            v.bigIcon = Px.Image(sd, "BigIcon", null, BigIcon.x, BigIcon.y, BigIcon.width, BigIcon.height);
            v.bigIcon.color = Color.white; // 그림 없이 만들 때 깔린 경고색(분홍)을 걷는다 — 그림은 실행 중에 넣는다
            v.descText = Px.Text(sd, "Desc", "", DescBox.x + 8f, DescBox.y + 6f, DescBox.width - 16f, 84f, 10, Color.white, TextAnchor.UpperLeft);
            v.descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            v.stateText = Px.Text(sd, "State", "", DescBox.x + 8f, DescBox.y + DescBox.height - 30f, DescBox.width - 16f, 24f, 10, Gold, TextAnchor.LowerLeft);
            v.stateText.horizontalOverflow = HorizontalWrapMode.Wrap;
            v.learn = Ref(Px.TextButton(sd, "Learn", PxArt.Get("Awaken/learn_button"), LearnBtn.x, LearnBtn.y, LearnBtn.width, LearnBtn.height,
                                        "배우기", 13, Color.white, null));

            // 스탯(스탯 탭) — 위 패널에 스탯 바 다섯 줄. 왼쪽 아이콘 칸(24×24)은 그림이 아직 안 와서 비워 둔다.
            var st = v.statDetail = Px.Stretch(board, "StatDetail");
            v.statValues = new Text[StatLabels.Length];
            for (int i = 0; i < StatLabels.Length; i++)
            {
                float y = StatBarY + i * StatBarStep;
                Px.Image(st, "Bar" + i, PxArt.Get("Awaken/stat_bar"), StatBarX, y, StatBarW, StatBarH);
                Px.Text(st, "Label" + i, StatLabels[i], StatLabelX, y, 150f, StatBarH, 16, Color.white, TextAnchor.MiddleLeft);
                v.statValues[i] = Px.Text(st, "Value" + i, "", StatValueRight - 150f, y, 150f, StatBarH, 16, Color.white, TextAnchor.MiddleRight);
            }

            v.tabAwaken = Ref(Px.TextButton(board, "TabAwaken", PxArt.Get("Awaken/tab_button"), TabAwaken.x, TabAwaken.y,
                                            TabAwaken.width, TabAwaken.height, "각성", 20, Color.white, null));
            v.tabStat = Ref(Px.TextButton(board, "TabStat", PxArt.Get("Awaken/tab_button"), TabStat.x, TabStat.y,
                                          TabStat.width, TabStat.height, "스탯", 20, Color.white, null));
            Px.Image(board, "SkillWindow", PxArt.Get("Awaken/skill_window"), SkillWindow.x, SkillWindow.y, SkillWindow.width, SkillWindow.height);

            // 스킬 칸 2줄 × 5층
            var tr = v.tree = Px.Stretch(board, "Tree");
            v.nodes = new NodeRef[2 * Cols];
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < Cols; c++)
                {
                    var b = Px.Button(tr, $"Node{r}{c}", PxArt.Get("Awaken/node_open"), NodeX[c], NodeY[r], NodeSize, NodeSize, null, out var inner);
                    var n = new NodeRef { frame = new ButtonRef { button = b, inner = inner } };
                    n.icon = Px.Image(inner, "Icon", null, IconInset, IconInset, IconSize, IconSize);
                    n.icon.color = Color.white; // 경고색(분홍) 걷기 — 그림은 실행 중에 넣는다
                    n.number = Px.Text(inner, "Tier", (c + 1).ToString(), 0f, 0f, NodeSize, NodeSize, 13, Color.white).WithOutline(Px.Ink);
                    n.badge = Px.Image(inner, "Learned", null, NodeSize - 6f, 2f, 4f, 4f);
                    n.badge.color = Gold;
                    n.cursor = Px.Image(inner, "Cursor", PxArt.Get("Awaken/node_cursor"), 0f, 0f, NodeSize, NodeSize);
                    v.nodes[r * Cols + c] = n;
                }

            // 아래 창(스탯 탭): 머리띠 가운데 제목 · 왼쪽 강화 목록 · 스크롤 표시 · 오른쪽 강화 상세
            var up = v.upgrades = Px.Stretch(board, "Upgrades");
            Px.Text(up, "Title", "스탯 강화", 403f, SkillWindow.y, 85f, 32f, 16, Color.white);
            var list = Px.Node(up, "List", ListX, ListY, ListW, ListH);
            list.gameObject.AddComponent<RectMask2D>(); // 네 번째 줄은 반쯤 잘려 보인다(스크린샷)
            v.rowStep = RowStep;
            v.rowsVisible = 3;
            v.upgradeRows = new UpgradeRowRef[5];
            for (int i = 0; i < 5; i++)
            {
                var b = Px.Button(list, "Row" + i, PxArt.Get("Awaken/row_off"), 0f, i * RowStep, ListW, RowH, null, out var inner);
                v.upgradeRows[i] = new UpgradeRowRef
                {
                    row = new ButtonRef { button = b, inner = inner },
                    name = Px.Text(inner, "Name", "", 10f, 0f, 80f, RowH, 11, Color.white, TextAnchor.MiddleLeft),
                    level = Px.Text(inner, "Lv", "", ListW - 10f - 50f, 0f, 50f, RowH, 11, Color.white, TextAnchor.MiddleRight),
                };
            }
            Px.Fill(up, "Divider", Px.Hex(0xd9d9d9), 437f, ScrollY, 1f, ScrollH);
            Px.Fill(up, "Divider2", Px.Hex(0x6e798c), 438f, ScrollY, 1f, ScrollH);
            v.thumb = Px.Fill(up, "Thumb", Px.Hex(0xd9d9d9), ScrollX, ScrollY, 3f, ThumbH).rectTransform;
            v.scrollTrackY = ScrollY;
            v.scrollTrackHeight = ScrollH;

            Px.Image(up, "Detail", PxArt.Get("Awaken/stat_detail"), DetailX, DetailY, 144f, 96f);
            v.detailName = Px.Text(up, "DetailName", "", DetailX + 7f, DetailY + 1f, 130f, 13f, 11, Color.white, TextAnchor.MiddleLeft);
            Px.Image(up, "Before", PxArt.Get("Awaken/stat_value_box"), DetailX + 12f, DetailY + 18f, 48f, 24f);
            v.beforeLv = Px.Text(up, "BeforeLv", "", DetailX + 12f, DetailY + 18f, 48f, 12f, 8, Color.white);
            v.beforeVal = Px.Text(up, "BeforeVal", "", DetailX + 12f, DetailY + 29f, 48f, 12f, 8, Color.white);
            Px.Text(up, "Arrow", ">>", DetailX + 60f, DetailY + 18f, 24f, 24f, 13, Color.white);
            Px.Image(up, "After", PxArt.Get("Awaken/stat_value_box"), DetailX + 84f, DetailY + 18f, 48f, 24f);
            v.afterLv = Px.Text(up, "AfterLv", "", DetailX + 84f, DetailY + 18f, 48f, 12f, 8, Color.white);
            v.afterVal = Px.Text(up, "AfterVal", "", DetailX + 84f, DetailY + 29f, 48f, 12f, 8, Lime);
            v.qtyButtons = new ButtonRef[QtyLabels.Length];
            for (int k = 0; k < QtyLabels.Length; k++)
                v.qtyButtons[k] = Ref(Px.TextButton(up, "Qty" + k, PxArt.Get("Awaken/mult_off"), DetailX + 12f + 42f * k, DetailY + 52f, 36f, 16f,
                                                    QtyLabels[k], 11, Color.white, null));
            v.buy = Ref(Px.TextButton(up, "Buy", PxArt.Get("Awaken/buy_button"), DetailX + 12f, DetailY + 74f, 120f, 16f, "", 11, Color.white, null));

            v.close = Ref(Px.IconButton(board, "Close", PxArt.Get("Common/button_small"), PxArt.Get("Icons/close"), CloseBox.x, CloseBox.y,
                                        CloseBox.width, CloseBox.height, 2f, 2f, 19f, 19f, null));

            // 에디터 전용 "SP +1"(사용자 2026-09-29 — 스킬을 한 층씩 찍어 보며 테스트) — 스킬 상세 안, 빌드한 게임에선 화면 코드가 끈다
            var sp = Px.TextButton(sd, "TestSp", PxArt.Sliced("Common/button", 4), 492f, 164f, 96f, 18f, "SP +1 (테스트)", 8, Color.white, null);
            sp.Art.type = Image.Type.Sliced;
            var sa = sp.gameObject.AddComponent<PxSlicedArt>(); // 실행 중 만든 9-슬라이스 그림은 프리팹에 안 남는다 — 경로만 남긴다
            sa.artPath = "Common/button";
            sa.border = 4;
            v.testSp = Ref(sp);
            return v;
        }
    }
}
