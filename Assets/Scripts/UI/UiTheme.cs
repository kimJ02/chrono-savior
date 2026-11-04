using UnityEngine;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 화면 밖 UI(로비 · 결과 · HUD)의 색·판·글자 모양 — 원본 CSS를 그대로 옮긴 것이다.
    /// (`.btn` project_test.html:18, `#hud` `:29`~`:47`, `#lobbyHead` `:85`, `.tabbtn` `:178`,
    ///  `#lobbyBody` `:180`, `#result` `:223`~`:227`, `#toast` `:230`)
    ///
    /// ## 왜 필요한가
    /// 우리 UI는 전부 사냥터 **위에** 그대로 얹힌다. IMGUI 기본 스킨은 밝은 회색 판에 검은 글씨를
    /// 전제로 만들어져 있어서, 그 위에 얹으면 버튼이 반투명 회색으로 보이고 글씨가 몬스터·발판과
    /// 섞여 읽히지 않는다. 원본이 이 문제를 푸는 방식은 **자리에 따라 다른 두 가지**다:
    ///
    ///   1. **읽어야 할 덩어리는 판으로 덮는다** — 로비 본문 `rgba(14,11,20,.92)`(`:180`),
    ///      결과 화면은 화면 전체를 `rgba(5,4,10,.88)`로 덮는다(`:223`).
    ///   2. **덮을 수 없는 자리(헤더·HUD)는 글자에 그림자를 준다** — `text-shadow:0 1px 2px #000`.
    ///      HUD는 게임을 가리면 안 되니 판을 깔 수 없고, 그림자가 유일한 대비 수단이다.
    ///
    /// 자리마다 맞는 쪽을 써야 한다. HUD에 판을 깔면 화면을 가리고, 로비 본문에 그림자만 주면
    /// 긴 글이 배경 위에서 계속 흔들린다.
    ///
    /// ## 테두리를 8×8 텍스처로 만드는 이유
    /// CSS의 `border:1px`을 IMGUI로 옮기려면 9-슬라이스가 필요하다. 가장자리 1px만 테두리색인
    /// 작은 텍스처를 만들고 <see cref="GUIStyle.border"/>를 1로 두면, 판이 아무리 커져도 테두리는
    /// 1px로 유지되고 가운데만 늘어난다. 단색 텍스처를 통째로 늘리면 테두리가 같이 두꺼워진다.
    /// </summary>
    public static class UiTheme
    {
        /// <summary>원본 캔버스 크기(`CONFIG.canvas`). 이 좌표계로 그리고 화면에 맞춰 확대한다.</summary>
        public const float DesignWidth = 1280f;
        public const float DesignHeight = 720f;

        // ── 원본 CSS 색 ──────────────────────────────────────────
        static readonly Color PanelBg     = Hex(0x0e, 0x0b, 0x14, 0.92f); // #lobbyBody
        static readonly Color PanelBorder = Hex(0x6a, 0x4a, 0x3a);
        static readonly Color TabOffBg    = Hex(0x0e, 0x0b, 0x14, 0.85f); // .tabbtn
        static readonly Color TabOffBorder= Hex(0x4a, 0x3a, 0x3a);
        static readonly Color TabOffText  = Hex(0xb8, 0xa8, 0x88);
        static readonly Color TabOnBg     = Hex(0x1e, 0x16, 0x1e, 0.95f); // .tabbtn.on
        static readonly Color TabOnBorder = Hex(0xc9, 0xa2, 0x4a);
        static readonly Color TabOnText   = Hex(0xff, 0xe9, 0xc0);
        static readonly Color BtnBg       = Hex(0x3a, 0x25, 0x20);        // .btn
        static readonly Color BtnBorder   = Hex(0x8a, 0x5a, 0x3a);
        static readonly Color BtnHoverBg  = Hex(0x5a, 0x35, 0x25);        // .btn:hover
        static readonly Color BtnHoverBd  = Hex(0xc9, 0xa2, 0x4a);
        static readonly Color ToastBg     = Hex(0x1e, 0x14, 0x0a, 0.95f); // #toast
        static readonly Color ToastBorder = Hex(0xc9, 0xa2, 0x4a);
        static readonly Color RowsBg      = Hex(0x00, 0x00, 0x00, 0.40f); // #resultRows
        static readonly Color RowsBorder  = Hex(0x4a, 0x3a, 0x3a);

        /// <summary>결과·일시정지 화면이 화면 전체를 덮는 색. 원본 `#result`(`:223`).</summary>
        public static readonly Color Scrim = Hex(0x05, 0x04, 0x0a, 0.88f);

        /// <summary>본문 글자색. 원본 `#lobbyHead .stat`(`:87`).</summary>
        public static readonly Color TextColor  = Hex(0xc8, 0xb8, 0xa0);
        /// <summary>버튼 글자색. 원본 `.btn`(`:19`).</summary>
        public static readonly Color BtnText    = Hex(0xff, 0xd8, 0xa0);
        /// <summary>강조 수치(골드·SP 등). 원본 `.goldc`(`:26`).</summary>
        public static readonly Color GoldColor  = Hex(0xff, 0xd7, 0x6e);
        /// <summary>제목. 원본 `#lobbyHead h1`(`:86`) · `#result h1`(`:224`).</summary>
        public static readonly Color TitleColor = Hex(0xe8, 0xd0, 0xa0);
        /// <summary>쓰러졌을 때의 결과 제목. 원본 `#result h1.dead`(`:225`).</summary>
        public static readonly Color DeadColor  = Hex(0xff, 0x6a, 0x5a);
        /// <summary>격파했을 때의 결과 제목. 원본 `#result h1.clear`(`:226`).</summary>
        public static readonly Color ClearColor = Hex(0x8e, 0xf0, 0xc8);
        /// <summary>토스트 글자색. 원본 `#toast`(`:230`).</summary>
        public static readonly Color ToastText  = Hex(0xff, 0xe9, 0xb0);

        // HUD 색 — 원본 `:35`~`:47`
        /// <summary>체력 바. 원본 `#hpfill` 그라데이션의 중간값(`:32`).</summary>
        public static readonly Color HpFill    = Hex(0xd9, 0x38, 0x30);
        /// <summary>경험치 바. 원본 `#expfill` 그라데이션의 중간값(`:33`).</summary>
        public static readonly Color ExpFill   = Hex(0xe4, 0xb2, 0x44);
        /// <summary>지역 줄. 원본 `#stageline`(`:43`).</summary>
        public static readonly Color StageLine = Hex(0xc8, 0xb8, 0x90);
        /// <summary>처치 줄. 원본 `#killline`(`:44`).</summary>
        public static readonly Color KillLine  = Hex(0x9a, 0xd0, 0xff);
        /// <summary>획득 줄. 원본 `#earnline`(`:45`).</summary>
        public static readonly Color EarnLine  = GoldColor;
        /// <summary>버프 줄. 원본 `#buffs`(`:37`).</summary>
        public static readonly Color BuffLine  = Hex(0x8e, 0xf0, 0xc8);
        /// <summary>콤보 숫자. 원본 `#combo .n`(`:54`).</summary>
        public static readonly Color ComboNum  = Hex(0xff, 0xb3, 0x40);
        /// <summary>시간이 얼마 안 남았을 때의 타이머. 원본 `#timer.low`(`:41`).</summary>
        public static readonly Color TimerLow  = DeadColor;

        // ── 스타일 ──────────────────────────────────────────────
        public static GUISkin Skin { get; private set; }
        /// <summary>로비 본문을 덮는 판(`#lobbyBody`).</summary>
        public static GUIStyle Panel { get; private set; }
        /// <summary>결과 화면의 수치 판(`#resultRows`).</summary>
        public static GUIStyle Rows { get; private set; }
        /// <summary>탭 하나(`.tabbtn` / `.tabbtn.on`). 선택 상태는 `onNormal`이 맡는다.</summary>
        public static GUIStyle Tab { get; private set; }
        /// <summary>로비 제목(`#lobbyHead h1`).</summary>
        public static GUIStyle Title { get; private set; }
        /// <summary>결과 화면 제목(`#result h1`, 52px).</summary>
        public static GUIStyle BigTitle { get; private set; }
        /// <summary>헤더 통계줄(`#lobbyHead .stat`).</summary>
        public static GUIStyle HeadStat { get; private set; }
        /// <summary>HUD 기본 글자(`#infoline`, 14px).</summary>
        public static GUIStyle Hud { get; private set; }
        /// <summary>HUD 작은 글자(`#stageline`·`#earnline`, 13px 이하).</summary>
        public static GUIStyle HudSmall { get; private set; }
        /// <summary>가운데 타이머(`#timer`, 38px).</summary>
        public static GUIStyle Timer { get; private set; }
        /// <summary>바 안의 글자(`.bar .txt`, 12px 굵게 흰색).</summary>
        public static GUIStyle BarText { get; private set; }
        /// <summary>토스트 판(`#toast`).</summary>
        public static GUIStyle Toast { get; private set; }

        // ══════════════ Figma 디자인 (2026-09-16) ══════════════
        //
        // 원본 HTML CSS와 별개로, 디자이너가 Figma에 그린 화면이 따로 있다
        // (파일 9EpUO2k4i9en6cWAATGVa4 — "타이틀" 35:2, "시간 회귀창" 31:4).
        // 값은 렌더에서 직접 뽑은 것이라 추정이 아니다.
        //
        // **해상도 관계가 중요하다**: 회귀창은 640×360으로 그려졌고 우리 설계 좌표는 1280×720이라
        // 정확히 **2배**다 — 픽셀아트가 정수배로 확대되어 도트가 안 깨진다. 타이틀은 1920×1080이라
        // 1280×720의 **1.5배**이므로 좌표를 1.5로 나눠 옮긴다(TitleScreen 참고).

        /// <summary>회귀창을 덮는 전체 화면 막. Figma "ESC 메뉴 어두운 배경"(157:12).</summary>
        public static readonly Color FigmaScrim = Hex(0x1e, 0x1e, 0x1e, 0.86f);
        /// <summary>회귀창 본체 회색. Figma "창 프레임"(162:2) — 그림이 없을 때의 대체색.</summary>
        public static readonly Color WindowGray = Hex(0x85, 0x85, 0x85);
        /// <summary>회귀창 제목 아래 구분선. 그림(window_frame)에 포함돼 있어 보통은 안 쓴다.</summary>
        public static readonly Color WindowDivider = Hex(0x0e, 0x07, 0x1b);

        /// <summary>타이틀 오른쪽 양피지 패널. Figma "오른쪽 메뉴 패널"(52:100).</summary>
        public static readonly Color Parchment = Hex(0xc4, 0xa8, 0x73);
        /// <summary>타이틀 메뉴 버튼·로고 박스 금색. Figma(63:3 · 35:18).</summary>
        public static readonly Color PlateGold = Hex(0xd2, 0xa0, 0x45);
        /// <summary>메뉴 버튼 테두리 갈색. Figma(63:3 테두리).</summary>
        public static readonly Color PlateEdge = Hex(0x63, 0x49, 0x20);
        /// <summary>타이틀 메뉴 위·아래 구분선. Figma(63:4 · 63:5).</summary>
        public static readonly Color TitleDivider = Color.black;

        // ── 전투 HUD 스킬 슬롯 — 틀·아이콘은 Figma 그림(181:2~181:4), 쿨다운 표시는 원본 CSS(`.slot` :60~:73) ──
        /// <summary>슬롯 안쪽 바탕(푸른 회색). Figma 슬롯 그림에서 뽑은 값 — 아이콘을 흐리게 할 때 이 색을 덮는다.</summary>
        public static readonly Color SlotFill  = Hex(0x92, 0xa1, 0xb9);
        /// <summary>슬롯 안쪽 테두리(남색). Figma 슬롯 그림.</summary>
        public static readonly Color SlotInset = Hex(0x03, 0x19, 0x3f);
        /// <summary>슬롯 틀(밝은 회색). Figma 슬롯 그림.</summary>
        public static readonly Color SlotFrame = Hex(0xb4, 0xb4, 0xb4);
        /// <summary>슬롯 바깥 윤곽(어두운 회색). Figma 슬롯 그림.</summary>
        public static readonly Color SlotEdge  = Hex(0x3d, 0x3d, 0x3d);
        /// <summary>쿨다운 덮개. 원본 `.slot .cd { background:rgba(0,0,10,.75) }`(`:64`).</summary>
        public static readonly Color SlotCooldownVeil = Hex(0x00, 0x00, 0x0a, 0.75f);
        /// <summary>쿨다운 숫자. 원본 `.slot .cdnum { color:#fff0b8 }`(`:65`).</summary>
        public static readonly Color SlotCooldownText = Hex(0xff, 0xf0, 0xb8);
        /// <summary>키 글자(Z·X·C). 원본 `.slot .key { color:#c8b890 }`(`:63`).</summary>
        public static readonly Color SlotKeyText = Hex(0xc8, 0xb8, 0x90);
        /// <summary>스택 글자. 원본 `.slot .stk { color:#8ef0c8 }`(`:71`).</summary>
        public static readonly Color SlotStackText = Hex(0x8e, 0xf0, 0xc8);
        /// <summary>쓸 자원이 없을 때의 스택 글자. 원본 `.slot.noStack .stk { color:#ff8a7a }`(`:73`).</summary>
        public static readonly Color SlotNoStackText = Hex(0xff, 0x8a, 0x7a);

        /// <summary>타이틀 메뉴 글자(굵은 검정 — 양피지 위라 흰색은 안 읽힌다).</summary>
        public static GUIStyle MenuLabel { get; private set; }
        /// <summary>회귀창 통계 라벨(작은 흰 글씨).</summary>
        public static GUIStyle StatLabel { get; private set; }
        /// <summary>회귀창 통계 값(큰 흰 글씨).</summary>
        public static GUIStyle StatValue { get; private set; }

        static UiTextures textures;

        /// <summary>
        /// 씬의 <see cref="UiTextures"/>가 `Awake`에서 자기를 넘긴다. 정적 클래스는 에셋 참조를
        /// 직렬화할 수 없어서 이 우회가 필요하다(그쪽 클래스 주석 참고).
        /// </summary>
        public static void SetTextures(UiTextures t) => textures = t;

        public static Texture2D WindowFrame => textures != null ? textures.windowFrame : null;
        public static Texture2D IconFlag => textures != null ? textures.iconFlag : null;
        public static Texture2D IconStar => textures != null ? textures.iconStar : null;
        public static Texture2D IconSkull => textures != null ? textures.iconSkull : null;
        public static Texture2D IconShard => textures != null ? textures.iconShard : null;
        public static Texture2D MenuPlate => textures != null ? textures.menuPlate : null;
        public static Texture2D SkillSlotAttack => textures != null ? textures.skillSlotAttack : null;
        public static Texture2D SkillSlotSkill => textures != null ? textures.skillSlotSkill : null;
        public static Texture2D SkillSlotJump => textures != null ? textures.skillSlotJump : null;

        static Texture2D barBg, white;

        /// <summary>
        /// 첫 <c>OnGUI</c>에서 한 번만 만든다. `GUI.skin`을 복제해야 해서 OnGUI 밖에서는 부를 수 없다.
        /// </summary>
        public static void Ensure()
        {
            if (Skin != null) return;

            Panel = Boxed(PanelBg, PanelBorder);
            Panel.padding = new RectOffset(18, 18, 14, 14); // 원본 `padding:14px 18px`

            Rows = Boxed(RowsBg, RowsBorder);
            Rows.padding = new RectOffset(40, 40, 16, 16);  // 원본 `#resultRows { padding:16px 40px }`

            var button = Boxed(BtnBg, BtnBorder);
            button.padding = new RectOffset(14, 14, 6, 6);  // 원본 `.btn { padding:6px 14px }`
            button.fontSize = 13;
            button.alignment = TextAnchor.MiddleCenter;
            button.wordWrap = false;
            Paint(button.normal, BtnBg, BtnBorder, BtnText);
            Paint(button.hover, BtnHoverBg, BtnHoverBd, BtnText);
            Paint(button.active, BtnHoverBg, BtnHoverBd, BtnText);
            Paint(button.focused, BtnBg, BtnBorder, BtnText);

            Tab = Boxed(TabOffBg, TabOffBorder);
            Tab.padding = new RectOffset(20, 20, 8, 8);      // 원본 `.tabbtn { padding:8px 20px }`
            Tab.fontSize = 15;
            Tab.alignment = TextAnchor.MiddleCenter;
            Paint(Tab.normal, TabOffBg, TabOffBorder, TabOffText);
            Paint(Tab.hover, TabOffBg, TabOnBorder, TabOnText);
            // 선택된 탭 — IMGUI에서 Toggle/Toolbar의 "켜짐"은 on* 상태가 그린다.
            Paint(Tab.onNormal, TabOnBg, TabOnBorder, TabOnText);
            Paint(Tab.onHover, TabOnBg, TabOnBorder, TabOnText);
            Paint(Tab.onActive, TabOnBg, TabOnBorder, TabOnText);

            // 칸보다 큰 글자는 잘리지 않고 넘쳐 그린다 — 기본(GUI.skin.label)은 칸 밖을 잘라서, 칸이 글자보다 낮은 곳
            // (회귀 결과 창 통계 이름·HUD 줄)에서 **받침이 잘려 글자가 깨져 보였다**(2026-10-24 사용자 지적).
            var label = new GUIStyle(GUI.skin.label)
            { richText = true, fontSize = 14, wordWrap = true, clipping = TextClipping.Overflow };
            label.normal.textColor = TextColor;
            label.hover.textColor = TextColor;

            Title = new GUIStyle(label) { fontSize = 28, fontStyle = FontStyle.Bold, wordWrap = false };
            Title.normal.textColor = TitleColor;

            // 원본 `#result h1 { font-size:52px }`. 그림자로만 대비를 만드는 자리라 굵게 간다.
            BigTitle = new GUIStyle(label)
            {
                fontSize = 44,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
            };
            BigTitle.normal.textColor = TitleColor;

            HeadStat = new GUIStyle(label) { fontSize = 15, wordWrap = false };

            Hud = new GUIStyle(label) { fontSize = 14, wordWrap = false };
            HudSmall = new GUIStyle(label) { fontSize = 13, wordWrap = false };

            Timer = new GUIStyle(label)
            {
                fontSize = 38,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
            };
            Timer.normal.textColor = Color.white;

            BarText = new GUIStyle(label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
            };
            BarText.normal.textColor = Color.white;

            Toast = Boxed(ToastBg, ToastBorder);
            Toast.padding = new RectOffset(26, 26, 10, 10);
            Toast.fontSize = 16;
            Toast.alignment = TextAnchor.MiddleCenter;
            Toast.normal.textColor = ToastText;

            // ── Figma 디자인용 스타일 ──
            MenuLabel = new GUIStyle(label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
                alignment = TextAnchor.MiddleCenter,
            };
            MenuLabel.normal.textColor = Color.black; // 양피지 위라 검정이 읽힌다

            StatLabel = new GUIStyle(label) { fontSize = 13, wordWrap = false };
            StatLabel.normal.textColor = Color.white;

            StatValue = new GUIStyle(label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                wordWrap = false,
            };
            StatValue.normal.textColor = Color.white;

            // 원본 `.bar { background:rgba(0,0,0,.55); border:1px solid rgba(255,255,255,.25) }`(`:31`).
            barBg = BoxTexture(Hex(0x00, 0x00, 0x00, 0.55f), new Color(1f, 1f, 1f, 0.25f));
            white = Solid(Color.white);

            Skin = Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            Skin.label = label;
            Skin.button = button;
            Skin.box = Panel;
            Skin.toggle = button;
        }

        /// <summary>
        /// 원본 `fitScreen()`(project_test.html:7176)과 같은 방식 — 1280×720으로 그리고 화면에 맞춰
        /// 통째로 확대한다. 이걸 안 하면 해상도마다 글자 크기와 여백 비율이 달라져서, 원본 CSS
        /// 픽셀값을 옮겨온 게 의미를 잃는다.
        /// </summary>
        /// <example><code>using (UiTheme.Scaled()) { /* 1280×720 좌표로 그린다 */ }</code></example>
        public static ScaledGui Scaled() => Scaled(DesignWidth, DesignHeight);

        /// <summary>
        /// 설계 해상도를 지정하는 형태. **Figma 화면마다 그려진 해상도가 달라서** 필요하다 —
        /// 타이틀은 1920×1080, 시간 회귀창은 640×360에 그려져 있다.
        ///
        /// 화면별 좌표계를 그대로 쓰면 **Figma 좌표를 나눗셈 없이 그대로 옮길 수 있다.** 억지로
        /// 1280×720에 맞추면 1.5로 나눈 소수 좌표가 곳곳에 박히고, 그 반올림 오차가 픽셀아트에서
        /// 1px씩 어긋나 보인다.
        /// </summary>
        public static ScaledGui Scaled(float designWidth, float designHeight)
        {
            Ensure();
            var saved = new ScaledGui(GUI.matrix, GUI.skin, CurrentWidth, CurrentHeight);

            float scale = Mathf.Min(Screen.width / designWidth, Screen.height / designHeight);
            GUI.matrix = Matrix4x4.TRS(
                new Vector3((Screen.width - designWidth * scale) * 0.5f,
                            (Screen.height - designHeight * scale) * 0.5f, 0f),
                Quaternion.identity, new Vector3(scale, scale, 1f));
            GUI.skin = Skin;

            CurrentWidth = designWidth;
            CurrentHeight = designHeight;
            return saved;
        }

        /// <summary>지금 `Scaled()` 블록이 쓰는 설계 좌표계 크기. 화면 전체를 덮을 때 이 값을 쓴다.</summary>
        public static float CurrentWidth { get; private set; } = DesignWidth;
        public static float CurrentHeight { get; private set; } = DesignHeight;

        /// <summary>`using`이 끝날 때 원래 행렬·스킨·설계 크기로 되돌린다 — 다른 OnGUI에 영향이 안 가게.</summary>
        public readonly struct ScaledGui : System.IDisposable
        {
            readonly Matrix4x4 matrix;
            readonly GUISkin skin;
            readonly float w, h;
            public ScaledGui(Matrix4x4 m, GUISkin s, float width, float height)
            { matrix = m; skin = s; w = width; h = height; }
            public void Dispose()
            {
                GUI.matrix = matrix;
                GUI.skin = skin;
                CurrentWidth = w;
                CurrentHeight = h;
            }
        }

        // ══════════════ Figma 화면용 그리기 도구 ══════════════

        /// <summary>단색 사각형. 판·구분선처럼 그림이 필요 없는 요소에 쓴다.</summary>
        public static void FillRect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, white);
            GUI.color = prev;
        }

        /// <summary>
        /// 텍스처를 사각형에 그린다. 그림이 없으면 <paramref name="fallback"/> 색으로 채운다 —
        /// 아트가 아직 없는 자리(타이틀 배경·로고)가 실제로 그 상태라 화면이 비지 않게 한다.
        /// </summary>
        public static void DrawTex(Rect r, Texture2D tex, Color fallback)
        {
            if (tex != null) GUI.DrawTexture(r, tex, ScaleMode.StretchToFill, alphaBlend: true);
            else FillRect(r, fallback);
        }

        /// <summary>
        /// 아이콘을 **원본 비율 그대로** 그린다. Figma 아이콘은 32×32 도트를 64×64로(정수 2배)
        /// 쓰도록 그려져 있어, 비율을 깨면 도트가 일그러진다.
        /// </summary>
        public static void DrawIcon(Rect box, Texture2D tex)
        {
            if (tex == null) return;
            GUI.DrawTexture(box, tex, ScaleMode.ScaleToFit, alphaBlend: true);
        }

        /// <summary>
        /// 타이틀 메뉴 버튼. Figma의 256×64 플레이트 그림을 배경으로 쓰고 글자를 얹는다.
        /// 그림이 없으면 금색 사각형 + 갈색 테두리로 대체한다(색은 렌더에서 뽑은 값).
        /// </summary>
        public static bool PlateButton(Rect r, string text, GUIStyle labelStyle = null)
        {
            if (MenuPlate != null)
            {
                GUI.DrawTexture(r, MenuPlate, ScaleMode.StretchToFill, alphaBlend: true);
            }
            else
            {
                FillRect(r, PlateGold);
                FillRect(new Rect(r.x, r.y, r.width, 2f), PlateEdge);
                FillRect(new Rect(r.x, r.yMax - 2f, r.width, 2f), PlateEdge);
            }

            GUI.Label(r, text, labelStyle ?? MenuLabel);
            // 그림을 배경으로 쓰므로 버튼 자체는 **투명**이어야 한다 — 기본 버튼 배경이 그려지면
            // 플레이트를 덮는다. GUIStyle.none이 그 역할이다.
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>원본 `.bar`(`:31`) — 바탕 위에 비율만큼 채운다. `text`가 있으면 가운데에 얹는다.</summary>
        public static void Bar(Rect r, float ratio, Color fill, string text = null)
        {
            GUI.DrawTexture(r, barBg);

            float w = Mathf.Clamp01(ratio) * (r.width - 2f);
            if (w > 0f)
            {
                Color prev = GUI.color;
                GUI.color = fill;
                GUI.DrawTexture(new Rect(r.x + 1f, r.y + 1f, w, r.height - 2f), white);
                GUI.color = prev;
            }

            if (!string.IsNullOrEmpty(text)) ShadowLabel(r, text, BarText);
        }

        /// <summary>
        /// 화면 전체를 덮는 어두운 막. 원본 `#result`(`:223`).
        ///
        /// **디자인 좌표가 아니라 실제 화면 좌표로 그린다.** 1280×720 비율이 아닌 화면에서는
        /// 확대된 디자인 영역 바깥에 레터박스가 남는데, 거기까지 안 덮으면 화면 위아래(또는 좌우)로
        /// 사냥터가 그대로 비쳐서 "화면을 덮는다"는 의도가 깨진다.
        /// </summary>
        public static void FullScreenScrim() => FullScreenFill(Scrim);

        /// <summary>
        /// 화면 **전체**를 한 색으로 덮는다.
        ///
        /// ⚠️ **설계 좌표가 아니라 실제 화면 좌표로 그린다.** `Scaled()`는 설계 비율(16:9)을 유지하려고
        /// letterbox를 남기는데, 설계 좌표로 사각형을 채우면 그 letterbox 띠에 **사냥터가 그대로 비친다**
        /// (플레이어·몹·발판이 타이틀 화면 가장자리에 보이는 증상). 화면을 덮는 게 목적인 배경은
        /// 반드시 이 함수를 쓸 것.
        /// </summary>
        public static void FullScreenFill(Color c)
        {
            Matrix4x4 prevMatrix = GUI.matrix;
            Color prevColor = GUI.color;

            GUI.matrix = Matrix4x4.identity;
            GUI.color = c;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), white);

            GUI.color = prevColor;
            GUI.matrix = prevMatrix;
        }

        /// <summary>
        /// 원본 `text-shadow:0 1px 2px #000`. **판으로 덮을 수 없는 자리(HUD·헤더·제목)에만** 쓴다 —
        /// 사냥터 위에 그대로 얹히기 때문에 그림자 말고는 대비를 만들 방법이 없다.
        /// </summary>
        public static void ShadowLabel(Rect r, string text, GUIStyle style) =>
            ShadowLabel(r, text, style, style.normal.textColor);

        /// <summary>색을 그 자리에서 정해야 할 때(타이머의 위험 색 등).</summary>
        public static void ShadowLabel(Rect r, string text, GUIStyle style, Color color)
        {
            Color prev = style.normal.textColor;

            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            GUI.Label(new Rect(r.x, r.y + ShadowOffset, r.width, r.height), text, style);

            style.normal.textColor = color;
            GUI.Label(r, text, style);

            style.normal.textColor = prev;
        }

        /// <summary>
        /// 그림자를 띄울 거리(설계 단위). **화면에서 최소 1픽셀**이 되도록 역산한다.
        ///
        /// 원본 CSS는 `0 1px`이지만, 우리는 설계 좌표를 `GUI.matrix`로 확대/축소해 그린다.
        /// Game 뷰가 설계 폭(예: 1280)보다 작으면 1 설계픽셀이 **1 화면픽셀 미만**이 되어,
        /// 그림자와 본문이 거의 같은 자리에 찍히고 두 겹이 뭉개져 **글자가 번져 보인다**
        /// (사용자가 "글씨가 깨진다"고 지적한 증상).
        ///
        /// 확대 중일 때(스케일 ≥ 1)는 원본대로 1을 쓴다 — 그 이상 띄우면 그림자가 따로 논다.
        /// </summary>
        static float ShadowOffset
        {
            get
            {
                float scale = Screen.width / Mathf.Max(1f, CurrentWidth);
                return scale >= 1f ? 1f : 1f / scale;
            }
        }

        // ── 만들기 ──────────────────────────────────────────────

        static GUIStyle Boxed(Color fill, Color border)
        {
            var s = new GUIStyle
            {
                border = new RectOffset(1, 1, 1, 1), // 9-슬라이스: 가장자리 1px은 안 늘어난다
                richText = true,
                wordWrap = false,
            };
            Paint(s.normal, fill, border, TextColor);
            return s;
        }

        static void Paint(GUIStyleState state, Color fill, Color border, Color text)
        {
            state.background = BoxTexture(fill, border);
            state.textColor = text;
        }

        /// <summary>가장자리 1px만 테두리색인 8×8 텍스처. `border = 1`과 짝을 이룬다.</summary>
        static Texture2D BoxTexture(Color fill, Color border)
        {
            const int n = 8;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    px[y * n + x] = (x == 0 || y == 0 || x == n - 1 || y == n - 1) ? border : fill;
            tex.SetPixels(px);
            tex.filterMode = FilterMode.Point; // 흐려지면 1px 테두리가 뭉갠다
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.Apply();
            return tex;
        }

        static Texture2D Solid(Color c)
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            tex.SetPixel(0, 0, c);
            tex.Apply();
            return tex;
        }

        static Color Hex(int r, int g, int b, float a = 1f) => new Color(r / 255f, g / 255f, b / 255f, a);
    }
}
