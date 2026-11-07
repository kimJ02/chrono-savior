using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Characters
{
/// <summary>
/// 마법사(원본 무기 "bow"="마법 지팡이") Z 공격 — 원본 `bowFire()`/`CONFIG.bow`(project_test.html:1932-1977)
/// 그대로 이식. Z를 누르고 있으면 차지, 떼면 그 시점 차지율(chargeK = chargeT/chargeMax)로 마법탄 하나를
/// 발사한다. X 스킬(`tryMageSkill`, :2164)은 <see cref="TryUseSkill"/> — 빌드에 따라 불길 이동
/// 텔레포트(폭발 계열)나 대붕괴(중력 계열)로 갈라진다.
///
/// 스킬트리 자체(갈래 선택·티어 습득)는 <see cref="PlayerProfile.TryLearnMageTier"/> — 이 클래스는
/// 그 결과(`ProfileService.Current.mageBranch`/`mageTier`)를 매 프레임 읽어서 공식에 반영만 한다
/// (원본 `bowFire`가 `meta.skills.bow`를 매번 새로 읽는 것과 동일한 방식).
/// </summary>
public class MageAttack : MonoBehaviour, ICharacterKit, IRunResettable, ISkillSlotSource
{
    public CharacterId Character => CharacterId.Mage;
    /// <summary>마법사는 원본 `updatePlayerCommon`의 즉시-속도 이동을 쓴다(관성은 섬영 전용).</summary>
    public CharacterMover2D.MoveMode RequiredMoveMode => CharacterMover2D.MoveMode.Instant;

    /// <summary>이 캐릭터로 전환됐을 때 — 쿨다운·차지를 초기화한다.</summary>
    public void OnSelected()
    {
        cdTimer = 0f;
        ultCdTimer = 0f;
        CancelCharge();
    }

    /// <summary>다른 캐릭터로 바뀔 때 — 진행 중이던 차지와 감속·표시물을 정리한다.</summary>
    public void OnDeselected() => CancelCharge();

    /// <summary>
    /// 새 사냥 시작 시 — 원본 `resetPlayerForRun()`의 `p.atkCds`·`p.ultCd`·`p.charging` 초기화
    /// (project_test.html:1516·:1526·:1534)와 같은 일이라 캐릭터 전환 초기화를 그대로 재사용한다.
    /// 쿨다운이 남은 채로 런에 들어가면 입장 직후 잠깐 공격이 안 나간다.
    /// </summary>
    public void ResetForRun() => OnSelected();

    /// <summary>차지 중인가 — 차지 단계 깜빡임(<see cref="MageChargeVfx"/>)이 읽는다.</summary>
    public bool IsCharging => charging;
    /// <summary>차지율 0~1(`chargeT / chargeMax`).</summary>
    public float ChargeRatio => chargeMax > 0f ? Mathf.Clamp01(chargeT / chargeMax) : 0f;

    void CancelCharge()
    {
        charging = false;
        chargeT = 0f;
        if (mover != null) mover.SpeedMultiplier = 1f;
    }

    // ---- HUD 스킬 슬롯 (원본 syncHUD 슬롯 부분 :6319~:6352) ----

    /// <summary>Z 칸 — 원본 `clamp(p.atkCds.bow / p.bowCdMax, 0, 1)`(:6321)과 남은 초.</summary>
    public SkillSlotState AttackSlot => SkillSlotState.Cooldown(cdTimer, attackCdMax);

    /// <summary>X 칸 — 원본 `clamp(p.ultCd / p.ultCdMax, 0, 1)`(:6352). 빌드가 없으면 X가 안 나가서 늘 비어 있다.</summary>
    public SkillSlotState SkillSlot => SkillSlotState.Cooldown(ultCdTimer, ultCdMax);

    /// <summary>
    /// 원본 버프 줄의 마법사 몫 — 차지 중이면 차지율(`:6291`), 폭발 빌드면 그 표시(`:6292`, 원본은 층을 안 보고
    /// 갈래만 본다). 원본 차지 앞 글자 '満'은 뺐다 — 일본 신자체라 한국어 윈도 기본 글꼴에 없어 네모로 뜰 수 있다
    /// (HUD의 다른 줄도 원본의 '殺'·'⚔'를 빼고 쓴다).
    /// </summary>
    public string BuffText
    {
        get
        {
            string s = charging ? $"차지 {Mathf.RoundToInt(chargeT / chargeMax * 100f)}%" : "";
            if (ProfileService.Current.mageBranch == MageBranch.Explosion)
                s += (s.Length > 0 ? "   " : "") + "폭발탄 · 불길 이동";
            return s;
        }
    }

    [Header("원본 CONFIG.bow 그대로 (거리·속도는 100px=1유닛 축척)")]
    // cooldown/baseDamage는 매 프레임 Core.PlayerStatCalculator + MageSpecConfig에서 다시 계산되는
    // "표시용 현재값"이다 — 인스펙터에서 수정해도 다음 프레임에 덮어써진다(BaseCooldownConst만 진짜 기준값).
    public float cooldown = 0.5f;          // B.cd ÷ 공격속도 배수 × 티어 쿨감(project_test.html:1941-1942)
    public float baseDamage = 9f;          // statAtk() × B.dmg(0.9) — 골드 강화(atk)가 반영됨
    const float BaseCooldownConst = 0.5f;  // 원본 B.cd(project_test.html:609) — 이 값만 고정 기준
    public float chargeMax = 1.0f;         // B.chargeMax
    public float chargeDmgMult = 1.6f;     // B.chargeDmg
    public int basePierce = 2;             // B.pierce
    public int chargePierce = 4;           // B.chargePierce
    public float projectileSpeed = 10.8f;  // B.speed 1080px/s
    public float projectileRange = 8.8f;   // B.range 880px
    public float chargeMoveSlow = 0.5f;    // 원본 chargeSlow
    public float nearestEnemySearchRadius = 6.2f; // 원본 nearestEnemy(maxDist=620px)(project_test.html:2155)

    [Tooltip("마탄 프리팹(Assets/Prefabs/Combat/MageBolt).")]
    [SerializeField] MageProjectile boltPrefab;

    /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
    public MageProjectile BoltPrefab { get => boltPrefab; set => boltPrefab = value; }

    public Color boltColor = new Color(0.55f, 0.8f, 1f);

    CharacterMover2D mover;
    CircleCollider2D bodyCol;
    PlayerHealth health;
    float cdTimer;
    bool charging;
    float chargeT;
    float ultCdTimer;
    // 쿨다운을 **건 순간**의 최대치 — HUD 슬롯 덮개 비율의 분모. 원본 `p.bowCdMax`(:1942)·`p.ultCdMax`(:2177)도
    // 발사·시전 때 정해진다(`cooldown`은 매 프레임 다시 계산되므로 분모로 쓰면 도중에 스탯이 바뀔 때 튄다).
    float attackCdMax;
    float ultCdMax;

    void Awake()
    {
        mover = GetComponent<CharacterMover2D>();
        bodyCol = GetComponent<CircleCollider2D>();
        health = GetComponent<PlayerHealth>();

        // 차지 표시(단계마다 캐릭터가 깜빡임, `MageChargeVfx`)는 씬의 Player에 같이 놓여 있다 — 예전엔 여기서 붙였다(2026-11-05 생성 책임 2-1, 씬이 원본).
    }

    /// <summary>
    /// 사냥이 끝나거나 로비로 나가는 순간 즉시 차지를 취소한다 — 아래 <see cref="Update"/>의
    /// IsRunning 가드만으로는 "차지 중에 죽어서 Run을 벗어난" 경우 로비에 반투명 차지 표시
    /// (차지 깜빡임 <see cref="MageChargeVfx"/>)가 그대로 남는 걸 못 막는다(Update 자체가 멈추니 그걸
    /// 꺼 줄 코드도 같이 멈춘다). 다음 StartRun의 ResetForRun이 결국 정리하긴 하지만, 그 사이
    /// 로비에 떠 있는 걸 막으려고 전환 즉시 처리한다.
    /// </summary>
    void OnEnable() => GameState.Changed += HandleGameStateChanged;
    void OnDisable() => GameState.Changed -= HandleGameStateChanged;
    void HandleGameStateChanged(GameScene scene)
    {
        if (scene != GameScene.Run) CancelCharge();
    }

    void Update()
    {
        var profile = ProfileService.Current;
        var branch = profile.mageBranch;
        int tier = profile.mageTier;

        // 골드 강화가 반영된 파생 스탯을 매 프레임 새로 계산(원본 statAtk()/statAs()가 호출마다
        // 다시 계산되는 함수인 것과 동일한 방식) — 필드 자기 자신이 아니라 항상 프로필+상수에서
        // 새로 굴리므로 매 프레임 곱해지는 식으로 값이 누적(compounding)되는 버그가 없다.
        baseDamage = PlayerStatCalculator.ComputeAtk(profile) * PlayerStatCalculator.BowDamageMult;
        // 원본 `p.bowCdMax = B.cd / statAs() * cdMult() * cdBonus`(:1942) — `cdMult()`가 '시간의 조각'이다.
        cooldown = BaseCooldownConst / PlayerStatCalculator.ComputeAttackSpeedMultiplier(profile)
            * PlayerStatCalculator.ComputeCooldownMultiplier(profile)
            * MageSpecConfig.BowCooldownBonus(branch, tier); // 원본 :1941-1942

        // 이번 프레임 시작 시점(직전 프레임까지의) 차지 상태를 기준으로 감속을 먼저 적용한다 —
        // 발사(release)되는 바로 그 프레임도 "떼기 직전까지는 차지 중"이었으므로 감속이 맞다.
        // 아래에서 곧바로 charging이 꺼질 수 있어(발사 처리) 순서를 이렇게 잡아야 한다.
        if (mover != null) mover.SpeedMultiplier = charging ? chargeMoveSlow : 1f;

        cdTimer -= Time.deltaTime;
        ultCdTimer -= Time.deltaTime;

        // 로비 등 Run 밖에서는 "새 입력을 받아 액션을 거는" 부분만 막는다 — 위 쿨다운 표시·차지
        // 감속 재계산은 그대로 둬야 한다(PlayMode 테스트가 Update()를 직접 돌려 이 값들을 확인하는데,
        // 테스트의 GameState는 기본 Lobby라 여기를 통째로 막으면 테스트가 깨진다 — 실제로 한 번
        // 그렇게 했다가 Cooldown_ScalesWithAttackSpeedUpgrade 등을 깨뜨렸다). 막아야 하는 건
        // GameInput.AttackHeld/UltDown을 "새로" 읽어 공격·스킬을 거는 지점뿐이다 — GameInput.
        // MenuConfirmDown이 같은 Z를 메뉴 확인에도 쓰기 때문에(10-24), 로비 진입 직후 메뉴를
        // 확인하려는 Z가 그대로 공격으로 읽혀 차지 0%짜리 화살이 나가는 게 이번 버그였다.
        // (GameInput도 사냥 밖에선 Z·X·C를 안 눌린 것으로 돌려준다 — 이 줄과 따로, 메뉴에서 사냥으로 들어올 때 누르던 키는 한 번 떼야 읽힌다.)
        if (!GameState.IsRunning) return;

        if (GameInput.UltDown && ultCdTimer <= 0f) TryUseSkill(profile, branch, tier);

        bool held = GameInput.AttackHeld;
        if (held && !charging && cdTimer <= 0f)
        {
            charging = true;
            chargeT = 0f;
        }

        if (charging)
        {
            chargeT = Mathf.Min(chargeMax, chargeT + Time.deltaTime);
            float k = chargeT / chargeMax;

            if (!held)
            {
                Fire(k, branch, tier);
                charging = false;
                cdTimer = attackCdMax = cooldown;
            }
        }
    }

    void Fire(float chargeK, MageBranch branch, int tier)
    {
        // 원본: aimX=우-좌, aimY=아래-위(화면좌표). Unity는 Y+가 위라 아래-위 항을 뒤집어서
        // "위 화살표=+Y"가 되게 맞췄다(원본과 시각적으로 동일한 결과).
        float aimX = (GameInput.Right ? 1 : 0) - (GameInput.Left ? 1 : 0);
        float aimY = (GameInput.Up ? 1 : 0) - (GameInput.Down ? 1 : 0);
        FireAimed(chargeK, branch, tier, aimX, aimY);
    }

    /// <summary>
    /// 조준 방향을 받아서 쏜다(원본 `bowFire(chargeK, aimX, aimY)` 본체). 방향키를 읽는 <see cref="Fire"/>와 나눈 건
    /// 테스트가 위·아래 조준을 재현하려고 — 키 입력은 테스트에서 흉내 낼 수 없다.
    /// </summary>
    void FireAimed(float chargeK, MageBranch branch, int tier, float aimX, float aimY)
    {
        bool gravityOrb = MageSpecConfig.IsGravityOrb(branch, tier);
        int fireTier = MageSpecConfig.FireTier(branch, tier);
        int facing = mover != null ? mover.Facing : 1;

        // 원본(project_test.html:1951-1954) — 중력 계열에서 방향키 없이 "아래"만 누르면(우리 좌표계는
        // 아래=aimY<0), 발사와 별개로 그 자리에 즉시 중력점을 하나 터뜨린다.
        // 위치는 원본 `(p.x, p.y - 34)` — p.y는 **발**이라 발에서 0.34 **위**다(PlayerBody 참고).
        // 예전엔 몸 중심에서 0.34 아래로 옮겨져 있었다(원본 Y+가 아래인 걸 "아래"로 잘못 읽음).
        Vector2 feet = PlayerBody.Feet(transform, bodyCol);
        if (gravityOrb && aimY < 0f && aimX == 0f)
            MageSkillEffects.DetonateGravityOrb(feet + new Vector2(0f, 0.34f), tier, chargeK, facing);

        if (aimX == 0f && aimY == 0f) aimX = facing;
        Vector2 aim = new Vector2(aimX, aimY).normalized;
        if (Mathf.Abs(aim.x) > 0.1f)
        {
            facing = aim.x > 0f ? 1 : -1; // 원본 :1959 `p.facing = Math.sign(aimX)` — 아래 탄 위치도 이 값을 쓴다
            if (mover != null) mover.SetFacing(facing);
        }

        float dmg = MageSpecConfig.BowDamage(branch, tier, chargeK, chargeDmgMult, baseDamage);
        int pierce = MageSpecConfig.BowPierce(branch, tier, chargeK, basePierce, chargePierce);
        bool inf = MageSpecConfig.BowInfinitePierce(branch, tier, chargeK);
        float spd = projectileSpeed * (1f + 0.3f * chargeK);
        float life = MageSpecConfig.BowLife(branch, tier, chargeK, projectileRange, projectileSpeed);
        float sizeMul = MageSpecConfig.BowSize(branch, tier, chargeK);
        bool explosive = MageSpecConfig.BowExplosive(branch, tier);
        float explosionPower = MageSpecConfig.BowExplosionPower(chargeK);
        bool charged = chargeK >= 0.5f;

        // 원본 `x: p.x + p.facing * 26, y: p.y - 36`(:1965) — 발에서 0.36 위, 좌우는 **바라보는 방향** 쪽. 위·아래로
        // 똑바로 쏠 때도 바라보는 쪽에서 나간다 — 예전엔 조준의 x 부호를 써서 수직 조준이면 늘 오른쪽에서 나갔다.
        // 높이는 몸 중심에서 재면 키 0.5짜리 고블린 머리 위로 지나간다(발 기준, PlayerBody).
        Vector3 spawnPos = new Vector3(feet.x + facing * 0.26f, feet.y + 0.36f, 0f);
        MageProjectile.Spawn(boltPrefab, spawnPos, aim * spd, dmg, pierce, life, sizeMul, boltColor,
            inf, charged, explosive, explosionPower, gravityOrb, chargeK, tier, transform.position, facing);
    }

    /// <summary>원본 `tryMageSkill()`(project_test.html:2164) — X 스킬.</summary>
    void TryUseSkill(PlayerProfile profile, MageBranch branch, int tier)
    {
        if (branch == MageBranch.None || tier < 1) return; // 원본: "빌드 1층부터 X 스킬 사용 가능"(:2169)

        int facing = mover != null ? mover.Facing : 1;
        if (branch == MageBranch.Explosion)
        {
            FireTeleportDash(tier, facing);
        }
        else
        {
            // 깔아 둔 블랙홀(중력점)이 범위 안에 없으면 아무것도 안 하고 쿨다운도 안 쓴다(2026-10-25 사용자 — 원본은 정면에 한 번 터뜨렸다).
            if (!MageSkillEffects.AnyWellInCollapseRange(transform.position)) return;
            Vector2? nearest = FindNearestEnemyPosition();
            MageSkillEffects.GravityCollapse(tier, transform.position, facing, nearest);
        }
        // 원본 `p.ultCdMax = ... * cdMult()`(:2177) — 궁극기 쿨타임에도 '시간의 조각'이 붙는다.
        ultCdTimer = ultCdMax = MageSpecConfig.UltCooldown(branch, tier)
                     * PlayerStatCalculator.ComputeCooldownMultiplier(ProfileService.Current); // 원본 :2177
    }

    /// <summary>원본 `mageFireTeleport()`(project_test.html:2132) — 방향키 방향(없으면 정면)으로
    /// 순간이동하고, 지나온 경로에 불길 장판을 남긴다.</summary>
    void FireTeleportDash(int tier, int facing)
    {
        float dx = (GameInput.Right ? 1 : 0) - (GameInput.Left ? 1 : 0);
        float dy = (GameInput.Up ? 1 : 0) - (GameInput.Down ? 1 : 0);
        TeleportTowards(dx, dy, tier, facing);
    }

    /// <summary>
    /// 원본 `mageFireTeleport()`(project_test.html:2132~:2152) 본체 — 방향(dx, dy, Unity라 위가 +)으로 순간이동하고
    /// 지나온 길에 불길을 깐다. 전부 **발** 기준이다(원본 `p.y`).
    ///
    /// 착지(원본 :2141~:2146): 도착 X는 맵 안(28px), 도착 높이는 천장 아래이면서 **도착 X에서 "지금 발 높이 이하"의
    /// 가장 가까운 발판·바닥**보다 위다 — 그래서 밟고 있는 발판을 뚫고 내려가지 않는다. 그 높이 밑의 발판·바닥보다
    /// 낮으면 그 위에 세운다. 예전엔 바닥 아래만 막고 나머지는 물리에 맡겨서, 발판 속으로 들어가거나 위로 쓰면
    /// 화면 밖까지 올라갔다.
    /// </summary>
    void TeleportTowards(float dx, float dy, int tier, int facing)
    {
        if (mover == null) return;
        Vector2 feet = PlayerBody.Feet(transform, bodyCol);
        float bodyOffset = transform.position.y - feet.y; // 몸 중심 = 발 + 반지름

        if (dx == 0f && dy == 0f) dx = facing;
        Vector2 dir = new Vector2(dx, dy).normalized;
        if (Mathf.Abs(dir.x) > 0.1f) mover.SetFacing(dir.x > 0f ? 1 : -1);

        float toX = Mathf.Clamp(feet.x + dir.x * MageSpecConfig.TeleportDistanceX,
            FieldBounds.MinX + MageSpecConfig.TeleportWallInset, FieldBounds.MaxX - MageSpecConfig.TeleportWallInset);
        float toY = feet.y + dir.y * MageSpecConfig.TeleportDistanceY;
        const float onePixel = 0.01f; // 원본 `groundYBelow(...) - 1`
        float floorFromHere = FieldLayout.SurfaceBelow(toX, feet.y);
        toY = Mathf.Clamp(toY, floorFromHere + onePixel, FieldLayout.TeleportCeilingY);
        float floorY = FieldLayout.SurfaceBelow(toX, toY);
        if (toY < floorY + onePixel) toY = floorY + onePixel;

        Vector2 lineFrom = feet + new Vector2(0f, MageSpecConfig.TeleportFlameLineHeight);
        Vector2 lineTo = new Vector2(toX, toY + MageSpecConfig.TeleportFlameLineHeight);

        if (TryGetComponent(out PlayerAfterimages afterimages)) afterimages.Spawn(transform.position); // 원본 출발 자리 잔상(:2147)
        mover.Teleport(new Vector3(toX, toY + bodyOffset, transform.position.z)); // 원본 vx = vy = 0 — Teleport가 속도를 비운다
        health?.GrantInvuln(MageSpecConfig.TeleportInvuln); // 원본 :2149
        HitFeel.Shake(5f, 0.15f); // 원본 mageFireTeleport(:2152)

        // 원본 spawnFlameLine(:2008) — 출발·도착 사이를 n등분한 n+1곳에 불길.
        int trailPoints = MageSpecConfig.TeleportTrailPoints(tier);
        for (int i = 0; i <= trailPoints; i++)
        {
            float t = trailPoints <= 0 ? 1f : (float)i / trailPoints;
            FireTrailZone.Spawn(Vector2.Lerp(lineFrom, lineTo, t), tier, MageSpecConfig.TeleportTrailPower);
        }
    }

    /// <summary>원본 `nearestEnemy()`(project_test.html:2155) — 대붕괴가 중력점이 하나도 없을 때 쓸 대체 좌표.</summary>
    Vector2? FindNearestEnemyPosition()
    {
        Vector2? best = null;
        float bestDist = nearestEnemySearchRadius;
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, nearestEnemySearchRadius))
        {
            if (!col.CompareTag("Enemy")) continue;
            var protectable = col.GetComponent<ISpawnProtectable>();
            if (protectable != null && protectable.IsSpawnProtected) continue;
            var dmgable = col.GetComponent<IDamageable>();
            if (dmgable != null && dmgable.IsDead) continue;

            float d = Vector2.Distance(transform.position, col.transform.position);
            if (d < bestDist) { bestDist = d; best = col.transform.position; }
        }
        return best;
    }
}
}
