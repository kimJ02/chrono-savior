using UnityEngine;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 해골 궁수(shooter)의 거리 유지 + 사격 — 원본 `updateEnemies()`의 shooter 분기
    /// (project_test.html:4084~4102)와 `CONFIG.shooter`(`:717`) 그대로.
    ///
    /// **너무 가까우면 물러나고, 너무 멀면 다가가고, 사거리 안(300~500px)에 들어오면 멈춰서 쏜다.**
    /// 쏘는 건 사거리 안에 들어와 멈췄을 때만이다 — 이동 중엔 쏘지 않는다(원본 구조 그대로).
    /// </summary>
    [RequireComponent(typeof(EnemyMove))]
    public class EnemyShooterMotion : MonoBehaviour, IEnemyMotion
    {
        [Header("원본 CONFIG.shooter 그대로 (거리는 100px=1유닛)")]
        [Tooltip("이보다 가까우면 물러난다. 원본 keepMin 300.")]
        public float keepMin = 3f;
        [Tooltip("이보다 멀면 다가간다. 원본 keepMax 500.")]
        public float keepMax = 5f;
        [Tooltip("사격 간격. 원본 shootCd 2.4.")]
        public float shootCooldown = 2.4f;
        [Tooltip("화염탄 속도. 원본 boltSpd 380 ÷100.")]
        public float boltSpeed = 3.8f;
        [Tooltip("화염탄 수명. 원본 boltLife 3.0.")]
        public float boltLife = 3f;

        [Header("발사 지점 (원본 e.x + dir*14, e.y - h*0.6)")]
        public float muzzleForward = 0.14f;
        public float muzzleHeight = 0.28f;
        [Tooltip("조준점 보정. 원본은 플레이어 y-30을 노린다.")]
        public float aimHeightOffset = 0.30f;

        [Tooltip("화염탄 프리팹(Assets/Prefabs/Combat/EnemyBolt) — Enemy_Shooter 프리팹에 들어 있다.")]
        [SerializeField] EnemyBolt boltPrefab;

        /// <summary>프리팹 검증과 테스트가 읽고 넣는다.</summary>
        public EnemyBolt BoltPrefab { get => boltPrefab; set => boltPrefab = value; }

        [Tooltip("탄 그림 — 스포너가 이 몹의 그림을 넣는다(비어 있으면 프리팹 그림).")]
        public Sprite boltSprite;
        public Color boltColor = new Color(0.78f, 0.42f, 1f);

        float shootTimer;
        EnemyMove mover;

        void Awake()
        {
            mover = GetComponent<EnemyMove>();
            // 원본은 스폰 시 `shootCd: rand(1.0, 2.0)`로 시작한다(:3967) — 여러 마리가 동시에 쏘지 않게.
            shootTimer = Random.Range(1f, 2f);
        }

        public float GetHorizontalSpeed(float dt, Transform target, float baseSpeed)
        {
            shootTimer -= dt;
            if (target == null) return 0f;

            float dx = target.position.x - transform.position.x;
            float adx = Mathf.Abs(dx);
            int towards = dx > 0f ? 1 : -1;

            float mvx;
            if (adx < keepMin) mvx = -towards * baseSpeed;       // 너무 가깝다 → 물러난다
            else if (adx > keepMax) mvx = towards * baseSpeed;   // 너무 멀다 → 다가간다
            else
            {
                // 사거리 안 — 멈춰서 조준하고 쏜다.
                mover.SetDirection(towards);
                if (shootTimer <= 0f)
                {
                    shootTimer = shootCooldown;
                    FireAt(target);
                }
                return 0f;
            }

            // 원본 `if (mvx !== 0) e.dir = Math.sign(mvx)` — 물러날 땐 물러나는 쪽을 본다.
            mover.SetDirection(mvx > 0f ? 1 : -1);
            return mvx;
        }

        void FireAt(Transform target)
        {
            Vector3 origin = transform.position + new Vector3(mover.Direction * muzzleForward, muzzleHeight, 0f);
            Vector3 aim = target.position + new Vector3(0f, aimHeightOffset, 0f);
            Vector2 dir = ((Vector2)(aim - origin)).normalized;
            if (dir.sqrMagnitude < 0.0001f) dir = new Vector2(mover.Direction, 0f);

            // 피해량은 이 몹의 접촉 피해와 같은 값을 쓴다(원본 `dmg: e.dmg`) — 지역 배율이 이미 반영돼 있다.
            // 성소 버프(×1.3)도 원본이 여기서 같이 곱한다(`:4098`).
            float dmg = mover.attackPower * Core.CombatModifiers.EnemyDamageMultiplier;
            EnemyBolt.Spawn(boltPrefab, origin, dir * boltSpeed, dmg, boltLife, boltSprite, boltColor);
        }
    }
}
