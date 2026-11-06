using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using ChronoSavior.Characters;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 씬 구성 검사 — **씬 파일이 유일한 원본**이 된 뒤(2026-11-04 씬 분할) 그 씬이 게임이 기대하는 모양인지 확인만 한다.
    /// 씬을 만들거나 고치지 않는다(예전 `BuildPartAScene`은 씬을 통째로 다시 만들어 손으로 고친 내용을 덮어썼다).
    ///
    /// 실행: 메뉴 `ChronoSavior/씬 구성 검증`, 또는 배치
    /// `Unity.exe -batchmode -quit -projectPath … -executeMethod ChronoSavior.Editor.SceneSetupValidator.RunBatch`
    /// (문제가 있으면 종료 코드 1).
    ///
    /// 검사: Build Settings 순서 · 씬마다 있어야 할 것/있으면 안 될 것 · 화면 목록(<see cref="PxScreens.All"/>) ·
    /// 판↔GameFlow 연결 · 발판 두 세트가 켜진 채 저장됐는지 · **씬끼리 걸친 직렬화 참조 0개** · 없어진 스크립트 0개.
    /// </summary>
    public static class SceneSetupValidator
    {
        public const string PersistentPath = "Assets/Scenes/00_Persistent.unity";
        public const string LobbyPath = "Assets/Scenes/10_Lobby.unity";
        public const string CombatPath = "Assets/Scenes/20_Combat.unity";

        [MenuItem("ChronoSavior/씬 구성 검증")]
        public static void RunMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup(); // 검사하려고 씬을 바꿔 열었다가 원래대로 돌려놓는다
            List<string> problems;
            try { problems = ValidateFromDisk(); }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            if (problems.Count == 0) EditorUtility.DisplayDialog("씬 구성 검증", "문제 없음", "확인");
            else EditorUtility.DisplayDialog("씬 구성 검증", $"문제 {problems.Count}개 — 콘솔 참고", "확인");
        }

        public static void RunBatch()
        {
            var problems = ValidateFromDisk();
            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }

        /// <summary>세 씬을 디스크에서 열어 검사한다. 문제 목록을 콘솔에도 남긴다.</summary>
        public static List<string> ValidateFromDisk()
        {
            var persistent = EditorSceneManager.OpenScene(PersistentPath, OpenSceneMode.Single);
            var lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Additive);
            var combat = EditorSceneManager.OpenScene(CombatPath, OpenSceneMode.Additive);
            var problems = Validate(persistent, lobby, combat);
            foreach (var p in problems) Debug.LogError("[SceneSetupValidator] " + p);
            Debug.Log(problems.Count == 0 ? "[SceneSetupValidator] 문제 없음" : $"[SceneSetupValidator] 문제 {problems.Count}개");
            return problems;
        }

        public static List<string> Validate(Scene persistent, Scene lobby, Scene combat)
        {
            var problems = new List<string>();

            // ── Build Settings: 0번 = 상주 씬(첫 씬), 1번 = 로비, 2번 = 전투 ──
            var build = EditorBuildSettings.scenes;
            if (build.Length != 3 || build[0].path != PersistentPath || build[1].path != LobbyPath || build[2].path != CombatPath || !build.All(s => s.enabled))
                problems.Add($"Build Settings는 [{PersistentPath}, {LobbyPath}, {CombatPath}] 순서로 다 켜져 있어야 한다 — 지금: " +
                             string.Join(", ", build.Select(s => s.path + (s.enabled ? "" : "(꺼짐)"))));

            CheckLobby(lobby, problems);

            // ── 00_Persistent ──
            One<GameBoot>(persistent, problems);
            var flow = One<GameFlow>(persistent, problems);
            One<AutoSave>(persistent, problems);
            var hitSound = One<HitSound>(persistent, problems);
            if (hitSound != null)
            {
                // 소리 장치·클립은 씬이 원본(2026-11-05 생성 책임 2-1 — 예전엔 Awake에서 붙이고 Resources에서 읽었다)
                var src = hitSound.GetComponent<AudioSource>();
                if (src == null) problems.Add("HitSound에 AudioSource가 없다");
                else if (src.playOnAwake || src.spatialBlend != 0f) problems.Add("HitSound의 AudioSource는 자동 재생 끔·2D(spatialBlend 0)여야 한다");
                // 클립이 연결돼 있는지만 본다 — 어느 파일인지는 묻지 않는다(외주·구매 소리로 갈아끼울 때 다른 곳에 둬도 되게, 2026-11-06 3.5-c).
                if (hitSound.Clip == null) problems.Add("HitSound 클립이 비어 있다");
            }
            One<EventSystem>(persistent, problems);
            var canvas = One<PixelCanvas>(persistent, problems);
            if (canvas != null)
            {
                foreach (var need in new[] { typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster) })
                    if (canvas.GetComponent(need) == null) problems.Add($"00_Persistent 판(PixelUI)에 {need.Name}이 없다");
                var c = canvas.GetComponent<Canvas>();
                var sc = canvas.GetComponent<CanvasScaler>();
                if (c != null && sc != null)
                {
                    // 판 설정의 기준은 PixelCanvas.Configure(테스트·캡처 판이 쓰는 값) — 씬의 판도 같아야 화면이 같다
                    var go = new GameObject("ExpectedCanvas") { hideFlags = HideFlags.HideAndDontSave };
                    var ec = go.AddComponent<Canvas>(); var es = go.AddComponent<CanvasScaler>();
                    PixelCanvas.Configure(ec, es);
                    if (c.renderMode != ec.renderMode || c.sortingOrder != ec.sortingOrder || c.pixelPerfect != ec.pixelPerfect ||
                        sc.uiScaleMode != es.uiScaleMode || !Mathf.Approximately(sc.referencePixelsPerUnit, es.referencePixelsPerUnit))
                        problems.Add("00_Persistent 판(PixelUI)의 Canvas/CanvasScaler 값이 PixelCanvas.Configure와 다르다");
                    Object.DestroyImmediate(go);
                }
                foreach (var screen in PxScreens.All)
                    if (canvas.GetComponent(screen) == null) problems.Add($"00_Persistent 판(PixelUI)에 화면 {screen.Name}이 없다 — PxScreens.All과 맞출 것");
                if (canvas.Flow == null || canvas.Flow != flow) problems.Add("00_Persistent 판(PixelUI)의 Flow가 같은 씬의 GameFlow를 가리키지 않는다");
            }
            var cam = One<Camera>(persistent, problems);
            if (cam != null)
            {
                if (!cam.CompareTag("MainCamera")) problems.Add("공용 카메라 태그가 MainCamera가 아니다(에디터 캡처 도구가 Camera.main으로 찾는다)");
                var lobbyHud = persistent.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<LobbyHud>(true)).FirstOrDefault();
                if (lobbyHud != null && lobbyHud.Camera != cam) problems.Add("로비 HUD(LobbyHud)의 카메라가 공용 카메라를 가리키지 않는다");
                foreach (var need in new[] { typeof(CameraFollow2D), typeof(SkyBackground), typeof(AudioListener), typeof(PixelPerfectCamera) })
                    if (cam.GetComponent(need) == null) problems.Add($"공용 카메라에 {need.Name}이 없다");
                CheckCamera(cam, problems);
            }
            None<PlayerRig>(persistent, problems);
            None<PlatformSet>(persistent, problems);
            None<CombatDirector>(persistent, problems);

            // ── 20_Combat ──
            var player = One<PlayerRig>(combat, problems);
            if (player != null)
            {
                if (!player.CompareTag("Player")) problems.Add("Player 태그가 아니다");
                foreach (var need in new[] { typeof(CharacterMover2D), typeof(PlayerHealth), typeof(MageAttack), typeof(GunnerAttack), typeof(BladeCombat), typeof(Rigidbody2D),
                                          typeof(PlayerAfterimages), typeof(BladeSlashVfx) })
                    if (player.GetComponent(need) == null) problems.Add($"Player에 {need.Name}이 없다");
                CheckCharacterArt(player, problems);
            }
            var director = One<CombatDirector>(combat, problems);
            if (director != null && (player == null || director.Player != player.gameObject))
                problems.Add("CombatDirector.Player가 이 씬의 Player를 가리키지 않는다 — 사냥 시작·보스 필드 진입 때 플레이어가 안 되돌아간다");
            var spawner = One<EnemySpawner>(combat, problems);
            if (spawner != null && spawner.monsterPrefab == null) problems.Add("EnemySpawner.monsterPrefab이 비어 있다");
            if (spawner != null) CheckProjectilePrefab(spawner.OrbPrefab, "EnemySpawner.orbPrefab", problems);
            CheckMonsterCatalog(spawner, persistent, problems);
            // 같은 씬 안은 직렬화 참조(`HANDOFF.md` "연결 규칙" 1, 2026-11-05 2b) — 비어 있으면 그 기능이 조용히 꺼진다.
            var playerTf = player != null ? player.transform : null;
            if (player != null && player.GetComponent<MageChargeFlash>() == null) problems.Add("Player에 MageChargeFlash(마법사 차지 깜빡임)가 없다 — 예전엔 MageAttack이 붙였지만 이제 씬에 놓는다");
            // 쏘는 쪽이 탄 프리팹을 들고 있다(2026-11-05 생성 책임 2-3 — 예전엔 탄을 코드로 지었다)
            if (player != null && player.TryGetComponent(out MageAttack mageKit)) CheckProjectilePrefab(mageKit.BoltPrefab, "Player MageAttack.boltPrefab", problems);
            if (player != null && player.TryGetComponent(out GunnerAttack gunnerKit))
            {
                CheckProjectilePrefab(gunnerKit.BulletPrefab, "Player GunnerAttack.bulletPrefab", problems);
                CheckProjectilePrefab(gunnerKit.DronePrefab, "Player GunnerAttack.dronePrefab", problems);
                CheckProjectilePrefab(gunnerKit.TurretPrefab, "Player GunnerAttack.turretPrefab", problems);
            }
            if (spawner != null && spawner.Player != playerTf) problems.Add("EnemySpawner.Player가 이 씬의 Player가 아니다 — 몹이 쫓을 대상·구슬이 끌려갈 곳이 없다");
            var portal = One<BossPortal>(combat, problems);
            if (portal != null && portal.Player != playerTf) problems.Add("BossPortal.Player가 이 씬의 Player가 아니다 — 포탈에 다가가도 반응이 없다");
            if (director != null && director.BossPortal != portal) problems.Add("CombatDirector.BossPortal이 이 씬의 BossPortal이 아니다 — 전투 화면에 포탈 안내·미니맵 점이 안 나온다");
            var visuals = One<StageVisuals>(combat, problems);
            CheckStageArt(visuals, combat.name, problems);
            if (visuals != null)
            {
                var groundGo = combat.GetRootGameObjects().FirstOrDefault(g => g.name == "Ground");
                if (visuals.Ground == null || groundGo == null || visuals.Ground != groundGo.transform) problems.Add("StageVisuals.Ground가 이 씬의 Ground가 아니다");
                var allSets = All<PlatformSet>(combat);
                if (visuals.PlatformSets == null || visuals.PlatformSets.Length != allSets.Count || allSets.Any(s => !visuals.PlatformSets.Contains(s)))
                    problems.Add("StageVisuals.PlatformSets가 이 씬의 발판 세트 전부가 아니다");
                if (visuals.Portal != portal) problems.Add("StageVisuals.Portal이 이 씬의 BossPortal이 아니다");
            }
            None<LobbyWorld>(combat, problems); // 로비는 자기 씬(10_Lobby)에만 — 2026-11-05 씬 분할 2차 3-4
            var sets = All<PlatformSet>(combat);
            if (director != null && (director.NormalPlatforms == null || director.NormalPlatforms.bossArena || !sets.Contains(director.NormalPlatforms)))
                problems.Add("CombatDirector.NormalPlatforms가 이 씬의 일반 무대 발판 세트가 아니다");
            if (director != null && (director.BossPlatforms == null || !director.BossPlatforms.bossArena || !sets.Contains(director.BossPlatforms)))
                problems.Add("CombatDirector.BossPlatforms가 이 씬의 보스 무대 발판 세트가 아니다");
            if (sets.Count != 2 || sets.Count(s => s.bossArena) != 1)
                problems.Add($"발판 세트는 일반 하나·보스 하나여야 한다 — 지금 {sets.Count}개(보스 {sets.Count(s => s.bossArena)})");
            foreach (var set in sets)
                if (!set.gameObject.activeSelf)
                    problems.Add($"{set.name}이 꺼진 채 저장돼 있다 — 꺼져 있으면 Awake가 안 불려 PlatformSet.Activate가 못 켠다(켠 채 저장할 것)");
            var ground = combat.GetRootGameObjects().FirstOrDefault(g => g.name == "Ground");
            if (ground == null || ground.GetComponent<BoxCollider2D>() == null) problems.Add("20_Combat에 Ground(BoxCollider2D)가 없다");
            else if (ground.layer != LayerMask.NameToLayer("Ground")) problems.Add("Ground가 Ground 레이어가 아니다");
            None<Camera>(combat, problems);
            None<AudioListener>(combat, problems);
            None<EventSystem>(combat, problems);
            None<PixelCanvas>(combat, problems);
            None<GameFlow>(combat, problems);

            // ── 세 씬 공통: 없어진 스크립트, 씬끼리 걸친 참조 ──
            foreach (var scene in new[] { persistent, lobby, combat })
            {
                foreach (var go in AllGameObjects(scene))
                {
                    int missing = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
                    if (missing > 0) problems.Add($"{scene.name}/{Path(go)}에 없어진 스크립트 {missing}개");
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp == null) continue;
                        var so = new SerializedObject(comp);
                        var it = so.GetIterator();
                        while (it.Next(true))
                        {
                            if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var target = it.objectReferenceValue;
                            var targetScene = target is GameObject tg ? tg.scene : target is Component tc ? tc.gameObject.scene : default;
                            if (targetScene.IsValid() && targetScene != scene)
                                problems.Add($"씬끼리 걸친 참조: {scene.name}/{Path(go)}.{comp.GetType().Name}.{it.propertyPath} → {targetScene.name}");
                        }
                    }
                }
            }
            return problems;
        }

        /// <summary>
        /// 공용 카메라의 화면 크기 설정 — 씬이 원본이고 실행 중에 고치는 코드는 없다(2026-11-05). 값의 기준은 `World/StageArtConfig`
        /// (월드 960×540px, 1유닛 = 128px, 정수배 확대, 어떤 창 크기에서도 보이는 범위가 같게).
        /// </summary>
        static void CheckCamera(Camera cam, List<string> problems)
        {
            if (!cam.orthographic) problems.Add("공용 카메라가 직교(Orthographic)가 아니다");
            CheckSavedViewportRect(cam.gameObject.scene.path, problems);
            if (Mathf.Abs(cam.orthographicSize - StageArtConfig.ViewHalfHeight) > 1e-4f)
                problems.Add($"공용 카메라 크기 {cam.orthographicSize} ≠ StageArtConfig.ViewHalfHeight {StageArtConfig.ViewHalfHeight}");
            var ppc = cam.GetComponent<PixelPerfectCamera>();
            if (ppc == null) return;
            if (ppc.assetsPPU != StageArtConfig.PixelsPerUnit) problems.Add($"PixelPerfectCamera assetsPPU {ppc.assetsPPU} ≠ {StageArtConfig.PixelsPerUnit}");
            if (ppc.refResolutionX != StageArtConfig.RefWidth || ppc.refResolutionY != StageArtConfig.RefHeight)
                problems.Add($"PixelPerfectCamera 기준 해상도 {ppc.refResolutionX}×{ppc.refResolutionY} ≠ {StageArtConfig.RefWidth}×{StageArtConfig.RefHeight}");
            if (ppc.gridSnapping != PixelPerfectCamera.GridSnapping.UpscaleRenderTexture) problems.Add("PixelPerfectCamera gridSnapping이 UpscaleRenderTexture가 아니다(정수배 확대)");
            if (ppc.cropFrame != PixelPerfectCamera.CropFrame.StretchFill) problems.Add("PixelPerfectCamera cropFrame이 StretchFill이 아니다(창 크기와 상관없이 같은 범위)");
        }

        /// <summary>
        /// 몬스터 목록(`Core/MonsterCatalog`) — 스포너(전투 씬)와 스테이지 선택 화면(상주 씬)이 **같은 에셋**을 보고, 몹 종류마다 데이터가 하나씩 있다(2026-11-05).
        /// 그림은 박쥐·고블린만 정식이고 나머지는 실행 중 도형이라 그림 유무는 보지 않는다.
        /// </summary>
        static void CheckMonsterCatalog(EnemySpawner spawner, Scene persistent, List<string> problems)
        {
            var catalog = spawner != null ? spawner.Catalog : null;
            if (spawner != null && catalog == null) { problems.Add("EnemySpawner의 몬스터 목록(MonsterCatalog)이 비어 있다"); return; }
            var stage = persistent.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<StageSelectScreen>(true)).FirstOrDefault();
            if (stage == null) return; // 화면이 없는 건 위 화면 목록 검사가 잡는다
            if (stage.Catalog == null) problems.Add("스테이지 선택 화면의 몬스터 목록(MonsterCatalog)이 비어 있다");
            else if (catalog != null && stage.Catalog != catalog) problems.Add("스테이지 선택 화면과 스포너가 서로 다른 몬스터 목록을 본다");
            if (catalog == null) return;
            foreach (ChronoSavior.Core.EnemyType t in System.Enum.GetValues(typeof(ChronoSavior.Core.EnemyType)))
                if (catalog.types.Count(d => d != null && d.type == t) != 1) problems.Add($"몬스터 목록에 {t} 데이터가 하나가 아니다");
            CheckEnemyPrefabs(catalog, problems);
        }

        /// <summary>
        /// 몹 프리팹 구성(2026-11-05 생성 책임 2-2) — 종류마다 데이터가 가리키는 프리팹에 **그 종류의 행동 부품만** 들어 있는지(박쥐 = 비행, 멧돼지 = 돌진,
        /// 해골 궁수 = 사격, 슬라임 = 분열, 나머지 = 기본 보행), 보스·성소 프리팹은 그 부품과 구조 설정을 갖는지. 예전엔 스포너가 실행 중에 붙였다.
        /// </summary>
        static void CheckEnemyPrefabs(ChronoSavior.Core.MonsterCatalog catalog, List<string> problems)
        {
            var behaviours = new[] { typeof(ChronoSavior.Enemies.EnemyFlyerMotion), typeof(ChronoSavior.Enemies.EnemyChargerMotion),
                                     typeof(ChronoSavior.Enemies.EnemyShooterMotion), typeof(ChronoSavior.Enemies.EnemySplitOnDeath),
                                     typeof(ChronoSavior.Enemies.Boss), typeof(ChronoSavior.Enemies.Shrine) };
            void Check(GameObject prefab, string what, System.Type expected)
            {
                if (prefab == null) { problems.Add($"{what} 프리팹이 비어 있다"); return; }
                if (prefab.GetComponent<ChronoSavior.Enemies.EnemyMove>() == null || prefab.GetComponent<ChronoSavior.Enemies.EnemyHealth>() == null)
                    problems.Add($"{what} 프리팹({prefab.name})에 EnemyMove·EnemyHealth가 없다");
                foreach (var b in behaviours)
                {
                    bool has = prefab.GetComponent(b) != null;
                    if (has != (b == expected)) problems.Add($"{what} 프리팹({prefab.name})의 {b.Name}: {(has ? "있으면 안 된다" : "있어야 한다")}");
                }
            }
            foreach (var d in catalog.types.Where(d => d != null))
            {
                System.Type expected = d.type switch
                {
                    ChronoSavior.Core.EnemyType.Flyer => typeof(ChronoSavior.Enemies.EnemyFlyerMotion),
                    ChronoSavior.Core.EnemyType.Charger => typeof(ChronoSavior.Enemies.EnemyChargerMotion),
                    ChronoSavior.Core.EnemyType.Shooter => typeof(ChronoSavior.Enemies.EnemyShooterMotion),
                    ChronoSavior.Core.EnemyType.Splitter => typeof(ChronoSavior.Enemies.EnemySplitOnDeath),
                    _ => null,
                };
                Check(d.prefab, $"{d.type}", expected);
            }
            Check(catalog.bossPrefab, "보스", typeof(ChronoSavior.Enemies.Boss));
            if (catalog.bossPrefab != null && catalog.bossPrefab.TryGetComponent(out ChronoSavior.Enemies.Boss bossKit)) CheckProjectilePrefab(bossKit.BoltPrefab, "보스 프리팹 Boss.boltPrefab", problems);
            var shooterData = catalog.Find(ChronoSavior.Core.EnemyType.Shooter);
            if (shooterData != null && shooterData.prefab != null && shooterData.prefab.TryGetComponent(out ChronoSavior.Enemies.EnemyShooterMotion shooterKit))
                CheckProjectilePrefab(shooterKit.BoltPrefab, "해골 궁수 프리팹 EnemyShooterMotion.boltPrefab", problems);
            if (catalog.bossPrefab != null && catalog.bossPrefab.GetComponent<ChronoSavior.Enemies.EnemyHealth>() is { isBoss: false })
                problems.Add("보스 프리팹의 EnemyHealth.isBoss가 꺼져 있다(레벨 페널티 하한이 보스 값이 아니게 된다)");
            Check(catalog.shrinePrefab, "성소", typeof(ChronoSavior.Enemies.Shrine));
            if (catalog.shrinePrefab != null)
            {
                if (catalog.shrinePrefab.GetComponent<ChronoSavior.Enemies.EnemyMove>() is { isStructure: false }) problems.Add("성소 프리팹이 구조물(isStructure)이 아니다");
                var rb = catalog.shrinePrefab.GetComponent<Rigidbody2D>();
                if (rb != null && (rb.bodyType != RigidbodyType2D.Kinematic || rb.gravityScale != 0f)) problems.Add("성소 프리팹은 움직이지 않는 구조물(Kinematic·중력 0)이어야 한다");
            }
        }

        /// <summary>
        /// 스테이지 그림 묶음(`StageArtSet`, 2026-11-06 에셋 교체 경계) — `StageVisuals`가 그림을 경로가 아니라 이 에셋에서 받으므로, 칸이 비면 실행 중에야 안 보인다. 여기서 미리 잡는다.
        /// </summary>
        static void CheckStageArt(StageVisuals visuals, string sceneName, List<string> problems)
        {
            if (visuals == null) return; // 없는 건 One<StageVisuals>가 잡는다
            var art = visuals.Art;
            if (art == null) { problems.Add($"{sceneName} StageVisuals의 그림 묶음(StageArtSet)이 비어 있다"); return; }
            if (art.groundTop == null || art.groundDirt == null || art.platformTile == null || art.background == null || art.portal == null)
                problems.Add($"{sceneName} 그림 묶음({art.name})에 빈 칸이 있다(땅 윗면·흙·발판·배경·포탈)");
            if (art.backgroundScale < 1) problems.Add($"{sceneName} 그림 묶음({art.name})의 배경 배율이 1보다 작다");
        }

        /// <summary>
        /// 탄 프리팹 — 비어 있지 않고, 프리팹 안의 직렬화 참조(리지드바디·판정·그림 자식)가 전부 이어져 있고, 런이 끝나면 사라지는 표식(RunTransient)이 있다.
        /// 구성이 프리팹에 있으니 참조 하나가 끊기면 실행 중에야 터진다 — 여기서 미리 잡는다.
        /// </summary>
        static void CheckProjectilePrefab(Component prefab, string what, List<string> problems)
        {
            if (prefab == null) { problems.Add($"{what}가 비어 있다"); return; }
            if (prefab.GetComponent<ChronoSavior.Core.RunTransient>() == null) problems.Add($"{what}({prefab.name})에 RunTransient가 없다 — 런이 끝나도 남는다");
            var so = new SerializedObject(prefab);
            for (var it = so.GetIterator(); it.NextVisible(true);)
                if (it.propertyType == SerializedPropertyType.ObjectReference && it.name != "m_Script" && it.objectReferenceValue == null)
                    problems.Add($"{what}({prefab.name})의 {it.name} 참조가 비어 있다");
        }

        /// <summary>
        /// 공용 카메라 Viewport Rect가 **씬 파일에** 화면 전체(0,0,1,1)로 저장돼 있는지. PixelPerfectCamera는 편집기에서 씬을 여는 순간
        /// 창 크기에 맞춰 위아래를 잘라 낸 값(예: y 0.125, 높이 0.75)을 메모리에 넣는다 — 그 상태로 씬을 저장하면 그 값이 파일에 박혀
        /// 실제 게임 화면에 검은 띠가 생긴다(2026-11-05 배치 저장에서 실제로 섞여 들어옴). 메모리 값은 열 때마다 바뀌니 파일을 읽는다.
        /// </summary>
        static void CheckSavedViewportRect(string scenePath, List<string> problems)
        {
            if (string.IsNullOrEmpty(scenePath) || !System.IO.File.Exists(scenePath)) return;
            var m = System.Text.RegularExpressions.Regex.Match(System.IO.File.ReadAllText(scenePath),
                @"m_NormalizedViewPortRect:\s*serializedVersion: \d+\s*x: (\S+)\s*y: (\S+)\s*width: (\S+)\s*height: (\S+)");
            if (!m.Success) { problems.Add("공용 카메라 Viewport Rect를 씬 파일에서 못 찾았다"); return; }
            if (m.Groups[1].Value != "0" || m.Groups[2].Value != "0" || m.Groups[3].Value != "1" || m.Groups[4].Value != "1")
                problems.Add($"공용 카메라 Viewport Rect가 씬 파일에 화면 전체로 저장돼 있지 않다(x {m.Groups[1].Value}, y {m.Groups[2].Value}, w {m.Groups[3].Value}, h {m.Groups[4].Value}) — 0,0,1,1로");
        }

        /// <summary>캐릭터 그림 에셋(`Core/CharacterArtSet`) — Player에 연결돼 있고, 배열이 캐릭터 수만큼이고, 키트가 있는 캐릭터는 그림이 있다(2026-11-05).</summary>
        static void CheckCharacterArt(PlayerRig player, List<string> problems)
        {
            var art = player.Art;
            if (art == null) { problems.Add("Player의 캐릭터 그림(CharacterArtSet)이 비어 있다"); return; }
            if (art.sprites == null || art.sprites.Length != ChronoSavior.Core.CharacterArtSet.Count || art.tints == null || art.tints.Length != ChronoSavior.Core.CharacterArtSet.Count)
                problems.Add($"캐릭터 그림 배열 길이가 캐릭터 수({ChronoSavior.Core.CharacterArtSet.Count})와 다르다");
            foreach (var kit in player.GetComponents<ICharacterKit>())
                if (art.SpriteOf(kit.Character) == null) problems.Add($"캐릭터 그림에 {kit.Character} 그림이 없다(키트는 있다)");
            // 로비 캐릭터 선택은 프로필만 바꾸고 Player가 따라간다 — 고를 수 있는 캐릭터는 키트가 있어야 한다(없으면 PlayerRig가 프로필을 되돌려 고른 게 무시된다).
            var kits = player.GetComponents<ICharacterKit>().Select(k => k.Character).ToHashSet();
            foreach (var id in CharacterSelectScreen.Selectable)
                if (!kits.Contains(id)) problems.Add($"캐릭터 선택에서 {id}를 고를 수 있는데 Player에 그 키트가 없다");
        }

        /// <summary>
        /// `10_Lobby`(2026-11-05 씬 분할 2차 3-2) — 로비 월드(건물·카메라 기준점)·그림만 있는 아바타·땅(그림 크기 입력용 — 판정은 꺼 둠)·스테이지 그림(땅·배경만).
        /// 전투 것(플레이어·스포너·전투 담당·발판·포탈)과 상주 것(카메라·판·흐름)은 없어야 한다.
        /// </summary>
        static void CheckLobby(Scene lobby, List<string> problems)
        {
            var world = One<LobbyWorld>(lobby, problems);
            var avatar = One<LobbyAvatar>(lobby, problems);
            if (avatar != null && avatar.Art == null) problems.Add("10_Lobby LobbyAvatar의 캐릭터 그림(CharacterArtSet)이 비어 있다");
            if (world != null && (avatar == null || world.Player != avatar.transform)) problems.Add("10_Lobby LobbyWorld.Player가 이 씬의 LobbyAvatar가 아니다");
            if (world != null && (world.CameraAnchor == null || world.CameraAnchor.parent != world.transform)) problems.Add("10_Lobby LobbyWorld.CameraAnchor가 이 오브젝트의 자식 기준점이 아니다");
            var ground = lobby.GetRootGameObjects().FirstOrDefault(g => g.name == "Ground");
            if (ground == null || ground.GetComponent<BoxCollider2D>() == null) problems.Add("10_Lobby에 Ground(BoxCollider2D — 땅 그림 크기 입력)가 없다");
            var visuals = One<StageVisuals>(lobby, problems);
            CheckStageArt(visuals, lobby.name, problems);
            if (visuals != null)
            {
                if (ground == null || visuals.Ground != ground.transform) problems.Add("10_Lobby StageVisuals.Ground가 이 씬의 Ground가 아니다");
                if (visuals.PlatformSets != null && visuals.PlatformSets.Length > 0) problems.Add("10_Lobby StageVisuals에 발판 세트가 있다(로비엔 발판이 없다)");
                if (visuals.Portal != null) problems.Add("10_Lobby StageVisuals에 포탈이 있다(로비엔 포탈이 없다)");
            }
            None<PlayerRig>(lobby, problems);
            None<EnemySpawner>(lobby, problems);
            None<CombatDirector>(lobby, problems);
            None<PlatformSet>(lobby, problems);
            None<BossPortal>(lobby, problems);
            None<Camera>(lobby, problems);
            None<AudioListener>(lobby, problems);
            None<EventSystem>(lobby, problems);
            None<PixelCanvas>(lobby, problems);
            None<GameFlow>(lobby, problems);
            None<GameBoot>(lobby, problems);
        }

        static T One<T>(Scene scene, List<string> problems) where T : Component
        {
            var found = All<T>(scene);
            if (found.Count != 1) problems.Add($"{scene.name}에 {typeof(T).Name}이 정확히 하나여야 한다 — 지금 {found.Count}개");
            return found.Count > 0 ? found[0] : null;
        }

        static void None<T>(Scene scene, List<string> problems) where T : Component
        {
            int n = All<T>(scene).Count;
            if (n > 0) problems.Add($"{scene.name}에 {typeof(T).Name}이 있으면 안 된다 — 지금 {n}개");
        }

        static List<T> All<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToList();

        static IEnumerable<GameObject> AllGameObjects(Scene scene) =>
            scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).Select(t => t.gameObject);

        static string Path(GameObject go) => go.transform.parent == null ? go.name : Path(go.transform.parent.gameObject) + "/" + go.name;
    }
}
