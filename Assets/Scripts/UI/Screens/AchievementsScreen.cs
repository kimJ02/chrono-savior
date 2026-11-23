using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 업적 — **Figma에 화면이 없다**. 예전 로비 "업적" 탭(원본 업적 탭 :6888)을 공용 창 틀에 옮겼다(로비 왼쪽 아래 "업적" 버튼).
    /// 두 줄로 업적 이름(달성 ★ / 아직 ☆), 고르면 설명 상자. 위에 달성 수와 누적 기록.
    ///
    /// **모양은 프리팹이 원본이다(2026-11-23 프리팹 UI 7호 — 첫 목록형)** — 창·글자 자리·견본 줄·격자 칸 크기·색은 <see cref="AchievementsScreenView"/> 프리팹에서 고친다.
    /// </summary>
    public class AchievementsScreen : PxScreen
    {
        static AchievementsScreen instance;
        bool open;

        public static void Open() { if (instance != null) instance.open = true; }
        public static void Close() { if (instance != null) instance.open = false; }
        public static bool IsOpen => instance != null && instance.open;

        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Popup;
        protected override bool Modal => true;
        protected override bool WantsVisible => open && GameState.Current == GameScene.Lobby;
        protected override NavLayout Layout => NavLayout.Grid;
        protected override int NavColumns => view != null ? view.Columns : 2;

        AchievementsScreenView view;
        AchievementRowView[] rows = new AchievementRowView[0];
        PxTooltip tip;

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

        /// <summary>테스트용 — 만들어진 업적 줄 수(= 업적 수).</summary>
        public int RowCount => rows.Length;

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

        /// <summary>
        /// 모양은 프리팹(<see cref="AchievementsScreenView"/>, `Resources/UI/Views/AchievementsScreenView`)이 원본이다(2026-11-23 프리팹 UI 7호).
        /// 업적 줄은 프리팹 안의 견본 줄을 업적 수만큼 복제해 만들고(데이터 길이만큼 — 동적), 여기선 그 줄들을 키보드 격자(2열)와 닫기에 연결한다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            if (instance == null) instance = this;
            view = LoadView<AchievementsScreenView>(AchievementsScreenView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[AchievementsScreen] 화면 프리팹(Resources/" + AchievementsScreenView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/업적 창 만들기", this);
                view = AchievementsScreenView.CreateFromCode(Root);
            }

            rows = view.BuildRows(Achievements.All.Length);
            for (int i = 0; i < rows.Length; i++)
            {
                var b = rows[i] != null ? rows[i].Wire() : null;
                if (b == null)
                {
                    Debug.LogError("[AchievementsScreen] 화면 프리팹의 견본 줄(List/RowTemplate)이 없거나 버튼이 빠졌다", view);
                    break;
                }
                AddNav(b);
            }
            view.WireClose(Close);
            tip = new PxTooltip(view.Board != null ? view.Board : Root);
        }

        protected override void OnEscape() => Close();

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            view.Title.text = $"업적  {Achievements.Count(p)} / {Achievements.All.Length}";
            view.Summary.text = $"누적 처치 {p.stats.totalKills:N0} · 최고 콤보 {p.stats.maxCombo} · 엘리트 {p.stats.elites} · 성소 {p.stats.shrines}" +
                                $" · 보스 {p.stats.bosses} · 뽑기 {p.stats.pulls}회";
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                var a = Achievements.All[i];
                bool got = p.achieved.Contains(a.id);
                rows[i].Show((got ? "★ " : "☆ ") + a.name, got ? view.GotColor : view.NotColor);
            }
        }

        protected override void OnNavChanged(int index)
        {
            if (tip == null || view == null || index < 0 || index >= rows.Length) return;
            var a = Achievements.All[index];
            tip.Show($"{a.name}\n{a.description}", view.TooltipAnchor(index));
        }
    }
}
