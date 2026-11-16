using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 설정 창 — 2026-11-16 Figma "설정창"(414:2) 새 판(팀원 zip + Figma 줄 아이콘): 창 틀 512×304 · 제목판 "설정" · X 닫기(40×40) ·
    /// 줄(설정바 480×32: 아이콘 24 + 이름 + ◀ 값 ▶ 또는 음량 막대 + 숫자칸) · 나눔선 · "적용" 버튼(144×32).
    /// 항목은 예전 넷 그대로 — 화면 모드 · 해상도 · 배경음 · 효과음(Figma의 마스터 볼륨은 게임에 없어 뺐다, Codex 2026-11-16).
    /// 값은 바꾸는 즉시 적용(예전과 같다) — "적용"은 창을 닫는다. 음량은 0~10 그대로 저장하고 0~100으로 보인다(×10), 막대는 눌러 끌 수 있다.
    /// 조작키 안내는 새 디자인에 자리가 없어 음량 줄 아래에 작게 남겼다. 닫기는 X·적용·ESC. 키보드: ↑/↓ 줄 고르기, ←/→ 값 바꾸기.
    /// </summary>
    public class SettingsWindow : PxScreen
    {
        static SettingsWindow instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;

        public static void Open()
        {
            if (instance != null) instance.open = true;
        }

        public static void Close()
        {
            if (instance != null) instance.open = false;
        }

        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Popup;
        protected override bool Modal => true;
        protected override bool WantsVisible => open;
        protected override bool TakesKeys => true;

        // Figma 좌표(어두운 배경 -314·-174를 원점으로 뺀 값)
        static readonly Rect Frame = new Rect(65f, 29f, 512f, 304f);     // 416:23 "설정 배경 1"
        static readonly Rect Plate = new Rect(250f, 7f, 128f, 32f);      // 416:32 "설정"(제목판)
        static readonly Rect TitleBox = new Rect(262f, 4f, 103f, 37f);   // 416:33 글자
        static readonly Rect CloseBox = new Rect(534f, 17f, 40f, 40f);   // 588:12 "나가기_버튼 1" + 416:35 "x 1"
        static readonly Rect ApplyBox = new Rect(250f, 283f, 144f, 32f); // 417:42 "적용버튼 1"
        static readonly Rect Divider = new Rect(83f, 149f, 475f, 3f);    // 416:31 "Rectangle 16"
        const float RowX = 81f;                                          // 설정바 왼쪽
        static readonly float[] RowY = { 72f, 110f, 159f, 197f };        // 화면 모드(420:93) · 해상도(420:89) · 배경음(마스터 419:58 자리) · 효과음(bgm 419:57 자리)
        // 줄 안(설정바 왼쪽 위 기준)
        const float IconX = 6f, IconY = 4f, LabelX = 36f, LabelW = 96f;
        const float LeftArrowX = 209f, RightArrowX = 442f, ArrowY = 4f, ArrowSize = 24f, GlyphInset = 4f, GlyphSize = 16f;
        const float ValueBoxX = 240f, ValueBoxY = 6f, ValueBoxW = 192f, ValueBoxH = 20f;
        const float BarX = 209f, BarY = 8f, BarW = 208f, BarH = 16f, KnobW = 20f, KnobH = 24f, KnobY = 4f;
        const float NumberX = 436f, NumberY = 4f, NumberW = 36f, NumberH = 24f;
        static readonly Color Selected = Px.Hex(0xffe066);
        static readonly Color Line = Px.Hex(0xc7cfdd);

        enum Row { Screen, Resolution, Bgm, Sfx }
        static readonly string[] RowNames = { "화면 모드", "해상도", "배경음", "효과음" };
        static readonly string[] RowIcons = { "Settings/icon_screen", "Settings/icon_resolution", "Settings/icon_bgm", "Settings/icon_sfx" };
        readonly Text[] labels = new Text[4];
        readonly Text[] values = new Text[4];
        readonly Image[] fills = new Image[2];
        readonly RectTransform[] knobs = new RectTransform[2];
        int row;

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
            Px.FullFill(Back, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, 0.35f));
            Px.Image(Content, "Frame", PxArt.Get("Settings/window"), Frame.x, Frame.y, Frame.width, Frame.height)
              .raycastTarget = true; // 창 뒤(일시정지 버튼)가 눌리지 않게
            Px.Image(Content, "TitlePlate", PxArt.Get("Common/title_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            Px.Text(Content, "Title", "설정", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 20, Color.white);
            Px.IconButton(Content, "Close", PxArt.Get("Common/close_frame"), PxArt.Get("Icons/close_x"),
                          CloseBox.x, CloseBox.y, CloseBox.width, CloseBox.height, 0f, 0f, CloseBox.width, CloseBox.height, Close);

            for (int i = 0; i < 4; i++)
            {
                int r = i;
                float y = RowY[i];
                Px.Image(Content, "Row" + i, PxArt.Get("Settings/row"), RowX, y, 480f, 32f);
                Px.Image(Content, "RowIcon" + i, PxArt.Get(RowIcons[i]), RowX + IconX, y + IconY, 24f, 24f);
                labels[i] = Px.Text(Content, "Label" + i, RowNames[i], RowX + LabelX, y, LabelW, 32f, 13, Color.white);
                if (i < 2)
                {
                    ArrowButton(RowX + LeftArrowX, y + ArrowY, false, () => { row = r; Change(-1); });
                    Px.Image(Content, "ValueBox" + i, PxArt.Get("Settings/value_box"), RowX + ValueBoxX, y + ValueBoxY, ValueBoxW, ValueBoxH);
                    values[i] = Px.Text(Content, "Value" + i, "", RowX + ValueBoxX, y + ValueBoxY, ValueBoxW, ValueBoxH, 12, Color.white);
                    ArrowButton(RowX + RightArrowX, y + ArrowY, true, () => { row = r; Change(+1); });
                }
                else
                {
                    int v = i - 2;
                    Px.Image(Content, "VolumeBack" + v, PxArt.Get("Settings/volume_back"), RowX + BarX, y + BarY, BarW, BarH);
                    fills[v] = Px.Image(Content, "VolumeFill" + v, PxArt.Get("Settings/volume_fill"), RowX + BarX, y + BarY, BarW, BarH);
                    fills[v].type = Image.Type.Filled;
                    fills[v].fillMethod = Image.FillMethod.Horizontal;
                    knobs[v] = (RectTransform)Px.Image(Content, "VolumeKnob" + v, PxArt.Get("Settings/volume_knob"), RowX + BarX, y + KnobY, KnobW, KnobH).transform;
                    // 눌러 끄는 칸 — 손잡이가 넘나드는 자리까지(막대 위아래 4도트)
                    var hit = Px.Fill(Content, "VolumeHit" + v, new Color(0f, 0f, 0f, 0f), RowX + BarX, y + KnobY, BarW, KnobH);
                    hit.raycastTarget = true;
                    var slider = hit.gameObject.AddComponent<PxSlider>();
                    slider.Steps = GameSettings.VolumeMax;
                    slider.OnChange = value => { row = r; SetVolume((Row)r, value); };
                    Px.Image(Content, "NumberBox" + v, PxArt.Get("Settings/number_box"), RowX + NumberX, y + NumberY, NumberW, NumberH);
                    values[i] = Px.Text(Content, "Value" + i, "", RowX + NumberX, y + NumberY, NumberW, NumberH, 12, Color.white);
                }
            }
            Px.Fill(Content, "Divider", Line, Divider.x, Divider.y, Divider.width, Divider.height);

            // 조작키 안내 — 새 Figma엔 자리가 없다(Figma 효과음 볼륨 줄 자리)
            Px.Text(Content, "Keys",
                    "←→ 이동 · C 점프 · ↓ 아래로 내려가기 · Z 공격 · X 스킬\nF 포탈 들어가기 · ESC 일시정지 · Enter/Z 메뉴 고르기",
                    RowX, 237f, 480f, 28f, 10, Color.white);

            Px.TextButton(Content, "Apply", PxArt.Get("Settings/apply"), ApplyBox.x, ApplyBox.y, ApplyBox.width, ApplyBox.height,
                          "적용", 16, Color.white, Close);
        }

        /// <summary>◀/▶ — 틀(설정_버튼 24×24) 안에 화살표(16×16). ▶는 ◀ 그림을 좌우로 뒤집는다.</summary>
        void ArrowButton(float x, float y, bool right, System.Action onClick)
        {
            var b = Px.IconButton(Content, right ? "ArrowRight" : "ArrowLeft", PxArt.Get("Settings/arrow_frame"), PxArt.Get("Settings/arrow"),
                                  x, y, ArrowSize, ArrowSize, GlyphInset, GlyphInset, GlyphSize, GlyphSize, onClick);
            if (!right) return;
            var glyph = (RectTransform)b.Inner.Find("Icon");
            glyph.localScale = new Vector3(-1f, 1f, 1f);
            glyph.anchoredPosition += new Vector2(GlyphSize, 0f); // 왼쪽 위 기준이라 뒤집으면 왼쪽으로 넘어간다
        }

        protected override void OnShown() => row = 0;

        protected override void OnEscape() => Close();

        protected override void OnKeys()
        {
            if (GameInput.MenuUpDown) row = (row + 3) % 4;
            if (GameInput.MenuDownDown) row = (row + 1) % 4;
            if (GameInput.MenuLeftDown) Change(-1);
            if (GameInput.MenuRightDown) Change(+1);
        }

        void Change(int d)
        {
            switch ((Row)row)
            {
                case Row.Screen:
                    GameSettings.Fullscreen = !GameSettings.Fullscreen;
                    break;
                case Row.Resolution:
                    var list = GameSettings.Resolutions();
                    int i = list.IndexOf(GameSettings.Resolution);
                    if (i < 0) i = 0;
                    GameSettings.Resolution = list[(i + d + list.Count) % list.Count];
                    break;
                case Row.Bgm:
                    GameSettings.BgmVolume += d;
                    break;
                case Row.Sfx:
                    GameSettings.SfxVolume += d;
                    break;
            }
        }

        void SetVolume(Row r, int value)
        {
            if (r == Row.Bgm) GameSettings.BgmVolume = value;
            else if (r == Row.Sfx) GameSettings.SfxVolume = value;
        }

        /// <summary>음량 0~10을 화면 숫자 0~100으로(Figma "50").</summary>
        public static int VolumeShown(int volume) => volume * 100 / GameSettings.VolumeMax;

        protected override void Refresh()
        {
            var res = GameSettings.Resolution;
            values[0].text = GameSettings.Fullscreen ? "전체 화면" : "창 모드";
            values[1].text = $"{res.x} X {res.y}";
            int[] vol = { GameSettings.BgmVolume, GameSettings.SfxVolume };
            for (int v = 0; v < 2; v++)
            {
                float ratio = vol[v] / (float)GameSettings.VolumeMax;
                values[2 + v].text = VolumeShown(vol[v]).ToString();
                fills[v].fillAmount = ratio;
                // 손잡이 가운데가 채움 끝에 오게, 막대 밖으로는 안 나가게
                float x = RowX + BarX + Mathf.Clamp(Mathf.Round(ratio * BarW - KnobW / 2f), 0f, BarW - KnobW);
                knobs[v].anchoredPosition = new Vector2(x, knobs[v].anchoredPosition.y);
            }
            for (int i = 0; i < 4; i++) labels[i].color = i == row ? Selected : Color.white;
        }

        /// <summary>테스트용 — 음량 막대 손잡이 왼쪽 x(판 좌표)와 숫자.</summary>
        public float KnobXForTest(int v) => knobs[v].anchoredPosition.x;
        public string ValueTextForTest(int i) => values[i].text;
    }
}
