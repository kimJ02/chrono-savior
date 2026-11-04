namespace ChronoSavior.Core
{
    /// <summary>
    /// 화면에 보이는 **덩치**를 한곳에서 정한다 — 플레이어 · 몹 · 성소 · 보스.
    ///
    /// ## 왜 한곳에 모으나
    /// 덩치는 세 군데에 흩어져 있었다: 몹은 `BuildEnemyData`가 굽는 `EnemyData.colliderRadius`,
    /// 플레이어는 `PlayerRig.spriteHeight`, 성소·보스는 `EnemySpawner`에 박힌 `localScale`.
    /// 그래서 "성소가 오니보다 작다" 같은 어긋남이 생겨도 **어디를 봐야 하는지조차 알기 어려웠다.**
    /// 값을 여기 모아 두면 서로의 비율을 한눈에 비교할 수 있다.
    ///
    /// ## 원본 대비
    /// 원본은 100px = 1유닛이므로 `CONFIG`의 w/h를 100으로 나눈 값이 "원본 크기"다.
    ///
    /// | | 원본(px) | 원본(유닛) | 지금 |
    /// |---|---|---|---|
    /// | 플레이어 | 34×56 (`:604`) | 0.34×0.56 | 높이 **0.50** (= 오니와 1:1) |
    /// | 오니 | 42×46 (`:711`) | 0.42×0.46 | 반지름 **0.25** (= 높이 0.50) |
    /// | 성소 | 64×96 (`:698`) | 0.64×0.96 | **원본 그대로** |
    /// | 보스 | 130×150 (`:721`) | 1.30×1.50 | **원본 그대로** |
    ///
    /// **플레이어 높이는 사용자가 직접 지정한 값이다** — 1.50(원본의 2.7배) → 1.00(발판 간격 문제, 09-16) →
    /// **0.50(2026-09-28, "캐릭터가 너무 크다, 몬스터랑 1대1 크기로")**. 원본 비율은 "플레이어가 오니보다
    /// 살짝 큰"(0.56 : 0.46 = 1.22배) 정도인데, 지금은 오니와 같은 높이(1배)다.
    /// 되돌리려면 <see cref="PlayerHeight"/> 하나만 만지면 된다(씬에 저장된 값이 아니라 이 상수를 따른다 —
    /// `PlayerRig.Awake` 참고).
    /// </summary>
    public static class EntitySizeConfig
    {
        /// <summary>
        /// 플레이어 그림 높이(유닛). 콜라이더 반지름도 이 값의 절반으로 따라간다 —
        /// 그림과 히트박스가 따로 놀면 "보이는 곳을 때렸는데 안 맞는" 상태가 된다.
        /// </summary>
        public const float PlayerHeight = 0.50f;

        /// <summary>
        /// ⚠️ <see cref="PlayerHeight"/>의 **상한은 발판 층간 간격(1.35유닛)**이다
        /// (`World.FieldLayout.NormalPlatforms` — 1.35 / 2.70 / 4.05 / 5.40).
        ///
        /// 넘기면 발판 위에 섰을 때 **머리가 윗 발판 콜라이더를 뚫는다.** 그러면
        ///   ① 위아래로 끼어 "벽에 막히는" 느낌이 나고,
        ///   ② 마법탄 총구(`MageAttack`의 `(±0.26, +0.36)` 오프셋)가 발판 안에서 생겨
        ///      `MageProjectile`이 Ground에 닿은 것으로 보고 **쏘자마자 터진다.**
        ///
        /// 2026-09-16에 1.50으로 올렸다가 정확히 이 증상이 나서 1.00으로 되돌렸다.
        /// 여유를 보면 실질 상한은 **1.15** 정도다.
        /// </summary>
        public const float PlayerHeightMax = 1.15f;

        /// <summary>플레이어 콜라이더 반지름(월드). 그림 높이의 절반.</summary>
        public const float PlayerRadius = PlayerHeight / 2f;

        /// <summary>
        /// 오니 기준 반지름(월드). 다른 몹은 원본 높이 비율로 여기서 파생된다
        /// (`BuildEnemyData.RadiusFor`).
        ///
        /// 예전엔 0.5였는데 그건 원본 오니(46px → 반지름 0.23)의 **2.17배**였다. 몹이 전부
        /// 원본보다 두 배로 커서, 원본 크기대로 만든 성소가 오니보다 작아 보이는 상황이 나왔다.
        /// </summary>
        public const float OniRadius = 0.25f;

        /// <summary>오니 원본 높이(px) — 다른 몹의 반지름을 이 비율로 계산한다.</summary>
        public const float OniHeightPx = 46f;

        /// <summary>성소 — 원본 `CONFIG.shrine.w/h = 64/96`(project_test.html:698).</summary>
        public const float ShrineWidth = 0.64f;
        public const float ShrineHeight = 0.96f;

        /// <summary>보스 — 원본 `CONFIG.boss.w/h = 130/150`(project_test.html:721).</summary>
        public const float BossWidth = 1.30f;
        public const float BossHeight = 1.50f;
    }
}
