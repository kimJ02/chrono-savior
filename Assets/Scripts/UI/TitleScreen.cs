using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 타이틀 화면 — **원본에 없는 화면이다.** 원본은 로비에서 바로 시작한다(`state.scene` 초기값
    /// `'lobby'`, project_test.html:1466). 디자이너가 Figma에 그린 타이틀(파일
    /// `9EpUO2k4i9en6cWAATGVa4`, 노드 35:2)을 적용하면서 새로 생겼다(2026-09-16).
    ///
    /// ## 좌표는 Figma 값 그대로다
    /// 이 화면은 **1920×1080**에 그려져 있어 `UiTheme.Scaled(1920, 1080)` 안에서 그린다.
    /// 그래서 아래 상수들이 Figma 인스펙터 값과 1:1로 같다 — 1280×720에 억지로 맞추려고 1.5로
    /// 나누면 소수 좌표가 곳곳에 박히고, 그 오차가 픽셀아트에서 1px씩 어긋나 보인다.
    ///
    /// ## 아직 자리표시자인 것
    /// Figma에도 **배경 도트아트와 로고가 아직 없다** — "도트아트 배경"·"Logo"·"양피지 도트"라는
    /// 텍스트만 놓여 있다. 그 자리를 색으로 채워 두었고, 그림이 들어오면
    /// <see cref="UiTextures"/>에 필드를 추가해 꽂으면 된다.
    /// 배경은 Figma에선 흰색이지만 여기서는 어둡게 뒀다 — 흰 화면은 "아직 안 그림"의 표시일 뿐이고,
    /// 실행해서 눈이 부시면 확인 자체가 어렵기 때문이다.
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleScreen : MonoBehaviour
    {
        // ── Figma 35:2 좌표 그대로 (1920×1080) ──
        const float DesignW = 1920f, DesignH = 1080f;

        /// <summary>오른쪽 메뉴 패널(52:100).</summary>
        static readonly Rect PanelRect = new Rect(1280f, 120f, 520f, 800f);
        /// <summary>타이틀 로고 박스(35:18).</summary>
        static readonly Rect LogoRect = new Rect(1399f, 230f, 282f, 154f);
        /// <summary>메뉴 위·아래 구분선(63:4 · 63:5). 렌더에서 실제로 보이는 두께는 2px다.</summary>
        static readonly Rect DividerTop = new Rect(1338f, 423f, 382f, 2f);
        static readonly Rect DividerBottom = new Rect(1338f, 822f, 382f, 2f);

        const float ButtonX = 1418f, ButtonW = 256f, ButtonH = 64f;
        /// <summary>버튼 4개의 Y(63:3 · 61:54 · 61:55 · 61:56) — 85px 간격.</summary>
        static readonly float[] ButtonY = { 463f, 548f, 633f, 718f };

        /// <summary>배경 자리표시자 색. Figma는 흰색이지만 위 클래스 주석의 이유로 어둡게 둔다.</summary>
        static readonly Color BackdropPlaceholder = new Color(0.10f, 0.09f, 0.13f);

        /// <summary>
        /// 이 화면은 1920×1080 좌표계라 **글자도 그 기준으로 커야 한다.** Figma의 메뉴 글자는
        /// 높이 48~58px(61:53·61:57)이고, 1280×720용 기본 크기(22px)를 그대로 쓰면 버튼 안에서
        /// 우표처럼 작아 보인다.
        /// </summary>
        const int MenuFontSize = 34;
        const int LogoFontSize = 44;

        GUIStyle menuStyle, logoStyle;

        GUIStyle MenuStyle => menuStyle ??= new GUIStyle(UiTheme.MenuLabel) { fontSize = MenuFontSize };
        GUIStyle LogoStyle => logoStyle ??= new GUIStyle(UiTheme.MenuLabel) { fontSize = LogoFontSize };

        RunController run;
        bool confirmingNewRun;
        string toast = "";
        float toastLeft;

        void Awake() => run = Object.FindFirstObjectByType<RunController>();

        void Update()
        {
            // 타이틀은 `Time.timeScale = 0`에서 돌아간다(RunController 설계) — 스케일된 시간은 안 흐른다.
            if (toastLeft > 0f) toastLeft -= Time.unscaledDeltaTime;
        }

        void OnGUI()
        {
            if (GameState.Current != GameScene.Title) return;
            if (PxScreens.Has<TitleMenu>()) return; // Figma 타이틀이 그린다(2026-10-24)
            if (run == null) run = Object.FindFirstObjectByType<RunController>();

            // ⚠️ 배경은 **화면 좌표로** 덮는다. 설계 좌표(1920×1080)로 채우면 16:9가 아닌 화면에서
            // letterbox 띠에 사냥터가 그대로 비친다 — 결과 화면에서 이미 겪은 것과 같은 함정이다.
            // 그래서 `Scaled()` 블록보다 **먼저** 그린다.
            UiTheme.FullScreenFill(BackdropPlaceholder);

            using var scaled = UiTheme.Scaled(DesignW, DesignH);

            // 오른쪽 양피지 패널(52:100).
            UiTheme.FillRect(PanelRect, UiTheme.Parchment);

            // 로고 박스(35:18) — 금색 판 + 검은 테두리 + "Logo" 자리표시 글자.
            UiTheme.FillRect(LogoRect, UiTheme.PlateGold);
            DrawBorder(LogoRect, Color.black, 2f);
            GUI.Label(LogoRect, "Logo", LogoStyle);

            UiTheme.FillRect(DividerTop, UiTheme.TitleDivider);
            UiTheme.FillRect(DividerBottom, UiTheme.TitleDivider);

            DrawMenu();
            DrawToast();
        }

        void DrawMenu()
        {
            // 새 원정 — 세이브를 지우고 처음부터. **되돌릴 수 없어서 한 번 더 묻는다**
            // (로비의 "전체 초기화"와 같은 이유 — IMGUI엔 confirm() 대화상자가 없다).
            if (!confirmingNewRun)
            {
                if (Button(0, "새 원정"))
                {
                    if (SaveService.HasSave) confirmingNewRun = true;
                    else StartFresh();
                }
            }
            else
            {
                if (Button(0, "정말? 기록 삭제")) StartFresh();
            }

            // 불러오기 — 자동 로드된 프로필로 로비에 들어간다. 세이브가 없으면 누를 게 없다.
            GUI.enabled = SaveService.HasSave;
            if (Button(1, "불러오기"))
            {
                confirmingNewRun = false;
                run?.EnterLobby();
            }
            GUI.enabled = true;

            // 설정 — Figma에 화면이 아직 없다. 버튼만 자리를 잡아 두고 눌리면 알려준다.
            if (Button(2, "설정"))
            {
                confirmingNewRun = false;
                ShowToast("설정 화면은 아직 없다");
            }

            if (Button(3, "나가기")) Quit();
        }

        bool Button(int index, string label) =>
            UiTheme.PlateButton(new Rect(ButtonX, ButtonY[index], ButtonW, ButtonH), label, MenuStyle);

        /// <summary>세이브를 지우고 새 프로필로 로비에 들어간다.</summary>
        void StartFresh()
        {
            SaveService.DeleteSave();
            ProfileService.Reset();
            confirmingNewRun = false;
            run?.EnterLobby();
        }

        static void Quit()
        {
#if UNITY_EDITOR
            // 에디터에서는 `Application.Quit()`이 아무 일도 안 한다 — 플레이 모드를 끄는 게
            // "나가기"에 해당하는 동작이다.
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void ShowToast(string message)
        {
            toast = message;
            toastLeft = 2f;
        }

        void DrawToast()
        {
            if (toastLeft <= 0f) return;
            var content = new GUIContent(toast);
            Vector2 size = UiTheme.Toast.CalcSize(content);
            GUI.Box(new Rect(PanelRect.center.x - size.x / 2f, 940f, size.x, size.y),
                    content, UiTheme.Toast);
        }

        /// <summary>사각형 테두리만 그린다(로고 박스의 검은 선).</summary>
        static void DrawBorder(Rect r, Color c, float thickness)
        {
            UiTheme.FillRect(new Rect(r.x, r.y, r.width, thickness), c);
            UiTheme.FillRect(new Rect(r.x, r.yMax - thickness, r.width, thickness), c);
            UiTheme.FillRect(new Rect(r.x, r.y, thickness, r.height), c);
            UiTheme.FillRect(new Rect(r.xMax - thickness, r.y, thickness, r.height), c);
        }
    }
}
