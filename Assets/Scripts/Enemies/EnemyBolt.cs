using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 해골 궁수가 쏘는 화살. 원본 `projectiles`의 `kind:'ebolt'`(project_test.html:4098 생성,
    /// `:3732`~`:3742` 갱신)에 대응한다 — **적이 소유한 투사체**라 플레이어만 맞힌다.
    ///
    /// 원본은 관통이 없고 플레이어에 닿는 즉시 사라지며, 수명이 다하거나 지면 아래로 내려가도 사라진다.
    /// </summary>
    public class EnemyBolt : MonoBehaviour
    {
        // 원본 판정 `rectsOverlap(w.x-9, w.y-9, 18, 18, ...)`(:3735) — 18×18px ÷100.
        const float HalfSize = 0.09f;

        float damage;
        float life;

        // 구성은 프리팹(Assets/Prefabs/Combat/EnemyBolt)에 있다(2026-11-05 생성 책임 2-3) — 18×18px 판정 상자·리지드바디·그림 자식(기본 그림 = 기본 몹 그림).
        [SerializeField] Rigidbody2D rb;
        [SerializeField] SpriteRenderer visualRenderer;

        /// <summary>
        /// 탄 하나를 쏜다 — 쏘는 쪽(해골 궁수·보스)이 들고 있는 프리팹을 세운다. 프리팹이 없으면 오류.
        /// <paramref name="sprite"/>가 있으면 그림을 갈아 끼운다(해골 궁수는 자기 그림을 줄여 쏜다), 없으면 프리팹 그림.
        /// </summary>
        public static EnemyBolt Spawn(EnemyBolt prefab, Vector3 pos, Vector2 velocity, float damage, float life, Sprite sprite, Color color)
        {
            if (prefab == null) { Debug.LogError("[EnemyBolt] 적 탄 프리팹이 없다 — 쏘는 몹의 boltPrefab을 확인"); return null; }
            var bolt = Instantiate(prefab, pos, Quaternion.identity); // RunTransient가 붙어 있어 런이 끝나면 사라진다(원본 `projectiles = []`, :4318)
            bolt.name = prefab.name; // "(Clone)" 없이 — 계층 창·디버그에서 종류 이름 그대로
            if (sprite != null) bolt.visualRenderer.sprite = sprite;
            bolt.visualRenderer.color = color;
            bolt.rb.linearVelocity = velocity;
            bolt.damage = damage;
            bolt.life = life;
            return bolt;
        }

        void Update()
        {
            // 원본 `w.t >= w.life || w.y > groundY + 20`(:3741) — 우리 좌표계는 Y+가 위라 부호를 뒤집는다.
            life -= Time.deltaTime;
            if (life <= 0f || transform.position.y < FieldBounds.GroundY - 0.2f) Destroy(gameObject);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player")) return;

            // 피해 난수(±10%)는 원본이 피격자 쪽(`damagePlayer`)에서 굴리므로 여기선 그대로 넘긴다.
            var target = other.GetComponent<IDamageable>();
            if (target != null && !target.IsDead) target.TakeDamage(damage, gameObject);

            Destroy(gameObject); // 원본도 명중 즉시 사라진다(관통 없음)
        }
    }
}
