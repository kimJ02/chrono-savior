using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Editor
{

/// <summary>
/// Part A(필드+카메라+플레이어) 씬을 코드로 조립한다. GUI 클릭 없이
/// `Unity.exe -batchmode -quit -executeMethod BuildPartAScene.Build`로 재현 가능하게 해서,
/// 다른 세션도 이 스크립트만 다시 실행하면 같은 결과를 얻을 수 있다(수동 클릭 산출물이 아님).
///
/// 이번 개정: 손으로 계산하던 중력/바닥판정을 실제 Physics2D(Rigidbody2D+Collider2D 충돌 해석)로
/// 바꾸고, 원본 NORMAL_PLATFORMS 배치를 그대로 옮긴 실물 발판을 추가했다(원본 3~4층 수직형 맵).
/// 필드 폭(26유닛)이 발판을 다 넣기엔 한 화면에 담기엔 넓어서 카메라를 X-스크롤로 바꿨다.
/// </summary>
public static class BuildPartAScene
{
    const string SpritePath = "Assets/Sprites/Circle.png";
    const string GroundLayer = "Ground";
    // 발판 좌표/두께는 FieldLayout.cs로 옮겨서 EnemySpawner의 스폰 포인트 계산과 공유한다
    // (두 군데 따로 들고 있으면 나중에 발판 배치가 또 바뀔 때 하나만 고치는 실수가 남).

    [MenuItem("Tools/ChronoSavior/Build Part A Scene")]
    public static void Build()
    {
        EnsureCircleSprite();
        BuildPrimitiveSprites.Build(); // 네모·세모·동그라미 (없으면 아래 주입이 null이 된다)
        EnsureGroundLayer();
        EnsureMonsterPrefabPhysics();
        Physics2D.gravity = new Vector2(0f, -26f); // 원본 2600px/s² → 26 (100px=1유닛)

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildGround();
        BuildPlatforms();
        var player = BuildPlayer();
        BuildCamera(player.transform);
        BuildEnemySpawner();
        // ⚠️ 반드시 `NewScene()` **뒤에** 만들 것 — 앞에서 만들면 씬이 교체되면서 그대로 버려진다
        // (실제로 그렇게 넣었다가 씬에 RunController가 없는 채로 빌드됐다).
        BuildRunController();

        Directory.CreateDirectory("Assets/Scenes");
        bool ok = EditorSceneManager.SaveScene(scene, "Assets/Scenes/CombatCore.unity");
        Debug.Log(ok ? "[BuildPartAScene] CombatCore.unity 저장 완료" : "[BuildPartAScene] 씬 저장 실패");

        RegisterAsDefaultScene();
    }

    /// <summary>
    /// 배치모드로 씬을 만들기만 하면 "다음에 에디터를 열었을 때 이 씬이 뜨는" 상태가 저절로 안 남는다
    /// (LastSceneManagerSetup.txt가 -quit 배치 실행에서는 갱신 안 됨 — 직접 확인함).
    /// 그래서 두 가지를 명시적으로 해둔다:
    /// 1) Build Settings의 씬 목록에 등록 — 어떤 씬이 "이 프로젝트의 씬"인지 명확해짐
    /// 2) playModeStartScene 지정 — 에디터에 어떤 씬이 열려있든 Play를 누르면 무조건 CombatCore가 실행된다.
    /// </summary>
    static void RegisterAsDefaultScene()
    {
        var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/CombatCore.unity");
        EditorSceneManager.playModeStartScene = sceneAsset;

        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene("Assets/Scenes/CombatCore.unity", true),
        };
        Debug.Log("[BuildPartAScene] playModeStartScene + Build Settings를 CombatCore.unity로 등록");
    }

    /// <summary>
    /// 발판/바닥 전용 물리 레이어. 접지 판정(OverlapCircle)이 플레이어·몬스터·투사체 콜라이더를
    /// 걸러내고 진짜 "땅"만 보게 하려고 분리했다. TagManager.asset의 layers 배열에 직접 쓴다
    /// (Project Settings 창을 열지 않고도 배치 스크립트에서 재현 가능하게).
    /// </summary>
    static void EnsureGroundLayer()
    {
        if (LayerMask.NameToLayer(GroundLayer) != -1) return;

        var tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
        if (tagManagerAssets.Length == 0)
        {
            Debug.LogError("[BuildPartAScene] TagManager.asset을 못 찾음 — Ground 레이어 등록 실패");
            return;
        }
        var tagManager = new SerializedObject(tagManagerAssets[0]);
        var layers = tagManager.FindProperty("layers");
        // 0~7은 내장 예약 레이어. 8번(첫 사용자 정의 슬롯)에 등록한다.
        layers.GetArrayElementAtIndex(8).stringValue = GroundLayer;
        tagManager.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[BuildPartAScene] Ground 레이어(8번) 등록");
    }

    static void BuildCamera(Transform playerTransform)
    {
        var camGO = new GameObject("Main Camera");
        camGO.tag = "MainCamera";
        var cam = camGO.AddComponent<Camera>();
        cam.orthographic = true;
        // 개정(2026-08-26): "맵이 원본보다 좁아 보인다"는 사용자 지적으로 재계산.
        // 원본 캔버스는 1280×720px 고정이고 mapW=2600px — 화면엔 항상 맵의 1280/2600≈49%만 보이고
        // 나머지는 가로 스크롤로 드러난다. 이전엔 이 비율과 무관하게 orthographicSize=5.5(발판이
        // 다 보이는 정도)로 임의로 잡아서 화면에 맵의 75%(19.6/26유닛)가 보였다 — 원본보다 훨씬
        // 넓게 보여서 "안 넓어 보인다"는 체감이 났다. 100px=1유닛 규칙을 카메라에도 그대로 적용:
        // 세로 절반 크기 = 720px/2 ÷100 = 3.6유닛. 가로는 orthographicSize×aspect로 자동 결정되는데
        // 원본 캔버스 비율(1280:720=16:9)로 맞추면 가로도 정확히 1280px÷100=12.8유닛이 되고,
        // 26/12.8 ≈ 2.03 ≈ 원본의 2600/1280 ≈ 2.03과 일치한다.
        // 세로 하단은 원본 그대로: 원본 캔버스는 groundY(620)가 화면 하단에서 100px(=1유닛) 위라
        // 바닥 아래 여백 1유닛을 그대로 가져온다(viewBottom=-1.0). 다만 세로 상단은 원본 값(6.2,
        // 발판이 y=185=4.35유닛까지였을 때 기준)을 그대로 쓰면 발판 층간 간격을 넓힌 우리 필드에선
        // 최상단 발판(5.40유닛) 위에 선 캐릭터 머리가 화면 위로 살짝 잘린다(발판 간격을 넓힌 건
        // 사용자가 명시적으로 요청한 편차라 되돌리지 않음 — docs/sprints/01-combat-core.md 1번 참고). 그래서 상단만
        // 우리 발판 높이 기준으로 다시 계산: 최상단 발판 위 캐릭터 전체(중심+반지름) + 여유 0.2.
        cam.orthographicSize = 3.84f;
        cam.transform.position = new Vector3(playerTransform.position.x, 2.84f, -10f);
        // ⚠️ 예전엔 `Color.white`였다 — 초기 스캐폴딩의 자리표시자가 그대로 남아 있었다.
        // 원본은 캔버스에 밤 하늘을 먼저 깔아서(`drawBackground()` · `regionSkyTint`) 흰 배경이
        // 보일 일이 없는데, 우리는 그 렌더링을 이식하지 않은 채 흰색으로 지우고 있었다.
        // 그 결과 로비 판(알파 0.92)을 통과한 **8%의 흰색**이 UI 전체를 뿌옇게 만들고 발판선이
        // 판을 가로질러 보였다. 지역별 하늘색은 `SkyBackground`가 계속 맞춘다.
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = RegionConfig.SkyColor(1);
        camGO.AddComponent<SkyBackground>();
        camGO.AddComponent<AudioListener>();

        var follow = camGO.AddComponent<CameraFollow2D>();
        follow.target = playerTransform;
    }

    static void BuildGround()
    {
        // 원본처럼 mapW 전체 폭의 단일 고정 바닥. 실제 충돌은 BoxCollider2D(Ground 레이어)가 담당하고,
        // LineRenderer는 눈에 보이는 표시일 뿐이다.
        var go = new GameObject("Ground");
        go.layer = LayerMask.NameToLayer(GroundLayer);
        float y = FieldBounds.GroundY;
        float width = FieldBounds.MaxX - FieldBounds.MinX;
        float centerX = (FieldBounds.MinX + FieldBounds.MaxX) * 0.5f;

        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(width, FieldLayout.PlatformThickness * 2f);
        go.transform.position = new Vector3(centerX, y - FieldLayout.PlatformThickness, 0f);

        var lr = go.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, new Vector3(FieldBounds.MinX, y, 0));
        lr.SetPosition(1, new Vector3(FieldBounds.MaxX, y, 0));
        lr.widthMultiplier = 0.08f;
        lr.useWorldSpace = true;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = new Color(0.25f, 0.25f, 0.28f, 1f);
        lr.sortingOrder = -1;
    }

    /// <summary>
    /// 원본 NORMAL_PLATFORMS 15개를 실물 콜라이더로 배치한다. 원본처럼 "아래/옆에서는 그냥
    /// 통과하고 위에서 떨어질 때만 착지되는" 원웨이 발판 — Unity 내장 PlatformEffector2D(
    /// useOneWay=true) + Collider2D.usedByEffector로 구현했다(막힌 콜라이더였던 이전 버전은
    /// 원본과 달리 점프 중 발판 밑면에 머리가 막히는 버그가 있었음 — 사용자 피드백으로 수정).
    /// 아래키 관통 낙하는 `CharacterMover2D.UpdateDropThrough`가 콜라이더 쌍 무시로 처리한다.
    ///
    /// **두 무대(일반/보스)를 다 만들어 두고 한쪽만 켠다** — 런타임에 발판을 만들고 없애면
    /// 콜라이더가 프레임 중간에 사라져 그 위에 서 있던 대상이 튀거나 빠진다.
    /// </summary>
    static void BuildPlatforms()
    {
        BuildPlatformSet("Platforms_Normal", FieldLayout.NormalPlatforms, boss: false);
        BuildPlatformSet("Platforms_Boss", FieldLayout.BossPlatforms, boss: true);
        BuildBossPortal();
    }

    /// <summary>
    /// 보스 필드로 통하는 포탈(원본에 없음 — 사용자 지시 2026-09-16).
    ///
    /// **발판 묶음의 자식으로 두지 않았다.** `PlatformSet.Activate`가 묶음 전체를 켜고 끄는데,
    /// 포탈은 자기 상태(잠김/해금/보스필드 여부)를 매 프레임 판단해야 하므로 GameObject가 꺼지면
    /// 그 판단 자체가 멈춘다. 대신 <see cref="BossPortal.ShouldBeVisible"/>이 렌더러만 끈다.
    /// </summary>
    static void BuildBossPortal()
    {
        var go = new GameObject("BossPortal");
        go.transform.position = new Vector3(
            FieldLayout.BossPortalX,
            FieldBounds.GroundY + FieldLayout.BossPortalHeight / 2f,
            0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        sr.sortingOrder = -1; // 몹·플레이어 뒤에 — 문은 배경 구조물이다

        // 원형 스프라이트(128px = 1.28유닛)를 문 비율로 늘린다.
        float unit = 1.28f;
        go.transform.localScale = new Vector3(
            FieldLayout.BossPortalWidth / unit, FieldLayout.BossPortalHeight / unit, 1f);

        go.AddComponent<BossPortal>();
    }

    static void BuildPlatformSet(string parentName, float[,] table, bool boss)
    {
        int groundLayer = LayerMask.NameToLayer(GroundLayer);
        var parentGo = new GameObject(parentName);
        parentGo.AddComponent<PlatformSet>().bossArena = boss;
        var parent = parentGo.transform;

        for (int i = 0; i < table.GetLength(0); i++)
        {
            float cx = table[i, 0];
            float cy = table[i, 1];
            float w = table[i, 2];

            var go = new GameObject($"Platform_{i}");
            go.transform.SetParent(parent);
            go.layer = groundLayer;
            go.transform.position = new Vector3(cx, cy, 0f);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(w, FieldLayout.PlatformThickness);
            col.usedByEffector = true;

            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;

            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, new Vector3(-w / 2f, 0f, 0f));
            lr.SetPosition(1, new Vector3(w / 2f, 0f, 0f));
            lr.useWorldSpace = false;
            lr.widthMultiplier = 0.08f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = new Color(0.4f, 0.32f, 0.22f, 1f); // 목조 발판 느낌의 갈색
            lr.sortingOrder = -1;
        }

        // 시작은 일반 무대. 보스 무대는 보스전에 입장할 때 켜진다(RunController).
        parentGo.SetActive(!boss);
    }

    static GameObject BuildPlayer()
    {
        var go = new GameObject("Player");
        go.tag = "Player";
        // "Player" 물리 레이어(6번) — Physics2D Layer Collision Matrix에서 Enemy(7번)와 충돌이
        // 꺼져 있다(PROGRESS.md 2026-08-29 로그: 원본엔 없는 물리 밀어내기 버그 수정).
        // 예전엔 팀원이 에디터에서 직접 레이어를 옮겨서 고쳤는데, 그건 씬 파일에만 남고 이 스크립트가
        // 씬을 다시 만들 때마다 Default(0)로 되돌아갔다 — 실제로 이번 세션에서 배치 재생성을 여러 번
        // 하면서 이 버그가 재발했다. 코드로 고정해서 다시는 안 사라지게 한다.
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) go.layer = playerLayer;
        // 원본 스폰 좌표 p.x=220 그대로(100px=1유닛 → 2.2)
        go.transform.position = new Vector3(2.2f, FieldBounds.GroundY + EntitySizeConfig.PlayerRadius, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        var circleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        sr.sprite = circleSprite;
        sr.color = new Color(0.35f, 0.55f, 1f); // 아군 = 파랑
        go.transform.localScale = new Vector3(0.8f, 0.8f, 1f);

        var col = go.AddComponent<CircleCollider2D>();
        col.radius = EntitySizeConfig.PlayerRadius; // 그림을 꽂으면 PlayerRig가 다시 맞춘다

        var rb = go.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;

        go.AddComponent<CharacterMover2D>();
        go.AddComponent<PlayerHealth>(); // 스프린트 2 — 체력 100, 무적 0.9초(원본 CONFIG.player)
        // --- 캐릭터 키트 (PlayerRig가 선택된 하나만 켠다) ---
        var mage = go.AddComponent<MageAttack>();
        mage.boltSprite = circleSprite; // 런타임 AssetDatabase 호출(빌드에서 못 씀) 없이 미리 꽂아줌

        var gunner = go.AddComponent<GunnerAttack>();
        gunner.bulletSprite = circleSprite;

        // 섬영 키트(팀원 작업, 2026-09-28 부착) — 회전베기는 즉시 판정이라 투사체 스프라이트가 필요 없다.
        // 드루이드는 기획에서 제외되어 키트 자체가 없다(2026-09-28 사용자 확정) — 더 안 붙는다.
        go.AddComponent<BladeCombat>();

        // 캐릭터 키트 전환기. **키트를 전부 붙인 뒤 마지막에** 추가해야 Awake에서 전부 찾는다
        // (GetComponents는 이미 붙어 있는 것만 본다).
        var rig = go.AddComponent<PlayerRig>();
        // 캐릭터 그림(프로토타입에서 구워낸 임시 스프라이트) — CharacterId 순서(마법사·메카닉·섬영·드루이드).
        // 드루이드는 키트가 없어 선택되지 않지만(기획 제외), 그림 배열 자체는 CharacterId 개수(4)에
        // 맞춰 그대로 둔다 — PlayerRig.ApplySprite가 인덱스로 접근하므로 배열을 줄이면 안 된다.
        // ⚠️ 프로토타입 캐릭터 그림을 **쓰지 않는다** — 사용자 지시(2026-09-16)
        // "일단 스킨 씌우지 말고 네모 세모 동그라미로만". 넷 다 네모이고 색으로 구분한다.
        // 되돌리려면 `BuildPrototypeSprites.Load("char_mage")` 식으로 바꾸고 색을 흰색으로 두면 된다.
        var square = BuildPrimitiveSprites.Load("Square");
        rig.characterSprites = new[] { square, square, square, square };
        rig.characterColors = new[]
        {
            new Color(0.42f, 0.62f, 1.00f), // 마법사 — 파랑
            new Color(1.00f, 0.78f, 0.30f), // 메카닉 — 노랑
            new Color(0.85f, 0.95f, 1.00f), // 섬영  — 흰빛(빠른 느낌)
            new Color(0.50f, 0.88f, 0.55f), // 드루이드 — 초록
        };

        return go;
    }

    const string MonsterPrefabPath = "Assets/Prefabs/Enemy_Base.prefab";

    /// <summary>
    /// Part B(docs/sprints/01-combat-core.md 2번) 웨이브 스포너를 씬에 등록. 프리팹은 Part B가 만든
    /// Assets/Prefabs/Enemy_Base.prefab(구 Monster.prefab)을 그대로 참조한다 — 여기서 새로 만들지 않는다.
    /// </summary>
    /// <summary>
    /// 게임 뼈대(로비↔사냥↔결과). 씬에 하나만 있으면 되고, 시작 시 스스로 로비 상태로 들어간다.
    /// </summary>
    static void BuildRunController()
    {
        var go = new GameObject("RunController");
        // ⚠️ AutoSave를 **RunController보다 먼저** 붙인다 — 같은 오브젝트에선 붙인 순서대로 Awake가
        // 도므로, 세이브를 먼저 읽어 `ProfileService.Current`를 교체한 뒤에 나머지가 그걸 보게 된다.
        go.AddComponent<AutoSave>();
        go.AddComponent<RunController>();

        // 화면(로비/HUD/결과)은 전부 이 오브젝트에 같이 붙인다. 각자 `GameState`를 보고 자기 차례에만
        // 그리므로 켜고 끌 필요가 없다 — 원본도 div 셋을 `hidden` 클래스로 토글할 뿐이다.
        var ui = new GameObject("UI");

        // Figma 이미지를 들고 있는 컴포넌트를 **먼저** 붙인다 — `Awake`에서 `UiTheme`에 넘기고,
        // 같은 오브젝트의 Awake는 붙인 순서로 돌기 때문에 다른 화면보다 앞서 있어야
        // 첫 프레임부터 그림이 보인다.
        BuildUiTextures.Apply(); // Point 필터·무압축 (도트가 흐려지지 않게)
        var tex = ui.AddComponent<UiTextures>();
        tex.windowFrame = BuildUiTextures.Load("window_frame");
        tex.iconFlag = BuildUiTextures.Load("icon_flag");
        tex.iconStar = BuildUiTextures.Load("icon_star");
        tex.iconSkull = BuildUiTextures.Load("icon_skull");
        tex.iconShard = BuildUiTextures.Load("icon_shard");
        tex.menuPlate = BuildUiTextures.Load("menu_plate");
        tex.skillSlotAttack = BuildUiTextures.Load("skill_slot_attack");
        tex.skillSlotSkill = BuildUiTextures.Load("skill_slot_skill");
        tex.skillSlotJump = BuildUiTextures.Load("skill_slot_jump");

        ui.AddComponent<TitleScreen>();
        ui.AddComponent<LobbyScreen>();
        ui.AddComponent<GameHud>();
        ui.AddComponent<ResultScreen>();
        ui.AddComponent<RegressionWindow>();
    }

    static void BuildEnemySpawner()
    {
        var monsterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
        if (monsterPrefab == null)
        {
            Debug.LogWarning($"[BuildPartAScene] {MonsterPrefabPath}를 못 찾아 EnemySpawner를 건너뜀.");
            return;
        }

        var go = new GameObject("EnemySpawner");
        var spawner = go.AddComponent<EnemySpawner>();
        spawner.monsterPrefab = monsterPrefab;
        // 구운 프로토타입 그림을 꽂는다. 없으면(굽기 전) null이 들어가고 코드가 알아서
        // 예전처럼 색칠한 원형으로 떨어진다 — 씬 빌드가 실패하지는 않는다.
        // 임시 도형(사용자 지시 2026-09-16 — 스킨 없이 네모·세모·동그라미로만).
        // 배정 기준은 `Core.PrimitiveShape` 주석 참고.
        spawner.circleSprite = BuildPrimitiveSprites.Load("Circle");
        spawner.squareSprite = BuildPrimitiveSprites.Load("Square");
        spawner.triangleSprite = BuildPrimitiveSprites.Load("Triangle");

        spawner.orbSprite = spawner.circleSprite;     // 주워 먹는 것 = 동그라미
        spawner.shrineSprite = spawner.squareSprite;  // 안 움직이는 구조물 = 네모
        spawner.bossSprite = spawner.triangleSprite;  // 예고 동작을 보고 피해야 하는 위협 = 세모

        // 몹 종류별 수치는 EnemyData(SO)에서 온다. 에셋이 없으면 여기서 만들어 둔다.
        BuildEnemyData.Build();

        // 7종 전부 등록한다. **어떤 종이 실제로 나올지는 여기가 아니라 지역별 해금표가 정한다**
        // (`EnemySpawnTable.Roll`, 원본 rollSpawnType :3933) — 꼬마 슬라임(Splitlet)는 그 표에 없어서
        // 웨이브로는 나오지 않고 슬라임이 죽을 때만 나오지만, 그때 수치를 찾아 쓰려면 여기 등록돼 있어야 한다.
        var types = new List<EnemyData>();
        foreach (EnemyType t in System.Enum.GetValues(typeof(EnemyType)))
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{t}.asset");
            if (data != null) types.Add(data);
            else Debug.LogWarning($"[BuildPartAScene] EnemyData 에셋이 없다: {t}");
        }
        spawner.enemyTypes = types.ToArray();
    }

    /// <summary>
    /// Enemy_Base.prefab(구 Monster.prefab)에 Rigidbody2D가 없었다 — 몹이 발판 높이에 스폰돼도 중력을
    /// 안 받아 허공에 뜬 채로 있거나(또는 발판 콜라이더를 그냥 통과)였다. 실제로 발판 위에 서 있으려면
    /// 플레이어와 동일하게 진짜 물리(Rigidbody2D + 이미 있는 CircleCollider2D)가 필요해서 프리팹에 직접 추가한다.
    ///
    /// 주의: `EnemyMove`에 `[RequireComponent(typeof(Rigidbody2D))]`를 붙여놨더니, 프리팹 파일에
    /// 실제로는 없는데도 로드 시점에 엔진이 메모리상으로만 자동 보충해서
    /// `prefab.GetComponent&lt;Rigidbody2D&gt;() != null`이 거짓으로 참이 되는 걸 직접 확인했다
    /// (그래서 "이미 있으면 건너뛴다"는 가드를 넣었다가 실제 파일엔 한 번도 저장 안 된 채로
    /// 넘어간 적이 있음). 그래서 "있는지 검사 후 건너뛰기"를 하지 않고 항상 로드 → 설정 → 저장한다
    /// — freezeRotation=true를 매번 명시적으로 세팅하는 것도 같은 이유(자동 보충된 기본값은 false).
    /// </summary>
    static void EnsureMonsterPrefabPhysics()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPrefabPath);
        if (prefab == null) return;

        var contents = PrefabUtility.LoadPrefabContents(MonsterPrefabPath);
        var rb = contents.GetComponent<Rigidbody2D>();
        if (rb == null) rb = contents.AddComponent<Rigidbody2D>();
        rb.freezeRotation = true;
        rb.gravityScale = 1f;

        // 스프린트 2 — 체력. 원본 고블린 hp 38(project_test.html:709).
        // 위 Rigidbody2D와 같은 이유로 "있는지 검사 후 건너뛰기"를 하지 않는다(RequireComponent 자동 보충 함정).
        var health = contents.GetComponent<ChronoSavior.Enemies.EnemyHealth>();
        if (health == null) health = contents.AddComponent<ChronoSavior.Enemies.EnemyHealth>();
        health.maxHp = 38f;

        // "Enemy" 물리 레이어(7번) — Player(6번)와 충돌이 꺼져 있어야 겹쳐도 안 밀린다. 지금은 이
        // 프리팹 파일에만 수동으로 박혀 있어서, Player 쪽처럼 코드가 씬을 다시 만들 때 이 프리팹
        // 자체를 처음부터 새로 만드는 경로가 생기면 같은 식으로 사라질 수 있다 — 방어적으로 여기서도 강제.
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) contents.layer = enemyLayer;

        PrefabUtility.SaveAsPrefabAsset(contents, MonsterPrefabPath);
        PrefabUtility.UnloadPrefabContents(contents);
        Debug.Log("[BuildPartAScene] Enemy_Base.prefab Rigidbody2D/EnemyHealth 확인/설정 완료");
    }

    /// <summary>
    /// 절차적으로 채워진 원 스프라이트를 생성한다. 유니티 기본 내장 리소스 중엔
    /// 신뢰할 수 있는 "꽉 찬 원" 스프라이트가 없어서(전부 UI용 라운드사각형) 직접 만든다.
    /// 픽셀당유닛(pixelsPerUnit)을 텍스처 폭과 같게 둬서 스케일 1일 때 지름 1유닛이 되게 한다.
    /// </summary>
    static void EnsureCircleSprite()
    {
        Directory.CreateDirectory("Assets/Sprites");
        if (File.Exists(SpritePath))
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (existing != null) return;
        }

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float r = size / 2f;
        Vector2 center = new Vector2(r, r);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float a = Mathf.Clamp01(r - d);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();

        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);

        var importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size;
        importer.filterMode = FilterMode.Bilinear;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}

}
