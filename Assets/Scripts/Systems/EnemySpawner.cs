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
/// 스프린트 2(성장곡선 검증, `docs/sprints/03-growth-curve-worksplit.md` 트랙 A) — 스폰 직후 "가상 지역
/// 레벨"(<see cref="RunProgress"/>)에 따라 몹 체력/공격력을 스케일링하고, 그 몹이 죽으면
/// 처치 보상(골드+EXP)을 지급한다. 둘 다 이 스포너가 몹 프리팹을 다루는 유일한 지점이라
/// 자연스럽게 여기서 담당한다(트랙 A/B 경계 — `Core/PlayerProfile.cs`·`Characters/` 전체는
/// 트랙 B 소유라 손대지 않는다). **스케일링/보상 수치는 전부 `Core/DifficultyScalingConfig`에
/// 모아뒀다** — 이 파일엔 매직넘버를 두지 않는다(2026-09-08 "0. 착수 전 필독" 추가 — 원본
/// 밸런스가 완전히 정리된 게 아니라 나중에 직접 조정할 가능성이 높아서, 그때 로직 코드를
/// 뒤지지 않고 숫자만 한 파일에서 바꾸게 하려는 목적).
/// </summary>
public class EnemySpawner : MonoBehaviour
{
    [Header("몬스터 프리팹")]
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
    [Tooltip("구슬 스프라이트. 씬 빌더가 꽂아준다. 비어 있어도 동작은 하고 안 보이기만 한다.")]
    public Sprite orbSprite;


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
        var monster = SpawnMonsterAt(position);
        // 원본 `noElite: true`(project_test.html:1842) — 꼬마 슬라임은 엘리트로 승격되지 않는다.
        ApplyEnemyData(monster, data, allowElite: false);

        var move = monster.GetComponent<EnemyMove>();
        if (move != null) move.SetSpawnProtection(spawnProtectSeconds);

        aliveMonsters.Add(monster.transform);
    }

    EnemyData FindData(EnemyType enemyType) => catalog != null ? catalog.Find(enemyType) : null;

    /// <summary>
    /// 몬스터 프리팹 하나를 그 자리에 세우고 **쫓을 플레이어를 넣는다** — 잡몹·꼬마 슬라임·성소·보스가 모두 여기를 지난다(2026-11-05 2b —
    /// 예전엔 몹이 스스로 Player 태그로 찾았다). 위치만 받고 나머지(데이터·보호·이름)는 부르는 쪽이 정한다.
    /// </summary>
    GameObject SpawnMonsterAt(Vector3 position)
    {
        var go = Instantiate(monsterPrefab, position, Quaternion.identity);
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

        // 발을 지면에 붙인다 — 구조물이라 물리로 안 떨어지므로 세울 때 정확히 놓아야 한다.
        var go = SpawnMonsterAt(new Vector3(x, FieldBounds.GroundY + EntitySizeConfig.ShrineHeight / 2f, 0f));
        go.name = "Shrine";
        RunTransient.Mark(go);

        // 구조물이라 움직이지 않는다(원본 `if (e.shrine) continue;` :4024).
        // **컴포넌트를 끄는 게 아니라 플래그로** 처리한다 — 원본은 그 한 줄 앞에서 spawnInvuln을
        // 깎기 때문에(`:4023`), Update가 멈추면 아래 스폰 보호가 영영 안 풀린다.
        var move = go.GetComponent<EnemyMove>();
        if (move != null)
        {
            move.isStructure = true;
            move.SetSpawnProtection(Shrine.SpawnProtect); // 원본 `spawnInvuln: 1.0`(:4008) — 몹의 2초와 다르다
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.gravityScale = 0f; rb.linearVelocity = Vector2.zero; rb.bodyType = RigidbodyType2D.Kinematic; }
        }

        int level = Mathf.Clamp(RegionConfig.RecommendedLevel(RunProgress.RegionLv) + Random.Range(0, 2), 1, 40);
        var health = go.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.SetMaxHp(Shrine.HpForLevel(level));
            health.SetLevel(level);
            health.Died += HandleShrineDestroyed;
        }

        // 원본 w:64 h:96(`:698`) — 고블린(42×46)의 두 배 높이인 세로로 긴 구조물이다.
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0.54f, 0.42f); // 원본 colors.soul '#ff8a6a'
        FitToWorldSize(go, catalog != null ? catalog.shrine : null,
                       EntitySizeConfig.ShrineWidth, EntitySizeConfig.ShrineHeight);

        go.AddComponent<Shrine>();
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
        if (monsterPrefab == null) return;

        int region = RunState.Region;
        float x = Mathf.Lerp(FieldBounds.MinX, FieldBounds.MaxX, 0.62f);
        var go = SpawnMonsterAt(new Vector3(x, FieldBounds.GroundY + EntitySizeConfig.BossHeight / 2f, 0f));
        HitFeel.Shake(8f, 0.5f); // 원본 spawnBoss(:4174)
        go.name = "Boss";
        RunTransient.Mark(go);

        var health = go.GetComponent<EnemyHealth>();
        if (health != null)
        {
            // 보스도 회귀 장벽을 받는다(원본 `spawnBoss`의 `rebirthWallHpMult(r)` :4159).
            health.SetMaxHp(Boss.HpForRegion(region) * RegressionConfig.WallEnemyHp(region, ProfileService.Current.regressions));
            health.SetLevel(Boss.LevelForRegion(region));
            health.knockbackMultiplier = Boss.KnockbackMultiplier; // 거의 안 밀린다
            // 레벨 페널티 하한이 25%가 아니라 50%다(원본 `levelFactor`의 보스 분기 :1652).
            // `Boss` 컴포넌트는 아래에서 붙기 때문에 EnemyHealth가 스스로는 알 수 없다.
            health.isBoss = true;
            health.Died += HandleBossDied;
        }

        var move = go.GetComponent<EnemyMove>();
        if (move != null)
        {
            move.attackPower = Boss.DamageForRegion(region)
                               * RegressionConfig.WallEnemyDamage(region, ProfileService.Current.regressions);
            move.moveSpeed = Boss.MoveSpeed;
        }

        // 원본 w:130 h:150(`:721`) — 고블린(42×46)의 3배쯤 되는 덩치다.
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0.78f, 0.42f, 1f); // 원본 colors.soul '#c86aff'
        // 탄환 스프라이트는 **그림을 갈아끼우기 전**의 원형을 쓴다 — 보스 그림을 그대로 쓰면
        // 화면을 가리는 거대한 탄이 날아간다.
        Sprite bolt = sr != null ? sr.sprite : null;
        FitToWorldSize(go, catalog != null ? catalog.boss : null, EntitySizeConfig.BossWidth, EntitySizeConfig.BossHeight);

        var boss = go.AddComponent<Boss>();
        boss.projectileSprite = bolt;
        if (move != null) move.RefreshTypeBehaviour(); // Boss가 IEnemyMotion이라 다시 찾게 한다

        // 웨이브 상한엔 **안 넣는다** — 원본 `enemies.filter(e => !e.boss && !e.shrine)`(:3978).
        // 넣으면 보스전 상한(4) 한 자리를 보스가 먹어 잡몹이 3마리만 나온다.
    }

    /// <summary>
    /// 성소·보스를 **원본 픽셀 크기 그대로** 세운다(100px = 1유닛).
    ///
    /// 예전엔 "스프라이트를 갈아끼우되 보이던 크기를 유지"하는 방식이었는데, 그 '보이던 크기'가
    /// 애초에 틀렸다. 프리팹의 기본 스프라이트가 32×32짜리 내장 이미지라, 거기에 맞춰 손으로
    /// 고른 `localScale`(성소 1.3×1.9)이 실제로는 0.42×0.61유닛밖에 안 됐다 —
    /// **원본 성소(0.64×0.96)보다 작고, 고블린보다도 작았다.** 눈으로 보기 전엔 알 수 없는 종류의
    /// 어긋남이라, 기준을 "원본 유닛 크기"로 바꿔 손으로 고른 숫자를 없앴다.
    ///
    /// 콜라이더가 같은 GameObject에 있어 `localScale`이 물리에도 걸리므로, 반지름은 역으로 나눠
    /// 월드 반지름을 따로 맞춘다(`ApplySize`가 잡몹에게 하는 것과 같은 계산).
    /// </summary>
    static void FitToWorldSize(GameObject go, Sprite sprite, float worldWidth, float worldHeight)
    {
        var sr = go.GetComponent<SpriteRenderer>();

        // 그림이 있으면 그림 크기를 기준으로, 없으면 프리팹의 기본 스프라이트를 기준으로 늘린다.
        Sprite basis = sprite != null ? sprite : (sr != null ? sr.sprite : null);
        Vector2 size = basis != null ? (Vector2)basis.bounds.size : Vector2.one;
        if (size.x < 0.0001f || size.y < 0.0001f) return;

        var scale = new Vector3(worldWidth / size.x, worldHeight / size.y, 1f);
        go.transform.localScale = scale;

        // 원형 콜라이더라 가로/세로 중 **작은 쪽**에 맞춘다 — 큰 쪽에 맞추면 그림 밖까지 맞는다.
        var col = go.GetComponent<CircleCollider2D>();
        if (col != null && Mathf.Abs(scale.x) > 0.0001f)
            col.radius = (Mathf.Min(worldWidth, worldHeight) / 2f) / scale.x;

        if (sprite != null && sr != null)
        {
            sr.sprite = sprite;
            sr.color = Color.white; // 그림에 색이 이미 칠해져 있다 — 또 곱하면 그 색으로 물든다
        }
    }

    /// <summary>
    /// 원본 `onBossKilled`(project_test.html:4282) + `killEnemy`의 보스 보상 분기.
    /// **처치 수엔 안 들어가지만 살기에는 들어간다**(`run.fury++`는 보스 포함, `run.kills++`는 제외).
    /// </summary>
    void HandleBossDied(EnemyHealth enemy)
    {
        int region = RunState.Region;
        float sc = DifficultyScalingConfig.RewardMultiplier(region);

        int exp = Mathf.RoundToInt(Boss.RewardExp * sc);
        int gold = Mathf.RoundToInt(Boss.RewardGold * sc); // 보스는 확률 무시하고 항상 드랍(:1818)
        ProfileService.Current.AddExp(exp);
        ProfileService.Current.AddGold(gold);
        RunState.RegisterReward(gold, exp);
        RunState.RegisterKill(isBoss: true);

        ProfileService.Current.stats.bosses++;
        ProfileService.Current.MarkBossCleared(region); // 다음 지역이 열린다
        Achievements.CheckNew(ProfileService.Current);

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
                GameObject monster = SpawnMonsterAt(spawnPos);
                ApplyEnemyData(monster, PickEnemyData(), allowElite: true);
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
        var rolled = FindData(EnemySpawnTable.Roll(RunProgress.RegionLv));
        return rolled != null ? rolled : types[Random.Range(0, types.Length)];
    }

    /// <summary>
    /// 스폰 직후 한 번: **종류별 기본 스탯(`EnemyData`) × 지역 배율(`DifficultyScalingConfig`)** 을 적용하고
    /// 처치 보상을 연결한다. 원본도 같은 두 층 구조다 — `CONFIG.enemyBase[type]`에 지역 배율을 곱한다(`:3958`).
    ///
    /// `EnemyData`가 안 꽂혀 있으면 프리팹에 들어 있는 값을 그대로 쓴다(스폰 자체는 계속 되게).
    /// </summary>
    void ApplyEnemyData(GameObject monster, EnemyData data, bool allowElite)
    {
        // 로비로 나가면 필드가 비워져야 한다(원본 `enemies = []`, project_test.html:4318).
        RunTransient.Mark(monster);

        int regionLv = RunProgress.RegionLv;

        // 원본 `elite = !opts.noElite && Math.random() < CONFIG.elite.chance`(project_test.html:3948).
        // 엘리트는 별도 종류가 아니라 **아무 몹에게나 붙는 승격**이라, 종류를 정한 뒤에 굴린다.
        // 몬스터 유인향 — 엘리트 출현률을 올린다(원본 `chance * (1 + itemPow('eliteLure'))` :3948).
        bool elite = allowElite &&
                     Random.value < EliteChance * (1f + ProfileService.Current.items.Pow("eliteLure"));
        if (elite) monster.AddComponent<EnemyElite>();

        var health = monster.GetComponent<EnemyHealth>();
        if (health != null)
        {
            float baseHp = data != null ? data.maxHp : health.MaxHp;
            float hp = DifficultyScalingConfig.ScaledHp(baseHp, regionLv);
            // 회귀 장벽 — 권장 회귀에 모자란 지역이면 몹이 훨씬 단단해진다(원본 `rebirthWallHpMult` :975).
            hp *= RegressionConfig.WallEnemyHp(regionLv, ProfileService.Current.regressions);
            if (elite) hp *= DifficultyScalingConfig.EliteHpMult;
            // 필드만 바꾸면 이미 실행된 Awake가 세팅한 CurrentHp엔 반영 안 되는 이 프로젝트 단골
            // 함정이 있어(EnemyHealth 참고) 반드시 SetMaxHp()를 통해서 바꾼다.
            health.SetMaxHp(hp);
            // 원본 `enemyLv()`(project_test.html:3924) — 지역 기준 레벨 ±1, 1~40으로 제한.
            // 난이도가 아니라 **레벨 페널티 전용**이다(체력·피해는 위 지역 배율이 이미 정했다).
            health.SetLevel(Mathf.Clamp(RegionConfig.RecommendedLevel(regionLv) + Random.Range(0, 2), 1, 40));
            if (data != null) health.knockbackMultiplier = data.knockbackMultiplier;
            health.Died += HandleEnemyDied;
        }

        var move = monster.GetComponent<EnemyMove>();
        if (move != null)
        {
            float baseDmg = data != null ? data.attackPower : move.attackPower;
            float dmg = DifficultyScalingConfig.ScaledDmg(baseDmg, regionLv);
            dmg *= RegressionConfig.WallEnemyDamage(regionLv, ProfileService.Current.regressions); // 원본 :976
            if (elite) dmg *= DifficultyScalingConfig.EliteDmgMult;
            move.attackPower = dmg;
            // 원본 `speed: base.speed * rand(0.9, 1.1)`(project_test.html:3960) — 같은 종이라도 마리마다
            // 걸음이 조금씩 다르다. 이게 없으면 한 웨이브가 통째로 한 덩어리로 몰려온다.
            if (data != null) move.moveSpeed = data.moveSpeed * Random.Range(0.9f, 1.1f);
        }

        if (data != null)
        {
            var sr = monster.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (data.sprite != null)
                {
                    // 정식 그림이 있으면 그걸 쓰고 **색조는 흰색으로 둔다** — 그림에 이미
                    // 종류별 색이 칠해져 있어서 여기서 또 곱하면 전부 그 색으로 물든다.
                    sr.sprite = data.sprite;
                    sr.color = Color.white;
                }
                else
                {
                    // 지금 기본 경로다(사용자 지시로 스킨을 끈 상태). 종류별 **도형**을 꽂고
                    // 종류색으로 칠한다 — 도형이 행동 부류를, 색이 종류를 구분한다.
                    var shapeSprite = SpriteFor(data.shape);
                    if (shapeSprite != null) sr.sprite = shapeSprite;
                    sr.color = data.color;
                }
            }
            AttachTypeBehaviour(monster, data, sr);
            spawnedData[monster] = data; // 처치 보상에서 종류별 exp/gold를 읽으려고 기억해둔다
        }

        // ⚠️ 스프라이트를 꽂은 **뒤에** 크기를 정한다 — 그림 높이를 보고 스케일을 계산하기 때문.
        ApplySize(monster, data, elite);
    }

    /// <summary>
    /// 종류별 몸집(`EnemyData.colliderRadius`)과 엘리트 확대(원본 `w: base.w * sizeMul`, `:3954`)를
    /// **`transform.localScale`로** 적용한다. 콜라이더 반지름만 바꾸면 스프라이트는 그대로라
    /// 오우거가 "보이는 것보다 훨씬 넓게 때리는" 몹이 된다 — 눈에 안 보이는 종류의 어긋남이라
    /// 굳이 스케일 쪽으로 통일했다(<see cref="EnemyMove.WorldRadius"/> 참고).
    ///
    /// 스폰 지점 Y는 프리팹 반지름(고블린 0.5) 기준으로 이미 계산돼 있으므로, 커진 만큼 들어올려
    /// 발을 지면/발판에 맞춘다. 안 그러면 큰 몹이 지면에 파묻힌 채 물리에 밀려 튀어오른다.
    /// </summary>
    void ApplySize(GameObject monster, EnemyData data, bool elite)
    {
        var col = monster.GetComponent<CircleCollider2D>();
        if (col == null) return;

        float prefabWorldRadius = col.radius * monster.transform.localScale.x; // 고블린 기준 0.5
        if (prefabWorldRadius <= 0f) return;

        float wantRadius = (data != null ? data.colliderRadius : prefabWorldRadius)
                           * (elite ? DifficultyScalingConfig.EliteScale : 1f);

        // ── 크기를 정하는 두 조건을 동시에 맞춘다 ──────────────────────────────
        // 콜라이더와 스프라이트가 **같은 GameObject**에 있어서 `localScale`이 둘 다에 걸린다.
        // 그래서 스케일 하나로 그림 크기를 맞추고, 콜라이더는 `radius`를 역으로 나눠 보정한다:
        //   월드 반지름 = radius × scale  →  radius = 원하는반지름 / scale
        // 이렇게 안 하면 둘 중 하나는 반드시 어긋난다(그림이 히트박스보다 작거나, 그 반대).
        // **렌더러에 실제로 꽂혀 있는 스프라이트**의 높이로 계산한다. 예전엔 `data.sprite`만 봤는데,
        // 그러면 도형 스프라이트로 갈아끼운 경우(지금 기본 경로)에 이 분기를 안 타서 프리팹 내장
        // 스프라이트 크기를 전제로 스케일이 잡혔다 — 덩치가 조용히 틀어지는 종류의 버그다.
        float scale = 1f;
        var sr = monster.GetComponent<SpriteRenderer>();
        float spriteHeight = sr != null && sr.sprite != null ? sr.sprite.bounds.size.y : 0f;
        if (spriteHeight > 0.0001f) scale = (wantRadius * 2f) / spriteHeight;
        else scale = wantRadius / prefabWorldRadius; // 스프라이트가 없으면 콜라이더 기준으로

        monster.transform.localScale = new Vector3(scale, scale, 1f);
        col.radius = wantRadius / scale;

        // 스폰 지점은 프리팹 반지름 기준으로 계산돼 있다 — 커진 만큼 들어올려 발을 지면에 맞춘다.
        // 안 그러면 큰 몹이 지면에 파묻힌 채 물리에 밀려 튀어오른다.
        monster.transform.position += new Vector3(0f, wantRadius - prefabWorldRadius, 0f);
    }

    /// <summary>
    /// 종류별 행동 스크립트를 붙인다 — 원본 `updateEnemies()`의 타입 분기(project_test.html:4042~4111)와
    /// `killEnemy()`의 분열 분기(`:1840`)에 각각 대응한다.
    ///
    /// 고블린·오우거·꼬마 슬라임은 **붙일 게 없다**. 셋 다 기본 보행 AI를 그대로 쓰고 수치만 다르기 때문이다
    /// (CLAUDE.md "SO는 수치만, 행동이 다르면 스크립트 분리" 규칙의 '수치만' 쪽).
    ///
    /// 붙인 뒤 <see cref="EnemyMove.RefreshTypeBehaviour"/>를 **반드시** 불러야 한다 —
    /// `Instantiate` 시점에 이미 끝난 `EnemyMove.Awake`는 여기서 붙인 컴포넌트를 모르기 때문이다.
    /// </summary>
    void AttachTypeBehaviour(GameObject monster, EnemyData data, SpriteRenderer sr)
    {
        switch (data.type)
        {
            case EnemyType.Flyer:
                monster.AddComponent<EnemyFlyerMotion>();
                // 원본은 박쥐만 스폰 지점보다 50px 위에서 나온다(`y: type === 'wisp' ? y - 50 : y`,
                // project_test.html:3955 — 원본은 Y+가 아래라 부호가 뒤집힌다).
                monster.transform.position += new Vector3(0f, 0.5f, 0f);
                break;

            case EnemyType.Charger:
                monster.AddComponent<EnemyChargerMotion>();
                break;

            case EnemyType.Shooter:
                var shooter = monster.AddComponent<EnemyShooterMotion>();
                // 화염탄은 런타임에 새 오브젝트로 생성된다 — 몹이 쓰는 스프라이트를 그대로 물려준다.
                if (sr != null) shooter.boltSprite = sr.sprite;
                shooter.boltColor = data.color;
                break;

            case EnemyType.Splitter:
                monster.AddComponent<EnemySplitOnDeath>();
                break;
        }

        var move = monster.GetComponent<EnemyMove>();
        if (move != null) move.RefreshTypeBehaviour();
    }

    /// <summary>
    /// 처치 보상(원본 killEnemy(), project_test.html:1793) — EXP는 항상 지급, 골드는 확률 드랍.
    /// 종류별 기본 보상은 <see cref="EnemyData"/>, 지역 배율은 <see cref="DifficultyScalingConfig"/>.
    /// 엘리트 배수·연쇄처치·살기(fury) 보너스는 아직 범위 밖 — "몹 1마리 = 고정 공식" 루프만 구현한다.
    /// </summary>
    void HandleEnemyDied(EnemyHealth enemy)
    {
        // 성소·보스는 일반 처치 보상 경로를 타지 않는다 — 원본도 각각 따로 처리한다
        // (성소 :1797에서 `return`, 보스는 고정 보상 + `onBossKilled`). 둘 다 전용 핸들러가 있다.
        if (enemy == null) return;
        if (enemy.GetComponent<Shrine>() != null) return;
        if (enemy.GetComponent<Boss>() != null) return;

        bool elite = enemy.GetComponent<EnemyElite>() != null;

        var stats = ProfileService.Current.stats;
        stats.totalKills++;
        if (elite) stats.elites++;

        RunProgress.RegisterKill();
        // 결과 화면·HUD가 읽는 이번 런 집계. 원본도 `run.kills`와 누적 통계를 따로 센다(:1857).
        RunState.RegisterKill(isBoss: false);
        // 지역 토벌 진행도 — 100마리를 채우면 그 지역 보스가 열린다(원본 :1866).
        ProfileService.Current.RegisterRegionKill(RunState.Region);

        EnemyData data = null;
        if (enemy != null) spawnedData.TryGetValue(enemy.gameObject, out data);

        int enemyLevel = enemy.Level;
        float sc = DifficultyScalingConfig.RewardMultiplier(RunProgress.RegionLv);
        // 원본 `eliteMult = e.elite ? CONFIG.elite.rewardMult : 1`(project_test.html:1810).
        if (elite) sc *= DifficultyScalingConfig.EliteRewardMult;

        float baseExp = data != null ? data.exp : 8f;
        int goldMin = data != null ? data.goldMin : 5;
        int goldMax = data != null ? data.goldMax : 10;

        // 수행의 굴레 — 경험치 획득 배수(원본 `expMultAll()` :1305).
        int expGained = Mathf.RoundToInt(baseExp * sc * CombatModifiers.ExpMultiplier);
        ProfileService.Current.AddExp(expGained);
        int goldGained = 0;

        // 원본 `if (Math.random() < goldDropChance || e.boss || e.elite)`(`:1818`) — 엘리트는 확률 무시하고 항상 드랍.
        if (elite || Random.value < DifficultyScalingConfig.GoldDropChance)
        {
            int rolled = Random.Range(goldMin, goldMax + 1); // Random.Range(int)는 상한이 배타적이라 +1
            // 원본 `goldMultAll()`(:1304) — 콤보가 재화도 늘린다.
            goldGained = Mathf.RoundToInt(rolled * sc * CombatModifiers.GoldMultiplier);
            ProfileService.Current.AddGold(goldGained);
        }

        // 연쇄 처치 — 0.8초 안에 3마리째부터 골드 보너스(원본 `killEnemy`의 연쇄 분기, :1828).
        int chainBonus = CombatModifiers.RegisterKillForChain(enemyLevel, isBoss: false);
        if (chainBonus > 0)
        {
            ProfileService.Current.AddGold(chainBonus);
            goldGained += chainBonus;
            // 원본 연쇄 분기(:1835~:1836) — 멈춤 0.10, 엘리트면 연쇄 수만큼 세게 흔든다.
            CombatEvents.RequestHitstop(HitFeel.HitstopChain);
            if (elite) HitFeel.Shake(Mathf.Min(14f, 5f + CombatModifiers.ChainKills), 0.25f);
        }

        // 원본 `run.goldEarned += g; run.expEarned += exp`(:1816·:1820) — 결과 화면에 쓴다.
        RunState.RegisterReward(goldGained, expGained);

        Achievements.CheckNew(ProfileService.Current);

        // 경험치 구슬 — 원본 `if (run.kills % CONFIG.orb.every === 0)`(:1860).
        // **이번 런 처치 수 기준**이라(누적이 아니다) 런마다 50마리째부터 다시 센다.
        if (RunState.Kills > 0 && RunState.Kills % ExpOrb.DropEveryKills == 0 && enemy != null)
            ExpOrb.Spawn(enemy.transform.position, orbSprite, player);

        if (enemy != null) spawnedData.Remove(enemy.gameObject);
    }
}
}
