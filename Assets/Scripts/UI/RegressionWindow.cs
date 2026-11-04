using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 시간 회귀 확인창 — Figma "시간 회귀창"(파일 `9EpUO2k4i9en6cWAATGVa4`, 노드 31:4) 적용.
    ///
    /// ## 이게 결과 창이 아니라 확인 창인 이유
    /// 제목은 "회귀 결과"지만 통계 칸이 **"획득 <b>가능한</b> 파편"**이다 — 아직 받지 않았다는 뜻이고,
    /// 우리 <see cref="PlayerProfile.ShardPreview"/>가 정확히 그 값이다. 그래서 이 창은
    /// 시간 회귀를 실행하기 **전에** "이만큼 받고 이번 생을 접겠는가"를 확인하는 자리로 붙였다.
    /// (디자인에 확인/취소 버튼이 없어서 아래 두 개는 내가 추가했다 — 되돌릴 수 없는 조작이라
    /// 창만 띄우고 끝낼 수는 없다.)
    ///
    /// ## 좌표는 Figma 값 그대로 (640×360)
    /// `UiTheme.Scaled(640, 360)` 안에서 그린다. 우리 기본 설계 좌표(1280×720)의 정확히 절반이라
    /// 픽셀아트가 정수배로 확대된다 — 도트가 안 깨지는 조건이다.
    ///
    /// ## 디자인과 다른 한 곳
    /// 디자인의 첫 칸은 **"최고 스테이지 / stage 11"**인데 우리에겐 아직 스테이지 개념이 없다
    /// (지역 9개만 있다). 그래서 **정복한 지역 수**를 같은 자리에 넣고 라벨도 그렇게 적었다 —
    /// 스테이지를 구현하면 이 한 줄만 바꾸면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class RegressionWindow : MonoBehaviour
    {
        const float DesignW = 640f, DesignH = 360f;

        // ── Figma 31:4 좌표 그대로 ──
        static readonly Rect WindowRect = new Rect(64f, 36f, 512f, 288f);   // 162:2
        static readonly Rect TitleRect = new Rect(64f, 46f, 512f, 26f);     // 162:12 (가운데 정렬로 대체)
        static readonly Rect SubtitleRect = new Rect(64f, 94f, 512f, 22f);  // 162:15

        // 통계 4칸: 아이콘 64×64, 라벨, 값. 좌/우 두 열 × 위/아래 두 행.
        static readonly Rect IconFlag = new Rect(113f, 115f, 64f, 64f);     // 163:16
        static readonly Rect IconShard = new Rect(338f, 118f, 64f, 64f);    // 163:20
        static readonly Rect IconStar = new Rect(113f, 202f, 64f, 64f);     // 165:22
        static readonly Rect IconSkull = new Rect(338f, 202f, 64f, 64f);    // 164:21

        const float LeftTextX = 177f, RightTextX = 402f;
        const float RowTopLabelY = 133f, RowTopValueY = 144f;     // 162:9 · 163:17
        const float RowBottomLabelY = 216f, RowBottomValueY = 231f; // 162:10 · 165:23
        const float LabelH = 15f, ValueH = 35f, TextW = 150f;

        // 확인/취소 버튼 — 디자인에 없어서 창 아래쪽 여백에 넣었다(플레이트 그림을 반으로 줄여 씀).
        static readonly Rect ConfirmRect = new Rect(140f, 278f, 160f, 32f);
        static readonly Rect CancelRect = new Rect(340f, 278f, 160f, 32f);

        /// <summary>열려 있는지. 로비가 이 값을 보고 자기 입력을 막지는 않는다(창이 위에 그려진다).</summary>
        public bool IsOpen { get; private set; }

        public void Open() => IsOpen = true;
        public void Close() => IsOpen = false;

        void OnGUI()
        {
            if (!IsOpen) return;
            if (PxScreens.Has<RegressionScreen>()) { IsOpen = false; RegressionScreen.Open(); return; } // Figma 회귀창이 그린다
            // 로비보다 **위에** 그려야 한다. IMGUI는 depth가 작은 쪽이 앞이다.
            GUI.depth = -10;

            if (GameState.Current != GameScene.Lobby) { IsOpen = false; return; }

            var profile = ProfileService.Current;

            // 어두운 배경(157:12) — **화면 좌표로** 덮는다. 설계 좌표(640×360)로 채우면 16:9가 아닌
            // 화면에서 letterbox 띠에 로비가 비쳐 보인다(TitleScreen 주석과 같은 이유).
            UiTheme.FullScreenFill(UiTheme.FigmaScrim);

            using var scaled = UiTheme.Scaled(DesignW, DesignH);

            // 창 프레임(162:2) — 제목 아래 구분선이 그림에 포함돼 있다.
            UiTheme.DrawTex(WindowRect, UiTheme.WindowFrame, UiTheme.WindowGray);

            UiTheme.ShadowLabel(TitleRect, "회귀 결과", Centered(UiTheme.Title, 20));
            UiTheme.ShadowLabel(SubtitleRect, "시간을 되감아 더 강한 회차로 넘어 갑니다",
                                Centered(UiTheme.StatLabel, 13));

            // 첫 칸: 디자인은 "최고 스테이지"지만 스테이지가 아직 없어 정복한 지역 수를 넣는다.
            Stat(IconFlag, UiTheme.IconFlag, LeftTextX, RowTopLabelY, RowTopValueY,
                 "정복한 지역", $"{profile.ClearedRegionCount()} / {RegionConfig.Count}");

            Stat(IconShard, UiTheme.IconShard, RightTextX, RowTopLabelY, RowTopValueY,
                 "획득 가능한 파편", $"{profile.ShardPreview():N0}");

            Stat(IconStar, UiTheme.IconStar, LeftTextX, RowBottomLabelY, RowBottomValueY,
                 "현재 레벨", $"Lv.{profile.level}");

            Stat(IconSkull, UiTheme.IconSkull, RightTextX, RowBottomLabelY, RowBottomValueY,
                 "총 처치 수", $"{profile.stats.totalKills:N0}");

            DrawButtons(profile);
        }

        void Stat(Rect iconBox, Texture2D icon, float textX, float labelY, float valueY,
                  string label, string value)
        {
            UiTheme.DrawIcon(iconBox, icon);
            UiTheme.ShadowLabel(new Rect(textX, labelY, TextW, LabelH), label, UiTheme.StatLabel);
            UiTheme.ShadowLabel(new Rect(textX, valueY, TextW, ValueH), value, UiTheme.StatValue);
        }

        void DrawButtons(PlayerProfile profile)
        {
            int gain = profile.ShardPreview();

            // 정복한 지역이 없으면 받을 파편이 없다 — 원본도 그때는 시간 회귀를 막는다.
            GUI.enabled = gain >= 1;
            if (UiTheme.PlateButton(ConfirmRect, gain >= 1 ? $"회귀 (+{gain})" : "회귀 불가"))
            {
                int got = profile.DoRegression();
                if (got > 0) SaveService.Save();
                IsOpen = false;
            }
            GUI.enabled = true;

            if (UiTheme.PlateButton(CancelRect, "취소")) IsOpen = false;
        }

        // 원본 스타일을 그 자리에서 고치면 다른 화면까지 따라 바뀌므로 사본을 만든다.
        static GUIStyle centeredTitle, centeredSmall;

        static GUIStyle Centered(GUIStyle basis, int fontSize)
        {
            if (fontSize >= 20)
                return centeredTitle ??= new GUIStyle(basis)
                { alignment = TextAnchor.MiddleCenter, fontSize = fontSize };
            return centeredSmall ??= new GUIStyle(basis)
            { alignment = TextAnchor.MiddleCenter, fontSize = fontSize };
        }
    }
}
