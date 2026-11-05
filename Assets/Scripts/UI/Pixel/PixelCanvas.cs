using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// Figma UI(640×360 도트)를 그리는 판 — 2026-10-24 사용자 "Figma UI 13페이지 다 넣을 거야".
    ///
    /// uGUI 캔버스 하나를 화면 전체에 깔고, 화면마다 가운데 640×360 판(<see cref="PxScreen.Content"/>)을 둔다.
    /// 판은 **화면을 꽉 채우게** 키운다(<see cref="ScaleFor"/>) — 표준 해상도(720p ×2, 1080p ×3, 1440p ×4)에선 딱 정수배라
    /// Figma 그림이 도트 그대로 보이고, 그 밖의 창 크기에선 소수배. 16:9가 아니면 판은 가운데, 남는 곳은 화면별 배경(어두운 막 등)만 덮는다.
    /// 글자는 번짐 없이(글꼴 가져오기 설정 Hinted Raster) 실제 화면 크기로 그려진다.
    ///
    /// 예전 IMGUI 화면(`UiTheme.Scaled`)은 글자를 설계 크기로 만든 뒤 키워서 흐렸고(사용자 "화질이 구리다"), 배치 모드에서
    /// 캡처할 수 없어 Figma와 나란히 비교할 수 없었다. uGUI 글자는 실제 화면 크기로 그려져 선명하고 캡처도 된다.
    ///
    /// **씬 원본**: 게임에선 `00_Persistent` 씬에 이 판(캔버스·모든 화면 컴포넌트)이 놓여 있다(2026-11-04 씬 분할).
    /// 화면 내용(버튼·글자)은 Figma 좌표대로 코드가 그린다 — 화면 컴포넌트는 씬에 있고, 그 안의 그림만 런타임에 만든다.
    /// 테스트·캡처처럼 씬이 없는 곳에서만 <see cref="Create"/>로 판을 통째로 만든다.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class PixelCanvas : MonoBehaviour
    {
        public const float Width = 640f, Height = 360f;

        public static PixelCanvas Instance { get; private set; }

        public Canvas Canvas { get; private set; }
        public CanvasScaler Scaler { get; private set; }

        /// <summary>층 — 앞에 적은 것이 아래.</summary>
        public enum Layer { Hud, Window, Popup, Fx }
        readonly RectTransform[] layers = new RectTransform[4];

        /// <summary>화면 크기 대신 이 배율을 쓴다(캡처용).</summary>
        public float? FixedScale;

        [Tooltip("같은 00_Persistent 씬의 GameFlow. 화면은 이 연결로만 게임 흐름(사냥 시작·로비·일시정지)을 부른다 — 씬을 뒤져 찾지 않는다.")]
        [SerializeField] GameFlow flow;

        /// <summary>화면들이 부르는 게임 흐름. 테스트·캡처처럼 GameFlow가 없는 판에선 null이다.</summary>
        public GameFlow Flow { get => flow; set => flow = value; }

        /// <summary>
        /// 씬이 없는 곳(테스트·캡처)에서 판을 만들고 Figma 화면을 전부 붙인다(<see cref="PxScreens"/>). 캔버스 컴포넌트를 **먼저** 붙이고
        /// 판을 붙인다 — 씬의 판과 똑같은 모양이 되고, <see cref="Init"/>은 어느 쪽에서도 컴포넌트를 만들지 않는다.
        /// </summary>
        public static PixelCanvas Create(Camera captureCamera = null)
        {
            var go = new GameObject("PixelUI");
            Configure(go.AddComponent<Canvas>(), go.AddComponent<CanvasScaler>());
            go.AddComponent<GraphicRaycaster>();
            var pc = go.AddComponent<PixelCanvas>();
            pc.Init(); // 편집 모드(캡처)에선 Awake가 안 불린다
            EnsureEventSystem();
            if (captureCamera != null) pc.UseCamera(captureCamera);
            PxScreens.AddAll(go);
            return pc;
        }

        void Awake() => Init();

        /// <summary>
        /// 층을 준비한다. 캔버스 컴포넌트(Canvas·CanvasScaler·GraphicRaycaster)는 **만들지 않는다** — 게임에선 씬(`00_Persistent/PixelUI`)에
        /// 놓여 있고(값은 씬이 원본, SceneSetupValidator가 <see cref="Configure"/>와 맞춰 본다), 테스트·캡처에선 <see cref="Create"/>가 먼저 붙인다.
        /// 빠져 있으면 조용히 보충하지 않고 크게 알린 뒤 멈춘다 — 보충하면 씬이 틀린 걸 아무도 모른다.
        /// </summary>
        public void Init()
        {
            if (Canvas != null) return;
            Instance = this;
            Canvas = GetComponent<Canvas>();
            Scaler = GetComponent<CanvasScaler>();
            if (Canvas == null || Scaler == null || !TryGetComponent(out raycaster))
            {
                Debug.LogError("[PixelCanvas] 판(PixelUI)에 Canvas·CanvasScaler·GraphicRaycaster 중 빠진 게 있다 — 메뉴 ChronoSavior/씬 구성 검증", this);
                enabled = false;
                return;
            }
            for (int i = 0; i < layers.Length; i++) layers[i] = Px.Stretch(transform, ((Layer)i).ToString());
            ApplyScale();
        }

        /// <summary>판의 캔버스 설정 — 씬의 판(`00_Persistent/PixelUI`)도 이 값이어야 한다(SceneSetupValidator가 맞춰 본다).</summary>
        public static void Configure(Canvas canvas, CanvasScaler scaler)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;
            canvas.pixelPerfect = true; // 도트를 화면 픽셀에 맞춘다
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referencePixelsPerUnit = 100f; // UI 그림 픽셀/유닛 100 = 캔버스 1단위가 그림 1도트
        }

        public const int SortingOrder = 100;

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        GraphicRaycaster raycaster;

        /// <summary>
        /// 화면이 입력(키·마우스)을 받는가 — 화면을 옮기는 동안(씬 불러오기 중)엔 받지 않는다(2026-11-05 씬 분할 2차 4-0).
        /// 흐름은 전환 중 요청을 거부하지만, 프로필을 직접 바꾸는 버튼(캐릭터 선택 등)까지 막으려면 입력 자체를 닫아야 한다.
        /// 키는 `PxScreen.HandleKeys`, 마우스는 이 판의 `GraphicRaycaster`가 이 값을 본다.
        /// </summary>
        public static bool AcceptsInput => !GameState.IsTransitioning;

        void Update()
        {
            ApplyScale();
            if (raycaster != null) raycaster.enabled = AcceptsInput;
        }

        public RectTransform LayerRoot(Layer layer) => layers[(int)layer];

        /// <summary>
        /// 그 화면 크기에서 쓸 배율 — **화면을 꽉 채운다**. 표준 해상도(720p ×2, 1080p ×3, 1440p ×4)는 딱 정수배라 도트가 고르고,
        /// 그 밖의 창(에디터 Game 창 등)은 소수배. 예전엔 정수배로 내려서 857×482 창에선 1배(640×360)로 작게 그려졌고
        /// 작은 글자가 뭉개져 보였다(2026-10-25 사용자 "UI 창들이 다 흐릿하다").
        /// </summary>
        public static float ScaleFor(float screenW, float screenH)
        {
            float fit = Mathf.Min(screenW / Width, screenH / Height);
            // 정수배에 아주 가까우면 정수배로 — 작게 맞출 땐 가장자리가 조금(2% 안) 비고, 크게 맞출 땐 많아야 2픽셀 넘친다.
            float whole = Mathf.Round(fit);
            if (whole >= 1f && Mathf.Abs(fit - whole) < 0.02f && whole * Width <= screenW + 2f && whole * Height <= screenH + 2f)
                return whole;
            return fit;
        }

        void ApplyScale()
        {
            float s = FixedScale ?? ScaleFor(Screen.width, Screen.height);
            if (s > 0f && !Mathf.Approximately(Scaler.scaleFactor, s)) Scaler.scaleFactor = s;
        }

        /// <summary>배치 모드 캡처용 — 카메라가 그리는 그림 위에 겹쳐 그린다.</summary>
        public void UseCamera(Camera cam)
        {
            Canvas.renderMode = RenderMode.ScreenSpaceCamera;
            Canvas.worldCamera = cam;
            Canvas.planeDistance = 1f;
        }

        /// <summary>게임에선 `00_Persistent`에 EventSystem이 놓여 있다. 씬이 없는 테스트·캡처(<see cref="Create"/>)에서만 여기서 만든다.</summary>
        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    /// <summary>
    /// 판에 붙는 Figma 화면 목록 — 화면을 새로 만들면 여기 한 줄 더한다. 게임 씬(`00_Persistent`)의 판에도 같은 목록이 놓여 있어야 하며,
    /// `Editor/SceneSetupValidator`가 둘이 같은지 검사한다. 월드 쪽(로비 건물 `LobbyWorld`)과 소리(`HitSound`)는 UI가 아니라 여기 없다.
    /// </summary>
    public static class PxScreens
    {
        public static void AddAll(GameObject canvas)
        {
            foreach (var type in All) canvas.AddComponent(type);
        }

        /// <summary>판에 붙는 화면 종류 — 씬 검증(`Editor/SceneSetupValidator`)이 씬의 판과 이 목록을 맞춰 본다.</summary>
        public static readonly System.Type[] All =
        {
            typeof(CombatHud), typeof(PauseMenu), typeof(SettingsWindow), typeof(TitleMenu), typeof(RegressionScreen),
            typeof(RelicScreen), typeof(AwakenScreen), typeof(StageSelectScreen), typeof(CharacterSelectScreen),
            typeof(RunResultScreen), typeof(LobbyHud), typeof(AchievementsScreen), typeof(CommonPopup),
        };
    }
}
