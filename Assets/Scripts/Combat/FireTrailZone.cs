using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 불길 이동(X 스킬, 폭발 계열)이 지나간 자리에 남는 화염 장판. 원본 `spawnFlameTrail`
    /// (project_test.html:1995)로 생성되고, 틱 처리는 `updateZones()`의 `fireTrail` 분기
    /// (project_test.html:3844-3857) 그대로 — 화상 스택을 얹는 것과, 트레일 자체의 직접 틱 피해는
    /// 서로 다른 두 개의 독립된 피해원이다(원본 그대로, 하나로 합치지 않는다).
    /// </summary>
    public class FireTrailZone : MonoBehaviour
    {
        float radius, life, tick, damageMult, power, age, tickTimer;
        int burnStacks;

        public static FireTrailZone Spawn(Vector2 pos, int tier, float power)
        {
            var go = new GameObject("FireTrailZone");
            // 런이 끝나거나 새로 시작하면 사라져야 한다(원본 `projectiles = []`/`zones = []`, :4318).
            go.AddComponent<RunTransient>();
            go.transform.position = pos;
            var z = go.AddComponent<FireTrailZone>();
            z.radius = MageSpecConfig.FlameTrailRadius(tier);
            z.life = MageSpecConfig.FlameTrailLife(tier);
            z.tick = MageSpecConfig.FlameTrailTickInterval;
            z.damageMult = MageSpecConfig.FlameTrailDamageMult(tier);
            z.burnStacks = MageSpecConfig.FlameTrailBurnStacks(tier);
            z.power = power;
            return z;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            tickTimer += dt;
            if (tickTimer >= tick)
            {
                tickTimer -= tick;
                float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
                foreach (var col in Physics2D.OverlapCircleAll(transform.position, radius))
                {
                    if (!col.CompareTag("Enemy")) continue;
                    var protectable = col.GetComponent<ISpawnProtectable>();
                    if (protectable != null && protectable.IsSpawnProtected) continue;

                    col.GetComponent<IElementAfflictable>()?.ApplyBurn(burnStacks);

                    var target = col.GetComponent<IDamageable>();
                    if (target != null && !target.IsDead)
                    {
                        float dmg = DamageCalculator.Roll(atk * damageMult * power, 0f, out _); // 원본 noCrit(isBurnTick)
                        target.TakeTickDamage(dmg); // 원본 kb:0 + isBurnTick(히트스톱 없음)
                    }
                }
            }
            if (age >= life) Destroy(gameObject);
        }
    }
}
