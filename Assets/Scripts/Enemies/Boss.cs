using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 지역 보스 — 원본 `spawnBoss()`(project_test.html:4155)와 `updateBoss()`(`:4176`) 그대로.
    ///
    /// **상태기계 4갈래**로 돌아간다. 어느 쪽을 고를지는 `idle`에서 플레이어 위치로 정한다(`:4189`):
    /// - 플레이어가 **높은 발판 위**(1.3유닛 이상) → **귀신불 3발**(원거리) — 발판으로 도망가도 안전하지 않게
    /// - 가까우면(2.6유닛 안) → **내려찍기**: 좌우 충격파 2발 + 즉시 근접 판정
    /// - 멀면(4.8유닛 밖, 55% 확률) → **돌진**
    /// - 나머지 → 걷기
    ///
    /// 체력 66%·33%를 지날 때 각각 **부하 3마리를 소환**한다(`:4182`).
    ///
    /// 수평 이동은 <see cref="IEnemyMotion"/>으로 `EnemyMove`에 넘겨서 중력·경계·넉백을 공유한다.
    /// 원본도 보스가 `enemies` 배열 안에 있고 갱신 분기만 따로다.
    ///
    /// **소유권 예외(2026-09-30, 팀원 추가)**: `IGrabExempt` 구현은 섬영 칼날폭풍 4층 "강제 연행"이
    /// 보스를 붙잡지 못하게 막으려고 팀원이 추가했다(원본 `e.boss` 제외, project_test.html:2694).
    /// 사용자 확인 받음 — `Core/IGrabExempt.cs` 주석과 PROGRESS.md 참고, 팀장 확인 필요.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EnemyMove))]
    [RequireComponent(typeof(EnemyHealth))]
    public class Boss : MonoBehaviour, IEnemyMotion, IEnemyContactDamageModifier, IGrabExempt
    {
        enum State { Idle, Walk, SlamTele, SlamRecover, SpitTele, SpitRecover, ChargeTele, Charge }

        // ---- 원본 CONFIG.boss(project_test.html:718) — 거리·속도는 100px = 1유닛 ----
        public const float HpBase = 800f;
        public const float HpGrowPerRegion = 2.15f;
        public const float DmgBase = 30f;
        public const float DmgGrowPerRegion = 1.4f;
        public const float MoveSpeed = 0.90f;      // 원본 90
        public const float ChargeSpeed = 6.20f;    // 원본 620
        public const float SlamRange = 2.60f;      // 원본 260
        public const float HighY = 1.30f;          // 원본 highY 130 — 이만큼 위면 원거리로 전환
        public const int RewardExp = 300;
        public const int RewardGold = 400;
        /// <summary>보스는 거의 안 밀린다. 원본 `e.boss ? 0.08`(`:1678`).</summary>
        public const float KnockbackMultiplier = 0.08f;

        // 귀신불(spit) — 원본 `CONFIG.boss.spit`
        public const int SpitCount = 3;
        public const float SpitSpeed = 4.60f;      // 원본 460
        public const float SpitLife = 3.0f;
        public const float SpitDamageMult = 0.9f;
        public const float SpitSpread = 0.35f;     // 라디안
        public const float SpitTele = 0.5f;
        public const float SpitRecover = 0.7f;

        public const float SlamShockSpeed = 4.30f; // 원본 430
        public const float SlamShockLife = 0.95f;
        public const float SlamShockDamageMult = 1.1f;
        public const float SlamContactDamageMult = 1.3f;  // 원본 `damagePlayer(b.dmg, b.x, 1.3)`
        public const float ChargeContactDamageMult = 1.4f; // 원본 `damagePlayer(b.dmg, b.x, 1.4)`

        /// <summary>부하 소환이 일어나는 체력 비율. 원본 `:4182`~`:4183`.</summary>
        public const float SummonPhase1 = 0.66f;
        public const float SummonPhase2 = 0.33f;
        public const int SummonCount = 3;

        /// <summary>원본 `hp: round(800 * 2.15^(r-1))`(`:4159`).</summary>
        public static float HpForRegion(int region) => HpBase * Mathf.Pow(HpGrowPerRegion, region - 1);
        /// <summary>원본 `dmg: round(30 * 1.4^(r-1))`(`:4164`).</summary>
        public static float DamageForRegion(int region) => DmgBase * Mathf.Pow(DmgGrowPerRegion, region - 1);
        /// <summary>원본 `lv = regionBaseLv(r) + 2`(`:4157`) — 잡몹보다 2레벨 높다.</summary>
        public static int LevelForRegion(int region) => RegionConfig.RecommendedLevel(region) + 2;

        public Sprite projectileSprite;
        public Color projectileColor = new Color(0.78f, 0.42f, 1f);

        State state = State.Idle;
        float stateTimer = 0.8f;
        bool summonedPhase1, summonedPhase2;
        EnemyMove mover;
        EnemyHealth health;

        void Awake()
        {
            mover = GetComponent<EnemyMove>();
            health = GetComponent<EnemyHealth>();
        }

        /// <summary>원본은 돌진 중 ×1.4, 내려찍기 근접 판정 ×1.3. 평상시 접촉은 배수 없음(`:4285`).</summary>
        public float ContactDamageMultiplier => state == State.Charge ? ChargeContactDamageMult : 1f;

        public float GetHorizontalSpeed(float dt, Transform target, float baseSpeed)
        {
            stateTimer -= dt;
            CheckPhaseSummons();

            if (target == null) return 0f;

            float dx = target.position.x - transform.position.x;
            float adx = Mathf.Abs(dx);
            // 원본 `player.y < groundY - highY` — 원본은 Y+가 아래라 부등호가 뒤집힌다.
            bool highUp = target.position.y > FieldBounds.GroundY + HighY;

            switch (state)
            {
                case State.Idle:
                    if (stateTimer <= 0f) ChooseNextAction(dx, adx, highUp);
                    return 0f;

                case State.Walk:
                    // 원본: 플레이어가 위로 도망가면 즉시 재판단한다(`:4198`).
                    if (highUp) { Enter(State.Idle, 0.12f); return 0f; }
                    if (dx != 0f) mover.SetDirection(dx > 0f ? 1 : -1);
                    // 근접하면 걷다 말고 바로 내려찍기(`:4204`).
                    if (adx < SlamRange * 0.7f) { Enter(State.SlamTele, 0.5f); return 0f; }
                    if (stateTimer <= 0f) Enter(State.Idle, 0.25f);
                    return mover.Direction * MoveSpeed;

                case State.SlamTele:
                    if (stateTimer <= 0f) { DoSlam(target); Enter(State.SlamRecover, 0.7f); }
                    return 0f;

                case State.SlamRecover:
                    if (stateTimer <= 0f) Enter(State.Idle, Random.Range(0.3f, 0.7f));
                    return 0f;

                case State.SpitTele:
                    if (dx != 0f) mover.SetDirection(dx > 0f ? 1 : -1);
                    if (stateTimer <= 0f) { DoSpit(target); Enter(State.SpitRecover, SpitRecover); }
                    return 0f;

                case State.SpitRecover:
                    if (stateTimer <= 0f) Enter(State.Idle, Random.Range(0.2f, 0.5f));
                    return 0f;

                case State.ChargeTele:
                    if (dx != 0f) mover.SetDirection(dx > 0f ? 1 : -1);
                    if (stateTimer <= 0f) Enter(State.Charge, 0.95f);
                    return 0f;

                default: // Charge
                {
                    // 원본은 속도를 먼저 정하고 끝 판정을 한다 — 돌진귀와 같은 순서다(`:4257`).
                    float vx = mover.Direction * ChargeSpeed;
                    if (stateTimer <= 0f || AtFieldEdge())
                    {
                        Enter(State.Idle, 0.8f);
                        HitFeel.Shake(6f, 0.2f); // 원본 :4262
                    }
                    return vx;
                }
            }
        }

        /// <summary>원본 `idle` 분기의 행동 선택(project_test.html:4189~4194).</summary>
        void ChooseNextAction(float dx, float adx, bool highUp)
        {
            if (dx != 0f) mover.SetDirection(dx > 0f ? 1 : -1);

            if (highUp) Enter(State.SpitTele, SpitTele);                      // 발판 위 → 원거리
            else if (adx < SlamRange) Enter(State.SlamTele, 0.55f);           // 가까움 → 내려찍기
            else if (adx > 4.8f && Random.value < 0.55f) Enter(State.ChargeTele, 0.5f); // 멂 → 돌진
            else Enter(State.Walk, Random.Range(0.7f, 1.2f));
        }

        void Enter(State next, float duration)
        {
            state = next;
            stateTimer = duration;
        }

        bool AtFieldEdge()
        {
            const float edge = 0.9f; // 원본 `b.x < 90 || b.x > mapW - 90`
            float x = transform.position.x;
            return x < FieldBounds.MinX + edge || x > FieldBounds.MaxX - edge;
        }

        /// <summary>
        /// 내려찍기 — 좌우로 충격파 하나씩(원본 `kind:'shock'`, `:4215`) + **즉시 근접 판정**(`:4219`).
        /// 충격파만 있으면 보스 발밑이 오히려 안전지대가 된다.
        /// </summary>
        void DoSlam(Transform target)
        {
            HitFeel.Shake(12f, 0.35f); // 원본 :4212
            float dmg = mover.attackPower;
            float y = FieldBounds.GroundY + 0.2f;

            foreach (int dir in new[] { -1, 1 })
            {
                Vector3 origin = new Vector3(transform.position.x + dir * 0.5f, y, 0f);
                EnemyBolt.Spawn(origin, new Vector2(dir * SlamShockSpeed, 0f),
                                dmg * SlamShockDamageMult, SlamShockLife, projectileSprite, projectileColor);
            }

            // 원본 `rectsOverlap(b.x - 150, groundY - slamH, 300, slamH, ...)` — 좌우 1.5유닛, 높이 1.5유닛.
            if (target != null
                && Mathf.Abs(target.position.x - transform.position.x) < 1.5f
                && target.position.y < FieldBounds.GroundY + 1.5f)
            {
                var damageable = target.GetComponent<IDamageable>();
                if (damageable != null && !damageable.IsDead)
                    damageable.TakeDamage(dmg * SlamContactDamageMult, gameObject);
            }
        }

        /// <summary>귀신불 3발을 부채꼴로(원본 `:4234`) — 플레이어를 향해 조준한다.</summary>
        void DoSpit(Transform target)
        {
            Vector3 origin = transform.position + new Vector3(mover.Direction * 0.4f, 0.7f, 0f);
            Vector2 toPlayer = (Vector2)(target.position + Vector3.up * 0.3f) - (Vector2)origin;
            float baseAngle = Mathf.Atan2(toPlayer.y, toPlayer.x);
            float dmg = mover.attackPower * SpitDamageMult;

            for (int i = 0; i < SpitCount; i++)
            {
                // 원본 `baseA + (i - (count-1)/2) * spread` — 가운데를 중심으로 좌우 대칭.
                float a = baseAngle + (i - (SpitCount - 1) / 2f) * SpitSpread;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SpitSpeed;
                EnemyBolt.Spawn(origin, v, dmg, SpitLife, projectileSprite, projectileColor);
            }
        }

        /// <summary>원본 `bossSummon`(project_test.html:4274) — 체력 66%·33%에서 각각 한 번씩.</summary>
        void CheckPhaseSummons()
        {
            if (health == null || health.MaxHp <= 0f) return;
            float ratio = health.CurrentHp / health.MaxHp;

            if (!summonedPhase1 && ratio < SummonPhase1) { summonedPhase1 = true; Summon(); }
            if (!summonedPhase2 && ratio < SummonPhase2) { summonedPhase2 = true; Summon(); }
        }

        void Summon()
        {
            for (int i = 0; i < SummonCount; i++)
            {
                // 원본: 첫 마리는 반드시 오니, 나머지는 오니/도깨비불 반반.
                var type = (i == 0 || Random.value < 0.5f) ? EnemyType.Oni : EnemyType.Wisp;
                float x = Mathf.Clamp(transform.position.x + Random.Range(-2.6f, 2.6f),
                                      FieldBounds.MinX + 0.8f, FieldBounds.MaxX - 0.8f);
                // 원본 `bossSummon`은 `protect`를 안 넘긴다(`:4278`) — 그래서 **기본 2초**다.
                // 분열귀 새끼(0.35초)와 같은 경로를 쓴다고 값까지 같으면 안 된다.
                EnemySpawnRequestBus.Request(new Vector2(x, FieldBounds.GroundY + 0.5f), type,
                                             EnemySpawnRequestBus.DefaultSpawnProtect);
            }
        }
    }
}
