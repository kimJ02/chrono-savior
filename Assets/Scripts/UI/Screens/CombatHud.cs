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
    /// 가운데 숫자 = 남은 초, 오른쪽 위 COMBO, 왼쪽 아래 스킬 칸 3개(Z·X·C), 오른쪽 아래 미니맵(실제 맵을 그린다 — <see cref="CombatMinimapView"/>),
    /// **경험치 = 화면 맨 아래 가로 바**(사용자 "A — Figma 디자인 가져와서": 숨겨진 옛 1920판 "경험치 바" 124:5의 색 그대로).
    ///
    /// Figma에 없는데 임시로 둔 것(사용자 확인 전): 남은 초 밑의 토벌 진행(100마리 → 보스가 게임의 뼈대라서),
    /// 포탈 근처 안내. 원본 HUD의 레벨·골드·버프 줄·이번 사냥 수입은 Figma에 없어서 뺐다(사용자에게 물어볼 것).
    /// 동작(쿨다운 덮개·숫자, 메카닉 충전 스택, 남은 시간 10초 이하 붉게, 콤보 0이면 숨김)은 원본·예전 IMGUI HUD와 같다.
    /// </summary>
    public class CombatHud : PxScreen
    {
        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Hud;

        [Tooltip("월드 위 팝업 글씨(예: 블랙홀 '중첩 x3')를 놓을 때 쓰는 공용 카메라(같은 상주 씬)")]
        [SerializeField] Camera cam;
        /// <summary>씬 검증·테스트가 읽고 넣는다.</summary>
        public Camera Camera { get => cam; set => cam = value; }

        // ── 월드 팝업 글씨(원본 `popDmg`) — 2026-11-09 ──
        public const float PopupLife = 0.8f, PopupRise = 0.35f;
        public const int PopupSize = 10;
        struct Popup { public Text text; public Vector2 world; public float t; }
        readonly List<Popup> popups = new List<Popup>();

        protected override void Awake()
        {
            base.Awake();
            CombatEvents.PopupRequested += ShowPopup;
        }

        protected override void OnDestroy()
        {
            CombatEvents.PopupRequested -= ShowPopup;
            base.OnDestroy();
        }

        /// <summary>지금 떠 있는 팝업 글씨들(테스트용).</summary>
        public IEnumerable<string> ActivePopups { get { foreach (var p in popups) if (p.text.enabled) yield return p.text.text; } }

        void ShowPopup(Vector2 world, string text, Color color)
        {
            if (Content == null) return;
            int i = popups.FindIndex(p => !p.text.enabled);
            Text t;
            if (i < 0)
            {
                t = Px.Text(Content, "Popup", "", 0f, 0f, 80f, 12f, PopupSize, color).WithOutline(Px.Ink);
                popups.Add(new Popup { text = t });
                i = popups.Count - 1;
            }
            t = popups[i].text;
            t.text = text;
            t.color = color;
            t.enabled = true;
            popups[i] = new Popup { text = t, world = world, t = 0f };
            PlacePopup(popups[i]);
        }

        void UpdatePopups()
        {
            for (int i = 0; i < popups.Count; i++)
            {
                var p = popups[i];
                if (!p.text.enabled) continue;
                p.t += Time.deltaTime; // 일시정지 땐 멈춘다
                if (p.t >= PopupLife) { p.text.enabled = false; popups[i] = p; continue; }
                var c = p.text.color;
                c.a = 1f - Mathf.Clamp01((p.t - PopupLife * 0.5f) / (PopupLife * 0.5f)); // 뒤 절반에 흐려진다
                p.text.color = c;
                popups[i] = p;
                PlacePopup(p);
            }
        }

        void PlacePopup(Popup p)
        {
            if (cam == null) return;
            Px.PlaceAtWorld(p.text, p.world + new Vector2(0f, PopupRise * Mathf.Clamp01(p.t / PopupLife)), cam.transform.position);
        }
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
        // 보스 체력바 — **상단 가운데, 타이머·보스 이름 바로 아래**(2026-11-10 사용자 A안). Figma(281:8)는 화면 한가운데(39,143) 560폭이라
        // 플레이 영역을 가로질러 가렸다. 그림은 Figma 막대의 왼쪽 절반 + 오른쪽 절반을 이어 붙인 240폭(`Hud/boss_bar_short` — 테두리 무늬가 있어 늘이면 뭉개진다).
        // 왼쪽 위 상태창(4~196)과 안 겹치게 x 200부터. 이름은 '토벌' 줄 자리(KillsBox)에 — 보스전엔 토벌 수가 없다.
        static readonly Rect BossBar = new Rect(200f, 60f, 240f, 16f);
        static readonly Rect BossFill = new Rect(4f, 4f, 232f, 8f);      // 막대 그림의 빈 안쪽(그림 픽셀)
        /// <summary>처음 나타날 때 0에서 차오르는 시간(Codex 상의 — 등장 연출 최소안).</summary>
        public const float BossIntroTime = 0.25f;
        /// <summary>맞은 만큼 연노랑 잔상이 남았다가 줄어드는 시간.</summary>
        public const float BossTrailTime = 0.35f;
        static readonly Color32 BossTrailColor = new Color32(255, 236, 190, 255);
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
        public float BossFillWidth => bossFill != null ? ((RectTransform)bossFill.transform).sizeDelta.x : 0f;
        public float BossTrailWidth => bossTrail != null ? ((RectTransform)bossTrail.transform).sizeDelta.x : 0f;
        public RectTransform BossBarRect => bossRoot;
        public string BossNameLabel => kills != null && RunState.Mode != RunMode.Normal ? kills.text : "";
        public int MinimapLineCount => minimap != null ? minimap.PlatformLineCount : 0;

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
        Image bossFill, bossTrail;
        float bossShown, bossTrailRatio, bossIntroT, bossTrailSpeed;
        bool bossWasShown;
        Image expFill;
        readonly Slot[] slots = new Slot[3];
        CombatMinimapView minimap;

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
            minimap = new CombatMinimapView(Content, Minimap);
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

        void BuildBossBar()
        {
            bossRoot = Px.Node(Content, "BossBar", BossBar.x, BossBar.y, BossBar.width, BossBar.height);
            bossTrail = Px.Fill(bossRoot, "Trail", (Color)BossTrailColor, BossFill.x, BossFill.y, BossFill.width, BossFill.height);
            bossFill = Px.Fill(bossRoot, "Fill", (Color)HpRed, BossFill.x, BossFill.y, BossFill.width, BossFill.height);
            Px.Image(bossRoot, "Frame", PxArt.Get("Hud/boss_bar_short"), 0f, 0f, BossBar.width, BossBar.height);
        }

        /// <summary>
        /// 보스 체력바 — 처음 나타나면 <see cref="BossIntroTime"/>에 걸쳐 차오르고, 맞으면 깎인 만큼 연노랑 잔상이 <see cref="BossTrailTime"/>에 걸쳐 줄어든다.
        /// 게임 시간(일시정지·히트스톱엔 멈춤). 채움 폭은 정수 픽셀.
        /// </summary>
        void RefreshBossBar(float? ratio)
        {
            bool show = ratio.HasValue;
            bossRoot.gameObject.SetActive(show);
            if (!show) { bossWasShown = false; return; }
            float target = Mathf.Clamp01(ratio.Value);
            float dt = Time.deltaTime;
            if (!bossWasShown) { bossWasShown = true; bossIntroT = 0f; bossShown = 0f; bossTrailRatio = 0f; }
            if (!Application.isPlaying) { bossIntroT = BossIntroTime; bossShown = bossTrailRatio = target; } // 편집기 캡처(시간이 안 흐름)는 최종 모습

            if (bossIntroT < BossIntroTime)
            {
                bossIntroT += dt;
                bossShown = target * Mathf.Clamp01(bossIntroT / BossIntroTime);
                bossTrailRatio = bossShown;
            }
            else
            {
                if (target < bossShown) // 새로 깎였다 — 잔상은 지금 자리에서 출발해 BossTrailTime에 걸쳐 따라온다
                {
                    bossTrailRatio = Mathf.Max(bossTrailRatio, bossShown);
                    bossTrailSpeed = (bossTrailRatio - target) / BossTrailTime;
                }
                bossShown = target;
                if (bossTrailRatio > bossShown) bossTrailRatio = Mathf.Max(bossShown, bossTrailRatio - bossTrailSpeed * dt);
                else bossTrailRatio = bossShown;
            }
            ((RectTransform)bossFill.transform).sizeDelta = new Vector2(Mathf.Round(bossShown * BossFill.width), BossFill.height);
            ((RectTransform)bossTrail.transform).sizeDelta = new Vector2(Mathf.Round(bossTrailRatio * BossFill.width), BossFill.height);
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
            UpdatePopups();

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

            kills.text = RunState.Mode == RunMode.Normal ? $"토벌 {v.Kills}/{RunState.RegionKillTarget}" : RegionConfig.BossNameOf(RunState.Region);
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

            RefreshBossBar(v.BossRatio);

            ((RectTransform)expFill.transform).sizeDelta =
                new Vector2(Mathf.Round(Mathf.Clamp01(v.ExpRatio) * (ExpBar.width - 2f)), ExpBar.height - 2f);

            minimap.Refresh(v.Player, v.Enemies, MinimapPortal());

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
            var parts = new List<string>();
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
                minimap.ForgetEnemies();
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

        /// <summary>
        /// 미니맵 포탈 점 자리 — 일반 사냥에서 포탈이 보이면 그 위치, 캡처·테스트(가짜 값)면 맵 오른쪽 끝 고정 자리, 그 밖엔 없음.
        /// </summary>
        Vector2? MinimapPortal()
        {
            if (Preview.HasValue) return new Vector2(FieldBounds.MaxX - 0.6f, 0f);
            return RunState.Mode == RunMode.Normal && showPortalDot ? portalPosition : (Vector2?)null;
        }
    }
}
