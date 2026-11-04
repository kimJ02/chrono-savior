using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 사수귀가 쏘는 도깨비 화염탄. 원본 `projectiles`의 `kind:'ebolt'`(project_test.html:4098 생성,
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

        public static EnemyBolt Spawn(Vector3 pos, Vector2 velocity, float damage, float life, Sprite sprite, Color color)
        {
            var go = new GameObject("EnemyBolt");
            // 런이 끝나거나 새로 시작하면 사라져야 한다(원본 `projectiles = []`/`zones = []`, :4318).
            go.AddComponent<RunTransient>();
            go.transform.position = pos;

            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = Vector3.one * (HalfSize * 2f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 3;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(HalfSize * 2f, HalfSize * 2f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearVelocity = velocity;

            var bolt = go.AddComponent<EnemyBolt>();
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
