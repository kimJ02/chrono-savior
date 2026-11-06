using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Systems
{
/// <summary>
/// **데이터로 몹 한 마리를 완성한다** — 세운 직후 한 번(2026-11-06 정리 순서 5-1, 예전엔 `EnemySpawner` 안에 있었다).
/// 종류별 기본 스탯(`EnemyData`) × 지역 배율(`DifficultyScalingConfig`) × 회귀 장벽, 엘리트 승격, 레벨, 그림·색, 그림 크기와 판정 크기,
/// 종류별 행동 값까지. 보스·성소도 여기서 스탯과 크기를 받는다.
///
/// 역할 나눔: **언제·어디·무엇을 세울지**와 처치 이벤트 연결·몹↔데이터 기억은 <see cref="EnemySpawner"/>,
/// **죽은 뒤 무엇을 주는지**는 처치 보상 쪽. 여기는 상태를 갖지 않는다 — 필요한 값(데이터·확률·도형 그림·목록)은 부르는 쪽이 넘긴다.
/// 난수 호출 순서는 옮기기 전과 같다(엘리트 → 레벨 → 이동속도) — 같은 시드면 같은 몹이 나온다.
/// </summary>
public static class EnemySetup
{
    /// <summary>
    /// 잡몹(꼬마 슬라임 포함)을 완성한다. 원본도 같은 두 층 구조다 — `CONFIG.enemyBase[type]`에 지역 배율을 곱한다(`:3958`).
    /// `EnemyData`가 없으면 프리팹에 들어 있는 값을 그대로 쓴다(스폰 자체는 계속 되게).
    /// </summary>
    /// <param name="allowElite">꼬마 슬라임은 승격되지 않는다(원본 `noElite: true`, project_test.html:1842).</param>
    /// <param name="eliteChance">엘리트 기본 확률(아이템 '몬스터 유인향'은 여기서 곱한다).</param>
    /// <param name="shapeSprite">정식 그림이 없을 때 꽂을 종류별 임시 도형(데이터가 없으면 쓰지 않는다).</param>
    public static void Apply(GameObject monster, EnemyData data, bool allowElite, float eliteChance, Sprite shapeSprite)
    {
        int regionLv = RunState.Region;

        // 원본 `elite = !opts.noElite && Math.random() < CONFIG.elite.chance`(project_test.html:3948).
        // 엘리트는 별도 종류가 아니라 **아무 몹에게나 붙는 승격**이라, 종류를 정한 뒤에 굴린다.
        // 몬스터 유인향 — 엘리트 출현률을 올린다(원본 `chance * (1 + itemPow('eliteLure'))` :3948).
        bool elite = allowElite &&
                     Random.value < eliteChance * (1f + ProfileService.Current.items.Pow("eliteLure"));
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
                    if (shapeSprite != null) sr.sprite = shapeSprite;
                    sr.color = data.color;
                }
            }
            ConfigureTypeBehaviour(monster, data, sr);
        }

        // ⚠️ 스프라이트를 꽂은 **뒤에** 크기를 정한다 — 그림 높이를 보고 스케일을 계산하기 때문.
        ApplySize(monster, data, elite);
    }

    /// <summary>
    /// 성소를 완성한다 — 원본 `spawnShrine()`(project_test.html:3996). 레벨은 지역 기준 ±1(원본 `enemyLv()`),
    /// 스폰 보호는 1초(원본 `spawnInvuln: 1.0` :4008 — 몹의 2초와 다르다).
    /// 움직이지 않는 구조물 설정(원본 `if (e.shrine) continue;` :4024 — Kinematic·중력 0·isStructure 플래그)은 프리팹(Enemy_Shrine)에 있다.
    /// </summary>
    public static void SetupShrine(GameObject go, MonsterCatalog catalog)
    {
        var move = go.GetComponent<EnemyMove>();
        if (move != null) move.SetSpawnProtection(Shrine.SpawnProtect);

        int level = Mathf.Clamp(RegionConfig.RecommendedLevel(RunState.Region) + Random.Range(0, 2), 1, 40);
        var health = go.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.SetMaxHp(Shrine.HpForLevel(level));
            health.SetLevel(level);
        }

        // 원본 w:64 h:96(`:698`) — 고블린(42×46)의 두 배 높이인 세로로 긴 구조물이다.
        var sr = go.GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(1f, 0.54f, 0.42f); // 원본 colors.soul '#ff8a6a'
        FitToWorldSize(go, catalog.shrine, catalog.shrineSize, Mathf.Min(EntitySizeConfig.ShrineWidth, EntitySizeConfig.ShrineHeight) / 2f);
    }

    /// <summary>
    /// 보스를 완성한다 — 원본 `spawnBoss()`(project_test.html:4155). 잡몹과 달리 **지역 배율을 따로 쓴다**(`hpBase 800 × 2.15^(r-1)`) —
    /// `EnemyData`를 안 거친다. 보스 부품(이동·공격 패턴)과 보스 표시(`EnemyHealth.isBoss`)는 프리팹(Enemy_Boss)에 있다.
    /// </summary>
    public static void SetupBoss(GameObject go, int region, MonsterCatalog catalog)
    {
        var health = go.GetComponent<EnemyHealth>();
        if (health != null)
        {
            // 보스도 회귀 장벽을 받는다(원본 `spawnBoss`의 `rebirthWallHpMult(r)` :4159).
            health.SetMaxHp(Boss.HpForRegion(region) * RegressionConfig.WallEnemyHp(region, ProfileService.Current.regressions));
            health.SetLevel(Boss.LevelForRegion(region));
            health.knockbackMultiplier = Boss.KnockbackMultiplier; // 거의 안 밀린다
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
        // 보스 탄은 탄 프리팹의 그림(기본 몹 그림)을 쓴다 — 보스 그림을 그대로 쓰면 화면을 가리는 거대한 탄이 날아간다(Enemy_Boss의 Boss.boltPrefab).
        FitToWorldSize(go, catalog.boss, catalog.bossSize, Mathf.Min(EntitySizeConfig.BossWidth, EntitySizeConfig.BossHeight) / 2f);
    }

    /// <summary>
    /// 종류별 **그림 크기**(`EnemyData.visualHeight`)와 **판정 크기**(`EnemyData.colliderRadius`)를 각각 적용한다 — 엘리트는 둘 다 확대
    /// (원본 `w: base.w * sizeMul`, `:3954`). 둘은 따로 정한다(2026-11-06 3.5-d): 그림을 바꿀 땐 그림 높이만, 판정은 게임 규칙이라 그림과 상관없다.
    /// 지금 임시 그림은 둘이 같다(그림 높이 = 판정 지름 — 예전 '보이는 것보다 넓게 때리는 오우거' 문제를 막으려고 맞춰 둔 값, `BuildEnemyData`).
    ///
    /// 스폰 지점 Y는 프리팹 반지름(고블린 0.5) 기준으로 이미 계산돼 있으므로, 판정이 커진 만큼 들어올려
    /// 발을 지면/발판에 맞춘다. 안 그러면 큰 몹이 지면에 파묻힌 채 물리에 밀려 튀어오른다.
    /// </summary>
    static void ApplySize(GameObject monster, EnemyData data, bool elite)
    {
        var col = monster.GetComponent<CircleCollider2D>();
        if (col == null) return;

        float prefabWorldRadius = col.radius * monster.transform.localScale.x; // 고블린 기준 0.5
        if (prefabWorldRadius <= 0f) return;

        float eliteMul = elite ? DifficultyScalingConfig.EliteScale : 1f;
        float wantRadius = (data != null ? data.colliderRadius : prefabWorldRadius) * eliteMul;       // 판정
        float wantHeight = (data != null && data.visualHeight > 0f ? data.visualHeight : wantRadius / eliteMul * 2f) * eliteMul;  // 그림(비어 있으면 판정 지름)

        // ── 콜라이더와 스프라이트가 **같은 GameObject**에 있어서 `localScale`이 둘 다에 걸린다 ──
        // 스케일 하나로 그림 높이를 맞추고, 콜라이더는 `radius`를 역으로 나눠 판정 반지름을 맞춘다:
        //   월드 반지름 = radius × scale  →  radius = 원하는반지름 / scale
        // **렌더러에 실제로 꽂혀 있는 스프라이트**의 높이로 계산한다. 예전엔 `data.sprite`만 봤는데,
        // 그러면 도형 스프라이트로 갈아끼운 경우(지금 기본 경로)에 이 분기를 안 타서 프리팹 내장
        // 스프라이트 크기를 전제로 스케일이 잡혔다 — 덩치가 조용히 틀어지는 종류의 버그다.
        float scale = 1f;
        var sr = monster.GetComponent<SpriteRenderer>();
        float spriteHeight = sr != null && sr.sprite != null ? sr.sprite.bounds.size.y : 0f;
        if (spriteHeight > 0.0001f) scale = wantHeight / spriteHeight;
        else scale = wantRadius / prefabWorldRadius; // 스프라이트가 없으면 콜라이더 기준으로

        monster.transform.localScale = new Vector3(scale, scale, 1f);
        col.radius = wantRadius / scale;

        // 스폰 지점은 프리팹 반지름 기준으로 계산돼 있다 — 커진 만큼 들어올려 발을 지면에 맞춘다.
        // 안 그러면 큰 몹이 지면에 파묻힌 채 물리에 밀려 튀어오른다.
        monster.transform.position += new Vector3(0f, wantRadius - prefabWorldRadius, 0f);
    }

    /// <summary>
    /// 종류별 행동의 **데이터 값**을 넣는다 — 행동 부품 자체(원본 `updateEnemies()`의 타입 분기 :4042~4111, `killEnemy()`의 분열 분기 :1840)는
    /// 종류별 프리팹에 들어 있다(2026-11-05 생성 책임 2-2). 고블린·오우거·꼬마 슬라임은 기본 보행이라 넣을 게 없다
    /// (CLAUDE.md "SO는 수치만, 행동이 다르면 스크립트 분리").
    /// </summary>
    static void ConfigureTypeBehaviour(GameObject monster, EnemyData data, SpriteRenderer sr)
    {
        // 원본은 박쥐만 스폰 지점보다 50px 위에서 나온다(`y: type === 'wisp' ? y - 50 : y`,
        // project_test.html:3955 — 원본은 Y+가 아래라 부호가 뒤집힌다).
        if (data.type == EnemyType.Flyer) monster.transform.position += new Vector3(0f, 0.5f, 0f);

        // 화염탄은 런타임에 새 오브젝트로 생성된다 — 몹이 쓰는 스프라이트·종류색을 그대로 물려준다.
        if (monster.TryGetComponent(out EnemyShooterMotion shooter))
        {
            if (sr != null) shooter.boltSprite = sr.sprite;
            shooter.boltColor = data.color;
        }
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
    /// 월드 반지름을 따로 맞춘다(<see cref="ApplySize"/>가 잡몹에게 하는 것과 같은 계산).
    /// **그림 크기(<paramref name="visualSize"/>, 몬스터 목록)와 판정 반지름(<paramref name="hitRadius"/>, `EntitySizeConfig`)은 따로 받는다**(2026-11-06 3.5-d).
    /// 원형 판정은 원본 크기의 가로/세로 중 **작은 쪽**에 맞춘다 — 큰 쪽에 맞추면 그림 밖까지 맞는다.
    /// </summary>
    static void FitToWorldSize(GameObject go, Sprite sprite, Vector2 visualSize, float hitRadius)
    {
        float worldWidth = visualSize.x, worldHeight = visualSize.y;
        var sr = go.GetComponent<SpriteRenderer>();

        // 그림이 있으면 그림 크기를 기준으로, 없으면 프리팹의 기본 스프라이트를 기준으로 늘린다.
        Sprite basis = sprite != null ? sprite : (sr != null ? sr.sprite : null);
        Vector2 size = basis != null ? (Vector2)basis.bounds.size : Vector2.one;
        if (size.x < 0.0001f || size.y < 0.0001f) return;

        var scale = new Vector3(worldWidth / size.x, worldHeight / size.y, 1f);
        go.transform.localScale = scale;

        var col = go.GetComponent<CircleCollider2D>();
        if (col != null && Mathf.Abs(scale.x) > 0.0001f)
            col.radius = hitRadius / scale.x;

        if (sprite != null && sr != null)
        {
            sr.sprite = sprite;
            sr.color = Color.white; // 그림에 색이 이미 칠해져 있다 — 또 곱하면 그 색으로 물든다
        }
    }
}
}
