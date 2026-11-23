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
    ///
    /// **모양은 프리팹이 원본이다(2026-11-23 프리팹 UI 6호)** — 위치·크기·그림·글꼴·색은 <see cref="RunResultScreenView"/> 프리팹에서 고친다.
    /// </summary>
    public class RunResultScreen : PxScreen
    {
        protected override bool WantsVisible => GameState.Current == GameScene.Result;
        protected override int DefaultNav => 1;

        RunResultScreenView view;

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

        /// <summary>
        /// 모양은 프리팹(<see cref="RunResultScreenView"/>, `Resources/UI/Views/RunResultScreenView`)이 원본이다(2026-11-23 프리팹 UI 6호).
        /// 여기선 버튼 둘에 동작과 키보드 순서(다시 입장 → 마을로)만 연결한다. 글자·막대는 <see cref="Refresh"/>가 채운다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            view = LoadView<RunResultScreenView>(RunResultScreenView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[RunResultScreen] 화면 프리팹(Resources/" + RunResultScreenView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/결과 창 만들기", this);
                view = RunResultScreenView.CreateFromCode(Root);
            }
            if (view.BoxCount != 3 || view.StatCount != 3 || view.ButtonCount != 2)
                Debug.LogError($"[RunResultScreen] 화면 프리팹 칸 수가 다르다(칸 {view.BoxCount}·통계 {view.StatCount}·버튼 {view.ButtonCount}) — 칸 3·통계 3·버튼 2여야 한다", view);
            AddNav(view.WireResultButton(RunResultScreenView.Slot.Retry, Retry));
            AddNav(view.WireResultButton(RunResultScreenView.Slot.Ok, ToLobby));
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

            view.Title.text = dead ? "시간선 붕괴" : "원정 기록";
            view.Title.color = dead ? view.DeadColor : view.TitleColor;
            // 2026-11-20 — 챕터 번호만 나오던 것("stage 2 실패")을 스테이지까지("stage 2-4 실패")
            string sl = ChapterConfig.StageLabel(s, RunState.Stage);
            view.Sub.text = dead ? $"stage {sl} 실패" : reason == RunEndReason.BossDead ? $"stage {sl} 돌파" : $"stage {sl} 원정 종료";
            view.TimeText.text = dead ? $"잔존 시간 {Clock(RunState.TimeLeft)}" : $"기록 시간 {Clock(RunState.Elapsed)}";

            string[] labels = dead ? new[] { "처치 수", "획득 전리품", "획득 경험치" } : new[] { "전리품", "경험치", "처치 수" };
            string kills = $"{RunState.Kills:N0}", gold = $"+{RunState.GoldEarned:N0} G", exp = $"+{RunState.ExpEarned:N0}";
            string[] values = dead ? new[] { kills, gold, exp } : new[] { gold, exp, kills };
            for (int i = 0; i < view.BoxCount; i++) { view.BoxLabel(i).text = labels[i]; view.BoxValue(i).text = values[i]; }

            view.Cause.enabled = dead;
            if (dead) view.Cause.text = $"붕괴 원인: {CauseOf(RunState.DeathSource)}의 공격에 시간선이 무너졌다 — 재화는 그대로 남는다";

            string[] statNames = { "준 피해량", "최대 콤보", "받은 피해" };
            string[] statVals = { $"{RunState.DamageDealt:N0}", $"{RunState.MaxCombo:N0}", $"{RunState.DamageTaken:N0}" };
            for (int i = 0; i < view.StatCount; i++) { view.StatLabel(i).text = statNames[i]; view.StatValue(i).text = statVals[i]; }

            // 2026-11-19 챕터 구조 — 그 스테이지 누적 처치(보스 스테이지는 격파 여부)
            int target = ChapterConfig.StageKillTarget;
            bool bossStage = RunState.Stage == ChapterConfig.BossStage;
            int stageKills = bossStage ? (p.IsBossCleared(s) ? target : 0) : Mathf.Min(p.StageKills(s, RunState.Stage), target);
            // 채움 끝을 정수 칸으로 — 100마리 기준이면 1마리 = 1.82칸이라 끝이 반 도트에 걸렸다(2026-11-20)
            view.SetProgress(stageKills / (float)target);
            view.BarText.text = bossStage ? (p.IsBossCleared(s) ? $"{ChapterConfig.StageLabel(s, RunState.Stage)} 격파" : $"{ChapterConfig.StageLabel(s, RunState.Stage)} 도전 중")
                              : $"{ChapterConfig.StageLabel(s, RunState.Stage)} 토벌 {stageKills}/{target}";

            view.ButtonLabel(RunResultScreenView.Slot.Retry).text = dead ? "재동기화" : "다시 입장";
            view.ButtonLabel(RunResultScreenView.Slot.Ok).text = dead ? "성문으로" : "마을로";
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

        void Retry() => StageSelectScreen.TryEnter(Flow, RunState.Region, RunState.Stage); // 같은 스테이지(중간 보스전이었으면 5스테이지)

        void ToLobby() => Flow?.EnterLobby(CommonPopup.AlertIfTransitionFails("로비"));
    }
}
