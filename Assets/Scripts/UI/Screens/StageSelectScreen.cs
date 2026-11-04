using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 마왕성 길목 선택(스테이지 선택) — Figma "마왕성 길목 선택"(32:56)의 640×360판: 왼쪽 판 = 스테이지 목록, 오른쪽 판 = 고른 스테이지
    /// (stage N · 등장 몬스터 5칸 · 스테이지 진행도 막대 · 보스 정보 · ↻ 닫기 · 원정 떠나기). 로비의 "던전" 건물에서 연다.
    ///
    /// Figma 640판은 왼쪽 판이 비어 있어서 **옛 1920판 목록**(stage1~9 · 돌파도 · 진입/잠김)으로 채웠다. ↻ = 창 닫기(사용자 "설정창 끄는 버튼").
    /// 보스 도전 버튼은 640판에 없다 — 보스는 지금처럼 필드 끝 포탈(사용자 2026-09-16).
    /// 원정 떠나기 = 사냥 시작, 그리고 **멸망까지 하루가 준다**(사용자 2026-10-24, `PlayerProfile.doomDays`). 입장료는 없다(사용자 2026-10-25 — 원본은 받았다).
    /// 등장 몬스터는 실제 출현표(`EnemySpawnTable`, 원본 `rollSpawnType`)대로, 그림은 스포너가 쓰는 몹 그림 그대로.
    /// </summary>
    public class StageSelectScreen : PxScreen
    {
        static StageSelectScreen instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;
        public static void Open() { if (instance != null) instance.open = true; }
        public static void Close() { if (instance != null) instance.open = false; }

        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Lobby;
        protected override NavLayout Layout => NavLayout.Vertical;

        // Figma 좌표(640판 원점 = 배경 181:7의 23·21)
        static readonly Color Bg = Px.Hex(0x656565);                         // 181:7
        static readonly Color BgBorder = Px.Hex(0x472e17);
        static readonly Rect ListFrame = new Rect(17f, 23f, 240f, 272f);      // 184:3 "스테이지 창 프레임 2"
        static readonly Rect InfoFrame = new Rect(337f, 23f, 240f, 272f);     // 184:2 "스테이지 창 프레임 1"
        static readonly Rect StageTitle = new Rect(349f, 34f, 200f, 29f);     // 189:2 "stage 3" 20px
        static readonly Rect MobLabel = new Rect(349f, 78f, 120f, 16f);       // 191:3 "등장 몬스터" 12px
        static readonly float[] MobX = { 357f, 399f, 441f, 483f, 525f };     // 187:8~187:12 32×32
        const float MobY = 109f, MobSize = 32f;
        static readonly Rect ProgLabel = new Rect(349f, 159f, 120f, 14f);     // 191:4 "스테이지 진행도" 12px
        static readonly Rect ProgBar = new Rect(349f, 194f, 128f, 32f);       // 184:5 "진행도 1"
        static readonly Rect ProgText = new Rect(349f, 202f, 128f, 16f);      // 191:8 "100/100" 12px 가운데
        static readonly Rect BossLabel = new Rect(493f, 159f, 80f, 14f);      // 191:5 "보스 정보" 12px
        static readonly Rect BossFrame = new Rect(493f, 178f, 64f, 64f);      // 186:7 (32px 그림 2배)
        static readonly Rect CloseBox = new Rect(368f, 260f, 24f, 24f);       // 195:10 + 195:11 ↻(19×19)
        static readonly Rect EnterBox = new Rect(429f, 257f, 128f, 32f);      // 193:9 + 195:12 "원정 떠나기" 12px #03193f
        // 왼쪽 목록(Figma에 없음 — 옛 1920판 내용)
        const float RowX = 27f, RowW = 220f, RowTop = 32f, RowStep = 28.5f, RowH = 24f;
        static readonly Color RowPlate = new Color(0f, 0f, 0f, 0.18f);
        static readonly Color RowSelected = Px.Hex(0xfac70e);
        static readonly Color EnterText = Px.Hex(0x03193f);
        static readonly Color32 BarRed = new Color32(234, 50, 60, 255);
        static readonly Color32 BarEmpty = new Color32(57, 31, 33, 255);

        readonly Text[] rowNames = new Text[RegionConfig.Count];
        readonly Text[] rowStates = new Text[RegionConfig.Count];
        readonly Image[] mobs = new Image[5];
        Text title, infoLine, progText;
        Image progFill, boss;
        Image cursor;
        PxButton enter;
        int selected = 1;
        Vector2Int barSpan;

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
            Px.FullFill(Back, "Background", Bg);
            Px.Fill(Content, "Border", BgBorder, 0f, 0f, PixelCanvas.Width, PixelCanvas.Height);
            Px.Fill(Content, "Inner", Bg, 1f, 1f, PixelCanvas.Width - 2f, PixelCanvas.Height - 2f);
            Px.Image(Content, "ListFrame", PxArt.Get("Stage/frame_list"), ListFrame.x, ListFrame.y, ListFrame.width, ListFrame.height);
            Px.Image(Content, "InfoFrame", PxArt.Get("Stage/frame_info"), InfoFrame.x, InfoFrame.y, InfoFrame.width, InfoFrame.height);

            for (int i = 0; i < RegionConfig.Count; i++)
            {
                int region = i + 1;
                float y = RowTop + i * RowStep;
                var plate = Px.Fill(Content, "Row" + region, RowPlate, RowX, y, RowW, RowH);
                var inner = Px.Stretch(plate.transform, "Inner");
                var b = plate.gameObject.AddComponent<PxButton>();
                b.Init(plate, inner);
                b.OnClick = () => { selected = region; Enter(); };
                rowNames[i] = Px.Text(inner, "Name", $"stage {region}", 18f, 0f, 120f, RowH, 12, Color.white, TextAnchor.MiddleLeft);
                rowStates[i] = Px.Text(inner, "State", "", 100f, 0f, RowW - 106f, RowH, 8, Color.white, TextAnchor.MiddleRight);
                AddNav(b);
            }
            cursor = Px.Image(Content, "Cursor", PxArt.Get("Common/cursor"), 0f, 0f, 12f, 12f);

            title = Px.Text(Content, "Title", "", StageTitle.x, StageTitle.y, StageTitle.width, StageTitle.height, 20, Color.white,
                            TextAnchor.MiddleLeft);
            infoLine = Px.Text(Content, "Info", "", 349f, 61f, 216f, 12f, 8, Color.white, TextAnchor.MiddleLeft);
            Px.Text(Content, "MobLabel", "등장 몬스터", MobLabel.x, MobLabel.y, MobLabel.width, MobLabel.height, 12, Color.white,
                    TextAnchor.MiddleLeft);
            for (int i = 0; i < 5; i++)
            {
                Px.Image(Content, "MobFrame" + i, PxArt.Get("Stage/mob_frame"), MobX[i], MobY, MobSize, MobSize);
                mobs[i] = Px.Image(Content, "Mob" + i, null, MobX[i] + 4f, MobY + 4f, MobSize - 8f, MobSize - 8f);
                mobs[i].preserveAspect = true;
            }

            Px.Text(Content, "ProgLabel", "스테이지 진행도", ProgLabel.x, ProgLabel.y, ProgLabel.width, ProgLabel.height, 12, Color.white,
                    TextAnchor.MiddleLeft);
            var bar = PxArt.Get("Stage/progress");
            barSpan = PxArt.ColorSpanX(bar, BarRed);
            Px.Image(Content, "ProgBack", PxArt.Derive(bar, "empty", c => PxArt.Same(c, BarRed) ? BarEmpty : c),
                     ProgBar.x, ProgBar.y, ProgBar.width, ProgBar.height);
            progFill = Px.Image(Content, "ProgFill", PxArt.Derive(bar, "fill", c => PxArt.Same(c, BarRed) ? c : new Color32(0, 0, 0, 0)),
                                ProgBar.x, ProgBar.y, ProgBar.width, ProgBar.height);
            progFill.type = Image.Type.Filled;
            progFill.fillMethod = Image.FillMethod.Horizontal;
            progText = Px.Text(Content, "ProgText", "", ProgText.x, ProgText.y, ProgText.width, ProgText.height, 12, Color.white)
                         .WithOutline(Px.Ink);

            Px.Text(Content, "BossLabel", "보스 정보", BossLabel.x, BossLabel.y, BossLabel.width, BossLabel.height, 12, Color.white,
                    TextAnchor.MiddleLeft);
            Px.Image(Content, "BossFrame", PxArt.Get("Stage/boss_frame"), BossFrame.x, BossFrame.y, BossFrame.width, BossFrame.height);
            boss = Px.Image(Content, "Boss", null, BossFrame.x + 8f, BossFrame.y + 8f, 48f, 48f);
            boss.preserveAspect = true;

            AddNav(Px.IconButton(Content, "Close", PxArt.Get("Common/button_small"), PxArt.Get("Icons/close"), CloseBox.x, CloseBox.y,
                                 CloseBox.width, CloseBox.height, 2f, 2f, 19f, 19f, Close));
            enter = AddNav(Px.TextButton(Content, "Enter", PxArt.Get("Common/button_long"), EnterBox.x, EnterBox.y, EnterBox.width,
                                         EnterBox.height, "원정 떠나기", 12, EnterText, Enter));
        }

        protected override void OnShown()
        {
            // 처음엔 열린 가장 높은 스테이지
            selected = RegressionScreen.HighestStage(ProfileService.Current);
        }

        protected override int DefaultNav => Mathf.Clamp(selected - 1, 0, RegionConfig.Count - 1);

        protected override void OnEscape() => Close();

        protected override void OnNavChanged(int index)
        {
            if (index < RegionConfig.Count) selected = index + 1;
            if (cursor != null)
                ((RectTransform)cursor.transform).anchoredPosition =
                    new Vector2(RowX + 3f, -(RowTop + (selected - 1) * RowStep + (RowH - 12f) / 2f));
        }

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            for (int i = 0; i < RegionConfig.Count; i++)
            {
                int r = i + 1;
                bool openRegion = p.IsRegionOpen(r);
                rowNames[i].color = r == selected ? RowSelected : openRegion ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                rowStates[i].text = !openRegion ? "잠김"
                    : p.IsBossCleared(r) ? "보스 격파"
                    : p.IsBossUnlocked(r) ? "포탈 열림"
                    : $"토벌 {p.RegionKills(r)}/{RunState.RegionKillTarget}";
                rowStates[i].color = openRegion ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            }

            int s = selected;
            title.text = $"stage {s}";
            infoLine.text = $"{RegionConfig.NameOf(s)} · 권장 Lv.{RegionConfig.RecommendedLevel(s)}+ · 멸망까지 {p.doomDays}일";

            var types = MonstersOf(s);
            var spawner = Object.FindFirstObjectByType<EnemySpawner>();
            for (int i = 0; i < 5; i++)
            {
                Sprite sp = i < types.Length ? SpriteOf(spawner, types[i]) : null;
                mobs[i].sprite = sp;
                mobs[i].enabled = sp != null;
            }
            var bs = spawner != null ? spawner.bossSprite : null;
            boss.sprite = bs;
            boss.enabled = bs != null;

            int kills = Mathf.Min(p.RegionKills(s), RunState.RegionKillTarget);
            float ratio = kills / (float)RunState.RegionKillTarget;
            float w = PxArt.Get("Stage/progress").rect.width;
            int span = barSpan.y - barSpan.x + 1;
            progFill.fillAmount = (barSpan.x + Mathf.Round(ratio * span)) / Mathf.Max(1f, w);
            progText.text = $"{kills}/{RunState.RegionKillTarget}";

            enter.Interactable = p.IsRegionOpen(s);
        }

        /// <summary>그 지역에 나오는 몹 종류 — 원본 `rollSpawnType` 순서(박쥐·고블린 → 멧돼지 → 해골 궁수 → 슬라임, 2지역부터 오우거).</summary>
        public static EnemyType[] MonstersOf(int region)
        {
            if (region <= 1) return new[] { EnemyType.Flyer, EnemyType.Grunt };
            if (region == 2) return new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Brute };
            if (region == 3) return new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Shooter, EnemyType.Brute };
            return new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Shooter, EnemyType.Splitter };
        }

        static Sprite SpriteOf(EnemySpawner spawner, EnemyType type)
        {
            if (spawner == null || spawner.enemyTypes == null) return null;
            foreach (var d in spawner.enemyTypes)
                if (d != null && d.type == type) return d.sprite;
            return null;
        }

        void Enter()
        {
            if (TryEnter(selected)) open = false;
        }

        /// <summary>
        /// 스테이지에 들어간다 — 잠김·멸망(0일)을 확인하고(안 되면 공용 팝업으로 알림) 사냥을 시작한다. 입장료는 없다(사용자 2026-10-25).
        /// 스테이지 선택의 "원정 떠나기"와 결과 화면의 "다시 원정"이 같이 쓴다. 들어가면 멸망까지 하루가 준다(`RunController.StartRun`).
        /// </summary>
        public static bool TryEnter(int s)
        {
            var p = ProfileService.Current;
            if (!p.IsRegionOpen(s)) { CommonPopup.Alert($"stage {s}", "이전 스테이지의 보스를 잡아야 열린다."); return false; }
            if (!p.CanEnterStage)
            {
                CommonPopup.Alert("멸망", "멸망까지 남은 기간이 다했다.\n시간 회귀(환생)로 되돌아가야 한다.");
                return false;
            }
            var run = Object.FindFirstObjectByType<RunController>();
            if (run == null) return false;
            run.StartRun(s, RunMode.Normal);
            return true;
        }
    }
}
