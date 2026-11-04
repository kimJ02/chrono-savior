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

        /// <summary>두 씬을 디스크에서 열어 검사한다. 문제 목록을 콘솔에도 남긴다.</summary>
        public static List<string> ValidateFromDisk()
        {
            var persistent = EditorSceneManager.OpenScene(PersistentPath, OpenSceneMode.Single);
            var combat = EditorSceneManager.OpenScene(CombatPath, OpenSceneMode.Additive);
            var problems = Validate(persistent, combat);
            foreach (var p in problems) Debug.LogError("[SceneSetupValidator] " + p);
            Debug.Log(problems.Count == 0 ? "[SceneSetupValidator] 문제 없음" : $"[SceneSetupValidator] 문제 {problems.Count}개");
            return problems;
        }

        public static List<string> Validate(Scene persistent, Scene combat)
        {
            var problems = new List<string>();

            // ── Build Settings: 0번 = 상주 씬(첫 씬), 1번 = 월드 ──
            var build = EditorBuildSettings.scenes;
            if (build.Length != 2 || build[0].path != PersistentPath || build[1].path != CombatPath || !build.All(s => s.enabled))
                problems.Add($"Build Settings는 [{PersistentPath}, {CombatPath}] 순서로 둘 다 켜져 있어야 한다 — 지금: " +
                             string.Join(", ", build.Select(s => s.path + (s.enabled ? "" : "(꺼짐)"))));

            // ── 00_Persistent ──
            One<GameBoot>(persistent, problems);
            var flow = One<GameFlow>(persistent, problems);
            One<AutoSave>(persistent, problems);
            One<HitSound>(persistent, problems);
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
                if (!cam.CompareTag("MainCamera")) problems.Add("공용 카메라 태그가 MainCamera가 아니다(StageVisuals가 Camera.main으로 찾는다)");
                foreach (var need in new[] { typeof(CameraFollow2D), typeof(SkyBackground), typeof(AudioListener), typeof(PixelPerfectCamera) })
                    if (cam.GetComponent(need) == null) problems.Add($"공용 카메라에 {need.Name}이 없다");
                CheckCamera(cam, problems);
            }
            None<PlayerRig>(persistent, problems);
            None<PlatformSet>(persistent, problems);

            // ── 20_Combat ──
            var player = One<PlayerRig>(combat, problems);
            if (player != null)
            {
                if (!player.CompareTag("Player")) problems.Add("Player 태그가 아니다");
                foreach (var need in new[] { typeof(CharacterMover2D), typeof(PlayerHealth), typeof(MageAttack), typeof(GunnerAttack), typeof(BladeCombat), typeof(Rigidbody2D),
                                          typeof(PlayerAfterimages), typeof(BladeSlashVfx) })
                    if (player.GetComponent(need) == null) problems.Add($"Player에 {need.Name}이 없다");
            }
            var spawner = One<EnemySpawner>(combat, problems);
            if (spawner != null && spawner.monsterPrefab == null) problems.Add("EnemySpawner.monsterPrefab이 비어 있다");
            One<BossPortal>(combat, problems);
            One<StageVisuals>(combat, problems);
            One<LobbyWorld>(combat, problems);
            var sets = All<PlatformSet>(combat);
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

            // ── 두 씬 공통: 없어진 스크립트, 씬끼리 걸친 참조 ──
            foreach (var scene in new[] { persistent, combat })
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
