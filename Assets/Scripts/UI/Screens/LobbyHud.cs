using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 로비 화면 위 표시 — Figma 로비 뼈대("Page 1")의 자리대로: 위 가운데 **멸망까지 남은 기간**(사용자 결정 7일)과 다음 원정 지역 이름,
    /// 왼쪽 위 캐릭터 정보, 왼쪽 아래 버튼 셋(캐릭터 · 업적 · 설정), 오른쪽 아래 미니맵(건물·캐릭터 자리), 맨 아래 경험치 바(전투 HUD와 같은 색).
    /// 건물 앞에 서면 "F / ↑ — 들어가기" 안내. 예전 IMGUI 로비(탭 화면)는 이 화면이 있으면 안 그린다.
    /// </summary>
    public class LobbyHud : PxScreen
    {
        [Tooltip("같은 씬(00_Persistent)의 공용 카메라 — 건물 글씨를 화면 위치로 옮길 때 쓴다.")]
        [SerializeField] Camera cam;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다(2026-11-05 2b — 예전엔 태그로 공용 카메라를 찾았다).</summary>
        public Camera Camera { get => cam; set => cam = value; }

        protected override PixelCanvas.Layer Layer => PixelCanvas.Layer.Hud;
        protected override bool WantsVisible => GameState.Current == GameScene.Lobby;
        protected override bool TakesKeys => false;

        // 모양(좌표·색)은 프리팹 LobbyHudView에 있다(2026-11-23 프리팹 UI 10호). 여기엔 글자 내용·자리 계산·버튼 동작만.
        LobbyHudView view;
        Text doom, region, who, money, prompt;
        Text[] buildingNames, buildingDoors;
        Image expFill, playerDot;
        static readonly string[] CharacterNames = { "마법사", "메카닉", "살수", "드루이드" }; // '살수' = 예전 섬영(화면 이름만, 2026-11-07)

        /// <summary>모양을 프리팹에서 불러왔는가 — false면 프리팹이 없어 코드로 지었다(테스트·검증용).</summary>
        public bool UsesViewPrefab { get; private set; }

        /// <summary>
        /// 캐릭터 버튼은 뺀다(2026-11-22 사용자 — 캐릭터는 새 원정·환생 때만 고른다, <see cref="CharacterSelectScreen.OpenForRun"/>; "이번 회차에서는 캐릭터를 바꿀 수 없다").
        /// 2026-11-24 팀원 프리팹 UI 위에 다시 얹음 — 프리팹은 그대로 두고, 첫 칸(캐릭터)을 숨긴 뒤 업적·설정을 한 칸씩 당긴다(프리팹의 칸 자리를 그대로 쓴다).
        /// </summary>
        void HideCharacterButton()
        {
            var q = view.quickButtons;
            if (q.Length < 3 || q[0]?.button == null) return;
            var slots = new Vector2[q.Length];
            for (int i = 0; i < q.Length; i++) slots[i] = q[i]?.button != null ? ((RectTransform)q[i].button.transform).anchoredPosition : Vector2.zero;
            q[0].button.gameObject.SetActive(false);
            for (int i = 1; i < q.Length; i++)
                if (q[i]?.button != null) ((RectTransform)q[i].button.transform).anchoredPosition = slots[i - 1];
        }

        /// <summary>
        /// 모양은 프리팹(<see cref="LobbyHudView"/>, `Resources/UI/Views/LobbyHudView`)이 원본이다(2026-11-23 프리팹 UI 10호).
        /// 여기선 조각을 받아 오고, 빠른 버튼 셋에 동작을 잇고, 미니맵 건물 칸·건물 글씨를 견본에서 건물 수만큼 복제한다.
        /// 프리팹이 없으면 경고를 남기고 예전과 같은 코드 모양으로 짓는다.
        /// </summary>
        protected override void Build()
        {
            view = LoadView<LobbyHudView>(LobbyHudView.ResourcePath);
            UsesViewPrefab = view != null;
            if (view == null)
            {
                Debug.LogWarning("[LobbyHud] 화면 프리팹(Resources/" + LobbyHudView.ResourcePath + ")이 없어 코드로 짓는다 — " +
                                 "메뉴 ChronoSavior/UI 프리팹/로비 HUD 만들기", this);
                view = LobbyHudView.CreateFromCode(Root);
            }
            if (view.quickButtons.Length != 3)
                Debug.LogError($"[LobbyHud] 화면 프리팹 빠른 버튼 수가 다르다({view.quickButtons.Length}) — 3(캐릭터·업적·설정)이어야 한다", view);

            doom = view.doom;
            region = view.region;
            who = view.who;
            money = view.money;
            prompt = view.prompt;
            expFill = view.expFill;
            playerDot = view.playerDot;

            System.Action[] quick = { CharacterSelectScreen.Open, AchievementsScreen.Open, SettingsWindow.Open };
            for (int i = 0; i < quick.Length && i < view.quickButtons.Length; i++) view.quickButtons[i].Wire(quick[i]);
            HideCharacterButton();

            // 미니맵 건물 칸 — 가로 자리만 건물 위치로(세로·크기는 견본)
            int n = LobbyWorld.Buildings.Length;
            var marks = LobbyHudView.Clone(view.buildingTemplate, n, "Building");
            for (int i = 0; i < n; i++)
            {
                if (marks[i] == null) continue;
                var rt = (RectTransform)marks[i].transform;
                rt.anchoredPosition = new Vector2(MapX(LobbyWorld.XOf(i)) - 2f, rt.anchoredPosition.y);
            }

            // 건물 글씨 — 월드 건물(LobbyWorld) 위에 화면 해상도로 겹쳐 그린다(월드에 그리면 늘어나 흐렸다, 사용자 2026-10-28). 자리는 매 프레임.
            buildingNames = LobbyHudView.Clone(view.buildingNameTemplate, n, "BuildingName");
            buildingDoors = LobbyHudView.Clone(view.buildingDoorTemplate, n, "BuildingDoor");
            for (int i = 0; i < n; i++) if (buildingNames[i] != null) buildingNames[i].text = LobbyWorld.Buildings[i].Name;
        }

        /// <summary>
        /// 건물 글씨를 건물 위에 놓는다 — 월드 좌표를 640판 좌표로. 로비 카메라는 가운데에 서 있고(`CameraFollow2D`) 화면에 보이는 월드는
        /// 가로 7.5 × 세로 4.22유닛(`StageArtConfig`)이 판 640×360에 딱 맞는다.
        /// </summary>
        void PlaceBuildingLabels()
        {
            if (cam == null || buildingNames == null) return;
            Vector2 c = cam.transform.position;
            for (int i = 0; i < buildingNames.Length; i++)
            {
                if (buildingNames[i] != null) Place(buildingNames[i], LobbyWorld.LabelWorldPos(i, LobbyWorld.NameFromTop), c);
                if (buildingDoors[i] == null) continue;
                Place(buildingDoors[i], LobbyWorld.LabelWorldPos(i, LobbyWorld.DoorFromTop), c);
                // 그 건물 앞에 서 있으면 "F / ↑"는 숨긴다 — 글씨가 캐릭터 위에 겹치고, 아래쪽 큰 안내가 대신 뜬다.
                buildingDoors[i].enabled = WorldView.Lobby == null || WorldView.Lobby.NearBuilding != i;
            }
        }

        static void Place(Text t, Vector2 world, Vector2 cam) => Px.PlaceAtWorld(t, world, cam);

        /// <summary>월드 x → 미니맵 안 가로 자리(px, 미니맵 왼쪽 기준).</summary>
        float MapX(float worldX)
        {
            float span = Mathf.Max(0.01f, FieldBounds.MaxX - FieldBounds.MinX);
            return view.MinimapX((worldX - FieldBounds.MinX) / span);
        }

        protected override void Refresh()
        {
            var p = ProfileService.Current;
            doom.text = $"멸망까지 남은 기간 {p.doomDays}일";
            var (c, st, _) = StageSelectScreen.NextPlay(p); // 2026-11-19 챕터 구조 — 이어서 할 곳
            region.text = $"다음 원정 {ChapterConfig.StageLabel(c, st)} · {ChapterConfig.NameOf(c)}";
            int id = (int)p.character;
            who.text = $"{(id < CharacterNames.Length ? CharacterNames[id] : "?")}  Lv.{p.level}  ·  SP {p.SpAvailable}";
            money.text = $"{p.gold:N0} G  ·  시간의 파편 {p.shards:N0}";

            long need = PlayerProfile.RequiredExp(p.level);
            float ratio = need > 0 ? Mathf.Clamp01(p.exp / (float)need) : 0f;
            view.SetExp(ratio);

            // 로비 월드는 읽기 창구(`Systems/WorldView.Lobby`)로만 읽는다(2026-11-05 2b — 예전엔 태그로 캐릭터를 찾고 정적 Instance를 읽었다).
            var world = WorldView.Lobby;
            var avatar = world != null ? world.AvatarPosition : null;
            playerDot.enabled = avatar.HasValue;
            if (avatar.HasValue)
            {
                var dot = (RectTransform)playerDot.transform;
                dot.anchoredPosition = new Vector2(MapX(avatar.Value.x) - 1f, dot.anchoredPosition.y); // 세로는 프리팹 자리
            }

            PlaceBuildingLabels();

            int near = world != null ? world.NearBuilding : -1;
            prompt.enabled = near >= 0 && !UiModal.Open;
            if (prompt.enabled) prompt.text = $"F / ↑ — {LobbyWorld.Buildings[near].Name} 들어가기";
        }
    }
}
