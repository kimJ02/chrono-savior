using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 마왕성 길목 선택(스테이지 선택) — Figma "마왕성 길목 선택"(32:56) 2026-11-19판, 챕터 구조(`docs/chapter-stage-spec.md`).
    /// 로비의 "던전" 건물에서 연다.
    ///
    /// - 왼쪽 = 세계 지도 한 장(섬). 지도 위 리본 5개 = 챕터 위치(사용자 2026-11-19: 1 오른쪽 아래 숲 · 2 가운데 아래 마을 ·
    ///   3 왼쪽 위 · 4 오른쪽 설산 · 5 오른쪽 위 하늘 신전). 지금 챕터(또는 마우스를 올린 챕터) 리본 위에 ▼ 표시.
    /// - 아래 띠 = [◀ 챕터 n · 이름 ▶] + 스테이지 칸 5개(1-1 ~ 1-5)와 이어진 선. ◀▶는 "챕터 n 스테이지 → 챕터 n 보스 → 챕터 n+1 스테이지" 순으로 넘긴다
    ///   (Figma 보스 화면의 아래 띠도 같은 ◀ 챕터 ▶ 판이라 보스를 한 쪽으로 봤다).
    /// - 오른쪽 판(일반) = 스테이지 이름 리본·등장 몬스터 3칸·스테이지 진행도(처치 n/100)·원정 떠나기.
    ///   오른쪽 판(보스) = 빨간 판·보스 아이콘·보스 정보(초상·이름)·주요 패턴 2칸·원정 떠나기.
    /// - 잠긴 챕터(3~5, <see cref="ChapterConfig.PlayableChapters"/>)·잠긴 스테이지는 보이되 들어갈 수 없다.
    /// 그림은 Figma 원본 이미지 그대로(1:1, 큰 리본·보스 아이콘만 정확히 2배) — 도트가 안 깨지게. 배경만 큰 일러스트라 원본을 640×360으로 한 번 줄여 넣었다.
    /// </summary>
    public class StageSelectScreen : PxScreen
    {
        [Tooltip("몬스터 그림을 읽는 목록(Assets/Data/Enemies/MonsterCatalog.asset) — 전투 씬의 스포너와 같은 에셋. 화면은 월드를 찾지 않는다.")]
        [SerializeField] MonsterCatalog catalog;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다.</summary>
        public MonsterCatalog Catalog { get => catalog; set => catalog = value; }

        static StageSelectScreen instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;
        public static void Open() { if (instance != null) instance.open = true; }
        public static void Close() { if (instance != null) instance.open = false; }

        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Lobby;
        protected override NavLayout Layout => NavLayout.Horizontal;

        // ── Figma 좌표(640판 원점 = 배경 181:7의 23·21, 보스 화면은 592:85의 720·21) ──
        const string A = "StageMap/";
        static readonly Rect Map = new Rect(29f, 12f, 298f, 272f);                  // 590:63
        /// <summary>챕터 1~5 리본 자리(지도 위, 64×16) — 사용자가 알려 준 챕터 위치에 맞춰 Figma 리본 5개를 배정.</summary>
        static readonly Vector2[] Ribbon =
        {
            new Vector2(228f, 184f), // 1 오른쪽 아래 숲      (539:44)
            new Vector2(134f, 176f), // 2 가운데 아래 마을    (539:43)
            new Vector2(85f, 97f),   // 3 왼쪽 위             (539:42)
            new Vector2(207f, 86f),  // 4 오른쪽 설산         (590:64)
            new Vector2(239f, 55f),  // 5 오른쪽 위 하늘 신전 (539:47)
        };
        const float RibbonW = 64f, RibbonH = 16f, MarkerDX = 24f, MarkerDY = -19f, MarkerSize = 16f; // 선택 1(539:50) = 리본 기준 +24·-19

        static readonly Rect Bar = new Rect(32f, 303f, 576f, 48f);                  // 539:67 stagebar
        // 보스 쪽 띠는 Figma(34,299)에서 일반 쪽(32,303)에 맞췄다 — 두 화면이 2·4도트 어긋나 넘길 때 띠가 튀었다(2026-11-22 사용자 "스테이지 바 위치가 안 맞음")
        static readonly Rect BossBar = new Rect(32f, 303f, 576f, 48f);              // 592:125 boss_stage
        static readonly Rect Plate = new Rect(32f, 303f, 110f, 48f);                // 539:70 chapter
        static readonly Rect BossPlate = new Rect(32f, 303f, 110f, 48f);            // 592:81(띠와 같이 옮김)
        static readonly Rect ArrowL = new Rect(52f, 319f, 16f, 16f), ArrowR = new Rect(122f, 319f, 16f, 16f);         // 539:75·539:74
        static readonly Rect BossArrowL = new Rect(36f, 319f, 16f, 16f), BossArrowR = new Rect(138f, 319f, 16f, 16f); // 590:71·590:72(띠와 같이 옮김)
        static readonly float[] FrameX = { 159f, 247f, 335f, 423f, 511f };          // 539:55~539:59 80×36
        const float FrameY = 305f, FrameW = 80f, FrameH = 36f;
        static readonly Rect Line = new Rect(199f, 339f, 352f, 4f);                 // 539:62
        const float NodeY = 333f, NodeSize = 16f;                                   // 539:60~539:66 (칸 가운데 아래)
        static readonly Rect BossFrame = new Rect(335f, 309f, 80f, 36f);            // 590:65(띠와 같이 옮김)

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

        static readonly Color TextDark = Px.Hex(0x03193f);
        static readonly Color Gold = Px.Hex(0xfac70e);
        static readonly Color Dim = new Color(1f, 1f, 1f, 0.4f);
        static readonly Color NodeDone = Px.Hex(0xfac70e);
        static readonly Color NodeTodo = new Color(0.55f, 0.55f, 0.6f, 1f);

        // ── 상태 ──
        int chapter = 1;
        bool bossPage;
        int stage = 1;        // 고른 스테이지(일반 쪽)
        int hoverChapter;     // 지도 리본에 마우스를 올린 챕터(0 = 없음)

        // ── 화면 조각 ──
        RectTransform normalRoot, bossRoot;
        readonly PxButton[] frames = new PxButton[ChapterConfig.StagesPerChapter];
        readonly Text[] frameLabels = new Text[ChapterConfig.StagesPerChapter];
        readonly Image[] nodes = new Image[ChapterConfig.StagesPerChapter];
        readonly Text[] ribbonLabels = new Text[ChapterConfig.ChapterCount];
        PxButton bossFrame, arrowL, arrowR, bossArrowL, bossArrowR, enter, bossEnter;
        Text plateNum, plateName, bossPlateNum, bossPlateName, bossFrameLabel;
        Text title, progText, lockText, bossNameText, bossLockText;
        Text powerText, bossPowerText; // 권장 전투력 · 내 전투력(2026-11-22 사용자 "환산 주 스탯 - 권장 전투력")
        Image marker, progFill, bossPortrait;
        readonly Image[] mobs = new Image[3];

        protected override void Awake()
        {
            base.Awake();
            instance = this;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (instance == this) instance = null;
            // 직접 잘라 만든 보스 얼굴 그림만 지운다(목록의 원래 그림은 에셋이라 건드리지 않는다, 2026-11-20)
            foreach (var sp in createdPortraits) if (sp != null) Destroy(sp);
            createdPortraits.Clear();
            portraitCache.Clear();
        }

        protected override void Build()
        {
            if (instance == null) instance = this;
            Px.FullFill(Back, "Letterbox", Color.black);
            Px.Image(Content, "Background", PxArt.Get(A + "background"), 0f, 0f, PixelCanvas.Width, PixelCanvas.Height);
            Px.Image(Content, "Map", PxArt.Get(A + "map"), Map.x, Map.y, Map.width, Map.height);

            // 지도 위 챕터 리본 — 마우스를 올리면 ▼, 누르면 그 챕터로
            for (int i = 0; i < ChapterConfig.ChapterCount; i++)
            {
                int c = i + 1;
                var b = Px.Button(Content, "Chapter" + c, PxArt.Get(A + "ribbon"), Ribbon[i].x, Ribbon[i].y, RibbonW, RibbonH,
                                  () => GoChapter(c, false), out var inner);
                ribbonLabels[i] = Px.Text(inner, "Label", ChapterConfig.NameOf(c), 0f, 0f, RibbonW, RibbonH - 2f, 8, TextDark);
                b.OnHover = () => hoverChapter = c;
                var hov = b.gameObject.AddComponent<RelicCellHover>();
                hov.Exit = () => { if (hoverChapter == c) hoverChapter = 0; };
            }
            marker = Px.Image(Content, "Marker", PxArt.Get(A + "select_marker"), 0f, 0f, MarkerSize, MarkerSize);

            BuildNormal();
            BuildBoss();
        }

        void BuildNormal()
        {
            normalRoot = Px.Stretch(Content, "Normal");
            var n = normalRoot;
            Px.Image(n, "Panel", PxArt.Get(A + "panel"), Panel.x, Panel.y, Panel.width, Panel.height);
            Px.Image(n, "TitleRibbon", PxArt.Get(A + "ribbon_title"), TitleRibbon.x, TitleRibbon.y, TitleRibbon.width, TitleRibbon.height);
            title = Px.Text(n, "Title", "", TitleRibbon.x, TitleRibbon.y, TitleRibbon.width, TitleRibbon.height - 6f, 12, TextDark);
            Px.Text(n, "MobLabel", "등장 몬스터", MobLabel.x, MobLabel.y, MobLabel.width, MobLabel.height, 12, Color.white, TextAnchor.MiddleLeft);
            powerText = Px.Text(n, "Power", "", Panel.x + 8f, 72f, Panel.width - 16f, 14f, 10, Color.white).WithOutline(Px.Ink);
            for (int i = 0; i < 3; i++)
            {
                Px.Image(n, "MobSlot" + i, PxArt.Get(A + "monster_slot"), MobX[i], MobY, MobSize, MobSize);
                // 몬스터 그림은 32×32 — 칸과 같은 자리에 1:1(줄이면 도트가 뭉개진다, 2026-11-19 사용자)
                mobs[i] = Px.Image(n, "Mob" + i, null, MobX[i], MobY, MobSize, MobSize);
                mobs[i].color = Color.white; // 그림 없이 만들 때 깔린 경고색(반투명 분홍)을 걷는다 — 안 걷으면 몹 그림이 분홍으로 물든다(2026-11-20)
            }
            Px.Text(n, "ProgLabel", "스테이지 진행도", ProgLabel.x, ProgLabel.y, ProgLabel.width, ProgLabel.height, 12, Color.white, TextAnchor.MiddleLeft);
            Px.Image(n, "ProgBack", PxArt.Get(A + "progress_back"), Prog.x, Prog.y, Prog.width, Prog.height);
            progFill = Px.Image(n, "ProgFill", PxArt.Get(A + "progress_fill"), Prog.x, Prog.y, Prog.width, Prog.height);
            progFill.type = Image.Type.Filled;
            progFill.fillMethod = Image.FillMethod.Horizontal;
            progText = Px.Text(n, "ProgText", "", Prog.x + 5f, Prog.y, Prog.width - 10f, Prog.height, 12, Color.white, TextAnchor.MiddleLeft)
                         .WithOutline(Px.Ink);
            lockText = Px.Text(n, "Lock", "", Panel.x + 12f, 229f, Panel.width - 24f, 14f, 8, Gold);

            Px.Image(n, "Bar", PxArt.Get(A + "stage_bar"), Bar.x, Bar.y, Bar.width, Bar.height);
            Px.Image(n, "Plate", PxArt.Get(A + "chapter_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            plateNum = Px.Text(n, "PlateNum", "", Plate.x + 22f, Plate.y + 5f, 66f, 17f, 12, Color.white);
            plateName = Px.Text(n, "PlateName", "", Plate.x + 8f, Plate.y + 24f, 94f, 17f, 10, Color.white);
            Px.Image(n, "Line", PxArt.Get(A + "stage_line"), Line.x, Line.y, Line.width, Line.height);
            for (int i = 0; i < ChapterConfig.StagesPerChapter; i++)
            {
                int s = i + 1;
                nodes[i] = Px.Image(n, "Node" + s, PxArt.Get(A + "stage_node"), FrameX[i] + (FrameW - NodeSize) / 2f, NodeY, NodeSize, NodeSize);
                // 칸을 누르면 그 스테이지를 고르기만 한다 — 출발은 "원정 떠나기"(2026-11-20 사용자: 누르자마자 하루가 줄던 것)
                frames[i] = Px.Button(n, "Stage" + s, PxArt.Get(A + "stage_frame"), FrameX[i], FrameY, FrameW, FrameH,
                                      () => PickStage(s), out var inner);
                frameLabels[i] = Px.Text(inner, "Label", "", 0f, 0f, FrameW, FrameH - 4f, 12, Color.white);
            }

            arrowL = Px.Button(n, "Prev", PxArt.Get(A + "arrow_left"), ArrowL.x, ArrowL.y, ArrowL.width, ArrowL.height, () => Step(-1), out _);
            Mirror(arrowL); // 원본 그림 arrow_left가 오른쪽(▶)을 보고 있다 — Figma는 뒤집어 썼다(2026-11-22 사용자 "화살표 UI가 이상함")
            arrowR = Px.Button(n, "Next", PxArt.Get(A + "arrow_right"), ArrowR.x, ArrowR.y, ArrowR.width, ArrowR.height, () => Step(+1), out _);
            enter = Px.TextButton(n, "Enter", PxArt.Get(A + "enter_button"), EnterBox.x, EnterBox.y, EnterBox.width, EnterBox.height,
                                  "원정 떠나기", 12, TextDark, EnterSelected);

            // 키보드 순서: ◀ · 1~5 · ▶ · 원정 떠나기 (보스 쪽은 아래에서 따로)
            AddNav(arrowL);
            foreach (var f in frames) AddNav(f);
            AddNav(arrowR);
            AddNav(enter);
        }

        void BuildBoss()
        {
            bossRoot = Px.Stretch(Content, "Boss");
            var b = bossRoot;
            Px.Image(b, "Panel", PxArt.Get(A + "boss_panel"), Panel.x, Panel.y, Panel.width, Panel.height);
            Px.Image(b, "Icon", PxArt.Get(A + "boss_icon"), BossIcon.x, BossIcon.y, BossIcon.width, BossIcon.height);
            Px.Text(b, "InfoLabel", "보스 정보", BossInfoLabel.x, BossInfoLabel.y, BossInfoLabel.width, BossInfoLabel.height, 12, Color.white, TextAnchor.MiddleLeft);
            Px.Image(b, "PortraitFrame", PxArt.Get(A + "boss_portrait"), BossPortrait.x, BossPortrait.y, BossPortrait.width, BossPortrait.height);
            // 보스 그림(80×64)에서 위쪽 가운데 32×32를 잘라 1:1로(줄이면 도트가 뭉개진다)
            bossPortrait = Px.Image(b, "Portrait", null, BossPortrait.x + 8f, BossPortrait.y + 8f, 32f, 32f);
            bossPortrait.color = Color.white; // 경고색(분홍) 걷기 — 위와 같다
            bossNameText = Px.Text(b, "Name", "", BossName.x, BossName.y, BossName.width, BossName.height, 12, Color.white, TextAnchor.MiddleLeft);
            bossPowerText = Px.Text(b, "Power", "", Panel.x + 8f, 25f, Panel.width - 16f, 12f, 10, Color.white).WithOutline(Px.Ink); // 보스 아이콘 위 빈 줄
            Px.Text(b, "PatternLabel", "주요 패턴", PatternLabel.x, PatternLabel.y, PatternLabel.width, PatternLabel.height, 12, Color.white, TextAnchor.MiddleLeft);
            foreach (var x in PatternX) Px.Image(b, "Pattern", PxArt.Get(A + "pattern_frame"), x, PatternY, 32f, 32f);
            bossLockText = Px.Text(b, "Lock", "", Panel.x + 12f, 232f, Panel.width - 24f, 12f, 8, Gold);

            Px.Image(b, "Bar", PxArt.Get(A + "boss_bar"), BossBar.x, BossBar.y, BossBar.width, BossBar.height);
            Px.Image(b, "Plate", PxArt.Get(A + "boss_chapter_plate"), BossPlate.x, BossPlate.y, BossPlate.width, BossPlate.height);
            bossPlateNum = Px.Text(b, "PlateNum", "", BossPlate.x + 22f, BossPlate.y + 5f, 66f, 17f, 12, Color.white);
            bossPlateName = Px.Text(b, "PlateName", "", BossPlate.x + 8f, BossPlate.y + 24f, 94f, 17f, 10, Color.white);
            // 보스 칸도 누르면 고르기만(출발은 원정 떠나기 — 일반 칸과 같다, 2026-11-20). 키보드 확인이면 원정 떠나기로 선택 이동.
            bossFrame = Px.Button(b, "BossStage", PxArt.Get(A + "boss_frame"), BossFrame.x, BossFrame.y, BossFrame.width, BossFrame.height,
                                  () => SelectNav(NavBossFrame), out var inner);
            bossFrameLabel = Px.Text(inner, "Label", "", 0f, 0f, BossFrame.width, BossFrame.height - 4f, 12, Color.white);

            bossArrowL = Px.Button(b, "Prev", PxArt.Get(A + "boss_arrow_left"), BossArrowL.x, BossArrowL.y, 16f, 16f, () => Step(-1), out _);
            bossArrowR = Px.Button(b, "Next", PxArt.Get(A + "boss_arrow_right"), BossArrowR.x, BossArrowR.y, 16f, 16f, () => Step(+1), out _);
            Mirror(bossArrowR); // 원본 그림 boss_arrow_right가 왼쪽(◀)을 보고 있다 — 위와 같다
            bossEnter = Px.TextButton(b, "Enter", PxArt.Get(A + "boss_enter_button"), EnterBox.x, EnterBox.y, EnterBox.width, EnterBox.height,
                                      "원정 떠나기", 12, Color.white, EnterSelected);
            AddNav(bossArrowL);
            AddNav(bossFrame);
            AddNav(bossArrowR);
            AddNav(bossEnter);
        }

        // Nav 순서: 일반 0 ◀, 1~5 스테이지, 6 ▶, 7 원정 / 보스 8 ◀, 9 보스, 10 ▶, 11 원정
        const int NavStage0 = 1, NavNormalEnter = 7, NavBossFrame = 9, NavBossEnter = 11;

        protected override void OnShown()
        {
            // 처음엔 지금 이어서 할 곳 — 열린 가장 앞 챕터에서 아직 안 깬 첫 스테이지(다 깼으면 보스, 보스까지 깼으면 그 챕터 5스테이지)
            var p = ProfileService.Current;
            (chapter, stage, bossPage) = NextPlay(p);
            hoverChapter = 0;
        }

        /// <summary>이어서 할 곳(챕터, 스테이지, 보스 쪽인지).</summary>
        public static (int chapter, int stage, bool boss) NextPlay(PlayerProfile p)
        {
            int best = 1;
            for (int c = 1; c <= ChapterConfig.PlayableChapters; c++) if (p.IsRegionOpen(c)) best = c;
            for (int s = 1; s <= ChapterConfig.StagesPerChapter; s++)
                if (!p.IsStageCleared(best, s)) return (best, s, false);
            if (!p.IsBossCleared(best)) return (best, ChapterConfig.BossStage, true);
            return (best, ChapterConfig.StagesPerChapter, false);
        }

        protected override int DefaultNav => bossPage ? NavBossFrame : NavStage0 + stage - 1;

        protected override void OnEscape() => Close();

        protected override void OnNavChanged(int index)
        {
            // 키보드·마우스로 스테이지 칸을 고르면 그 스테이지가 오른쪽 판에
            if (!bossPage && index >= NavStage0 && index < NavStage0 + ChapterConfig.StagesPerChapter) stage = index - NavStage0 + 1;
        }

        /// <summary>◀▶ — 챕터 n 스테이지 → 챕터 n 보스 → 챕터 n+1 스테이지 …</summary>
        void Step(int dir)
        {
            int page = (chapter - 1) * 2 + (bossPage ? 1 : 0) + dir;
            int max = ChapterConfig.ChapterCount * 2 - 1;
            if (page < 0 || page > max) return;
            // 앞으로 넘어 새 챕터면 1스테이지부터, 보스에서 뒤로 오면 바로 옆인 5스테이지
            if (page % 2 == 0) stage = dir > 0 ? 1 : ChapterConfig.StagesPerChapter;
            GoChapter(page / 2 + 1, page % 2 == 1);
        }

        // ── 키보드(2026-11-22 사용자): ←→ = 스테이지 1~5만, ↑ = 다음(챕터 n 보스 → n+1), ↓ = 이전, 확인 = 출발 ──
        protected override bool OwnsNavKeys => true;

        protected override void OnKeys()
        {
            if (GameInput.MenuUpDown) { Step(+1); return; }
            if (GameInput.MenuDownDown) { Step(-1); return; }
            if (!bossPage)
            {
                int dx = (GameInput.MenuRightDown ? 1 : 0) - (GameInput.MenuLeftDown ? 1 : 0);
                if (dx != 0)
                {
                    stage = Mathf.Clamp(stage + dx, 1, ChapterConfig.StagesPerChapter);
                    SelectNav(NavStage0 + stage - 1);
                    return;
                }
            }
            if (GameInput.MenuConfirmDown) EnterSelected();
        }

        void GoChapter(int c, bool boss)
        {
            chapter = Mathf.Clamp(c, 1, ChapterConfig.ChapterCount);
            bossPage = boss;
            if (!boss) stage = Mathf.Clamp(stage, 1, ChapterConfig.StagesPerChapter);
            Refresh();
            SelectNav(boss ? NavBossFrame : NavStage0 + stage - 1);
        }

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            normalRoot.gameObject.SetActive(!bossPage);
            bossRoot.gameObject.SetActive(bossPage);
            for (int k = 0; k < Nav.Count; k++)
            {
                bool bossItem = k >= NavBossFrame - 1;
                if (bossItem == bossPage) continue;
                Nav[k].Selected = false;
            }

            // 지도 — 리본 글자, ▼(마우스를 올린 챕터 → 없으면 지금 챕터)
            for (int i = 0; i < ChapterConfig.ChapterCount; i++)
                ribbonLabels[i].color = p.IsRegionOpen(i + 1) ? TextDark : new Color(TextDark.r, TextDark.g, TextDark.b, 0.45f);
            int mc = hoverChapter > 0 ? hoverChapter : chapter;
            var m = Ribbon[mc - 1];
            ((RectTransform)marker.transform).anchoredPosition = new Vector2(m.x + MarkerDX, -(m.y + MarkerDY));

            bool chapterOpen = p.IsRegionOpen(chapter);
            string chapterLock = !ChapterConfig.IsPlayable(chapter) ? "아직 열리지 않은 챕터" : !chapterOpen ? "앞 챕터 보스를 잡아야 열린다" : "";

            if (!bossPage)
            {
                plateNum.text = $"챕터 {chapter}";
                plateName.text = ChapterConfig.NameOf(chapter);
                arrowL.Interactable = chapter > 1;
                for (int i = 0; i < ChapterConfig.StagesPerChapter; i++)
                {
                    int s = i + 1;
                    bool openS = p.IsStageOpen(chapter, s), done = p.IsStageCleared(chapter, s);
                    frameLabels[i].text = ChapterConfig.StageLabel(chapter, s);
                    frameLabels[i].color = s == stage ? Gold : openS ? Color.white : Dim;
                    nodes[i].color = done ? NodeDone : NodeTodo;
                }

                title.text = ChapterConfig.StageLabel(chapter, stage);
                var types = MonstersOf(chapter);
                for (int i = 0; i < 3; i++)
                {
                    Sprite sp = i < types.Length ? SpriteOf(types[i], chapter) : null;
                    mobs[i].sprite = sp;
                    mobs[i].enabled = sp != null;
                }
                int kills = Mathf.Min(p.StageKills(chapter, stage), ChapterConfig.StageKillTarget);
                float ratio = kills / (float)ChapterConfig.StageKillTarget;
                progFill.fillAmount = Mathf.Round(ratio * Prog.width) / Prog.width; // 도트 단위로 채운다
                bool done5 = p.IsStageCleared(chapter, stage);
                progText.text = done5 ? "클리어" : $"{Mathf.RoundToInt(ratio * 100f)}%";
                bool can = p.IsStageOpen(chapter, stage);
                SetPower(powerText, p, chapter, stage);
                lockText.text = chapterLock != "" ? chapterLock : !can ? "앞 스테이지를 깨야 열린다"
                              : stage == ChapterConfig.StagesPerChapter && !done5 ? $"{ChapterConfig.StageKillTarget}마리 처치 후 포탈 → 중간 보스" : "";
                enter.Interactable = can && p.CanEnterStage;
            }
            else
            {
                bossPlateNum.text = $"챕터 {chapter}";
                bossPlateName.text = ChapterConfig.NameOf(chapter);
                bossFrameLabel.text = ChapterConfig.StageLabel(chapter, ChapterConfig.BossStage);
                bossArrowR.Interactable = chapter < ChapterConfig.ChapterCount;
                bossNameText.text = RegionConfig.BossNameOf(chapter);
                SetPower(bossPowerText, p, chapter, ChapterConfig.BossStage);
                var bs = BossPortraitOf(chapter);
                bossPortrait.sprite = bs;
                bossPortrait.enabled = bs != null;
                bool can = p.IsStageOpen(chapter, ChapterConfig.BossStage);
                bossLockText.text = chapterLock != "" ? chapterLock : p.IsBossCleared(chapter) ? "격파함 — 다시 도전할 수 있다"
                                  : !can ? "1~5 스테이지를 모두 깨야 열린다" : "";
                bossEnter.Interactable = can && p.CanEnterStage;
            }
        }

        /// <summary>
        /// 그 챕터 카드에 **보여 줄** 몹 종류(칸 3개) — 원본 `rollSpawnType` 순서(박쥐·고블린 → 멧돼지 → 해골 궁수 → 슬라임, 2지역부터 오우거).
        /// 스폰 확률표(`Systems/EnemySpawnTable`)의 복사본이 아니라 **표시 정책**이다. 챕터 = 지역 번호.
        /// 표와 어긋나지 않게 `Tests/PlayMode/UI/StageSelectShowcaseTests`가 '보여 주는 종류는 그 지역에서 실제로 나온다'를 고정한다.
        /// </summary>
        public static EnemyType[] MonstersOf(int region)
        {
            if (region <= 1) return new[] { EnemyType.Flyer, EnemyType.Grunt };
            if (region == 2) return new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Brute };
            if (region == 3) return new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Shooter, EnemyType.Brute };
            return new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Shooter, EnemyType.Splitter };
        }

        /// <summary>
        /// 그 챕터에서 실제로 보이는 그림 — 지역 그림(움직임 첫 장, 까마귀·쥐·멧돼지·오크통 32×32)이 있으면 그것, 없으면 예전 기본 그림.
        /// 예전엔 기본 그림(분홍 도깨비불 등)만 읽어 게임과 다른 몹이 떴다(2026-11-19 사용자).
        /// </summary>
        Sprite SpriteOf(EnemyType type, int chapter)
        {
            var d = catalog != null ? catalog.Find(type) : null;
            if (d == null) return null;
            var skin = d.SkinFor(chapter);
            if (skin != null && skin.move != null && skin.move.frames != null && skin.move.frames.Length > 0 && skin.move.frames[0] != null)
                return skin.move.frames[0];
            return d.sprite;
        }

        readonly System.Collections.Generic.Dictionary<int, Sprite> portraitCache = new System.Collections.Generic.Dictionary<int, Sprite>();
        readonly System.Collections.Generic.List<Sprite> createdPortraits = new System.Collections.Generic.List<Sprite>();

        // 보스 얼굴 32×32를 자를 자리 — 그림(80×64) 왼쪽 위 기준 픽셀(2026-11-20 사용자 "1보스 얼굴 가운데로").
        // 두꺼비(덩어리 지배자): 두 눈(x 17~44, y 19~27)이 가운데 오게. 부서진 여신: 머리·후광이 가운데 오게.
        static readonly Vector2Int BlobFace = new Vector2Int(14, 8);
        static readonly Vector2Int GoddessFace = new Vector2Int(24, 0);

        /// <summary>그 챕터 보스 그림에서 얼굴 32×32를 잘라 낸 것 — 1:1로 찍어 도트를 지킨다. 그림이 없으면 목록의 예전 보스 그림.</summary>
        Sprite BossPortraitOf(int chapter)
        {
            if (portraitCache.TryGetValue(chapter, out var cached)) return cached;
            Sprite src = null;
            Vector2Int? face = null;
            var prefab = catalog != null ? catalog.BossPrefabFor(chapter) : null;
            if (prefab != null)
            {
                // 보스 그림은 프리팹의 움직이는 그림(서 있기·튀기 첫 장) — 렌더러 기본 그림은 조약돌 같은 대체 그림이라 안 쓴다
                SpriteFlipbook fb = null;
                var goddess = prefab.GetComponent<ChronoSavior.Enemies.BrokenGoddessBoss>();
                var blob = prefab.GetComponent<ChronoSavior.Enemies.BlobLordBoss>();
                if (goddess != null) { fb = goddess.IdleArt; face = GoddessFace; }
                else if (blob != null) { fb = blob.BounceArt; face = BlobFace; }
                if (fb != null && fb.frames != null && fb.frames.Length > 0) src = fb.frames[0];
            }
            Sprite result = catalog != null ? catalog.boss : null;
            if (src != null)
            {
                var r = src.textureRect;
                const float S = 32f;
                float w = Mathf.Min(S, r.width), h = Mathf.Min(S, r.height);
                float x = Mathf.Round(r.x + (r.width - w) / 2f), y = Mathf.Round(r.y + r.height - h - Mathf.Max(0f, (r.height - h) * 0.25f));
                if (face.HasValue && src.texture.width >= S && src.texture.height >= S)
                {
                    // 얼굴 자리(그림 왼쪽 위 기준) — 텍스처 좌표(아래가 0)로 바꾼다
                    w = h = S;
                    x = Mathf.Clamp(face.Value.x, 0, src.texture.width - (int)S);
                    y = Mathf.Clamp(src.texture.height - face.Value.y - (int)S, 0, src.texture.height - (int)S);
                }
                result = Sprite.Create(src.texture, new Rect(x, y, w, h), new Vector2(0.5f, 0.5f), src.pixelsPerUnit);
                result.name = src.name + "_portrait";
                createdPortraits.Add(result);
            }
            portraitCache[chapter] = result;
            return result;
        }

        /// <summary>스테이지 칸을 눌렀다 — 그 스테이지를 고르고 선택 표시도 그 칸으로(출발하지 않는다).</summary>
        void PickStage(int s)
        {
            if (bossPage) return;
            stage = s;
            SelectNav(NavStage0 + s - 1); // 누르면 고르기만(출발은 원정 떠나기·키보드 확인)
        }

        static readonly Color PowerOk = Px.Hex(0x8be38b), PowerTight = Px.Hex(0xfac70e), PowerLow = Px.Hex(0xff6b6b);

        /// <summary>"권장 전투력 21,500 · 내 전투력 40,000" — 내 쪽이 권장의 100% 이상 초록, 70% 이상 노랑, 그 아래 빨강.</summary>
        static void SetPower(Text t, PlayerProfile p, int chapter, int stage)
        {
            if (t == null) return;
            int rec = CombatPower.RecommendedFor(chapter, stage), mine = CombatPower.Of(p);
            int g = CombatPower.Grade(mine, rec);
            var c = g == 2 ? PowerOk : g == 1 ? PowerTight : PowerLow;
            t.text = $"권장 전투력 {rec:N0} · 내 전투력 <color=#{ColorUtility.ToHtmlStringRGB(c)}>{mine:N0}</color>";
        }

        /// <summary>좌우로 뒤집는다(픽셀 그대로 — 1:1 유지). 기준점이 왼쪽 위라 폭만큼 옮겨 같은 자리에 둔다.</summary>
        static void Mirror(PxButton b)
        {
            var rt = (RectTransform)b.transform;
            rt.localScale = new Vector3(-1f, 1f, 1f);
            rt.anchoredPosition += new Vector2(rt.sizeDelta.x, 0f);
        }

        void EnterSelected() =>
            TryEnter(Flow, chapter, bossPage ? ChapterConfig.BossStage : stage, () => open = false); // 전투 씬이 실제로 뜬 뒤에만 닫는다

        /// <summary>
        /// 그 챕터의 스테이지에 들어간다 — 잠김·멸망(0일)을 확인하고(안 되면 공용 팝업) 사냥을 시작한다. 들어가면 멸망까지 하루(보스 스테이지 포함).
        /// 스테이지 선택의 "원정 떠나기"와 결과 화면의 "다시 입장"이 같이 쓴다.
        /// </summary>
        public static bool TryEnter(GameFlow flow, int chapter, int stage, System.Action entered = null)
        {
            var p = ProfileService.Current;
            string label = ChapterConfig.StageLabel(chapter, stage);
            if (!ChapterConfig.IsPlayable(chapter)) { CommonPopup.Alert(label, "아직 열리지 않은 챕터다."); return false; }
            if (!p.IsStageOpen(chapter, stage))
            {
                CommonPopup.Alert(label, stage == ChapterConfig.BossStage ? "1~5 스테이지를 모두 깨야 열린다." : "앞 스테이지를 깨야 열린다.");
                return false;
            }
            if (!p.CanEnterStage)
            {
                CommonPopup.Alert("멸망", "멸망까지 남은 기간이 다했다.\n시간 회귀(환생)로 되돌아가야 한다.");
                return false;
            }
            if (flow == null) return false;
            return flow.StartStage(chapter, stage, CommonPopup.AlertIfTransitionFails(label, entered));
        }

        /// <summary>예전 호출(지역 하나) — 그 챕터에서 이어서 할 스테이지로.</summary>
        public static bool TryEnter(GameFlow flow, int chapter, System.Action entered = null)
        {
            var p = ProfileService.Current;
            int s = 1;
            while (s < ChapterConfig.StagesPerChapter && p.IsStageCleared(chapter, s)) s++;
            return TryEnter(flow, chapter, s, entered);
        }
    }
}
