using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 타이틀 — Figma "타이틀"(35:2)의 640×360판: 배경 그림 · "CHRONO SAVIOR" 로고 · 메뉴 넷(새 원정 · 불러오기 · 설정 · 나가기).
    /// 메뉴 칸은 반투명 검은 판(`rgba(0,0,0,.33)`), 고른 메뉴는 노란 글자(#fac70e)와 왼쪽 ◆, 나머지는 황갈색(#d4ae88) — Figma 그대로.
    ///
    /// 동작은 예전 타이틀(`TitleScreen`)과 같다: 새 원정은 기록이 있으면 한 번 더 묻고(이제 공용 팝업으로) 지우고 시작,
    /// 불러오기는 기록이 있을 때만, 설정은 설정 창, 나가기는 종료. 키보드: ↑/↓ 고르기, Enter·Space·Z.
    /// (2026-11-19부터 "새 원정"·"불러오기"는 둘 다 세이브 슬롯 창을 연다 — 아래 <see cref="Choose"/>. 위 줄은 예전 동작 기록.)
    ///
    /// **모양은 프리팹이 원본이다(2026-11-23 프리팹 UI 3호)** — 위치·크기·그림·글자·색은 <see cref="TitleMenuView"/> 프리팹에서 고친다.
    /// </summary>
    public class TitleMenu : PxScreen
    {
        protected override bool WantsVisible => GameState.Current == GameScene.Title;
        protected override NavLayout Layout => NavLayout.Vertical;

        TitleMenuView view;

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

        /// <summary>
        /// 모양은 프리팹(<see cref="TitleMenuView"/>, `Resources/UI/Views/TitleMenuView`)이 원본이다(2026-11-23 프리팹 UI 3호).
        /// 여기선 메뉴 판 넷에 동작과 키보드 순서(↑/↓, 새 원정 → 불러오기 → 설정 → 나가기)만 연결한다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            view = LoadView<TitleMenuView>(TitleMenuView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[TitleMenu] 화면 프리팹(Resources/" + TitleMenuView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/타이틀 화면 만들기", this);
                view = TitleMenuView.CreateFromCode(Root);
            }
            if (view.MenuCount != MenuCount)
                Debug.LogError($"[TitleMenu] 화면 프리팹 메뉴가 {view.MenuCount}개다 — {MenuCount}개(새 원정·불러오기·설정·나가기)여야 한다", view);
            for (int i = 0; i < Mathf.Min(view.MenuCount, MenuCount); i++)
            {
                int k = i;
                AddNav(view.WireTitleButton(i, () => Choose(k)));
            }
        }

        const int MenuCount = 4;

        protected override void Refresh()
        {
            Nav[1].Interactable = true; // 불러오기 — 2026-11-19부터 세이브 슬롯 창을 연다(슬롯은 늘 셋 다 있다)
            for (int i = 0; i < Nav.Count; i++)
            {
                var label = view.Label(i);
                if (label == null) continue;
                Color c = i == NavIndex ? view.SelectedColor : view.NormalColor;
                if (!Nav[i].Interactable) c.a = 0.45f;
                label.color = c;
            }
        }

        protected override void OnNavChanged(int index)
        {
            if (view != null) view.PlaceCursor(index);
        }


        void Choose(int i)
        {
            switch (i)
            {
                // 2026-11-19 세이브 슬롯(교수님 지시 — 플레이테스트 시나리오 3개): "새 원정"·"불러오기" 둘 다 슬롯 창을 연다.
                // 타이틀 모양은 그대로(Figma). 새 출발은 슬롯 1, 처음부터 다시는 슬롯의 [초기화].
                case 0:
                case 1:
                    SaveSlotScreen.Open();
                    break;
                case 2:
                    SettingsWindow.Open();
                    break;
                case 3:
                    Quit();
                    break;
            }
        }

        /// <summary>세이브를 지우고 새 프로필로 로비에 들어간다 — 흐름은 `GameFlow.StartNewExpedition`이 맡는다(화면은 프로필을 직접 바꾸지 않는다).</summary>
        void StartFresh()
        {
            // 새 원정은 캐릭터부터 고른다(캐릭터 선택창 뼈대의 뒤로가기/선택하기) — 로비 씬이 실제로 뜬 뒤에만 연다.
            Flow?.StartNewExpedition(CommonPopup.AlertIfTransitionFails("새 원정", CharacterSelectScreen.OpenForRun));
        }

        static void Quit()
        {
#if UNITY_EDITOR
            // 에디터에서는 `Application.Quit()`이 아무 일도 안 한다 — 플레이 모드를 끄는 게 "나가기"에 해당한다.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
