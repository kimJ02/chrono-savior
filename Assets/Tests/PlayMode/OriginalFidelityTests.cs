using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 원본 대조에서 **뒤늦게 발견한 편차**들의 재발 방지 — 전부 "컴파일도 되고 테스트도 통과하는데
/// 원본과 조용히 다르게 동작하던" 것들이다.
///
/// 이 파일이 따로 있는 이유는 이 편차들이 한 가지 공통 원인을 갖기 때문이다:
/// **값은 정의해 놓고 호출부를 안 만든 것.** 상수도 함수도 멀쩡히 있으니 컴파일러는 아무 말도 안 하고,
/// 그 값을 직접 검사하는 단위 테스트도 통과한다 — 아무도 그걸 "쓰는지"는 안 봤기 때문이다.
/// 그래서 여기엔 개별 회귀 테스트와 함께 <see cref="DefinedApis_AreActuallyCalledSomewhere"/>
/// (정의만 있고 호출부가 없는 API를 소스에서 직접 찾는 검사)를 같이 둔다.
/// </summary>
public class OriginalFidelityTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test") || go.name == "Shrine"
                               || go.name == "Boss" || go.name == "ExpOrb"))
                Object.DestroyImmediate(go);
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunState.Reset();
        RunTransient.Reset();
        EnemySpawnRequestBus.Reset();
        ProfileService.Reset();
    }

    // ═══════════════════════ 1. 보스 레벨 페널티 하한 ═══════════════════════

    /// <summary>
    /// 원본 `levelFactor`(project_test.html:1651)는 **보스에게만 하한 0.5**를 준다(잡몹은 0.25).
    /// 배수 함수엔 그 분기가 있었지만 <see cref="EnemyHealth"/>가 `isBoss`를 안 넘겨서, 저레벨로
    /// 보스에 가면 원본의 두 배로 단단했다 — 함수는 맞고 **호출이 틀린** 전형적인 경우다.
    /// </summary>
    [UnityTest]
    public IEnumerator BossTakesDamage_WithHigherLevelPenaltyFloor()
    {
        ProfileService.Current.level = 1;

        var normal = NewDummyEnemy(isBoss: false);
        var boss = NewDummyEnemy(isBoss: true);
        yield return null;

        const float raw = 1000f;
        normal.TakeDamage(raw, null);
        boss.TakeDamage(raw, null);

        float normalTaken = normal.MaxHp - normal.CurrentHp;
        float bossTaken = boss.MaxHp - boss.CurrentHp;

        Assert.AreEqual(raw * 0.25f, normalTaken, 1f, "잡몹 하한이 25%가 아니다");
        Assert.AreEqual(raw * 0.5f, bossTaken, 1f, "보스 하한이 50%가 아니다 — isBoss가 안 넘어갔다");
    }

    static EnemyHealth NewDummyEnemy(bool isBoss)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var move = go.AddComponent<EnemyMove>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
                         .SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;

        var health = go.AddComponent<EnemyHealth>();
        health.isBoss = isBoss;
        health.SetMaxHp(999999f);
        health.SetLevel(40); // 내 레벨 1 → 39레벨 차 → 하한에 걸린다
        return health;
    }

    // ═══════════════════════ 2. 경험치 구슬의 경험치 배수 ═══════════════════════

    /// <summary>
    /// 원본 `:4466`은 구슬에도 `expMultAll()`을 곱한다. 빠져 있으면 '수행의 굴레'를 아무리 모아도
    /// **구슬로 얻는 경험치만 그대로**라, 아이템이 반쪽만 작동하는 상태가 된다.
    /// </summary>
    [UnityTest]
    public IEnumerator ExpOrb_AppliesExpMultiplier()
    {
        ProfileService.Current.level = 5;
        RunState.Begin(1, RunMode.Normal);

        int plain = Mathf.Max(ExpOrb.MinExp,
            Mathf.RoundToInt(PlayerProfile.RequiredExp(5) * ExpOrb.ExpPercent));

        ProfileService.Current.items.Add("exp"); // 수행의 굴레(곱연산)
        float mult = CombatModifiers.ExpMultiplier;
        Assert.Greater(mult, 1f, "경험치 아이템이 배수에 안 붙는다(전제 조건)");

        yield return CollectOneOrb();

        long gained = RunState.ExpEarned;
        Assert.AreEqual(Mathf.RoundToInt(plain * mult), gained, 1,
            "구슬 경험치에 배수가 안 곱해졌다");
    }

    static IEnumerator CollectOneOrb()
    {
        var player = new GameObject("TestPlayer") { tag = "Player" };
        player.transform.position = new Vector3(5f, FieldBounds.GroundY - ExpOrb.PlayerCenterOffset, 0f);
        ExpOrb.Spawn(TestPrefabs.ExpOrb, new Vector3(5f, FieldBounds.GroundY, 0f), player.transform);
        yield return null;
        yield return null;
    }

    // ═══════════════════════ 3. 성소 ═══════════════════════

    /// <summary>
    /// 원본은 적 버프를 매 프레임 `enemies.some(e => ... && e.spawnInvuln <= 0)`로 다시 계산한다(`:4414`).
    /// **아직 손댈 수 없는 성소가 필드 전체를 먼저 강화하면 안 된다** — 세울 때 플래그를 한 번
    /// 켜는 방식이면 스폰 보호 1초 동안 그런 상태가 된다.
    /// </summary>
    [UnityTest]
    public IEnumerator Shrine_DoesNotBuffEnemiesWhileSpawnProtected()
    {
        var go = NewShrineObject(spawnProtect: 5f);
        yield return null;

        Assert.AreEqual(1f, CombatModifiers.EnemyDamageMultiplier, 1e-4f,
            "스폰 보호 중인데 적이 벌써 강해졌다");

        go.GetComponent<EnemyMove>().SetSpawnProtection(0f);
        yield return null;

        Assert.Greater(CombatModifiers.EnemyDamageMultiplier, 1f,
            "보호가 풀렸는데 적 버프가 안 걸린다");
    }

    static GameObject NewShrineObject(float spawnProtect)
    {
        var go = new GameObject("TestShrine") { tag = "Enemy" };
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var move = go.AddComponent<EnemyMove>();
        move.isStructure = true;
        move.SetSpawnProtection(spawnProtect);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<EnemyHealth>();
        go.AddComponent<Shrine>();
        return go;
    }

    /// <summary>
    /// 성소 타이머는 원본에서 **일반 사냥에서만** 돈다(`if (run.mode === 'normal')`, `:4430`).
    /// 보스전에도 돌면 좁은 보스 무대에 부술 수 없는 구조물이 서서 피할 자리가 사라진다.
    /// </summary>
    [Test]
    public void ShrineTimer_DoesNotRunInBossMode()
    {
        var spawner = NewSpawner();
        RunState.Begin(1, RunMode.Boss);
        SetShrineTimer(spawner, 0.001f);

        InvokeUpdateShrine(spawner, 1f);

        Assert.AreEqual(0.001f, GetShrineTimer(spawner), 1e-5f, "보스전인데 성소 타이머가 돌았다");
        Assert.IsNull(GetAliveShrine(spawner), "보스전에서 성소가 나왔다");
    }

    /// <summary>
    /// 원본은 성소가 살아 있어도 타이머를 **계속 깎는다**(`:4437`) — 방어는 `spawnShrine()` 안에 있다(`:3997`).
    /// 살아 있는 동안 타이머를 멈춰 세우면 "부수고 나서 35초를 새로 기다린다"가 되어, 성소가
    /// 원본보다 훨씬 드물게 나온다.
    /// </summary>
    [Test]
    public void ShrineTimer_KeepsTickingWhileOneIsAlive()
    {
        var spawner = NewSpawner();
        RunState.Begin(1, RunMode.Normal);
        SetShrineTimer(spawner, 5f);
        SetAliveShrine(spawner, new GameObject("TestFakeShrine").transform);

        InvokeUpdateShrine(spawner, 1f);

        Assert.AreEqual(4f, GetShrineTimer(spawner), 1e-4f,
            "성소가 살아 있다고 타이머까지 멈췄다");
    }

    // ═══════════════════════ 4. 웨이브 상한 ═══════════════════════

    /// <summary>
    /// 원본 웨이브 상한은 `enemies.filter(e => !e.boss && !e.shrine)`로 센다(`:3978`) — **보스와 성소는
    /// 자리를 차지하지 않는다.** 이 둘을 목록에 넣으면 특히 보스전(상한 4)에서 잡몹이 조용히
    /// 한두 마리씩 덜 나온다. 눈에 안 띄는 종류의 편차라 테스트로 고정한다.
    /// </summary>
    [UnityTest]
    public IEnumerator BossAndShrine_DoNotCountTowardWaveCap()
    {
        var spawner = NewSpawner();
        RunState.Begin(1, RunMode.Boss);

        typeof(EnemySpawner).GetMethod("SpawnBoss", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(spawner, null);
        typeof(EnemySpawner).GetMethod("SpawnShrine", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(spawner, null);
        yield return null;

        var alive = (IList)typeof(EnemySpawner)
            .GetField("aliveMonsters", BindingFlags.NonPublic | BindingFlags.Instance)
            .GetValue(spawner);

        Assert.AreEqual(0, alive.Count,
            "보스/성소가 웨이브 상한을 잡아먹고 있다");
    }

    static EnemySpawner NewSpawner()
    {
        var prefab = new GameObject("TestMonsterPrefab") { tag = "Enemy" };
        prefab.AddComponent<SpriteRenderer>();
        prefab.AddComponent<CircleCollider2D>().radius = 0.5f;
        prefab.AddComponent<EnemyMove>();
        prefab.AddComponent<EnemyHealth>();

        // 보스·성소는 몬스터 목록의 프리팹으로 세운다(2026-11-05 생성 책임 2-2) — 게임의 Enemy_Boss·Enemy_Shrine처럼 부품을 넣은 꺼진 원본.
        var catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
        catalog.types = new EnemyData[0];
        catalog.bossPrefab = TestEnemyPrefab(prefab, "TestBossPrefab", go => { go.AddComponent<Boss>().Equipped(); go.GetComponent<EnemyHealth>().isBoss = true; });
        catalog.shrinePrefab = TestEnemyPrefab(prefab, "TestShrinePrefab", go => { go.AddComponent<Shrine>(); go.GetComponent<EnemyMove>().isStructure = true; });

        var go = new GameObject("TestSpawner");
        var spawner = go.AddComponent<EnemySpawner>();
        spawner.monsterPrefab = prefab;
        spawner.Catalog = catalog;
        spawner.eliteChanceOverride = 0f;
        // `waveTimer`가 0에서 시작하므로 켜 두면 첫 프레임에 웨이브가 나간다 — 여기 테스트들은
        // 전부 private 메서드를 직접 부르므로 자동 웨이브는 잡음일 뿐이다.
        spawner.enabled = false;
        return spawner;
    }

    /// <summary>
    /// 기본 몹 원본을 복제해 부품을 더한 원본(테스트용 '프리팹 변형') — 이걸 Instantiate한 복제본은 부품이 다 있는 채로 깨어난다(Awake가 알아본다).
    /// 원본 자체는 기본 몹 원본처럼 켜진 채 둔다(꺼 두면 복제본도 꺼진 채 나온다).
    /// </summary>
    static GameObject TestEnemyPrefab(GameObject basePrefab, string name, System.Action<GameObject> addParts)
    {
        var go = Object.Instantiate(basePrefab);
        go.name = name;
        addParts(go);
        return go;
    }

    static void InvokeUpdateShrine(EnemySpawner s, float dt) =>
        typeof(EnemySpawner).GetMethod("UpdateShrine", BindingFlags.NonPublic | BindingFlags.Instance)
                            .Invoke(s, new object[] { dt });

    static FieldInfo ShrineTimerField =>
        typeof(EnemySpawner).GetField("shrineTimer", BindingFlags.NonPublic | BindingFlags.Instance);
    static FieldInfo AliveShrineField =>
        typeof(EnemySpawner).GetField("aliveShrine", BindingFlags.NonPublic | BindingFlags.Instance);

    static void SetShrineTimer(EnemySpawner s, float v) => ShrineTimerField.SetValue(s, v);
    static float GetShrineTimer(EnemySpawner s) => (float)ShrineTimerField.GetValue(s);
    static void SetAliveShrine(EnemySpawner s, Transform t) => AliveShrineField.SetValue(s, t);
    static Transform GetAliveShrine(EnemySpawner s) => (Transform)AliveShrineField.GetValue(s);

    // ═══════════════════════ 5. 덩치 ═══════════════════════

    /// <summary>
    /// 덩치는 **서로의 비율**이 맞아야 의미가 있다. 예전엔 성소를 손으로 고른 `localScale`로
    /// 세웠는데, 프리팹 기본 스프라이트가 32×32짜리 내장 이미지라 실제로는 0.42×0.61유닛밖에
    /// 안 됐다 — 고블린보다 작은 성소가 나왔고, **눈으로 보기 전엔 아무도 몰랐다.**
    ///
    /// 원본 비율(`CONFIG`의 w/h ÷ 100)을 기준으로 못 박는다:
    /// 고블린 0.46 &lt; 성소 0.96 &lt; 보스 1.50.
    /// </summary>
    [Test]
    public void EntitySizes_KeepOriginalProportions()
    {
        float oniHeight = EntitySizeConfig.OniRadius * 2f;

        Assert.Greater(EntitySizeConfig.ShrineHeight, oniHeight,
            "성소가 고블린보다 작다 — 원본은 두 배 높이다(64×96 vs 42×46)");
        Assert.Greater(EntitySizeConfig.BossHeight, EntitySizeConfig.ShrineHeight,
            "보스가 성소보다 작다");

        // 원본 그대로여야 하는 것들(`:698`·`:721`) — 여기가 틀어지면 무대 대비 덩치가 통째로 달라진다.
        Assert.AreEqual(0.96f, EntitySizeConfig.ShrineHeight, 1e-4f);
        Assert.AreEqual(1.50f, EntitySizeConfig.BossHeight, 1e-4f);
        // 고블린은 원본 46px의 절반 언저리 — 정확히 0.46은 아니고 0.50이다(반지름을 0.25로 딱 떨어지게 잡음).
        Assert.AreEqual(0.50f, oniHeight, 1e-4f);
    }

    /// <summary>
    /// **플레이어는 발판 사이를 지나갈 수 있어야 한다.**
    ///
    /// 2026-09-16에 플레이어 높이를 1.5로 올렸다가, 발판 층간 간격이 1.35라 머리가 윗 발판
    /// 콜라이더를 뚫었다. 그러면 ①위아래로 끼어 "벽에 막히는" 느낌이 나고 ②마법탄 총구가
    /// 발판 안에서 생겨 `MageProjectile`이 Ground에 닿은 것으로 보고 **쏘자마자 터진다.**
    ///
    /// 숫자만 보면 멀쩡해서 **플레이해보기 전엔 아무도 모르는** 종류라 테스트로 고정한다.
    /// </summary>
    [Test]
    public void PlayerFitsBetweenPlatformFloors()
    {
        // 층 높이가 1.35 간격으로 쌓여 있다(1.35 / 2.70 / 4.05 / 5.40).
        float gap = FieldLayout.NormalPlatforms[4, 1] - FieldLayout.NormalPlatforms[0, 1];
        Assert.Greater(gap, 0f, "발판 층 간격을 못 읽었다");

        Assert.Less(EntitySizeConfig.PlayerHeight, gap,
            $"플레이어 높이({EntitySizeConfig.PlayerHeight})가 발판 층간 간격({gap})보다 크다 — " +
            "발판 위에 서면 머리가 윗 발판을 뚫고, 공격이 발판 안에서 생겨 즉시 터진다");

        Assert.LessOrEqual(EntitySizeConfig.PlayerHeight, EntitySizeConfig.PlayerHeightMax,
            "여유를 둔 실질 상한을 넘었다");
    }

    // ═══════════════════════ 6. 쿨감 아이템 ═══════════════════════

    /// <summary>
    /// '시간의 조각'은 원본에서 기본공격·궁극기 쿨타임 **전부**에 곱해진다(`:1942`·`:2177`·`:2196`).
    /// 계산 함수만 있고 아무 데도 안 곱하던 상태여서, 아이템을 상한까지 모아도 체감이 0이었다.
    /// </summary>
    [UnityTest]
    public IEnumerator CooldownItem_ShortensGunnerCooldown()
    {
        var go = new GameObject("TestGunner");
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        var gunner = go.AddComponent<GunnerAttack>().Equipped();
        yield return null;

        float before = gunner.cooldown;

        for (int i = 0; i < 4; i++) ProfileService.Current.items.Add("cd");
        yield return null;

        Assert.Less(gunner.cooldown, before - 1e-4f, "쿨감 아이템이 쿨타임에 안 붙는다");
        Assert.AreEqual(before * PlayerStatCalculator.ComputeCooldownMultiplier(ProfileService.Current),
                        gunner.cooldown, 1e-4f);
    }

    // ═══════════════════════ 7. "정의만 하고 안 쓰는" 것 자체를 막는다 ═══════════════════════

    /// <summary>
    /// 오늘 잡은 편차 중 절반이 같은 모양이었다 — **상수/함수는 만들었는데 호출부가 없다.**
    /// 컴파일러는 그걸 오류로 안 보고, 그 값을 직접 확인하는 단위 테스트도 멀쩡히 통과한다.
    /// (살기 공속, 쿨감 아이템, 성소 스폰 보호, 보스 레벨 하한이 전부 이 경우였다.)
    ///
    /// 그래서 "이건 반드시 어딘가에서 쓰여야 한다"는 API 목록을 두고 **소스에서 직접** 호출부를 찾는다.
    /// 리플렉션으로는 못 잡는다 — 안 불리는 메서드도 타입에는 멀쩡히 존재하기 때문이다.
    ///
    /// 새 API를 목록에 넣는 기준: **정의와 사용처가 멀리 떨어져 있어서, 빠뜨려도 조용히 넘어가는 것.**
    /// (자기 파일 안에서만 쓰는 헬퍼는 넣지 않는다 — 안 쓰면 컴파일러가 경고한다.)
    /// </summary>
    [Test]
    public void DefinedApis_AreActuallyCalledSomewhere()
    {
        // (찾을 이름, 정의된 파일 — 이 파일 안의 사용은 세지 않는다)
        // 그래서 **정의와 사용처가 같은 파일인 것은 넣지 않는다**(예: EnemySplitOnDeath의
        // SplitSpawnProtect). 그건 안 쓰면 컴파일러가 알려주므로 이 검사가 필요 없다.
        var mustBeWired = new (string api, string declaredIn)[]
        {
            ("ComputeCooldownMultiplier",  "PlayerStatCalculator.cs"),
            ("ComputeDamageTakenMultiplier","PlayerStatCalculator.cs"),
            ("ComputeAttackSpeedMultiplier","PlayerStatCalculator.cs"),
            ("ExpMultiplier",              "CombatModifiers.cs"),
            ("GoldMultiplier",             "CombatModifiers.cs"),
            ("LevelFactor",                "CombatModifiers.cs"),
            ("EnemyMoveSpeedMultiplier",   "CombatModifiers.cs"),
            ("EnemyDamageMultiplier",      "CombatModifiers.cs"),
            ("SpawnProtect",               "Shrine.cs"),
            ("DefaultSpawnProtect",        "EnemySpawnRequestBus.cs"),
            ("WallPlayerDamage",           "RegressionConfig.cs"),
            ("StartNewExpedition",         "GameFlow.cs"),   // 타이틀 "새 원정"(TitleMenu)이 부른다 — 2026-11-05
            // 상태 소유권(2026-11-06 4-3·4-4) — 바꾸는 길을 하나로 모은 메서드
            ("SetMaxX",                    "FieldBounds.cs"),      // FieldLayout.SetBossArena
            ("SetSelectedCharacter",       "PlayerProfile.cs"),    // 캐릭터 선택 화면·PlayerRig
            ("RecordBossKill",             "PlayerProfile.cs"),    // EnemySpawner(보스 격파)
            ("RecordShrineBroken",         "PlayerProfile.cs"),    // Shrine
            ("RecordEnemyKill",            "PlayerProfile.cs"),    // EnemySpawner
            ("RecordCombo",                "PlayerProfile.cs"),    // CombatModifiers.AddCombo
            ("NoteCombo",                  "RunState.cs"),         // CombatModifiers.AddCombo
            ("TryUnlockAchievement",       "PlayerProfile.cs"),    // Achievements.CheckNew
            // 책임별 분리(2026-11-06 5단계) — 스포너가 부르는 몹 설정·보상
            ("EnemySetup.Apply",           "EnemySetup.cs"),       // EnemySpawner.ApplyEnemyData(이름이 흔해 클래스까지 적는다)
            ("SetupShrine",                "EnemySetup.cs"),       // EnemySpawner.SpawnShrine
            ("SetupBoss",                  "EnemySetup.cs"),       // EnemySpawner.SpawnBoss
            ("GrantKill",                  "EnemyRewards.cs"),     // EnemySpawner.HandleEnemyDied
            ("GrantBossKill",              "EnemyRewards.cs"),     // EnemySpawner.HandleBossDied
            ("EnemySpawnPlacement.TryPick", "EnemySpawnPlacement.cs"), // EnemySpawner.SpawnWaveOf
            ("TryPayForPull",              "PlayerProfile.cs"),    // GachaService
            ("RecordPull",                 "ItemInventory.cs"),    // GachaService
        };

        var scriptFiles = Directory.GetFiles(
            Path.Combine(Application.dataPath, "Scripts"), "*.cs", SearchOption.AllDirectories);

        var unwired = new List<string>();
        foreach (var (api, declaredIn) in mustBeWired)
        {
            bool used = false;
            foreach (var file in scriptFiles)
            {
                if (Path.GetFileName(file) == declaredIn) continue;
                if (File.ReadAllText(file).Contains(api)) { used = true; break; }
            }
            if (!used) unwired.Add($"{declaredIn}의 {api} — 정의만 있고 부르는 곳이 없다");
        }

        Assert.IsEmpty(unwired, string.Join("\n", unwired));
    }
}

}
