using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 전투 HUD — Figma "스테이지 HUD"(124:3, 640×360). 2026-10-24 사용자 결정대로:
    /// 초상화 틀의 **빨간 칸 = 플레이어 체력**(숫자 포함), 하늘·어두운 칸은 아직 뜻이 정해지지 않아 **비워 둔다**,
    /// 가운데 숫자 = 남은 초, 오른쪽 위 COMBO, 왼쪽 아래 스킬 칸 3개(Z·X·C), 오른쪽 아래 미니맵(실제 맵을 그린다),
    /// **경험치 = 화면 맨 아래 가로 바**(사용자 "A — Figma 디자인 가져와서": 숨겨진 옛 1920판 "경험치 바" 124:5의 색 그대로).
    ///
    /// Figma에 없는데 임시로 둔 것(사용자 확인 전): 남은 초 밑의 토벌 진행(100마리 → 보스가 게임의 뼈대라서),
    /// 포탈 근처 안내. 원본 HUD의 레벨·골드·버프 줄·이번 사냥 수입은 Figma에 없어서 뺐다(사용자에게 물어볼 것).
    /// 동작(쿨다운 덮개·숫자, 메카닉 충전 스택, 남은 시간 10초 이하 붉게, 콤보 0이면 숨김)은 원본·예전 IMGUI HUD와 같다.
    /// </summary>
    public class CombatHud : PxScreen
    {
        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Hud;
        protected override bool TakesKeys => false;
        // 원본 `#hud`는 로비에서만 숨긴다 — 일시정지·결과 화면 뒤에도 보인다.
        protected override bool WantsVisible =>
            GameState.Current == GameScene.Run || GameState.Current == GameScene.Pause || GameState.Current == GameScene.Result;

        // ── Figma 좌표 ──
        static readonly Vector2 FramePos = new Vector2(4f, 7f);       // 385:2 "Frame 5"
        const float FrameW = 192f, FrameH = 64f;                       // 166:29 "UI합본 1"
        // 틀 그림 안의 칸(그림 픽셀): 빨간 체력 칸 x 66~188 · y 3~14, 초상화 흰 창 3~60
        const int HpX0 = 66, HpW = 123;
        static readonly Rect HpTextBox = new Rect(66f, 3f, 123f, 12f);
        static readonly Color32 HpRed = new Color32(234, 50, 60, 255);
        static readonly Color32 BarCyan = new Color32(12, 241, 255, 255);
        static readonly Color32 BarEmpty = new Color32(57, 31, 33, 255);  // 틀 그림의 어두운 칸 색
        static readonly Rect PortraitWindow = new Rect(3f, 3f, 58f, 58f);
        // 초상화(339:4): 64px 캐릭터 그림을 Figma는 2.985배로 잘라 넣었다 → 3배로, 창 가운데에서 Figma와 같은 도트가 오는
        // 정수 자리(Figma 이미지 왼쪽 위 = 틀 기준 -55.1·-10.1 → -56·-10). 창 왼쪽 위 기준.
        // 그림은 Figma 초상화 그림 그대로(`Hud/portrait`) — 게임 캐릭터 그림(10/14 팀원)은 그 뒤 Figma에서 바뀐 판과 다르다.
        static readonly Vector2 PortraitOffset = new Vector2(-59f, -13f);
        const float PortraitScale = 3f;

        // 173:2 "10" — Figma는 이 숫자만 옛 글꼴 "DNF Bit Bit OTF" 24px라 v2보다 글자가 작다. v2로 같은 크기로 보이게 20px
        // (글자 테두리 자리를 Figma 그림과 재서 맞춤). 가운데 정렬이라 상자만 넓혔다.
        static readonly Rect TimerBox = new Rect(280f, 20f, 80f, 26f);
        const int TimerSize = 20;
        static readonly Rect KillsBox = new Rect(220f, 47f, 200f, 12f);  // Figma에 없음(임시)
        static readonly Rect ComboLabel = new Rect(533f, 46f, 99f, 35f); // 181:5 "COMBO" 24px
        static readonly Rect ComboNum = new Rect(533f, 81f, 99f, 23f);   // 181:6 "32" 24px(560·45 상자의 가운데 = 582.5 그대로)
        const float SlotX = 20f, SlotY = 303f, SlotStep = 40f, SlotSize = 32f; // 181:2~181:4
        static readonly Rect Minimap = new Rect(504f, 280f, 128f, 64f);  // 166:30 "지도 1"
        static readonly Rect BossBar = new Rect(39f, 143f, 560f, 16f);   // 281:8 "보스 체력바 2"
        static readonly Rect BossFill = new Rect(4f, 4f, 552f, 8f);      // 막대 그림의 빈 안쪽(그림 픽셀)
        static readonly Rect PortalBox = new Rect(120f, 286f, 400f, 12f); // Figma에 없음(임시)
        static readonly Rect BuffBox = new Rect(6f, 73f, 480f, 12f);     // Figma에 없음(임시) — 원본 `#buffs`처럼 상태창 바로 아래
        static readonly Color BuffColor = new Color32(0x8e, 0xf0, 0xc8, 0xff); // 원본 `#buffs { color:#8ef0c8 }`(:38)
        static readonly Rect ExpBar = new Rect(0f, 356f, 640f, 4f);      // 124:5 1920×10 → 640판 4px(테두리 1 + 채움 2)

        // ── 색 ──
        static readonly Color ExpBorder = Px.Hex(0x472e17);  // 124:5 테두리
        static readonly Color ExpBack = Px.Hex(0x57306e);    // 124:6 "경험치 바"
        static readonly Color ExpFill = Px.Hex(0x44ff00);    // 124:7 "누적 경험치"(#4f0)
        static readonly Color NumberBottom = Px.Hex(0xc8c8d2); // Figma 숫자 아래쪽 연회색
        static readonly Color TimerLow = Px.Hex(0xff6a5a);     // 원본 `.low`(10초 이하)
        static readonly Color SlotFill = Px.Hex(0x92a1b9);     // 스킬 칸 아이콘 바탕
        static readonly Color SlotCooldownVeil = Px.Hex(0x00000a, 0.75f);
        static readonly Color SlotCooldownText = Px.Hex(0xfff0b8);
        static readonly Color SlotKeyText = Px.Hex(0xc8b890);
        static readonly Color SlotStackText = Px.Hex(0x8ef0c8);
        static readonly Color SlotNoStackText = Px.Hex(0xff8a7a);
        static readonly Color PortalOpen = Px.Hex(0xffd84a);
        static readonly Color PortalLocked = Px.Hex(0xd8d0c0);
        /// <summary>쿨다운 중 아이콘을 흐리는 덮개 — 원본 `opacity:.34`를 바탕색 66% 덮개로(예전 IMGUI HUD와 같은 계산).</summary>
        const float IconDimVeil = 1f - 0.34f;

        // 미니맵: 맵 가로(26유닛)를 128px에, 높이는 땅이 58·발판 1층마다 약 11px(Figma 그림의 층 간격 16px에 가깝게 4층이 들어가도록)
        const float MinimapGroundY = 58f, MinimapPxPerUnitY = 8f;
        static readonly Color MinimapLine = Px.Ink;
        static readonly Color MinimapEnemy = Px.Hex(0xea323c);
        static readonly Color MinimapPlayer = Color.white;
        static readonly Color MinimapPortal = Px.Hex(0x0cf1ff);

        /// <summary>캡처·테스트용 가짜 값 — 있으면 실제 게임 대신 이걸 그린다(`Editor/CaptureUiPreview`).</summary>
        public PreviewState? Preview;

        // ── 테스트가 읽는 값 ──
        public float HpFillAmount => hpFill != null ? hpFill.fillAmount : 0f;
        public string HpLabel => hpText != null ? hpText.text : "";
        public string TimerLabel => timer != null ? timer.text : "";
        public string BuffsLabel => buffs != null ? buffs.text : "";
        public float ExpFillWidth => expFill != null ? ((RectTransform)expFill.transform).sizeDelta.x : 0f;
        public bool ComboShown => comboNum != null && comboNum.enabled;
        public bool BossBarShown => bossRoot != null && bossRoot.gameObject.activeSelf;
        public int MinimapLineCount => platformLines.Count;

        public struct PreviewState
        {
            public float Hp, MaxHp, ExpRatio, TimeLeft;
            public int Combo, Kills;
            public SkillSlotState Attack, Skill;
            public Vector2 Player;
            public Vector2[] Enemies;
            public float? BossRatio;
            public bool PortalInRange, PortalUnlocked;
            /// <summary>버프 줄(원본 `#buffs`) — null이면 빈 줄.</summary>
            public string Buffs;
        }

        Image hpFill, flash;
        Text hpText, timer, kills, comboLabel, comboNum, portal, buffs;
        PxTextGradient timerGradient;
        Image portrait;
        RectTransform bossRoot;
        Image bossFill;
        Image expFill;
        readonly Slot[] slots = new Slot[3];
        RectTransform minimap;
        readonly List<Image> platformLines = new List<Image>();
        readonly List<Image> enemyDots = new List<Image>();
        Image playerDot, portalDot;
        bool? minimapBossArena;
        readonly List<Vector2> enemyPositions = new List<Vector2>();
        float enemyScanTimer;

        /// <summary>미니맵 포탈 점 자리 — 마지막으로 본 포탈 위치(포탈이 안 보이면 안 그린다).</summary>
        Vector2 portalPosition;

        class Slot
        {
            public RectTransform Root;
            public Image[] Dim = new Image[3], Veil = new Image[3];
            public Text Cooldown, Key, Stack;
        }

        // 스킬 칸 안쪽(그림 도트): 네 귀퉁이가 한 도트씩 깎인 24×24 — 몸통·맨 윗줄·맨 아랫줄 세 조각(예전 HUD와 같은 모양)
        static readonly Rect[] SlotInner = { new Rect(4f, 5f, 24f, 22f), new Rect(5f, 4f, 22f, 1f), new Rect(5f, 27f, 22f, 1f) };

        protected override void Build()
        {
            // 원본 `state.redFlash` — 맞으면 화면 전체가 잠깐 붉어진다(HUD보다 아래, 게임 위).
            flash = Px.FullFill(Back, "HurtFlash", Color.clear, blocksClicks: false);

            BuildStatusFrame();
            BuildTexts();
            for (int i = 0; i < 3; i++) slots[i] = BuildSlot(i);
            BuildMinimap();
            BuildBossBar();
            BuildExpBar();
        }

        void BuildStatusFrame()
        {
            var src = PxArt.Get("Hud/status_frame");
            // 바탕: 빨강·하늘 칸을 어두운 칸 색으로 비운 틀(하늘·어두운 칸은 아직 뜻이 없어 빈칸 — 사용자 10/23)
            var empty = PxArt.Derive(src, "empty", c => PxArt.Same(c, HpRed) || PxArt.Same(c, BarCyan) ? BarEmpty : c);
            var frame = Px.Image(Content, "StatusFrame", empty, FramePos.x, FramePos.y, FrameW, FrameH);

            // 체력: 틀 그림에서 빨간 칸만 남긴 그림을 왼쪽부터 채운다(모서리 깎인 모양 그대로)
            var red = PxArt.Derive(src, "hp", c => PxArt.Same(c, HpRed) ? c : new Color32(0, 0, 0, 0));
            hpFill = Px.Image(frame.transform, "HpFill", red, 0f, 0f, FrameW, FrameH);
            hpFill.type = Image.Type.Filled;
            hpFill.fillMethod = Image.FillMethod.Horizontal;
            hpFill.fillOrigin = (int)Image.OriginHorizontal.Left;

            var window = Px.Node(frame.transform, "Portrait", PortraitWindow.x, PortraitWindow.y,
                                 PortraitWindow.width, PortraitWindow.height);
            window.gameObject.AddComponent<RectMask2D>();
            portrait = Px.Image(window, "Face", PxArt.Get("Hud/portrait"), PortraitOffset.x, PortraitOffset.y,
                                64f * PortraitScale, 64f * PortraitScale);

            hpText = Px.Text(frame.transform, "HpText", "", HpTextBox.x, HpTextBox.y, HpTextBox.width, HpTextBox.height,
                             8, Color.white).WithOutline(Px.Ink);
        }

        void BuildTexts()
        {
            timer = Px.Text(Content, "Timer", "", TimerBox.x, TimerBox.y, TimerBox.width, TimerBox.height, TimerSize, Color.white);
            timerGradient = Number(timer);
            kills = Px.Text(Content, "Kills", "", KillsBox.x, KillsBox.y, KillsBox.width, KillsBox.height, 8, Color.white)
                      .WithOutline(Px.Ink);
            buffs = Px.Text(Content, "Buffs", "", BuffBox.x, BuffBox.y, BuffBox.width, BuffBox.height, 8, BuffColor, TextAnchor.MiddleLeft)
                      .WithOutline(Px.Ink); // 원본 `text-shadow:0 1px 2px #000`
            comboLabel = Px.Text(Content, "ComboLabel", "COMBO", ComboLabel.x, ComboLabel.y, ComboLabel.width,
                                 ComboLabel.height, 24, Color.white);
            Number(comboLabel);
            comboNum = Px.Text(Content, "ComboNum", "", ComboNum.x, ComboNum.y, ComboNum.width, ComboNum.height, 24, Color.white);
            Number(comboNum);
            portal = Px.Text(Content, "PortalPrompt", "", PortalBox.x, PortalBox.y, PortalBox.width, PortalBox.height, 10,
                             Color.white).WithOutline(Px.Ink);
        }

        /// <summary>Figma HUD 큰 숫자 — 위 흰색·아래 연회색, 1도트 테두리.</summary>
        static PxTextGradient Number(Text t)
        {
            var g = t.gameObject.AddComponent<PxTextGradient>();
            g.Top = Color.white;
            g.Bottom = NumberBottom;
            t.WithOutline(Px.Ink);
            return g;
        }

        Slot BuildSlot(int i)
        {
            string[] icons = { "Hud/icon_attack", "Hud/icon_skill", "Hud/icon_jump" };
            string[] keys = { "Z", "X", "C" };
            var s = new Slot();
            s.Root = Px.Node(Content, "Slot" + i, SlotX + i * SlotStep, SlotY, SlotSize, SlotSize);
            Px.Image(s.Root, "Icon", PxArt.Get(icons[i]), 0f, 0f, SlotSize, SlotSize);
            for (int k = 0; k < 3; k++)
            {
                var r = SlotInner[k];
                s.Dim[k] = Px.Fill(s.Root, "Dim" + k, new Color(SlotFill.r, SlotFill.g, SlotFill.b, IconDimVeil),
                                   r.x, r.y, r.width, r.height);
                s.Veil[k] = Px.Fill(s.Root, "Veil" + k, SlotCooldownVeil, r.x, r.y, r.width, r.height);
            }
            Px.Image(s.Root, "Frame", PxArt.Get("Hud/slot_frame"), 0f, 0f, SlotSize, SlotSize);
            s.Cooldown = Px.Text(s.Root, "Cooldown", "", 0f, 0f, SlotSize, SlotSize, 10, SlotCooldownText).WithOutline(Px.Ink);
            // 원본 `.key`는 칸 안 아래지만 Figma 칸은 안쪽이 아이콘 그림이라 칸 바로 밑에(예전 HUD와 같음)
            s.Key = Px.Text(s.Root, "Key", keys[i], 0f, SlotSize + 1f, SlotSize, 9f, 8, SlotKeyText).WithOutline(Px.Ink);
            s.Stack = Px.Text(s.Root, "Stack", "", 0f, 2f, SlotSize - 3f, 9f, 8, SlotStackText, TextAnchor.MiddleRight)
                        .WithOutline(Px.Ink);
            return s;
        }

        void BuildMinimap()
        {
            minimap = Px.Node(Content, "Minimap", Minimap.x, Minimap.y, Minimap.width, Minimap.height);
            portalDot = Px.Fill(minimap, "Portal", MinimapPortal, 0f, 0f, 2f, 3f);
            playerDot = Px.Fill(minimap, "Player", MinimapPlayer, 0f, 0f, 2f, 2f);
        }

        void BuildBossBar()
        {
            bossRoot = Px.Node(Content, "BossBar", BossBar.x, BossBar.y, BossBar.width, BossBar.height);
            bossFill = Px.Fill(bossRoot, "Fill", (Color)HpRed, BossFill.x, BossFill.y, BossFill.width, BossFill.height);
            Px.Image(bossRoot, "Frame", PxArt.Get("Hud/boss_bar"), 0f, 0f, BossBar.width, BossBar.height);
        }

        void BuildExpBar()
        {
            Px.Fill(Content, "ExpBorder", ExpBorder, ExpBar.x, ExpBar.y, ExpBar.width, ExpBar.height);
            Px.Fill(Content, "ExpBack", ExpBack, ExpBar.x + 1f, ExpBar.y + 1f, ExpBar.width - 2f, ExpBar.height - 2f);
            expFill = Px.Fill(Content, "ExpFill", ExpFill, ExpBar.x + 1f, ExpBar.y + 1f, 0f, ExpBar.height - 2f);
        }

        // ───────────────────────── 갱신 ─────────────────────────

        protected override void Refresh()
        {
            var v = Preview ?? Live();

            // 체력 — 칸 폭을 도트 단위로 채운다(반 도트가 안 생기게)
            float ratio = v.MaxHp > 0f ? Mathf.Clamp01(v.Hp / v.MaxHp) : 0f;
            hpFill.fillAmount = (HpX0 + Mathf.Round(ratio * HpW)) / FrameW;
            hpText.text = $"{Mathf.Ceil(v.Hp)}/{Mathf.Ceil(v.MaxHp)}";

            int t = Mathf.CeilToInt(v.TimeLeft);
            timer.text = t.ToString();
            bool low = t <= 10; // 원본 `classList.toggle('low', t <= 10)`
            if (timerGradient.Top != (low ? TimerLow : Color.white))
            {
                timerGradient.Top = low ? TimerLow : Color.white;
                timerGradient.Bottom = low ? new Color(TimerLow.r * 0.8f, TimerLow.g * 0.8f, TimerLow.b * 0.8f, 1f) : NumberBottom;
                timer.SetVerticesDirty();
            }

            kills.text = RunState.Mode == RunMode.Normal ? $"토벌 {v.Kills}/{RunState.RegionKillTarget}" : "보스전";
            buffs.text = v.Buffs ?? "";

            bool combo = v.Combo > 0; // 원본: 0이면 숨김(:1633)
            comboLabel.enabled = comboNum.enabled = combo;
            if (combo) comboNum.text = v.Combo.ToString();

            RefreshSlot(slots[0], v.Attack);
            RefreshSlot(slots[1], v.Skill);
            RefreshSlot(slots[2], default); // 점프는 원본도 쿨다운이 없다

            portal.enabled = v.PortalInRange;
            if (v.PortalInRange)
            {
                portal.text = v.PortalUnlocked ? "F — 보스 필드로 들어가기" : $"토벌 {v.Kills}/{RunState.RegionKillTarget} — 아직 열리지 않았다";
                portal.color = v.PortalUnlocked ? PortalOpen : PortalLocked;
            }

            bossRoot.gameObject.SetActive(v.BossRatio.HasValue);
            if (v.BossRatio.HasValue)
                ((RectTransform)bossFill.transform).sizeDelta = new Vector2(Mathf.Round(Mathf.Clamp01(v.BossRatio.Value) * BossFill.width), BossFill.height);

            ((RectTransform)expFill.transform).sizeDelta =
                new Vector2(Mathf.Round(Mathf.Clamp01(v.ExpRatio) * (ExpBar.width - 2f)), ExpBar.height - 2f);

            RefreshMinimap(v);

            var fc = new Color(1f, 30f / 255f, 10f / 255f, HitFeel.RedFlash * HitFeel.RedFlashAlphaScale);
            if (flash.color != fc) flash.color = fc;
        }

        /// <summary>
        /// 버프 줄 — 원본 `buffTxt`(project_test.html:6283~6318)와 같은 순서·같은 조건. 원본 기호(⚔·殺·⛩·⚠)는 도트 글꼴에 없어
        /// 글자로 바꿨다. 캐릭터별 몫(마법사 차지율, 메카닉 충전·부품 등)은 키트가 만든다(<see cref="ISkillSlotSource.BuffText"/>).
        /// 2026-11-04 옛 IMGUI HUD를 지우면서 새 HUD에 다시 붙였다(사용자 "버프 표시줄 추가").
        /// </summary>
        public static string BuffLine(ISkillSlotSource kit)
        {
            var parts = new System.Collections.Generic.List<string>();
            float dm = CombatModifiers.DamageMultiplier;
            if (dm > 1.01f) parts.Add($"피해 ×{dm:0.00}");                                   // :6284
            if (RunState.Fury > 0) parts.Add($"살기 x{RunState.Fury}");                       // :6285
            if (CombatModifiers.ShrineBuffLeft > 0f)
                parts.Add($"성소의 가호 {Mathf.CeilToInt(CombatModifiers.ShrineBuffLeft)}초");  // :6286
            if (CombatModifiers.ShrineActive) parts.Add("! 몬스터들이 성소의 힘을 받는 중!");   // :6287
            int gap = RegressionConfig.Gap(RunState.Region, ProfileService.Current.regressions);
            if (gap > 0) parts.Add($"! 회귀 부족 {gap}회");                                   // :6288~6289
            string own = kit?.BuffText;
            if (!string.IsNullOrWhiteSpace(own)) parts.Add(own.Trim());                       // :6290~6317
            return string.Join("  ", parts);
        }

        /// <summary>
        /// 지금 상태 — 전투 씬의 담당이 내준 읽기 창구(`Systems/WorldView.Combat`)에서만 읽는다(2026-11-05 2b — 예전엔 태그·타입으로
        /// 씬을 뒤져 플레이어·포탈·보스를 찾았다). 전투 씬이 없으면 체력·칸·포탈·보스는 비운다.
        /// </summary>
        PreviewState Live()
        {
            var view = WorldView.Combat;
            var profile = ProfileService.Current;
            long need = PlayerProfile.RequiredExp(profile.level);
            var src = view?.PlayerKit;
            var s = new PreviewState
            {
                Hp = view != null ? view.PlayerHp : 0f,
                MaxHp = view != null ? view.PlayerMaxHp : 0f,
                ExpRatio = need > 0 ? profile.exp / (float)need : 0f,
                TimeLeft = RunState.TimeLeft,
                Combo = CombatModifiers.Combo,
                Kills = profile.RegionKills(RunState.Region),
                Attack = src != null ? src.AttackSlot : default,
                Skill = src != null ? src.SkillSlot : default,
                Player = view != null ? view.PlayerPosition : Vector2.zero,
                Buffs = BuffLine(src),
            };
            if (view == null)
            {
                // 전투 씬이 없으면(타이틀·전환 중) 지난 포탈 점·적 점을 남기지 않는다(Codex 2b 리뷰).
                showPortalDot = false;
                enemyPositions.Clear();
                return s;
            }
            if (RunState.Mode == RunMode.Normal)
            {
                var portalView = view.Portal;
                showPortalDot = portalView.Visible;
                portalPosition = portalView.Position;
                s.PortalInRange = portalView.Visible && portalView.InRange;
                s.PortalUnlocked = portalView.Unlocked;
            }
            else
            {
                showPortalDot = false;
                s.BossRatio = view.BossHpRatio;
            }
            return s;
        }

        bool showPortalDot;

        static void RefreshSlot(Slot s, SkillSlotState st)
        {
            bool dim = st.Cooling || st.NoStack;
            float clipTop = 4f + (1f - Mathf.Clamp01(st.Fraction)) * 24f; // 아래에서부터 차오르는 덮개(원본 `.cd { height: frac% }`)
            for (int k = 0; k < 3; k++)
            {
                s.Dim[k].enabled = dim;
                var r = SlotInner[k];
                float top = Mathf.Max(r.y, clipTop), bottom = r.y + r.height;
                bool show = st.Fraction > 0f && bottom > top;
                s.Veil[k].enabled = show;
                if (show)
                {
                    var rt = (RectTransform)s.Veil[k].transform;
                    rt.anchoredPosition = new Vector2(r.x, -top);
                    rt.sizeDelta = new Vector2(r.width, bottom - top);
                }
            }
            s.Cooldown.enabled = st.Cooling;
            if (st.Cooling) s.Cooldown.text = st.CooldownText;
            s.Stack.enabled = st.HasStack;
            if (st.HasStack)
            {
                s.Stack.text = $"{st.Stacks}/{st.StackMax}";
                s.Stack.color = st.NoStack ? SlotNoStackText : SlotStackText;
            }
        }

        // ───────────────────────── 미니맵 ─────────────────────────

        static Vector2 MapPoint(Vector2 world)
        {
            float span = Mathf.Max(0.01f, FieldBounds.MaxX - FieldBounds.MinX);
            float x = (world.x - FieldBounds.MinX) / span * Minimap.width;
            float y = MinimapGroundY - (world.y - FieldBounds.GroundY) * MinimapPxPerUnitY;
            return new Vector2(Mathf.Round(Mathf.Clamp(x, 0f, Minimap.width - 1f)), Mathf.Round(Mathf.Clamp(y, 1f, Minimap.height - 1f)));
        }

        void RefreshMinimap(PreviewState v)
        {
            if (minimapBossArena != FieldLayout.IsBossArena) RebuildPlatforms();

            var p = MapPoint(v.Player);
            Place(playerDot, p.x - 1f, p.y - 2f);

            bool showPortal = !Preview.HasValue && RunState.Mode == RunMode.Normal && showPortalDot;
            portalDot.enabled = showPortal || Preview.HasValue;
            if (portalDot.enabled)
            {
                var q = MapPoint(showPortal ? portalPosition : new Vector2(FieldBounds.MaxX - 0.6f, 0f));
                Place(portalDot, q.x - 1f, MinimapGroundY - 3f);
            }

            IList<Vector2> enemies = v.Enemies;
            if (enemies == null)
            {
                enemyScanTimer -= Time.unscaledDeltaTime;
                if (enemyScanTimer <= 0f)
                {
                    enemyScanTimer = 0.15f; // 몹 위치는 0.15초마다(매 프레임 읽을 필요 없다)
                    var view = WorldView.Combat;
                    if (view != null) view.CopyEnemyPositions(enemyPositions);
                    else enemyPositions.Clear(); // 전투 씬이 없으면 점도 없다
                }
                enemies = enemyPositions;
            }
            while (enemyDots.Count < enemies.Count) enemyDots.Add(Px.Fill(minimap, "Enemy", MinimapEnemy, 0f, 0f, 2f, 2f));
            for (int i = 0; i < enemyDots.Count; i++)
            {
                bool on = i < enemies.Count;
                enemyDots[i].enabled = on;
                if (on)
                {
                    var e = MapPoint(enemies[i]);
                    Place(enemyDots[i], e.x - 1f, e.y - 2f);
                }
            }
            playerDot.transform.SetAsLastSibling(); // 플레이어 점이 몹 점 위에
        }

        void RebuildPlatforms()
        {
            minimapBossArena = FieldLayout.IsBossArena;
            foreach (var l in platformLines)
                if (l != null) { if (Application.isPlaying) Destroy(l.gameObject); else DestroyImmediate(l.gameObject); }
            platformLines.Clear();
            // 땅 — 맨 아래 한 줄
            platformLines.Add(Px.Fill(minimap, "Ground", MinimapLine, 0f, MinimapGroundY, Minimap.width, 1f));
            var table = FieldLayout.Platforms;
            for (int i = 0; i < table.GetLength(0); i++)
            {
                float cx = table[i, 0], w = table[i, 2];
                var a = MapPoint(new Vector2(cx - w / 2f, FieldLayout.PlatformTopY(i)));
                var b = MapPoint(new Vector2(cx + w / 2f, FieldLayout.PlatformTopY(i)));
                platformLines.Add(Px.Fill(minimap, "Platform" + i, MinimapLine, a.x, a.y, Mathf.Max(1f, b.x - a.x), 1f));
            }
            foreach (var l in platformLines) l.transform.SetAsFirstSibling();
        }

        static void Place(Image img, float x, float y) =>
            ((RectTransform)img.transform).anchoredPosition = new Vector2(x, -y);
    }
}
