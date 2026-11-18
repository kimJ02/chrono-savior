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
    ///   - **[스탯]**: Figma에 없는 면 — 같은 패널을 써서 스탯 5줄(설명 상자)·골드(SP 상자)·원정 강화 5종(스킬 칸 자리)을 보여 준다.
    /// 스킬 아이콘은 Figma에 마법사 것만 있다. 메카닉·섬영은 아이콘 대신 층 숫자(사용자 확인 전).
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
        // [스탯] 탭(원정 강화) — 스킬 칸 자리(머리띠 아래)에 다섯 줄
        const float UpX = 306f, UpTop = 238f, UpStep = 20f, UpBtnX = 528f, UpBtnW = 60f, UpBtnH = 18f;
        static readonly Color Dim = new Color(0.55f, 0.55f, 0.6f, 1f);
        static readonly Color Gold = Px.Hex(0xfac70e);

        readonly Text[] stats = new Text[5];
        Text spText, descText, stateText, learnLabel;
        RectTransform skillDetail, statDetail;
        Image bigIcon;
        PxButton learnButton;
        readonly Image[,] badges = new Image[2, Cols];
        readonly Image[,] cursors = new Image[2, Cols];
        readonly PxButton[,] nodes = new PxButton[2, Cols];
        readonly Image[,] icons = new Image[2, Cols];
        readonly Text[,] numbers = new Text[2, Cols];
        readonly PxButton[] upButtons = new PxButton[5];
        readonly Text[] upLabels = new Text[5];
        RectTransform treeRoot, statRoot;
        PxButton tabAwaken, tabStat;
        bool statTab;

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
                        IconPrefix = new string[2],
                        Branch = p.gunnerBranch == GunnerBranch.Laser ? 0 : p.gunnerBranch == GunnerBranch.Installer ? 1 : -1,
                        Tier = p.gunnerTier,
                        Learn = row => p.TryLearnGunnerTier(row == 0 ? GunnerBranch.Laser : GunnerBranch.Installer),
                    };
                case CharacterId.Blade:
                    return new Spec
                    {
                        Names = new[] { "집중 빌드", "칼날폭풍 빌드" },
                        Texts = new[] { SpecTexts.FocusTiers, SpecTexts.StormTiers },
                        IconPrefix = new string[2],
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
            Px.Image(Content, "DescBox", PxArt.Get("Awaken/desc_window"), DescBox.x, DescBox.y, DescBox.width, DescBox.height);
            Px.Image(Content, "SpBox", PxArt.Get("Awaken/sp_box"), SpBox.x, SpBox.y, SpBox.width, SpBox.height);
            spText = Px.Text(Content, "Sp", "", SpBox.x + 5f, SpBox.y, SpBox.width - 10f, SpBox.height, 13, Gold, TextAnchor.MiddleLeft);

            // 스킬 상세(각성 탭) — 큰 아이콘 + 설명 + 배우기
            skillDetail = Px.Stretch(Content, "SkillDetail");
            Px.Image(skillDetail, "BigFrame", PxArt.Get("Awaken/big_frame"), BigFrame.x, BigFrame.y, BigFrame.width, BigFrame.height);
            bigIcon = Px.Image(skillDetail, "BigIcon", null, BigIcon.x, BigIcon.y, BigIcon.width, BigIcon.height);
            descText = Px.Text(skillDetail, "Desc", "", DescBox.x + 8f, DescBox.y + 6f, DescBox.width - 16f, 84f, 10, Color.white, TextAnchor.UpperLeft);
            descText.horizontalOverflow = HorizontalWrapMode.Wrap;
            stateText = Px.Text(skillDetail, "State", "", DescBox.x + 8f, DescBox.y + DescBox.height - 30f, DescBox.width - 16f, 24f, 10, Gold, TextAnchor.LowerLeft);
            stateText.horizontalOverflow = HorizontalWrapMode.Wrap;
            learnButton = Px.TextButton(skillDetail, "Learn", PxArt.Get("Awaken/learn_button"), LearnBtn.x, LearnBtn.y, LearnBtn.width, LearnBtn.height,
                                        "배우기", 13, Color.white, LearnSelected);
            learnLabel = learnButton.Inner.GetComponentInChildren<Text>();

            // 스탯(스탯 탭) — 설명 상자 자리에 다섯 줄
            statDetail = Px.Stretch(Content, "StatDetail");
            for (int i = 0; i < 5; i++)
                stats[i] = Px.Text(statDetail, "Stat" + i, "", DescBox.x + 10f, DescBox.y + 6f + i * 23f, DescBox.width - 20f, 20f, 13, Color.white, TextAnchor.MiddleLeft);

            tabAwaken = Px.TextButton(Content, "TabAwaken", PxArt.Get("Common/icon_frame"), TabAwaken.x, TabAwaken.y,
                                      TabAwaken.width, TabAwaken.height, "각성", 20, Color.white, () => SetTab(false));
            tabStat = Px.TextButton(Content, "TabStat", PxArt.Get("Common/icon_frame"), TabStat.x, TabStat.y,
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

            statRoot = Px.Stretch(Content, "Upgrades");
            for (int i = 0; i < 5; i++)
            {
                var stat = UpgradeOrder[i];
                float y = UpTop + i * UpStep;
                upLabels[i] = Px.Text(statRoot, "Label" + i, "", UpX, y, 214f, UpBtnH, 10, Color.white, TextAnchor.MiddleLeft);
                upButtons[i] = Px.TextButton(statRoot, "Buy" + i, PxArt.Sliced("Common/button", 4), UpBtnX, y, UpBtnW, UpBtnH,
                                             "강화", 10, Color.white, () => Buy(stat));
                upButtons[i].Art.type = Image.Type.Sliced;
            }

            Px.IconButton(Content, "Close", PxArt.Get("Common/button_small"), PxArt.Get("Icons/close"), CloseBox.x, CloseBox.y,
                          CloseBox.width, CloseBox.height, 2f, 2f, 19f, 19f, Close);
#if UNITY_EDITOR
            // 에디터 전용 "SP +1"(사용자 2026-09-29 — 스킬을 한 층씩 찍어 보며 테스트). 예전 로비 전문화 탭에 있던 것을 옮겼다.
            // SP는 레벨에서 나오므로 레벨을 1 올린다. 빌드한 게임엔 없다 — 그래서 프로필 직접 대입의 예외로 둔다(`docs/state-ownership.md`).
            var sp = Px.TextButton(Content, "TestSp", PxArt.Sliced("Common/button", 4), 492f, 164f, 96f, 18f, "SP +1 (테스트)", 8,
                                   Color.white, () => { ProfileService.Current.level++; SaveService.Save(); });
            sp.Art.type = Image.Type.Sliced;
#endif
        }

        /// <summary>테스트용 — 그 칸 틀 그림 이름(node_learned·node_open·node_locked).</summary>
        public string NodeFrameName(int r, int c) => nodes[r, c].Art.sprite != null ? nodes[r, c].Art.sprite.name : "";
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
            else { foreach (var b in upButtons) AddNav(b); }
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
            stats[0].text = $"레벨: {p.level}";
            stats[1].text = $"최대체력: {PlayerStatCalculator.ComputeMaxHp(p):0}";
            stats[2].text = $"공격력: {PlayerStatCalculator.ComputeAtk(p):0}";
            stats[3].text = $"이동속도: {PlayerStatCalculator.ComputeMoveSpeedMultiplier(p) * 100f:0}%";
            stats[4].text = $"공격속도: {PlayerStatCalculator.ComputeAttackSpeedMultiplier(p) * 100f:0}%";
            spText.text = statTab ? $"{p.gold:N0} G" : $"SP {p.SpAvailable} / {p.SpTotal}";

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
                int level = p.GetUpgradeLevel(stat);
                int cost = GoldUpgradeConfig.Cost(stat, level);
                upLabels[i].text = $"{UpgradeName(stat)}  Lv.{level}    {cost:N0} G";
                upButtons[i].Interactable = p.gold >= cost;
            }
        }

        static string UpgradeName(UpgradeStat s) => s switch
        {
            UpgradeStat.Hp => "체력",
            UpgradeStat.Atk => "공격력",
            UpgradeStat.Ms => "이동속도",
            UpgradeStat.AtkSpeed => "공격속도",
            UpgradeStat.Crit => "치명타 확률",
            _ => s.ToString(),
        };

        protected override void OnNavChanged(int index) { if (!statTab) RefreshTree(ProfileService.Current); }

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

        static void Buy(UpgradeStat stat)
        {
            // 골드 차감과 단계 상승은 프로필이 한 번에 처리한다(원본 `buyGoldUpgrade` :7042)
            if (ProfileService.Current.TryBuyUpgrade(stat)) SaveService.Save();
        }
    }
}
