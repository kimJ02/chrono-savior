using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 스프린트 2(성장곡선 검증) 트랙 A — 지역(<see cref="RunState.Region"/>, 예전 '가상 지역 레벨')과
/// 처치 보상(<see cref="EnemySpawner"/>.HandleEnemyDied)이 실제로 맞물려 도는지 검증한다.
/// 공식 자체는 `docs/sprints/03-growth-curve-worksplit.md`(원본 CONFIG.scale, project_test.html:727) 그대로.
///
/// `RunState`/`ProfileService.Current`는 둘 다 정적 상태라 테스트끼리 서로 오염시키므로
/// 매 테스트 시작 전 반드시 초기화한다.
/// </summary>
public class RegionScalingAndRewardsTests
{
    // 고블린 기본 스탯(원본 CONFIG.enemyBase.oni, project_test.html:709).
    // 2026-09-09 SO 전환으로 `DifficultyScalingConfig`에서 `Core/EnemyData`(에셋)로 옮겨졌다.
    // 아래 테스트들은 `EnemyData`를 안 꽂고 **프리팹 기본값 폴백 경로**를 검증하므로 기대값을 여기 둔다.
    const float OniBaseHp = 38f;
    const float OniBaseDmg = 13f;
    const float OniBaseExp = 8f;

    [SetUp]
    public void ResetStatics()
    {
        // 난이도 기준이 RunState.Region으로 바뀌어서, 앞 테스트가 올려둔 지역이 남으면
        // 뒤 테스트의 몹 체력이 배로 뛴다(정적 상태 오염 — 이 프로젝트 단골 함정).
        RunState.Reset();
        RunTransient.Reset();
        // 콤보·연쇄는 정적 상태다 — 안 지우면 앞 테스트의 콤보가 뒤 테스트의 골드를 부풀린다.
        CombatModifiers.Reset();
        ProfileService.Replace(new PlayerProfile());
    }

    /// <summary>스포너가 읽을 몬스터 목록 — 메모리에만 만드는 인스턴스(2026-11-05, 예전엔 스포너 필드에 몹 데이터를 직접 넣었다).</summary>
    static MonsterCatalog CatalogOf(params EnemyData[] types)
    {
        var c = ScriptableObject.CreateInstance<MonsterCatalog>();
        c.types = types;
        return c;
    }

    [TearDown]
    public void Cleanup()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go == null) continue;
            if (go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        }
        RunState.Reset();
        RunTransient.Reset();
    }

    /// <summary>스폰용 프리팹 템플릿 — EnemyHealth/EnemyMove가 실제로 붙어 있어야
    /// EnemySpawner.ApplyRegionScaling이 컴포넌트를 찾아 스탯을 바꿀 수 있다.</summary>
    static GameObject NewMonsterPrefab()
    {
        var go = new GameObject("TestMonsterPrefab");
        go.tag = "Enemy";
        go.AddComponent<SpriteRenderer>();          // EnemyHealth가 RequireComponent로 요구
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        go.AddComponent<EnemyMove>();                // Rigidbody2D도 같이 붙음
        go.AddComponent<EnemyHealth>();
        return go;
    }

    static EnemySpawner NewSpawner(GameObject prefab)
    {
        var spawnerGO = new GameObject("TestSpawner");
        var spawner = spawnerGO.AddComponent<EnemySpawner>();
        spawner.monsterPrefab = prefab;
        // waveTimer 기본값이 0이라 그냥 두면 Update()가 첫 프레임에 자동으로 SpawnWave()를 한 번 더
        // 불러버려서(원본 의도상 정상 동작 — 스포너가 활성화되자마자 첫 웨이브가 바로 터짐) 테스트가
        // 기대하는 "정확히 N마리"가 깨진다(실제로 겪음: 1마리만 스폰했는데 2마리로 나옴). 컴포넌트를
        // 통째로 꺼버리면 aliveMonsters.RemoveAll(null) 같은 나머지 Update 로직까지 같이 죽어서
        // 40마리 연속 처치 테스트가 깨지므로, waveTimer만 충분히 크게 밀어 자동 웨이브만 막는다.
        var timerField = typeof(EnemySpawner).GetField("waveTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(spawner, 9999f);
        // 엘리트는 8% 확률로 체력×4·피해×1.5·보상×5를 걸어버려서, 그냥 두면 아래 테스트들의 기대값이
        // 가끔씩만 틀리는 간헐적 실패가 된다(실제로 40마리 처치 테스트가 EXP 320 대신 384로 깨졌다).
        // 엘리트 자체는 전용 테스트에서 확률 1로 고정해 검증한다.
        spawner.eliteChanceOverride = 0f;
        return spawner;
    }

    static void InvokeSpawnWave(EnemySpawner spawner)
    {
        var method = typeof(EnemySpawner).GetMethod("SpawnWave", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(method, "EnemySpawner.SpawnWave 리플렉션 실패 — 시그니처가 바뀌었는지 확인");
        method.Invoke(spawner, null);
    }

    static List<Transform> GetAlive(EnemySpawner spawner)
    {
        var field = typeof(EnemySpawner).GetField("aliveMonsters", BindingFlags.NonPublic | BindingFlags.Instance);
        return (List<Transform>)field.GetValue(spawner);
    }

    /// <summary>스폰 직후 무적을 꺼야 TakeDamage가 실제로 먹힌다(EnemyHealthTests와 동일한 함정).</summary>
    static void RemoveSpawnProtection(GameObject enemy)
    {
        var move = enemy.GetComponent<EnemyMove>();
        var timerField = typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        timerField.SetValue(move, 0f);
        enemy.GetComponent<Rigidbody2D>().gravityScale = 0f; // 테스트끼리 물리 간섭 방지(이 프로젝트 단골 함정)
    }

    /// <summary>
    /// 난수 쓰는 순서(2026-11-06 5단계 — 설정·보상을 `EnemySetup`·`EnemyRewards`로 옮기면서 고정): 몹 설정은 엘리트 → 레벨 → 이동속도,
    /// 처치 보상은 골드 드랍 확률 → 골드 양. 순서가 바뀌면 같은 시드에서 다른 몹·다른 보상이 나온다.
    /// </summary>
    [Test]
    public void SetupAndRewards_UseRandomInTheSameOrder()
    {
        var data = ScriptableObject.CreateInstance<EnemyData>();
        var go = new GameObject("TestOrderMonster");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var health = go.AddComponent<EnemyHealth>();
        var move = go.AddComponent<EnemyMove>();

        Random.InitState(1234);
        bool elite = Random.value < 0.5f;
        int levelStep = Random.Range(0, 2);
        float speed = data.moveSpeed * Random.Range(0.9f, 1.1f);
        Random.InitState(1234);
        EnemySetup.Apply(go, data, allowElite: true, eliteChance: 0.5f, shapeSprite: null);
        Assert.AreEqual(elite, go.GetComponent<EnemyElite>() != null, "엘리트가 첫 난수");
        Assert.AreEqual(Mathf.Clamp(RegionConfig.RecommendedLevel(RunState.Region) + levelStep, 1, 40), health.Level, "레벨이 둘째");
        Assert.AreEqual(speed, move.moveSpeed, 1e-5f, "이동속도가 셋째");

        var victim = new GameObject("TestOrderVictim");
        var victimHealth = victim.AddComponent<EnemyHealth>();
        int seed = 0;
        while (true) { Random.InitState(seed); if (Random.value < DifficultyScalingConfig.GoldDropChance) break; seed++; } // 골드가 떨어지는 시드
        Random.InitState(seed);
        _ = Random.value; // 드랍 확률
        int rolled = Random.Range(data.goldMin, data.goldMax + 1);
        int expectedGold = Mathf.RoundToInt(rolled * DifficultyScalingConfig.RewardMultiplier(RunState.Region) * CombatModifiers.GoldMultiplier);
        Random.InitState(seed);
        EnemyRewards.GrantKill(victimHealth, data);
        Assert.AreEqual(expectedGold, RunState.GoldEarned, "골드 드랍 확률 다음에 양을 굴린다");
        Object.DestroyImmediate(data);
    }

    /// <summary>
    /// 보스 처치 보상(2026-11-06 정리 순서 5-2 — `EnemyRewards`로 옮기면서 고정한 특성 테스트): 지역 배율을 곱한 고정 EXP·골드(확률 없음),
    /// 처치 수엔 안 들고 살기엔 든다, 보스 통계와 지역 정복(원본 `onBossKilled` :4282·`killEnemy` 보스 분기).
    /// </summary>
    [Test]
    public void BossKill_GrantsScaledFixedRewards_AndConquersTheRegion()
    {
        RunState.Begin(2, RunMode.Boss);
        float sc = DifficultyScalingConfig.RewardMultiplier(2);

        EnemyRewards.GrantBossKill(2);

        Assert.AreEqual(Mathf.RoundToInt(Boss.RewardGold * sc), RunState.GoldEarned);
        Assert.AreEqual(Mathf.RoundToInt(Boss.RewardExp * sc), RunState.ExpEarned);
        Assert.AreEqual(RunState.GoldEarned, ProfileService.Current.gold, "골드는 프로필에도 들어간다");
        Assert.AreEqual(0, RunState.Kills, "보스는 처치 수에 안 든다");
        Assert.AreEqual(1, RunState.Fury, "보스도 살기엔 든다");
        Assert.AreEqual(1, ProfileService.Current.stats.bosses);
        Assert.IsTrue(ProfileService.Current.IsBossCleared(2), "지역이 정복된다");
    }

    /// <summary>
    /// 스폰 시점의 RegionLv에 따라 몹 체력/공격력이 원본 CONFIG.scale 공식대로 스케일링되는지 확인한다.
    /// hp = 38 × 2.15^(RegionLv-1), dmg = 13 × 1.4^(RegionLv-1).
    /// </summary>
    [UnityTest]
    public IEnumerator SpawnWave_ScalesEnemyStatsByRegionLevel()
    {
        RunState.Begin(3, RunMode.Normal); // 3지역에 입장한 상태로 스폰시킨다

        var prefab = NewMonsterPrefab();
        var spawner = NewSpawner(prefab);
        spawner.maxSpawnPerWave = 1;
        spawner.maxAliveTotal = 10;

        InvokeSpawnWave(spawner);
        yield return null;

        var alive = GetAlive(spawner);
        Assert.AreEqual(1, alive.Count, "1마리만 스폰하도록 했는데 개수가 다르다");

        var health = alive[0].GetComponent<EnemyHealth>();
        var move = alive[0].GetComponent<EnemyMove>();
        // 3지역은 권장 회귀 1회다 — 시간 회귀 0회로 들어가면 **회귀 장벽**이 몹 체력 ×2.8, 피해 ×1.45로
        // 얹힌다(원본 `rebirthWallHpMult` :975). 지역 배율만 기대하면 여기서 어긋난다.
        int regressions = ProfileService.Current.regressions;
        float expectedHp = DifficultyScalingConfig.ScaledHp(OniBaseHp, 3)
                           * RegressionConfig.WallEnemyHp(3, regressions);
        float expectedDmg = DifficultyScalingConfig.ScaledDmg(OniBaseDmg, 3)
                            * RegressionConfig.WallEnemyDamage(3, regressions);
        Assert.AreEqual(expectedHp, health.MaxHp, 0.01f, "3지역 체력 스케일링(DifficultyScalingConfig.ScaledHp(OniBaseHp, 3))이 안 맞는다");
        Assert.AreEqual(expectedDmg, move.attackPower, 0.01f, "3지역 공격력 스케일링(DifficultyScalingConfig.ScaledDmg(OniBaseDmg, 3))이 안 맞는다");
        // SetMaxHp()를 거쳤다면 CurrentHp도 같이 갱신돼야 한다(안 그러면 이 프로젝트 단골 함정 재발).
        Assert.AreEqual(expectedHp, health.CurrentHp, 0.01f, "SetMaxHp() 대신 필드 직접 대입을 써서 CurrentHp가 안 갱신됐다");
    }

    /// <summary>
    /// SO 전환 검증 — `EnemyData`를 꽂으면 프리팹 기본값 대신 **그 종류의 수치**가 적용되는지.
    /// 오우거 값(체력 160·피해 26·넉백 0.4, 원본 `:710`·`:1678`)으로 확인한다.
    /// </summary>
    [UnityTest]
    public IEnumerator SpawnWave_AppliesEnemyDataStats_InsteadOfPrefabDefaults()
    {
        var prefab = NewMonsterPrefab();
        var spawner = NewSpawner(prefab);
        spawner.maxSpawnPerWave = 1;

        var bigOni = ScriptableObject.CreateInstance<EnemyData>();
        bigOni.type = EnemyType.Brute;
        bigOni.maxHp = 160f;
        bigOni.attackPower = 26f;
        bigOni.moveSpeed = 0.44f;
        bigOni.knockbackMultiplier = 0.4f;
        bigOni.colliderRadius = 0.87f;
        spawner.Catalog = CatalogOf(bigOni);

        InvokeSpawnWave(spawner);
        yield return null;

        var alive = GetAlive(spawner);
        Assert.AreEqual(1, alive.Count);

        var health = alive[0].GetComponent<EnemyHealth>();
        var move = alive[0].GetComponent<EnemyMove>();

        // 1지역이라 지역 배율은 1 — EnemyData 값이 그대로 나와야 한다.
        Assert.AreEqual(160f, health.MaxHp, 0.01f, "EnemyData의 체력이 안 쓰였다(프리팹 기본값 38이 그대로인 듯)");
        Assert.AreEqual(160f, health.CurrentHp, 0.01f, "SetMaxHp()를 안 거쳐서 CurrentHp가 안 갱신됐다");
        Assert.AreEqual(26f, move.attackPower, 0.01f, "EnemyData의 공격력이 안 쓰였다");
        // 원본 `speed: base.speed * rand(0.9, 1.1)`(:3960) — 마리마다 걸음이 다르므로 범위로 본다.
        Assert.GreaterOrEqual(move.moveSpeed, 0.44f * 0.9f - 0.001f, "EnemyData의 이동속도가 안 쓰였다");
        Assert.LessOrEqual(move.moveSpeed, 0.44f * 1.1f + 0.001f, "이동속도 편차가 원본 ±10%를 벗어났다");
        Assert.AreEqual(0.4f, health.knockbackMultiplier, 0.01f, "오우거 넉백 저항(0.4, 원본 :1678)이 안 쓰였다");
        // 몸집은 콜라이더 반지름이 아니라 transform 스케일로 준다 — 그래야 스프라이트와 판정이 같이 커진다
        // (반지름만 키우면 오우거가 "보이는 것보다 넓게 때리는" 몹이 된다). 실제 월드 반지름으로 확인한다.
        Assert.AreEqual(0.87f, move.WorldRadius, 0.01f, "EnemyData의 몸집이 안 쓰였다");

        Object.DestroyImmediate(bigOni);
    }

    /// <summary>몹 1마리를 죽였을 때 EXP는 항상 지급되고(1지역, sc=1 → 8 그대로) 처치 수도 오르는지 확인한다.</summary>
    [UnityTest]
    public IEnumerator EnemyDeath_AlwaysGrantsExp_AndRegistersKill()
    {
        var attacker = new GameObject("TestAttacker");
        var prefab = NewMonsterPrefab();
        var spawner = NewSpawner(prefab);
        spawner.maxSpawnPerWave = 1;
        spawner.maxAliveTotal = 10;

        InvokeSpawnWave(spawner);
        yield return null;

        var enemyGO = GetAlive(spawner)[0].gameObject;
        RemoveSpawnProtection(enemyGO);
        var health = enemyGO.GetComponent<EnemyHealth>();

        health.TakeDamage(health.MaxHp * 10f, attacker); // 확실히 즉사시킬 만큼(레벨 페널티가 깎아도 남게)
        yield return null;

        Assert.AreEqual(1, RunState.Kills, "죽였는데 이번 런 처치 수가 안 올랐다");
        int expectedExp = Mathf.RoundToInt(OniBaseExp * DifficultyScalingConfig.RewardMultiplier(1));
        Assert.AreEqual(expectedExp, ProfileService.Current.exp, "1지역(sc=1) 고블린 처치 EXP는 OniBaseExp 그대로여야 한다");
    }

    /// <summary>
    /// 골드는 75% 확률 드랍이라 한 번으로는 검증할 수 없다 — 여러 마리를 죽여서 통계적으로 확인한다.
    /// 40마리 연속 무드랍 확률은 0.25^40 ≈ 0에 수렴하므로 골드가 0으로 남으면 확률 로직 자체가 깨진 것.
    /// 이 구간(1~40마리)에선 RegionLv가 계속 1로 고정돼(100마리 미만) EXP는 정확히 8×40으로 결정적이다.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyDeath_DropsGoldMostOfTheTime_OverManyKills()
    {
        var attacker = new GameObject("TestAttacker");
        var prefab = NewMonsterPrefab();
        var spawner = NewSpawner(prefab);
        spawner.maxSpawnPerWave = 1;
        spawner.maxAliveTotal = 1; // 매번 한 마리씩만 살아있게(죽이고 바로 다음 마리 스폰)

        const int kills = 40;
        for (int i = 0; i < kills; i++)
        {
            InvokeSpawnWave(spawner);
            yield return null;

            var enemyGO = GetAlive(spawner)[GetAlive(spawner).Count - 1].gameObject;
            RemoveSpawnProtection(enemyGO);
            var health = enemyGO.GetComponent<EnemyHealth>();
            health.TakeDamage(health.MaxHp * 10f, attacker);
            yield return null;
        }

        Assert.AreEqual(kills, RunState.Kills);
        int expectedExp = Mathf.RoundToInt(OniBaseExp * DifficultyScalingConfig.RewardMultiplier(1)) * kills;
        Assert.AreEqual(expectedExp, ProfileService.Current.exp, $"40마리 전부 1지역(sc=1)이라 EXP는 결정적이어야 한다(OniBaseExp×{kills})");
        Assert.Greater(ProfileService.Current.gold, 0, $"{kills}마리 죽였는데 골드가 한 번도 안 드랍됐다(DifficultyScalingConfig.GoldDropChance 확률상 사실상 불가능) — 확률 로직 확인 필요");
    }

    /// <summary>
    /// 결과 화면에 뜰 이번 런 집계(처치수·골드·경험치)가 실제 처치 경로에서 쌓이는지.
    /// 배선이 빠지면 **에러 없이 결과 화면이 전부 0으로만 뜬다** — 눈에 안 띄는 종류의 고장이라 못 박는다.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyKill_AccumulatesRunTotalsForResultScreen()
    {
        RunState.Begin(1, RunMode.Normal);
        Assert.AreEqual(0, RunState.Kills);
        Assert.AreEqual(0, RunState.ExpEarned);

        var attacker = new GameObject("TestAttacker");
        var prefab = NewMonsterPrefab();
        var spawner = NewSpawner(prefab);
        spawner.maxSpawnPerWave = 1;
        spawner.maxAliveTotal = 10;

        InvokeSpawnWave(spawner);
        yield return null;

        var enemyGO = GetAlive(spawner)[0].gameObject;
        RemoveSpawnProtection(enemyGO);
        var health = enemyGO.GetComponent<EnemyHealth>();
        health.TakeDamage(health.MaxHp * 10f, attacker);
        yield return null;

        Assert.AreEqual(1, RunState.Kills, "몹을 잡았는데 이번 런 처치수가 안 올랐다");
        Assert.Greater(RunState.ExpEarned, 0, "EXP는 항상 지급되므로 집계도 0이면 안 된다");
        Assert.AreEqual(ProfileService.Current.exp, RunState.ExpEarned,
            "결과 화면 집계와 실제 지급액이 어긋난다");
    }

    /// <summary>
    /// 엘리트 승격(원본 `CONFIG.elite` :696) — 체력 ×4, 피해 ×1.5, 몸집 ×1.35.
    /// 확률을 1로 고정해서 결정적으로 본다.
    /// </summary>
    [UnityTest]
    public IEnumerator Elite_MultipliesHpDamageAndSize()
    {
        var prefab = NewMonsterPrefab();
        var spawner = NewSpawner(prefab);
        spawner.maxSpawnPerWave = 1;
        spawner.eliteChanceOverride = 1f;

        var oni = ScriptableObject.CreateInstance<EnemyData>();
        oni.type = EnemyType.Grunt;
        oni.maxHp = OniBaseHp;
        oni.attackPower = OniBaseDmg;
        oni.moveSpeed = 0.76f;
        oni.colliderRadius = 0.5f;
        spawner.Catalog = CatalogOf(oni);

        InvokeSpawnWave(spawner);
        yield return null;

        var enemy = GetAlive(spawner)[0];
        Assert.IsNotNull(enemy.GetComponent<EnemyElite>(), "엘리트 표식이 안 붙었다");

        var health = enemy.GetComponent<EnemyHealth>();
        var move = enemy.GetComponent<EnemyMove>();
        Assert.AreEqual(OniBaseHp * DifficultyScalingConfig.EliteHpMult, health.MaxHp, 0.01f);
        Assert.AreEqual(OniBaseDmg * DifficultyScalingConfig.EliteDmgMult, move.attackPower, 0.01f);
        Assert.AreEqual(0.5f * DifficultyScalingConfig.EliteScale, move.WorldRadius, 0.01f);

        Object.DestroyImmediate(oni);
    }

    /// <summary>
    /// 엘리트 보상 ×5, 그리고 **골드는 확률을 무시하고 항상 드랍**한다
    /// (원본 `if (Math.random() &lt; goldDropChance || e.boss || e.elite)`, :1818).
    /// </summary>
    [UnityTest]
    public IEnumerator Elite_AlwaysDropsGold_AndMultipliesReward()
    {
        var attacker = new GameObject("TestAttacker");
        var prefab = NewMonsterPrefab();
        var spawner = NewSpawner(prefab);
        spawner.maxSpawnPerWave = 1;
        spawner.maxAliveTotal = 1;
        spawner.eliteChanceOverride = 1f;

        var oni = ScriptableObject.CreateInstance<EnemyData>();
        oni.type = EnemyType.Grunt;
        oni.maxHp = OniBaseHp;
        oni.attackPower = OniBaseDmg;
        oni.exp = OniBaseExp;
        oni.goldMin = 5;
        oni.goldMax = 10;
        oni.colliderRadius = 0.5f;
        spawner.Catalog = CatalogOf(oni);

        // 한 마리로는 "항상 드랍"과 "75% 확률로 마침 드랍됨"을 구분할 수 없다 — 여러 마리로 본다.
        const int kills = 12;
        for (int i = 0; i < kills; i++)
        {
            InvokeSpawnWave(spawner);
            yield return null;
            var enemyGO = GetAlive(spawner)[GetAlive(spawner).Count - 1].gameObject;
            RemoveSpawnProtection(enemyGO);
            var h = enemyGO.GetComponent<EnemyHealth>();
            h.TakeDamage(h.MaxHp * 10f, attacker);
            yield return null;
        }

        int expectedExp = Mathf.RoundToInt(OniBaseExp * DifficultyScalingConfig.EliteRewardMult) * kills;
        Assert.AreEqual(expectedExp, ProfileService.Current.exp, "엘리트 EXP가 ×5가 아니다");
        // 최소 굴림(5)×5배×12마리 = 300. 확률 드랍이었다면 12번 전부 나올 확률은 0.75^12 ≈ 3%.
        Assert.GreaterOrEqual(ProfileService.Current.gold, 5 * 5 * kills,
            "엘리트는 골드 드랍 확률을 무시하고 매번 떨궈야 한다(원본 :1818)");

        Object.DestroyImmediate(oni);
    }
}
}
