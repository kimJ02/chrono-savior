using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 업적 — **Figma에 화면이 없다**. 예전 로비 "업적" 탭(원본 업적 탭 :6888)을 공용 창 틀에 옮겼다(로비 왼쪽 아래 "업적" 버튼).
    /// 두 줄로 업적 이름(달성 ★ / 아직 ☆), 고르면 설명 상자. 위에 달성 수와 누적 기록.
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
        protected override int NavColumns => 2;

        static readonly Rect Frame = new Rect(64f, 36f, 512f, 288f);
        const float ListX = 96f, ListY = 112f, ColW = 224f, RowH = 11f;
        static readonly Color Got = Px.Hex(0xfac70e);

        Text title, summary;
        readonly Text[] names = new Text[Achievements.All.Length];
        PxTooltip tip;

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
            Px.FullFill(Back, "Backdrop", new Color(Px.Backdrop.r, Px.Backdrop.g, Px.Backdrop.b, 0.5f));
            Px.Image(Content, "Frame", PxArt.Get("Common/window_frame"), Frame.x, Frame.y, Frame.width, Frame.height).raycastTarget = true;
            title = Px.Text(Content, "Title", "업적", 220f, 56f, 200f, 22f, 16, Color.white);
            summary = Px.Text(Content, "Summary", "", 96f, 96f, 448f, 12f, 8, Color.white, TextAnchor.MiddleLeft);
            for (int i = 0; i < names.Length; i++)
            {
                float x = ListX + (i % 2) * ColW, y = ListY + (i / 2) * RowH; // 두 줄씩(방향키 격자와 같은 순서)
                var plate = Px.Fill(Content, "Row" + i, Color.clear, x, y, ColW - 8f, RowH);
                var inner = Px.Stretch(plate.transform, "Inner");
                var b = plate.gameObject.AddComponent<PxButton>();
                b.Init(plate, inner);
                names[i] = Px.Text(inner, "Name", "", 0f, 0f, ColW - 8f, RowH, 8, Color.white, TextAnchor.MiddleLeft);
                AddNav(b);
            }
            Px.IconButton(Content, "Close", PxArt.Get("Common/button_small"), PxArt.Get("Icons/close"), 536f, 54f, 24f, 24f,
                          2f, 2f, 19f, 19f, Close);
            tip = new PxTooltip(Content);
        }

        protected override void OnEscape() => Close();

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            title.text = $"업적  {Achievements.Count(p)} / {Achievements.All.Length}";
            summary.text = $"누적 처치 {p.stats.totalKills:N0} · 최고 콤보 {p.stats.maxCombo} · 엘리트 {p.stats.elites} · 성소 {p.stats.shrines}" +
                           $" · 보스 {p.stats.bosses} · 뽑기 {p.stats.pulls}회";
            for (int i = 0; i < names.Length; i++)
            {
                var a = Achievements.All[i];
                bool got = p.achieved.Contains(a.id);
                names[i].text = (got ? "★ " : "☆ ") + a.name;
                names[i].color = got ? Got : new Color(1f, 1f, 1f, 0.7f);
            }
        }

        protected override void OnNavChanged(int index)
        {
            if (tip == null || index < 0 || index >= names.Length) return;
            var a = Achievements.All[index];
            float x = ListX + (index % 2) * ColW, y = ListY + (index / 2) * RowH;
            tip.Show($"{a.name}\n{a.description}", new Rect(x, y + RowH, ColW - 60f, 0f));
        }
    }
}
