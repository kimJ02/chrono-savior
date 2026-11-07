using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 불길 이동(X 스킬, 폭발 계열)이 지나간 자리에 남는 화염 장판. 원본 `spawnFlameTrail`
    /// (project_test.html:1995)로 생성되고, 틱 처리는 `updateZones()`의 `fireTrail` 분기
    /// (project_test.html:3844-3857) 그대로 — 화상 스택을 얹는 것과, 트레일 자체의 직접 틱 피해는
    /// 서로 다른 두 개의 독립된 피해원이다(원본 그대로, 하나로 합치지 않는다).
    ///
    /// **그림**(2026-11-07 VFX 스프린트, 팀원 문서 "경로 따라 일정 간격"): 장판마다 Pixel Fire 불꽃(`mage/fire_trail`, 32×48 × 2)이 되풀이되고
    /// 끝나기 <see cref="FadeTime"/>초 전부터 흐려진다. 불꽃 밑동은 마법사 발 높이(장판 중심 − <see cref="MageSpecConfig.TeleportFlameLineHeight"/>)에 둔다.
    /// 판정 반경(0.66~0.98)과 그림 크기는 따로다. 그림이 없으면(팩을 안 가져온 클론) 예전처럼 안 보인다.
    /// </summary>
    public class FireTrailZone : MonoBehaviour
    {
        /// <summary>불꽃 그림 배율(정수 — 도트 배율 2)·재생 속도·끝에 흐려지는 시간(초).</summary>
        public const int FlameScale = 2;
        public const float FlameFps = 12f, FadeTime = 0.3f;
        /// <summary>정렬: 땅(-5) 위, 캐릭터·적(0)과 잔상(-1) **뒤** — 불꽃(0.75유닛)이 캐릭터(0.5)보다 커서 앞에 두면 도착한 마법사를 가렸다(캡처). CLAUDE.md 예약표.</summary>
        public const int FlameSortingOrder = -2;

        float radius, life, tick, damageMult, power, age, tickTimer;
        int burnStacks;
        VfxPlayer flame;

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
            z.flame = SpawnFlame(go.transform);
            return z;
        }

        static VfxPlayer SpawnFlame(Transform zone)
        {
            var frames = VfxFrames.Get(VfxKeys.MageFireTrail);
            if (frames.Length == 0) return null;
            var go = new GameObject("Flame", typeof(SpriteRenderer));
            go.transform.SetParent(zone, false);
            float height = frames[0].bounds.size.y * FlameScale;
            go.transform.localPosition = new Vector3(0f, height * 0.5f - MageSpecConfig.TeleportFlameLineHeight, 0f);
            go.transform.localScale = new Vector3(FlameScale, FlameScale, 1f);
            go.GetComponent<SpriteRenderer>().sortingOrder = FlameSortingOrder;
            var player = go.AddComponent<VfxPlayer>();
            player.Play(frames, FlameFps, true, 0f, Color.white);
            return player;
        }

        /// <summary>남은 수명에 따른 불꽃 투명도 — 끝나기 <see cref="FadeTime"/>초 전부터 0으로.</summary>
        public static float FlameAlpha(float remaining) => Mathf.Clamp01(remaining / FadeTime);

        /// <summary>불꽃 그림(테스트용 — 그림이 없으면 null).</summary>
        public VfxPlayer Flame => flame;

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
            if (flame != null) flame.Alpha = FlameAlpha(life - age);
            if (age >= life) Destroy(gameObject);
        }
    }
}
