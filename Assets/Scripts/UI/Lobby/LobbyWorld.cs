using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 걸어 다니는 로비 — Figma "Page 1"(로비 뼈대): 땅 위에 건물 넷(환생 · 가챠 · 훈련소 · 던전), 캐릭터가 걸어가서 들어간다
    /// (사용자 2026-10-24 "ㅇㅇ 바꾸고"). 원본·예전 로비는 탭 화면이었다.
    ///   - 환생 → 시간 회귀 · 가챠 → 유물 개방 · 훈련소 → 각성창(전문화·원정 강화) · 던전 → 스테이지 선택.
    ///   - **로비는 지금처럼 시간이 멈춘 채(timeScale 0)** 캐릭터만 실제 시간으로 좌우로 걷는다 — 공격·몹·스포너가 전부 "로비에선 시간이
    ///     멈춘다"는 전제로 돌아가서(로비에서 Z를 눌러도 아무것도 안 나가게), 물리 이동 대신 위치만 옮긴다. 점프는 없다.
    ///   - 건물 그림이 아직 없어 Figma 공용 창 틀에 이름·아이콘을 붙인 자리표시로 세웠다(뼈대의 회색 상자 자리).
    ///   - 발판은 로비에서 숨긴다(사냥을 시작하면 다시 켜진다). 들어가기: 건물 앞에서 F 또는 ↑.
    ///   - **한 화면에 다 보인다**(2026-10-28 사용자 "맨 왼쪽에서 나와 던전까지 너무 오래 걸린다, 캐릭터가 너무 작다, 아이콘이 한눈에"):
    ///     건물을 캐릭터 키(0.5)의 약 1.6배로 줄이고 필드 가운데에 모았다. 캐릭터도 가운데서 나오고, 로비에선 카메라가 가운데에
    ///     서 있고(`CameraFollow2D`) 캐릭터는 건물 줄 안에서만 걷는다.
    /// </summary>
    /// <remarks>로비 화면(`LobbyHud`)이 읽는 창구(<see cref="ILobbyView"/>, `WorldView.Lobby`)도 이 컴포넌트다(2026-11-05 2b — 예전엔 정적 `Instance`).</remarks>
    public class LobbyWorld : MonoBehaviour, ILobbyView
    {
        public struct Building { public string Name, Icon; public float Offset; public System.Action Open; }

        /// <summary>건물 자리 — 필드 가운데(<see cref="CenterX"/>)에서 떨어진 거리(유닛). 뼈대의 왼쪽→오른쪽 순서.</summary>
        public static readonly Building[] Buildings =
        {
            new Building { Name = "환생", Icon = "Icons/shard", Offset = -2.7f, Open = RegressionScreen.Open },
            new Building { Name = "가챠", Icon = "Icons/shard_dark", Offset = -0.9f, Open = RelicScreen.Open },
            new Building { Name = "훈련소", Icon = "Hud/icon_attack", Offset = 0.9f, Open = AwakenScreen.Open },
            new Building { Name = "던전", Icon = "Hud/boss_warning", Offset = 2.7f, Open = StageSelectScreen.Open },
        };

        /// <summary>필드 가운데 — 캐릭터가 나오는 곳이자 건물 줄의 가운데. 로비 카메라도 여기 선다.</summary>
        public static float CenterX => (FieldBounds.MinX + FieldBounds.MaxX) * 0.5f;
        public static float XOf(int index) => CenterX + Buildings[index].Offset;
        /// <summary>이만큼 가까우면 건물 앞이다(유닛) — 건물 반폭(0.375) + 캐릭터 반폭쯤.</summary>
        public const float EnterRange = 0.6f;
        /// <summary>로비에서 걸을 수 있는 범위(가운데에서 좌우로) — 맨 끝 건물 조금 바깥까지. 이 안이면 카메라가 가운데에 서 있어도 다 보인다.</summary>
        public const float WalkHalfRange = 3.3f;
        /// <summary>건물 크기(그림 도트, 1도트 = 1/128유닛) — 96×104 = 0.75×0.81유닛, 캐릭터(0.5)의 약 1.6배.</summary>
        public const float BuildingW = 96f, BuildingH = 104f;
        /// <summary>건물 글씨 자리 — 건물 위쪽에서 이만큼(그림 도트) 내려온 곳(이름·"F / ↑").</summary>
        public const float NameFromTop = 15f, DoorFromTop = 83f;

        /// <summary>i번째 건물에서 위쪽으로부터 `fromTop`도트 내려온 곳의 월드 좌표 — 로비 표시가 글씨를 겹쳐 그린다.</summary>
        public static Vector2 LabelWorldPos(int index, float fromTop) =>
            new Vector2(XOf(index), FieldBounds.GroundY + (BuildingH - fromTop) * WorldPx);
        const float WorldPx = 1f / 128f; // 그림 1도트 = 월드 1/128유닛(C안)

        /// <summary>캐릭터가 앞에 서 있는 건물 번호 — 없으면 -1.</summary>
        public int NearIndex { get; private set; } = -1;

        int ILobbyView.NearBuilding => NearIndex;
        Vector2? ILobbyView.AvatarPosition => player != null ? (Vector2)player.position : (Vector2?)null;

        [Tooltip("로비에서 걸어 다닐 캐릭터 — 지금은 같은 씬(20_Combat)의 Player(씬 분할 2차 3단계에서 로비 아바타로 바뀐다).")]
        [SerializeField] Transform player;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다(2026-11-05 2b — 예전엔 Player 태그로 찾았다).</summary>
        public Transform Player { get => player; set => player = value; }

        GameObject root;
        float walkSpeed = 2.7f;
        bool wasLobby;

        void OnEnable() => WorldView.Attach(this);

        void OnDisable()
        {
            WorldView.Detach(this);
            if (cameraAnchor != null) CameraFollow2D.Untrack(cameraAnchor);
        }

        /// <summary>
        /// 로비 카메라가 붙는 자리 — 필드 가운데, 캐릭터가 땅에 섰을 때 높이. 로비는 한 화면이라(건물 넷이 다 보인다, 사용자 2026-10-28) 카메라가 캐릭터를
        /// 따라가지 않고 여기 서 있다(2026-11-05 2b — 예전엔 카메라가 '지금 로비인지'를 직접 보고 가운데에 섰다). 씬 분할 3단계의 로비 아바타도 같은 기준점을 쓴다.
        /// </summary>
        Transform cameraAnchor;

        Transform CameraAnchor()
        {
            if (cameraAnchor == null)
            {
                cameraAnchor = new GameObject("CameraAnchor").transform;
                cameraAnchor.SetParent(transform, false); // 이 오브젝트(월드 씬)의 자식 — 씬을 내리면 같이 사라진다
            }
            cameraAnchor.position = new Vector3(CenterX, FieldBounds.GroundY + EntitySizeConfig.PlayerRadius, 0f); // 지금 무대(일반) 기준
            return cameraAnchor;
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root);
        }

        void Update()
        {
            bool lobby = GameState.Current == GameScene.Lobby;
            if (lobby && !wasLobby) EnterLobby();
            if (!lobby && wasLobby && root != null) root.SetActive(false);
            wasLobby = lobby;
            if (!lobby) { NearIndex = -1; return; }

            if (player == null) return;
            bool windowOpen = UiModal.Open;
            if (!windowOpen)
            {
                int dir = (GameInput.Right ? 1 : 0) - (GameInput.Left ? 1 : 0);
                if (dir != 0)
                {
                    var pos = player.position;
                    pos.x = Mathf.Clamp(pos.x + dir * walkSpeed * Time.unscaledDeltaTime, CenterX - WalkHalfRange, CenterX + WalkHalfRange);
                    player.position = pos;
                    if (player.TryGetComponent(out Rigidbody2D rb)) rb.position = pos; // 다음 물리 단계가 되돌리지 않게
                }
            }
            NearIndex = Nearest(player.position.x);
            if (!windowOpen && NearIndex >= 0 && (GameInput.InteractDown || GameInput.MenuUpDown)) Buildings[NearIndex].Open();
        }

        /// <summary>그 자리에서 들어갈 수 있는 건물(없으면 -1).</summary>
        public static int Nearest(float x)
        {
            int best = -1;
            float bestD = EnterRange;
            for (int i = 0; i < Buildings.Length; i++)
            {
                float d = Mathf.Abs(XOf(i) - x);
                if (d <= bestD) { bestD = d; best = i; }
            }
            return best;
        }


        /// <summary>로비에 들어올 때 — 발판을 숨기고, 건물을 세우고, 캐릭터를 시작 자리 땅 위에.</summary>
        public void EnterLobby()
        {
            if (!ReferenceEquals(WorldView.Lobby, this)) WorldView.Attach(this); // 편집 모드 캡처에선 OnEnable이 안 불린다
            PlatformSet.HideAll();
            if (root == null) Build();
            root.SetActive(true);
            CameraFollow2D.Track(CameraAnchor(), snap: true); // 시간이 멈춘 로비라 바로 붙는다
            if (player != null)
            {
                if (player.TryGetComponent(out ChronoSavior.Characters.CharacterMover2D mover)) walkSpeed = mover.moveSpeed; // 사냥과 같은 걸음
                var pos = new Vector3(CenterX, FieldBounds.GroundY + EntitySizeConfig.PlayerRadius, player.position.z);
                player.position = pos;
                if (player.TryGetComponent(out Rigidbody2D rb)) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
            }
        }

        void Build()
        {
            root = new GameObject("LobbyBuildings");
            root.transform.SetParent(transform, false); // 이 오브젝트(월드 씬)의 자식 — 씬을 내리면 같이 사라진다
            for (int i = 0; i < Buildings.Length; i++) BuildBuilding(Buildings[i], XOf(i));
            root.SetActive(false);
        }

        void BuildBuilding(Building b, float x)
        {
            // 월드 캔버스 하나 = 건물 하나. 1캔버스 단위 = 그림 1도트 = 1/128유닛이라 게임 도트와 같은 크기로 보인다.
            var go = new GameObject("Building_" + b.Name, typeof(RectTransform));
            go.transform.SetParent(root.transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = -50; // 배경(-100) 위, 땅(-5)·캐릭터 아래
            var rt = (RectTransform)go.transform;
            const float w = BuildingW, h = BuildingH;
            rt.sizeDelta = new Vector2(w, h);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.localScale = Vector3.one * WorldPx;
            rt.position = new Vector3(x, FieldBounds.GroundY, 0f);

            // 틀은 9-슬라이스로 줄인다 — 그림째 줄이면 1도트 테두리가 깨진다.
            var facade = Px.Image(rt, "Facade", PxArt.Sliced("Stage/frame_info", 8), 0f, 0f, w, h);
            facade.type = Image.Type.Sliced;
            Px.Image(rt, "Icon", PxArt.Get(b.Icon), w / 2f - 20f, 28f, 40f, 40f);
            // 글씨(이름·"F / ↑")는 여기 안 그린다 — 월드에 12도트로 그리면 화면에서 늘어나 흐렸다(사용자 2026-10-28 "글자가 흐리다").
            // 로비 표시(`LobbyHud`)가 화면 해상도로 이 건물 위에 겹쳐 그린다(<see cref="LabelWorldPos"/>).
        }
    }
}
