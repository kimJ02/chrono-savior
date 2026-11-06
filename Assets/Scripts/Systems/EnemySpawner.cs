using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.World;

namespace ChronoSavior.Systems
{
/// <summary>
/// Part B (feature/monster-combat) - 몬스터 스폰 알고리즘 (docs/sprints/01-combat-core.md 2번).
///
///   매 waveInterval(3.6초)마다 웨이브 발생:
///     maxSpawnPerWave(7)마리까지 스폰 시도
///     단, (현재 생존 수 + 이번에 스폰할 수) >= maxAliveTotal(22) 면 중단
///     각 마리마다 필드 안에서 무작위 위치를 뽑고, "이번 웨이브에서 이미 배치한" 몹과 너무
///     가까우면(minSpacing 미달) 다시 뽑는다 — 최대 maxPlacementRetries(10)회.
///     그래도 실패하면 이번 마리는 스킵. **간격 체크는 이번 웨이브 안에서만** 한다(원본
///     spawnWave()의 `const placed=[]`가 호출마다 새로 시작 — 이전 웨이브에서 이미 살아있는
///     몹과는 겹쳐도 됨. 전체 생존 몹과 비교하면 필드가 찰수록 원본보다 스폰 실패가 잦아진다).
///
/// 스폰 지점은 원본 buildSpawnPoints()를 그대로 반영한다 — 필드 전체에서 균등 랜덤이 아니라
/// "발판 15개의 중심 X + 바닥 위 380px 간격 지점(6곳)" 총 21개의 정해진 스폰 포인트 중 하나를
/// 골라 그 지점 폭(w) 안에서 살짝 흔든다(spawnWave, rand(-pt.w/2+26, pt.w/2-26)).
///
/// **발판 스폰 포인트는 실제로 그 발판 높이에 스폰한다**(Y를 항상 GroundY로 고정했던 이전
/// 버전은 "발판 위에도 나온다"는 말과 실제 동작이 달랐음 — 사용자가 직접 확인하고 지적).
/// 몹은 이제 Rigidbody2D로 실제 중력을 받으므로(EnemyMove 참고) 발판 위에 스폰하면 물리로
/// 그 위에 서 있는다. FieldLayout이 발판/바닥그리드 좌표의 단일 출처다.
///
/// **역할 나눔(2026-11-06 정리 순서 5)**: 이 스포너는 **언제·어디·무엇을 세울지**(웨이브·보스전 부하·성소·보스 등장·몹이 낳는 몹)와
/// 세운 몹의 처치 연결·몹↔데이터 기억·경험치 구슬만 맡는다. 데이터로 몹 한 마리를 완성하는 일(지역 배율·엘리트·크기)은 <see cref="EnemySetup"/>,
/// 처치 보상(경험치·골드·통계·토벌)은 <see cref="EnemyRewards"/>. **스케일링/보상 수치는 전부 `Core/DifficultyScalingConfig`에
/// 모아뒀다** — 이 파일엔 매직넘버를 두지 않는다(2026-09-08 "0. 착수 전 필독" 추가 — 원본
/// 밸런스가 완전히 정리된 게 아니라 나중에 직접 조정할 가능성이 높아서, 그때 로직 코드를
/// 뒤지지 않고 숫자만 한 파일에서 바꾸게 하려는 목적).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("몬스터 프리팹")]
    [Tooltip("기본 몹 프리팹(Enemy_Base) — 스폰 높이를 잡는 몸 반지름의 기준이고, 데이터에 프리팹이 없을 때 쓴다. " +
             "종류별 프리팹은 몹 데이터(EnemyData.prefab)가, 보스·성소 프리팹은 몬스터 목록(MonsterCatalog)이 가리킨다.")]
    public GameObject monsterPrefab;

    [Tooltip("같은 씬(20_Combat)의 플레이어 — 만든 몹이 쫓을 대상(EnemyMove.Target)과 경험치 구슬이 끌려갈 곳으로 넣어 준다.")]
    [SerializeField] Transform player;

    /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다.</summary>
    public Transform Player { get => player; set => player = value; }

    [Header("웨이브 설정 (docs/sprints/01-combat-core.md 2번)")]
    public float waveInterval = 3.6f;
    public int maxSpawnPerWave = 7;
    public int maxAliveTotal = 22;

    /// <summary>런 시작 후 첫 웨이브까지. 원본 `run.waveTimer = 0.6`(project_test.html:4314).</summary>
    public const float FirstWaveDelay = 0.6f;

    [Header("보스전 미니언 (원본 CONFIG.run, project_test.html:695)")]
    [Tooltip("보스전에서 잡몹이 나오는 간격. 원본 minionInterval 7.")]
    public float minionInterval = 7f;
    [Tooltip("보스전 한 번에 나오는 잡몹 수. 원본 minionWave 2.")]
    public int minionWave = 2;
    [Tooltip("보스전에서 동시에 살아 있을 수 있는 잡몹 수. 원본 maxMinions 4.")]
    public int maxMinions = 4;

    [Header("배치 간격")]
    [Tooltip("같은 웨이브 안에서 몹 사이 최소 간격(월드 유닛). 원본 spawnWave()의 52px(X축 전용) ÷100.")]
    public float minSpacing = 0.52f;
    public int maxPlacementRetries = 10;

    [Header("엘리트 (원본 CONFIG.elite, project_test.html:696)")]
    [Tooltip("엘리트 승격 확률. **음수로 두면 DifficultyScalingConfig.EliteChance(원본 0.08)를 쓴다** — " +
             "평소엔 그대로 두고, 확률을 0이나 1로 고정해야 하는 테스트와 밸런스 실험에서만 덮어쓴다. " +
             "기본값을 여기 숫자로 박아두지 않는 건 씬에 저장된 값이 나중에 설정 파일과 어긋나는 걸 막으려는 것.")]
    public float eliteChanceOverride = -1f;

    float EliteChance => eliteChanceOverride >= 0f ? eliteChanceOverride : DifficultyScalingConfig.EliteChance;

    [Header("성소 (원본 CONFIG.shrine, project_test.html:698)")]
    [Tooltip("성소를 스폰할지. 끄면 아예 안 나온다(테스트·디버그용).")]
    public bool spawnShrines = true;

    [Header("경험치 구슬")]
    [Tooltip("경험치 구슬 프리팹(Assets/Prefabs/Combat/ExpOrb).")]
    [SerializeField] ExpOrb orbPrefab;

    /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
    public ExpOrb OrbPrefab { get => orbPrefab; set => orbPrefab = value; }


    [Header("임시 도형 (정식 아트 전까지 — 씬 빌더가 꽂아준다)")]
    [Tooltip("동그라미. 근접 추적 몹과 경험치 구슬이 쓴다.")]
    public Sprite circleSprite;
    [Tooltip("네모. 성소가 쓴다(플레이어도 같은 도형이지만 그쪽은 PlayerRig가 꽂는다).")]
    public Sprite squareSprite;
    [Tooltip("세모. 방향성 위협(비행·사격·돌진)과 보스가 쓴다.")]
    public Sprite triangleSprite;

    /// <summary>도형 → 스프라이트. 배정 기준은 `Core.PrimitiveShape` 주석 참고.</summary>
    Sprite SpriteFor(PrimitiveShape shape) => shape switch
    {
        PrimitiveShape.Square => squareSprite,
        PrimitiveShape.Triangle => triangleSprite,
        _ => circleSprite,
    };

    [Header("몬스터 목록 (Assets/Data/Enemies/MonsterCatalog.asset)")]
    [Tooltip("잡몹 종류별 수치·그림과 보스·성소 그림. 스테이지 선택 화면(상주 씬)도 같은 에셋을 본다. 비어 있으면 프리팹에 들어 있는 값을 그대로 쓴다.")]
    [SerializeField] MonsterCatalog catalog;

    /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다.</summary>
    public MonsterCatalog Catalog { get => catalog; set => catalog = value; }

    /// <summary>
    /// 웨이브 상한에 **세는 대상**. 원본 `enemies.filter(e => !e.boss && !e.shrine)`(`:3978`)와 같아야 한다 —
    /// 보스와 성소를 여기 넣으면 그 둘이 상한 자리를 차지해서, 특히 보스전(상한 4)에선
    /// 잡몹이 조용히 한두 마리씩 덜 나온다.
    /// </summary>
    readonly List<Transform> aliveMonsters = new List<Transform>();
    // 스폰된 몹이 어떤 종류였는지 — 처치 보상에서 종류별 exp/gold를 읽어야 해서 들고 있는다.
    readonly Dictionary<GameObject, EnemyData> spawnedData = new Dictionary<GameObject, EnemyData>();
    // 사냥 전엔 첫 웨이브 대기로 둔다 — 0이면 시간이 멈춘(timeScale 0) 화면에서도 `0 <= 0`이라 씬이 뜨자마자 한 무리를 만든다(사냥 시작이 다시 맞춘다).
    float waveTimer = FirstWaveDelay;
    float cachedMonsterRadius = -1f; // Awake 시점엔 monsterPrefab이 아직 할당 전이라(씬 빌드 순서상)
                                      // 필요할 때 지연 계산한다(GetMonsterRadius 참고).

    // 몹이 몹을 낳는 요청(슬라임 → 꼬마 슬라임, 보스 → 부하)을 받는다. 정적 이벤트라 **반드시 OnDisable에서
    // 해제**해야 파괴된 스포너를 계속 참조하지 않는다(CLAUDE.md "낮은 층이 높은 층의 기능을 요청" 항목).

    float shrineTimer = Shrine.FirstAt;
    Transform aliveShrine;

    void OnEnable()
    {
        EnemySpawnRequestBus.Requested += HandleSpawnRequest;
        GameState.Changed += HandleSceneChanged;
        RunEvents.BossFieldEntered += HandleBossFieldEntered;
    }

    void OnDisable()
    {
        EnemySpawnRequestBus.Requested -= HandleSpawnRequest;
        GameState.Changed -= HandleSceneChanged;
        RunEvents.BossFieldEntered -= HandleBossFieldEntered;
    }

    /// <summary>
    /// 새 사냥이 시작되면 웨이브·성소 타이머를 처음으로 되돌린다. 원본 `startRun`이
    /// `run.shrineTimer = CONFIG.shrine.firstAt`로 초기화하는 것(:4315)에 대응한다 —
    /// 안 하면 두 번째 런에서 성소가 입장하자마자 튀어나온다.
    /// </summary>
    void HandleSceneChanged(GameScene scene)
    {
        if (scene != GameScene.Run) return;
        // 원본 `run.waveTimer = 0.6`(project_test.html:4314) — **첫 웨이브는 0.6초 뒤에 온다.**
        // 여기에 waveInterval(3.6)을 넣으면 입장 후 3초를 빈 필드에서 기다리게 된다.
        waveTimer = RunState.Mode == RunMode.Boss ? minionInterval : FirstWaveDelay;
        shrineTimer = Shrine.FirstAt;
        aliveShrine = null;

        // 원본 `if (mode === 'boss') spawnBoss()`(project_test.html:4324).
        // 지금은 로비가 항상 일반 모드로 입장시키므로(보스는 필드의 포탈로 들어간다) 이 분기는
        // 평소엔 안 타지만, `StartRun(region, RunMode.Boss)`로 바로 보스전을 여는 경로(테스트 등)를
        // 위해 남겨 둔다.
        if (RunState.Mode == RunMode.Boss) SpawnBoss();
    }

    /// <summary>
    /// 보스 포탈로 보스 필드에 들어왔다 — **원본에 없는 경로**(사용자 지시 2026-09-16).
    /// 원본은 보스전이 별개의 런이라 `startRun`에서 한 번만 보스를 세우면 됐지만, 이제는
    /// **런 도중에** 무대가 바뀌므로 그 시점에 보스를 세우고 잡몹 타이머를 보스전 간격으로 돌린다.
    ///
    /// `CombatDirector`가 이미 `RunTransient.DestroyAllIn`로 필드를 비운 뒤에 이 이벤트를 쏘므로
    /// 여기서 다시 지우지 않는다 — 두 번 지우면 이 프레임에 세운 보스까지 날아간다.
    /// </summary>
    void HandleBossFieldEntered()
    {
        aliveMonsters.RemoveAll(t => t == null); // 방금 지워진 잡몹들을 목록에서도 떨어낸다
        waveTimer = minionInterval;
        shrineTimer = Shrine.FirstAt; // 보스전엔 성소가 안 나오지만(:4430) 다음 런을 위해 되돌린다
        aliveShrine = null;
        SpawnBoss();
    }

    /// <summary>
    /// 몹이 낳는 몹 — 슬라임의 꼬마 슬라임(원본 `:1842`)와 보스의 부하(`:4278`)가 같이 지나간다.
    /// 웨이브 상한과 무관하게 즉시 스폰한다 — 원본도 죽은 자리에서 바로 낳는다.
    ///
    /// **스폰 보호 시간은 요청자가 정한다**: 꼬마 슬라임은 0.35초, 부하는 기본 2초다. 여기서 하나로
    /// 고정하면 둘 중 하나가 원본과 달라진다(예전엔 부하까지 0.35초를 받고 있었다).
    /// </summary>
    void HandleSpawnRequest(Vector2 position, EnemyType enemyType, float spawnProtectSeconds)
    {
        if (monsterPrefab == null) return;

        var data = FindData(enemyType);
        var monster = SpawnMonsterAt(PrefabOf(data), position);
        // 원본 `noElite: true`(project_test.html:1842) — 꼬마 슬라임은 엘리트로 승격되지 않는다.
        ApplyEnemyData(monster, data, allowElite: false);

        var move = monster.GetComponent<EnemyMove>();
        if (move != null) move.SetSpawnProtection(spawnProtectSeconds);

        aliveMonsters.Add(monster.transform);
    }

    EnemyData FindData(EnemyType enemyType) => catalog != null ? catalog.Find(enemyType) : null;

    /// <summary>
    /// 그 종류의 프리팹 — 종류마다 다른 행동 부품(비행·돌진·사격·분열)이 프리팹에 들어 있다(2026-11-05 생성 책임 2-2, 예전엔 여기서 붙였다).
    /// 데이터가 없거나 프리팹이 비어 있으면 기본 프리팹(기본 보행) — 실제 데이터는 씬 구성 검증이 종류별 프리팹을 확인한다.
    /// </summary>
    GameObject PrefabOf(EnemyData data) => data != null && data.prefab != null ? data.prefab : monsterPrefab;

    /// <summary>
    /// 프리팹 하나를 그 자리에 세우고 **쫓을 플레이어를 넣는다** — 잡몹·꼬마 슬라임·성소·보스가 모두 여기를 지난다(2026-11-05 2b —
    /// 예전엔 몹이 스스로 Player 태그로 찾았다). 위치만 받고 나머지(데이터·보호·이름)는 부르는 쪽이 정한다.
    /// 행동 부품은 프리팹에 이미 들어 있어서 세우는 순간(`Awake`) 몹이 자기 행동을 안다 — 데이터가 필요한 값(체력·속도·탄 그림)은 그 뒤 여기서 넣는다.
    /// </summary>
    GameObject SpawnMonsterAt(GameObject prefab, Vector3 position)
    {
        var go = Instantiate(prefab, position, Quaternion.identity);
        if (go.TryGetComponent(out EnemyMove move)) move.Target = player;
        return go;
    }


    void Update()
    {
        aliveMonsters.RemoveAll(t => t == null);

        waveTimer -= Time.deltaTime;
        if (waveTimer <= 0f)
        {
            // 보스전은 잡몹이 훨씬 드물게, 적게, 상한도 낮게 나온다(원본 `:4444`) —
            // 보스와 싸우는 게 본체라 잡몹이 화면을 채우면 안 된다.
            bool boss = RunState.Mode == RunMode.Boss;
            // 요기 응집 — 필드 최대 몹이 늘어난다(원본 `effMaxEnemies() = baseMax + itemAdd('mob')` :3929).
            int cap = maxAliveTotal + Mathf.RoundToInt(ProfileService.Current.items.Add(ItemStat.Mob));

            if (boss)
            {
                // 원본 보스전은 `run.minionTimer = CONFIG.run.minionInterval` — **스폰 속도 아이템이 안 붙는다**(:4444).
                waveTimer = minionInterval;
                // 원본 `if (enemies.filter(e => !e.boss).length < maxMinions) spawnWave(minionWave)`(:4445).
                // 상한을 웨이브 크기로 넘기는 게 아니라 **문지기**다 — 4마리 미만이면 2마리를 통째로 부른다.
                if (aliveMonsters.Count < maxMinions) SpawnWaveOf(minionWave, cap);
            }
            else
            {
                // 황천의 문 — 스폰 속도가 빨라진다(원본 `effWaveInterval() = waveInterval / (1 + itemAdd('rate'))` :3930).
                waveTimer = waveInterval / (1f + ProfileService.Current.items.Add(ItemStat.Rate));
                SpawnWaveOf(maxSpawnPerWave, cap);
            }
        }

        UpdateShrine(Time.deltaTime);
    }

    /// <summary>
    /// 원본 `run.shrineTimer`(project_test.html:4315 초기화, `spawnShrine` `:3996`).
    /// **동시에 하나만** 존재한다(`if (enemies.some(e => e.shrine)) return`, `:3997`) —
    /// 여러 개가 겹치면 적 버프가 중첩되는 게 아니라 그냥 부수기만 번거로워진다.
    /// </summary>
    void UpdateShrine(float dt)
    {
        if (!spawnShrines || monsterPrefab == null) return;
        // 원본은 성소 타이머 자체가 `if (run.mode === 'normal')` 블록 안에 있다(`:4430`) —
        // **보스전엔 성소가 안 나온다.** 좁은 보스 무대에 구조물이 서면 피할 자리가 사라진다.
        if (RunState.Mode != RunMode.Normal) return;

        // 원본은 성소가 살아 있어도 타이머를 **계속 깎는다**(`:4437`). 살아 있는 동안 멈춰 세우면
        // 방치할수록 다음 성소가 늦어지고, 부순 직후엔 35초를 새로 기다리게 된다 — 둘 다 원본과 다르다.
        shrineTimer -= dt;
        if (shrineTimer > 0f) return;
        shrineTimer = Shrine.Interval;

        // 동시에 하나만 — 원본은 이 방어가 `spawnShrine()` 안에 있고, **타이머를 되돌리지 않는다**(`:3997`).
        if (aliveShrine != null) return;
        SpawnShrine();
    }

    /// <summary>원본 `spawnShrine()`(project_test.html:3996) — **바닥 스폰 지점 중 하나**에 세운다.</summary>
    void SpawnShrine()
    {
        // 원본은 `spawnPoints.filter(pt => pt.y === groundY)` — 발판 위엔 안 세운다.
        int groundCount = FieldLayout.GroundGridX.Length;
        float x = FieldLayout.GroundGridX[Random.Range(0, groundCount)];

        var prefab = catalog != null ? catalog.shrinePrefab : null;
        if (prefab == null) { Debug.LogError("[EnemySpawner] 성소 프리팹(MonsterCatalog.shrinePrefab)이 없다"); return; }

        // 발을 지면에 붙인다 — 구조물이라 물리로 안 떨어지므로 세울 때 정확히 놓아야 한다.
        // 움직이지 않는 구조물 설정(원본 `if (e.shrine) continue;` :4024 — 컴포넌트를 끄지 않고 isStructure 플래그, Kinematic·중력 0)은 프리팹(Enemy_Shrine)에 있다.
        // 플래그인 이유: 원본은 그 한 줄 앞에서 spawnInvuln을 깎기 때문에(`:4023`), Update가 멈추면 아래 스폰 보호가 영영 안 풀린다.
        var go = SpawnMonsterAt(prefab, new Vector3(x, FieldBounds.GroundY + EntitySizeConfig.ShrineHeight / 2f, 0f));
        go.name = "Shrine";
        RunTransient.Mark(go);

        EnemySetup.SetupShrine(go, catalog); // 스폰 보호·레벨·체력·색·크기
        if (go.TryGetComponent(out EnemyHealth health)) health.Died += HandleShrineDestroyed;

        aliveShrine = go.transform;
        // 웨이브 상한엔 **안 넣는다** — 원본 `enemies.filter(e => !e.boss && !e.shrine)`(:3978).
    }

    void HandleShrineDestroyed(EnemyHealth _) => aliveShrine = null;

    /// <summary>
    /// 원본 `spawnBoss()`(project_test.html:4155). 맵 우측 62% 지점의 바닥에 세운다.
    /// 잡몹과 달리 **지역 배율을 따로 쓴다**(`hpBase 800 × 2.15^(r-1)`) — `EnemyData`를 안 거친다.
    /// </summary>
    void SpawnBoss()
    {
        var prefab = catalog != null ? catalog.bossPrefab : null;
        if (prefab == null) { Debug.LogError("[EnemySpawner] 보스 프리팹(MonsterCatalog.bossPrefab)이 없다"); return; }

        int region = RunState.Region;
        float x = Mathf.Lerp(FieldBounds.MinX, FieldBounds.MaxX, 0.62f);
        // Boss 부품(이동·공격 패턴)과 보스 표시(EnemyHealth.isBoss — 레벨 페널티 하한 50%, 원본 `levelFactor` :1652)는 프리팹(Enemy_Boss)에 있다.
        var go = SpawnMonsterAt(prefab, new Vector3(x, FieldBounds.GroundY + EntitySizeConfig.BossHeight / 2f, 0f));
        HitFeel.Shake(8f, 0.5f); // 원본 spawnBoss(:4174)
        go.name = "Boss";
        RunTransient.Mark(go);

        EnemySetup.SetupBoss(go, region, catalog); // 체력·레벨·넉백·공격·속도·색·크기
        if (go.TryGetComponent(out EnemyHealth health)) health.Died += HandleBossDied;

        // 웨이브 상한엔 **안 넣는다** — 원본 `enemies.filter(e => !e.boss && !e.shrine)`(:3978).
        // 넣으면 보스전 상한(4) 한 자리를 보스가 먹어 잡몹이 3마리만 나온다.
    }

    /// <summary>보스가 쓰러졌다 — 보상(<see cref="EnemyRewards.GrantBossKill"/>)을 주고 전투 담당에 알린다.</summary>
    void HandleBossDied(EnemyHealth enemy)
    {
        EnemyRewards.GrantBossKill(RunState.Region);

        // 원본은 격파 1.6초 뒤에 `endRun('bossdead')`을 부른다(:4289). 지연을 주는 주체가
        // `CombatDirector`라 이벤트로 넘긴다 — 예전엔 이 호출이 아예 없어서 **보스를 잡아도 런이
        // 안 끝나고 제한시간까지 빈 무대에 남아 있었다.**
        RunEvents.RaiseBossDefeated();
    }

    /// <summary>기본 웨이브(일반 사냥). 이름을 나눠 둔 건 테스트가 리플렉션으로 이 메서드를 찾기 때문 —
    /// 같은 이름의 오버로드가 있으면 `GetMethod`가 모호하다고 던진다(실제로 겪음).</summary>
    void SpawnWave() => SpawnWaveOf(maxSpawnPerWave, maxAliveTotal);

    void SpawnWaveOf(int count, int aliveCap)
    {
        if (monsterPrefab == null)
        {
            Debug.LogWarning("[EnemySpawner] monsterPrefab이 비어 있어 스폰을 건너뜀.");
            return;
        }

        var placedThisWave = new List<Vector2>(); // 원본 spawnWave()의 `const placed=[]`와 동일 — 이 웨이브 안에서만 겹침 체크

        for (int i = 0; i < count; i++)
        {
            // "현재 살아있는 몹 수 + 이번에 스폰할 수 >= 상한 이면 중단" — 이번에 하나 더 스폰하면
            // 상한을 넘는 시점에 멈춘다.
            if (aliveMonsters.Count + placedThisWave.Count >= aliveCap) break;

            if (TryGetSpawnPosition(placedThisWave, out Vector2 spawnPos))
            {
                var data = PickEnemyData();
                GameObject monster = SpawnMonsterAt(PrefabOf(data), spawnPos);
                ApplyEnemyData(monster, data, allowElite: true);
                aliveMonsters.Add(monster.transform);
                placedThisWave.Add(spawnPos);
            }
            // 10회 재시도 후에도 자리를 못 찾으면 이번 마리는 스킵하고 다음 마리로 넘어간다.
        }
    }

    bool TryGetSpawnPosition(List<Vector2> placedThisWave, out Vector2 result)
    {
        for (int attempt = 0; attempt < maxPlacementRetries; attempt++)
        {
            Vector2 candidate = PickSpawnPoint();
            candidate.x = FieldBounds.ClampX(candidate.x);

            if (IsFarEnoughFromPlaced(candidate, placedThisWave))
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// 원본 spawnWave()의 `spawnPoints[randInt(...)]` + `rand(-pt.w/2+26, pt.w/2-26)`를 그대로 옮김.
    /// 발판 중심(15개)과 바닥 그리드(6개) 중 하나를 균등 랜덤으로 고른 뒤 그 지점 폭 안에서 흔든다.
    /// 발판 포인트는 **그 발판의 실제 착지 Y**(발판 윗면 + 몹 반지름)를 쓴다 — 몹이 물리로
    /// 그 위에 서 있게 된다. 바닥그리드 포인트는 FieldBounds.GroundY.
    /// </summary>
    Vector2 PickSpawnPoint()
    {
        int platformCount = FieldLayout.Platforms.GetLength(0);
        int groundCount = FieldLayout.GroundGridX.Length;
        int idx = Random.Range(0, platformCount + groundCount);

        float centerX, centerY, width;
        float radius = GetMonsterRadius();
        if (idx < platformCount)
        {
            centerX = FieldLayout.Platforms[idx, 0];
            centerY = FieldLayout.PlatformLandingY(idx, radius);
            width = FieldLayout.Platforms[idx, 2];
        }
        else
        {
            centerX = FieldLayout.GroundGridX[idx - platformCount];
            centerY = FieldBounds.GroundY + radius;
            width = FieldLayout.GroundGridPointWidth;
        }

        const float margin = 0.26f; // 원본 26px 여백 ÷100
        float halfSpan = Mathf.Max(0f, width / 2f - margin);
        return new Vector2(centerX + Random.Range(-halfSpan, halfSpan), centerY);
    }

    float GetMonsterRadius()
    {
        if (cachedMonsterRadius < 0f)
        {
            var col = monsterPrefab != null ? monsterPrefab.GetComponent<CircleCollider2D>() : null;
            cachedMonsterRadius = col != null ? col.radius : 0.5f;
        }
        return cachedMonsterRadius;
    }

    bool IsFarEnoughFromPlaced(Vector2 candidate, List<Vector2> placedThisWave)
    {
        // 원본: `Math.abs(q.x-x)<52 && Math.abs(q.y-y)<12` — 둘 다 만족해야(AND) "너무 가깝다".
        // 발판 층마다 Y가 다르므로(이전엔 전부 GroundY라 Y조건이 항상 참이었음) 이제 Y차이가
        // 크면(다른 층) 겹쳐도 통과시킨다 — 원본과 동일.
        const float yThreshold = 0.12f; // 원본 12px ÷100
        foreach (var p in placedThisWave)
        {
            if (Mathf.Abs(p.x - candidate.x) < minSpacing && Mathf.Abs(p.y - candidate.y) < yThreshold)
                return false;
        }
        return true;
    }

    /// <summary>
    /// 이번에 스폰할 몹 종류를 고른다 — 원본 지역별 해금표(<see cref="EnemySpawnTable"/>) 그대로.
    /// 표가 고른 종류의 `EnemyData`가 등록돼 있지 않으면(예: 아직 에셋을 안 꽂았을 때) 그냥
    /// 등록된 것 중 아무거나로 떨어진다 — 스폰 자체가 멈추는 것보단 낫다.
    /// </summary>
    EnemyData PickEnemyData()
    {
        var types = catalog != null ? catalog.types : null;
        if (types == null || types.Length == 0) return null;
        var rolled = FindData(EnemySpawnTable.Roll(RunState.Region));
        return rolled != null ? rolled : types[Random.Range(0, types.Length)];
    }

    /// <summary>
    /// 세운 잡몹을 데이터로 완성하고(<see cref="EnemySetup.Apply"/>) 이 스포너에 연결한다 — 필드 정리 표시, 처치 보상 구독,
    /// 처치 보상에서 종류별 exp/gold를 읽으려고 몹↔데이터 기억.
    /// </summary>
    void ApplyEnemyData(GameObject monster, EnemyData data, bool allowElite)
    {
        // 로비로 나가면 필드가 비워져야 한다(원본 `enemies = []`, project_test.html:4318).
        RunTransient.Mark(monster);
        EnemySetup.Apply(monster, data, allowElite, EliteChance, data != null ? SpriteFor(data.shape) : null);
        if (monster.TryGetComponent(out EnemyHealth health)) health.Died += HandleEnemyDied;
        if (data != null) spawnedData[monster] = data;
    }

    /// <summary>
    /// 일반 몹이 쓰러졌다 — 그 몹의 데이터로 보상을 주고(<see cref="EnemyRewards.GrantKill"/>), 50마리째마다 경험치 구슬을 세운다.
    /// 성소·보스는 전용 핸들러가 있어 여기를 타지 않는다(원본도 각각 따로 처리한다).
    /// </summary>
    void HandleEnemyDied(EnemyHealth enemy)
    {
        if (enemy == null) return;
        if (enemy.GetComponent<Shrine>() != null) return;
        if (enemy.GetComponent<Boss>() != null) return;

        spawnedData.TryGetValue(enemy.gameObject, out var data);
        spawnedData.Remove(enemy.gameObject);
        EnemyRewards.GrantKill(enemy, data);

        // 경험치 구슬 — 원본 `if (run.kills % CONFIG.orb.every === 0)`(:1860).
        // **이번 런 처치 수 기준**이라(누적이 아니다) 런마다 50마리째부터 다시 센다.
        if (RunState.Kills > 0 && RunState.Kills % ExpOrb.DropEveryKills == 0)
            ExpOrb.Spawn(orbPrefab, enemy.transform.position, player);
    }
}
}
