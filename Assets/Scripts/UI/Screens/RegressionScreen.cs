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

        // 스크린샷에서 잰 값(640판 환산, ±1도트) — 창 왼쪽 위(W) 기준 상대 좌표
        static readonly Vector2 W = new Vector2(64f, 36f);               // 창 512×304(Common/window_tall)
        static readonly Rect Plate = new Rect(192f, -15f, 128f, 32f);    // 제목판(Common/title_plate)
        static readonly Rect StageBar = new Rect(54f, 52f, 400f, 48f);   // 정보창 large
        static readonly Rect ShardBar = new Rect(54f, 110f, 400f, 48f);
        static readonly Rect LevelBar = new Rect(54f, 167f, 184f, 48f);  // 정보창 small
        static readonly Rect KillBar = new Rect(270f, 167f, 184f, 48f);
        static readonly Rect CancelBox = new Rect(95f, 236f, 96f, 32f);  // 회귀창 버튼
        static readonly Rect OkBox = new Rect(321f, 236f, 96f, 32f);
        const float LabelH = 14f, ValueH = 24f;

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

        /// <summary>막대 안(막대 왼쪽 위 기준) 가운데 (cx, cy)에 글자 한 줄.</summary>
        Text Line(Rect bar, string name, string text, float cx, float cy, float w, float h, int size)
        {
            return Px.Text(Content, name, text, W.x + bar.x + cx - w / 2f, W.y + bar.y + cy - h / 2f, w, h, size, Color.white);
        }

        protected override void Build()
        {
            if (instance == null) instance = this;
            Px.FullFill(Back, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, Px.BackdropAlpha));
            Px.Image(Content, "Frame", PxArt.Get("Common/window_tall"), W.x, W.y, 512f, 304f).raycastTarget = true;
            Px.Image(Content, "TitlePlate", PxArt.Get("Common/title_plate"), W.x + Plate.x, W.y + Plate.y, Plate.width, Plate.height);
            Px.Text(Content, "Title", "회귀 결과", W.x + Plate.x, W.y + Plate.y, Plate.width, Plate.height, 24, Color.white);
            Px.Text(Content, "Subtitle", "시간을 되감아 더 강한 회차로 넘어 갑니다", W.x, W.y + 22f, 512f, 20f, 13, Color.white);

            // 큰 막대 — 이전 최고 스테이지 ⟫ 회귀 후 스테이지
            Px.Image(Content, "StageBar", PxArt.Get("Regression/info_large"), W.x + StageBar.x, W.y + StageBar.y, StageBar.width, StageBar.height);
            Line(StageBar, "LabelFrom", "이전 최고 스테이지", 82f, 8f, 160f, LabelH, 11);
            values[0] = Line(StageBar, "ValueFrom", "", 82f, 33f, 160f, ValueH, 20);
            Line(StageBar, "Arrow", ">>>", 200f, 24f, 60f, 24f, 20);
            Line(StageBar, "LabelTo", "회귀 후 스테이지", 320f, 8f, 160f, LabelH, 11);
            values[1] = Line(StageBar, "ValueTo", "", 320f, 33f, 160f, ValueH, 20);

            // 큰 막대 — 획득한 파편
            Px.Image(Content, "ShardBar", PxArt.Get("Regression/info_large"), W.x + ShardBar.x, W.y + ShardBar.y, ShardBar.width, ShardBar.height);
            Line(ShardBar, "LabelShard", "획득한 파편", 200f, 9f, 160f, LabelH, 11);
            values[2] = Line(ShardBar, "ValueShard", "", 200f, 31f, 200f, ValueH, 20);

            // 작은 막대 둘 — 왼쪽 아이콘(32×32 1:1), 위 이름, 아래 값
            SmallBar(LevelBar, "Level", "Icons/star", "최고 레벨", 3);
            SmallBar(KillBar, "Kill", "Icons/skull", "총 처치 수", 4);

            AddNav(Px.TextButton(Content, "Cancel", PxArt.Get("Regression/button"), W.x + CancelBox.x, W.y + CancelBox.y, CancelBox.width,
                                 CancelBox.height, "취소", 13, Color.white, Close));
            ok = AddNav(Px.TextButton(Content, "Ok", PxArt.Get("Regression/button"), W.x + OkBox.x, W.y + OkBox.y, OkBox.width,
                                      OkBox.height, "회귀", 13, Color.white, Regress));
            okLabel = ok.Inner.GetComponentInChildren<Text>();
        }

        void SmallBar(Rect bar, string key, string icon, string label, int valueIndex)
        {
            Px.Image(Content, key + "Bar", PxArt.Get("Regression/info_small"), W.x + bar.x, W.y + bar.y, bar.width, bar.height);
            Px.Image(Content, key + "Icon", PxArt.Get(icon), W.x + bar.x + 6f, W.y + bar.y + 8f, 32f, 32f);
            Line(bar, key + "Label", label, 108f, 9f, 130f, LabelH + 2f, 13);
            values[valueIndex] = Line(bar, key + "Value", "", 112f, 31f, 130f, ValueH, 20);
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
            // 새 회차 — 캐릭터를 다시 고른다(2026-11-22 사용자: 환생할 때마다 캐릭터 선택창, 이번 회차엔 못 바꾼다)
            CommonPopup.Alert("시간 회귀", got > 0 ? $"시간의 파편 +{got}\n새 회차가 시작됐다." : "새 회차가 시작됐다.", CharacterSelectScreen.OpenForRun);
        }
    }
}
