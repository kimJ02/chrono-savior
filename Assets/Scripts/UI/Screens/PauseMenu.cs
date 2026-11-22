using UnityEngine;
using UnityEngine.UI;
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
    ///
    /// **로비 메뉴로도 쓴다**(2026-11-22 사용자 "로비에서 ESC 누르면 타이틀로 갈 수 있게" — 메뉴 방식 선택). 로비에서 ESC → 같은 모양의
    /// "메뉴": ▶ 계속(닫기) · ⚙ 설정 · ↩ 타이틀로(저장 후 확인창). 새 화면을 만들지 않은 건 씬의 판(PixelUI)에 화면을 더하려면 씬을 고쳐야 해서다.
    /// </summary>
    public class PauseMenu : PxScreen
    {
        // Figma 좌표(배경 7·6을 원점으로 뺀 값)
        static readonly Rect Band = new Rect(0f, 110f, 640f, 128f);      // 42:74
        static readonly Rect Plate = new Rect(256f, 96f, 128f, 32f);     // 77:2
        static readonly Rect TitleBox = new Rect(268f, 93f, 103f, 37f);  // 154:3 (20px 흰색 가운데)
        const float FrameY = 142f, FrameSize = 64f, IconInset = 16f, IconSize = 32f;
        static readonly float[] FrameX = { 148f, 288f, 428f };          // 155:5 · 155:6 · 155:4

        const string RunKeys = "←→ 이동 · C 점프 · ↓ 아래로 내려가기 · Z 공격 · X 스킬\nF 포탈 들어가기 · ESC 일시정지 · Enter/Z 메뉴 고르기";
        const string LobbyKeys = "←→ 이동 · C 점프 · F 건물 들어가기\nESC 메뉴 닫기 · Enter/Space/Z 메뉴 고르기";

        static PauseMenu instance;
        bool lobbyOpen; // 로비 메뉴가 열려 있다
        Text title, keys;

        /// <summary>로비 메뉴가 열려 있는가(테스트용).</summary>
        public static bool LobbyMenuOpen => instance != null && instance.lobbyOpen;

        /// <summary>로비 메뉴를 연다(ESC와 같다 — 테스트용).</summary>
        public static void OpenLobbyMenu() { if (instance != null && GameState.Current == GameScene.Lobby) instance.lobbyOpen = true; }

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
            Px.FullFill(Back, "Backdrop", WithAlpha(Px.Backdrop, Px.BackdropAlpha));
            Px.Image(Content, "Band", PxArt.Get("Pause/band"), Band.x, Band.y, Band.width, Band.height);
            Px.Image(Content, "TitlePlate", PxArt.Get("Common/title_plate"), Plate.x, Plate.y, Plate.width, Plate.height);
            title = Px.Text(Content, "Title", "일시정지", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 20, Color.white);

            AddNav(Icon(0, "Icons/play", Resume));
            AddNav(Icon(1, "Icons/gear", () => SettingsWindow.Open()));
            AddNav(Icon(2, "Icons/exit", Leave));

            // 조작키 안내 — 예전엔 설정 창에 있었는데 2026-11-16 Figma 새 설정창엔 자리가 없어 여기(띠 아래 빈 곳)로 옮겼다. Figma엔 없는 글자.
            keys = Px.Text(Content, "Keys", RunKeys, 0f, 262f, 640f, 32f, 10, Color.white);
            keys.lineSpacing = 1.3f;
        }

        PxButton Icon(int i, string icon, System.Action onClick) =>
            Px.IconButton(Content, "Button" + i, PxArt.Get("Common/icon_frame"), PxArt.Get(icon),
                          FrameX[i], FrameY, FrameSize, FrameSize, IconInset, IconInset, IconSize, IconSize, onClick);

        protected override bool WantsVisible =>
            GameState.Current == GameScene.Pause || (lobbyOpen && GameState.Current == GameScene.Lobby);

        // 로비 메뉴는 창처럼 ESC를 가져간다(닫기). 사냥 일시정지는 예전대로 GameFlow가 ESC를 받는다.
        protected override bool Modal => lobbyOpen;

        protected override void OnEscape() => lobbyOpen = false;

        void Update()
        {
            // 로비를 떠나면 닫아 둔다
            if (GameState.Current != GameScene.Lobby) { lobbyOpen = false; return; }
            // 로비에서 ESC — 다른 창이 ESC를 쓰는 중(또는 방금 닫음)이면 그 창 몫이다
            if (!lobbyOpen && GameInput.PauseDown && !UiModal.OwnsEscape && PixelCanvas.AcceptsInput)
            {
                SfxEvents.RaiseUiEsc();
                lobbyOpen = true;
            }
        }

        protected override void Refresh()
        {
            bool lobby = GameState.Current == GameScene.Lobby;
            if (title != null) title.text = lobby ? "메뉴" : "일시정지";
            if (keys != null) keys.text = lobby ? LobbyKeys : RunKeys;
        }

        void Resume()
        {
            if (lobbyOpen) { lobbyOpen = false; return; }
            Flow?.Resume();
        }

        void Leave()
        {
            if (!lobbyOpen) { Flow?.EnterLobby(CommonPopup.AlertIfTransitionFails("로비")); return; }
            // 로비 → 타이틀(저장하고 묻는다). 타이틀에서 다른 세이브 슬롯을 고를 수 있다.
            CommonPopup.Confirm("타이틀로", "진행을 저장하고 타이틀 화면으로 돌아간다.", () =>
            {
                lobbyOpen = false;
                SaveService.Save();
                Flow?.EnterTitle(CommonPopup.AlertIfTransitionFails("타이틀로"));
            }, "돌아가기", "취소");
        }

        static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
