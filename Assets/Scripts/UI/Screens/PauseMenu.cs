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
    ///
    /// **모양은 프리팹이 원본이다(2026-11-22 프리팹 UI 시범 1호)** — <see cref="PauseMenuView"/>(`Resources/UI/Views/PauseMenuView`)를 불러와
    /// 버튼 동작·키보드 순서만 여기서 연결한다. 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
    /// </summary>
    public class PauseMenu : PxScreen
    {
        PauseMenuView view;

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

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

            // 키보드 순서 = 프리팹 buttons 순서: ▶ 계속 · ⚙ 설정 · ↩ 로비로
            System.Action[] actions = { Resume, () => SettingsWindow.Open(), ToLobby };
            if (view.ButtonCount != actions.Length)
                Debug.LogError($"[PauseMenu] 화면 프리팹 버튼이 {view.ButtonCount}개다 — {actions.Length}개(계속·설정·로비로)여야 한다", view);
            for (int i = 0; i < Mathf.Min(view.ButtonCount, actions.Length); i++)
                AddNav(view.WireButton(i, actions[i]));
        }

        protected override bool WantsVisible => GameState.Current == GameScene.Pause;


        void Resume() => Flow?.Resume();

        void ToLobby() => Flow?.EnterLobby(CommonPopup.AlertIfTransitionFails("로비"));
    }
}
