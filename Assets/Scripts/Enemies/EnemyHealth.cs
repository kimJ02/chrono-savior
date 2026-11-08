using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Combat;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 적의 체력·피격 반응·사망. 원본 `dealDamage()`(project_test.html:1657)에서 **대상 쪽 처리**에 해당하는 부분
    /// (`e.hp -= dmg` / `e.flash = 1` / `e.kbx = ...` / `e.hp <= 0 → killEnemy`)을 옮긴 것이다.
    /// 피해량 계산(치명타·난수 변동) 자체는 공격 쪽(`Combat.DamageCalculator`)이 담당한다 — 원본도
    /// `dealDamage` 안에서 공격자 스탯으로 먼저 계산한 뒤 대상 체력을 깎는 순서다.
    ///
    /// 피해 경로에서 콤보 적립(`CombatModifiers.AddCombo`)과 히트스톱 요청(`CombatEvents.RequestHitstop`)도 한다.
    /// 분열은 `EnemySplitOnDeath`(프리팹 부품), 사망 시 골드·경험치는 <see cref="Died"/> 이벤트를 구독한 스포너가
    /// `Systems/EnemyRewards`로 지급한다 — 이 클래스 자체는 보상을 계산하지 않는다(Enemies 도메인이 Systems를 몰라야 하므로).
    /// 원본과 아직 다른 것은 `docs/original-parity.md`.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public class EnemyHealth : MonoBehaviour, IDamageable, IElementAfflictable, IVulnerable
    {
        [Header("스탯 (원본 CONFIG.enemyBase.oni — project_test.html:709)")]
        [Tooltip("최대 체력. 원본 고블린 38. 몹 종류가 늘면 EnemyData(SO)로 옮긴다(스프린트 4).")]
        public float maxHp = 38f;

        [Header("피격 반응 (원본 그대로)")]
        [Tooltip("기본 넉백 속도(무기별 kb 지정이 없을 때). 원본 kbBase 240px/s ÷100(project_test.html:1678).")]
        public float knockbackSpeed = 2.4f;
        [Tooltip("종류별 넉백 배율. 원본 :1678 — 오우거 0.4, 나머지 1. 스폰 시 EnemyData에서 주입된다.")]
        public float knockbackMultiplier = 1f;

        [Tooltip("보스인지. 레벨 페널티 하한이 25%가 아니라 50%가 된다(원본 :1652). " +
                 "`Boss` 컴포넌트는 Instantiate **뒤에** 붙기 때문에 Awake의 GetComponent로는 못 찾는다 — " +
                 "이 프로젝트 단골 함정이라 스포너가 직접 켜 준다.")]
        public bool isBoss;
        [Tooltip("피격 플래시 감쇠 속도. 원본 `e.flash -= dt*6`(project_test.html:4020) — 1에서 0까지 약 0.167초.")]
        public float flashDecay = 6f;
        [Tooltip("플래시 최대 강도. 원본은 흰색을 alpha `flash*0.75`로 덧그린다(project_test.html:4799).")]
        public float flashStrength = 0.75f;

        [Header("화상 (원본 CONFIG.elem.burn — project_test.html:692)")]
        public int maxBurnStacks = 10;
        public float burnDuration = 4.0f;   // 원본 life: 4.0 — 매 적용마다 리필(누적 아님)
        public float burnTickInterval = 0.5f;
        public float burnDamagePerStackPct = 0.08f; // statAtk × 이 값 × 스택 수, 0.5초마다

        /// <summary>현재 체력. UI/테스트가 읽는다.</summary>
        public float CurrentHp { get; private set; }
        public float MaxHp => maxHp;
        public bool IsDead => CurrentHp <= 0f;

        /// <summary>사망 직전(Destroy 전) 1회 발생 — 트랙 A(처치 보상)가 여기 구독해서 골드/EXP를 지급한다.</summary>
        public event System.Action<EnemyHealth> Died;

        SpriteRenderer sr;
        // 피격 플래시 — 몸 모양 그대로 흰빛을 덮는 자식 렌더러(2026-10-24). 예전엔 `sr.color`를 흰색 쪽으로 섞었는데,
        // (1) 흰색 그림(정식 아트)은 곱해도 더 밝아지지 않아 플래시가 안 보였고, (2) 되돌릴 "원래 색"을 Awake에서
        // 잡았는데 그땐 아직 프리팹 색(빨강)이라 — 스포너가 흰색·종류색을 칠하는 건 그 뒤다 — 한 번 맞으면 빨갛게 물들었다.
        SpriteRenderer flashSr;
        [Tooltip("피격 깜빡임 효과 머티리얼(Assets/Materials/SpriteFlash — 그림의 몸 모양(알파)만 살리고 색은 꼭짓점 색으로 칠하는 셰이더). 외주 효과로 바꿀 땐 이 칸만. — 몹 프리팹(Enemy_Base, 변형은 물려받음)에 들어 있다.")]
        [SerializeField] Material flashMaterial;

        /// <summary>프리팹 검증과 테스트가 읽고 넣는다(2026-11-06 3.5-e — 예전엔 `Resources`에서 셰이더를 읽었다).</summary>
        public Material FlashMaterial { get => flashMaterial; set => flashMaterial = value; }
        ISpawnProtectable spawnProtect;
        EnemyMove mover;
        float flash;

        int burnStacks;
        float burnRemaining;
        float burnTickTimer;
        VfxPlayer burnFlame;
        VfxDots burnEmbers;

        /// <summary>지금 화상 스택(그림·테스트용).</summary>
        public int BurnStacks => burnStacks;

        /// <summary>
        /// 화상 그림(2026-11-09 사용자 "적이 불타는 VFX가 뜨면서 도트 데미지를 받아야") — 화상 피해 자체는 원래 있었지만(원본 그대로 0.5초마다)
        /// 눈에 안 보였다. 불타는 동안 몸에 Pixel Fire 불꽃(<see cref="VfxKeys.MageBurn"/>)이 붙고, 틱마다 몸이 주황으로 깜빡인다(흰 피격 깜빡임과 구별).
        /// 원본은 머리 위에 스택 숫자만 그렸다 — 그림은 의도적 편차(`docs/original-parity.md`).
        /// </summary>
        public static readonly Color BurnFlashColor = new Color(1f, 0.55f, 0.15f);
        public const int BurnFlameScale = 2;
        public const float BurnFlameFps = 12f;
        Color flashColor = Color.white;
        // 원본 `e.vulnT` / `e.gravityExpose`(project_test.html:3968에서 0으로 시작).
        float vulnRemaining;
        float gravityExpose;

        /// <summary>
        /// 몹 레벨 — 원본 `enemyLv()`(project_test.html:3924, `regionBaseLv(region) + randInt(0,1)`).
        /// **난이도가 아니라 레벨 페널티 전용이다**(체력·피해 배율은 지역이 따로 정한다) —
        /// 내 레벨이 이보다 낮으면 레벨당 5%씩 내 피해가 깎인다(`levelFactor` `:1651`).
        /// 스포너가 스폰 시 넣어 준다. 안 넣으면 1로 남아 페널티가 없다.
        /// </summary>
        public int Level { get; private set; } = 1;

        /// <summary>스폰 시 1회. `SetMaxHp`와 같은 이유로 필드 대입이 아니라 메서드로 받는다.</summary>
        public void SetLevel(int level) => Level = Mathf.Max(1, level);

        static readonly List<EnemyHealth> active = new List<EnemyHealth>();

        /// <summary>
        /// 켜져 있는 적 전부(잡몹·소환·성소·보스) — 스스로 등록한다(2026-11-05 2b). 전투 담당(`Systems/CombatDirector`)이 미니맵·보스 체력바에 쓰려고
        /// 읽는다(예전엔 화면이 씬을 뒤져 찾았다). 밖에선 읽기만.
        /// </summary>
        public static IReadOnlyList<EnemyHealth> Active => active;

        void OnEnable() => active.Add(this);
        void OnDisable() => active.Remove(this);

        void Awake()
        {
            CurrentHp = maxHp;
            sr = GetComponent<SpriteRenderer>();
            // 같은 오브젝트의 EnemyMove가 둘 다 제공한다. 인터페이스로 받는 건 Core 규칙 때문이 아니라
            // (같은 도메인이라 직접 참조해도 되지만) 나중에 스폰 무적을 다른 컴포넌트가 맡아도 되게.
            spawnProtect = GetComponent<ISpawnProtectable>();
            mover = GetComponent<EnemyMove>();
        }

        void Update()
        {
            if (flash > 0f)
            {
                flash = Mathf.Max(0f, flash - Time.deltaTime * flashDecay);
                ShowFlash();
            }
            UpdateBurn(Time.deltaTime);
            // 원본 `e.vulnT = Math.max(0, e.vulnT - dt)`(project_test.html:1726).
            if (vulnRemaining > 0f) vulnRemaining = Mathf.Max(0f, vulnRemaining - Time.deltaTime);
            // 원본 `e.gravityExpose = Math.max(0, e.gravityExpose - dt * 0.35)`(:1727) — 중력점에서 벗어나면 노출이 빠진다.
            // 예전엔 이게 없어서, 한 번 끌려 본 적은 노출이 계속 남아 다음에 조금만 끌려도 바로 취약해졌다.
            if (gravityExpose > 0f)
                gravityExpose = Mathf.Max(0f, gravityExpose - Time.deltaTime * MageSpecConfig.GravityExposureDecayPerSecond);
        }

        // ---- IVulnerable (원본 vulnT, project_test.html:1663·:1726·:3818) ----

        public bool IsVulnerable => vulnRemaining > 0f;

        public void ApplyVulnerable(float duration) => vulnRemaining = Mathf.Max(vulnRemaining, duration);

        /// <summary>
        /// 원본 `e.gravityExpose += dt * (1 + stack*0.16); if (>= 1.25) e.vulnT = max(vulnT, 3.0)`(`:3817`).
        /// 누적치는 **줄어들지 않는다** — 한 번 임계치를 넘긴 적은 이후 중력점에 닿을 때마다 곧바로 다시 취약해진다.
        /// </summary>
        public void AddGravityExposure(float exposure)
        {
            if (IsDead) return;
            gravityExpose += exposure;
            if (gravityExpose >= MageSpecConfig.GravityVulnerableThreshold)
                ApplyVulnerable(MageSpecConfig.VulnerableDuration);
        }

        /// <summary>원본 `updateElements()`의 burn 분기(project_test.html:1729-1741) 그대로.</summary>
        void UpdateBurn(float dt)
        {
            if (burnStacks <= 0 || IsDead) return;
            burnRemaining -= dt;
            burnTickTimer += dt;
            if (burnTickTimer >= burnTickInterval)
            {
                burnTickTimer -= burnTickInterval;
                float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
                float raw = atk * burnDamagePerStackPct * burnStacks;
                // 화상 틱은 원본이 noCrit·noHitstop·kb:0으로 넘긴다 — 여기선 크리티컬 확률 0, 넉백 0.
                float dmg = DamageCalculator.Roll(raw, 0f, out _);
                burnTick = true; // 화상 틱은 타격 소리를 안 낸다(EnemyTickDamaged에서 빠짐)
                try { TakeTickDamage(dmg); }
                finally { burnTick = false; }
            }
            if (burnRemaining <= 0f) burnStacks = 0; // 원본 `if (e.burnT <= 0) e.burnS = 0`
            UpdateBurnFlame();
        }

        /// <summary>불타는 동안만 불꽃 그림 — 몹 배율과 무관하게 월드 배율 <see cref="BurnFlameScale"/>(정수), 몸 아래쪽에 붙는다. 그림이 없으면 없이.</summary>
        void UpdateBurnFlame()
        {
            bool burning = burnStacks > 0 && !IsDead;
            if (burnEmbers != null) burnEmbers.Emitting = burning; // 끄면 남은 불씨만 사라지고 스스로 지워진다
            if (!burning)
            {
                if (burnFlame != null) burnFlame.gameObject.SetActive(false);
                return;
            }
            // 튀어 오르는 불씨(그림 없이 점, 2026-11-09 Codex 합의) — 몹에 붙여 몹 배율과 무관하게
            if (burnEmbers == null && VfxFrames.Has(VfxKeys.MageBurn))
                burnEmbers = VfxDots.Attach(transform, VfxDots.Motion.Rise, 6, new Color(1f, 0.6f, 0.2f), 0.15f, 0.5f, 0.6f, (sr != null ? sr.sortingOrder : 0) + 3);
            if (burnFlame == null)
            {
                var frames = VfxFrames.Get(VfxKeys.MageBurn);
                if (frames.Length == 0) return;
                var go = new GameObject("BurnFlame", typeof(SpriteRenderer));
                go.transform.SetParent(transform, false);
                var fsr = go.GetComponent<SpriteRenderer>();
                fsr.sortingOrder = (sr != null ? sr.sortingOrder : 0) + 2; // 몸·피격 깜빡임 위
                burnFlame = go.AddComponent<VfxPlayer>();
                burnFlame.Play(frames, BurnFlameFps, true, 0f, Color.white);
            }
            var s = transform.lossyScale;
            burnFlame.transform.localScale = new Vector3(BurnFlameScale / Mathf.Max(0.01f, Mathf.Abs(s.x)), BurnFlameScale / Mathf.Max(0.01f, Mathf.Abs(s.y)), 1f);
            float bodyHalf = sr != null && sr.sprite != null ? sr.bounds.extents.y : 0.25f;
            float flameHalf = burnFlame.GetComponent<SpriteRenderer>().bounds.extents.y;
            burnFlame.transform.position = (Vector2)transform.position + new Vector2(0f, flameHalf - bodyHalf * 0.6f); // 불꽃 밑동이 몸 아래쪽에
            burnFlame.gameObject.SetActive(true);
        }

        public void ApplyBurn(int stacks)
        {
            if (IsDead) return;
            burnStacks = Mathf.Min(maxBurnStacks, burnStacks + stacks);
            burnRemaining = burnDuration; // 원본 `e.burnT = E.life` — 누적이 아니라 리필
            UpdateBurnFlame();
        }

        public void TakeDamage(float amount, GameObject source)
        {
            if (source == null)
            {
                TakeDamageWithKnockback(amount, null, 1f, knockbackSpeed);
                return;
            }
            // 원본 `sign(e.x - player.x)`(project_test.html:1679) — 가해자 반대 방향으로 밀린다.
            // 정확히 같은 X에 겹쳐 있으면 sign이 0이라 원본은 `|| player.facing`으로 대체하는데,
            // 우리는 가해자 방향 정보가 없어 오른쪽(+1)으로 고정한다(겹친 순간에만 생기는 예외).
            float dir = Mathf.Sign(transform.position.x - source.transform.position.x);
            if (Mathf.Approximately(dir, 0f)) dir = 1f;
            TakeDamageWithKnockback(amount, source, dir, knockbackSpeed);
        }

        // 지금 들어온 피해가 지속 피해 틱인지. 히트스톱만 이 값을 본다(위 주석 참고).
        bool inTickDamage;
        // 그 틱이 화상(지속 피해)인지 — 무기 틱(레이저 등)과 달리 타격 소리를 안 낸다.
        bool burnTick;

        /// <summary>
        /// 지속 피해 틱(화상·불길 장판·중력점). 넉백 0, 히트스톱 없음 — 원본 `dealDamage`의
        /// `kb: 0` + `!opts.isBurnTick` 조합(project_test.html:1678·:1686)에 대응한다.
        /// </summary>
        public void TakeTickDamage(float amount)
        {
            inTickDamage = true;
            try { TakeDamageWithKnockback(amount, null, 1f, 0f); }
            finally { inTickDamage = false; } // 중간에 죽어서 예외가 나도 플래그가 남지 않게
        }

        public float TakeDamageWithKnockback(float amount, GameObject source, float knockbackDirSign, float knockbackSpeedOverride,
                                            float hitstop = HitFeel.HitstopHit)
        {
            if (IsDead) return 0f;
            // 원본은 `dealDamage` 진입 즉시 스폰 무적을 확인하고 0을 반환한다(project_test.html:1659).
            // 공격 스크립트(MageProjectile)도 별도로 확인하는데, 그건 **관통 카운트를 소모하지 않기 위해서**라
            // 역할이 다르다 — 여기 검사는 "어떤 경로로 들어온 피해든 무적이면 무효"를 보장한다.
            if (spawnProtect != null && spawnProtect.IsSpawnProtected) return 0f;

            // 대상에 달린 배수 두 개를 여기서 곱한다(`DamageCalculator`는 대상 참조가 없다).
            //  - `vulnMult` — 취약한 적은 받는 피해 ×1.2 (원본 :1663)
            //  - `lvF`      — 내 레벨이 몹보다 낮으면 레벨당 -5%, 하한 25%(보스는 50%) (원본 `levelFactor` :1651)
            // **둘을 한 번에 곱하고 한 번만 반올림한다** — 따로 반올림하면 원본(곱셈을 다 모은 뒤
            // 한 번 반올림)과의 오차가 곱해진 만큼 커진다.
            float mult = 1f;
            // 원본 `vulnMult = 1.2 + itemPow('vuln')`(:1663) — 각인을 가지면 취약이 더 아프다.
            if (IsVulnerable) mult *= DamageCalculator.VulnerableMultiplier + ProfileService.Current.items.Pow("vuln");
            mult *= CombatModifiers.LevelFactor(ProfileService.Current.level, Level, isBoss);
            amount = Mathf.Max(1f, JsMath.Round(amount * mult)); // 원본 `max(1, round(...))` — 배수가 1이어도 여기서 한 번(굴림은 반올림 전 값을 넘긴다)

            // 원본은 `dealDamage` 안에서 조건 없이 `addCombo()`를 부른다(:1682) — 화상 틱도 포함이다.
            CombatModifiers.AddCombo();
            // 히트스톱은 **직접 타격만** 건다(원본 `!opts.isBurnTick`, :1686).
            if (inTickDamage && !burnTick) CombatEvents.RaiseEnemyTickDamaged(gameObject); // 레이저 등 무기 틱 — 타격 소리용
            if (!inTickDamage)
            {
                CombatEvents.RaiseEnemyDamaged(gameObject);
                CombatEvents.RequestHitstop(hitstop); // 공격마다 다른 길이(원본 `opts.hitstop`, 없음 = `noHitstop`)
            }

            CurrentHp -= amount;
            flash = 1f; // 원본 `e.flash = 1`(project_test.html:1666)
            flashColor = burnTick ? BurnFlashColor : Color.white; // 화상 틱은 주황(2026-11-09 — 불타는 피해가 보이게)
            ShowFlash();

            // 원본 `kbBase * (1 + itemPow('knockback')) * (e.type === 'bigOni' ? 0.4 : 1)`(:1678).
            if (mover != null && knockbackSpeedOverride > 0f)
            {
                float kb = knockbackSpeedOverride * knockbackMultiplier
                           * (1f + ProfileService.Current.items.Pow("knockback")); // 파쇄의 망치
                mover.ApplyKnockback(knockbackDirSign * kb);
            }

            // 균열의 각인 — 때린 적을 취약 상태로 만든다(원본 `if (itemPow('vuln') > 0) e.vulnT = max(vulnT, 3)` :1677).
            if (!inTickDamage && ProfileService.Current.items.Pow("vuln") > 0f)
                ApplyVulnerable(MageSpecConfig.VulnerableDuration);

            if (CurrentHp <= 0f) Die();
            return amount; // 실제로 준 피해(원본 `dealDamage`의 반환값)
        }

        /// <summary>지금 플래시 세기(1 → 0). 테스트·연출이 읽는다.</summary>
        public float FlashAmount => flash;

        /// <summary>플래시를 그리는 자식 렌더러(처음 맞을 때 만든다). 테스트가 읽는다.</summary>
        public SpriteRenderer FlashRenderer => flashSr;

        /// <summary>
        /// 원본 `if (e.flash > 0) { globalAlpha = flash × 0.75; 흰색 }`(:4798) — 몸 모양 흰 실루엣을 같은 세기로 덮는다.
        /// 원본은 흰 타원인데 그림 몹에선 몸 모양이 자연스러워 바꿨다(의도적 편차, `docs/original-parity.md` 9절).
        /// </summary>
        void ShowFlash()
        {
            if (sr == null) return;
            if (flashSr == null)
            {
                if (flashMaterial == null) return; // 몹 프리팹엔 늘 있다(씬 구성 검증이 본다) — 테스트가 맨손으로 만든 몹은 깜빡임 없이
                var go = new GameObject("HitFlash");
                go.transform.SetParent(transform, false);
                flashSr = go.AddComponent<SpriteRenderer>();
                flashSr.sharedMaterial = flashMaterial;
            }
            flashSr.sprite = sr.sprite;
            flashSr.flipX = sr.flipX;
            flashSr.flipY = sr.flipY;
            flashSr.sortingLayerID = sr.sortingLayerID;
            flashSr.sortingOrder = sr.sortingOrder + 1;
            flashSr.color = new Color(flashColor.r, flashColor.g, flashColor.b, flash * flashStrength);
            flashSr.enabled = flash > 0f;
        }

        void Die()
        {
            CurrentHp = 0f;
            // 도메인을 가로지르는 알림(히트스톱 등). 처치 보상은 아래 `Died` 구독자가 따로 처리한다.
            CombatEvents.RaiseEnemyKilled(gameObject);
            // 원본 killEnemy(project_test.html:1793)의 골드·경험치는 Died 구독자(트랙 A)가 처리.
            // 연쇄처치 보너스·슬라임 분열은 아직 범위 밖 — 나중 스프린트에서 붙인다.
            Died?.Invoke(this);
            Destroy(gameObject);
        }

        /// <summary>
        /// 난이도 스케일링(트랙 A, "가상 지역 레벨")이 스폰 직후 호출한다. <c>maxHp</c> 필드만 바꾸면
        /// 이미 실행된 <see cref="Awake"/>가 세팅한 <c>CurrentHp</c>엔 반영되지 않는다 — 이 프로젝트에서
        /// 반복적으로 겪은 함정(RequireComponent 자동보충, AddComponent의 동기 Awake 실행)과 같은 종류라
        /// 전용 메서드로 묶어 실수를 막는다.
        /// </summary>
        public void SetMaxHp(float newMaxHp)
        {
            maxHp = newMaxHp;
            CurrentHp = newMaxHp;
        }
    }
}
