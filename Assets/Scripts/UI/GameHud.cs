using UnityEngine;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 사냥 중 HUD — 원본 `#hud`(project_test.html:487)와 `syncHUD()`(`:6255`)에 대응한다.
    ///
    /// ## 왜 판을 안 깔았나
    /// 로비·결과 화면은 어두운 판으로 배경을 덮어서 글씨를 읽히게 만든다. **HUD는 그럴 수 없다** —
    /// 판을 깔면 정작 봐야 할 사냥터를 가린다. 원본이 쓰는 방법은 둘이다:
    ///
    ///   1. 수치는 **바**로 보여준다 — 반투명 검정 바탕(`rgba(0,0,0,.55)`)에 색 채움(`.bar` `:31`).
    ///      바 자체가 작은 대비 영역이라 그 위의 글씨가 읽힌다.
    ///   2. 바 밖의 글자에는 전부 **그림자**를 준다 — `text-shadow:0 1px 2px #000`(`:35`~`:47`).
    ///
    /// 예전엔 IMGUI 기본 라벨을 그대로 썼는데, 그건 밝은 판에 검은 글씨를 전제로 한 스킨이라
    /// 어두운 사냥터 위에서 몬스터·발판과 섞였다.
    ///
    /// ## 스킬 슬롯 (Z·X·C)
    /// 왼쪽 아래 세 칸 — **모양은 Figma**("보스 HUD" 124:3의 181:2~181:4, 640×360 그림을 정확히 2배로),
    /// **동작은 원본**(`#slots` `:59`, `buildSlots` `:6217`, `setSlotCooldown` `:6248`, `syncHUD` 슬롯 부분 `:6319`):
    /// 쿨다운이 돌면 어두운 덮개가 아래에서 차오르고 남은 초가 뜨며 아이콘이 흐려진다. 메카닉 X 칸엔 충전 스택·부품
    /// `n/최대`가 붙는다. 값은 키트가 <see cref="ISkillSlotSource"/>로 내준다 — 구현하지 않은 키트(지금은 섬영)는
    /// 아이콘만 보인다.
    ///
    /// ## 아직 없는 것
    /// 보스 체력바(`#bossbar` `:49`)는 원본에 있지만 여기 없다.
    /// 가독성 문제가 아니라 **따로 만들어야 하는 기능**이라 이번 범위 밖으로 뒀다.
    ///
    /// 일시정지·결과 화면에서도 계속 그린다 — 원본도 `#hud`를 로비에서만 숨긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameHud : MonoBehaviour
    {
        // 원본 좌표 그대로(`#topbar` :30, `#timerbox` :39, `#combo` :52, `#hints` :74).
        const float BarWidth = 320f;   // 원본 `#topbar { width:320px }`
        const float BarHeight = 20f;   // 원본 `.bar { height:20px }`
        const float BarGap = 26f;      // 20 + margin-bottom 6
        const float LeftX = 16f;       // 원본 `left:16px`
        const float TopY = 14f;        // 원본 `top:14px`

        /// <summary>이 이하로 남으면 타이머가 붉어진다. 원본 `classList.toggle('low', t <= 10)`(`:6267`).</summary>
        const int LowTimeSeconds = 10;

        // 스킬 슬롯 — Figma "보스 HUD"(640×360)의 181:2~181:4 좌표를 **2배**로(우리 설계 좌표 1280×720이 정확히 2배라
        // 32도트 그림이 정수배로 커져 도트가 안 깨진다). 원본 CSS는 58×58·간격 8·left 16·bottom 14였다.
        const float SlotSize = 64f;    // Figma 32×32
        const float SlotLeft = 40f;    // Figma x 20
        const float SlotTop = 606f;    // Figma y 303
        const float SlotStep = 80f;    // Figma 칸 간격 40(칸 32 + 틈 8)
        /// <summary>슬롯 그림의 도트 수(가로·세로). 안쪽 영역 좌표를 이 도트 단위로 적는다.</summary>
        const float SlotDots = 32f;
        /// <summary>
        /// 쿨다운 중 아이콘을 흐리는 덮개의 불투명도. 원본은 아이콘을 `opacity:.34`로 흐리는데, Figma 칸은 안쪽 바탕이
        /// 한 색이라 **바탕색을 66%로 덮으면 아이콘을 34%로 그린 것과 계산이 똑같다**(틀은 안 덮는다).
        /// </summary>
        const float IconDimVeil = 1f - 0.34f;

        PlayerHealth health;
        PlayerRig rig;
        BossPortal portal;

        void Awake() => FindPlayer();

        void FindPlayer()
        {
            var player = GameObject.FindGameObjectWithTag("Player");
            if (player == null) return;
            health = player.GetComponent<PlayerHealth>();
            rig = player.GetComponent<PlayerRig>();
        }

        /// <summary>원본 `state.redFlash`(:6203~:6204) — 플레이어가 맞으면 화면 전체가 잠깐 붉어진다(`Core/HitFeel`).</summary>
        static void DrawHurtFlash()
        {
            float r = HitFeel.RedFlash;
            if (r <= 0f || Event.current.type != EventType.Repaint) return;
            var prev = GUI.color;
            GUI.color = new Color(1f, 30f / 255f, 10f / 255f, r * HitFeel.RedFlashAlphaScale);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }

        void OnGUI()
        {
            // 로비·타이틀에서는 HUD가 없다(원본도 `#hud`를 로비에서 숨긴다).
            if (GameState.Current == GameScene.Lobby || GameState.Current == GameScene.Title) return;
            if (PxScreens.Has<CombatHud>()) return; // Figma 전투 HUD가 그린다(2026-10-24)
            if (health == null) FindPlayer();
            DrawHurtFlash(); // 화면 전체라 배율(Scaled) 밖에서

            using var scaled = UiTheme.Scaled();

            var profile = ProfileService.Current;

            DrawTopBar(profile);
            DrawTimerBox();
            DrawCombo();
            DrawPortalPrompt();
            DrawSkillSlots();
            DrawHints();
        }

        /// <summary>지금 캐릭터 키트가 내주는 슬롯 상태. 구현 안 한 키트면 null(슬롯엔 아이콘만).</summary>
        ISkillSlotSource SlotSource => rig != null ? rig.CurrentKit as ISkillSlotSource : null;

        /// <summary>원본 `#topbar`(`:30`) — 체력·경험치 바와 그 아래 정보줄.</summary>
        void DrawTopBar(PlayerProfile profile)
        {
            float y = TopY;

            if (health != null)
            {
                float ratio = health.MaxHp > 0f ? health.CurrentHp / health.MaxHp : 0f;
                UiTheme.Bar(new Rect(LeftX, y, BarWidth, BarHeight), ratio, UiTheme.HpFill,
                            $"HP {Mathf.Ceil(health.CurrentHp)} / {Mathf.Ceil(health.MaxHp)}");
            }
            y += BarGap;

            long need = PlayerProfile.RequiredExp(profile.level);
            UiTheme.Bar(new Rect(LeftX, y, BarWidth, BarHeight),
                        need > 0 ? profile.exp / (float)need : 0f, UiTheme.ExpFill,
                        $"EXP {profile.exp} / {need}");
            y += BarGap;

            // 원본 `#infoline`(`:36`) — 수치만 금색(`b { color:#ffd76e }`).
            UiTheme.ShadowLabel(new Rect(LeftX, y, 520f, 20f),
                $"Lv.<b>{profile.level}</b>     골드 <b>{profile.gold:N0}</b> G", UiTheme.Hud);
            y += 22f;

            // 원본 `#buffs`(`:37`) — 지금 몸에 걸린 것들. 원본은 캐릭터별 자원(마나·충전 등)까지
            // 여기 붙이는데, 우리는 아직 전투 배수 체인만 있다.
            string buffs = BuffText();
            if (!string.IsNullOrEmpty(buffs))
                UiTheme.ShadowLabel(new Rect(LeftX, y, 700f, 20f), buffs, UiTheme.HudSmall, UiTheme.BuffLine);
        }

        /// <summary>
        /// 보스 포탈 안내 — **원본에 없다**(원본은 보스전을 로비에서 고른다).
        /// 포탈은 맵 우측 끝에 있어 화면 밖일 때가 많으므로, **범위에 들어왔을 때만** 알려준다.
        /// 항상 띄워두면 사냥 중 계속 시야를 먹는다.
        /// </summary>
        void DrawPortalPrompt()
        {
            if (RunState.Mode != RunMode.Normal) return;
            if (portal == null) portal = Object.FindFirstObjectByType<BossPortal>();
            if (portal == null || !portal.PlayerInRange) return;

            string text = portal.IsUnlocked
                ? "🌀 <b>F</b> — 보스 필드로 들어간다"
                : $"🔒 토벌 {ProfileService.Current.RegionKills(RunState.Region)} / {RunState.RegionKillTarget}"
                  + " — 아직 열리지 않았다";

            UiTheme.ShadowLabel(new Rect(0f, UiTheme.DesignHeight - 130f, UiTheme.DesignWidth, 24f),
                                text, Center(UiTheme.Hud),
                                portal.IsUnlocked ? UiTheme.GoldColor : UiTheme.StageLine);
        }

        /// <summary>원본 `buffTxt`(`:6288`~`:6318`) 중 우리가 가진 것들.</summary>
        string BuffText()
        {
            string s = "";

            // 원본 `:6288` — 성소가 살아 있으면 가장 먼저 경고한다.
            if (CombatModifiers.ShrineActive) s += "⚠ 몬스터들이 성소의 힘을 받는 중!   ";
            if (CombatModifiers.ShrineBuffLeft > 0f)
                s += $"⛩ 성소의 가호 {CombatModifiers.ShrineBuffLeft:0.0}초   ";

            s += $"살기 {RunState.Fury}   피해 ×{CombatModifiers.DamageMultiplier:0.00}";
            if (CombatModifiers.ChainKills >= CombatModifiers.ChainKillMin)
                s += $"   연쇄 ×{CombatModifiers.ChainKills}";

            // 원본 `:6290` — 권장 회귀에 모자란 지역이면 몹이 단단해지고 내 피해가 줄어든다.
            int gap = RegressionConfig.Gap(RunState.Region, ProfileService.Current.regressions);
            if (gap > 0) s += $"   ⚠ 회귀 부족 {gap}회";

            // 원본 `:6291`~`:6317` — 캐릭터별 몫(마법사 차지율, 메카닉 충전 스택·부품 …)은 키트가 내준다.
            string kit = SlotSource?.BuffText;
            if (!string.IsNullOrEmpty(kit)) s += "   " + kit;

            // 포탈이 열렸는지 — 열렸으면 우측 끝으로 가면 된다는 걸 알아야 한다(원본에 없는 안내).
            if (RunState.Mode == RunMode.Normal && ProfileService.Current.IsBossUnlocked(RunState.Region))
                s += "   🌀 보스 포탈 열림(우측 끝)";

            return s;
        }

        /// <summary>원본 `#timerbox`(`:39`) — 화면 위 가운데. 남은 시간이 가장 크게 온다.</summary>
        void DrawTimerBox()
        {
            int t = Mathf.CeilToInt(RunState.TimeLeft);
            UiTheme.ShadowLabel(new Rect(0f, 12f, UiTheme.DesignWidth, 46f), t.ToString(), UiTheme.Timer,
                                t <= LowTimeSeconds ? UiTheme.TimerLow : Color.white);

            string stage = $"{RunState.Region}지역 · {RegionConfig.NameOf(RunState.Region)}"
                           + (RunState.Mode == RunMode.Boss ? " — 보스전" : "");
            UiTheme.ShadowLabel(Centered(60f, 18f), stage, Center(UiTheme.HudSmall), UiTheme.StageLine);

            // 원본 `:6271` — 일반 사냥에서만 지역 토벌 진행도를 같이 보여준다(보스전은 진행도가 안 오른다).
            string kills = RunState.Mode == RunMode.Normal
                ? $"이번 사냥 {RunState.Kills}마리 · 토벌 " +
                  $"{ProfileService.Current.RegionKills(RunState.Region)}/{RunState.RegionKillTarget}"
                : $"이번 사냥 {RunState.Kills}마리";
            UiTheme.ShadowLabel(Centered(80f, 20f), kills, Center(UiTheme.Hud), UiTheme.KillLine);

            UiTheme.ShadowLabel(Centered(102f, 18f),
                $"+{RunState.GoldEarned:N0} G · +{RunState.ExpEarned:N0} EXP",
                Center(UiTheme.HudSmall), UiTheme.EarnLine);
        }

        /// <summary>
        /// 원본 `#combo`(`:52`) — 오른쪽 위에 크게. 콤보가 0이면 아예 안 보인다(`:1633`).
        /// 왼쪽 정보줄에 작게 끼워 넣으면 "쌓을수록 이득"이라는 신호가 안 온다.
        /// </summary>
        void DrawCombo()
        {
            int n = CombatModifiers.Combo;
            if (n <= 0) return;

            const float right = 28f;
            var numRect = new Rect(UiTheme.DesignWidth - right - 400f, 52f, 400f, 50f);
            UiTheme.ShadowLabel(numRect, n.ToString(), Right(ComboNumber()), UiTheme.ComboNum);

            // 원본 라벨은 `COMBO ×(1 + n * dmgPer)` — 콤보 수가 아니라 **지금 배수**를 보여준다.
            float mult = 1f + n * CombatModifiers.ComboDamagePer;
            UiTheme.ShadowLabel(new Rect(numRect.x, numRect.yMax - 2f, numRect.width, 22f),
                                $"COMBO ×{mult:0.00}", Right(UiTheme.Hud), UiTheme.GoldColor);
        }

        /// <summary>원본 `#hints`(`:74`) — 오른쪽 아래 조작 안내.</summary>
        void DrawHints() =>
            UiTheme.ShadowLabel(new Rect(UiTheme.DesignWidth - 16f - 600f, UiTheme.DesignHeight - 34f, 600f, 20f),
                                "ESC — 일시정지 / 로비로 나가기", Right(UiTheme.HudSmall),
                                new Color(0.66f, 0.60f, 0.47f)); // 원본 `#hints { color:#a89878 }`

        /// <summary>
        /// 원본 `#slots`(`:59`) — Z 기본공격 · X 전문화 스킬 · C 점프(`buildSlots` `:6225`~`:6229`).
        /// 점프 칸은 원본도 쿨다운을 안 건다.
        /// </summary>
        void DrawSkillSlots()
        {
            var src = SlotSource;
            DrawSlot(0, UiTheme.SkillSlotAttack, "Z", src != null ? src.AttackSlot : default);
            DrawSlot(1, UiTheme.SkillSlotSkill, "X", src != null ? src.SkillSlot : default);
            DrawSlot(2, UiTheme.SkillSlotJump, "C", default);
        }

        /// <summary>
        /// 슬롯 한 칸 — 그리는 순서가 원본 DOM 순서(`.icon` → `.cd` → `.cdnum` → `.key` → `.stk`)와 같아야
        /// 덮개가 아이콘 위에, 숫자가 덮개 위에 온다.
        /// </summary>
        static void DrawSlot(int index, Texture2D art, string key, SkillSlotState s)
        {
            var r = new Rect(SlotLeft + index * SlotStep, SlotTop, SlotSize, SlotSize);

            if (art != null) GUI.DrawTexture(r, art, ScaleMode.StretchToFill, alphaBlend: true);
            else DrawSlotFallback(r, key);

            // 원본 `.slot.cooling .icon { opacity:.34 }` · `.slot.noStack .icon { color:#8a8070 }` — 아이콘을 흐린다.
            if (s.Cooling || s.NoStack)
            {
                var dim = UiTheme.SlotFill;
                dim.a = IconDimVeil;
                FillSlotInner(r, 0f, dim);
            }

            // 원본 `.cd { height: frac% }` — 아래에서부터 차오르는 어두운 덮개.
            if (s.Fraction > 0f) FillSlotInner(r, 1f - s.Fraction, UiTheme.SlotCooldownVeil);

            if (s.Cooling)
                UiTheme.ShadowLabel(r, s.CooldownText, SlotNumberStyle(), UiTheme.SlotCooldownText);

            // 원본 `.key`는 칸 안 아래에 붙지만, Figma 칸은 안쪽이 전부 틀·아이콘 그림이라 칸 **바로 밑**에 둔다.
            UiTheme.ShadowLabel(new Rect(r.x, r.yMax + 1f, r.width, 16f), key, SlotKeyStyle(), UiTheme.SlotKeyText);

            // 원본 `.stk` — 오른쪽 위. 부품이 없으면(`.noStack`) 붉게.
            if (s.HasStack)
                UiTheme.ShadowLabel(new Rect(r.x, r.y + 5f, r.width - 7f, 16f), $"{s.Stacks}/{s.StackMax}",
                                    SlotStackStyle(), s.NoStack ? UiTheme.SlotNoStackText : UiTheme.SlotStackText);
        }

        /// <summary>
        /// 슬롯 그림의 안쪽 바탕(남색 테두리 안)을, 위에서 <paramref name="from01"/>(0~1) 지점부터 아래 끝까지 칠한다.
        /// 안쪽은 **네 귀퉁이가 한 도트씩 깎인** 사각형이라(32도트 그림 기준 x·y 4~27) 겹치지 않는 세 조각으로 나눠
        /// 칠한다 — 한 사각형으로 칠하면 남색 귀퉁이까지 덮이고, 겹쳐 칠하면 겹친 곳만 진해진다.
        /// </summary>
        static void FillSlotInner(Rect slot, float from01, Color c)
        {
            const float innerTop = 4f, innerHeight = 24f;
            float clip = innerTop + Mathf.Clamp01(from01) * innerHeight;
            FillDots(slot, 4f, 5f, 24f, 22f, clip, c); // 몸통(맨 윗줄·맨 아랫줄 제외)
            FillDots(slot, 5f, 4f, 22f, 1f, clip, c);  // 맨 윗줄(귀퉁이 제외)
            FillDots(slot, 5f, 27f, 22f, 1f, clip, c); // 맨 아랫줄(귀퉁이 제외)
        }

        /// <summary>도트 좌표(<paramref name="x"/>, <paramref name="y"/>, 크기)의 사각형 중 <paramref name="clipTop"/> 아래만 칠한다.</summary>
        static void FillDots(Rect slot, float x, float y, float w, float h, float clipTop, Color c)
        {
            float top = Mathf.Max(y, clipTop);
            float bottom = y + h;
            if (bottom <= top) return;
            float k = slot.width / SlotDots;
            UiTheme.FillRect(new Rect(slot.x + x * k, slot.y + top * k, w * k, (bottom - top) * k), c);
        }

        /// <summary>
        /// 슬롯 그림이 없을 때(씬에 안 꽂혔거나 테스트) — 같은 색의 틀을 도형으로 그리고 아이콘 자리에 키 글자를 둔다.
        /// 다른 Figma 화면과 같은 방침이다(<see cref="UiTheme.DrawTex"/>: 그림이 없어도 화면이 비지 않게).
        /// </summary>
        static void DrawSlotFallback(Rect r, string key)
        {
            float k = r.width / SlotDots;
            UiTheme.FillRect(r, UiTheme.SlotEdge);
            UiTheme.FillRect(Inset(r, 1f * k), UiTheme.SlotFrame);
            UiTheme.FillRect(Inset(r, 3f * k), UiTheme.SlotInset);
            UiTheme.FillRect(Inset(r, 4f * k), UiTheme.SlotFill);
            GUI.Label(r, key, SlotFallbackIconStyle());
        }

        static Rect Inset(Rect r, float d) => new Rect(r.x + d, r.y + d, r.width - 2f * d, r.height - 2f * d);

        static Rect Centered(float y, float h) => new Rect(0f, y, UiTheme.DesignWidth, h);

        // ── 정렬만 바꾼 사본 (원본 스타일을 그 자리에서 고치면 다른 화면까지 따라 바뀐다) ──
        static GUIStyle centerSmall, centerHud, rightHud, rightSmall, comboNum;
        static GUIStyle slotNumber, slotKey, slotStack, slotFallbackIcon;

        static GUIStyle Center(GUIStyle basis)
        {
            if (basis == UiTheme.HudSmall)
                return centerSmall ??= new GUIStyle(basis) { alignment = TextAnchor.UpperCenter };
            return centerHud ??= new GUIStyle(basis) { alignment = TextAnchor.UpperCenter };
        }

        static GUIStyle Right(GUIStyle basis)
        {
            if (basis == UiTheme.HudSmall)
                return rightSmall ??= new GUIStyle(basis) { alignment = TextAnchor.UpperRight };
            if (basis == UiTheme.Hud)
                return rightHud ??= new GUIStyle(basis) { alignment = TextAnchor.UpperRight };
            return basis; // 이미 정렬이 잡힌 스타일(콤보 숫자)은 그대로
        }

        // 슬롯 글자 — 원본 58px 칸 기준 크기를 64px 칸에 맞춰 조금 키웠다.
        /// <summary>쿨다운 숫자. 원본 `.slot .cdnum { font-size:17px; font-weight:900 }`(`:65`).</summary>
        static GUIStyle SlotNumberStyle() => slotNumber ??= new GUIStyle(UiTheme.Hud)
        {
            fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
        };

        /// <summary>키 글자. 원본 `.slot .key { font-size:10px }`(`:63`).</summary>
        static GUIStyle SlotKeyStyle() => slotKey ??= new GUIStyle(UiTheme.HudSmall)
        {
            fontSize = 11, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter,
        };

        /// <summary>스택 글자. 원본 `.slot .stk { font-size:11px; font-weight:bold }`(`:71`).</summary>
        static GUIStyle SlotStackStyle() => slotStack ??= new GUIStyle(UiTheme.HudSmall)
        {
            fontSize = 12, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperRight,
        };

        /// <summary>그림이 없을 때 아이콘 자리에 두는 큰 키 글자(Figma 아이콘 색 #0e071b).</summary>
        static GUIStyle SlotFallbackIconStyle()
        {
            if (slotFallbackIcon == null)
            {
                slotFallbackIcon = new GUIStyle(UiTheme.Hud)
                {
                    fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
                };
                slotFallbackIcon.normal.textColor = new Color(0x0e / 255f, 0x07 / 255f, 0x1b / 255f);
            }
            return slotFallbackIcon;
        }

        /// <summary>원본 `#combo .n { font-size:44px; font-style:italic }`(`:54`).</summary>
        static GUIStyle ComboNumber()
        {
            if (comboNum == null)
                comboNum = new GUIStyle(UiTheme.Hud)
                {
                    fontSize = 40,
                    fontStyle = FontStyle.BoldAndItalic,
                    alignment = TextAnchor.UpperRight,
                };
            return comboNum;
        }
    }
}
