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

            // 보유 유물 칸(2026-11-19) — 슬롯 3(중력 법사 풀세팅) 프로필로, 몇 칸은 막 발동한 것처럼 반짝이게
            ChronoSavior.Core.ProfileService.Replace(ChronoSavior.Systems.SavePresets.Build(3));
            hud.Preview = new CombatHud.PreviewState
            {
                Hp = 72f, MaxHp = 100f, ExpRatio = 0.62f, TimeLeft = 47f, Combo = 32, Kills = 37,
                Attack = default, Skill = ChronoSavior.Core.SkillSlotState.Cooldown(1.2f, 3f),
                Player = new Vector2(4f, FieldBounds.GroundY), Enemies = new Vector2[0],
            };
            foreach (var id in new[] { "lantern", "gravitySpring", "doomBell" }) ChronoSavior.Core.Relics.Proc(id);
            Shot(pc, cam, outDir, "전투HUD_유물", sel => hud.ShowForCapture(sel));
            ChronoSavior.Core.ProfileService.Reset();

            Shot(pc, cam, outDir, "일시정지", sel => pause.ShowForCapture(sel));
            Shot(pc, cam, outDir, "설정", sel => { pause.ShowForCapture(false); settings.ShowForCapture(sel); });

            var title = pc.GetComponent<TitleMenu>();
            var popup = pc.GetComponent<CommonPopup>();
            Shot(pc, cam, outDir, "타이틀", sel => title.ShowForCapture(sel));
            ChronoSavior.Systems.SaveService.EnsureSlots(); // 슬롯 창(2026-11-19) — 비어 있으면 시나리오 처음 상태로
            var slots = pc.GetComponent<SaveSlotScreen>();
            Shot(pc, cam, outDir, "세이브슬롯", sel => { title.ShowForCapture(false); slots.ShowForCapture(sel); });
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
            // 몬스터 칸·보스 그림을 실제 에셋으로 보이게(게임에선 00_Persistent 씬에 연결돼 있다, 2026-11-20)
            if (stage.Catalog == null) stage.Catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
            Shot(pc, cam, outDir, "스테이지선택", sel => stage.ShowForCapture(sel));
            // 2026-11-19 챕터 구조 — 1챕터 1~5를 깬 상태면 보스 쪽(빨간 화면)이 열린다
            for (int st = 1; st <= ChapterConfig.StagesPerChapter; st++) prof.MarkStageCleared(1, st);
            Shot(pc, cam, outDir, "스테이지선택_보스", sel => stage.ShowForCapture(sel));
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
        /// <summary>
        /// 몹 지역 그림 캡처(2026-11-17 2스테이지 몬스터) — 같은 종류 넷(박쥐·고블린·멧돼지·오우거 자리)을 1지역·2지역으로 실제 스폰 코드(`EnemySetup.Apply`)로 세워
        /// `enemy_skins_region1/2`로 찍는다. 배치: `-executeMethod ChronoSavior.Editor.CaptureUiPreview.RunEnemySkins -captureOut &lt;폴더&gt;`.
        /// </summary>
        public static void RunEnemySkins()
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
            HideAll(pc);
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
            // (종류, 엘리트) — 땅에 서는 몹은 물리가 쉬는 자리(판정 바닥 = 지면)에 세운다. 발이 땅에 붙는지 보는 캡처(2026-11-17).
            var lineup = new[] { (EnemyType.Flyer, false), (EnemyType.Grunt, false), (EnemyType.Grunt, true), (EnemyType.Charger, false), (EnemyType.Charger, true), (EnemyType.Brute, false) };
            foreach (int region in new[] { 1, 2 })
            {
                RunState.Begin(region, RunMode.Normal);
                var made = new System.Collections.Generic.List<GameObject>();
                for (int i = 0; i < lineup.Length; i++)
                {
                    var (type, elite) = lineup[i];
                    var data = System.Array.Find(catalog.types, d => d != null && d.type == type);
                    var go = Object.Instantiate(data.prefab);
                    go.transform.position = new Vector3(6.8f + i * 0.62f, FieldBounds.GroundY + 0.5f, 0f);
                    ChronoSavior.Systems.EnemySetup.Apply(go, data, allowElite: elite, eliteChance: elite ? 1000f : 0f, shapeSprite: null);
                    var col = go.GetComponent<CircleCollider2D>();
                    float y = type == EnemyType.Flyer ? FieldBounds.GroundY + 1.1f : FieldBounds.GroundY + col.radius * go.transform.localScale.y;
                    go.transform.position = new Vector3(go.transform.position.x, y, 0f);
                    made.Add(go);
                }
                Render(pc, cam, outDir, "enemy_skins_region" + region, 1920, 1080, 3f);
                foreach (var go in made) Object.DestroyImmediate(go);
            }
            RunState.Reset();
        }

        /// <summary>
        /// 캐릭터 자세 캡처(2026-11-17 플립북 전환) — 세 캐릭터 × 자세(서기·이동·상승·하강·피격·공격, 마법사는 차지)를 각 묶음 가운데 프레임으로 실제 전투 화면 배율에 세운다.
        /// 위 줄은 왼쪽을 보고, 아래 줄은 뒤집어(오른쪽) — 발이 땅에 닿는지, 공격 그림에서 몸이 옆으로 튀지 않는지(빨간 세로 점 = 루트 x) 본다.
        /// 배치: `-executeMethod ChronoSavior.Editor.CaptureUiPreview.RunCharacterPoses -captureOut &lt;폴더&gt;`.
        /// </summary>
        public static void RunCharacterPoses()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "UiCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            var visuals = Object.FindFirstObjectByType<StageVisuals>();
            visuals.Build();
            PlaceWorld(cam, visuals, player, 6f, FieldBounds.GroundY);
            player.SetActive(false);
            var pc = PixelCanvas.Create(cam);
            HideAll(pc);
            var art = AssetDatabase.LoadAssetAtPath<CharacterArtSet>("Assets/Data/Characters/CharacterArtSet.asset");
            var marker = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0f), 128f);
            var made = new System.Collections.Generic.List<GameObject>();
            var poses = new[] { CharacterPose.Idle, CharacterPose.Move, CharacterPose.Rise, CharacterPose.Fall, CharacterPose.Hit, CharacterPose.Attack, CharacterPose.Charge };
            foreach (var who in new[] { CharacterId.Mage, CharacterId.Gunner, CharacterId.Blade })
            {
                var set = art.AnimSetOf(who);
                float scale = EntitySizeConfig.ScaleToHeight(set.idle.frames[0], art.HeightOf(who));
                for (int flip = 0; flip < 2; flip++)
                {
                    for (int i = 0; i < poses.Length; i++)
                    {
                        if (poses[i] == CharacterPose.Charge && set.charge == null) continue;
                        var book = set.Get(poses[i]);
                        float x = 3.9f + i * 0.62f;
                        float feetY = FieldBounds.GroundY + (flip == 0 ? 2.05f : 1.0f);
                        var go = new GameObject($"Pose_{who}_{poses[i]}");
                        go.transform.position = new Vector3(x, feetY + EntitySizeConfig.PlayerRadius, 0f);
                        go.transform.localScale = new Vector3(scale, scale, 1f);
                        var sr = go.AddComponent<SpriteRenderer>();
                        sr.sprite = book.frames[book.frames.Length / 2];
                        sr.flipX = flip == 1;
                        made.Add(go);
                        var m = new GameObject("RootX").AddComponent<SpriteRenderer>();
                        m.sprite = marker; m.color = Color.red; m.sortingOrder = 10;
                        m.transform.position = new Vector3(x, feetY + EntitySizeConfig.PlayerRadius * 2f + 0.06f, 0f);
                        m.transform.localScale = new Vector3(2f, 6f, 1f);
                        made.Add(m.gameObject);
                        var floor = new GameObject("Floor").AddComponent<SpriteRenderer>();
                        floor.sprite = marker; floor.color = new Color(1f, 1f, 1f, 0.5f); floor.sortingOrder = 10;
                        floor.transform.position = new Vector3(x, feetY - 1f / 128f, 0f);
                        floor.transform.localScale = new Vector3(72f, 1f, 1f);
                        made.Add(floor.gameObject);
                    }
                }
                Render(pc, cam, outDir, "poses_" + who.ToString().ToLowerInvariant(), 1920, 1080, 3f);
                foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
                made.Clear();
            }
            player.SetActive(true);
        }

        /// <summary>
        /// 마법사 차지 손 오브·일렁임 캡처(2026-11-17) — 씬 Player의 `MageChargeVfx` 칸에 연결된 그림 그대로, 차지 몸 그림 위에 겹쳐(오브 +1·일렁임 +2) 찍는다.
        /// 위 줄 0차·폭발(빨강), 아래 줄 블랙홀(보라) — 차지 30%·60%·85%(일렁임 시작)·100%, 그리고 오른쪽을 본 100%.
        /// 배치: `-executeMethod ChronoSavior.Editor.CaptureUiPreview.RunMageOrb -captureOut &lt;폴더&gt;`.
        /// </summary>
        public static void RunMageOrb()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "UiCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            var visuals = Object.FindFirstObjectByType<StageVisuals>();
            visuals.Build();
            PlaceWorld(cam, visuals, player, 6f, FieldBounds.GroundY);
            player.SetActive(false);
            var pc = PixelCanvas.Create(cam);
            HideAll(pc);
            var charge = player.GetComponent<ChronoSavior.Characters.MageChargeVfx>();
            var art = AssetDatabase.LoadAssetAtPath<CharacterArtSet>("Assets/Data/Characters/CharacterArtSet.asset");
            var set = art.AnimSetOf(CharacterId.Mage);
            float scale = EntitySizeConfig.ScaleToHeight(set.idle.frames[0], art.HeightOf(CharacterId.Mage));
            var made = new System.Collections.Generic.List<GameObject>();
            SpriteRenderer Put(string n, Sprite s, Vector3 pos, bool flip, int order)
            {
                var go = new GameObject(n);
                go.transform.position = pos;
                go.transform.localScale = new Vector3(scale, scale, 1f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = s; sr.flipX = flip; sr.sortingOrder = order;
                made.Add(go);
                return sr;
            }
            var ratios = new[] { 0.3f, 0.6f, 0.85f, 1f, 1f };
            var branches = new[] { MageBranch.None, MageBranch.Gravity };
            for (int row = 0; row < branches.Length; row++)
            {
                var orb = charge.OrbFrames(branches[row]);
                var ki = charge.KiFrames(branches[row]);
                for (int i = 0; i < ratios.Length; i++)
                {
                    bool flip = i == ratios.Length - 1;
                    var pos = new Vector3(4.2f + i * 0.75f, FieldBounds.GroundY + (row == 0 ? 2.05f : 1.0f) + EntitySizeConfig.PlayerRadius, 0f);
                    Put("Body", set.charge.frames[i % set.charge.Count], pos, flip, 0);
                    int idx = Mathf.Clamp(Mathf.RoundToInt(ratios[i] * (orb.Length - 1)), 0, orb.Length - 1);
                    Put("Orb", orb[idx], pos, flip, 1);
                    if (idx >= ChronoSavior.Characters.MageChargeVfx.KiEffectStartOrbFrame) Put("Ki", ki[i % ki.Length], pos, flip, 2);
                }
            }
            Render(pc, cam, outDir, "mage_orb_red_violet", 1920, 1080, 3f);
            foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            player.SetActive(true);
        }

        /// <summary>
        /// 보스 '덩어리 지배자' 몸 그림 캡처(2026-11-17) — 프리팹 칸에 연결된 묶음(튀어오르기·투척·이동·피격·사망)에서 한 장씩, 실제 스폰 크기 계산(`EnemySetup`)으로 세운다.
        /// 배치: `-executeMethod ChronoSavior.Editor.CaptureUiPreview.RunBossArt -captureOut &lt;폴더&gt;`.
        /// </summary>
        public static void RunBossArt()
        {
            string outDir = ArgValue("-captureOut") ?? Path.Combine(Directory.GetCurrentDirectory(), "Temp", "UiCapture");
            Directory.CreateDirectory(outDir);
            EditorSceneManager.OpenScene(SceneSetupValidator.PersistentPath, OpenSceneMode.Single);
            EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Additive);
            FieldLayout.SetBossArena(false);
            var cam = Camera.main;
            var player = GameObject.Find("Player");
            var visuals = Object.FindFirstObjectByType<StageVisuals>();
            visuals.Build();
            PlaceWorld(cam, visuals, player, 4f, FieldBounds.GroundY);
            var pc = PixelCanvas.Create(cam);
            HideAll(pc);
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
            var prefab = catalog.BossPrefabFor(1);
            var probe = prefab.GetComponent<ChronoSavior.Enemies.BlobLordBoss>();
            var shots = new[] { probe.BounceArt.frames[2], probe.ThrowArt.frames[3], probe.MoveArt.frames[1], probe.HitArt.frames[0], probe.DeathArt.frames[4] };
            var made = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < shots.Length; i++)
            {
                var go = Object.Instantiate(prefab);
                go.transform.position = new Vector3(4.9f + i * 1.15f, FieldBounds.GroundY + 0.6f, 0f);
                ChronoSavior.Systems.EnemySetup.SetupBoss(go, 1, catalog);
                go.GetComponent<SpriteRenderer>().sprite = shots[i];
                made.Add(go);
            }
            Render(pc, cam, outDir, "boss_art", 1920, 1080, 3f);
            foreach (var go in made) Object.DestroyImmediate(go);
        }

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

            // 1. 차지 손 오브·오브 일렁임(2026-11-16, 외주 팩 오라는 사용자 요청으로 뺐다) — 몇 단계 차지율을 강제로 넣어 찍는다.
            {
                var mageAttack = player.GetComponent<ChronoSavior.Characters.MageAttack>();
                var chargeVfx = player.GetComponent<ChronoSavior.Characters.MageChargeVfx>();
                var chargingField = typeof(ChronoSavior.Characters.MageAttack).GetField("charging", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var chargeTField = typeof(ChronoSavior.Characters.MageAttack).GetField("chargeT", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                foreach (var (label, ratio) in new[] { ("orb_early", 0.3f), ("orb_ki", 0.85f), ("orb_full", 1f) })
                {
                    chargingField.SetValue(mageAttack, true);
                    chargeTField.SetValue(mageAttack, ratio * mageAttack.chargeMax);
                    chargeVfx.Refresh(0f);
                    Render(pc, cam, outDir, "mage_charge_" + label, 1920, 1080, 3f);
                }
                chargingField.SetValue(mageAttack, false);
                chargeVfx.Refresh(0f); // 오브·일렁임을 다시 끈다 — 다음 캡처 블록에 안 남게
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

        /// <summary>
        /// 메카닉 "광선 연쇄" 미리보기(2026-11-23 사용자 — 그림은 검사받고 적용): 실제 전투 화면에 적 3마리, 1→2→3으로 튀는 모습을 1/30초마다 찍는다.
        /// 본 레이저와 같은 머티리얼·셰이더(GunnerChainBeamVfx). -captureOut 폴더에 chain_00.png… 로 저장.
        /// </summary>
        public static void RunChainBeam()
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

            // 적 3마리 — 1지역 쥐 그림(실제 몹 그림)
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
            var grunt = catalog.Find(EnemyType.Grunt);
            var skin = grunt.SkinFor(1);
            Sprite art = skin != null && skin.move != null && skin.move.frames.Length > 0 ? skin.move.frames[0] : grunt.sprite;
            var made = new System.Collections.Generic.List<GameObject>();
            var spots = new[] { new Vector2(5.0f, feet + 0.25f), new Vector2(7.4f, feet + 1.35f), new Vector2(9.6f, feet + 0.25f) };
            foreach (var p in spots)
            {
                var e = new GameObject("PreviewEnemy", typeof(SpriteRenderer));
                e.transform.position = p;
                e.transform.localScale = Vector3.one * DotScale.Value;
                var sr = e.GetComponent<SpriteRenderer>();
                sr.sprite = art;
                made.Add(e);
            }
            float halfW = art != null ? DotScale.Units(art.rect.width) * 0.3f : 0.3f;
            Vector2 Edge(Vector2 from, Vector2 to) => from + (to - from).normalized * halfW;

            var mat = kit.BeamMaterial;
            var hop1 = ChronoSavior.Characters.GunnerChainBeamVfx.Play(mat, Edge(spots[0], spots[1]), Edge(spots[1], spots[0]), 0f, 1f);
            var hop2 = ChronoSavior.Characters.GunnerChainBeamVfx.Play(mat, Edge(spots[1], spots[2]), Edge(spots[2], spots[1]), hop1.ArriveTime + 0.04f, -1f);
            made.Add(hop1.gameObject); made.Add(hop2.gameObject);
            float total = hop2.ArriveTime + hop2.Duration - hop2.ExtendTime + 0.05f;
            int frame = 0;
            for (float t = 0f; t <= total; t += 1f / 30f, frame++)
            {
                hop1.Evaluate(t);
                hop2.Evaluate(t);
                Render(pc, cam, outDir, $"chain_{frame:00}", 1920, 1080, 3f);
            }
            foreach (var p2 in Object.FindObjectsByType<ChronoSavior.Combat.VfxPlayer>(FindObjectsSortMode.None)) Object.DestroyImmediate(p2.gameObject);
            foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            Debug.Log("[CaptureUiPreview] 광선 연쇄 미리보기 저장: " + outDir + " (" + frame + "장)");
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 광선 연쇄 "맞은 효과" 후보 4개(2026-11-23 사용자 "레이저 맞은 햄스터 효과가 별로") — 받은 메카닉 VFX만, 적 피격 깜빡임(SpriteFlash)까지.
        /// -captureOut 폴더 아래 후보마다 하위 폴더에 1/30초 장면.
        /// </summary>
        public static void RunChainBeamHits()
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
            var flashMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteFlash.mat");
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
            var grunt = catalog.Find(EnemyType.Grunt);
            var skin = grunt.SkinFor(1);
            Sprite art = skin != null && skin.move != null && skin.move.frames.Length > 0 ? skin.move.frames[0] : grunt.sprite;
            var spots = new[] { new Vector2(5.0f, feet + 0.25f), new Vector2(7.4f, feet + 1.35f), new Vector2(9.6f, feet + 0.25f) };
            float halfW = art != null ? DotScale.Units(art.rect.width) * 0.3f : 0.3f;
            Vector2 Edge(Vector2 from, Vector2 to) => from + (to - from).normalized * halfW;
            Vector2 Center(Vector2 p) => p + new Vector2(0f, art != null ? DotScale.Units(art.rect.height) * 0.5f - DotScale.Units(art.pivot.y) : 0f);

            var variants = new (string name, string key, float fps, bool center, bool rotate)[]
            {
                ("A_지금_작은충격", ChronoSavior.Combat.VfxKeys.MechanicHitSpark, 30f, false, true),
                ("B_파란불꽃_흩어짐", ChronoSavior.Combat.VfxKeys.MechanicDroneHit, 30f, false, false),
                ("C_보라전기_고리", ChronoSavior.Combat.VfxKeys.MechanicZap, 20f, true, false),
                ("D_하얀번개_찌릿", ChronoSavior.Combat.VfxKeys.MechanicRelayAura, 30f, true, false),
            };
            foreach (var v in variants)
            {
                ChronoSavior.Characters.GunnerChainBeamVfx.HitKey = v.key;
                ChronoSavior.Characters.GunnerChainBeamVfx.HitFps = v.fps;
                ChronoSavior.Characters.GunnerChainBeamVfx.HitAtCenter = v.center;
                ChronoSavior.Characters.GunnerChainBeamVfx.HitRotate = v.rotate;
                string dir = Path.Combine(outDir, v.name);
                Directory.CreateDirectory(dir);
                var made = new System.Collections.Generic.List<GameObject>();
                var flashes = new SpriteRenderer[spots.Length];
                for (int i = 0; i < spots.Length; i++)
                {
                    var e = new GameObject("PreviewEnemy", typeof(SpriteRenderer));
                    e.transform.position = spots[i];
                    e.transform.localScale = Vector3.one * DotScale.Value;
                    e.GetComponent<SpriteRenderer>().sprite = art;
                    var f = new GameObject("Flash", typeof(SpriteRenderer));
                    f.transform.SetParent(e.transform, false);
                    var fsr = f.GetComponent<SpriteRenderer>();
                    fsr.sprite = art;
                    fsr.sharedMaterial = flashMat;
                    fsr.sortingOrder = 1;
                    fsr.color = new Color(1f, 1f, 1f, 0f);
                    flashes[i] = fsr;
                    made.Add(e);
                }
                var mat = kit.BeamMaterial;
                var hop1 = ChronoSavior.Characters.GunnerChainBeamVfx.Play(mat, Edge(spots[0], spots[1]), Edge(spots[1], spots[0]), 0f, 1f, Center(spots[1]));
                var hop2 = ChronoSavior.Characters.GunnerChainBeamVfx.Play(mat, Edge(spots[1], spots[2]), Edge(spots[2], spots[1]), hop1.ArriveTime + 0.04f, -1f, Center(spots[2]));
                made.Add(hop1.gameObject); made.Add(hop2.gameObject);
                float total = hop2.ArriveTime + 0.6f;
                int frame = 0;
                for (float t = 0f; t <= total; t += 1f / 30f, frame++)
                {
                    hop1.Evaluate(t);
                    hop2.Evaluate(t);
                    // 피격 깜빡임 — 게임과 같게 맞은 순간 1에서 초당 6씩 줄고, 흰색을 0.75 세기로 덧그린다(EnemyHealth)
                    float f1 = t >= hop1.ArriveTime ? Mathf.Max(0f, 1f - (t - hop1.ArriveTime) * 6f) : 0f;
                    float f2 = t >= hop2.ArriveTime ? Mathf.Max(0f, 1f - (t - hop2.ArriveTime) * 6f) : 0f;
                    flashes[1].color = new Color(1f, 1f, 1f, f1 * 0.75f);
                    flashes[2].color = new Color(1f, 1f, 1f, f2 * 0.75f);
                    Render(pc, cam, dir, $"hit_{frame:00}", 1920, 1080, 3f);
                }
                foreach (var p2 in Object.FindObjectsByType<ChronoSavior.Combat.VfxPlayer>(FindObjectsSortMode.None)) Object.DestroyImmediate(p2.gameObject);
                foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            }
            Debug.Log("[CaptureUiPreview] 맞은 효과 후보 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 편지 유물 메카닉 그림 후보(2026-11-23, 사용자 "편지부터 다시" → "레이저 쏘는 걸로 다시 찍어"). 게임 코드는 안 바꾸고 여기서만 재현한다.
        /// 최종 빌드 레이저(5층 곡선)로 가까운 적을 처치 → 처치된 적에서 연쇄 광선이 가까운 적 3마리에 동시에 뻗고(GPT 합의 — 레이저가 편지를 배달해야
        /// "레이저가 닿은 적만" 규칙을 지킨다) → 닿는 순간 머리 위 표식, 1초(마지막 0.3초 빠르게) → 폭발 그림 하나씩(0.06초 시차). 도트 배율 2 하나만.
        /// </summary>
        public static void RunLetterCandidates()
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
            var flashMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SpriteFlash.mat");
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
            var grunt = catalog.Find(EnemyType.Grunt);
            var skin = grunt.SkinFor(1);
            Sprite art = skin != null && skin.move != null && skin.move.frames.Length > 0 ? skin.move.frames[0] : grunt.sprite;
            // 0 = 레이저로 처치할 적, 1~3 = 편지 받을 적(가까운 순: 오른쪽 땅·오른쪽 발판·왼쪽 발판), 4 = 왼쪽 땅(4번째로 멀어 안 받는다)
            // 땅 위 적을 한 줄로 세우면 광선끼리 겹쳐 한 줄로 보였다(2차 캡처) — 방향이 서로 다르게 둔다. 화면 오른쪽 끝(9.9)은 잘렸다.
            var spots = new[] { new Vector2(7.4f, feet + 0.25f), new Vector2(8.6f, feet + 0.25f), new Vector2(8.8f, feet + 1.63f), new Vector2(5.0f, feet + 1.63f), new Vector2(4.2f, feet + 0.25f) };
            int[] letterTargets = { 1, 2, 3 };
            Vector2 Center(Vector2 p) => p + new Vector2(0f, DotScale.Units(art.rect.height * 0.5f - art.pivot.y));
            Vector2 Feet(Vector2 p) => p - new Vector2(0f, DotScale.Units(art.pivot.y));
            // 표식은 머리 위 — 몸에 겹치면 적이 가려져 누가 표식을 받았는지 안 보였다(1차 캡처)
            Vector2 Above(Vector2 p, float markPx) => p + new Vector2(0f, DotScale.Units(art.rect.height - art.pivot.y + markPx * 0.5f + 1f));
            float halfW = DotScale.Units(art.rect.width) * 0.3f;
            Vector2 Edge(Vector2 from, Vector2 to) => from + (to - from).normalized * halfW;
            Vector2 Bez(Vector2 a0, Vector2 c0, Vector2 b0, float u) => (1f - u) * (1f - u) * a0 + 2f * (1f - u) * u * c0 + u * u * b0;

            // (이름, 표식 키, 표식 크기(그림 px), 표식 fps, 폭발 키(없으면 null), 폭발 fps, 건너뛸 장 수, 미리 시작할 장 수(그림이 닿는 장을 피해 순간에 맞춘다), 발밑 기준, 그림 가운데~바닥 px,
            //  하늘에서 레이저가 내리꽂힘, 그 레이저가 닿을 때 하얀 번개(승인 D))
            // 2차(2026-11-23 사용자 "전기 기둥 구리다, 빛이나 전기 효과처럼"): 레이저 낙하·전기 폭발
            // 3차(2026-11-23 사용자 — 전기 기둥(B)으로 되돌린 뒤 "이거 모양좀 어떻게 할 수 없나" = 6번째 장부터의 물결 번개):
            //  frames = 쓸 장(null이면 전부), fade = 끝에 사라지는 시간, hitKey = 닿는 순간 적 가운데 함께 띄울 그림
            var variants = new (string name, string mark, float markPx, float markFps, string boom, float boomFps, int skip, int preRoll, bool atFeet, float bottomPx, bool skyBeam, bool skySpark,
                                int[] frames, float fade, string hitKey)[]
            {
                ("A_지금_전기기둥", "mechanic/pellet", 16f, 10f, "mechanic/warp", 24f, 0, 4, true, 58f, false, false, null, 0f, null),
                ("가_물결번개지움", "mechanic/pellet", 16f, 10f, "mechanic/warp_ga", 24f, 0, 4, true, 58f, false, false, null, 0f, null),
                ("나_뾰족번개합성", "mechanic/pellet", 16f, 10f, "mechanic/warp_na", 24f, 0, 4, true, 58f, false, false, null, 0f, null),
            };
            const float KillAt = 0.6f, Fuse = 1f, Rush = 0.3f, Stagger = 0.06f, Dt = 1f / 30f;
            const int Tier = 5;
            var bf = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var tField = typeof(ChronoSavior.Characters.GunnerBeamVfx).GetField("t", bf);
            var impactTField = typeof(ChronoSavior.Characters.GunnerBeamVfx).GetField("impactT", bf);
            var applyM = typeof(ChronoSavior.Characters.GunnerBeamVfx).GetMethod("Apply", bf);
            foreach (var v in variants)
            {
                string dir = Path.Combine(outDir, v.name);
                Directory.CreateDirectory(dir);
                var made = new System.Collections.Generic.List<GameObject>();
                var bodies = new GameObject[spots.Length];
                var flashes = new SpriteRenderer[spots.Length];
                for (int i = 0; i < spots.Length; i++)
                {
                    var e = new GameObject("PreviewEnemy", typeof(SpriteRenderer));
                    e.transform.position = spots[i];
                    e.transform.localScale = Vector3.one * DotScale.Value;
                    e.GetComponent<SpriteRenderer>().sprite = art;
                    var f = new GameObject("Flash", typeof(SpriteRenderer));
                    f.transform.SetParent(e.transform, false);
                    var fsr = f.GetComponent<SpriteRenderer>();
                    fsr.sprite = art;
                    fsr.sharedMaterial = flashMat;
                    fsr.sortingOrder = 1;
                    fsr.color = new Color(1f, 1f, 1f, 0f);
                    flashes[i] = fsr;
                    bodies[i] = e;
                    made.Add(e);
                }

                // 본 레이저 — 실제 게임과 같은 5층 곡선 경로(총구 → 앞으로 나갔다가 목표로 휜다, 그림은 목표 가장자리에서 끝)
                var beam = ChronoSavior.Characters.GunnerBeamVfx.Create(kit.BeamMaterial, kit.LaserTipPrefab);
                made.Add(beam.gameObject);
                Vector2 o = kit.FeetPosition + new Vector2(ChronoSavior.Characters.GunnerSpecConfig.BeamOriginForward, ChronoSavior.Characters.GunnerSpecConfig.BeamOriginHeight);
                Vector2 tc = Center(spots[0]);
                Vector2 tv = tc - o;
                float len = tv.magnitude;
                Vector2 ctrl = new Vector2(o.x + ChronoSavior.Characters.GunnerSpecConfig.BeamCurveAhead(len), o.y);
                Vector2 end = tc + tv / len * ChronoSavior.Characters.GunnerSpecConfig.BeamCurveOvershoot;
                var path = new System.Collections.Generic.List<Vector2> { o };
                for (int i = 1; i <= ChronoSavior.Characters.GunnerSpecConfig.BeamCurveSamples; i++)
                {
                    Vector2 q = Bez(o, ctrl, end, i / (float)ChronoSavior.Characters.GunnerSpecConfig.BeamCurveSamples);
                    path.Add(q);
                    if (Vector2.Distance(q, tc) <= halfW) break;
                }
                beam.Show(path, Tier, true, ChronoSavior.Characters.GunnerSpecConfig.BeamWidth(Tier));
                bool released = false;

                // 처치 순간 — 처치된 적에서 편지 받을 적들로 연쇄 광선이 동시에
                var hops = new ChronoSavior.Characters.GunnerChainBeamVfx[letterTargets.Length];
                for (int k = 0; k < letterTargets.Length; k++)
                {
                    int j = letterTargets[k];
                    Vector2 from = Edge(tc, Center(spots[j]));
                    Vector2 to = Edge(Center(spots[j]), tc);
                    hops[k] = ChronoSavior.Characters.GunnerChainBeamVfx.Play(kit.BeamMaterial, from, to, KillAt, k % 2 == 0 ? 1f : -1f, Center(spots[j]), hitSpark: false); // 배달 광선은 번쩍임 없이 — 표식이 붙는다
                    made.Add(hops[k].gameObject);
                }
                float arrive = hops[0].ArriveTime;
                var marks = new ChronoSavior.Combat.VfxPlayer[letterTargets.Length];
                var booms = new ChronoSavior.Combat.VfxPlayer[letterTargets.Length];
                var hitFx = new ChronoSavior.Combat.VfxPlayer[letterTargets.Length];
                // 하늘 레이저 — 피해 순간에 적 머리에 닿게, 그만큼 먼저 화면 위에서 뻗기 시작한다
                var sky = new ChronoSavior.Characters.GunnerChainBeamVfx[letterTargets.Length];
                if (v.skyBeam)
                    for (int k = 0; k < letterTargets.Length; k++)
                    {
                        int j = letterTargets[k];
                        Vector2 headTop = Above(spots[j], 0f);
                        var probe = ChronoSavior.Characters.GunnerChainBeamVfx.Play(kit.BeamMaterial, headTop + Vector2.up * 3.4f, headTop, 0f, 1f, Center(spots[j]), v.skySpark);
                        float lead = probe.ArriveTime;
                        Object.DestroyImmediate(probe.gameObject);
                        sky[k] = ChronoSavior.Characters.GunnerChainBeamVfx.Play(kit.BeamMaterial, headTop + Vector2.up * 3.4f, headTop, arrive + Fuse + Stagger * k - lead, 1f, Center(spots[j]), v.skySpark);
                        made.Add(sky[k].gameObject);
                    }
                float total = arrive + Fuse + Stagger * (letterTargets.Length - 1) + 0.8f;
                int frame = 0;
                for (float t = 0f; t <= total; t += Dt, frame++)
                {
                    // 레이저: 처치 순간까지 쏘다가 손을 뗀다(사그라듦)
                    if (!released && t >= KillAt) { beam.Release(); released = true; }
                    if (beam.gameObject.activeSelf)
                    {
                        tField.SetValue(beam, released ? t - KillAt : t);
                        impactTField.SetValue(beam, t);
                        applyM.Invoke(beam, null);
                    }
                    // 레이저 맞는 동안 깜빡임(틱마다), 처치 순간 사라진다
                    bodies[0].SetActive(t < KillAt);
                    flashes[0].color = new Color(1f, 1f, 1f, (t % 0.13f) < 0.05f ? 0.75f : 0f);
                    foreach (var h in hops) h.Evaluate(t);
                    foreach (var h in sky) if (h != null) h.Evaluate(t);

                    for (int k = 0; k < letterTargets.Length; k++)
                    {
                        int j = letterTargets[k];
                        float since = t - arrive; // 광선이 닿은 뒤
                        if (since >= 0f && since < Fuse)
                        {
                            if (marks[k] == null) { marks[k] = ChronoSavior.Combat.VfxPlayer.Spawn(v.mark, Above(spots[j], v.markPx), DotScale.Value, v.markFps, 4, loop: true); made.Add(marks[k].gameObject); }
                            float mt = since < Fuse - Rush ? since : (Fuse - Rush) + (since - (Fuse - Rush)) * 2.5f; // 마지막 0.3초는 2.5배 빠르게 — 곧 터진다
                            marks[k].gameObject.SetActive(true);
                            marks[k].SetTimeForCapture(mt);
                        }
                        else if (marks[k] != null) marks[k].gameObject.SetActive(false);

                        float hitAt = arrive + Fuse + Stagger * k;        // 피해 순간
                        float bt = t - hitAt + v.preRoll / v.boomFps;     // 그림 시간(그림이 닿는 장이 피해 순간에 오게)
                        if (v.boom != null && bt >= 0f && booms[k] == null)
                        {
                            Vector2 at = v.atFeet ? Feet(spots[j]) + new Vector2(0f, DotScale.Units(v.bottomPx)) : Center(spots[j]);
                            booms[k] = ChronoSavior.Combat.VfxPlayer.Spawn(v.boom, at, DotScale.Value, v.boomFps, 4);
                            if (booms[k] != null) made.Add(booms[k].gameObject);
                            if (booms[k] != null && v.frames != null)
                            {
                                var all = ChronoSavior.Combat.VfxFrames.Get(v.boom);
                                var sub = new Sprite[v.frames.Length];
                                for (int q = 0; q < sub.Length; q++) sub[q] = all[v.frames[q]];
                                booms[k].Play(sub, v.boomFps, false, v.fade, Color.white);
                            }
                        }
                        if (booms[k] != null) booms[k].SetTimeForCapture(Mathf.Max(0f, bt) + v.skip / v.boomFps);
                        float sinceHit = t - hitAt;
                        if (v.hitKey != null && sinceHit >= 0f)
                        {
                            if (hitFx[k] == null) { hitFx[k] = ChronoSavior.Combat.VfxPlayer.Spawn(v.hitKey, Center(spots[j]), DotScale.Value, 30f, 5); if (hitFx[k] != null) made.Add(hitFx[k].gameObject); }
                            if (hitFx[k] != null) hitFx[k].SetTimeForCapture(sinceHit);
                        }
                        flashes[j].color = new Color(1f, 1f, 1f, sinceHit >= 0f ? Mathf.Max(0f, 1f - sinceHit * 6f) * 0.75f : 0f);
                    }
                    Render(pc, cam, dir, $"f_{frame:00}", 1920, 1080, 3f);
                }
                foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            }
            Debug.Log("[CaptureUiPreview] 편지 후보 저장: " + outDir);
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 경험치 구슬 그림 후보(2026-11-23 사용자 "그냥 보라색 원인데 어떻게 고치면 좋을지 — gif로 보여줘"). 게임 코드는 안 바꾸고 여기서만 재현한다.
        /// 떨어져 둥실(원본 sin(t·3)·0.08) → 플레이어가 다가와 1.5 안 → 6.4/s로 끌려옴 → 0.44 안에서 획득. 후보: A 지금(보라 원) / B 에너지 구슬(작은) / C 에너지 구슬(큰).
        /// B·C는 끌려올 때 혜성 모양(날아오는 쪽으로 꼬리), 먹을 때 플레이어 자리 반짝임. 도트 배율 2 하나.
        /// </summary>
        public static void RunExpOrbCandidates()
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
            var orbPrefab = AssetDatabase.LoadAssetAtPath<ChronoSavior.Systems.ExpOrb>("Assets/Prefabs/Combat/ExpOrb.prefab");
            Vector3 playerStart = player.transform.position;

            var variants = new (string name, string idle, string comet, string pickup)[]
            {
                ("A_지금_보라원", null, null, null),
                ("B_에너지구슬_작은", "orb/pellet", "orb/comet", "orb/pickup"),
                ("C_에너지구슬_큰", "orb/pellet_large", "orb/comet", "orb/pickup"),
            };
            const float Dt = 1f / 30f, WalkFrom = 0.5f, WalkTo = 0.9f, WalkDist = 0.6f;
            foreach (var v in variants)
            {
                string dir = Path.Combine(outDir, v.name);
                Directory.CreateDirectory(dir);
                player.transform.position = playerStart;
                var made = new System.Collections.Generic.List<GameObject>();
                float baseY = feet + ChronoSavior.Systems.ExpOrb.DropHeight + 0.1f;
                Vector2 orbPos = new Vector2(playerStart.x + 2.0f, baseY);

                // 구슬 그림
                GameObject orbGo;
                SpriteRenderer orbSr = null;
                Sprite[] idleF = v.idle != null ? ChronoSavior.Combat.VfxFrames.Get(v.idle) : null, cometF = v.comet != null ? ChronoSavior.Combat.VfxFrames.Get(v.comet) : null,
                         pickF = v.pickup != null ? ChronoSavior.Combat.VfxFrames.Get(v.pickup) : null;
                if (v.idle == null)
                {
                    var orb = ChronoSavior.Systems.ExpOrb.Spawn(orbPrefab, new Vector3(orbPos.x, feet, 0f), null);
                    orbGo = orb.gameObject;
                }
                else
                {
                    orbGo = new GameObject("PreviewOrb", typeof(SpriteRenderer));
                    orbSr = orbGo.GetComponent<SpriteRenderer>();
                    orbSr.sortingOrder = 4;
                    orbGo.transform.localScale = Vector3.one * DotScale.Value;
                }
                made.Add(orbGo);
                SpriteRenderer pickSr = null;
                if (pickF != null && pickF.Length > 0)
                {
                    var pg = new GameObject("PreviewPickup", typeof(SpriteRenderer));
                    pickSr = pg.GetComponent<SpriteRenderer>();
                    pickSr.sortingOrder = 5;
                    pg.transform.localScale = Vector3.one * DotScale.Value;
                    pickSr.enabled = false;
                    made.Add(pg);
                }

                bool pulled = false, taken = false;
                float takenAt = 0f, t = 0f;
                for (int frame = 0; frame < 66; frame++, t += Dt)
                {
                    // 플레이어가 구슬 쪽으로 조금 걸어간다
                    float walk = Mathf.Clamp01((t - WalkFrom) / (WalkTo - WalkFrom));
                    player.transform.position = playerStart + new Vector3(walk * WalkDist, 0f, 0f);
                    Vector2 target = (Vector2)player.transform.position + Vector2.up * ChronoSavior.Systems.ExpOrb.PlayerCenterOffset;
                    Vector2 vel = Vector2.zero;
                    if (!taken)
                    {
                        float d = Vector2.Distance(target, orbPos);
                        if (d < ChronoSavior.Systems.ExpOrb.CollectRange) { taken = true; takenAt = t; }
                        else if (pulled || d < ChronoSavior.Systems.ExpOrb.MagnetRange)
                        {
                            pulled = true;
                            vel = (target - orbPos).normalized * ChronoSavior.Systems.ExpOrb.MagnetSpeed;
                            orbPos += vel * Dt;
                        }
                    }
                    Vector2 shown = pulled ? orbPos : new Vector2(orbPos.x, baseY + Mathf.Sin(t * 3f) * 0.08f);
                    orbGo.SetActive(!taken);
                    orbGo.transform.position = shown;
                    if (orbSr != null && !taken)
                    {
                        if (pulled && cometF != null && cometF.Length > 0)
                        {
                            orbSr.sprite = cometF[Mathf.FloorToInt(t * 15f) % cometF.Length];
                            float ang = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg;
                            orbGo.transform.rotation = Quaternion.Euler(0f, 0f, ang); // 그림 머리가 오른쪽 — 날아오는 쪽으로
                        }
                        else
                        {
                            orbSr.sprite = idleF[Mathf.FloorToInt(t * 15f) % idleF.Length];
                            orbGo.transform.rotation = Quaternion.identity;
                        }
                    }
                    if (pickSr != null)
                    {
                        int pi = taken ? Mathf.FloorToInt((t - takenAt) * 24f) : -1;
                        pickSr.enabled = taken && pi < pickF.Length;
                        if (pickSr.enabled) { pickSr.sprite = pickF[pi]; pickSr.transform.position = target; }
                    }
                    Render(pc, cam, dir, $"f_{frame:00}", 1920, 1080, 3f);
                }
                foreach (var go in made) if (go != null) Object.DestroyImmediate(go);
            }
            player.transform.position = playerStart;
            Debug.Log("[CaptureUiPreview] 경험치 구슬 후보 저장: " + outDir);
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
            visuals.PlaceBackground(camX, cam.transform.position.y); // 로비처럼 배경을 안 쓰는 무대는 건너뛴다
        }

        static void FitPlayerSprite(GameObject player)
        {
            if (player == null) return;
            var sr = player.GetComponentInChildren<SpriteRenderer>();
            var rig = player.GetComponent<ChronoSavior.Characters.PlayerRig>();
            var art = rig != null ? rig.Art : null;
            // 게임과 같게(2026-11-17 플립북): 애니메이션이 있으면 서기 첫 장, 높이는 CharacterArtSet.HeightOf
            var set = art != null ? art.AnimSetOf(CharacterId.Mage) : null;
            var sprite = set != null && set.idle != null && set.idle.Count > 0 ? set.idle.frames[0] : art != null ? art.SpriteOf(CharacterId.Mage) : null;
            if (sr != null && sprite != null)
            {
                sr.sprite = sprite;
                sr.color = art.TintOf(CharacterId.Mage);
            }
            if (sr == null || sr.sprite == null) return;
            float s = (art != null ? art.HeightOf(CharacterId.Mage) : EntitySizeConfig.PlayerHeight) / sr.sprite.bounds.size.y;
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
