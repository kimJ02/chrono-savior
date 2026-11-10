using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 덩어리 지배자의 대형 조약돌(2026-11-10 팀원 명세 패턴 2). **직선**, 유도 없음, 폭발 없음 — 플레이어에 닿으면 한 번 피해를 주고 사라지고,
    /// 안 맞으면 던질 때 정한 끝점(바닥·발판 윗면 또는 맵 끝)까지 날아가 거기서 부서진다(명세 "경고 위치를 벗어나도 원래 목표로 계속").
    /// 끝점은 던지는 쪽(<see cref="BlobLordBoss"/> — 플레이어 발밑 표면)이 정해 경고를 같은 자리에 띄운다 — 그래서 경고와 실제 궤적이 어긋나지 않는다.
    /// 사이에 발판이 있어도 지나간다(한쪽 방향 발판 — 보스가 발판 밑에 서 있을 때 머리 위 발판에 바로 떨어지던 문제, 2026-11-10 캡처).
    /// 구성은 프리팹(`Prefabs/Combat/BlobLordBoulder` — 트리거 원·Kinematic 리지드바디·그림)에 있다.
    /// </summary>
    public class BoulderProjectile : MonoBehaviour
    {
        /// <summary>투사체 3 — CLAUDE.md 정렬표.</summary>
        public const int SortingOrder = 3;
        /// <summary>안전장치 — 끝점까지 못 가도 이 시간이 지나면 사라진다.</summary>
        public const float MaxLife = 4f;
        public const float SpinDegPerSec = 360f;

        [SerializeField] Rigidbody2D rb;
        [SerializeField] SpriteRenderer visual;

        Vector2 start, dir;
        float travel, damage, life;
        bool done;

        /// <summary>바닥·벽에 닿아 사라졌다(맞히고 사라진 건 아님) — 쏜 쪽이 경고를 지우고 흔든다.</summary>
        public event System.Action<BoulderProjectile> Landed;
        /// <summary>어떻게든 사라졌다(맞힘·떨어짐 모두) — 쏜 쪽이 경고를 지운다. 맞혔을 때 경고가 남던 문제(2026-11-10 캡처).</summary>
        public event System.Action<BoulderProjectile> Finished;
        public bool HitPlayer { get; private set; }
        public Vector2 End => start + dir * travel;

        public static BoulderProjectile Spawn(BoulderProjectile prefab, Vector2 from, Vector2 to, float speed, float damage)
        {
            if (prefab == null) { Debug.LogError("[BoulderProjectile] 조약돌 프리팹이 없다 — 덩어리 지배자의 boulderPrefab을 확인"); return null; }
            var b = Instantiate(prefab, from, Quaternion.identity); // RunTransient가 붙어 있어 런이 끝나면 사라진다
            b.name = prefab.name;
            b.start = from;
            Vector2 d = to - from;
            b.travel = d.magnitude;
            b.dir = b.travel > 0.0001f ? d / b.travel : Vector2.right;
            b.damage = damage;
            b.rb.linearVelocity = b.dir * speed;
            if (b.visual != null) b.visual.sortingOrder = SortingOrder;
            return b;
        }

        void Update()
        {
            if (done) return;
            life += Time.deltaTime;
            if (visual != null) visual.transform.Rotate(0f, 0f, -Mathf.Sign(dir.x == 0f ? 1f : dir.x) * SpinDegPerSec * Time.deltaTime);
            // 끝점을 지나쳤으면(프레임 사이) 끝점에 놓고 부서진다
            if (Vector2.Dot((Vector2)transform.position - start, dir) >= travel || life >= MaxLife)
            {
                transform.position = End;
                Finish(landed: true);
            }
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (done || !other.CompareTag("Player")) return;
            var target = other.GetComponent<IDamageable>();
            if (target != null && !target.IsDead) target.TakeDamage(damage, gameObject);
            HitPlayer = true;
            Finish(landed: false);
        }

        void Finish(bool landed)
        {
            done = true;
            rb.linearVelocity = Vector2.zero;
            if (landed) Landed?.Invoke(this);
            Finished?.Invoke(this);
            Destroy(gameObject);
        }
    }
}
