using System.IO;
using System.Linq;
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
            var visuals = Object.FindFirstObjectByType<StageVisuals>(); // 전투 씬에 놓인 것 — 그 씬의 땅·발판을 직렬화로 안다(2026-11-05 2b)
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
                Skill = ChronoSavior.Core.SkillSlotState.Cooldown(1.2f, 3f),
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
                Skill = ChronoSavior.Core.SkillSlotState.Cooldown(0f, 3f, 0, 3, noStack: true),
                Player = new Vector2(24.8f, FieldBounds.GroundY), PortalInRange = true, PortalUnlocked = true, BossRatio = 0.55f,
            };
            Shot(pc, cam, outDir, "전투HUD_보스바·포탈", sel => hud.ShowForCapture(sel));
            // 보스전 — 체력바는 상단 가운데, 보스 이름 아래(2026-11-10)
            ChronoSavior.Core.RunState.Begin(1, ChronoSavior.Core.RunMode.Boss);
            hud.Preview = new CombatHud.PreviewState
            {
                Hp = 64f, MaxHp = 100f, ExpRatio = 0.4f, TimeLeft = 75f, Combo = 5,
                Skill = ChronoSavior.Core.SkillSlotState.Cooldown(0.8f, 3f),
                Player = new Vector2(4f, FieldBounds.GroundY), BossRatio = 0.62f,
            };
            Shot(pc, cam, outDir, "전투HUD_보스전", sel => hud.ShowForCapture(sel));
            ChronoSavior.Core.RunState.Reset();

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

            // 걸어 다니는 로비(10_Lobby, 2026-11-05 씬 분할 2차) — 사냥 월드를 숨기고 로비 씬을 겹쳐 열어, 건물을 세우고 아바타를 가챠 건물 앞에
            foreach (var root in UnityEngine.SceneManagement.SceneManager.GetSceneByPath(SceneSetupValidator.CombatPath).GetRootGameObjects()) root.SetActive(false);
            var lobbyScene = EditorSceneManager.OpenScene(SceneSetupValidator.LobbyPath, OpenSceneMode.Additive);
            var lobbyWorld = System.Linq.Enumerable.First(System.Linq.Enumerable.SelectMany(lobbyScene.GetRootGameObjects(), r => r.GetComponentsInChildren<LobbyWorld>(true)));
            var lobbyVisuals = System.Linq.Enumerable.First(System.Linq.Enumerable.SelectMany(lobbyScene.GetRootGameObjects(), r => r.GetComponentsInChildren<StageVisuals>(true)));
            var lobbyHud = pc.GetComponent<LobbyHud>();
            lobbyHud.Camera = cam;
            lobbyWorld.EnterLobby();
            lobbyVisuals.Build();
            var avatar = lobbyWorld.Player != null ? lobbyWorld.Player.gameObject : null;
            // 로비 카메라는 가운데에 서 있다(가운데 기준점) — 캐릭터는 가챠 건물 앞에
            PlaceWorld(cam, lobbyVisuals, avatar, LobbyWorld.CenterX, FieldBounds.GroundY);
            if (avatar != null) avatar.transform.position = new Vector3(LobbyWorld.XOf(1), avatar.transform.position.y, 0f);
            Shot(pc, cam, outDir, "로비", sel => lobbyHud.ShowForCapture(sel));
            var achieve = pc.GetComponent<AchievementsScreen>();
            Shot(pc, cam, outDir, "업적", sel => { lobbyHud.ShowForCapture(false); achieve.ShowForCapture(sel); });

            Debug.Log("[CaptureUiPreview] 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 마법사 이펙트(2026-11-07 VFX 스프린트)를 실제 전투 화면 배율로 찍는다 — `-executeMethod ChronoSavior.Editor.CaptureUiPreview.RunMageVfx`.
        /// 그림이 없는 클론이면 빈 화면만 찍힌다(팩을 먼저 가져올 것). 편집 화면이라 애니메이션은 첫 프레임 — 프레임 진행은 테스트가 본다.
        /// </summary>
        public static void RunMageVfx()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "UiCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            FitPlayerSprite(player);
            var visuals = Object.FindFirstObjectByType<StageVisuals>();
            visuals.Build();
            PlaceWorld(cam, visuals, player, 6f, FieldBounds.GroundY);
            var pc = PixelCanvas.Create(cam);
            foreach (var s in pc.GetComponents<PxScreen>()) s.HideForCapture();
            var made = new System.Collections.Generic.List<GameObject>();
            void Clear() { foreach (var go in made) if (go != null) Object.DestroyImmediate(go); made.Clear(); }

            // 1. 차지 오라 — 갈래마다 색(MageChargeVfx와 같은 키·배율·색)
            foreach (var branch in new[] { MageBranch.None, MageBranch.Explosion, MageBranch.Gravity })
            {
                var auraKey = ChronoSavior.Characters.MageChargeVfx.AuraKey(branch);
                var aura = ChronoSavior.Combat.VfxPlayer.Spawn(auraKey, player.transform.position, 1f,
                    ChronoSavior.Characters.MageChargeVfx.AuraFps, ChronoSavior.Characters.MageChargeVfx.AuraSortingOrder(player.GetComponent<SpriteRenderer>()), true, 0f,
                    ChronoSavior.Characters.MageChargeVfx.AuraTint(branch));
                if (aura != null) { aura.Play(ChronoSavior.Combat.VfxFrames.Get(auraKey).Skip(5).ToArray(), 15f, true, 0f, ChronoSavior.Characters.MageChargeVfx.AuraTint(branch)); made.Add(aura.gameObject); }
                Render(pc, cam, outDir, "mage_charge_" + branch.ToString().ToLowerInvariant(), 1920, 1080, 3f);
                Clear();
            }

            // 2. 탄 — 차지 0·완충(크기 1·1.9), 0차·폭발·중력
            var bolt = AssetDatabase.LoadAssetAtPath<ChronoSavior.Characters.MageProjectile>("Assets/Prefabs/Combat/MageBolt.prefab");
            float y = player.transform.position.y;
            int col = 0;
            foreach (var (explosive, gravity) in new[] { (false, false), (true, false), (false, true) })
            {
                foreach (float size in new[] { 1f, 1.9f })
                {
                    var b = ChronoSavior.Characters.MageProjectile.Spawn(bolt, new Vector3(7f + col * 0.9f, y + (size > 1f ? 0.9f : 0f), 0f), Vector2.zero, 1f, 1, 5f, size,
                        new Color(0.55f, 0.8f, 1f), false, false, explosive, 1f, gravity, 0f, 1, Vector2.zero, 1);
                    made.Add(b.gameObject);
                }
                col++;
            }
            Render(pc, cam, outDir, "mage_bolts", 1920, 1080, 3f);
            Clear();

            // 3. 불길(텔레포트 경로 — 발 높이 + 0.28에 5곳) + 화염 폭발(3차, 가운데 프레임)
            float feet = FieldBounds.GroundY;
            // 화염돌진: 바닥 불꽃(무작위 간격·모양, 다 솟은 상태) + 몸 뒤 분사(돌진 중간)
            var flameT = typeof(ChronoSavior.Combat.PathFlame).GetField("t", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var flameApply = typeof(ChronoSavior.Combat.PathFlame).GetMethod("Apply", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            for (float x = 3.4f; x <= 5.6f; x += Random.Range(ChronoSavior.Characters.MageAttack.FlameSpacingMin, ChronoSavior.Characters.MageAttack.FlameSpacingMax))
            {
                var f = ChronoSavior.Combat.PathFlame.Ignite(new Vector2(x, feet), 0f, 2f);
                if (f == null) continue;
                flameT.SetValue(f, 0.5f); flameApply.Invoke(f, null);
                made.Add(f.gameObject);
            }
            var dash = ChronoSavior.Combat.FireDashVfx.Play(new Vector2(4.6f, feet + 0.25f), new Vector2(7.2f, feet + 0.25f), 0.2f, null, 1f);
            typeof(ChronoSavior.Combat.FireDashVfx).GetField("t", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(dash, 0.07f);
            typeof(ChronoSavior.Combat.FireDashVfx).GetMethod("Apply", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(dash, null);
            made.Add(dash.gameObject);
            foreach (var p2 in Object.FindObjectsByType<ChronoSavior.Combat.VfxPlayer>(FindObjectsSortMode.None)) if (p2.name.Contains("dash_puff")) made.Add(p2.gameObject);
            float fr = ChronoSavior.Combat.MageSpecConfig.FireExplosionRadius(3);
            var fireFrames = ChronoSavior.Combat.VfxFrames.Get(ChronoSavior.Combat.VfxKeys.MageFireExplosion);
            if (fireFrames.Length > 0)
            {
                var boom = ChronoSavior.Combat.VfxPlayer.Spawn(ChronoSavior.Combat.VfxKeys.MageFireExplosion, new Vector2(9f, feet + 0.6f),
                    ChronoSavior.Core.DotScale.Value, 15f, ChronoSavior.Combat.MageSkillEffects.ExplosionSortingOrder);
                boom.Play(fireFrames.Skip(4).ToArray(), 15f, false, 0f, Color.white);
                made.Add(boom.gameObject);
            }
            Render(pc, cam, outDir, "mage_fire", 1920, 1080, 3f);
            Clear();

            // 4. 중력 — 중력장 구체(중첩 1·3·5, 다 커진 크기) + 착탄 폭발(2차·차지 0.6) + 대붕괴 폭발(가운데 프레임)과 고리
            var gravityFrames = ChronoSavior.Combat.VfxFrames.Get(ChronoSavior.Combat.VfxKeys.MageGravityExplosion);
            int k = 0;
            foreach (int stack in new[] { 1, 3, 5 })
            {
                var well = ChronoSavior.Combat.GravityWellZone.SpawnOrMerge(new Vector2(3.5f + k * 3.2f, feet + 1.4f), 1, 0f);
                for (int m = 1; m < stack; m++) well = ChronoSavior.Combat.GravityWellZone.SpawnOrMerge(well.transform.position, 2, 0f);
                var orb = well.GetComponentInChildren<ChronoSavior.Combat.GravityVortexVfx>();
                if (orb != null) orb.transform.localScale = Vector3.one * ChronoSavior.Core.DotScale.Value;
                made.Add(well.gameObject);
                k++;
            }
            Render(pc, cam, outDir, "mage_gravity_wells", 1920, 1080, 3f);
            Clear();
            ChronoSavior.Combat.GravityWellZone.ClearAllForTests();
            if (gravityFrames.Length > 0)
            {
                float gr = ChronoSavior.Combat.MageSpecConfig.GravityImpactRadius(2, 0.6f);
                var impact = ChronoSavior.Combat.VfxPlayer.Spawn(ChronoSavior.Combat.VfxKeys.MageGravityExplosion, new Vector2(4.5f, feet + 0.6f),
                    ChronoSavior.Core.DotScale.Value, 15f, ChronoSavior.Combat.MageSkillEffects.ExplosionSortingOrder);
                impact.Play(gravityFrames.Skip(3).ToArray(), 15f, false, 0f, Color.white);
                made.Add(impact.gameObject);
                float cr = ChronoSavior.Combat.MageSpecConfig.CollapseRadius(3, 3);
                var collapseFrames = ChronoSavior.Combat.VfxFrames.Get(ChronoSavior.Combat.VfxKeys.MageCollapseExplosion);
                var collapse = ChronoSavior.Combat.VfxPlayer.Spawn(ChronoSavior.Combat.MageSkillEffects.CollapseArtKey(3, false), new Vector2(10f, feet + 1.4f),
                    ChronoSavior.Core.DotScale.Value, 15f, ChronoSavior.Combat.MageSkillEffects.ExplosionSortingOrder);
                collapse.Play(collapseFrames.Skip(3).ToArray(), 15f, false, 0f, Color.white);
                made.Add(collapse.gameObject);
            }
            Render(pc, cam, outDir, "mage_gravity_bursts", 1920, 1080, 3f);
            Clear();
            Debug.Log("[CaptureUiPreview] 마법사 이펙트 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 메카닉·살수 이펙트(2026-11-08)를 전투 화면 배율로 찍는다 — `-executeMethod ChronoSavior.Editor.CaptureUiPreview.RunMechanicAssassinVfx`.
        /// 편집 화면이라 실제 키트 코드(빔 갱신·장판 그림·탄·설치기)를 직접 부르고, 살수 상태는 캡처용으로 비공개 필드를 채운다.
        /// </summary>
        public static void RunMechanicAssassinVfx()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "UiCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            FitPlayerSprite(player);
            var visuals = Object.FindFirstObjectByType<StageVisuals>();
            visuals.Build();
            PlaceWorld(cam, visuals, player, 6f, FieldBounds.GroundY);
            var pc = PixelCanvas.Create(cam);
            foreach (var s in pc.GetComponents<PxScreen>()) s.HideForCapture();
            float feet = FieldBounds.GroundY;
            var kit = player.GetComponent<ChronoSavior.Characters.GunnerAttack>();
            var made = new System.Collections.Generic.List<GameObject>();
            void Clear() { foreach (var go in made) if (go != null) Object.DestroyImmediate(go); made.Clear(); }

            // 1. 레이저 — 층마다 굵기(1·3·5층)
            foreach (int tier in new[] { 1, 3, 5 })
            {
                var beam = new ChronoSavior.Characters.GunnerLaserBeam(kit, player.GetComponent<ChronoSavior.Characters.CharacterMover2D>());
                beam.Update(0.016f, true, tier);
                var bv = beam.Vfx; // 편집 화면이라 시간이 안 흐른다 — 켜진 뒤 0.6초(반복 구간)로 옮겨 찍는다
                if (bv != null)
                {
                    var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                    typeof(ChronoSavior.Characters.GunnerBeamVfx).GetField("t", bf).SetValue(bv, 0.6f);
                    typeof(ChronoSavior.Characters.GunnerBeamVfx).GetMethod("Apply", bf).Invoke(bv, null);
                }
                var root = GameObject.Find("GunnerBeam");
                if (root != null) made.Add(root);
                Render(pc, cam, outDir, "mech_laser_t" + tier, 1920, 1080, 3f);
                Clear();
            }

            // 2. 드론 탄(펠렛) + 설치기 셋(중계기 머리) + 링크·내부 장판
            var bulletPrefab = AssetDatabase.LoadAssetAtPath<ChronoSavior.Characters.GunnerBullet>("Assets/Prefabs/Combat/GunnerBullet.prefab");
            for (int i = 0; i < 3; i++)
                made.Add(ChronoSavior.Characters.GunnerBullet.Spawn(bulletPrefab, new Vector3(4.6f + i * 0.5f, feet + 1.3f, 0f), new Vector2(1f, -0.2f * i), 1f, 1, 5f, 1f + i * 0.4f,
                    ChronoSavior.Characters.GunnerAttack.LaserColor, artKey: ChronoSavior.Combat.VfxKeys.MechanicPellet).gameObject);
            var turretPrefab = AssetDatabase.LoadAssetAtPath<ChronoSavior.Characters.GunnerTurret>("Assets/Prefabs/Combat/GunnerTurret.prefab");
            var turrets = new System.Collections.Generic.List<ChronoSavior.Characters.GunnerTurret>();
            foreach (float x in new[] { 7.2f, 8.4f, 9.6f })
            {
                var t = ChronoSavior.Characters.GunnerTurret.Spawn(turretPrefab, new Vector2(x, feet), 3);
                turrets.Add(t); made.Add(t.gameObject);
            }
            // 드론(문서 그림) — 편집 화면이라 그림 갱신을 직접 부른다
            var dronePrefab = AssetDatabase.LoadAssetAtPath<ChronoSavior.Characters.GunnerDrone>("Assets/Prefabs/Combat/GunnerDrone.prefab");
            foreach (float dx in new[] { 0f, 0.6f })
            {
                var dr = Object.Instantiate(dronePrefab, new Vector3(5.2f + dx, feet + 1.0f + dx * 0.3f, 0f), Quaternion.identity);
                var flagsP = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                typeof(ChronoSavior.Characters.GunnerDrone).GetProperty("Life").SetValue(dr, 5f);
                typeof(ChronoSavior.Characters.GunnerDrone).GetProperty("Age").SetValue(dr, 1f);
                typeof(ChronoSavior.Characters.GunnerDrone).GetMethod("DrawVisual", flagsP).Invoke(dr, null);
                made.Add(dr.gameObject);
            }
            var pts = ChronoSavior.Characters.GunnerField.FieldPoints(turrets);
            var segs = ChronoSavior.Characters.GunnerField.LinkSegments(pts, ChronoSavior.Characters.GunnerSpecConfig.LinkMax(3));
            var field = player.GetComponent<ChronoSavior.Characters.GunnerFieldVfx>();
            field.Show(pts, segs, 3, kit.bulletSprite);
            var fieldRoot = GameObject.Find("GunnerFieldLinks");
            if (fieldRoot != null) made.Add(fieldRoot);
            Render(pc, cam, outDir, "mech_field", 1920, 1080, 3f);
            Clear();

            // 3. 살수 — 집중(배경 어둡게·하트비트) + 궤적, 파열(섬광·스플래터), 칼날폭풍(회전베기·연기)
            var blade = player.GetComponent<ChronoSavior.Characters.BladeCombat>();
            var bvfx = player.GetComponent<ChronoSavior.Characters.BladeVfx>();
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var bt = typeof(ChronoSavior.Characters.BladeCombat);
            GameState.Set(GameScene.Run);
            ProfileService.Current.bladeTier = 4;
            var trail = (System.Collections.Generic.List<Vector2>)bt.GetField("trailPts", flags).GetValue(blade);
            trail.Clear();
            for (int i = 0; i < 18; i++) trail.Add(new Vector2(3.6f + i * 0.24f, feet + 0.45f + Mathf.Sin(i * 0.5f) * 0.35f));
            bt.GetField("trailT", flags).SetValue(blade, 2f);
            bt.GetField("focusT", flags).SetValue(blade, 1.5f);
            for (int k = 0; k < 20; k++) bvfx.Refresh(0.05f); // 어두운 판이 다 차게
            foreach (var n in new[] { "FocusShade", "FocusHeartbeat", "BladeTrailWide", "BladeTrailThin" }) { var g = GameObject.Find(n); if (g != null) made.Add(g); }
            Render(pc, cam, outDir, "blade_focus", 1920, 1080, 3f);
            bt.GetField("focusT", flags).SetValue(blade, 0f);
            for (int k = 0; k < 20; k++) bvfx.Refresh(0.05f);
            typeof(ChronoSavior.Characters.BladeVfx).GetMethod("OnTrailRipped", flags).Invoke(bvfx, new object[] { trail.ToArray() });
            trail.Clear();
            bvfx.Refresh(0.0f);
            foreach (var p2 in Object.FindObjectsByType<ChronoSavior.Combat.VfxPlayer>(FindObjectsSortMode.None)) made.Add(p2.gameObject);
            Render(pc, cam, outDir, "blade_rip", 1920, 1080, 3f);
            Clear();
            bt.GetField("storming", flags).SetValue(blade, true);
            for (int i = 0; i < 6; i++)
            {
                player.transform.position = new Vector3(4.2f + i * 0.4f, feet + 1.4f - i * 0.2f, 0f);
                bvfx.Refresh(0.016f);
            }
            foreach (var p2 in Object.FindObjectsByType<ChronoSavior.Combat.VfxPlayer>(FindObjectsSortMode.None)) made.Add(p2.gameObject);
            Render(pc, cam, outDir, "blade_storm", 1920, 1080, 3f);
            Clear();
            Debug.Log("[CaptureUiPreview] 메카닉·살수 이펙트 저장: " + outDir);
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
            // 그림 전체에 그린다 — 씬을 열 때 PixelPerfectCamera가 배치 창 비율에 맞춰 Viewport Rect를 잘라 두어(예: 1920×810),
            // 첫 장만 화면이 좁게 잡혀 UI가 가장자리로 밀려 찍혔다(2026-11-05 씬 분할 마감 4-3). 그 뒤 장은 첫 렌더 때 PPC가 다시 맞춰 괜찮았다.
            cam.rect = new Rect(0f, 0f, 1f, 1f);
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
            visuals.transform.Find("Background").position = StageVisuals.BackgroundPosition(visuals.Art, camX, cam.transform.position.y);
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
