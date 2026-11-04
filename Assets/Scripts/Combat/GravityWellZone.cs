using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Combat
{
    /// <summary>
    /// 중력 계열 마법사의 중력점. 원본 `spawnGravityWell`(생성/중첩, project_test.html:2044)과
    /// `updateZones()`의 `gravityWell` 분기(끌어당김·틱 피해, project_test.html:3806-3843)를 옮겼다.
    ///
    /// 활성 인스턴스를 <see cref="Active"/>에 정적으로 등록해 두 곳에서 참조한다: (1) 새 중력탄이
    /// 착탄했을 때 "근처에 살아있는 중력점이 있으면 중첩만 늘린다"는 검색(<see cref="SpawnOrMerge"/>),
    /// (2) X 스킬 대붕괴(`MageSkillEffects.GravityCollapse`)가 터뜨릴 대상 목록을 얻을 때.
    /// `ProfileService`와 같은 정적 상태라 테스트 간 오염을 막으려면 <see cref="ClearAllForTests"/>를
    /// 반드시 `[TearDown]`에서 불러야 한다.
    /// </summary>
    public class GravityWellZone : MonoBehaviour
    {
        public static readonly List<GravityWellZone> Active = new List<GravityWellZone>();

        public int Tier { get; private set; }
        public int Stack { get; private set; }
        public float Radius { get; private set; }
        public float Pull { get; private set; }
        /// <summary>스폰(또는 마지막 중첩) 이후 지난 시간. 원본 `z.t` — 대붕괴가 "가장 최근 것 우선"으로 정렬할 때 쓴다.</summary>
        public float Age { get; private set; }
        /// <summary>중력장이 사라질 때까지 남은 시간(초) — 소용돌이 그림이 끝나갈 때 흐려지는 데 쓴다.</summary>
        public float Remaining => life - Age;

        float life;
        float dmgTickTimer;
        GravityVortexVfx vortex;

        public static void ClearAllForTests()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
                if (Active[i] != null) Destroy(Active[i].gameObject);
            Active.Clear();
        }

        public static GravityWellZone SpawnOrMerge(Vector2 pos, int tier, float chargeK)
        {
            if (tier <= 0) return null;
            // 원본 `y = Math.min(y, groundY - 20)`(:2047)은 Y+가 아래인 캔버스에서 "바닥보다 아래로 못 간다"는 뜻이다.
            // Unity는 Y+가 위라 `Max`여야 한다 — 예전엔 `Min`이라 중력점이 **몇 층에서 터지든 늘 바닥 아래(-0.2)**에
            // 생겼다(2026-10-03 원본 대조에서 발견). 같은 실수를 `MageSkillEffects.DetonateGravityOrb`는 09-28에 고쳤다.
            // (팀원도 10/07에 따로 찾아 같은 줄로 고쳤다 — 2026-10-14 병합 때 하나로 합침.)
            pos.y = Mathf.Max(pos.y, FieldBounds.GroundY + MageSpecConfig.GravityGroundClearance);
            int stackGain = MageSpecConfig.GravityStackGain(tier, chargeK);

            // 원본: 2티어부터만 "근처 기존 중력점에 중첩"이 가능하다(project_test.html:2050).
            if (tier >= 2)
            {
                foreach (var well in Active)
                {
                    if (well == null) continue;
                    float mergeR = MageSpecConfig.GravityWellMergeRadiusBase + well.Stack * MageSpecConfig.GravityWellMergeRadiusPerStack;
                    if (Vector2.Distance(well.transform.position, pos) < mergeR)
                    {
                        well.Stack = Mathf.Clamp(well.Stack + stackGain, 1, 5);
                        well.Age = 0f;
                        well.life = Mathf.Max(well.life, MageSpecConfig.GravityWellLife(tier, chargeK));
                        well.Radius = MageSpecConfig.GravityWellRadius(tier, well.Stack, chargeK);
                        well.Pull = MageSpecConfig.GravityWellPull(tier, well.Stack, chargeK);
                        well.UpdateRingRadius();
                        return well;
                    }
                }
            }

            int limit = MageSpecConfig.GravityWellLimit(tier);
            if (Active.Count >= limit)
            {
                GravityWellZone oldest = null;
                float oldestAge = -1f;
                foreach (var w in Active)
                {
                    if (w != null && w.Age > oldestAge) { oldestAge = w.Age; oldest = w; }
                }
                if (oldest != null)
                {
                    Active.Remove(oldest);
                    Destroy(oldest.gameObject);
                }
            }

            var go = new GameObject("GravityWellZone");
            // 런이 끝나거나 새로 시작하면 사라져야 한다(원본 `projectiles = []`/`zones = []`, :4318).
            go.AddComponent<RunTransient>();
            go.transform.position = pos;
            var zone = go.AddComponent<GravityWellZone>();
            zone.Tier = tier;
            zone.Stack = stackGain;
            zone.life = MageSpecConfig.GravityWellLife(tier, chargeK);
            zone.Radius = MageSpecConfig.GravityWellRadius(tier, stackGain, chargeK);
            zone.Pull = MageSpecConfig.GravityWellPull(tier, stackGain, chargeK);
            zone.SetupRing();
            Active.Add(zone);
            return zone;
        }

        void SetupRing()
        {
            // 예전엔 여기서 맨 바깥 보라 고리(LineRenderer)를 그렸다 — 2026-10-25 사용자 "맨 바깥 고리는 지워" → 소용돌이 그림만 남긴다.
            // 끌어당기는 범위 판정(Radius)은 그대로다.
            vortex = GravityVortexVfx.Attach(this);
            UpdateRingRadius();
        }

        void UpdateRingRadius()
        {
            if (vortex != null) vortex.SetRadius(Radius);
        }

        void OnDestroy() => Active.Remove(this);

        void Update()
        {
            float dt = Time.deltaTime;
            Age += dt;

            float pullR = Radius + MageSpecConfig.GravityPullRangeExtra(Stack);
            var affected = new List<(IDamageable target, float nearBonus)>();

            foreach (var col in Physics2D.OverlapCircleAll(transform.position, pullR))
            {
                if (!col.CompareTag("Enemy")) continue;
                var protectable = col.GetComponent<ISpawnProtectable>();
                if (protectable != null && protectable.IsSpawnProtected) continue;

                Vector2 toWell = (Vector2)transform.position - (Vector2)col.transform.position;
                float d = Mathf.Max(toWell.magnitude, 0.01f);

                var pullable = col.GetComponent<IGravityAffectable>();
                if (pullable != null)
                {
                    float nearEase = Mathf.Clamp(1f - d / pullR, 0.12f, 1f);
                    float force = Pull * (0.45f + nearEase) * (1f + Stack * 0.18f);
                    Vector2 dir = toWell / d;
                    pullable.ApplyGravityWellPull(new Vector2(dir.x * force * dt, dir.y * force * dt * 0.28f));
                }

                // 원본 `e.gravityExpose += dt * (1 + stack*0.16)` → 1.25 넘으면 취약 3초(`:3817`~`:3819`).
                // 끌어당김·틱 피해와 달리 **범위 안에 있기만 하면** 누적된다(보스 예외 없음).
                var vulnerable = col.GetComponent<IVulnerable>();
                vulnerable?.AddGravityExposure(dt * (1f + Stack * MageSpecConfig.GravityExposureRatePerStack));

                var target = col.GetComponent<IDamageable>();
                if (target != null && !target.IsDead)
                    affected.Add((target, Mathf.Clamp(1f - d / pullR, 0f, 1f) * 0.05f));
            }

            dmgTickTimer += dt;
            float tickInterval = MageSpecConfig.GravityDamageTickInterval(Stack);
            if (dmgTickTimer >= tickInterval)
            {
                dmgTickTimer -= tickInterval;
                float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
                foreach (var (target, nearBonus) in affected)
                {
                    if (target.IsDead) continue;
                    float mult = MageSpecConfig.GravityTickDamageMult(Tier, Stack, nearBonus);
                    int dmg = DamageCalculator.Roll(atk * mult, 0f, out _); // 원본 noCrit
                    target.TakeTickDamage(dmg); // 원본 kb:0 + isBurnTick(히트스톱 없음)
                }
            }

            if (Age >= life)
            {
                Active.Remove(this);
                Destroy(gameObject);
            }
        }
    }
}
