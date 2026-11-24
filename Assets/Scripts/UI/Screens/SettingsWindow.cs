using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 설정 창 — 2026-11-16 Figma "설정창"(414:2) 새 판(팀원 zip + Figma 줄 아이콘): 창 틀 512×304 · 제목판 "설정" · X 닫기(40×40) ·
    /// 줄(설정바 480×32: 아이콘 24 + 이름 + ◀ 값 ▶ 또는 음량 막대 + 숫자칸) · 나눔선 · "적용" 버튼(144×32).
    /// 항목은 Figma 다섯 줄 — 화면 모드 · 해상도 · 마스터 볼륨(2026-11-16 새 기능, 효과음·배경음에 곱한다) · 배경음 · 효과음.
    /// 값은 바꾸는 즉시 적용(예전과 같다) — "적용"은 창을 닫는다. 음량은 0~10 그대로 저장하고 0~100으로 보인다(×10), 막대는 눌러 끌 수 있다.
    /// 예전 조작키 안내는 새 디자인에 자리가 없어 일시정지 화면 아래로 옮겼다. 닫기는 X·적용·ESC. 키보드: ↑/↓ 줄 고르기, ←/→ 값 바꾸기.
    ///
    /// **모양은 프리팹이 원본이다(2026-11-23 프리팹 UI 5호)** — 위치·크기·그림·글자·색은 <see cref="SettingsWindowView"/> 프리팹에서 고친다.
    /// </summary>
    public class SettingsWindow : PxScreen
    {
        static SettingsWindow instance;
        bool open;

        public static bool IsOpen => instance != null && instance.open;

        public static void Open()
        {
            if (instance != null) instance.open = true;
        }

        public static void Close()
        {
            if (instance != null) instance.open = false;
        }

        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Popup;
        protected override bool Modal => true;
        protected override bool WantsVisible => open;
        protected override bool TakesKeys => true;

        enum Row { Screen, Resolution, Master, Bgm, Sfx }
        const int Rows = 5, Volumes = 3;
        SettingsWindowView view;
        int row;

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

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
        /// 모양은 프리팹(<see cref="SettingsWindowView"/>, `Resources/UI/Views/SettingsWindowView`)이 원본이다(2026-11-23 프리팹 UI 5호).
        /// 여기선 X·적용(닫기)·◀▶(값 바꾸기)·음량 막대(누르고 끌기)에 동작만 연결한다. 키보드는 아래 <see cref="OnKeys"/>.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            if (instance == null) instance = this;
            view = LoadView<SettingsWindowView>(SettingsWindowView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[SettingsWindow] 화면 프리팹(Resources/" + SettingsWindowView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/설정 창 만들기", this);
                view = SettingsWindowView.CreateFromCode(Root);
            }
            if (view.RowCount != Rows || view.ValueCount != Rows || view.ArrowRowCount != Rows - Volumes || view.VolumeCount != Volumes)
                Debug.LogError($"[SettingsWindow] 화면 프리팹 칸 수가 다르다(줄 {view.RowCount}·값 {view.ValueCount}·◀▶ {view.ArrowRowCount}·음량 {view.VolumeCount}) — " +
                               $"줄 {Rows}·값 {Rows}·◀▶ {Rows - Volumes}·음량 {Volumes}이어야 한다", view);

            view.WireSettings(Close,
                              (r, d) => { row = r; Change(d); },
                              (v, value) => { row = 2 + v; SetVolume((Row)(2 + v), value); },
                              GameSettings.VolumeMax);
        }

        // 지난번에 그린 값 — 그대로면 다시 그리지 않는다(Refresh는 보이는 동안 매 프레임)
        bool shownFullscreen;
        Vector2Int shownRes;
        int shownMaster = -1, shownBgm = -1, shownSfx = -1, shownRow = -1;

        protected override void OnShown() { row = 0; shownRow = -1; }

        protected override void OnHidden() => GameSettings.Save();

        protected override void OnEscape() => Close();

        protected override void OnKeys()
        {
            if (GameInput.MenuUpDown) row = (row + Rows - 1) % Rows;
            if (GameInput.MenuDownDown) row = (row + 1) % Rows;
            if (GameInput.MenuLeftDown) Change(-1);
            if (GameInput.MenuRightDown) Change(+1);
        }

        void Change(int d)
        {
            switch ((Row)row)
            {
                case Row.Screen:
                    GameSettings.Fullscreen = !GameSettings.Fullscreen;
                    break;
                case Row.Resolution:
                    var list = GameSettings.Resolutions();
                    int i = list.IndexOf(GameSettings.Resolution);
                    if (i < 0) i = 0;
                    GameSettings.Resolution = list[(i + d + list.Count) % list.Count];
                    break;
                case Row.Master:
                    GameSettings.MasterVolume += d;
                    break;
                case Row.Bgm:
                    GameSettings.BgmVolume += d;
                    break;
                case Row.Sfx:
                    GameSettings.SfxVolume += d;
                    break;
            }
        }

        void SetVolume(Row r, int value)
        {
            if (r == Row.Master) GameSettings.MasterVolume = value;
            else if (r == Row.Bgm) GameSettings.BgmVolume = value;
            else if (r == Row.Sfx) GameSettings.SfxVolume = value;
        }

        /// <summary>음량 0~10을 화면 숫자 0~100으로(Figma "50").</summary>
        public static int VolumeShown(int volume) => volume * 100 / GameSettings.VolumeMax;

        protected override void Refresh()
        {
            bool fs = GameSettings.Fullscreen;
            var res = GameSettings.Resolution;
            int m = GameSettings.MasterVolume, b = GameSettings.BgmVolume, s = GameSettings.SfxVolume;
            if (fs == shownFullscreen && res == shownRes && m == shownMaster && b == shownBgm && s == shownSfx && row == shownRow) return;
            shownFullscreen = fs; shownRes = res; shownMaster = m; shownBgm = b; shownSfx = s; shownRow = row;

            view.Value(0).text = fs ? "전체 화면" : "창 모드";
            view.Value(1).text = $"{res.x} X {res.y}";
            ShowVolume(0, m);
            ShowVolume(1, b);
            ShowVolume(2, s);
            for (int i = 0; i < view.RowCount; i++) view.Label(i).color = i == row ? view.SelectedColor : view.NormalColor;
        }

        void ShowVolume(int v, int volume)
        {
            view.Value(2 + v).text = VolumeShown(volume).ToString();
            view.ShowVolume(v, volume / (float)GameSettings.VolumeMax); // 손잡이 가운데가 채움 끝에 오게, 막대 밖으로는 안 나가게
        }

        /// <summary>테스트용 — 음량 막대 손잡이 왼쪽 x(2026-11-23부터 줄 안 좌표 — 예전엔 판 좌표)와 숫자.</summary>
        public float KnobXForTest(int v) => view.KnobX(v);
        public string ValueTextForTest(int i) => view.Value(i).text;
    }
}
