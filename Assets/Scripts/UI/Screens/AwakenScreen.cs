using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 각성창 — Figma "각성창"(16:2) 2026-11-18판(640×360): 왼쪽 전신 그림, 오른쪽 위 **고른 스킬 상세 패널**(큰 아이콘·설명·SP·배우기 버튼),
    /// 아래 [각성]·[스탯] 탭과 5열×2줄 스킬 칸. 로비의 "훈련소" 건물에서 연다.
    ///   - **[각성] = 전문화 스킬트리**: 두 갈래 × 5층. 칸을 고르면(마우스 올림·키보드) 위 패널에 그 스킬이 뜨고, 금테 커서가 따라간다.
    ///     Figma에 칸 상태 그림은 "미획득" 한 종류뿐 — 배운 칸(흰 테두리+금 점)·막힌 칸(어두운 테두리·아이콘)은 우리가 정했다.
    ///     동작은 원본대로(층 비용 1~5 SP, 갈래는 처음 찍을 때 고정 — 그래서 첫 층만 공용 팝업으로 한 번 더 묻는다).
    ///   - **[스탯]**(2026-11-22 드라이브 새 그림 + 사용자 스크린샷): 위 패널엔 스탯 5줄(레벨·최대체력·공격력·이동속도·공격속도), 아래 창엔 왼쪽 **강화 목록**(5종, 세 줄 보이고 스크롤)과
    ///     오른쪽 **강화 상세**(지금 → 산 뒤 단계·값, 구매 단위 ×1/×10/최대, [골드/값 강화하기]). 구매 단위·최대 계산은 원본 그대로(`previewGoldUpgrade`/`buyGoldUpgrade`).
    /// 스킬 아이콘은 마법사·살수·엔지니어 것이 들어와 있다(칼날폭풍 3층만 아직 없어 층 숫자).
    /// </summary>
    public class AwakenScreen : PxScreen
    {
        static AwakenScreen instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;
        public static void Open() { if (instance != null) instance.open = true; }
        public static void Close() { if (instance != null) instance.open = false; }

        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Lobby;
        protected override NavLayout Layout => NavLayout.Grid;
        protected override int NavColumns => statTab ? 1 : Cols;

        // Figma 좌표(640판 원점 = 배경 324:2의 -959·-534)
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
        const int Cols = 5, Tiers = 5;
        const float NodeSize = 32f, IconInset = 1f, IconSize = 30f;         // 2026-11-18 Figma 도트 아이콘 30×30 — 1:1로 그려야 안 깨진다(32칸 가운데)
        static readonly Rect CloseBox = new Rect(608f, 8f, 24f, 24f);       // Figma에 없음
        // [스탯] 탭 — 사용자 스크린샷(2026-11-22)에서 잰 값(640판 환산, ±1도트). 위 패널 스탯 바 5줄:
        const float StatBarX = 226f, StatBarY = 36f, StatBarStep = 29f, StatBarW = 360f, StatBarH = 24f;
        const float StatLabelX = 306f, StatValueRight = 576f;          // 글자: 바 왼쪽 +80, 값은 오른쪽 맞춤
        // 아래 창: 목록(왼쪽, 세 줄 보임) · 스크롤 표시 · 상세(오른쪽 144×96)
        const float ListX = 300f, ListY = 234f, ListW = 128f, ListH = 104f, RowH = 24f, RowStep = 28f;
        const int RowsVisible = 3;
        const float ScrollX = 434f, ScrollY = 230f, ScrollH = 108f, ThumbH = 8f;
        const float DetailX = 445f, DetailY = 236f;
        static readonly string[] StatLabels = { "레벨", "최대체력", "공격력", "이동속도", "공격속도" };
        static readonly int[] Qtys = { 1, 10, GoldUpgradeConfig.BuyMax };
        static readonly string[] QtyLabels = { "X1", "X10", "최대" };
        static readonly Color Lime = Px.Hex(0xeeff00);                  // 산 뒤 값(스크린샷의 노랑)
        static readonly Color Dim = new Color(0.55f, 0.55f, 0.6f, 1f);
        static readonly Color Gold = Px.Hex(0xfac70e);

        readonly Text[] statValues = new Text[5];
        Text spText, descText, stateText, learnLabel;
        RectTransform skillDetail, statDetail;
        Image bigIcon;
        PxButton learnButton;
        readonly Image[,] badges = new Image[2, Cols];
        readonly Image[,] cursors = new Image[2, Cols];
        readonly PxButton[,] nodes = new PxButton[2, Cols];
        readonly Image[,] icons = new Image[2, Cols];
        readonly Text[,] numbers = new Text[2, Cols];
        readonly PxButton[] upRows = new PxButton[5];
        readonly Text[] upNames = new Text[5];
        readonly Text[] upLevels = new Text[5];
        readonly PxButton[] qtyButtons = new PxButton[3];
        PxButton buyButton;
        Text buyLabel, detailName, beforeLv, beforeVal, afterLv, afterVal;
        RectTransform thumb;
        RectTransform treeRoot, statRoot;
        PxButton tabAwaken, tabStat;
        bool statTab;
        int selStat, listTop, qtyIndex;
        bool hoverPick; // 마우스 올림으로 고른 건 목록을 밀지 않는다(반쯤 보이는 줄에 올렸다고 목록이 달아나면 못 돌아온다)

        static readonly UpgradeStat[] UpgradeOrder = { UpgradeStat.Hp, UpgradeStat.Atk, UpgradeStat.Ms, UpgradeStat.AtkSpeed, UpgradeStat.Crit };

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

        // ── 캐릭터별 트리 ──
        class Spec
        {
            public string[] Names;
            public string[][] Texts;
            public string[] IconPrefix;
            public int Branch; // -1 아직, 0 = 1줄, 1 = 2줄
            public int Tier;
            public System.Func<int, bool> Learn;
        }

        static Spec SpecOf(PlayerProfile p)
        {
            switch (p.character)
            {
                case CharacterId.Mage:
                    return new Spec
                    {
                        Names = new[] { "중력 계열", "폭발 계열" },
                        Texts = new[] { SpecTexts.GravityTiers, SpecTexts.ExplosionTiers },
                        IconPrefix = new[] { "Skills/mage_gravity_", "Skills/mage_explosion_" },
                        Branch = p.mageBranch == MageBranch.Gravity ? 0 : p.mageBranch == MageBranch.Explosion ? 1 : -1,
                        Tier = p.mageTier,
                        Learn = row => p.TryLearnMageTier(row == 0 ? MageBranch.Gravity : MageBranch.Explosion),
                    };
                case CharacterId.Gunner:
                    return new Spec
                    {
                        Names = new[] { "레이저 빌드", "설치기 빌드" },
                        Texts = new[] { SpecTexts.LaserTiers, SpecTexts.InstallerTiers },
                        IconPrefix = new[] { "Skills/gunner_laser_", "Skills/gunner_installer_" },
                        Branch = p.gunnerBranch == GunnerBranch.Laser ? 0 : p.gunnerBranch == GunnerBranch.Installer ? 1 : -1,
                        Tier = p.gunnerTier,
                        Learn = row => p.TryLearnGunnerTier(row == 0 ? GunnerBranch.Laser : GunnerBranch.Installer),
                    };
                case CharacterId.Blade:
                    return new Spec
                    {
                        Names = new[] { "집중 빌드", "칼날폭풍 빌드" },
                        Texts = new[] { SpecTexts.FocusTiers, SpecTexts.StormTiers },
                        // 칼날폭풍 3층(벽 타기)은 그림이 아직 없다 — 없으면 층 번호가 대신 보인다(2026-11-22).
                        IconPrefix = new[] { "Skills/blade_focus_", "Skills/blade_storm_" },
                        Branch = p.bladeBranch == BladeBranch.Focus ? 0 : p.bladeBranch == BladeBranch.Storm ? 1 : -1,
                        Tier = p.bladeTier,
                        Learn = row => p.TryLearnBladeTier(row == 0 ? BladeBranch.Focus : BladeBranch.Storm),
                    };
                default:
                    return null;
            }
        }

        protected override void Build()
        {
            if (instance == null) instance = this;
            Px.FullFill(Back, "Background", Bg);

            Px.Image(Content, "Portrait", PxArt.Get("Awaken/portrait_full"), Portrait.x, Portrait.y, Portrait.width, Portrait.height);
            Px.Image(Content, "TopPanel", PxArt.Get("Awaken/stat_window"), TopPanel.x, TopPanel.y, TopPanel.width, TopPanel.height);

            // 스킬 상세(각성 탭) — 큰 아이콘 + 설명 + SP + 배우기
            skillDetail = Px.Stretch(Content, "SkillDetail");
            Px.Image(skillDetail, "DescBox", PxArt.Get("Awaken/desc_window"), DescBox.x, DescBox.y, DescBox.width, DescBox.height);
            Px.Image(skillDetail, "SpBox", PxArt.Get("Awaken/sp_box"), SpBox.x, SpBox.y, SpBox.width, SpBox.height);
            spText = Px.Text(skillDetail, "Sp", "", SpBox.x + 5f, SpBox.y, SpBox.width - 10f, SpBox.height, 13, Gold, TextAnchor.MiddleLeft);
            Px.Image(skillDetail, "BigFrame", PxArt.Get("Awaken/big_frame"), BigFrame.x, BigFrame.y, BigFrame.width, BigFrame.height);
            bigIcon = Px.Image(skillDetail, "BigIcon", null, BigIcon.x, BigIcon.y, BigIcon.width, BigIcon.height);
            descText = Px.Text(skillDetail, "Desc", "", DescBox.x + 8f, DescBox.y + 6f, DescBox.width - 16f, 84f, 10, Color.white, TextAnchor.UpperLeft);
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            stateText = Px.Text(skillDetail, "State", "", DescBox.x + 8f, DescBox.y + DescBox.height - 30f, DescBox.width - 16f, 24f, 10, Gold, TextAnchor.LowerLeft);
            stateText.horizontalOverflow = HorizontalWrapMode.Wrap;
            learnButton = Px.TextButton(skillDetail, "Learn", PxArt.Get("Awaken/learn_button"), LearnBtn.x, LearnBtn.y, LearnBtn.width, LearnBtn.height,
                                        "배우기", 13, Color.white, LearnSelected);
            learnLabel = learnButton.Inner.GetComponentInChildren<Text>();

            // 스탯(스탯 탭) — 위 패널에 스탯 바 다섯 줄. 왼쪽 아이콘 칸(24×24)은 그림이 아직 안 와서 비워 둔다.
            statDetail = Px.Stretch(Content, "StatDetail");
            for (int i = 0; i < 5; i++)
            {
                float y = StatBarY + i * StatBarStep;
                Px.Image(statDetail, "Bar" + i, PxArt.Get("Awaken/stat_bar"), StatBarX, y, StatBarW, StatBarH);
                Px.Text(statDetail, "Label" + i, StatLabels[i], StatLabelX, y, 150f, StatBarH, 16, Color.white, TextAnchor.MiddleLeft);
                statValues[i] = Px.Text(statDetail, "Value" + i, "", StatValueRight - 150f, y, 150f, StatBarH, 16, Color.white, TextAnchor.MiddleRight);
            }

            tabAwaken = Px.TextButton(Content, "TabAwaken", PxArt.Get("Awaken/tab_button"), TabAwaken.x, TabAwaken.y,
                                      TabAwaken.width, TabAwaken.height, "각성", 20, Color.white, () => SetTab(false));
            tabStat = Px.TextButton(Content, "TabStat", PxArt.Get("Awaken/tab_button"), TabStat.x, TabStat.y,
                                    TabStat.width, TabStat.height, "스탯", 20, Color.white, () => SetTab(true));
            Px.Image(Content, "SkillWindow", PxArt.Get("Awaken/skill_window"), SkillWindow.x, SkillWindow.y,
                     SkillWindow.width, SkillWindow.height);

            treeRoot = Px.Stretch(Content, "Tree");
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < Cols; c++)
                {
                    int row = r, col = c;
                    var b = Px.Button(treeRoot, $"Node{r}{c}", PxArt.Get("Awaken/node_open"), NodeX[c], NodeY[r], NodeSize, NodeSize,
                                      () => ClickNode(row, col), out var inner);
                    icons[r, c] = Px.Image(inner, "Icon", null, IconInset, IconInset, IconSize, IconSize);
                    numbers[r, c] = Px.Text(inner, "Tier", (c + 1).ToString(), 0f, 0f, NodeSize, NodeSize, 13, Color.white)
                                      .WithOutline(Px.Ink);
                    badges[r, c] = Px.Image(inner, "Learned", null, NodeSize - 6f, 2f, 4f, 4f);
                    badges[r, c].color = Gold;
                    cursors[r, c] = Px.Image(inner, "Cursor", PxArt.Get("Awaken/node_cursor"), 0f, 0f, NodeSize, NodeSize);
                    nodes[r, c] = b;
                }

            // 아래 창(스탯 탭): 머리띠 가운데 제목 · 왼쪽 강화 목록 · 스크롤 표시 · 오른쪽 강화 상세
            statRoot = Px.Stretch(Content, "Upgrades");
            Px.Text(statRoot, "Title", "스탯 강화", 403f, SkillWindow.y, 85f, 32f, 16, Color.white);
            var list = Px.Node(statRoot, "List", ListX, ListY, ListW, ListH);
            list.gameObject.AddComponent<RectMask2D>(); // 네 번째 줄은 반쯤 잘려 보인다(스크린샷)
            for (int i = 0; i < 5; i++)
            {
                int row = i;
                upRows[i] = Px.Button(list, "Row" + i, PxArt.Get("Awaken/row_off"), 0f, i * RowStep, ListW, RowH, () => SelectNav(row), out var inner);
                upNames[i] = Px.Text(inner, "Name", "", 10f, 0f, 80f, RowH, 11, Color.white, TextAnchor.MiddleLeft);
                upLevels[i] = Px.Text(inner, "Lv", "", ListW - 10f - 50f, 0f, 50f, RowH, 11, Color.white, TextAnchor.MiddleRight);
            }
            Px.Fill(statRoot, "Divider", Px.Hex(0xd9d9d9), 437f, ScrollY, 1f, ScrollH);
            Px.Fill(statRoot, "Divider2", Px.Hex(0x6e798c), 438f, ScrollY, 1f, ScrollH);
            thumb = Px.Fill(statRoot, "Thumb", Px.Hex(0xd9d9d9), ScrollX, ScrollY, 3f, ThumbH).rectTransform;

            Px.Image(statRoot, "Detail", PxArt.Get("Awaken/stat_detail"), DetailX, DetailY, 144f, 96f);
            detailName = Px.Text(statRoot, "DetailName", "", DetailX + 7f, DetailY + 1f, 130f, 13f, 11, Color.white, TextAnchor.MiddleLeft);
            Px.Image(statRoot, "Before", PxArt.Get("Awaken/stat_value_box"), DetailX + 12f, DetailY + 18f, 48f, 24f);
            beforeLv = Px.Text(statRoot, "BeforeLv", "", DetailX + 12f, DetailY + 18f, 48f, 12f, 8, Color.white);
            beforeVal = Px.Text(statRoot, "BeforeVal", "", DetailX + 12f, DetailY + 29f, 48f, 12f, 8, Color.white);
            Px.Text(statRoot, "Arrow", ">>", DetailX + 60f, DetailY + 18f, 24f, 24f, 13, Color.white);
            Px.Image(statRoot, "After", PxArt.Get("Awaken/stat_value_box"), DetailX + 84f, DetailY + 18f, 48f, 24f);
            afterLv = Px.Text(statRoot, "AfterLv", "", DetailX + 84f, DetailY + 18f, 48f, 12f, 8, Color.white);
            afterVal = Px.Text(statRoot, "AfterVal", "", DetailX + 84f, DetailY + 29f, 48f, 12f, 8, Lime);
            for (int k = 0; k < 3; k++)
            {
                int q = k;
                qtyButtons[k] = Px.TextButton(statRoot, "Qty" + k, PxArt.Get("Awaken/mult_off"), DetailX + 12f + 42f * k, DetailY + 52f, 36f, 16f,
                                              QtyLabels[k], 11, Color.white, () => qtyIndex = q);
            }
            buyButton = Px.TextButton(statRoot, "Buy", PxArt.Get("Awaken/buy_button"), DetailX + 12f, DetailY + 74f, 120f, 16f, "", 11, Color.white, BuySelected);
            buyLabel = buyButton.Inner.GetComponentInChildren<Text>();

            Px.IconButton(Content, "Close", PxArt.Get("Common/button_small"), PxArt.Get("Icons/close"), CloseBox.x, CloseBox.y,
                          CloseBox.width, CloseBox.height, 2f, 2f, 19f, 19f, Close);
#if UNITY_EDITOR
            // 에디터 전용 "SP +1"(사용자 2026-09-29 — 스킬을 한 층씩 찍어 보며 테스트). 예전 로비 전문화 탭에 있던 것을 옮겼다.
            // SP는 레벨에서 나오므로 레벨을 1 올린다. 빌드한 게임엔 없다 — 그래서 프로필 직접 대입의 예외로 둔다(`docs/state-ownership.md`).
            var sp = Px.TextButton(skillDetail, "TestSp", PxArt.Sliced("Common/button", 4), 492f, 164f, 96f, 18f, "SP +1 (테스트)", 8,
                                   Color.white, () => { ProfileService.Current.level++; SaveService.Save(); });
            sp.Art.type = Image.Type.Sliced;
#endif
        }

        /// <summary>테스트용 — 그 칸 틀 그림 이름(node_learned·node_open·node_locked).</summary>
        public string NodeFrameName(int r, int c) => nodes[r, c].Art.sprite != null ? nodes[r, c].Art.sprite.name : "";
        /// <summary>테스트용 — [스탯] 탭으로(false면 [각성]) 바꾸고 한 번 갱신한다.</summary>
        public void ShowTabForTest(bool stat) { SetTab(stat); Refresh(); }
        /// <summary>테스트용 — 스탯 바 i번째 값 글자.</summary>
        public string StatValueText(int i) => statValues[i].text;
        /// <summary>테스트용 — 강화 목록 줄(0~4)과 구매 단위(0=×1, 1=×10, 2=최대)를 고르고 갱신한다.</summary>
        public void PickUpgradeForTest(int row, int qty) { selStat = row; qtyIndex = qty; EnsureSelectionVisible(); Refresh(); }
        /// <summary>테스트용 — [강화하기]를 누른 것처럼(저장은 하지 않는다 — 테스트 세이브 경로는 샌드박스지만 굳이 안 쓴다).</summary>
        public void BuyForTest() { ProfileService.Current.TryBuyUpgrades(UpgradeOrder[selStat], Qtys[qtyIndex]); Refresh(); }
        public bool BuyEnabled => buyButton.Interactable;
        public string BuyText => buyLabel.text;
        public string DetailNameText => detailName.text;
        public string BeforeLevelText => beforeLv.text;
        public string AfterLevelText => afterLv.text;
        /// <summary>테스트용 — 칸을 누른 것처럼.</summary>
        public void ClickNodeForTest(int r, int c) => ClickNode(r, c);

        protected override void OnShown() => SetTab(false);

        protected override void OnEscape() => Close();

        void SetTab(bool stat)
        {
            statTab = stat;
            treeRoot.gameObject.SetActive(!stat);
            statRoot.gameObject.SetActive(stat);
            Nav.Clear();
            if (!stat) { foreach (var n in nodes) AddNav(n); }
            else
            {
                for (int i = 0; i < upRows.Length; i++)
                {
                    int row = i;
                    AddNav(upRows[i]).OnHover = () => { hoverPick = true; SelectNav(row); hoverPick = false; };
                }
                foreach (var q in qtyButtons) AddNav(q);
                AddNav(buyButton);
            }
            AddNav(tabAwaken);
            AddNav(tabStat);
            SelectNav(0);
            skillDetail.gameObject.SetActive(!stat);
            statDetail.gameObject.SetActive(stat);
            RefreshDetail();
        }

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            // 지금 탭은 글자를 노랗게(Figma는 두 탭이 같은 모양 — 어느 쪽인지 보이게)
            tabAwaken.Inner.GetComponentInChildren<Text>().color = statTab ? Color.white : Gold;
            tabStat.Inner.GetComponentInChildren<Text>().color = statTab ? Gold : Color.white;
            statValues[0].text = $"LV.{p.level}";
            statValues[1].text = FormatStat(UpgradeStat.Hp, PlayerStatCalculator.ComputeMaxHp(p));
            statValues[2].text = FormatStat(UpgradeStat.Atk, PlayerStatCalculator.ComputeAtk(p));
            statValues[3].text = FormatStat(UpgradeStat.Ms, PlayerStatCalculator.ComputeMoveSpeedMultiplier(p));
            statValues[4].text = FormatStat(UpgradeStat.AtkSpeed, PlayerStatCalculator.ComputeAttackSpeedMultiplier(p));
            spText.text = $"SP {p.SpAvailable} / {p.SpTotal}";

            if (!statTab) RefreshTree(p);
            else RefreshUpgrades(p);
        }

        void RefreshTree(PlayerProfile p)
        {
            var spec = SpecOf(p);
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < Cols; c++)
                {
                    int t = c + 1;
                    bool exists = spec != null && t <= Tiers;
                    bool locked = !exists || (spec.Branch >= 0 && spec.Branch != r);
                    bool learned = exists && spec.Branch == r && spec.Tier >= t;
                    string frame = learned ? "Awaken/node_learned" : locked ? "Awaken/node_locked" : "Awaken/node_open";
                    nodes[r, c].SetSprite(PxArt.Get(frame));

                    string iconPath = exists && spec.IconPrefix[r] != null ? spec.IconPrefix[r] + t : null;
                    var icon = iconPath != null ? PxArt.Get(iconPath) : null;
                    icons[r, c].sprite = icon;
                    icons[r, c].enabled = icon != null;
                    icons[r, c].color = locked ? Dim : Color.white;
                    numbers[r, c].enabled = exists && icon == null;
                    numbers[r, c].color = locked ? Dim : Color.white;
                    badges[r, c].enabled = learned;
                    cursors[r, c].enabled = r * Cols + c == NavIndex;
                }
            RefreshDetail();
        }

        void RefreshUpgrades(PlayerProfile p)
        {
            for (int i = 0; i < 5; i++)
            {
                var stat = UpgradeOrder[i];
                ((RectTransform)upRows[i].transform).anchoredPosition = new Vector2(0f, -(i - listTop) * RowStep);
                upRows[i].SetSprite(PxArt.Get(i == selStat ? "Awaken/row_on" : "Awaken/row_off"));
                upNames[i].text = UpgradeName(stat);
                upLevels[i].text = $"Lv.{p.GetUpgradeLevel(stat)}";
            }
            int maxTop = UpgradeOrder.Length - RowsVisible;
            float t = maxTop > 0 ? (float)listTop / maxTop : 0f;
            thumb.anchoredPosition = new Vector2(ScrollX, -(ScrollY + t * (ScrollH - ThumbH)));

            // 상세 — 지금 단계 → 산 뒤 단계(구매 단위만큼, 원본 `previewGoldUpgrade`)
            var sel = UpgradeOrder[selStat];
            int lv = p.GetUpgradeLevel(sel);
            var (count, cost) = GoldUpgradeConfig.Preview(sel, lv, p.gold, Qtys[qtyIndex]);
            long shownCost = count > 0 ? cost : GoldUpgradeConfig.Cost(sel, lv); // "최대"인데 한 단계도 못 사면 한 단계 값을 보인다
            detailName.text = UpgradeName(sel);
            beforeLv.text = $"LV.{lv}";
            beforeVal.text = FormatStat(sel, PlayerStatCalculator.StatAtUpgradeLevel(p, sel, lv));
            afterLv.text = $"LV.{lv + count}";
            afterVal.text = FormatStat(sel, PlayerStatCalculator.StatAtUpgradeLevel(p, sel, lv + count));
            for (int k = 0; k < qtyButtons.Length; k++) qtyButtons[k].SetSprite(PxArt.Get(k == qtyIndex ? "Awaken/mult_on" : "Awaken/mult_off"));
            buyLabel.text = $"G {p.gold:N0}/{shownCost:N0}  강화하기";
            buyButton.Interactable = count > 0 && p.gold >= cost;
        }

        static string UpgradeName(UpgradeStat s) => s switch
        {
            UpgradeStat.Hp => "최대 체력",
            UpgradeStat.Atk => "공격력",
            UpgradeStat.Ms => "이동속도",
            UpgradeStat.AtkSpeed => "공격속도",
            UpgradeStat.Crit => "치명타 확률",
            _ => s.ToString(),
        };

        /// <summary>스탯 값 글자 — 체력·공격력은 수, 이동·공속·치명타는 퍼센트(배수 1.0 = 100%, 확률 0.1 = 10%).</summary>
        static string FormatStat(UpgradeStat s, float v) =>
            s == UpgradeStat.Hp || s == UpgradeStat.Atk ? Mathf.Round(v).ToString("N0") : $"{Mathf.Round(v * 100f):0}%";

        protected override void OnNavChanged(int index)
        {
            if (!statTab) RefreshTree(ProfileService.Current);
            else if (index >= 0 && index < UpgradeOrder.Length)
            {
                selStat = index; // 목록 줄(0~4)을 고르면 그 강화가 상세에 뜬다
                if (!hoverPick) EnsureSelectionVisible(); // 키보드·클릭으로 고른 줄은 보이게 밀고, 마우스 올림은 가만히 둔다
            }
        }

        void EnsureSelectionVisible()
        {
            if (selStat < listTop) listTop = selStat;
            else if (selStat > listTop + RowsVisible - 1) listTop = selStat - (RowsVisible - 1);
        }

        /// <summary>마우스 휠 — 목록을 한 줄씩 민다(고른 줄은 그대로).</summary>
        protected override void OnKeys()
        {
            if (!statTab) return;
            float wheel = GameInput.MenuScroll;
            if (wheel == 0f) return;
            listTop = Mathf.Clamp(listTop + (wheel > 0f ? -1 : 1), 0, UpgradeOrder.Length - RowsVisible);
        }

        /// <summary>테스트용 — 마우스 휠을 한 칸 굴린 것처럼(위로 = true).</summary>
        public void ScrollListForTest(bool up) => listTop = Mathf.Clamp(listTop + (up ? -1 : 1), 0, UpgradeOrder.Length - RowsVisible);
        public int ListTop => listTop;

        /// <summary>지금 고른 칸(없으면 null) — 상세 패널과 [배우기]가 같이 본다.</summary>
        bool Selected(out int r, out int c)
        {
            r = c = 0;
            if (statTab || NavIndex < 0 || NavIndex >= 2 * Cols) return false;
            r = NavIndex / Cols; c = NavIndex % Cols;
            return true;
        }

        void RefreshDetail()
        {
            if (descText == null) return;
            bool has = Selected(out int r, out int c);
            bigIcon.enabled = false;
            learnButton.gameObject.SetActive(!statTab);
            learnButton.Interactable = false;
            if (!has) { descText.text = ""; stateText.text = ""; return; }
            int t = c + 1;
            var p = ProfileService.Current;
            var spec = SpecOf(p);
            if (spec == null) { descText.text = "이 캐릭터는 아직 전문화가 없다."; stateText.text = ""; return; }
            string path = spec.IconPrefix[r] != null ? spec.IconPrefix[r] + t : null;
            var icon = path != null ? PxArt.Get(path) : null;
            bigIcon.sprite = icon;
            bigIcon.enabled = icon != null;
            bigIcon.color = Color.white; // 그림 없이 만들 때 깔린 경고색(분홍)을 걷는다
            descText.text = $"{spec.Names[r]} {t}층 · SP {t}\n{spec.Texts[r][c]}";

            string state;
            bool can = false;
            if (spec.Branch >= 0 && spec.Branch != r) state = "다른 갈래를 골라서 막혔다";
            else if (spec.Branch == r && spec.Tier >= t) state = "배움";
            else if (spec.Tier == t - 1) { can = p.SpAvailable >= t; state = can ? $"누르면 배운다 (SP {t})" : $"SP가 모자라다 (SP {t} 필요)"; }
            else state = $"{t - 1}층을 먼저 배워야 한다";
            stateText.text = state;
            learnButton.Interactable = can;
        }

        void LearnSelected()
        {
            if (Selected(out int r, out int c)) ClickNode(r, c);
        }

        void ClickNode(int r, int c)
        {
            var p = ProfileService.Current;
            var spec = SpecOf(p);
            int t = c + 1;
            if (spec == null || t > Tiers) return;
            if (spec.Branch >= 0 && spec.Branch != r) return;
            if (spec.Tier != t - 1) return;
            if (p.SpAvailable < t)
            {
                CommonPopup.Alert("SP 부족", $"{spec.Names[r]} {t}층에는 SP {t}개가 필요하다.\n지금 SP {p.SpAvailable}개.");
                return;
            }
            if (spec.Branch < 0)
                CommonPopup.Confirm(spec.Names[r], $"{spec.Names[r]}로 정할까요?\n한번 고르면 다른 갈래는 배울 수 없다.",
                                    () => Learn(spec, r), "정하기");
            else Learn(spec, r);
        }

        static void Learn(Spec spec, int row)
        {
            if (spec.Learn(row)) SaveService.Save(); // 원본 `learnSkill` 직후 저장(:7020)
        }

        void BuySelected()
        {
            // 골드 차감과 단계 상승은 프로필이 한 번에 처리한다(원본 `buyGoldUpgrade(k, qty)` :7042)
            if (ProfileService.Current.TryBuyUpgrades(UpgradeOrder[selStat], Qtys[qtyIndex]) > 0) SaveService.Save();
        }
    }
}
