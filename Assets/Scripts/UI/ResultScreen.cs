using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 결과 화면과 일시정지 화면 — 원본은 이 둘이 **같은 `#result` 패널**이다(project_test.html:539).
    /// `pauseRun()`이 제목을 "일시정지"로 바꾸고 "게임으로 돌아가기" 버튼을 켜는 식이라(`:4332`),
    /// 여기서도 한 컴포넌트가 두 상태를 그린다.
    ///
    /// **화면 전체를 덮는다** — 원본 `#result { position:absolute; inset:0; background:rgba(5,4,10,.88) }`
    /// (`:223`). 예전엔 가운데 작은 상자만 띄웠는데, 그러면 상자 밖에 사냥터가 그대로 보여서
    /// "게임이 끝났다"는 신호가 약하고 상자 안 글씨도 배경과 대비가 부족했다. 원본이 화면을 통째로
    /// 덮는 건 연출이 아니라 **읽히게 만드는 장치**다.
    ///
    /// 버튼은 판단을 하지 않는다 — 전부 <see cref="RunController"/>의 공개 메서드를 부른다(UI 층 규칙).
    /// </summary>
    [DisallowMultipleComponent]
    public class ResultScreen : MonoBehaviour
    {
        RunController run;

        void Awake() => run = Object.FindFirstObjectByType<RunController>();

        // 원본 `#result`는 flex로 가운데 정렬한다(`:223`). 우리는 좌표를 직접 잡으므로
        // "제목 → 수치 판 → 버튼"의 세로 위치를 여기서 정한다.
        const float TitleY = 168f;
        const float RowsY = 258f;      // 원본 `#resultRows { margin:22px 0 }`
        const float RowsWidth = 420f;  // 원본 `min-width:340px` + 좌우 padding 40px
        const float RowHeight = 34f;   // 원본 `line-height:2.1` × 16px
        const float ButtonWidth = 260f;

        void OnGUI()
        {
            var scene = GameState.Current;
            if (scene != GameScene.Pause && scene != GameScene.Result) return;
            if (scene == GameScene.Pause && PxScreens.Has<PauseMenu>()) return; // Figma 일시정지가 그린다(2026-10-24)
            if (scene == GameScene.Result && PxScreens.Has<RunResultScreen>()) return; // Figma 원정 기록·전투 패배가 그린다
            if (run == null) run = Object.FindFirstObjectByType<RunController>();

            using var scaled = UiTheme.Scaled();

            UiTheme.FullScreenScrim();

            if (scene == GameScene.Pause) DrawPause();
            else DrawResult();
        }

        /// <summary>원본 `pauseRun()`이 채우는 내용(project_test.html:4337~4341).</summary>
        void DrawPause()
        {
            DrawTitle("일시정지", UiTheme.TitleColor);

            float y = DrawRows(new[]
            {
                ("현재 지역", $"{RunState.Region}지역 · {RegionConfig.NameOf(RunState.Region)}"),
                ("이번 사냥", $"{RunState.Kills}마리"),
                ("남은 시간", $"{Mathf.CeilToInt(RunState.TimeLeft)}초"),
            });

            if (Button(y, "게임으로 돌아가기 (ESC)")) run?.Resume();
            if (Button(y + 42f, "로비로 돌아가기")) run?.EnterLobby();
        }

        /// <summary>원본 `endRun(reason)`이 채우는 내용(project_test.html:4358~4377).</summary>
        void DrawResult()
        {
            // 원본은 제목 색으로 결말을 구분한다 — `.dead`는 붉은색, `.clear`는 옥색(`:225`·`:226`).
            var reason = run == null ? RunEndReason.Timeout : run.LastEndReason;
            (string title, Color color) = reason switch
            {
                RunEndReason.Dead => ("斬 · 敗  —  쓰러졌다", UiTheme.DeadColor),
                RunEndReason.BossDead => ("討伐 完了  —  보스 격파!", UiTheme.ClearColor),
                _ => ("시간 종료", UiTheme.TitleColor),
            };

            DrawTitle(title, color);

            float y = DrawRows(new[]
            {
                ("처치한 몬스터", $"{RunState.Kills}마리"),
                ("획득 골드", $"+{RunState.GoldEarned} G"),
                ("획득 경험치", $"+{RunState.ExpEarned}"),
            });

            if (reason == RunEndReason.Dead)
            {
                // 원본 결과 화면의 안내 문구 그대로(`:4376`) — 죽어도 재화는 남는다.
                UiTheme.ShadowLabel(new Rect(0f, y, UiTheme.DesignWidth, 22f),
                                    "죽어도 획득한 재화는 유지된다. 강화하고 다시 도전하자.",
                                    Centered());
                y += 30f;
            }

            if (Button(y, "로비로 돌아가기")) run?.EnterLobby();
        }

        void DrawTitle(string text, Color color) =>
            UiTheme.ShadowLabel(new Rect(0f, TitleY, UiTheme.DesignWidth, 60f),
                                text, UiTheme.BigTitle, color);

        /// <summary>
        /// 원본 `#resultRows`(`:227`) — 어두운 판 위에 **이름은 왼쪽, 값은 오른쪽**(`b { float:right }`).
        /// 값을 이름 뒤에 이어 붙이면 줄마다 숫자 위치가 달라져서 한눈에 안 들어온다.
        /// </summary>
        /// <returns>판 아래의 다음 Y 좌표.</returns>
        float DrawRows((string label, string value)[] rows)
        {
            float h = rows.Length * RowHeight + 32f; // + 위아래 padding 16px
            var panel = new Rect((UiTheme.DesignWidth - RowsWidth) / 2f, RowsY, RowsWidth, h);
            GUI.Box(panel, GUIContent.none, UiTheme.Rows);

            float y = panel.y + 16f;
            foreach (var (label, value) in rows)
            {
                var line = new Rect(panel.x + 40f, y, panel.width - 80f, RowHeight);
                GUI.Label(line, label, LeftRow());
                GUI.Label(line, value, RightRow());
                y += RowHeight;
            }

            return panel.yMax + 22f; // 원본 `margin:22px 0`
        }

        bool Button(float y, string text) =>
            GUI.Button(new Rect((UiTheme.DesignWidth - ButtonWidth) / 2f, y, ButtonWidth, 32f), text);

        // ── 정렬만 바꾼 사본 (원본 스타일을 그 자리에서 고치면 다른 화면까지 따라 바뀐다) ──
        static GUIStyle centered, leftRow, rightRow;

        static GUIStyle Centered() =>
            centered ??= new GUIStyle(UiTheme.Hud) { alignment = TextAnchor.MiddleCenter };

        static GUIStyle LeftRow() =>
            leftRow ??= new GUIStyle(UiTheme.Hud) { alignment = TextAnchor.MiddleLeft, fontSize = 16 };

        static GUIStyle RightRow()
        {
            if (rightRow == null)
            {
                rightRow = new GUIStyle(UiTheme.Hud)
                {
                    alignment = TextAnchor.MiddleRight,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                };
                rightRow.normal.textColor = UiTheme.GoldColor; // 원본 `#resultRows b { color:#ffd76e }`
            }
            return rightRow;
        }
    }
}
