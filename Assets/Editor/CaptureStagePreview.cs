using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 1스테이지 화면을 PNG로 찍는다 — 배치 테스트는 그래픽 없이(-nographics) 돌아 화면을 못 보니, 눈으로 확인할 그림을 만든다.
    /// 씬을 열기만 하고 저장하지 않는다. `PixelPerfectCamera`와 같은 결과가 되게 960×540으로 그린다(키우는 건 보는 쪽에서 2배).
    ///
    /// 배치 실행(그래픽 필요 — `-nographics` 빼고):
    ///   Unity.exe -batchmode -projectPath . -executeMethod ChronoSavior.Editor.CaptureStagePreview.Run -captureOut &lt;폴더&gt; -quit
    /// </summary>
    public static class CaptureStagePreview
    {
        public static void Run()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "StageCapture");
            Directory.CreateDirectory(outDir);

            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single); // 카메라는 상주 씬, 월드는 전투 씬(2026-11-04 씬 분할)
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            FitPlayerSprite(player);

            var visuals = Object.FindFirstObjectByType<StageVisuals>(); // 전투 씬에 놓인 것 — 그 씬의 땅·발판을 직렬화로 안다(2026-11-05 2b)
            visuals.Build();

            // (이름, 플레이어 x, 발 높이) — 땅 / 1층 발판 / 필드 오른쪽 끝
            Shot(cam, visuals, player, outDir, "1_땅", 4.0f, FieldBounds.GroundY);
            Shot(cam, visuals, player, outDir, "2_발판1층", FieldLayout.Platforms[0, 0], FieldLayout.PlatformTopY(0));
            Shot(cam, visuals, player, outDir, "3_오른쪽끝", FieldBounds.MaxX - 2f, FieldBounds.GroundY);
            Debug.Log("[CaptureStagePreview] 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 발판 위 카메라 높이 안 3가지를 1층·2층에서 찍고, 포탈(잠김·열림)도 찍는다 — 사용자가 고르기 전 비교용(2026-10-24).
        /// ① 지금(서 있는 높이로 다 따라감) ② 1층까지는 그대로(그 위부터 따라감) ③ 절반만 따라감.
        /// </summary>
        public static void RunCameraOptions()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "StageCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single); // 카메라는 상주 씬, 월드는 전투 씬(2026-11-04 씬 분할)
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            FitPlayerSprite(player);
            var visuals = Object.FindFirstObjectByType<StageVisuals>(); // 전투 씬에 놓인 것 — 그 씬의 땅·발판을 직렬화로 안다(2026-11-05 2b)
            visuals.Build();

            float l1 = FieldLayout.PlatformTopY(0), l2 = FieldLayout.PlatformTopY(4);
            float x1 = FieldLayout.Platforms[0, 0], x2 = FieldLayout.Platforms[4, 0];
            float off = StageArtConfig.CameraYOffset, min = CameraFollow2D.MinY;
            float Rule2(float f) => Mathf.Max(min, f + off - l1);
            float Rule3(float f) => min + 0.5f * (f - FieldBounds.GroundY);
            Shot(cam, visuals, player, outDir, "A1_지금_1층", x1, l1);
            Shot(cam, visuals, player, outDir, "A2_지금_2층", x2, l2);
            Shot(cam, visuals, player, outDir, "B1_1층까지그대로_1층", x1, l1, Rule2(l1));
            Shot(cam, visuals, player, outDir, "B2_1층까지그대로_2층", x2, l2, Rule2(l2));
            Shot(cam, visuals, player, outDir, "C1_절반만_1층", x1, l1, Rule3(l1));
            Shot(cam, visuals, player, outDir, "C2_절반만_2층", x2, l2, Rule3(l2));

            var portal = Object.FindFirstObjectByType<BossPortal>();
            if (portal != null)
            {
                var sr = portal.GetComponent<SpriteRenderer>();
                float px = FieldLayout.BossPortalX - 1.2f;
                sr.color = portal.lockedColor;
                Shot(cam, visuals, player, outDir, "P1_포탈_잠김", px, FieldBounds.GroundY);
                sr.color = portal.unlockedColor;
                Shot(cam, visuals, player, outDir, "P2_포탈_열림", px, FieldBounds.GroundY);
            }
            Debug.Log("[CaptureStagePreview] 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 피격 플래시 확인 — 고블린 그림 3마리(그대로 / 플래시 0.75 / 0.4)를 플레이어 옆에 두고 찍는다(2026-10-24).
        /// `EnemyHealth.ShowFlash`와 같은 자식 렌더러·셰이더(`Resources/Shaders/SpriteFlash`)를 쓴다.
        /// </summary>
        public static void RunFlashCheck()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "StageCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single); // 카메라는 상주 씬, 월드는 전투 씬(2026-11-04 씬 분할)
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            FitPlayerSprite(player);
            var visuals = Object.FindFirstObjectByType<StageVisuals>(); // 전투 씬에 놓인 것 — 그 씬의 땅·발판을 직렬화로 안다(2026-11-05 2b)
            visuals.Build();
            var oni = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Prototype/enemy_grunt.png");
            var mat = new Material(Resources.Load<Shader>("Shaders/SpriteFlash"));
            float[] alphas = { 0f, 0.75f, 0.4f };
            for (int i = 0; i < alphas.Length; i++)
            {
                var go = new GameObject("FlashCheck" + i);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = oni;
                float s = 0.46f / oni.bounds.size.y; // 판정 높이(EntitySizeConfig 고블린 46px)에 맞춤 — 지금 게임과 같게
                go.transform.localScale = new Vector3(s, s, 1f);
                go.transform.position = new Vector3(5.0f + i * 0.9f, FieldBounds.GroundY + 0.23f, 0f);
                if (alphas[i] <= 0f) continue;
                var f = new GameObject("HitFlash").AddComponent<SpriteRenderer>();
                f.transform.SetParent(go.transform, false);
                f.sprite = oni;
                f.sharedMaterial = mat;
                f.sortingOrder = sr.sortingOrder + 1;
                f.color = new Color(1f, 1f, 1f, alphas[i]);
            }
            Shot(cam, visuals, player, outDir, "F_플래시", 4.0f, FieldBounds.GroundY);
            Debug.Log("[CaptureStagePreview] 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        /// <summary>포탈 크기 비교(1배·1.5배·2배, 열린 색) — 사용자가 고르기 전 비교용(2026-10-24).</summary>
        public static void RunPortalSizes()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "StageCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single); // 카메라는 상주 씬, 월드는 전투 씬(2026-11-04 씬 분할)
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            FitPlayerSprite(player);
            var visuals = Object.FindFirstObjectByType<StageVisuals>(); // 전투 씬에 놓인 것 — 그 씬의 땅·발판을 직렬화로 안다(2026-11-05 2b)
            visuals.Build();
            var portal = Object.FindFirstObjectByType<BossPortal>();
            var sr = portal.GetComponent<SpriteRenderer>();
            sr.color = portal.unlockedColor;
            float h = sr.sprite.bounds.size.y;
            foreach (float k in new[] { 1f, 1.5f, 2f })
            {
                portal.transform.localScale = new Vector3(k, k, 1f);
                portal.transform.position = new Vector3(FieldLayout.BossPortalX, FieldBounds.GroundY + h * k / 2f, 0f);
                Shot(cam, visuals, player, outDir, "S" + k.ToString("0.0"), FieldLayout.BossPortalX - 1.2f, FieldBounds.GroundY);
            }
            Debug.Log("[CaptureStagePreview] 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        static void Shot(Camera cam, StageVisuals visuals, GameObject player, string dir, string name, float x, float feetY,
                         float? camYOverride = null)
        {
            if (player != null) player.transform.position = new Vector3(x, feetY + EntitySizeConfig.PlayerRadius, 0f);
            float camX = Mathf.Clamp(x, FieldBounds.MinX + StageArtConfig.ViewHalfWidth, FieldBounds.MaxX - StageArtConfig.ViewHalfWidth);
            cam.transform.position = new Vector3(camX, camYOverride ?? CameraFollow2D.DesiredY(feetY), -10f);
            cam.orthographicSize = StageArtConfig.ViewHalfHeight;
            visuals.PlaceBackground(camX, cam.transform.position.y); // 로비처럼 배경을 안 쓰는 무대는 건너뛴다

            var rt = new RenderTexture(StageArtConfig.RefWidth, StageArtConfig.RefHeight, 24, RenderTextureFormat.ARGB32)
            { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            cam.rect = new Rect(0f, 0f, 1f, 1f); // 그림 전체에 — 열 때 PixelPerfectCamera가 잘라 둔 Viewport Rect가 첫 장에 남지 않게(CaptureUiPreview.Render와 같은 이유)
            // 배치 모드 편집 화면 렌더에선 SRP 배처가 켜져 있으면 모든 스프라이트가 첫 텍스처(배경)로 찍혔다(2026-10-24).
            // 캡처할 때만 끄고 되돌린다 — 게임 설정은 안 바뀐다.
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            bool batcher = urp != null && urp.useSRPBatcher;
            if (urp != null) urp.useSRPBatcher = false;
            cam.Render();
            if (urp != null) urp.useSRPBatcher = batcher;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        /// <summary>실행 중 `PlayerRig.ApplySprite`가 하는 크기 맞추기를 편집 모드에서 똑같이(64px·128px/유닛이면 1배).</summary>
        static void FitPlayerSprite(GameObject player)
        {
            if (player == null) return;
            var sr = player.GetComponentInChildren<SpriteRenderer>();
            var rig = player.GetComponent<ChronoSavior.Characters.PlayerRig>();
            var art = rig != null ? rig.Art : null;
            if (sr != null && art != null && art.SpriteOf(CharacterId.Mage) != null)
            {
                sr.sprite = art.SpriteOf(CharacterId.Mage); // 실행 중엔 PlayerRig가 고른 캐릭터 그림으로 바꾼다
                sr.color = art.TintOf(CharacterId.Mage);
            }
            if (sr == null || sr.sprite == null) return;
            float h = sr.sprite.bounds.size.y;
            float s = EntitySizeConfig.PlayerHeight / h;
            sr.transform.localScale = new Vector3(s, s, 1f);
        }

        static string ArgValue(string key)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == key) return args[i + 1];
            return null;
        }
    }
}
