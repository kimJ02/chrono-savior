using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 일시정지 — Figma "일시정지 메뉴"(32:119, 640×360). 띠 위에 "일시정지" 판, 아이콘 버튼 셋: ▶ 계속 · ⚙ 설정 · ↩ 로비로.
    ///
    /// 원본(`#pause`)은 지역·처치 수·남은 시간을 같이 보여 줬지만 Figma엔 없다 — Figma대로 뺐다(사용자 "Figma에 나온 대로").
    /// 동작은 원본과 같다: ESC로 열고 닫고(`GameFlow`), "로비로"는 바로 나간다(원본 `:1027`~`:1028`).
    /// 키보드: ←/→로 고르고 Enter·Space·Z로 누른다.
    /// </summary>
    public class PauseMenu : PxScreen
    {
        // Figma 좌표(배경 7·6을 원점으로 뺀 값)
        static readonly Rect Band = new Rect(0f, 110f, 640f, 128f);      // 42:74
        static readonly Rect Plate = new Rect(256f, 96f, 128f, 32f);     // 77:2
        static readonly Rect TitleBox = new Rect(268f, 93f, 103f, 37f);  // 154:3 (20px 흰색 가운데)
        const float FrameY = 142f, FrameSize = 64f, IconInset = 16f, IconSize = 32f;
        static readonly float[] FrameX = { 148f, 288f, 428f };          // 155:5 · 155:6 · 155:4


        protected override void Build()
        {
            Px.FullFill(Back, "Backdrop", WithAlpha(Px.Backdrop, Px.BackdropAlpha));
            Px.Image(Content, "Band", PxArt.Get("Pause/band"), Band.x, Band.y, Band.width, Band.height);
            Px.Image(Content, "TitlePlate", PxArt.Get("Common/title_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            Px.Text(Content, "Title", "일시정지", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 20, Color.white);

            AddNav(Icon(0, "Icons/play", Resume));
            AddNav(Icon(1, "Icons/gear", () => SettingsWindow.Open()));
            AddNav(Icon(2, "Icons/exit", ToLobby));
        }

        PxButton Icon(int i, string icon, System.Action onClick) =>
            Px.IconButton(Content, "Button" + i, PxArt.Get("Common/icon_frame"), PxArt.Get(icon),
                          FrameX[i], FrameY, FrameSize, FrameSize, IconInset, IconInset, IconSize, IconSize, onClick);

        protected override bool WantsVisible => GameState.Current == GameScene.Pause;


        void Resume() => Flow?.Resume();

        void ToLobby() => Flow?.EnterLobby();

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
