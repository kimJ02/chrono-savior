using UnityEngine;
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
    ///   - **건물·배경 그림은 사용자가 올린 로비 그림 7장**(2026-11-06, <see cref="Buildings"/>·<see cref="BuildBackground"/>) — 건물 넷(환생=우주
    ///     성당, 가챠=수정 분수, 훈련소=과녁 사격장, 던전=해골 게이트) + 배경 3겹(하늘·먼 산·가까운 산). **전부 `SpriteRenderer`·128 PPU·배율 1**로
    ///     통일했다(2026-11-06 팀 지적 — 건물은 UI 캔버스(100 PPU)였고 배경은 Transform 배율 3이라 그림마다 규격이 따로였다). 배경 그림은 그래서
    ///     룰: **PPU를 맞추고 Transform 배율은 건드리지 않는다.** 배경 그림 크기는 **점 보간 2배(640×360→1280×720, 배율은 1 그대로)** —
    ///     3배(1920×1080, 전투 배경 베낌, "작업환경을 크게 잡았다" 지적) → 2배 → 팀 요청으로 원본 640×360(배율 1)까지 줄여봤는데, 원본은
    ///     화면(7.5×4.22유닛)보다 작아서(5×2.8125유닛) 가장자리에 빈 틈이 생겼다(그 틈에 비치던 `StageVisuals`의 구버전 숲 배경도 2026-11-08에
    ///     꺼버려서 — `StageVisuals.IncludeBackground` — 이젠 틈이 그냥 빈 화면으로 보인다). 그래서 **다시 2배로** — 화면을 여유 있게(가로 1.25·
    ///     세로 0.7유닛) 덮으면서도 정수 배라 도트는 고르다. ⚠️ 땅 타일(배율 2, PPU 64)과는 도트 굵기가 맞지만, 건물(배율 1)·캐릭터(1.33)보다는
    ///     점이 굵게 보인다 — 화면을 덮으려면 어쩔 수 없는 트레이드오프(2026-11-08 사용자와 합의, "배경만 2배").
    ///     건물은 이전엔 Figma 공용 창 틀에 이름·아이콘만 붙인 자리표시였다(뼈대의 회색 상자 자리) — 이제 실제 그림을 그대로 쓴다.
    ///   - 로비는 자기 씬(`10_Lobby`)이라 발판이 없다(2026-11-05 씬 분할 2차 3단계 — 예전엔 전투 씬의 발판을 숨겼다). 들어가기: 건물 앞에서 F 또는 ↑.
    ///   - **씬이 켜지면 스스로 준비한다**(<see cref="EnterLobby"/>, `OnEnable`) — 흐름(`GameFlow`, 3층)은 이 화면(4층)을 모르므로 부르지 않는다.
    ///     전환이 실패해 멈췄던 로비가 다시 켜져도 같은 준비를 한 번 더 할 뿐이다(건물은 한 번만 세우고, 캐릭터 시작 자리도 처음 한 번만).
    ///   - **한 화면에 다 보인다**(2026-10-28 사용자 "맨 왼쪽에서 나와 던전까지 너무 오래 걸린다, 캐릭터가 너무 작다, 아이콘이 한눈에"):
    ///     건물을 캐릭터 키(0.5)의 약 2배로 모았다(아래 <see cref="BuildingSize"/>). 캐릭터도 가운데서 나오고, 로비에선 카메라가 가운데에
    ///     서 있고(`CameraFollow2D`) 캐릭터는 건물 줄 안에서만 걷는다.
    /// </summary>
    /// <remarks>로비 화면(`LobbyHud`)이 읽는 창구(<see cref="ILobbyView"/>, `WorldView.Lobby`)도 이 컴포넌트다(2026-11-05 2b — 예전엔 정적 `Instance`).</remarks>
    public class LobbyWorld : MonoBehaviour, ILobbyView
    {
        public struct Building { public string Name, Art; public float Offset; public float BottomPadPx; public System.Action Open; }

        /// <summary>로비 그림(건물·배경) 자리 — `Assets/Resources/Art/Figma/Lobby/`(UI 아님·128 PPU·`FigmaArtImporter`). 가운데 피벗.</summary>
        public const string ArtFolder = "Art/Figma/Lobby/";

        /// <summary>건물 자리 — 필드 가운데(<see cref="CenterX"/>)에서 떨어진 거리(유닛). 뼈대의 왼쪽→오른쪽 순서.
        /// <see cref="Building.Art"/>는 <see cref="ArtFolder"/> 아래 파일 이름(확장자 빼고). <see cref="Building.BottomPadPx"/>는
        /// 2026-11-08 측정값 — 아래 <see cref="BuildBuilding"/> 참고.</summary>
        public static readonly Building[] Buildings =
        {
            new Building { Name = "환생", Art = "building_regression", Offset = -2.7f, BottomPadPx = 6, Open = RegressionScreen.Open },
            new Building { Name = "가챠", Art = "building_gacha", Offset = -0.9f, BottomPadPx = 7, Open = RelicScreen.Open },
            new Building { Name = "훈련소", Art = "building_awaken", Offset = 0.9f, BottomPadPx = 6, Open = AwakenScreen.Open },
            new Building { Name = "던전", Art = "building_dungeon", Offset = 2.7f, BottomPadPx = 6, Open = StageSelectScreen.Open },
        };

        /// <summary>필드 가운데 — 캐릭터가 나오는 곳이자 건물 줄의 가운데. 로비 카메라도 여기 선다.</summary>
        public static float CenterX => (FieldBounds.MinX + FieldBounds.MaxX) * 0.5f;
        public static float XOf(int index) => CenterX + Buildings[index].Offset;
        /// <summary>이만큼 가까우면 건물 앞이다(유닛) — 건물 반폭(0.5) + 캐릭터 반폭쯤.</summary>
        public const float EnterRange = 0.75f;
        /// <summary>로비에서 걸을 수 있는 범위(가운데에서 좌우로) — 맨 끝 건물 조금 바깥까지. 이 안이면 카메라가 가운데에 서 있어도 다 보인다.</summary>
        public const float WalkHalfRange = 3.3f;
        /// <summary>건물 그림 크기(128×128px · 128 PPU · 배율 1 그대로 = 1×1유닛, 캐릭터(0.5)의 2배) — 2026-11-06 사용자가 올린 실제 그림.
        /// 자리표시(96×104)보다 커졌지만, 줄여서 그리면 픽셀 밀도가 캐릭터·배경과 어긋난다(중력법사 스킬이펙트와 같은 문제라 피했다).</summary>
        public const float BuildingSize = 1f;
        /// <summary>건물 글씨 자리 — 건물 위쪽에서 이만큼(그림 도트) 내려온 곳(이름·"F / ↑"). 이름은 96×104 자리표시 때 비율(15/104)을
        /// 128×128 실제 그림에 그대로 맞췄다. **"F / ↑"는 2026-11-08에 내렸다** — 원래 102(문 높이 흉내)였는데 건물 중간쯤이라 그림에
        /// 묻혀서 가독성이 떨어진다는 지적(사용자) — 건물마다 문 위치가 달라 "문"에 맞추는 대신, 아예 바닥 타일 바로 위(118 = 아래에서
        /// 10px)로 통일해서 어느 건물이든 땅 가까이 또렷하게 뜨게 했다.</summary>
        public const float NameFromTop = 18f, DoorFromTop = 118f;
        const float BuildingPx = 128f; // 건물 그림 한 변(도트) — 글씨 자리 계산용

        /// <summary>배경(-100, `StageVisuals`가 재사용하던 숲 그림) 위·건물(-50) 아래 — 가까운 산이 제일 앞.</summary>
        public const int SkyOrder = -99, MountainsFarOrder = -98, MountainsNearOrder = -97;

        /// <summary>i번째 건물에서 위쪽으로부터 `fromTop`도트 내려온 곳의 월드 좌표 — 로비 표시가 글씨를 겹쳐 그린다.</summary>
        public static Vector2 LabelWorldPos(int index, float fromTop) =>
            new Vector2(XOf(index), FieldBounds.GroundY + (BuildingPx - fromTop) / BuildingPx);

        /// <summary>캐릭터가 앞에 서 있는 건물 번호 — 없으면 -1.</summary>
        public int NearIndex { get; private set; } = -1;

        int ILobbyView.NearBuilding => NearIndex;
        Vector2? ILobbyView.AvatarPosition => player != null ? (Vector2)player.position : (Vector2?)null;

        [Tooltip("로비에서 걸어 다닐 캐릭터 — 같은 씬(10_Lobby)의 로비 아바타(LobbyAvatar).")]
        [SerializeField] Transform player;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다(2026-11-05 2b — 예전엔 Player 태그로 찾았다).</summary>
        public Transform Player { get => player; set => player = value; }

        GameObject root;
        Transform background;
        bool placed;

        void OnEnable() => EnterLobby();

        void OnDisable()
        {
            WorldView.Detach(this);
            if (cameraAnchor != null) CameraFollow2D.Untrack(cameraAnchor);
        }

        /// <summary>
        /// 로비 카메라가 붙는 자리 — 필드 가운데, 캐릭터가 땅에 섰을 때 높이. 로비는 한 화면이라(건물 넷이 다 보인다, 사용자 2026-10-28) 카메라가 캐릭터를
        /// 따라가지 않고 여기 서 있다(2026-11-05 2b — 예전엔 카메라가 '지금 로비인지'를 직접 보고 가운데에 섰다).
        /// 10_Lobby에 이 오브젝트의 자식으로 놓여 있다(2026-11-05 생성 책임 2-1 — 예전엔 실행 중에 만들었다). 위치는 준비할 때 지금 무대 기준으로 맞춘다.
        /// </summary>
        [Tooltip("같은 씬(10_Lobby)의 카메라 기준점 — 이 오브젝트의 자식. 위치는 실행 중에 필드 가운데로 맞춘다.")]
        [SerializeField] Transform cameraAnchor;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public Transform CameraAnchor { get => cameraAnchor; set => cameraAnchor = value; }

        void Update()
        {
            // 로비 화면일 때만 걷고 들어간다 — 씬을 불러오는 중(아직 타이틀·전환 중)엔 입력을 받지 않는다.
            if (GameState.Current != GameScene.Lobby || GameState.IsTransitioning) { NearIndex = -1; return; }

            if (player == null) return;
            bool windowOpen = UiModal.Open;
            if (!windowOpen)
            {
                int dir = (GameInput.Right ? 1 : 0) - (GameInput.Left ? 1 : 0);
                if (dir != 0)
                {
                    var pos = player.position;
                    pos.x = Mathf.Clamp(pos.x + dir * PlayerMoveConfig.BaseSpeed * Time.unscaledDeltaTime, CenterX - WalkHalfRange, CenterX + WalkHalfRange);
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


        /// <summary>
        /// 로비 준비 — 창구 등록, 건물 세우기(한 번), 카메라를 가운데 기준점에, 캐릭터를 시작 자리 땅 위에(처음 한 번).
        /// 씬이 켜질 때(`OnEnable`) 부른다. 몇 번 불러도 같다. 공개인 건 편집 모드 캡처(`Editor/CaptureUiPreview`)가 OnEnable 없이 부르려고.
        /// </summary>
        public void EnterLobby()
        {
            if (!ReferenceEquals(WorldView.Lobby, this)) WorldView.Attach(this);
            if (root == null) Build();
            root.SetActive(true);
            if (cameraAnchor != null) // 씬엔 늘 있다(씬 검증이 본다) — 기준점 없이 도는 테스트용
            {
                cameraAnchor.position = new Vector3(CenterX, FieldBounds.GroundY + EntitySizeConfig.PlayerRadius, 0f); // 지금 무대(일반) 기준
                CameraFollow2D.Track(cameraAnchor, snap: true); // 시간이 멈춘 로비라 바로 붙는다
            }
            if (player != null && !placed)
            {
                placed = true;
                var pos = new Vector3(CenterX, FieldBounds.GroundY + EntitySizeConfig.PlayerRadius, player.position.z);
                player.position = pos;
                if (player.TryGetComponent(out Rigidbody2D rb)) { rb.position = pos; rb.linearVelocity = Vector2.zero; }
            }
        }

        void Build()
        {
            BuildBackground();
            root = new GameObject("LobbyBuildings");
            root.transform.SetParent(transform, false); // 이 오브젝트(월드 씬)의 자식 — 씬을 내리면 같이 사라진다
            for (int i = 0; i < Buildings.Length; i++) BuildBuilding(Buildings[i], XOf(i));
        }

        /// <summary>
        /// 배경 3겹(하늘·먼 산·가까운 산)을 한 번 세운다 — 로비 카메라는 안 움직이니(<see cref="EnterLobby"/>) `StageVisuals`의
        /// 전투 배경처럼 매 프레임 시차 계산을 할 필요가 없다. Transform 배율은 쓰지 않는다(2026-11-06 팀 지적: PPU를 맞추고 배율은
        /// 건드리지 말 것). **그림 크기 변천**: 3배(1920×1080, 전투 배경 베낌, "작업환경을 크게 잡았다") → 2배 → 원본 640×360(화면보다
        /// 작아 틈 생김) → **다시 2배(1280×720)로 확정(2026-11-08)** — 화면(7.5×4.22유닛)을 여유 있게 덮는 가장 작은 정수 배.
        /// </summary>
        void BuildBackground()
        {
            if (background != null) return;
            var sky = Resources.Load<Sprite>(ArtFolder + "bg_sky");
            var far = Resources.Load<Sprite>(ArtFolder + "bg_mountains_far");
            var near = Resources.Load<Sprite>(ArtFolder + "bg_mountains_near");
            if (sky == null || far == null || near == null)
            {
                Debug.LogWarning("[LobbyWorld] 로비 배경 그림을 못 찾았다 — Resources/" + ArtFolder);
                return;
            }

            background = new GameObject("Background").transform;
            background.SetParent(transform, false);
            float h = sky.bounds.size.y; // 배율 1이라 그림 실제 크기(유닛) 그대로 — 지금은 2배(1280×720=10×5.625유닛)
            // 카메라 중심(바닥+CameraYOffset)에 맞춰 가운데로 둔다 — 카메라가 안 움직이니(EnterLobby) 이 한 점만 기준으로
            // 잡으면 된다. 2배 캔버스가 화면(7.5×4.22유닛)보다 가로 1.25·세로 0.7유닛씩 더 크므로 가장자리까지 넉넉히 덮는다.
            var pos = new Vector3(CenterX, FieldBounds.GroundY + StageArtConfig.CameraYOffset, 0f);
            BackgroundLayer("Sky", sky, SkyOrder, pos);
            BackgroundLayer("MountainsFar", far, MountainsFarOrder, pos);
            BackgroundLayer("MountainsNear", near, MountainsNearOrder, pos);
        }

        void BackgroundLayer(string name, Sprite sprite, int order, Vector3 pos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(background, false);
            // 배율은 1(건드리지 않는다) — 그림 자체가 필요한 픽셀 수로 들어온다.
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
        }

        void BuildBuilding(Building b, float x)
        {
            // SpriteRenderer 하나 = 건물 하나. 128×128px·128 PPU·배율 1 = 1×1유닛(배경·캐릭터와 같은 규격, 2026-11-06 팀 지적으로
            // UI 캔버스(100 PPU)에서 바꿨다). 그림은 가운데 피벗(`pixel-standard.md` 5절 — 임포트 기본값).
            var sprite = Resources.Load<Sprite>(ArtFolder + b.Art);
            if (sprite == null)
            {
                Debug.LogWarning("[LobbyWorld] 건물 그림을 못 찾았다 — Resources/" + ArtFolder + b.Art);
                return;
            }
            var go = new GameObject("Building_" + b.Name);
            go.transform.SetParent(root.transform, false);
            // 가운데 피벗이라 반 칸 위로 — 거기서 b.BottomPadPx만큼 더 내린다. 128×128 캔버스 그림 밑에 투명 여백이
            // 6~7px씩 있어서(그림마다 다름, 2026-11-08 실측) 반 칸만 올리면 그림 테두리(상자)는 땅에 맞아도 실제
            // 보이는 건물 밑동은 땅보다 떠 보였다 — "땅에 건물이 안 붙어있다"는 지적(사용자). 여백만큼 내려서 보이는
            // 밑동이 땅(GroundY)에 닿게 맞췄다.
            float y = FieldBounds.GroundY + BuildingSize / 2f - b.BottomPadPx / BuildingPx;
            go.transform.position = new Vector3(x, y, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = -50; // 배경(-97~-99) 위, 땅(-5)·캐릭터 아래
        }
    }
}
