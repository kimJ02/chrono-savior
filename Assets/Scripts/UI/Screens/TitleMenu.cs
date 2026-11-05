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
    /// </summary>
    public class TitleMenu : PxScreen
    {
        protected override bool WantsVisible => GameState.Current == GameScene.Title;
        protected override NavLayout Layout => NavLayout.Vertical;

        // Figma 좌표(640판 원점 0·0)
        static readonly Rect Logo = new Rect(155f, 20f, 329f, 185f);   // 375:2 (그림이 칸 높이의 94.47%)
        const float PlateX = 256f, PlateW = 128f, PlateH = 32f;          // 404:5 · 404:9 · 404:10 · 404:11
        static readonly float[] PlateY = { 200f, 238f, 276f, 314f };
        const int MenuSize = 12;                                         // 319:2~319:5 12px
        const float CursorGap = 6f, CursorSize = 16f;                    // ◆(404:15) — "새 원정" 글자 왼쪽 6px 앞(278 + 16 = 294 … 글자 300)
        static readonly Color Plate = new Color(0f, 0f, 0f, 0.33f);
        static readonly Color Selected = Px.Hex(0xfac70e);
        static readonly Color Normal = Px.Hex(0xd4ae88);
        static readonly string[] Labels = { "새 원정", "불러오기", "설정", "나가기" };

        readonly Text[] labels = new Text[4];
        Image cursor;

        protected override void Build()
        {
            // 16:9가 아닌 화면의 남는 곳은 검게, 판엔 배경 그림
            Px.FullFill(Back, "Letterbox", Color.black);
            Px.Image(Content, "Background", PxArt.Get("Title/background"), 0f, 0f, PixelCanvas.Width, PixelCanvas.Height);
            Px.Image(Content, "Logo", PxArt.Get("Title/logo"), Logo.x, Logo.y, Logo.width, Logo.height);

            for (int i = 0; i < 4; i++)
            {
                int k = i;
                var plate = Px.Fill(Content, "Menu" + i, Plate, PlateX, PlateY[i], PlateW, PlateH);
                var inner = Px.Stretch(plate.transform, "Inner");
                var b = plate.gameObject.AddComponent<PxButton>();
                b.Init(plate, inner);
                b.OnClick = () => Choose(k);
                labels[i] = Px.Text(inner, "Label", Labels[i], 0f, 0f, PlateW, PlateH, MenuSize, Normal);
                AddNav(b);
            }
            cursor = Px.Image(Content, "Cursor", PxArt.Get("Common/cursor"), 0f, 0f, CursorSize, CursorSize);
        }

        protected override void Refresh()
        {
            Nav[1].Interactable = SaveService.HasSave; // 불러오기 — 기록이 없으면 누를 게 없다
            for (int i = 0; i < 4; i++)
            {
                Color c = i == NavIndex ? Selected : Normal;
                if (!Nav[i].Interactable) c.a = 0.45f;
                labels[i].color = c;
            }
        }

        protected override void OnNavChanged(int index)
        {
            if (cursor == null) return;
            float textW = labels[index].preferredWidth;
            float x = PlateX + (PlateW - textW) / 2f - CursorGap - CursorSize;
            float y = PlateY[index] + (PlateH - CursorSize) / 2f;
            ((RectTransform)cursor.transform).anchoredPosition = new Vector2(Mathf.Round(x), -Mathf.Round(y));
        }


        void Choose(int i)
        {
            switch (i)
            {
                case 0:
                    if (SaveService.HasSave)
                        CommonPopup.Confirm("새 원정", "지금까지의 기록이 모두 지워집니다.\n처음부터 새로 시작할까요?", StartFresh, "시작");
                    else StartFresh();
                    break;
                case 1:
                    if (SaveService.HasSave) Flow?.EnterLobby(CommonPopup.AlertIfTransitionFails("불러오기"));
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
            Flow?.StartNewExpedition(CommonPopup.AlertIfTransitionFails("새 원정", CharacterSelectScreen.Open));
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
