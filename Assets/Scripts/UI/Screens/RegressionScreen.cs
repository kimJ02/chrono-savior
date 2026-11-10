using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 시간 회귀 — Figma "시간 회귀창"(31:4, 640×360): "회귀 결과" 창에 네 칸(깃발·파편·별·해골 아이콘 + 이름 + 큰 숫자).
    /// 라벨은 Figma대로 **최고 스테이지 · 획득 가능한 파편 · 최고 레벨 · 총 처치 수**(9/16판은 스테이지가 없어서
    /// "정복한 지역"·"현재 레벨"로 바꿔 썼다 — 이제 스테이지 선택이 stage 1~9라 Figma 라벨 그대로).
    ///   - 최고 스테이지 = 열린 가장 높은 지역(stage N) · 최고 레벨 = 캐릭터들 중 가장 높은 레벨.
    /// 회귀/취소 버튼은 Figma에 없어서 공용 팝업 버튼으로 창 아래쪽에 넣었다. 동작은 예전 회귀창과 같다
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

        // Figma 좌표(640판)
        static readonly Rect Frame = new Rect(64f, 36f, 512f, 288f);       // 162:2
        static readonly Rect TitleBox = new Rect(267f, 49f, 105f, 21f);    // 162:12 24px
        static readonly Rect SubBox = new Rect(201f, 96f, 237f, 19f);      // 162:15 13px
        static readonly Rect[] Icons =
        {
            new Rect(113f, 115f, 64f, 64f), new Rect(338f, 118f, 64f, 64f),  // 163:16 깃발 · 163:20 시간의 파편
            new Rect(113f, 202f, 64f, 64f), new Rect(338f, 202f, 64f, 64f),  // 165:22 별 · 164:21 해골
        };
        static readonly string[] IconArt = { "Icons/flag", "Icons/shard", "Icons/star", "Icons/skull" };
        static readonly string[] Labels = { "최고 스테이지", "획득 가능한 파편", "최고 레벨", "총 처치 수" };
        static readonly Vector2[] LabelPos = { new Vector2(177f, 133f), new Vector2(402f, 133f), new Vector2(177f, 216f), new Vector2(402f, 216f) };
        static readonly Vector2[] ValuePos = { new Vector2(177f, 144f), new Vector2(402f, 144f), new Vector2(177f, 231f), new Vector2(402f, 231f) };
        const float LabelH = 15f, ValueH = 35f, TextW = 150f;
        const int LabelSize = 10, ValueSize = 24;
        // 버튼(디자인에 없음) — 공용 팝업 버튼 96×32를 창 아래 여백에
        static readonly Rect CancelBox = new Rect(176f, 284f, 96f, 32f);
        static readonly Rect OkBox = new Rect(368f, 284f, 96f, 32f);

        readonly Text[] values = new Text[4];
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

        protected override void Build()
        {
            if (instance == null) instance = this;
            Px.FullFill(Back, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, Px.BackdropAlpha));
            Px.Image(Content, "Frame", PxArt.Get("Regression/frame"), Frame.x, Frame.y, Frame.width, Frame.height).raycastTarget = true;
            Px.Text(Content, "Title", "회귀 결과", TitleBox.x - 40f, TitleBox.y, TitleBox.width + 80f, TitleBox.height, 24, Color.white);
            Px.Text(Content, "Subtitle", "시간을 되감아 더 강한 회차로 넘어 갑니다", SubBox.x - 40f, SubBox.y,
                    SubBox.width + 80f, SubBox.height, 13, Color.white);
            for (int i = 0; i < 4; i++)
            {
                var r = Icons[i];
                Px.Image(Content, "Icon" + i, PxArt.Get(IconArt[i]), r.x, r.y, r.width, r.height);
                Px.Text(Content, "Label" + i, Labels[i], LabelPos[i].x, LabelPos[i].y, TextW, LabelH, LabelSize, Color.white,
                        TextAnchor.MiddleLeft);
                values[i] = Px.Text(Content, "Value" + i, "", ValuePos[i].x, ValuePos[i].y, TextW, ValueH, ValueSize, Color.white,
                                    TextAnchor.MiddleLeft);
            }
            AddNav(Px.TextButton(Content, "Cancel", PxArt.Get("Common/button"), CancelBox.x, CancelBox.y, CancelBox.width,
                                 CancelBox.height, "취소", 13, Color.white, Close));
            ok = AddNav(Px.TextButton(Content, "Ok", PxArt.Get("Common/button"), OkBox.x, OkBox.y, OkBox.width,
                                      OkBox.height, "회귀", 13, Color.white, Regress));
            okLabel = ok.Inner.GetComponentInChildren<Text>();
        }

        protected override void OnEscape() => Close();

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            int gain = p.ShardPreview();
            values[0].text = $"stage {HighestStage(p)}";
            values[1].text = gain.ToString("N0");
            values[2].text = $"Lv.{HighestLevel(p)}";
            values[3].text = p.stats.totalKills.ToString("N0");
            // 2026-11-10 정정 — 던전에 못 들어가게 됐으면(멸망까지 0일) 정복한 지역이 없어도(파편 0)
            // 회귀를 열어준다. 안 그러면 "날짜 다 떨어졌는데 보스도 못 깼다"에서 아무것도 못 하는 소프트락.
            bool stuck = !p.CanEnterStage;
            ok.Interactable = gain >= 1 || stuck;
            okLabel.text = gain >= 1 ? $"회귀 (+{gain})" : stuck ? "회귀 (파편 없음)" : "회귀 불가";
        }

        /// <summary>열린 가장 높은 지역(= 스테이지) 번호.</summary>
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
            int got = p.DoRegression();
            open = false;
            if (got <= 0) return;
            SaveService.Save();
            CommonPopup.Alert("시간 회귀", $"시간의 파편 +{got}\n새 회차가 시작됐다.");
        }
    }
}
