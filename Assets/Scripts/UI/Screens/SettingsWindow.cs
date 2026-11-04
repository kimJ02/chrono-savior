using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 설정 창 — **Figma에 그림이 없는 화면**이라 Figma 공용 부품(창 틀 "창 프레임 1"·작은 버튼·↻ 닫기)으로 만들었다(2026-10-24).
    /// 항목은 사용자와 정한 것: 화면 모드 · 해상도 · 배경음 · 효과음 · 조작키 안내. 닫기는 ↻ 버튼(사용자 "설정창 끄는 버튼")과 ESC.
    /// 일시정지·타이틀에서 연다(<see cref="Open"/>). 키보드: ↑/↓ 줄 고르기, ←/→ 값 바꾸기.
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

        // 공용 팝업(53:32)과 같은 자리 — 창 틀 512×288을 (64, 36)에, 제목은 "팝업창" 글자 자리(16px).
        static readonly Rect Frame = new Rect(64f, 36f, 512f, 288f);
        static readonly Rect TitleBox = new Rect(280f, 58f, 80f, 20f);
        // ↻ 닫기 — 스테이지 선택의 작은 버튼(195:10, 32px 그림을 24로)·아이콘(195:11, 32px 그림을 19로)과 같은 크기
        static readonly Rect CloseBox = new Rect(536f, 54f, 24f, 24f);
        const float CloseIconInset = 2f, CloseIconSize = 19f;

        const float LabelX = 112f, LabelW = 150f;
        const float RowTop = 104f, RowStep = 38f, RowH = 24f;
        const float LeftArrowX = 330f, RightArrowX = 506f, ArrowSize = 24f;
        const float ValueX = LeftArrowX + ArrowSize, ValueW = RightArrowX - (LeftArrowX + ArrowSize);
        static readonly Color Selected = Px.Hex(0xffe066);
        static readonly Color Arrow = Px.Hex(0x0e1a3c);

        enum Row { Screen, Resolution, Bgm, Sfx }
        static readonly string[] RowNames = { "화면", "해상도", "배경음", "효과음" };
        readonly Text[] labels = new Text[4];
        readonly Text[] values = new Text[4];
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
            Px.Image(Content, "Frame", PxArt.Get("Common/window_frame"), Frame.x, Frame.y, Frame.width, Frame.height)
              .raycastTarget = true; // 창 뒤(일시정지 버튼)가 눌리지 않게
            Px.Text(Content, "Title", "설정", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 16, Color.white);
            Px.IconButton(Content, "Close", PxArt.Get("Common/button_small"), PxArt.Get("Icons/close"),
                          CloseBox.x, CloseBox.y, CloseBox.width, CloseBox.height,
                          CloseIconInset, CloseIconInset, CloseIconSize, CloseIconSize, Close);

            for (int i = 0; i < 4; i++)
            {
                int r = i;
                float y = RowTop + i * RowStep;
                labels[i] = Px.Text(Content, "Label" + i, RowNames[i], LabelX, y, LabelW, RowH, 13, Color.white,
                                    TextAnchor.MiddleLeft);
                ArrowButton(LeftArrowX, y, "<", () => { row = r; Change(-1); });
                values[i] = Px.Text(Content, "Value" + i, "", ValueX, y, ValueW, RowH, 13, Color.white);
                ArrowButton(RightArrowX, y, ">", () => { row = r; Change(+1); });
            }

            Px.Text(Content, "KeysTitle", "조작", LabelX, RowTop + 4 * RowStep + 2f, LabelW, 16f, 13, Color.white,
                    TextAnchor.MiddleLeft);
            Px.Text(Content, "Keys",
                    "←→ 이동 · C 점프 · ↓ 아래로 내려가기 · Z 공격 · X 스킬\nF 포탈 들어가기 · ESC 일시정지 · Enter/Z 메뉴 고르기",
                    LabelX, RowTop + 4 * RowStep + 22f, 420f, 28f, 10, Color.white, TextAnchor.UpperLeft);
        }

        void ArrowButton(float x, float y, string glyph, System.Action onClick) =>
            Px.TextButton(Content, "Arrow", PxArt.Get("Common/button_small"), x, y, ArrowSize, ArrowSize,
                          glyph, 13, Arrow, onClick);

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

        protected override void Refresh()
        {
            var res = GameSettings.Resolution;
            values[0].text = GameSettings.Fullscreen ? "전체 화면" : "창 모드";
            values[1].text = $"{res.x} × {res.y}";
            values[2].text = GameSettings.BgmVolume.ToString();
            values[3].text = GameSettings.SfxVolume.ToString();
            for (int i = 0; i < 4; i++) labels[i].color = i == row ? Selected : Color.white;
        }
    }
}
