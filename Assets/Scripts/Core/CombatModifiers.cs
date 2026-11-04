using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 전투 배수 체인 — 원본 `dmgMultAll()`(project_test.html:1296) · `goldMultAll()`(`:1304`) ·
    /// `levelFactor()`(`:1651`) · `addCombo()`(`:1621`) · 연쇄 처치(`:1828`)를 한곳에 모은 것.
    ///
    /// 원본 최종 피해식(`dealDamage` `:1664`)은 이렇게 생겼다:
    /// <code>
    /// max(1, round( statAtk × dmgMultAll() × mult × vulnMult × lvF × itemDmgVs
    ///               × rebirthWall × rand(0.9,1.1) × (crit ? critMult : 1) ))
    /// </code>
    /// 이 클래스가 맡는 건 **대상과 무관한 항**(`dmgMultAll`, 골드 배수)과 **레벨 페널티 계산식**이다.
    /// 대상에 달린 항(`vulnMult`, 실제 `lvF` 적용)은 맞는 쪽인 `Enemies/EnemyHealth`가 곱한다 —
    /// `DamageCalculator`가 대상 참조를 갖지 않는 순수 계산기라서 그렇다.
    ///
    /// **아직 없는 항**(해당 시스템이 없어서 전부 ×1로 빠져 있다): `itemMul('dmg')`·`itemDmgVs`(아이템),
    /// `rebirthWallPlayerDmgMult`(회귀 장벽), `lastStand`/`airDmg`(아이템 조건부).
    /// 이것들이 생기면 <see cref="DamageMultiplier"/> 한 곳에 곱하기만 하면 된다.
    ///
    /// ## 왜 정적 클래스인가
    /// 콤보·살기는 "지금 이 런의 전역 상태"라 씬 어디서든 같은 값을 봐야 한다
    /// (`GameState`/`RunState`/`FieldBounds`와 같은 이유). 정적 상태라 **테스트는 반드시
    /// <see cref="Reset"/>을 부른다** — 안 그러면 앞 테스트의 콤보가 뒤 테스트의 피해를 부풀린다.
    /// </summary>
    public static class CombatModifiers
    {
        // ---- 원본 CONFIG 그대로 ----
        /// <summary>콤보 유지 시간. 원본 `CONFIG.combo.window`(project_test.html:742).</summary>
        public const float ComboWindow = 3.0f;
        /// <summary>콤보당 피해 배수. 원본 `CONFIG.comboMult.dmg`(`:703`).</summary>
        public const float ComboDamagePer = 0.012f;
        /// <summary>콤보당 재화 배수. 원본 `CONFIG.comboMult.money`(`:703`).</summary>
        public const float ComboMoneyPer = 0.008f;

        /// <summary>살기(처치당) 피해 배수. 원본 `CONFIG.fury.dmgPer`(`:702`).</summary>
        public const float FuryDamagePer = 0.008f;
        /// <summary>살기(처치당) 공격속도 배수. 원본 `CONFIG.fury.asPer`(`:702`).</summary>
        public const float FuryAttackSpeedPer = 0.004f;

        /// <summary>레벨 1 차이당 피해 감소. 원본 `CONFIG.levelPen.per`(`:729`).</summary>
        public const float LevelPenaltyPer = 0.05f;
        /// <summary>레벨 페널티 하한. 원본 `CONFIG.levelPen.floor`(`:729`).</summary>
        public const float LevelPenaltyFloor = 0.25f;
        /// <summary>보스는 페널티를 절반만 받는다. 원본 `e.boss ? Math.max(0.5, levelFactor(e.lv)) : ...`(`:1662`).</summary>
        public const float BossLevelPenaltyFloor = 0.5f;

        /// <summary>연쇄 처치 판정 시간. 원본 `CONFIG.chain_kill.window`(`:704`).</summary>
        public const float ChainKillWindow = 0.8f;
        /// <summary>연쇄 처치 보너스가 시작되는 처치 수. 원본 `CONFIG.chain_kill.minN`(`:704`).</summary>
        public const int ChainKillMin = 3;

        /// <summary>성소 버프의 플레이어 피해 배수. 원본 `CONFIG.shrine.pDmg`(`:698`).</summary>
        public const float ShrineDamageMult = 1.3f;
        /// <summary>성소 버프의 플레이어 이동속도 배수. 원본 `CONFIG.shrine.pMs`(`:698`).</summary>
        public const float ShrineMoveSpeedMult = 1.25f;

        // ---- 상태 ----

        /// <summary>현재 콤보 수. 원본 `combo.n`.</summary>
        public static int Combo { get; private set; }
        /// <summary>콤보가 끊기기까지 남은 시간. 원본 `combo.t`.</summary>
        public static float ComboTimeLeft { get; private set; }
        /// <summary>이번 런 최고 콤보(결과 화면·업적용). 원본 `meta.stats.maxCombo`(`:1623`).</summary>
        public static int MaxCombo { get; private set; }

        /// <summary>연쇄 처치 카운트. 원본 `run.chainN`.</summary>
        public static int ChainKills { get; private set; }
        static float chainTimeLeft;

        /// <summary>성소를 **부순 뒤** 플레이어가 받는 가호의 남은 시간. 원본 `player.shrineBuffT`(`:1300`).</summary>
        public static float ShrineBuffLeft { get; private set; }

        /// <summary>
        /// **성소가 필드에 살아 있는지** — 원본 `run.shrineActive`(project_test.html:4017).
        ///
        /// ⚠️ 위의 <see cref="ShrineBuffLeft"/>와 **방향이 정반대라 헷갈리기 쉽다**:
        /// 성소가 살아 있는 동안은 **적이** 강해지고(피해 ×1.3, 이속 ×1.25),
        /// 부수면 그 힘이 **플레이어**에게 15초간 넘어온다(피해 ×1.3, 이속 ×1.25).
        /// </summary>
        public static bool ShrineActive { get; private set; }

        /// <summary>성소가 살아 있는 동안 적 피해 배수. 원본 `CONFIG.shrine.eDmg`(`:698`, 적용 `:4148`).</summary>
        public const float ShrineEnemyDamageMult = 1.3f;
        /// <summary>성소가 살아 있는 동안 적 이동속도 배수. 원본 `CONFIG.shrine.eMs`(`:698`, 적용 `:4040`).</summary>
        public const float ShrineEnemyMoveSpeedMult = 1.25f;
        /// <summary>성소 파괴 시 플레이어 가호 지속시간. 원본 `CONFIG.shrine.buffDur`(`:698`).</summary>
        public const float ShrineBuffDuration = 15f;

        /// <summary>적이 자기 피해에 곱할 값. 성소가 없으면 1.</summary>
        public static float EnemyDamageMultiplier => ShrineActive ? ShrineEnemyDamageMult : 1f;
        /// <summary>적이 자기 이동속도에 곱할 값. 성소가 없으면 1.</summary>
        public static float EnemyMoveSpeedMultiplier => ShrineActive ? ShrineEnemyMoveSpeedMult : 1f;

        /// <summary>성소가 나타나거나 부서질 때 필드가 알려 준다.</summary>
        public static void SetShrineActive(bool active) => ShrineActive = active;

        // ---- 배수 ----

        /// <summary>
        /// 원본 `dmgMultAll()`(project_test.html:1296) — **콤보 × 살기 × 성소**.
        /// 아이템 항은 아직 없다(클래스 주석 참고).
        ///
        /// 살기는 <see cref="RunState.Fury"/>를 읽는다 — 처치 집계는 이미 거기서 하고 있어서
        /// 같은 숫자를 두 군데 들고 있지 않으려는 것이다.
        /// </summary>
        public static float DamageMultiplier =>
            // 원본 `1 + combo.n * comboMult.dmg * (1 + itemPow('combo'))`(:1297) — '연격의 부적'은
            // 콤보 **배수 자체**를 키운다(콤보 수가 아니라).
            (1f + Combo * ComboDamagePer * (1f + ProfileService.Current.items.Pow("combo")))
            * (1f + RunState.Fury * FuryDamagePer)
            * ProfileService.Current.items.Mul(ItemStat.Dmg)   // '전생의 투지'
            * (ShrineBuffLeft > 0f ? ShrineDamageMult : 1f)
            * RegressionWallPlayerDamage;

        /// <summary>
        /// 회귀 장벽 — 권장 회귀 횟수에 모자란 지역에서는 **내 피해가 줄어든다**(×0.62^부족분).
        /// 원본 `rebirthWallPlayerDmgMult(run.region)`(project_test.html:977, 적용 `:1664`).
        /// 몹 체력·피해 쪽 장벽은 스폰할 때 `EnemySpawner`가 곱한다.
        /// </summary>
        public static float RegressionWallPlayerDamage =>
            RegressionConfig.WallPlayerDamage(RunState.Region, ProfileService.Current.regressions);

        /// <summary>원본 `goldMultAll()`(project_test.html:1304) — 콤보만(아이템 항은 아직 없다).</summary>
        public static float GoldMultiplier =>
            (1f + Combo * ComboMoneyPer) * ProfileService.Current.items.Mul(ItemStat.Gold);

        /// <summary>원본 `expMultAll() = itemMul('exp')`(project_test.html:1305).</summary>
        public static float ExpMultiplier => ProfileService.Current.items.Mul(ItemStat.Exp);

        /// <summary>
        /// 원본 `statAs()`에 곱해지는 살기 항(`CONFIG.fury.asPer`, `:702`).
        /// 공격 쿨다운을 **나누는** 값이라 커질수록 빨라진다.
        /// </summary>
        public static float AttackSpeedMultiplier => 1f + RunState.Fury * FuryAttackSpeedPer;

        /// <summary>원본 `player.shrineBuffT > 0 ? CONFIG.shrine.pMs : 1`.</summary>
        public static float MoveSpeedMultiplier => ShrineBuffLeft > 0f ? ShrineMoveSpeedMult : 1f;

        /// <summary>
        /// 메이플식 레벨 차이 페널티 — 원본 `levelFactor(mobLv)`(project_test.html:1651).
        /// **내 레벨이 몹보다 높아도 이득은 없다**(상한 1). 낮을 때만 레벨당 5%씩 깎이고 25%가 하한이다.
        /// 이게 "오버강화해도 상위 지역은 여전히 도전"이 되게 만드는 장치라 완화하면 지역 압력이 사라진다.
        /// </summary>
        public static float LevelFactor(int myLevel, int mobLevel, bool isBoss = false)
        {
            if (myLevel >= mobLevel) return 1f;
            float f = Mathf.Max(LevelPenaltyFloor, 1f - (mobLevel - myLevel) * LevelPenaltyPer);
            // 원본은 보스에만 하한을 0.5로 올려 준다(`:1662`).
            return isBoss ? Mathf.Max(BossLevelPenaltyFloor, f) : f;
        }

        // ---- 갱신 ----

        /// <summary>
        /// 원본 `addCombo()`(project_test.html:1621). **원본은 화상 틱을 포함한 모든 피해에서 부른다**
        /// (`dealDamage` 안에 조건 없이 있다) — 임의로 "직접 타격만"으로 좁히지 말 것.
        /// </summary>
        public static void AddCombo()
        {
            Combo++;
            ComboTimeLeft = ComboWindow;
            if (Combo > MaxCombo) MaxCombo = Combo;
            // 업적('연격의 미학')이 보는 건 세션이 아니라 **영구 기록**이라 프로필에도 남긴다.
            var stats = ProfileService.Current.stats;
            if (Combo > stats.maxCombo) stats.maxCombo = Combo;
        }

        /// <summary>원본 `updateCombo(dt)`(project_test.html:1630) + 연쇄 처치 창 감소.</summary>
        public static void Tick(float dt)
        {
            if (Combo > 0)
            {
                ComboTimeLeft -= dt;
                if (ComboTimeLeft <= 0f) Combo = 0;
            }

            if (chainTimeLeft > 0f)
            {
                chainTimeLeft -= dt;
                if (chainTimeLeft <= 0f) ChainKills = 0;
            }

            if (ShrineBuffLeft > 0f) ShrineBuffLeft = Mathf.Max(0f, ShrineBuffLeft - dt);
        }

        /// <summary>
        /// 처치 1건을 연쇄 처치에 집계하고 **보너스 골드를 돌려준다**(0이면 보너스 없음).
        /// 원본 `killEnemy`의 연쇄 분기(project_test.html:1828~1837):
        /// <code>
        /// run.chainN = run.chainT > 0 ? run.chainN + 1 : 1;
        /// run.chainT = window;
        /// if (chainN >= 3 && !boss) bonus = round(chainN * (4 + lv * 2) * goldMultAll())
        /// </code>
        /// </summary>
        public static int RegisterKillForChain(int enemyLevel, bool isBoss)
        {
            ChainKills = chainTimeLeft > 0f ? ChainKills + 1 : 1;
            chainTimeLeft = ChainKillWindow;

            if (ChainKills < ChainKillMin || isBoss) return 0;
            return Mathf.RoundToInt(ChainKills * (4f + enemyLevel * 2f) * GoldMultiplier);
        }

        /// <summary>성소를 부쉈을 때. 원본 `player.shrineBuffT = CONFIG.shrine.buffDur`(project_test.html:1798).</summary>
        public static void GrantShrineBuff(float duration) =>
            // '성소의 은총'이 지속시간을 늘린다(원본 `buffDur * (1 + itemPow('shrineBless'))` :1798).
            ShrineBuffLeft = Mathf.Max(ShrineBuffLeft,
                duration * (1f + ProfileService.Current.items.Pow("shrineBless")));

        /// <summary>
        /// 새 런 시작 시. 원본 `startRun`이 `combo.n = 0`·`run.chainN = 0`으로 되돌린다(`:4307`·`:4319`).
        /// 살기는 <see cref="RunState.Begin"/>이 이미 0으로 만든다.
        /// </summary>
        public static void ResetForRun()
        {
            Combo = 0;
            ComboTimeLeft = 0f;
            ChainKills = 0;
            chainTimeLeft = 0f;
            ShrineBuffLeft = 0f;
            ShrineActive = false;
        }

        /// <summary>테스트 격리용 — 최고 콤보까지 전부 지운다(정적 상태 오염 방지).</summary>
        public static void Reset()
        {
            ResetForRun();
            MaxCombo = 0;
        }
    }
}
