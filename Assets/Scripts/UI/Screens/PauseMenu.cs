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
    /// **모양은 프리팹이 원본이다(2026-11-22 프리팹 UI 시범 1호)** — <see cref="PauseMenuView"/>(`Resources/UI/Views/PauseMenuView`)를 불러와
    /// 버튼 동작·키보드 순서만 여기서 연결한다. 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
    ///
    /// **로비 메뉴로도 쓴다**(2026-11-22 사용자 "로비에서 ESC 누르면 타이틀로 갈 수 있게" — 메뉴 방식 선택, 2026-11-24 팀원 프리팹 UI 위에 다시 얹음).
    /// 로비에서 ESC → 같은 모양의 "메뉴": ▶ 계속(닫기) · ⚙ 설정 · ↩ 타이틀로(저장 후 확인창). 제목·조작키 글자만 로비용으로 바꾼다(프리팹 글자 "Title"·"Keys").
    /// </summary>
    public class PauseMenu : PxScreen
    {
        PauseMenuView view;

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

        const string LobbyTitle = "메뉴";
        const string LobbyKeys = "←→ 이동 · C 점프 · F 건물 들어가기\nESC 메뉴 닫기 · Enter/Space/Z 메뉴 고르기";

        static PauseMenu instance;
        bool lobbyOpen; // 로비 메뉴가 열려 있다
        Text title, keys;
        string runTitle, runKeys; // 프리팹에 적힌 사냥 일시정지 글자(되돌릴 때)

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
            view = LoadView<PauseMenuView>(PauseMenuView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[PauseMenu] 화면 프리팹(Resources/" + PauseMenuView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/일시정지 화면 만들기", this);
                view = PauseMenuView.CreateFromCode(Root);
            }

            // 키보드 순서 = 프리팹 buttons 순서: ▶ 계속 · ⚙ 설정 · ↩ 로비로(로비 메뉴에선 타이틀로)
            System.Action[] actions = { Resume, () => SettingsWindow.Open(), Leave };
            if (view.ButtonCount != actions.Length)
                Debug.LogError($"[PauseMenu] 화면 프리팹 버튼이 {view.ButtonCount}개다 — {actions.Length}개(계속·설정·로비로)여야 한다", view);
            for (int i = 0; i < Mathf.Min(view.ButtonCount, actions.Length); i++)
                AddNav(view.WireButton(i, actions[i]));

            // 제목·조작키 글자 — 로비 메뉴일 때만 바꾼다
            foreach (var t in view.GetComponentsInChildren<Text>(true))
            {
                if (t.name == "Title" && title == null) title = t;
                else if (t.name == "Keys" && keys == null) keys = t;
            }
            runTitle = title != null ? title.text : "일시정지";
            runKeys = keys != null ? keys.text : "";
        }

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
            if (title != null) title.text = lobby ? LobbyTitle : runTitle;
            if (keys != null) keys.text = lobby ? LobbyKeys : runKeys;
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
    }
}
