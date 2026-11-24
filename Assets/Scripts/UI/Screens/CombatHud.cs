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
    /// 전투 HUD — Figma "스테이지 HUD"(124:3, 640×360). **2026-11-22 새 디자인(Drive `HUD.zip`)으로 교체**: 초상화 틀이 없어지고
    /// 왼쪽 위 판 = 체력 막대 + 골드 막대, 가운데 위 판 = 보스 이름·체력, 오른쪽 아래 미니맵 판. 아래는 그 전 결정(2026-10-24)이며 초상화·빈 칸 부분은 더 안 쓴다:
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
            if (view == null) return;
            int i = popups.FindIndex(p => !p.text.enabled);
            Text t;
            if (i < 0)
            {
                t = NewPopupText();
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

        /// <summary>팝업 글씨 하나 — 프리팹의 견본(`PopupTemplate`)을 복제한다(견본이 없으면 예전처럼 코드로).</summary>
        Text NewPopupText()
        {
            Text t;
            if (view.popupTemplate != null)
            {
                t = Instantiate(view.popupTemplate, view.popupTemplate.transform.parent, false);
                t.gameObject.SetActive(true);
            }
            else t = Px.Text(view.board != null ? view.board : Content, "Popup", "", 0f, 0f, 80f, 12f, PopupSize, Color.white).WithOutline(Px.Ink);
            t.name = "Popup";
            return t;
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

        // 모양(좌표·색)은 프리팹 CombatHudView에 있다(2026-11-23 프리팹 UI 14호). 여기엔 값 읽기·글자·막대 폭·연출 계산만.
        /// <summary>처음 나타날 때 0에서 차오르는 시간(Codex 상의 — 등장 연출 최소안).</summary>
        public const float BossIntroTime = 0.25f;
        /// <summary>맞은 만큼 연노랑 잔상이 남았다가 줄어드는 시간.</summary>
        public const float BossTrailTime = 0.35f;

        CombatHudView view;
        // 막대 100% 폭 — 처음 프리팹 폭(채움 칸·바탕)을 기준으로 한다(프리팹에서 폭을 바꾸면 그 폭으로 채운다)
        float hpFullW = 156f, hpH = 12f, bossFullW = 394f, bossH = 10f, expFullW = 638f, expH = 2f;

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

        /// <summary>캡처·테스트용 가짜 값 — 있으면 실제 게임 대신 이걸 그린다(`Editor/CaptureUiPreview`).</summary>
        public PreviewState? Preview;

        // ── 테스트가 읽는 값 ──
        public float HpFillAmount => hpFill != null && hpFullW > 0f ? ((RectTransform)hpFill.transform).sizeDelta.x / hpFullW : 0f;
        public string HpLabel => hpText != null ? hpText.text : "";
        public string GoldLabel => goldText != null ? goldText.text : "";
        public string TimerLabel => timer != null ? timer.text : "";
        public string BuffsLabel => buffs != null ? buffs.text : "";
        public float ExpFillWidth => expFill != null ? ((RectTransform)expFill.transform).sizeDelta.x : 0f;
        public bool ComboShown => comboNum != null && comboNum.enabled;
        public bool BossBarShown => bossRoot != null && bossRoot.gameObject.activeSelf;
        public float BossFillWidth => bossFill != null ? ((RectTransform)bossFill.transform).sizeDelta.x : 0f;
        public float BossTrailWidth => bossTrail != null ? ((RectTransform)bossTrail.transform).sizeDelta.x : 0f;
        public RectTransform BossBarRect => bossRoot;
        public string BossNameLabel => bossName != null && RunState.Mode != RunMode.Normal ? bossName.text : "";
        public int MinimapLineCount => minimap != null ? minimap.PlatformLineCount : 0;

        public struct PreviewState
        {
            public float Hp, MaxHp, ExpRatio, TimeLeft;
            public int Combo, Kills, Gold;
            public SkillSlotState Attack, Skill;
            public Vector2 Player;
            public Vector2[] Enemies;
            public float? BossRatio;
            public bool PortalInRange, PortalUnlocked;
            /// <summary>버프 줄(원본 `#buffs`) — null이면 빈 줄.</summary>
            public string Buffs;
        }

        Image hpFill, flash;
        Text hpText, goldText, timer, kills, comboLabel, comboNum, portal, buffs;
        PxTextGradient timerGradient;
        RectTransform bossRoot;
        Text bossName;
        Image bossSkullLeft, bossSkullRight;
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
            public Rect[] Inner = new Rect[3]; // 덮개가 차오를 범위 = 같은 번호 Dim 칸 자리(칸 안 좌표, y는 아래로)
            public Text Cooldown, Key, Stack;
        }

        /// <summary>
        /// 모양은 프리팹(<see cref="CombatHudView"/>, `Resources/UI/Views/CombatHudView`)이 원본이다(2026-11-23 프리팹 UI 14호).
        /// 여기선 조각을 받아 오고, 막대 100% 폭을 프리팹에서 재 두고, 유물 칸·미니맵 안쪽을 그 자리에 짓는다(개수가 실행 중에 바뀌는 부분).
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            view = LoadView<CombatHudView>(CombatHudView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[CombatHud] 화면 프리팹(Resources/" + CombatHudView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/전투 HUD 만들기", this);
                view = CombatHudView.CreateFromCode(Root);
            }
            if (view.slots.Length != slots.Length)
                Debug.LogError($"[CombatHud] 화면 프리팹 스킬 칸 수가 다르다({view.slots.Length}) — {slots.Length}(Z·X·C)이어야 한다", view);

            flash = view.hurtFlash;
            hpFill = view.hpFill;
            hpText = view.hpText;
            goldText = view.goldText;
            if (hpFill != null) { var r = ((RectTransform)hpFill.transform).sizeDelta; hpFullW = r.x; hpH = r.y; }

            timer = view.timer;
            timerGradient = view.timerGradient;
            kills = view.kills;
            buffs = view.buffs;
            comboLabel = view.comboLabel;
            comboNum = view.comboNum;
            portal = view.portal;

            // 보유 유물 칸(2026-11-19 — Figma에 없음) — 자리는 프리팹, 칸은 코드
            relicStrip = view.relicStrip != null ? RelicHudStrip.Attach(view.relicStrip) : null;

            for (int i = 0; i < slots.Length && i < view.slots.Length; i++)
            {
                var vs = view.slots[i];
                var s = new Slot { Root = vs.root, Cooldown = vs.cooldown, Key = vs.key, Stack = vs.stack };
                for (int k = 0; k < 3 && k < vs.dim.Length && k < vs.veil.Length; k++)
                {
                    s.Dim[k] = vs.dim[k];
                    s.Veil[k] = vs.veil[k];
                    var rt = (RectTransform)vs.dim[k].transform;
                    s.Inner[k] = new Rect(rt.anchoredPosition.x, -rt.anchoredPosition.y, rt.sizeDelta.x, rt.sizeDelta.y);
                }
                slots[i] = s;
            }

            minimap = view.minimap != null ? new CombatMinimapView(view.minimap) : null;

            bossRoot = view.bossRoot;
            bossName = view.bossName;
            bossSkullLeft = view.bossSkullLeft;
            bossSkullRight = view.bossSkullRight;
            bossFill = view.bossFill;
            bossTrail = view.bossTrail;
            if (bossFill != null) { var r = ((RectTransform)bossFill.transform).sizeDelta; bossFullW = r.x; bossH = r.y; }

            expFill = view.expFill;
            if (view.expBack != null) expFullW = ((RectTransform)view.expBack.transform).sizeDelta.x;
            if (expFill != null) expH = ((RectTransform)expFill.transform).sizeDelta.y;
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
            PlaceBossSkulls();
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
            ((RectTransform)bossFill.transform).sizeDelta = new Vector2(Mathf.Round(bossShown * bossFullW), bossH);
            ((RectTransform)bossTrail.transform).sizeDelta = new Vector2(Mathf.Round(bossTrailRatio * bossFullW), bossH);
        }

        /// <summary>해골 둘을 이름 글자 양옆에 붙인다 — 이름 길이에 따라 자리가 달라진다.</summary>
        void PlaceBossSkulls()
        {
            // 세로 자리·해골 크기는 프리팹 그대로, 가로만 이름 길이에 맞춘다
            float half = Mathf.Ceil(bossName.preferredWidth) / 2f, mid = bossRoot.sizeDelta.x / 2f, gap = view.bossSkullGap;
            var l = (RectTransform)bossSkullLeft.transform;
            var r = (RectTransform)bossSkullRight.transform;
            l.anchoredPosition = new Vector2(Mathf.Round(mid - half - gap - l.sizeDelta.x), l.anchoredPosition.y);
            r.anchoredPosition = new Vector2(Mathf.Round(mid + half + gap), r.anchoredPosition.y);
        }

        // ───────────────────────── 갱신 ─────────────────────────

        RelicHudStrip relicStrip;

        // 2026-11-20 최적화 — 값이 바뀔 때만 글자를 새로 만든다(매 프레임 문자열을 만들던 것)
        long shownGold = long.MinValue;
        int shownHp = int.MinValue, shownMaxHp = int.MinValue, shownTime = int.MinValue, shownCombo = int.MinValue;
        int shownKills = int.MinValue, shownRegion = int.MinValue, shownStage = int.MinValue, shownPortalKills = int.MinValue;
        RunMode shownMode;
        bool shownMidBoss, shownPortalUnlocked;
        string shownBuffs;

        protected override void Refresh()
        {
            var v = Preview ?? Live();
            UpdatePopups();
            relicStrip?.Refresh(true);

            // 체력 — 칸 폭을 도트 단위로 채운다(반 도트가 안 생기게)
            float ratio = v.MaxHp > 0f ? Mathf.Clamp01(v.Hp / v.MaxHp) : 0f;
            ((RectTransform)hpFill.transform).sizeDelta = new Vector2(Mathf.Round(ratio * hpFullW), hpH);
            // 2026-11-20 최적화 — 값이 바뀔 때만 글자를 새로 만든다
            int hpNow = Mathf.CeilToInt(v.Hp), hpMax = Mathf.CeilToInt(v.MaxHp);
            if (hpNow != shownHp || hpMax != shownMaxHp)
            {
                shownHp = hpNow; shownMaxHp = hpMax;
                hpText.text = $"{hpNow}/{hpMax}";
            }
            if (v.Gold != shownGold) { shownGold = v.Gold; goldText.text = v.Gold.ToString("N0"); }

            int t = Mathf.CeilToInt(v.TimeLeft);
            if (t != shownTime) { shownTime = t; timer.text = t.ToString(); }
            bool low = t <= 10; // 원본 `classList.toggle('low', t <= 10)`
            var timerLow = view.timerLow;
            if (timerGradient != null && timerGradient.Top != (low ? timerLow : Color.white))
            {
                timerGradient.Top = low ? timerLow : Color.white;
                timerGradient.Bottom = low ? new Color(timerLow.r * 0.8f, timerLow.g * 0.8f, timerLow.b * 0.8f, 1f) : view.numberBottom;
                timer.SetVerticesDirty();
            }

            // 2026-11-19 챕터 구조 — 일반은 "1-3 토벌 37/100". 보스전엔 토벌 수가 없고 이름은 보스 판 위 줄(팀원 HUD) — 중간 보스전은 "1-5 중간 보스"
            if (v.Kills != shownKills || RunState.Region != shownRegion || RunState.Stage != shownStage
                || RunState.Mode != shownMode || RunState.IsMidBoss != shownMidBoss)
            {
                shownKills = v.Kills; shownRegion = RunState.Region; shownStage = RunState.Stage;
                shownMode = RunState.Mode; shownMidBoss = RunState.IsMidBoss;
                string label = ChapterConfig.StageLabel(RunState.Region, RunState.Stage);
                kills.text = RunState.Mode == RunMode.Normal ? $"{label} 토벌 {v.Kills}/{ChapterConfig.StageKillTarget}" : "";
                bossName.text = RunState.IsMidBoss ? $"{label} 중간 보스" : RegionConfig.BossNameOf(RunState.Region);
            }
            string b = v.Buffs ?? "";
            if (!string.Equals(b, shownBuffs)) { shownBuffs = b; buffs.text = b; }

            bool combo = v.Combo > 0; // 원본: 0이면 숨김(:1633)
            comboLabel.enabled = comboNum.enabled = combo;
            if (combo && v.Combo != shownCombo) { shownCombo = v.Combo; comboNum.text = v.Combo.ToString(); }

            RefreshSlot(slots[0], v.Attack);
            RefreshSlot(slots[1], v.Skill);
            RefreshSlot(slots[2], default); // 점프는 원본도 쿨다운이 없다

            portal.enabled = v.PortalInRange;
            if (v.PortalInRange && (v.PortalUnlocked != shownPortalUnlocked || v.Kills != shownPortalKills || portal.text.Length == 0))
            {
                shownPortalUnlocked = v.PortalUnlocked; shownPortalKills = v.Kills;
                portal.text = v.PortalUnlocked ? "F — 중간 보스에게 가기" : $"토벌 {v.Kills}/{ChapterConfig.StageKillTarget} — 아직 열리지 않았다";
                portal.color = v.PortalUnlocked ? view.portalOpen : view.portalLocked;
            }

            RefreshBossBar(v.BossRatio);

            ((RectTransform)expFill.transform).sizeDelta = new Vector2(Mathf.Round(Mathf.Clamp01(v.ExpRatio) * expFullW), expH);

            minimap?.Refresh(v.Player, v.Enemies, MinimapPortal());

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
            // 2026-11-20 최적화 — 들어가는 값이 지난번과 같으면 지난 줄을 그대로 돌려준다(매 프레임 목록·문자열을 만들던 것)
            float dm = CombatModifiers.DamageMultiplier;
            int dmKey = dm > 1.01f ? Mathf.RoundToInt(dm * 100f) : 0;
            int shrineLeft = CombatModifiers.ShrineBuffLeft > 0f ? Mathf.CeilToInt(CombatModifiers.ShrineBuffLeft) : 0;
            int gapKey = RegressionConfig.Gap(RunState.Region, ProfileService.Current.regressions);
            string ownKey = kit?.BuffText;
            if (buffLine != null && dmKey == buffDm && RunState.Fury == buffFury && shrineLeft == buffShrine
                && CombatModifiers.ShrineActive == buffShrineActive && gapKey == buffGap && string.Equals(ownKey, buffOwn))
                return buffLine;
            buffDm = dmKey; buffFury = RunState.Fury; buffShrine = shrineLeft;
            buffShrineActive = CombatModifiers.ShrineActive; buffGap = gapKey; buffOwn = ownKey;
            buffLine = BuildBuffLine(kit);
            return buffLine;
        }

        static string buffLine, buffOwn;
        static int buffDm, buffFury, buffShrine, buffGap;
        static bool buffShrineActive;

        static string BuildBuffLine(ISkillSlotSource kit)
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
            var world = WorldView.Combat; // (필드 view = 화면 모양과 헷갈리지 않게 world)
            var profile = ProfileService.Current;
            long need = PlayerProfile.RequiredExp(profile.level);
            var src = world?.PlayerKit;
            var s = new PreviewState
            {
                Hp = world != null ? world.PlayerHp : 0f,
                MaxHp = world != null ? world.PlayerMaxHp : 0f,
                ExpRatio = need > 0 ? profile.exp / (float)need : 0f,
                TimeLeft = RunState.TimeLeft,
                Combo = CombatModifiers.Combo,
                Kills = profile.StageKills(RunState.Region, RunState.Stage),
                Gold = profile.gold,
                Attack = src != null ? src.AttackSlot : default,
                Skill = src != null ? src.SkillSlot : default,
                Player = world != null ? world.PlayerPosition : Vector2.zero,
                Buffs = BuffLine(src),
            };
            if (world == null)
            {
                // 전투 씬이 없으면(타이틀·전환 중) 지난 포탈 점·적 점을 남기지 않는다(Codex 2b 리뷰).
                showPortalDot = false;
                minimap?.ForgetEnemies();
                return s;
            }
            if (RunState.Mode == RunMode.Normal)
            {
                var portalView = world.Portal;
                showPortalDot = portalView.Visible;
                portalPosition = portalView.Position;
                s.PortalInRange = portalView.Visible && portalView.InRange;
                s.PortalUnlocked = portalView.Unlocked;
            }
            else
            {
                showPortalDot = false;
                s.BossRatio = world.BossHpRatio;
            }
            return s;
        }

        bool showPortalDot;

        void RefreshSlot(Slot s, SkillSlotState st)
        {
            if (s == null) return;
            bool dim = st.Cooling || st.NoStack;
            // 아래에서부터 차오르는 덮개(원본 `.cd { height: frac% }`) — 범위 = Dim 세 조각을 합친 세로 범위(처음 모양은 y 4~28)
            float top0 = float.MaxValue, bottom0 = float.MinValue;
            for (int k = 0; k < 3; k++) { if (s.Dim[k] == null) continue; top0 = Mathf.Min(top0, s.Inner[k].y); bottom0 = Mathf.Max(bottom0, s.Inner[k].yMax); }
            float clipTop = top0 + (1f - Mathf.Clamp01(st.Fraction)) * (bottom0 - top0);
            for (int k = 0; k < 3; k++)
            {
                if (s.Dim[k] == null || s.Veil[k] == null) continue;
                s.Dim[k].enabled = dim;
                var r = s.Inner[k];
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
                s.Stack.color = st.NoStack ? view.noStackText : view.stackText;
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
