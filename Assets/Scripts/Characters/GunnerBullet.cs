using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉이 쏘는 총알 하나. 원본 `projectiles`의 `kind:'bullet'`(project_test.html:2198 생성,
    /// `:3695`~`:3730` 갱신)에 대응한다. 캐릭터의 Z 총알과 레이저 드론의 탄이 같은 종류다.
    ///
    /// 마법사의 마법탄(<see cref="MageProjectile"/>)과 판정 크기·넉백·원소 적용이 전부 다르다:
    /// - 판정: 원본 `rectsOverlap(x-13*size, y-5*size, 26*size, 10*size, ...)`(`:3719`) — 26×10px(0.26×0.10유닛)
    /// - 넉백: `kb: 95`, 방향은 **총알 진행 방향**(`kbDir: sign(w.vx)`, `:3721`)
    /// - `skill: false`라 원소(화상 등)를 묻히지 않는다
    ///
    /// 전문화에 따라 켜지는 것(원본 `gunnerFire` opts / 드론 탄):
    /// - **유도**(`homing`): 목표가 없거나 죽었거나 이미 맞힌 적이면 탐지 거리 안의 가장 가까운(아직 안 맞힌) 적을 새로
    ///   고르고, 속력은 유지한 채 `min(1, dt*turnRate)` 비율로 그쪽으로 방향을 섞는다(`:3696`~`:3712`).
    /// - **충전 스택**(`stackOnHit`): 적중할 때마다 메카닉 스택/부품을 쌓는다(`:3722`). 드론 탄은 0.
    /// </summary>
    public class GunnerBullet : MonoBehaviour
    {
        // 원본 판정 절반 크기(13px, 5px → 0.13, 0.05유닛). size 배수를 곱해 실제 콜라이더를 만든다.
        const float BaseHalfWidth = 0.13f;
        const float BaseHalfHeight = 0.05f;
        /// <summary>원본 `kb: 95`(project_test.html:3721) ÷100.</summary>
        const float HitKnockback = 0.95f;
        /// <summary>원본 `w.seekRange || 420` — 유도탄인데 탐지 거리가 없으면 4.2유닛.</summary>
        const float DefaultSeekRange = 4.2f;
        /// <summary>원본 `w.turnRate || 10`.</summary>
        const float DefaultTurnRate = 10f;

        float damage;
        int pierceLeft;
        float life;
        float knockbackDirSign;
        bool homing;
        float seekRange;
        float turnRate;
        GunnerAttack stackOwner;
        int stackOnHit;
        Rigidbody2D rb;
        Transform visualTf;
        Collider2D homingTarget;
        readonly HashSet<Collider2D> alreadyHit = new HashSet<Collider2D>();

        /// <summary>지금 유도 중인 적(테스트·디버그용).</summary>
        public Collider2D HomingTarget => homingTarget;

        public static GunnerBullet Spawn(Vector3 pos, Vector2 velocity, float damage, int pierce, float life,
            float sizeMul, Sprite sprite, Color color,
            bool homing = false, float seekRange = 0f, float turnRate = 0f,
            GunnerAttack stackOwner = null, int stackOnHit = 0)
        {
            var go = new GameObject("GunnerBullet");
            // 런이 끝나거나 새로 시작하면 사라져야 한다(원본 `projectiles = []`/`zones = []`, :4318).
            go.AddComponent<RunTransient>();
            go.transform.position = pos;

            // 판정과 비주얼을 분리해서 스케일이 이중으로 곱해지는 걸 막는다(MageProjectile과 같은 이유).
            var visual = new GameObject("Visual");
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = new Vector3(BaseHalfWidth * 2f * sizeMul, BaseHalfHeight * 2f * sizeMul, 1f);
            var sr = visual.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = 3;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(BaseHalfWidth * 2f * sizeMul, BaseHalfHeight * 2f * sizeMul);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearVelocity = velocity;

            var bullet = go.AddComponent<GunnerBullet>();
            bullet.rb = rb;
            bullet.visualTf = visual.transform;
            bullet.damage = damage;
            bullet.pierceLeft = pierce;
            bullet.life = life;
            bullet.knockbackDirSign = velocity.x >= 0f ? 1f : -1f; // 원본 kbDir: sign(w.vx)
            bullet.homing = homing;
            bullet.seekRange = seekRange > 0f ? seekRange : DefaultSeekRange;
            bullet.turnRate = turnRate > 0f ? turnRate : DefaultTurnRate;
            bullet.stackOwner = stackOwner;
            bullet.stackOnHit = stackOnHit;
            bullet.OrientVisual(velocity);
            return bullet;
        }

        void Update()
        {
            if (homing) Steer(Time.deltaTime);

            // 원본 `w.t >= w.life || w.x < -60 || w.x > mapW + 60`(project_test.html:3729).
            life -= Time.deltaTime;
            if (life <= 0f
                || transform.position.x < FieldBounds.MinX - 0.6f
                || transform.position.x > FieldBounds.MaxX + 0.6f)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>원본 유도(`:3696`~`:3712`) — 속력은 그대로 두고 방향만 목표 쪽으로 섞는다.</summary>
        void Steer(float dt)
        {
            if (rb == null) return;
            if (!GunnerTargets.TryMake(homingTarget, out _) || alreadyHit.Contains(homingTarget))
            {
                homingTarget = null;
                float bestD = seekRange;
                Vector2 pos = transform.position;
                foreach (var e in GunnerTargets.Query(pos, seekRange + GunnerTargets.QueryMargin))
                {
                    if (alreadyHit.Contains(e.collider)) continue;
                    float d = Vector2.Distance(e.center, pos);
                    if (d < bestD) { bestD = d; homingTarget = e.collider; }
                }
            }
            if (!GunnerTargets.TryMake(homingTarget, out var t)) return;

            Vector2 v = rb.linearVelocity;
            Vector2 to = t.center - (Vector2)transform.position;
            float len = to.magnitude;
            if (len == 0f) len = 1f;
            float sp = v.magnitude;
            if (sp == 0f) sp = 13.2f; // 원본 `|| 1320`
            float steer = Mathf.Min(1f, dt * turnRate);
            rb.linearVelocity = Vector2.Lerp(v, to / len * sp, steer);
            OrientVisual(rb.linearVelocity);
        }

        /// <summary>
        /// **그림만** 진행 방향으로 돌린다. 판정 상자(루트의 BoxCollider2D)는 돌리지 않는다 — 원본 판정이
        /// 진행 방향과 무관한 축 정렬 사각형(`rectsOverlap(x-13*size, y-5*size, 26*size, 10*size)`)이라서다.
        /// </summary>
        void OrientVisual(Vector2 v)
        {
            if (visualTf == null || v.sqrMagnitude < 1e-6f) return;
            visualTf.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
        }

        void OnTriggerEnter2D(Collider2D other)
        {
            // 관통을 다 쓴 총알은 Destroy가 프레임 끝에야 반영돼서, 같은 물리 스텝에 겹친 다른 적의
            // 트리거가 한 번 더 들어올 수 있다 — 원본은 적중 즉시 remove라 추가 적중이 없다.
            if (pierceLeft <= 0) return;
            if (!other.CompareTag("Enemy")) return;
            // 원본은 스폰 무적 대상을 hitSet에 아예 안 넣는다 — 관통도 안 깎이고 무적이 풀리면 다시 걸린다.
            var protectable = other.GetComponent<ISpawnProtectable>();
            if (protectable != null && protectable.IsSpawnProtected) return;
            if (!alreadyHit.Add(other)) return;

            var target = other.GetComponent<IDamageable>();
            if (target != null && !target.IsDead)
            {
                // 치명타·±10% 난수는 적중할 때마다 새로 굴린다(원본 dealDamage).
                float critChance = PlayerStatCalculator.ComputeCritChance(ProfileService.Current);
                int dmg = DamageCalculator.Roll(damage, critChance, out _);
                // 넉백 방향은 **지금** 진행 방향(유도탄은 휘면서 바뀐다) — 원본 `kbDir: Math.sign(w.vx)`.
                if (rb != null && Mathf.Abs(rb.linearVelocity.x) > 1e-4f)
                    knockbackDirSign = rb.linearVelocity.x > 0f ? 1f : -1f;
                target.TakeDamageWithKnockback(dmg, null, knockbackDirSign, HitKnockback, HitFeel.HitstopNone); // 원본 noHitstop(:3721)
            }

            // 원본 `if (w.stackOnHit) addGunnerStack(w.stackOnHit)`(:3722) — 갈래가 없으면 AddStack이 무시한다.
            if (stackOnHit > 0 && stackOwner != null) stackOwner.AddStack(stackOnHit);

            // 원본 `w.pierceLeft--; if (w.pierceLeft <= 0) remove = true;`(project_test.html:3723-3725).
            pierceLeft--;
            if (pierceLeft <= 0) Destroy(gameObject);
        }
    }
}
