using UnityEngine;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 멧돼지(charger)의 이동 상태기계 — 원본 `updateEnemies()`의 charger 분기
    /// (project_test.html:4061~4083)와 `CONFIG.charger`(`:716`) 그대로.
    ///
    /// **접근(walk) → 예고(tele) → 질주(charge) → 지침(tired) → 접근** 순환이다.
    /// 예고와 지침 동안은 **완전히 멈춘다**(`mvx = 0`) — 이게 이 몹을 피할 수 있게 만드는 핵심이라
    /// "멈추면 어색하다"고 임의로 걷게 바꾸지 말 것.
    ///
    /// 질주 중 접촉 피해가 1.4배가 되는 건(`:4147`) <see cref="EnemyMove"/>가 아니라 여기서
    /// <see cref="IsCharging"/>으로 알려주고, 접촉 판정 쪽이 곱한다.
    /// </summary>
    [RequireComponent(typeof(EnemyMove))]
    public class EnemyChargerMotion : MonoBehaviour, IEnemyMotion, IEnemyContactDamageModifier
    {
        enum State { Walk, Telegraph, Charge, Tired }

        [Header("원본 CONFIG.charger 그대로 (거리는 100px=1유닛)")]
        [Tooltip("이 X거리 안이면 돌진을 시작한다. 원본 aggroX 300.")]
        public float aggroX = 3f;
        [Tooltip("Y차이가 이보다 크면 돌진하지 않는다(다른 층이면 안 덤빔). 원본 aggroY 90.")]
        public float aggroY = 0.9f;
        [Tooltip("돌진 예고(정지) 시간. 원본 teleT 0.5.")]
        public float telegraphTime = 0.5f;
        [Tooltip("질주 지속 시간. 원본 chargeT 0.7.")]
        public float chargeTime = 0.7f;
        [Tooltip("질주 속도. 원본 chargeSpd 500 ÷100.")]
        public float chargeSpeed = 5f;
        [Tooltip("질주 후 지침(정지) 시간. 원본 tiredT 0.9.")]
        public float tiredTime = 0.9f;
        [Tooltip("돌진 재사용 대기. 원본 cd 2.6.")]
        public float chargeCooldown = 2.6f;
        [Tooltip("질주 중 접촉 피해 배수. 원본 dmgMult 1.4(:4147).")]
        public float chargeDamageMultiplier = 1.4f;
        [Tooltip("걸어서 추적을 시작하는 거리. 원본 기본 보행과 같은 300.")]
        public float walkAggroX = 3f;

        /// <summary>질주 중인지. 접촉 피해 배수(원본 :4147)를 곱할지 판단하는 데 쓴다.</summary>
        public bool IsCharging => state == State.Charge;
        public float ContactDamageMultiplier => IsCharging ? chargeDamageMultiplier : 1f;

        State state = State.Walk;
        float stateTimer;
        float cooldownTimer;
        EnemyMove mover;

        void Awake()
        {
            mover = GetComponent<EnemyMove>();
            // 원본 `ai: { st:'walk', t:0, cd: rand(0.5, 1.5) }`(project_test.html:3966) — 스폰 직후
            // 여러 마리가 동시에 돌진하지 않게 첫 쿨다운만 무작위로 시작한다.
            cooldownTimer = Random.Range(0.5f, 1.5f);
        }

        public float GetHorizontalSpeed(float dt, Transform target, float baseSpeed)
        {
            // 원본은 상태 타이머와 쿨다운을 매 프레임 같이 깎는다(`A.t -= dt; A.cd -= dt`).
            stateTimer -= dt;
            cooldownTimer -= dt;

            float dx = target != null ? target.position.x - transform.position.x : 0f;
            float dyAbs = target != null ? Mathf.Abs(target.position.y - transform.position.y) : Mathf.Infinity;

            // ⚠️ 원본은 **속도를 먼저 정하고 그 다음에 상태를 전이한다**(project_test.html:4065~4082).
            // 그래서 상태가 바뀌는 그 프레임은 아직 "이전 상태의 속도"로 움직인다 — 예고에 들어가는
            // 프레임은 한 번 더 걷고, 지침에 들어가는 프레임은 마지막으로 한 번 더 질주한다.
            // 전이를 먼저 하면 그 한 프레임이 0이 되는데, 이게 특히 질주 끝에서 눈에 띈다
            // (원본은 끝까지 밀고 들어오는데 우리 쪽은 마지막에 살짝 멈칫한다).
            switch (state)
            {
                case State.Walk:
                {
                    // 원본: 추적 범위 안이면 방향만 맞추고 계속 걷는다.
                    if (target != null && Mathf.Abs(dx) < walkAggroX && dx != 0f)
                        mover.SetDirection(dx > 0f ? 1 : -1);

                    float mvx = mover.Direction * baseSpeed;

                    if (cooldownTimer <= 0f && target != null && Mathf.Abs(dx) < aggroX && dyAbs < aggroY)
                    {
                        state = State.Telegraph;
                        stateTimer = telegraphTime;
                        if (dx != 0f) mover.SetDirection(dx > 0f ? 1 : -1); // 예고 시점의 방향으로 고정
                    }
                    return mvx;
                }

                case State.Telegraph:
                    // 예고 중엔 멈춘다 — 피할 틈을 주는 게 이 몹의 핵심.
                    if (stateTimer <= 0f) { state = State.Charge; stateTimer = chargeTime; }
                    return 0f;

                case State.Charge:
                {
                    float mvx = mover.Direction * chargeSpeed;
                    // 원본은 맵 좌우 끝(60px 안쪽)에 닿아도 질주를 끝낸다.
                    if (stateTimer <= 0f || AtFieldEdge()) { state = State.Tired; stateTimer = tiredTime; }
                    return mvx;
                }

                default: // Tired
                    if (stateTimer <= 0f) { state = State.Walk; cooldownTimer = chargeCooldown; }
                    return 0f;
            }
        }

        bool AtFieldEdge()
        {
            const float edge = 0.6f; // 원본 `e.x < 60 || e.x > mapW - 60` ÷100
            float x = transform.position.x;
            return x < Core.FieldBounds.MinX + edge || x > Core.FieldBounds.MaxX - edge;
        }
    }
}
