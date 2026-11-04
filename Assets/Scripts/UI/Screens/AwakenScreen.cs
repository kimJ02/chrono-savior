using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 각성창 — Figma "각성창"(16:2)의 640×360판: 왼쪽 큰 캐릭터 그림, 오른쪽 위 스탯 창, 아래 [각성]·[스탯] 탭과 강화창.
    /// 로비의 "훈련소" 건물에서 연다.
    ///   - **[각성] = 전문화 스킬트리**(예전 로비 "전문화" 탭): 두 갈래 × 6칸. 금테 = 배운 층, 은테 = 배울 수 있는 갈래,
    ///     회색 = 다른 갈래를 골라서 막힌 줄(Figma 예시가 정확히 이 모양 — 1줄 두 칸 금테, 2줄 회색). 6번째 칸은 아직 없는 층(사용자 10/23).
    ///     동작은 원본대로(층 비용 1~5 SP, 갈래는 처음 찍을 때 고정 — 그래서 첫 층만 공용 팝업으로 한 번 더 묻는다).
    ///   - **[스탯] = 원정 강화**(예전 로비 "강화" 탭, Figma "원정 강화창"은 뼈대만): 골드 영구 강화 5종을 강화창 안에.
    /// 스킬 아이콘은 Figma에 마법사 것만 있다(1줄 보라 = 중력, 2줄 불 = 폭발). 메카닉·섬영은 아이콘 대신 층 숫자(사용자 확인 전).
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

        // Figma 좌표(640판 원점 = 배경 324:2의 -960·-534)
        static readonly Color Bg = Px.Hex(0x414141);
        static readonly Rect PortraitMask = new Rect(0f, 3f, 296f, 357f);  // 348:10 (-94·3 389×357)의 판 안쪽
        static readonly Vector2 PortraitPos = new Vector2(-92f, -1f);      // 64px 그림을 6배(384)로(Figma 6.08배), 마스크 기준
        static readonly Rect StatWindow = new Rect(215f, 27f, 384f, 160f); // 348:5
        static readonly float[] StatY = { 50f, 74f, 98f, 122f, 146f };     // 24:12307~25:12312 15px, 24px 간격
        const float StatX = 237f, StatW = 220f, StatH = 19f;
        static readonly Rect TabAwaken = new Rect(215f, 197f, 64f, 64f);   // 348:2 + 348:6 "각성" 20px
        static readonly Rect TabStat = new Rect(215f, 277f, 64f, 64f);     // 348:4 + 348:7 "스탯"
        static readonly Rect SkillWindow = new Rect(295f, 197f, 304f, 144f); // 348:9
        static readonly float[] NodeX = { 320f, 365f, 410f, 455f, 500f, 545f }; // 348:13~348:24 (31.76 → 32)
        static readonly float[] NodeY = { 226f, 281f };
        const int Cols = 6, Tiers = 5;
        const float NodeSize = 32f, IconInset = 6f, IconSize = 21f;         // 354:25~354:34 21×21
        static readonly Rect CloseBox = new Rect(608f, 8f, 24f, 24f);       // Figma에 없음
        // 층 설명 상자는 왼쪽 캐릭터 그림 위에 — 칸 옆에 띄우면 이웃 칸을 가린다
        const float TipX = 8f, TipY = 214f;
        // [스탯] 탭(원정 강화) — 강화창 안에 다섯 줄
        const float UpX = 310f, UpTop = 206f, UpStep = 26f, UpBtnX = 528f, UpBtnW = 60f, UpBtnH = 20f;
        static readonly Color Dim = new Color(0.55f, 0.55f, 0.6f, 1f);
        static readonly Color Gold = Px.Hex(0xfac70e);

        readonly Text[] stats = new Text[5];
        Text spText, goldText;
        readonly PxButton[,] nodes = new PxButton[2, Cols];
        readonly Image[,] icons = new Image[2, Cols];
        readonly Text[,] numbers = new Text[2, Cols];
        readonly PxButton[] upButtons = new PxButton[5];
        readonly Text[] upLabels = new Text[5];
        RectTransform treeRoot, statRoot;
        PxButton tabAwaken, tabStat;
        PxTooltip tip;
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

            var mask = Px.Node(Content, "PortraitMask", PortraitMask.x, PortraitMask.y, PortraitMask.width, PortraitMask.height);
            mask.gameObject.AddComponent<RectMask2D>();
            Px.Image(mask, "Portrait", PxArt.Get("Hud/portrait"), PortraitPos.x, PortraitPos.y, 384f, 384f);

            Px.Image(Content, "StatWindow", PxArt.Get("Awaken/stat_window"), StatWindow.x, StatWindow.y, StatWindow.width, StatWindow.height);
            for (int i = 0; i < 5; i++)
                stats[i] = Px.Text(Content, "Stat" + i, "", StatX, StatY[i], StatW, StatH, 15, Color.white, TextAnchor.MiddleLeft);
            spText = Px.Text(Content, "Sp", "", 460f, StatY[0], 126f, StatH, 13, Gold, TextAnchor.MiddleRight);
            goldText = Px.Text(Content, "Gold", "", 460f, StatY[1], 126f, StatH, 13, Gold, TextAnchor.MiddleRight);

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
            // SP는 레벨에서 나오므로 레벨을 1 올린다. 빌드한 게임엔 없다.
            var sp = Px.TextButton(Content, "TestSp", PxArt.Sliced("Common/button", 4), 470f, 160f, 116f, 18f, "SP +1 (테스트)", 8,
                                   Color.white, () => { ProfileService.Current.level++; SaveService.Save(); });
            sp.Art.type = Image.Type.Sliced;
#endif
            tip = new PxTooltip(Content);
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
            tip?.Hide();
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
            spText.text = $"SP {p.SpAvailable} / {p.SpTotal}";
            goldText.text = $"{p.gold:N0} G";

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
                }
            RefreshTip();
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

        protected override void OnNavChanged(int index) => RefreshTip();

        void RefreshTip()
        {
            if (tip == null) return;
            if (statTab || NavIndex < 0 || NavIndex >= 2 * Cols) { tip.Hide(); return; }
            int r = NavIndex / Cols, c = NavIndex % Cols, t = c + 1;
            var p = ProfileService.Current;
            var spec = SpecOf(p);
            if (spec == null) { tip.ShowAt("이 캐릭터는 아직 전문화가 없다.", TipX, TipY); return; }
            if (t > Tiers) { tip.ShowAt($"{spec.Names[r]} {t}층\n아직 없는 층이다.", TipX, TipY); return; }

            string state;
            if (spec.Branch >= 0 && spec.Branch != r) state = "다른 갈래를 골라서 막혔다";
            else if (spec.Branch == r && spec.Tier >= t) state = "배움";
            else if (spec.Tier == t - 1) state = p.SpAvailable >= t ? $"누르면 배운다 (SP {t})" : $"SP가 모자라다 (SP {t} 필요)";
            else state = $"{t - 1}층을 먼저 배워야 한다";
            tip.ShowAt($"{spec.Names[r]} {t}층 · SP {t}\n{spec.Texts[r][c]}\n<color=#fac70e>{state}</color>", TipX, TipY);
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
