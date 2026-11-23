using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 시간 회귀 — "회귀 결과" 창(드라이브 새 그림 + 사용자 스크린샷 2026-11-22, 창 512×304): 제목판 · 부제 ·
    /// **큰 막대(이전 최고 스테이지 ⟫ 회귀 후 스테이지)** · **큰 막대(획득한 파편)** · **작은 막대 둘(최고 레벨 · 총 처치 수, 왼쪽 아이콘)** · 아래 버튼 둘(취소 · 회귀).
    ///   - 이전 최고 스테이지 = 열린 가장 높은 지역(stage N) · 최고 레벨 = 캐릭터들 중 가장 높은 레벨.
    ///   - 스크린샷 오른쪽 막대는 글자가 "이전 최고 스테이지"로 왼쪽과 같고 값이 stage 9 → stage 11이라 의미를 알 수 없어,
    ///     2026-11-19 챕터 구조부터는 **환생해도 진행이 남는다** — 왼쪽은 가장 멀리 깬 스테이지(예: 2-보스), 오른쪽은 회귀 뒤 이어서 할 스테이지(예: 3-1).
    /// 버튼 글자는 스크린샷에 없어서 예전대로(취소 / 회귀 (+파편)). 동작은 예전과 같다
    /// (`PlayerProfile.DoRegression`, 정복한 지역이 없으면 회귀 불가). 로비의 "환생" 건물에서 연다.
    ///
    /// **모양은 프리팹이 원본이다(2026-11-23 프리팹 UI 4호)** — 위치·크기·그림·글자는 <see cref="RegressionScreenView"/> 프리팹에서 고친다.
    /// </summary>
    public class RegressionScreen : PxScreen
    {
        static RegressionScreen instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;
        public static void Open() { if (instance != null) instance.open = true; }
        public static void Close() { if (instance != null) instance.open = false; }

        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Lobby;
        protected override int DefaultNav => 1;

        // values: 0 이전 최고 스테이지 · 1 회귀 후 스테이지 · 2 획득한 파편 · 3 최고 레벨 · 4 총 처치 수
        readonly Text[] values = new Text[5];
        PxButton ok;
        Text okLabel;

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

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

        /// <summary>
        /// 모양은 프리팹(<see cref="RegressionScreenView"/>, `Resources/UI/Views/RegressionScreenView`)이 원본이다(2026-11-23 프리팹 UI 4호).
        /// 여기선 값 글자 자리·버튼을 받아 동작과 키보드 순서(취소 → 회귀)만 연결한다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            if (instance == null) instance = this;
            var view = LoadView<RegressionScreenView>(RegressionScreenView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[RegressionScreen] 화면 프리팹(Resources/" + RegressionScreenView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/시간 회귀 창 만들기", this);
                view = RegressionScreenView.CreateFromCode(Root);
            }
            if (view.ValueCount != values.Length || view.ButtonCount != 2)
                Debug.LogError($"[RegressionScreen] 화면 프리팹 값 칸 {view.ValueCount}개·버튼 {view.ButtonCount}개 — 값 5개·버튼 2개(취소·회귀)여야 한다", view);

            for (int i = 0; i < values.Length; i++) values[i] = view.Value(i);
            AddNav(view.WireRegressionButton(RegressionScreenView.Slot.Cancel, Close));
            ok = AddNav(view.WireRegressionButton(RegressionScreenView.Slot.Ok, Regress));
            okLabel = view.OkLabel;
        }

        /// <summary>테스트용 — 값 글자(0 이전 최고 스테이지 · 1 회귀 후 스테이지 · 2 획득한 파편 · 3 최고 레벨 · 4 총 처치 수).</summary>
        public string ValueText(int i) => values[i].text;

        protected override void OnEscape() => Close();

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            int gain = p.ShardPreview();
            // 2026-11-19 챕터 구조 — 진행은 환생해도 남는다: 왼쪽 = 가장 멀리 깬 스테이지, 오른쪽 = 회귀 뒤 이어서 할 스테이지
            values[0].text = HighestClearedLabel(p);
            var (nc, ns, _) = StageSelectScreen.NextPlay(p);
            values[1].text = ChapterConfig.StageLabel(nc, ns);
            values[2].text = gain.ToString("N0");
            values[3].text = $"Lv.{HighestLevel(p)}";
            values[4].text = p.stats.totalKills.ToString("N0");
            // 2026-11-10 정정 — 던전에 못 들어가게 됐으면(멸망까지 0일) 정복한 지역이 없어도(파편 0)
            // 회귀를 열어준다. 안 그러면 "날짜 다 떨어졌는데 보스도 못 깼다"에서 아무것도 못 하는 소프트락.
            bool stuck = !p.CanEnterStage;
            ok.Interactable = gain >= 1 || stuck;
            okLabel.text = gain >= 1 ? $"회귀 (+{gain})" : stuck ? "회귀 (파편 없음)" : "회귀 불가";
        }

        /// <summary>열린 가장 높은 지역(= 스테이지) 번호.</summary>
        /// <summary>가장 멀리 깬 스테이지 이름(예: "2-보스", "1-4") — 아무것도 안 깼으면 "-".</summary>
        public static string HighestClearedLabel(PlayerProfile p)
        {
            for (int c = ChapterConfig.ChapterCount; c >= 1; c--)
            {
                if (p.IsBossCleared(c)) return ChapterConfig.StageLabel(c, ChapterConfig.BossStage);
                for (int st = ChapterConfig.StagesPerChapter; st >= 1; st--)
                    if (p.IsStageCleared(c, st)) return ChapterConfig.StageLabel(c, st);
            }
            return "-";
        }

        public static int HighestStage(PlayerProfile p)
        {
            int best = 1;
            for (int r = 1; r <= RegionConfig.Count; r++) if (p.IsRegionOpen(r)) best = r;
            return best;
        }

        /// <summary>캐릭터들 중 가장 높은 레벨(레벨은 캐릭터마다 따로 오른다).</summary>
        public static int HighestLevel(PlayerProfile p)
        {
            int best = 1;
            foreach (CharacterId id in System.Enum.GetValues(typeof(CharacterId))) best = Mathf.Max(best, p.ProgressOf(id).level);
            return best;
        }

        void Regress()
        {
            var p = ProfileService.Current;
            int before = p.regressions;
            int got = p.DoRegression();
            open = false;
            if (p.regressions == before) return; // 회귀가 일어나지 않았다
            // 파편 0개 회귀(멸망 0일에서 정복 없이)도 회귀다 — 바로 저장한다(2026-11-20, 예전엔 0개면 저장을 건너뛰었다)
            SaveService.Save();
            CommonPopup.Alert("시간 회귀", got > 0 ? $"시간의 파편 +{got}\n새 회차가 시작됐다." : "새 회차가 시작됐다.");
        }
    }
}
