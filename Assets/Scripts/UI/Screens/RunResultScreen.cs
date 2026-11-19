using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 사냥이 끝난 뒤 — 2026-11-16 Figma "결과창"(77:3) 새 판(팀원 zip): 창 틀 512×304 · 위 띠(제목 "원정기록" + "stage N") ·
    /// 칸 셋(128×56 — 전리품·경험치·처치 수) · "원정 통계" 이름표 + 나눔선 · 통계 상자(192×64, 세 줄) · 오른쪽 진행 막대 · 버튼 둘(96×32).
    /// Codex(2026-11-16): 새 배치에 **지금 기록을 그대로 옮긴다** — 기록·잔존 시간은 띠 오른쪽, 사망 원인은 띠 아래 한 줄,
    /// 통계 세 줄 = Figma대로 준 피해량·최대 콤보·받은 피해(2026-11-16 집계 신설 — `RunState.DamageDealt/Taken`),
    /// "원정 진행도" 막대 = 토벌 진행(n/목표) 한 줄만(2026-11-18 사용자 — 시간 동조율·멸망까지 막대는 뺐다. 레벨·남은 날은 다른 화면에서 본다).
    /// 쓰러졌을 때(시간선 붕괴)도 같은 틀 — 제목이 붉고 칸 순서·버튼 글자만 다르다.
    /// "다시 입장"·"재동기화" = 같은 스테이지에 다시 들어간다(멸망 하루 — 스테이지 선택과 같은 규칙). "마을로"·"성문으로" = 로비.
    /// </summary>
    public class RunResultScreen : PxScreen
    {
        protected override bool WantsVisible => GameState.Current == GameScene.Result;
        protected override int DefaultNav => 1;

        // Figma 좌표(640×360 판 그대로)
        static readonly Rect Frame = new Rect(64f, 28f, 512f, 304f);      // 588:16 "결과백그라운드 1"
        static readonly Rect Banner = new Rect(72f, 36f, 496f, 46f);      // 588:13 "결과배너 1"
        static readonly Rect TitleBox = new Rect(89f, 37f, 260f, 29f);    // 448:48 "원정기록"
        static readonly Rect SubBox = new Rect(89f, 64f, 260f, 16f);      // 448:49 "stage 1-2"
        static readonly Rect TimeBox = new Rect(360f, 36f, 196f, 46f);    // Figma엔 없음 — 띠 오른쪽
        static readonly Rect CauseBox = new Rect(80f, 86f, 480f, 18f);    // Figma엔 없음 — 띠와 칸 사이(쓰러졌을 때만)
        static readonly float[] BoxX = { 108f, 256f, 404f };              // 447:23·25·24 "경과창 프레임"
        const float BoxY = 108f, BoxW = 128f, BoxH = 56f, BoxLine = 27f;  // 칸 안 가로줄 y(그림에서 잰 값)
        static readonly Rect StatsPlate = new Rect(108f, 180f, 64f, 20f); // 447:26 이름표
        static readonly Rect StatsLine = new Rect(172f, 188f, 360f, 4f);  // 590:42 "나눔선 1"
        static readonly Rect StatsBox = new Rect(108f, 226f, 192f, 64f);  // 447:30
        static readonly float[] StatRowY = { 3f, 22f, 41f };              // 통계 상자 안 세 칸 위(그림의 가로줄 21·40 사이)
        const float StatRowH = 18f;
        static readonly Rect ProgressPlate = new Rect(335f, 203f, 64f, 20f); // 590:43 이름표 "원정 진행도"
        const float BarX = 340f, BarW = 180f, BarH = 16f;
        static readonly float[] BarY = { 223f };                // 590:47 · 590:46 · 448:37 막대 세 줄(이름표·버튼과 안 겹치게 조금 내림)
        static readonly Rect RetryBox = new Rect(341f, 278f, 96f, 32f);   // 590:50 "버튼 1"
        static readonly Rect OkBox = new Rect(447f, 278f, 96f, 32f);      // 448:47
        static readonly Color Dead = Px.Hex(0xff6a5a);

        Text title, sub, time, cause, retryLabel, okLabel;
        readonly Text[] boxLabels = new Text[3];
        readonly Text[] boxValues = new Text[3];
        readonly Text[] statLabels = new Text[3];
        readonly Text[] statValues = new Text[3];
        readonly Image[] barFills = new Image[1];
        readonly Text[] barTexts = new Text[1];

        protected override void Build()
        {
            Px.FullFill(Back, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, Px.BackdropAlpha));
            Px.Image(Content, "Frame", PxArt.Get("Common/window_tall"), Frame.x, Frame.y, Frame.width, Frame.height).raycastTarget = true;
            Px.Image(Content, "Banner", PxArt.Get("Result/banner"), Banner.x, Banner.y, Banner.width, Banner.height);
            title = Px.Text(Content, "Title", "", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 20, Color.white, TextAnchor.MiddleLeft);
            sub = Px.Text(Content, "Sub", "", SubBox.x, SubBox.y, SubBox.width, SubBox.height, 10, Color.white, TextAnchor.MiddleLeft);
            time = Px.Text(Content, "Time", "", TimeBox.x, TimeBox.y, TimeBox.width, TimeBox.height, 12, Color.white, TextAnchor.MiddleRight);
            cause = Px.Text(Content, "Cause", "", CauseBox.x, CauseBox.y, CauseBox.width, CauseBox.height, 10, Dead);
            for (int i = 0; i < 3; i++)
            {
                Px.Image(Content, "Cell" + i, PxArt.Get("Result/reward_box"), BoxX[i], BoxY, BoxW, BoxH);
                boxLabels[i] = Px.Text(Content, "CellLabel" + i, "", BoxX[i], BoxY + 3f, BoxW, BoxLine - 3f, 10, Color.white);
                boxValues[i] = Px.Text(Content, "CellValue" + i, "", BoxX[i], BoxY + BoxLine + 1f, BoxW, BoxH - BoxLine - 4f, 13, Color.white);
            }

            Px.Image(Content, "StatsPlate", PxArt.Get("Result/name_plate"), StatsPlate.x, StatsPlate.y, StatsPlate.width, StatsPlate.height);
            Px.Text(Content, "StatsLabel", "원정 통계", StatsPlate.x, StatsPlate.y, StatsPlate.width, StatsPlate.height, 10, Color.white);
            Px.Image(Content, "StatsLine", PxArt.Get("Result/divider"), StatsLine.x, StatsLine.y, StatsLine.width, StatsLine.height);
            Px.Image(Content, "StatsBox", PxArt.Get("Result/stats_box"), StatsBox.x, StatsBox.y, StatsBox.width, StatsBox.height);
            for (int i = 0; i < 3; i++)
            {
                float y = StatsBox.y + StatRowY[i];
                statLabels[i] = Px.Text(Content, "StatLabel" + i, "", StatsBox.x + 7f, y, 90f, StatRowH, 10, Color.white, TextAnchor.MiddleLeft);
                statValues[i] = Px.Text(Content, "StatValue" + i, "", StatsBox.x + 92f, y, 92f, StatRowH, 10, Color.white, TextAnchor.MiddleRight);
            }

            // 막대 하나 — 바탕 → 채움(진한 하늘, Figma 색) → 틀 → 글자
            var fill = PxArt.Get("Result/bar_fill");
            Sprite[] fills = { fill };
            for (int i = 0; i < 1; i++)
            {
                float y = BarY[i];
                Px.Image(Content, "BarBack" + i, PxArt.Get("Result/bar_back"), BarX, y, BarW, BarH);
                barFills[i] = Px.Image(Content, "BarFill" + i, fills[i], BarX - 1f, y, 182f, BarH); // 채움 그림은 182칸
                barFills[i].type = Image.Type.Filled;
                barFills[i].fillMethod = Image.FillMethod.Horizontal;
                Px.Image(Content, "BarFrame" + i, PxArt.Get("Result/bar_frame"), BarX, y, BarW, BarH);
                barTexts[i] = Px.Text(Content, "BarText" + i, "", BarX, y, BarW, BarH, 10, Color.white).WithOutline(Px.Ink);
            }
            Px.Image(Content, "ProgressPlate", PxArt.Get("Result/name_plate"), ProgressPlate.x, ProgressPlate.y, ProgressPlate.width, ProgressPlate.height);
            Px.Text(Content, "ProgressLabel", "원정 진행도", ProgressPlate.x, ProgressPlate.y, ProgressPlate.width, ProgressPlate.height, 10, Color.white);

            var retry = AddNav(Px.TextButton(Content, "Retry", PxArt.Get("Result/button"), RetryBox.x, RetryBox.y, RetryBox.width,
                                             RetryBox.height, "다시 입장", 12, Color.white, Retry));
            var ok = AddNav(Px.TextButton(Content, "Ok", PxArt.Get("Result/button"), OkBox.x, OkBox.y, OkBox.width, OkBox.height,
                                          "마을로", 12, Color.white, ToLobby));
            retryLabel = retry.Inner.GetComponentInChildren<Text>();
            okLabel = ok.Inner.GetComponentInChildren<Text>();
        }

        /// <summary>캡처·테스트용 — 실제 끝난 이유 대신.</summary>
        public RunEndReason? PreviewReason;

        // 결과는 한 번 정해지면 안 바뀐다 — 열릴 때(또는 미리보기 이유가 바뀔 때)만 채운다(Refresh는 보이는 동안 매 프레임, Gemini 검토 2026-11-16)
        bool dirty = true;
        RunEndReason? shownPreview;

        protected override void OnShown() => dirty = true;

        protected override void Refresh()
        {
            if (!dirty && PreviewReason == shownPreview) return;
            dirty = false;
            shownPreview = PreviewReason;
            var reason = PreviewReason ?? (Flow != null ? Flow.LastEndReason : RunEndReason.Timeout);
            bool dead = reason == RunEndReason.Dead;
            var p = ProfileService.Current;
            int s = RunState.Region;

            title.text = dead ? "시간선 붕괴" : "원정 기록";
            title.color = dead ? Dead : Color.white;
            sub.text = dead ? $"stage {s} 실패" : reason == RunEndReason.BossDead ? $"stage {s} 돌파" : $"stage {s} 원정 종료";
            time.text = dead ? $"잔존 시간 {Clock(RunState.TimeLeft)}" : $"기록 시간 {Clock(RunState.Elapsed)}";

            string[] labels = dead ? new[] { "처치 수", "획득 전리품", "획득 경험치" } : new[] { "전리품", "경험치", "처치 수" };
            string kills = $"{RunState.Kills:N0}", gold = $"+{RunState.GoldEarned:N0} G", exp = $"+{RunState.ExpEarned:N0}";
            string[] values = dead ? new[] { kills, gold, exp } : new[] { gold, exp, kills };
            for (int i = 0; i < 3; i++) { boxLabels[i].text = labels[i]; boxValues[i].text = values[i]; }

            cause.enabled = dead;
            if (dead) cause.text = $"붕괴 원인: {CauseOf(RunState.DeathSource)}의 공격에 시간선이 무너졌다 — 재화는 그대로 남는다";

            string[] statNames = { "준 피해량", "최대 콤보", "받은 피해" };
            string[] statVals = { $"{RunState.DamageDealt:N0}", $"{RunState.MaxCombo:N0}", $"{RunState.DamageTaken:N0}" };
            for (int i = 0; i < 3; i++) { statLabels[i].text = statNames[i]; statValues[i].text = statVals[i]; }

            int target = Mathf.Max(1, RunState.RegionKillTarget);
            int regionKills = Mathf.Min(p.RegionKills(s), target);
            barFills[0].fillAmount = regionKills / (float)target;
            barTexts[0].text = $"토벌 {regionKills}/{target}";

            retryLabel.text = dead ? "재동기화" : "다시 입장";
            okLabel.text = dead ? "성문으로" : "마을로";
        }

        static string Clock(float seconds)
        {
            int t = Mathf.Max(0, Mathf.CeilToInt(seconds));
            return $"{t / 60:00}:{t % 60:00}";
        }

        /// <summary>쓰러뜨린 것의 이름 — 보스면 Figma 예시대로 "수문장", 아니면 "몬스터".</summary>
        public static string CauseOf(GameObject source)
        {
            if (source != null && source.GetComponentInParent<Boss>() != null) return "수문장";
            return "몬스터";
        }

        void Retry() => StageSelectScreen.TryEnter(Flow, RunState.Region);

        void ToLobby() => Flow?.EnterLobby(CommonPopup.AlertIfTransitionFails("로비"));
    }
}
