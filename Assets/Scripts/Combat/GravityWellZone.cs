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
    ///
    /// 목록은 **밖에선 읽기만** 한다(2026-11-05, `HANDOFF.md` "연결 규칙" 3) — 넣고 빼는 건 이 클래스 안(생성·수명 끝·개수 상한)과
    /// <see cref="Vanish"/>뿐이다. 예전엔 공개 가변 `List`라 대붕괴가 목록을 직접 고쳤다.
    /// </summary>
    public class GravityWellZone : MonoBehaviour
    {
        static readonly List<GravityWellZone> active = new List<GravityWellZone>();

        /// <summary>살아 있는 중력점들(읽기 전용).</summary>
        public static IReadOnlyList<GravityWellZone> Active => active;

        public int Tier { get; private set; }
        public int Stack { get; private set; }
        public float Radius { get; private set; }
        public float Pull { get; private set; }
        /// <summary>스폰(또는 마지막 중첩) 이후 지난 시간. 원본 `z.t` — 대붕괴가 "가장 최근 것 우선"으로 정렬할 때 쓴다.</summary>
        public float Age { get; private set; }
        /// <summary>중력장이 사라질 때까지 남은 시간(초) — 구체 그림이 끝나갈 때 흐려지는 데 쓴다.</summary>
        public float Remaining => life - Age;

        float life;
        float dmgTickTimer;

        public static void ClearAllForTests()
        {
            for (int i = active.Count - 1; i >= 0; i--)
                if (active[i] != null) Destroy(active[i].gameObject);
            active.Clear();
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
                foreach (var well in active)
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
                        well.ShowMerged(); // 구체 그림(GravityVortexVfx)은 Stack을 읽어 스스로 커진다 — 여기선 '합쳐졌다'는 신호만
                        return well;
                    }
                }
            }

            int limit = MageSpecConfig.GravityWellLimit(tier);
            if (active.Count >= limit)
            {
                GravityWellZone oldest = null;
                float oldestAge = -1f;
                foreach (var w in active)
                {
                    if (w != null && w.Age > oldestAge) { oldestAge = w.Age; oldest = w; }
                }
                if (oldest != null) oldest.Vanish();
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
            // 예전엔 맨 바깥 보라 고리(LineRenderer)도 그렸다 — 2026-10-25 사용자 "맨 바깥 고리는 지워" → 구체 그림만. 끌어당기는 범위 판정(Radius)은 그대로다.
            GravityVortexVfx.Attach(zone);
            active.Add(zone);
            return zone;
        }

        /// <summary>'중첩 xN' 글씨 색(원본 `#d8b4ff`).</summary>
        public static readonly Color MergeTextColor = new Color(0xd8 / 255f, 0xb4 / 255f, 1f);

        /// <summary>
        /// 합쳐졌다는 신호(2026-11-09 사용자 "중첩이 제대로 되냐"·"타격감", Codex 합의) — 원본 `popDmg('중첩 x' + stack)`(:2060)·보라 파티클(:2059)을 옮겼다:
        /// 구체가 한 번 '쿵' 부풀고 보라 반짝임이 튀며, 구체 위에 "중첩 xN"이 잠깐 떠오른다(전투 HUD가 그린다). 그림만.
        /// </summary>
        void ShowMerged()
        {
            var orb = GetComponentInChildren<GravityVortexVfx>();
            if (orb != null) orb.Pulse();
            float top = GravityVortexVfx.ScaleFor(Stack) * 0.375f + 0.2f;
            CombatEvents.RaisePopup((Vector2)transform.position + new Vector2(0f, top), "중첩 x" + Stack, MergeTextColor);
        }

        /// <summary>
        /// 지금 바로 없앤다 — 목록에서 **즉시** 빼고 오브젝트를 지운다. `Destroy`는 프레임 끝에 일어나서, 같은 프레임 안의 다음 검색
        /// (대붕괴가 연달아 찾기, 새 중력탄의 중첩 검색)이 사라질 중력점을 또 찾지 않게 먼저 뺀다. 수명 끝·개수 상한·대붕괴가 쓴다.
        /// </summary>
        public void Vanish()
        {
            active.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy() => active.Remove(this);

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
                    float dmg = DamageCalculator.Roll(atk * mult, 0f, out _); // 원본 noCrit
                    target.TakeTickDamage(dmg); // 원본 kb:0 + isBurnTick(히트스톱 없음)
                }
            }

            if (Age >= life) Vanish();
        }
    }
}
