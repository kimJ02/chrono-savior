using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 마왕성 길목 선택(스테이지 선택) 화면의 **모양**(2026-11-23 프리팹 UI 8호) — 배경·세계 지도·챕터 리본 5개·▼ 표시·
    /// 일반 쪽(`Normal`: 오른쪽 판·등장 몬스터 칸·진행도 막대·원정 떠나기·아래 띠의 챕터 판·스테이지 칸 5개·◀▶)과
    /// 보스 쪽(`Boss`: 빨간 판·보스 아이콘·초상·이름·패턴 칸·아래 띠·보스 칸·◀▶·원정 떠나기).
    /// 모양은 프리팹 `Assets/Resources/UI/Views/StageSelectScreenView.prefab`이 원본이고, 위치·크기·그림·글꼴·색은 에디터에서 고친다.
    /// 무엇을 보여 줄지(챕터·스테이지 글자, 몬스터·보스 그림, 진행도, 잠김 문구), 어느 쪽을 켤지, 버튼 동작·키보드 순서는 <see cref="StageSelectScreen"/>가 코드로 한다.
    ///
    /// - ▼ 표시는 고른(또는 마우스를 올린) 챕터 리본 자리 + <see cref="markerOffset"/>에 놓인다 — 리본을 옮기면 ▼도 따라간다.
    ///   리본과 ▼는 판(Board)의 직접 자식, 왼쪽 위 기준(앵커·피벗 0,1)으로 둘 것.
    /// - 리본의 마우스 올림/내림 알림(`RelicCellHover`)은 프리팹에 넣지 않고 실행 중에 붙인다(그 클래스는 `RelicHudStrip.cs` 안에 있어 프리팹에 저장할 수 없다).
    /// 프리팹은 손으로 쓰지 않는다: 메뉴 `ChronoSavior/UI 프리팹/스테이지 선택 만들기`(`Editor/BuildUiViewPrefabs`)가
    /// <see cref="CreateFromCode"/>(예전 `StageSelectScreen.Build`와 같은 Figma 좌표)로 지은 것을 그대로 저장한다.
    /// </summary>
    public class StageSelectScreenView : MonoBehaviour
    {
        public const string ResourcePath = "UI/Views/StageSelectScreenView";
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
                button.Init(button.GetComponent<Image>(), inner);
                button.OnClick = onClick;
                return button;
            }
        }

        [Header("지도 — 챕터 1~5 리본, ▼ 표시")]
        public ButtonRef[] ribbons = new ButtonRef[0];
        public Text[] ribbonLabels = new Text[0];
        public RectTransform marker;
        [Tooltip("▼ 자리 = 리본 왼쪽 위 + 이 값(px, y는 아래로) — Figma 선택 1(539:50) = 리본 기준 +24·-19")]
        public Vector2 markerOffset = new Vector2(24f, -19f);

        [Header("일반 쪽(Normal)")]
        public RectTransform normalRoot;
        public Text title;
        public Image[] mobs = new Image[0];
        public Image progFill;
        public Text progText;
        public Text lockText;
        public Text plateNum;
        public Text plateName;
        public ButtonRef[] frames = new ButtonRef[0];
        public Text[] frameLabels = new Text[0];
        public Image[] nodes = new Image[0];
        public ButtonRef arrowL = new ButtonRef();
        public ButtonRef arrowR = new ButtonRef();
        public ButtonRef enter = new ButtonRef();

        [Header("보스 쪽(Boss)")]
        public RectTransform bossRoot;
        public Image bossPortrait;
        public Text bossNameText;
        public Text bossLockText;
        public Text bossPlateNum;
        public Text bossPlateName;
        public ButtonRef bossFrame = new ButtonRef();
        public Text bossFrameLabel;
        public ButtonRef bossArrowL = new ButtonRef();
        public ButtonRef bossArrowR = new ButtonRef();
        public ButtonRef bossEnter = new ButtonRef();

        [Header("색 — 리본 글자·고른 칸·잠긴 칸·진행 점")]
        public Color textDark = new Color32(0x03, 0x19, 0x3f, 0xff);
        [Tooltip("잠긴 챕터 리본 글자의 불투명도")]
        [Range(0f, 1f)] public float lockedRibbonAlpha = 0.45f;
        public Color gold = new Color32(0xfa, 0xc7, 0x0e, 0xff);
        public Color dim = new Color(1f, 1f, 1f, 0.4f);
        public Color nodeDone = new Color32(0xfa, 0xc7, 0x0e, 0xff);
        public Color nodeTodo = new Color(0.55f, 0.55f, 0.6f, 1f);

        /// <summary>c번째(1부터) 챕터 리본 위 ▼ 자리(판 기준 anchoredPosition).</summary>
        public Vector2 MarkerPositionFor(int chapter)
        {
            int i = Mathf.Clamp(chapter - 1, 0, ribbons.Length - 1);
            var rt = (RectTransform)ribbons[i].button.transform;
            return new Vector2(rt.anchoredPosition.x + markerOffset.x, rt.anchoredPosition.y - markerOffset.y);
        }

        /// <summary>진행도 막대를 비율(0~1)로 — 채움 칸 폭을 정수 도트로 나눠 채운다.</summary>
        public void SetProgress(float ratio)
        {
            if (progFill == null) return;
            float px = Mathf.Max(1f, ((RectTransform)progFill.transform).sizeDelta.x);
            progFill.fillAmount = Mathf.Round(Mathf.Clamp01(ratio) * px) / px;
        }

        // ── 처음 모양 — Figma "마왕성 길목 선택"(32:56) 2026-11-19판. 640판 원점 = 배경 181:7의 23·21, 보스 화면은 592:85의 720·21(예전 StageSelectScreen에서 옮김) ──
        const string A = "StageMap/";
        static readonly Rect Map = new Rect(29f, 12f, 298f, 272f);                  // 590:63
        static readonly Vector2[] Ribbon =
        {
            new Vector2(228f, 184f), // 1 오른쪽 아래 숲      (539:44)
            new Vector2(134f, 176f), // 2 가운데 아래 마을    (539:43)
            new Vector2(85f, 97f),   // 3 왼쪽 위             (539:42)
            new Vector2(207f, 86f),  // 4 오른쪽 설산         (590:64)
            new Vector2(239f, 55f),  // 5 오른쪽 위 하늘 신전 (539:47)
        };
        const float RibbonW = 64f, RibbonH = 16f, MarkerSize = 16f;

        static readonly Rect Bar = new Rect(32f, 303f, 576f, 48f);                  // 539:67 stagebar
        static readonly Rect BossBar = new Rect(34f, 299f, 576f, 48f);              // 592:125 boss_stage
        static readonly Rect Plate = new Rect(32f, 303f, 110f, 48f);                // 539:70 chapter
        static readonly Rect BossPlate = new Rect(34f, 299f, 110f, 48f);            // 592:81
        static readonly Rect ArrowL = new Rect(52f, 319f, 16f, 16f), ArrowR = new Rect(122f, 319f, 16f, 16f);         // 539:75·539:74
        static readonly Rect BossArrowL = new Rect(38f, 315f, 16f, 16f), BossArrowR = new Rect(140f, 315f, 16f, 16f); // 590:71·590:72
        static readonly float[] FrameX = { 159f, 247f, 335f, 423f, 511f };          // 539:55~539:59 80×36
        const float FrameY = 305f, FrameW = 80f, FrameH = 36f;
        static readonly Rect Line = new Rect(199f, 339f, 352f, 4f);                 // 539:62
        const float NodeY = 333f, NodeSize = 16f;                                   // 539:60~539:66 (칸 가운데 아래)
        static readonly Rect BossFrame = new Rect(337f, 305f, 80f, 36f);            // 590:65

        static readonly Rect Panel = new Rect(370f, 23f, 240f, 272f);               // 539:76 맵선택
        static readonly Rect TitleRibbon = new Rect(426f, 36f, 128f, 32f);          // 539:51 (64×16 그림 2배)
        static readonly Rect MobLabel = new Rect(383f, 94f, 120f, 16f);             // 191:3 "등장 몬스터"
        static readonly float[] MobX = { 412f, 474f, 536f };                        // 540:81~540:83 32×32
        const float MobY = 126f, MobSize = 32f;
        static readonly Rect ProgLabel = new Rect(383f, 175f, 120f, 14f);           // 191:4 "스테이지 진행도"
        static readonly Rect Prog = new Rect(390f, 208f, 200f, 24f);                // 597:141·597:142
        static readonly Rect EnterBox = new Rect(426f, 245f, 128f, 24f);            // 626:4 + 195:12 "원정 떠나기"

        static readonly Rect BossIcon = new Rect(458f, 38f, 64f, 64f);              // 610:43 (32×32 그림 2배)
        static readonly Rect BossInfoLabel = new Rect(384f, 101f, 86f, 14f);        // 592:91 "보스 정보"
        static readonly Rect BossPortrait = new Rect(384f, 121f, 48f, 48f);         // 593:128
        static readonly Rect BossName = new Rect(440f, 121f, 120f, 48f);            // 593:131 "보스 이름"
        static readonly Rect PatternLabel = new Rect(383f, 176f, 80f, 17f);         // 593:132 "주요 패턴"
        static readonly float[] PatternX = { 438f, 538f };                          // 593:135·593:136 32×32
        const float PatternY = 199f;

        static ButtonRef Ref(PxButton b, RectTransform inner) => new ButtonRef { button = b, inner = inner };

        /// <summary>
        /// Figma 좌표로 모양을 짓는다 — 프리팹을 처음 만들 때(에디터 메뉴)와, 프리팹이 없을 때의 대체로만 쓴다.
        /// 프리팹을 에디터에서 고친 뒤엔 이 좌표와 달라도 된다(프리팹이 원본).
        /// </summary>
        public static StageSelectScreenView CreateFromCode(Transform parent)
        {
            var root = Px.Stretch(parent, "StageSelectScreenView");
            var v = root.gameObject.AddComponent<StageSelectScreenView>();

            // 16:9가 아닌 화면의 남는 곳은 검게(예전엔 PxScreen.Back 층) → 판(예전 PxScreen.Content)
            Px.FullFill(root, "Letterbox", Color.black);
            var board = Px.Board(root);
            Px.Image(board, "Background", PxArt.Get(A + "background"), 0f, 0f, PixelCanvas.Width, PixelCanvas.Height);
            Px.Image(board, "Map", PxArt.Get(A + "map"), Map.x, Map.y, Map.width, Map.height);

            // 지도 위 챕터 리본
            v.ribbons = new ButtonRef[ChapterConfig.ChapterCount];
            v.ribbonLabels = new Text[ChapterConfig.ChapterCount];
            for (int i = 0; i < ChapterConfig.ChapterCount; i++)
            {
                int c = i + 1;
                var b = Px.Button(board, "Chapter" + c, PxArt.Get(A + "ribbon"), Ribbon[i].x, Ribbon[i].y, RibbonW, RibbonH, null, out var inner);
                v.ribbons[i] = Ref(b, inner);
                v.ribbonLabels[i] = Px.Text(inner, "Label", ChapterConfig.NameOf(c), 0f, 0f, RibbonW, RibbonH - 2f, 8, v.textDark);
            }
            v.marker = (RectTransform)Px.Image(board, "Marker", PxArt.Get(A + "select_marker"), 0f, 0f, MarkerSize, MarkerSize).transform;

            BuildNormal(v, board);
            BuildBoss(v, board);
            return v;
        }

        static void BuildNormal(StageSelectScreenView v, RectTransform board)
        {
            var n = v.normalRoot = Px.Stretch(board, "Normal");
            Px.Image(n, "Panel", PxArt.Get(A + "panel"), Panel.x, Panel.y, Panel.width, Panel.height);
            Px.Image(n, "TitleRibbon", PxArt.Get(A + "ribbon_title"), TitleRibbon.x, TitleRibbon.y, TitleRibbon.width, TitleRibbon.height);
            v.title = Px.Text(n, "Title", "", TitleRibbon.x, TitleRibbon.y, TitleRibbon.width, TitleRibbon.height - 6f, 12, v.textDark);
            Px.Text(n, "MobLabel", "등장 몬스터", MobLabel.x, MobLabel.y, MobLabel.width, MobLabel.height, 12, Color.white, TextAnchor.MiddleLeft);
            v.mobs = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                Px.Image(n, "MobSlot" + i, PxArt.Get(A + "monster_slot"), MobX[i], MobY, MobSize, MobSize);
                // 몬스터 그림은 32×32 — 칸과 같은 자리에 1:1(줄이면 도트가 뭉개진다, 2026-11-19 사용자)
                v.mobs[i] = Px.Image(n, "Mob" + i, null, MobX[i], MobY, MobSize, MobSize);
                v.mobs[i].color = Color.white; // 그림 없이 만들 때 깔린 경고색(반투명 분홍)을 걷는다(2026-11-20)
            }
            Px.Text(n, "ProgLabel", "스테이지 진행도", ProgLabel.x, ProgLabel.y, ProgLabel.width, ProgLabel.height, 12, Color.white, TextAnchor.MiddleLeft);
            Px.Image(n, "ProgBack", PxArt.Get(A + "progress_back"), Prog.x, Prog.y, Prog.width, Prog.height);
            v.progFill = Px.Image(n, "ProgFill", PxArt.Get(A + "progress_fill"), Prog.x, Prog.y, Prog.width, Prog.height);
            v.progFill.type = Image.Type.Filled;
            v.progFill.fillMethod = Image.FillMethod.Horizontal;
            v.progText = Px.Text(n, "ProgText", "", Prog.x + 5f, Prog.y, Prog.width - 10f, Prog.height, 12, Color.white, TextAnchor.MiddleLeft)
                           .WithOutline(Px.Ink);
            v.lockText = Px.Text(n, "Lock", "", Panel.x + 12f, 229f, Panel.width - 24f, 14f, 8, v.gold);

            Px.Image(n, "Bar", PxArt.Get(A + "stage_bar"), Bar.x, Bar.y, Bar.width, Bar.height);
            Px.Image(n, "Plate", PxArt.Get(A + "chapter_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            v.plateNum = Px.Text(n, "PlateNum", "", Plate.x + 22f, Plate.y + 5f, 66f, 17f, 12, Color.white);
            v.plateName = Px.Text(n, "PlateName", "", Plate.x + 8f, Plate.y + 24f, 94f, 17f, 10, Color.white);
            Px.Image(n, "Line", PxArt.Get(A + "stage_line"), Line.x, Line.y, Line.width, Line.height);
            int stages = ChapterConfig.StagesPerChapter;
            v.nodes = new Image[stages];
            v.frames = new ButtonRef[stages];
            v.frameLabels = new Text[stages];
            for (int i = 0; i < stages; i++)
            {
                int s = i + 1;
                v.nodes[i] = Px.Image(n, "Node" + s, PxArt.Get(A + "stage_node"), FrameX[i] + (FrameW - NodeSize) / 2f, NodeY, NodeSize, NodeSize);
                var f = Px.Button(n, "Stage" + s, PxArt.Get(A + "stage_frame"), FrameX[i], FrameY, FrameW, FrameH, null, out var inner);
                v.frames[i] = Ref(f, inner);
                v.frameLabels[i] = Px.Text(inner, "Label", "", 0f, 0f, FrameW, FrameH - 4f, 12, Color.white);
            }

            var l = Px.Button(n, "Prev", PxArt.Get(A + "arrow_left"), ArrowL.x, ArrowL.y, ArrowL.width, ArrowL.height, null, out var li);
            var r = Px.Button(n, "Next", PxArt.Get(A + "arrow_right"), ArrowR.x, ArrowR.y, ArrowR.width, ArrowR.height, null, out var ri);
            v.arrowL = Ref(l, li);
            v.arrowR = Ref(r, ri);
            var e = Px.TextButton(n, "Enter", PxArt.Get(A + "enter_button"), EnterBox.x, EnterBox.y, EnterBox.width, EnterBox.height,
                                  "원정 떠나기", 12, v.textDark, null);
            v.enter = Ref(e, e.Inner);
        }

        static void BuildBoss(StageSelectScreenView v, RectTransform board)
        {
            var b = v.bossRoot = Px.Stretch(board, "Boss");
            Px.Image(b, "Panel", PxArt.Get(A + "boss_panel"), Panel.x, Panel.y, Panel.width, Panel.height);
            Px.Image(b, "Icon", PxArt.Get(A + "boss_icon"), BossIcon.x, BossIcon.y, BossIcon.width, BossIcon.height);
            Px.Text(b, "InfoLabel", "보스 정보", BossInfoLabel.x, BossInfoLabel.y, BossInfoLabel.width, BossInfoLabel.height, 12, Color.white, TextAnchor.MiddleLeft);
            Px.Image(b, "PortraitFrame", PxArt.Get(A + "boss_portrait"), BossPortrait.x, BossPortrait.y, BossPortrait.width, BossPortrait.height);
            // 보스 그림(80×64)에서 위쪽 가운데 32×32를 잘라 1:1로(줄이면 도트가 뭉개진다)
            v.bossPortrait = Px.Image(b, "Portrait", null, BossPortrait.x + 8f, BossPortrait.y + 8f, 32f, 32f);
            v.bossPortrait.color = Color.white; // 경고색(분홍) 걷기
            v.bossNameText = Px.Text(b, "Name", "", BossName.x, BossName.y, BossName.width, BossName.height, 12, Color.white, TextAnchor.MiddleLeft);
            Px.Text(b, "PatternLabel", "주요 패턴", PatternLabel.x, PatternLabel.y, PatternLabel.width, PatternLabel.height, 12, Color.white, TextAnchor.MiddleLeft);
            foreach (var x in PatternX) Px.Image(b, "Pattern", PxArt.Get(A + "pattern_frame"), x, PatternY, 32f, 32f);
            v.bossLockText = Px.Text(b, "Lock", "", Panel.x + 12f, 232f, Panel.width - 24f, 12f, 8, v.gold);

            Px.Image(b, "Bar", PxArt.Get(A + "boss_bar"), BossBar.x, BossBar.y, BossBar.width, BossBar.height);
            Px.Image(b, "Plate", PxArt.Get(A + "boss_chapter_plate"), BossPlate.x, BossPlate.y, BossPlate.width, BossPlate.height);
            v.bossPlateNum = Px.Text(b, "PlateNum", "", BossPlate.x + 22f, BossPlate.y + 5f, 66f, 17f, 12, Color.white);
            v.bossPlateName = Px.Text(b, "PlateName", "", BossPlate.x + 8f, BossPlate.y + 24f, 94f, 17f, 10, Color.white);
            var f = Px.Button(b, "BossStage", PxArt.Get(A + "boss_frame"), BossFrame.x, BossFrame.y, BossFrame.width, BossFrame.height, null, out var inner);
            v.bossFrame = Ref(f, inner);
            v.bossFrameLabel = Px.Text(inner, "Label", "", 0f, 0f, BossFrame.width, BossFrame.height - 4f, 12, Color.white);

            var l = Px.Button(b, "Prev", PxArt.Get(A + "boss_arrow_left"), BossArrowL.x, BossArrowL.y, 16f, 16f, null, out var li);
            var r = Px.Button(b, "Next", PxArt.Get(A + "boss_arrow_right"), BossArrowR.x, BossArrowR.y, 16f, 16f, null, out var ri);
            v.bossArrowL = Ref(l, li);
            v.bossArrowR = Ref(r, ri);
            var e = Px.TextButton(b, "Enter", PxArt.Get(A + "boss_enter_button"), EnterBox.x, EnterBox.y, EnterBox.width, EnterBox.height,
                                  "원정 떠나기", 12, Color.white, null);
            v.bossEnter = Ref(e, e.Inner);
        }
    }
}
