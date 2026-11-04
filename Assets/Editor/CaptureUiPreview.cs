using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// Figma UI 화면을 PNG로 찍는다(2026-10-24) — 1스테이지 게임 화면 위에 UI를 겹쳐서 두 가지로:
    /// `이름_1080.png` = 1920×1080(UI 3배, 실제로 보이는 모습), `이름_360.png` = 640×360(UI 1배, 선택 표시 없이 — Figma 그림과 1:1 비교용).
    /// `-captureSizes 857x482,1072x602`를 주면 그 창 크기로도 찍는다(`이름_857x482.png`, 배율은 게임과 같은 <see cref="PixelCanvas.ScaleFor"/>).
    /// 씬을 열기만 하고 저장하지 않는다.
    ///
    /// 배치 실행(그래픽 필요 — `-nographics` 빼고):
    ///   Unity.exe -batchmode -projectPath . -executeMethod ChronoSavior.Editor.CaptureUiPreview.Run -captureOut &lt;폴더&gt; -quit
    /// </summary>
    public static class CaptureUiPreview
    {
        static Vector2Int[] extraSizes = new Vector2Int[0];

        public static void Run()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "UiCapture");
            Directory.CreateDirectory(outDir);
            extraSizes = ParseSizes(ArgValue("-captureSizes"));

            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single); // 카메라는 상주 씬, 월드는 전투 씬(2026-11-04 씬 분할)
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            FitPlayerSprite(player);
            var visuals = new GameObject("StageVisuals").AddComponent<StageVisuals>();
            visuals.Build();
            PlaceWorld(cam, visuals, player, 4f, FieldBounds.GroundY);

            var pc = PixelCanvas.Create(cam);
            var pause = pc.GetComponent<PauseMenu>();
            var settings = pc.GetComponent<SettingsWindow>();
            var hud = pc.GetComponent<CombatHud>();
            hud.Preview = new CombatHud.PreviewState
            {
                Hp = 72f, MaxHp = 100f, ExpRatio = 0.62f, TimeLeft = 47f, Combo = 32, Kills = 37,
                Attack = default,
                Skill = ChronoSavior.Characters.SkillSlotState.Cooldown(1.2f, 3f),
                Player = new Vector2(4f, FieldBounds.GroundY),
                Enemies = new[] { new Vector2(7f, 0f), new Vector2(9.5f, 1.35f), new Vector2(15f, 0f), new Vector2(18.6f, 2.7f), new Vector2(22f, 4.05f) },
            };

            Shot(pc, cam, outDir, "전투HUD", sel => hud.ShowForCapture(sel));
            // Figma 그림과 같은 값(남은 10초·COMBO 32·체력 가득) — 글자 자리를 1:1로 잰다
            hud.Preview = new CombatHud.PreviewState
            {
                Hp = 100f, MaxHp = 100f, ExpRatio = 0f, TimeLeft = 10f, Combo = 32, Kills = 0,
                Player = new Vector2(4f, FieldBounds.GroundY),
            };
            Shot(pc, cam, outDir, "전투HUD_Figma값", sel => hud.ShowForCapture(sel));
            hud.Preview = new CombatHud.PreviewState
            {
                Hp = 18f, MaxHp = 100f, ExpRatio = 0.9f, TimeLeft = 8f, Combo = 0, Kills = 100,
                Skill = ChronoSavior.Characters.SkillSlotState.Cooldown(0f, 3f, 0, 3, noStack: true),
                Player = new Vector2(24.8f, FieldBounds.GroundY), PortalInRange = true, PortalUnlocked = true, BossRatio = 0.55f,
            };
            Shot(pc, cam, outDir, "전투HUD_보스바·포탈", sel => hud.ShowForCapture(sel));

            Shot(pc, cam, outDir, "일시정지", sel => pause.ShowForCapture(sel));
            Shot(pc, cam, outDir, "설정", sel => { pause.ShowForCapture(false); settings.ShowForCapture(sel); });

            var title = pc.GetComponent<TitleMenu>();
            var popup = pc.GetComponent<CommonPopup>();
            Shot(pc, cam, outDir, "타이틀", sel => title.ShowForCapture(sel));
            Shot(pc, cam, outDir, "공용팝업", sel =>
            {
                title.ShowForCapture(false);
                CommonPopup.Confirm("새 원정", "지금까지의 기록이 모두 지워집니다.\n처음부터 새로 시작할까요?", null, "시작");
                popup.ShowForCapture(sel);
            });

            var regress = pc.GetComponent<RegressionScreen>();
            Shot(pc, cam, outDir, "시간회귀", sel => regress.ShowForCapture(sel));

            var relic = pc.GetComponent<RelicScreen>();
            var prof = ProfileService.Current;
            prof.shards = 400;
            ChronoSavior.Systems.GachaService.Pull(prof, 20); // 칸이 차 보이게(저장 안 함)
            prof.shards = 130;
            Shot(pc, cam, outDir, "유물개방", sel => relic.ShowForCapture(sel));

            var awaken = pc.GetComponent<AwakenScreen>();
            prof.character = CharacterId.Mage;
            prof.level = 6;               // SP 5
            prof.mageBranch = MageBranch.Gravity;
            prof.mageTier = 2;            // Figma 예시처럼 1줄 두 칸 배움, 2줄 막힘
            prof.gold = 2400;
            Shot(pc, cam, outDir, "각성", sel => awaken.ShowForCapture(sel));

            var stage = pc.GetComponent<StageSelectScreen>();
            Shot(pc, cam, outDir, "스테이지선택", sel => stage.ShowForCapture(sel));
            var charSel = pc.GetComponent<CharacterSelectScreen>();
            Shot(pc, cam, outDir, "캐릭터선택", sel => charSel.ShowForCapture(sel));
            var result = pc.GetComponent<RunResultScreen>();
            result.PreviewReason = RunEndReason.BossDead;
            Shot(pc, cam, outDir, "원정기록", sel => result.ShowForCapture(sel));
            result.PreviewReason = RunEndReason.Dead;
            Shot(pc, cam, outDir, "전투패배", sel => result.ShowForCapture(sel));

            // 걸어 다니는 로비 — 건물을 세우고 가챠 건물 앞에서
            var lobbyWorld = pc.GetComponent<LobbyWorld>();
            var lobbyHud = pc.GetComponent<LobbyHud>();
            lobbyWorld.EnterLobby();
            // 로비 카메라는 가운데에 서 있다(CameraFollow2D) — 캐릭터는 가챠 건물 앞에
            PlaceWorld(cam, visuals, player, LobbyWorld.CenterX, FieldBounds.GroundY);
            if (player != null) player.transform.position = new Vector3(LobbyWorld.XOf(1), player.transform.position.y, 0f);
            Shot(pc, cam, outDir, "로비", sel => lobbyHud.ShowForCapture(sel));
            var achieve = pc.GetComponent<AchievementsScreen>();
            Shot(pc, cam, outDir, "업적", sel => { lobbyHud.ShowForCapture(false); achieve.ShowForCapture(sel); });

            Debug.Log("[CaptureUiPreview] 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        /// <summary>블랙홀 마법사 착탄 폭발(`GravityImpactVfx`)을 시간대별로 찍는다 — `-executeMethod ChronoSavior.Editor.CaptureUiPreview.RunGravityVfx`.</summary>
        public static void RunGravityVfx()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "UiCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single); // 카메라는 상주 씬, 월드는 전투 씬(2026-11-04 씬 분할)
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            FitPlayerSprite(player);
            var visuals = new GameObject("StageVisuals").AddComponent<StageVisuals>();
            visuals.Build();
            PlaceWorld(cam, visuals, player, 6f, FieldBounds.GroundY);
            var pc = PixelCanvas.Create(cam);
            foreach (var s in pc.GetComponents<PxScreen>()) s.HideForCapture();
            float r = ChronoSavior.Combat.MageSpecConfig.GravityImpactRadius(2, 0.6f);
            var vfx = ChronoSavior.Combat.GravityImpactVfx.Spawn(new Vector2(9f, FieldBounds.GroundY + 0.5f), r);
            foreach (float t in new[] { 0.0f, 0.14f, 0.27f, 0.40f, 0.55f })
            {
                vfx.ShowAt(t);
                Render(pc, cam, outDir, $"gravity_impact_{(int)(t * 100):D2}", 1920, 1080, 3f);
            }
            // 중력장이 남아 있는 동안(착탄 폭발이 끝난 뒤) 소용돌이가 도는 모습
            Object.DestroyImmediate(vfx.gameObject);
            var well = ChronoSavior.Combat.GravityWellZone.SpawnOrMerge(new Vector2(9f, FieldBounds.GroundY + 0.5f), 2, 0.6f);
            var vortex = well.GetComponentInChildren<ChronoSavior.Combat.GravityVortexVfx>();
            foreach (float t in new[] { 0.8f, 0.93f, 1.07f, 1.2f, 1.33f, 1.47f })
            {
                vortex.ApplyAt(t, 3f);
                Render(pc, cam, outDir, $"gravity_vortex_{(int)(t * 100):D3}", 1920, 1080, 3f);
            }
            vortex.ApplyAt(1.0f, 0.2f);
            Render(pc, cam, outDir, "gravity_vortex_ending", 1920, 1080, 3f);
            Debug.Log("[CaptureUiPreview] 폭발 반지름 " + r + " 중력장 반지름 " + well.Radius + " 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        static void Shot(PixelCanvas pc, Camera cam, string dir, string name, System.Action<bool> show)
        {
            HideAll(pc);
            show(true);
            Render(pc, cam, dir, name + "_1080", 1920, 1080, 3f);
            HideAll(pc);
            show(false);
            Render(pc, cam, dir, name + "_360", 640, 360, 1f);
            foreach (var size in extraSizes)
            {
                HideAll(pc);
                show(true);
                Render(pc, cam, dir, $"{name}_{size.x}x{size.y}", size.x, size.y, PixelCanvas.ScaleFor(size.x, size.y));
            }
        }

        /// <summary>"857x482,1072x602" → 창 크기들.</summary>
        static Vector2Int[] ParseSizes(string arg)
        {
            if (string.IsNullOrEmpty(arg)) return new Vector2Int[0];
            var list = new System.Collections.Generic.List<Vector2Int>();
            foreach (var part in arg.Split(','))
            {
                var wh = part.Split('x');
                if (wh.Length == 2 && int.TryParse(wh[0], out int w) && int.TryParse(wh[1], out int h)) list.Add(new Vector2Int(w, h));
            }
            return list.ToArray();
        }

        static void HideAll(PixelCanvas pc)
        {
            foreach (var s in pc.GetComponents<PxScreen>()) s.HideForCapture();
        }

        static void Render(PixelCanvas pc, Camera cam, string dir, string name, int w, int h, float scale)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            pc.UseCamera(cam); // 캔버스 크기를 새 그림 크기로 다시 잡게
            pc.Scaler.scaleFactor = scale;
            pc.Canvas.scaleFactor = scale;
            Canvas.ForceUpdateCanvases();

            // 배치 모드 편집 화면 렌더에선 SRP 배처를 끈다(CaptureStagePreview와 같은 이유).
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            bool batcher = urp != null && urp.useSRPBatcher;
            if (urp != null) urp.useSRPBatcher = false;
            cam.Render();
            if (urp != null) urp.useSRPBatcher = batcher;

            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        /// <summary>게임 화면 자리 — CaptureStagePreview.Shot과 같은 배치(플레이어·카메라·배경).</summary>
        static void PlaceWorld(Camera cam, StageVisuals visuals, GameObject player, float x, float feetY)
        {
            if (player != null) player.transform.position = new Vector3(x, feetY + EntitySizeConfig.PlayerRadius, 0f);
            float camX = Mathf.Clamp(x, FieldBounds.MinX + StageArtConfig.ViewHalfWidth, FieldBounds.MaxX - StageArtConfig.ViewHalfWidth);
            cam.transform.position = new Vector3(camX, CameraFollow2D.DesiredY(feetY), -10f);
            cam.orthographicSize = StageArtConfig.ViewHalfHeight;
            visuals.transform.Find("Background").position = StageVisuals.BackgroundPosition(camX, cam.transform.position.y);
        }

        static void FitPlayerSprite(GameObject player)
        {
            if (player == null) return;
            var sr = player.GetComponentInChildren<SpriteRenderer>();
            var rig = player.GetComponent<ChronoSavior.Characters.PlayerRig>();
            var art = rig != null ? rig.Art : null;
            if (sr != null && art != null && art.SpriteOf(CharacterId.Mage) != null)
            {
                sr.sprite = art.SpriteOf(CharacterId.Mage);
                sr.color = art.TintOf(CharacterId.Mage);
            }
            if (sr == null || sr.sprite == null) return;
            float s = EntitySizeConfig.PlayerHeight / sr.sprite.bounds.size.y;
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
