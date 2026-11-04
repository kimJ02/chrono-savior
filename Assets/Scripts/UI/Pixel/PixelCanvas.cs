using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
    /// 씬을 다시 굽지 않게(`BuildPartAScene`) 게임 씬이 열리면 스스로 만든다(<see cref="Install"/>).
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            // 게임 씬에서만 — 테스트가 쓰는 빈 씬엔 플레이어가 없다.
            if (Instance != null || GameObject.FindGameObjectWithTag("Player") == null) return;
            Create();
        }

        /// <summary>판을 만들고 Figma 화면을 전부 붙인다(<see cref="PxScreens"/>).</summary>
        public static PixelCanvas Create(Camera captureCamera = null)
        {
            var go = new GameObject("PixelUI");
            var pc = go.AddComponent<PixelCanvas>();
            pc.Init();
            if (captureCamera != null) pc.UseCamera(captureCamera);
            PxScreens.AddAll(go);
            return pc;
        }

        void Awake() => Init();

        /// <summary>캔버스·층을 만든다. 편집 모드(캡처)에선 Awake가 안 불려서 <see cref="Create"/>가 직접 부른다.</summary>
        public void Init()
        {
            if (Canvas != null) return;
            Instance = this;
            Canvas = gameObject.AddComponent<Canvas>();
            Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            Canvas.sortingOrder = 100;  // 예전 IMGUI(OnGUI)는 그 위에 그려진다 — 바꾼 화면은 IMGUI 쪽을 끈다
            Canvas.pixelPerfect = true; // 도트를 화면 픽셀에 맞춘다
            Scaler = gameObject.AddComponent<CanvasScaler>();
            Scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            Scaler.referencePixelsPerUnit = 100f; // UI 그림 픽셀/유닛 100 = 캔버스 1단위가 그림 1도트
            gameObject.AddComponent<GraphicRaycaster>();
            for (int i = 0; i < layers.Length; i++) layers[i] = Px.Stretch(transform, ((Layer)i).ToString());
            EnsureEventSystem();
            ApplyScale();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update() => ApplyScale();

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

        static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    /// <summary>판에 붙는 Figma 화면 목록 — 화면을 새로 만들면 여기 한 줄 더한다.</summary>
    public static class PxScreens
    {
        public static void AddAll(GameObject canvas)
        {
            canvas.AddComponent<CombatHud>();
            canvas.AddComponent<PauseMenu>();
            canvas.AddComponent<SettingsWindow>();
            canvas.AddComponent<TitleMenu>();
            canvas.AddComponent<RegressionScreen>();
            canvas.AddComponent<RelicScreen>();
            canvas.AddComponent<AwakenScreen>();
            canvas.AddComponent<StageSelectScreen>();
            canvas.AddComponent<CharacterSelectScreen>();
            canvas.AddComponent<RunResultScreen>();
            canvas.AddComponent<LobbyWorld>();
            canvas.AddComponent<LobbyHud>();
            canvas.AddComponent<AchievementsScreen>();
            canvas.AddComponent<CommonPopup>();
            canvas.AddComponent<HitSound>(); // 적 타격 소리(Kenney footstep_carpet_002)
        }

        /// <summary>그 Figma 화면이 들어와 있는가 — 예전 IMGUI 화면이 같은 화면을 또 그리지 않게 묻는다.</summary>
        public static bool Has<T>() where T : PxScreen =>
            PixelCanvas.Instance != null && PixelCanvas.Instance.GetComponent<T>() != null;
    }
}
