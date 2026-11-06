using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 사냥이 끝난 뒤 — Figma "원정 기록창"(77:3, 시간 종료·보스 격파)과 "전투 패배창"(80:2, 쓰러짐)은 **뼈대만** 있어서,
    /// 뼈대 내용을 Figma 공용 창 틀·버튼으로 640판에 옮겼다(사용자 "다 넣을거야").
    ///   - 원정 기록: 제목 · "stage N 돌파/원정 종료" · 기록 시간 · 전리품(골드)·경험치·격파 수 · 원정 통계 · 시간 동조율(레벨 진행) · 다시 원정/확인
    ///   - 시간선 붕괴: 제목 · "stage N 실패" · 잔존 시간 · 격파 수·획득 전리품·획득 경험치 · 붕괴 원인 · 재동기화/성문으로
    /// "다시 원정"·"재동기화" = 같은 스테이지에 다시 들어간다(멸망 하루 — 스테이지 선택과 같은 규칙). 확인·성문으로 = 로비.
    /// 원정 통계의 "받은 피해"는 아직 안 재서 뺐다(뼈대엔 있음).
    /// </summary>
    public class RunResultScreen : PxScreen
    {
        protected override bool WantsVisible => GameState.Current == GameScene.Result;
        protected override int DefaultNav => 1;

        static readonly Rect Frame = new Rect(64f, 36f, 512f, 288f);
        static readonly Rect TitleBox = new Rect(84f, 46f, 472f, 28f);
        static readonly Rect SubBox = new Rect(104f, 96f, 300f, 16f);
        static readonly Rect TimeBox = new Rect(404f, 96f, 132f, 16f);
        static readonly float[] BoxX = { 104f, 249f, 394f };
        const float BoxY = 120f, BoxW = 142f, BoxH = 46f;
        static readonly Rect RetryBox = new Rect(176f, 284f, 96f, 32f);
        static readonly Rect OkBox = new Rect(368f, 284f, 96f, 32f);
        static readonly Color Cell = new Color(0f, 0f, 0f, 0.22f);
        static readonly Color Gold = Px.Hex(0xfac70e);
        static readonly Color Dead = Px.Hex(0xff6a5a);
        static readonly Color32 BarRed = new Color32(234, 50, 60, 255);
        static readonly Color32 BarEmpty = new Color32(57, 31, 33, 255);

        Text title, sub, time, cause, statsText, syncLabel, syncText, retryLabel, okLabel;
        readonly Text[] boxLabels = new Text[3];
        readonly Text[] boxValues = new Text[3];
        Image syncFill;
        Vector2Int barSpan;

        protected override void Build()
        {
            Px.FullFill(Back, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, Px.BackdropAlpha));
            Px.Image(Content, "Frame", PxArt.Get("Common/window_frame"), Frame.x, Frame.y, Frame.width, Frame.height).raycastTarget = true;
            title = Px.Text(Content, "Title", "", TitleBox.x, TitleBox.y, TitleBox.width, TitleBox.height, 20, Color.white);
            sub = Px.Text(Content, "Sub", "", SubBox.x, SubBox.y, SubBox.width, SubBox.height, 12, Color.white, TextAnchor.MiddleLeft);
            time = Px.Text(Content, "Time", "", TimeBox.x, TimeBox.y, TimeBox.width, TimeBox.height, 12, Color.white, TextAnchor.MiddleRight);
            for (int i = 0; i < 3; i++)
            {
                Px.Fill(Content, "Cell" + i, Cell, BoxX[i], BoxY, BoxW, BoxH);
                boxLabels[i] = Px.Text(Content, "CellLabel" + i, "", BoxX[i] + 8f, BoxY + 4f, BoxW - 16f, 14f, 10, Color.white, TextAnchor.MiddleLeft);
                boxValues[i] = Px.Text(Content, "CellValue" + i, "", BoxX[i] + 8f, BoxY + 20f, BoxW - 16f, 22f, 16, Gold, TextAnchor.MiddleLeft);
            }
            cause = Px.Text(Content, "Cause", "", 104f, 172f, 432f, 14f, 10, Dead, TextAnchor.MiddleLeft);
            Px.Text(Content, "StatsLabel", "원정 통계", 104f, 192f, 200f, 14f, 12, Color.white, TextAnchor.MiddleLeft);
            statsText = Px.Text(Content, "Stats", "", 104f, 210f, 200f, 60f, 10, Color.white, TextAnchor.UpperLeft);
            statsText.lineSpacing = 1.3f;

            syncLabel = Px.Text(Content, "SyncLabel", "시간 동조율", 340f, 192f, 196f, 14f, 12, Color.white, TextAnchor.MiddleLeft);
            var bar = PxArt.Get("Stage/progress");
            barSpan = PxArt.ColorSpanX(bar, BarRed);
            Px.Image(Content, "SyncBack", PxArt.Derive(bar, "empty", c => PxArt.Same(c, BarRed) ? BarEmpty : c), 340f, 214f, 128f, 32f);
            syncFill = Px.Image(Content, "SyncFill", PxArt.Derive(bar, "fill", c => PxArt.Same(c, BarRed) ? c : new Color32(0, 0, 0, 0)),
                                340f, 214f, 128f, 32f);
            syncFill.type = Image.Type.Filled;
            syncFill.fillMethod = Image.FillMethod.Horizontal;
            syncText = Px.Text(Content, "SyncText", "", 340f, 222f, 128f, 16f, 10, Color.white).WithOutline(Px.Ink);

            var retry = AddNav(Px.TextButton(Content, "Retry", PxArt.Get("Common/button"), RetryBox.x, RetryBox.y, RetryBox.width,
                                             RetryBox.height, "다시 원정", 13, Color.white, Retry));
            var ok = AddNav(Px.TextButton(Content, "Ok", PxArt.Get("Common/button"), OkBox.x, OkBox.y, OkBox.width, OkBox.height,
                                          "확인", 13, Color.white, ToLobby));
            retryLabel = retry.Inner.GetComponentInChildren<Text>();
            okLabel = ok.Inner.GetComponentInChildren<Text>();
        }


        /// <summary>캡처·테스트용 — 실제 끝난 이유 대신.</summary>
        public RunEndReason? PreviewReason;

        protected override void Refresh()
        {
            var reason = PreviewReason ?? (Flow != null ? Flow.LastEndReason : RunEndReason.Timeout);
            bool dead = reason == RunEndReason.Dead;
            var p = ProfileService.Current;
            int s = RunState.Region;

            title.text = dead ? "시간선 붕괴" : "원정 기록";
            title.color = dead ? Dead : Color.white;
            sub.text = dead ? $"stage {s} 실패" : reason == RunEndReason.BossDead ? $"stage {s} 돌파" : $"stage {s} 원정 종료";
            time.text = dead ? $"잔존 시간 {Clock(RunState.TimeLeft)}" : $"기록 시간 {Clock(RunState.Elapsed)}";

            string[] labels = dead ? new[] { "격파 수", "획득 전리품", "획득 경험치" } : new[] { "전리품", "경험치", "격파 수" };
            string kills = $"{RunState.Kills:N0}", gold = $"+{RunState.GoldEarned:N0} G", exp = $"+{RunState.ExpEarned:N0}";
            string[] values = dead ? new[] { kills, gold, exp } : new[] { gold, exp, kills };
            for (int i = 0; i < 3; i++) { boxLabels[i].text = labels[i]; boxValues[i].text = values[i]; }

            cause.enabled = dead;
            if (dead) cause.text = $"붕괴 원인: {CauseOf(RunState.DeathSource)}의 공격에 시간선이 무너졌다 — 재화는 그대로 남는다";

            statsText.text = $"최대 콤보  {RunState.MaxCombo}\n토벌  {p.RegionKills(s)}/{RunState.RegionKillTarget}\n멸망까지  {p.doomDays}일";

            long need = PlayerProfile.RequiredExp(p.level);
            float ratio = need > 0 ? Mathf.Clamp01(p.exp / (float)need) : 0f;
            int span = barSpan.y - barSpan.x + 1;
            syncFill.fillAmount = (barSpan.x + Mathf.Round(ratio * span)) / 128f;
            syncText.text = $"Lv.{p.level} · {Mathf.FloorToInt(ratio * 100f)}%";

            retryLabel.text = dead ? "재동기화" : "다시 원정";
            okLabel.text = dead ? "성문으로" : "확인";
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
