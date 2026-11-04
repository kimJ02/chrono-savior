using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 레이저 빌드의 **Z 평타 광선** — 레이저 1층부터 Z를 누르고 있는 동안 총알 대신 전방으로 뻗는
    /// 관통 광선이 나간다. 원본 `updateGunnerBeam(dt, held)`(project_test.html:2224~:2333)를 그대로 옮겼다.
    ///
    /// - 1~2층: 정면 직선. 틱마다 광선 위(폭 + 적 폭×0.35 이내)의 적 전부에게 피해 + 적중마다 충전 스택 +1.
    /// - 3층부터: 정면 콘(3층 ±0.26rad·4층 ±0.44·5층 ±0.61) 안의 가장 가까운 적 쪽으로 방향이 보정된다.
    ///   한번 잡은 목표는 죽거나·멀어지거나·콘 밖으로 나갈 때까지 유지한다(빔이 이리저리 튀지 않게).
    ///   콘 안에 적이 없으면 사거리 55%로 짧아지고 살짝 흔들린다.
    /// - 5층: 총구에서 앞으로 뻗다가 목표로 **휘는 곡선**(2차 베지어)이 되고, 프레임마다 부드럽게 따라간다.
    ///
    /// 틱 피해는 원본 `skill:false, kb:0, noHitstop` — 치명타는 되지만 넉백·히트스톱이 없어서
    /// <see cref="IDamageable.TakeTickDamage"/>로 보낸다(일반 경로는 적중마다 히트스톱이 걸린다).
    /// 틱 간격엔 공격속도만 곱하고 쿨감(cdMult)은 곱하지 않는다(원본 그대로).
    ///
    /// 상태가 캐릭터에 붙어 있으므로(원본 `p.gunnerBeam`) MonoBehaviour가 아니라 <see cref="GunnerAttack"/>가 들고 있는 객체다.
    /// </summary>
    public class GunnerLaserBeam
    {
        readonly GunnerAttack kit;
        readonly CharacterMover2D mover;

        /// <summary>이번 프레임에 광선이 켜져 있는지(원본 `p.gunnerBeam != null`).</summary>
        public bool Active { get; private set; }
        public Vector2 Origin { get; private set; }
        /// <summary>광선 방향(단위 벡터). 곡선이면 곡선 끝점 방향.</summary>
        public Vector2 Direction { get; private set; }
        /// <summary>직선 광선 사거리(목표가 없으면 이미 55%로 줄어든 값).</summary>
        public float Range { get; private set; }
        public float Width { get; private set; }
        public bool Curved { get; private set; }
        public Vector2 CurveControl { get; private set; }
        public Vector2 CurveEnd { get; private set; }
        /// <summary>유도 목표(원본 `p.gunnerBeamTarget`). 3층 미만이면 항상 null.</summary>
        public Collider2D Target { get; private set; }

        // 원본 `p.gunnerBeamTick` — 광선이 켜져 있을 때만 쌓이고, 뗐다고 초기화되진 않는다(원본 그대로).
        float tickTimer;
        // 원본 `p.gunnerBeamCurve` — 직전 프레임 곡선(부드럽게 따라가기용).
        bool hasPrevCurve;
        Vector2 prevControl, prevEnd;

        GameObject visualRoot;
        SpriteRenderer[] segments;

        public GunnerLaserBeam(GunnerAttack kit, CharacterMover2D mover)
        {
            this.kit = kit;
            this.mover = mover;
        }

        /// <summary>
        /// 원본 `updateGunnerBeam(dt, held)` — 광선이 켜져 있으면 true(그 프레임엔 총알을 쏘지 않는다).
        /// <paramref name="laserTier"/>는 레이저 갈래 티어(다른 갈래·0차면 0 → 항상 false).
        /// </summary>
        public bool Update(float dt, bool held, int laserTier)
        {
            int tier = laserTier;
            if (tier <= 0) { Active = false; HideVisual(); return false; }
            if (!held)
            {
                Active = false;
                Target = null;
                hasPrevCurve = false;
                HideVisual();
                return false;
            }

            int facing = mover != null ? mover.Facing : 1;
            // 원본 ox = p.x + facing*26, oy = p.y - 38 — p.y는 발이라 발 위치에서 잰다(GunnerAttack.FeetPosition).
            Vector2 o = kit.FeetPosition
                        + new Vector2(facing * GunnerSpecConfig.BeamOriginForward, GunnerSpecConfig.BeamOriginHeight);
            Vector2 d = new Vector2(facing, 0f);
            float range = GunnerSpecConfig.BeamRange(tier);
            bool curved = false;
            Vector2 control = default, end = default;

            if (GunnerSpecConfig.BeamHoming(tier))
            {
                float seekR = GunnerSpecConfig.BeamSeekRange(tier);
                float maxAngle = GunnerSpecConfig.BeamConeHalfAngle(tier);
                float baseAngle = facing > 0 ? 0f : Mathf.PI;

                // 잡고 있던 목표는 죽거나(스폰 무적 포함)·탐지 거리 밖·콘 밖이면 놓는다.
                var bt = Target;
                if (bt != null && (!GunnerTargets.TryMake(bt, out var cur)
                                   || Vector2.Distance(cur.center, o) > seekR
                                   || !InCone(cur.center, o, baseAngle, maxAngle)))
                    bt = null;
                if (bt == null)
                {
                    // 콘 밖의 적은 후보에서 빼고, 콘 안에서 가장 가까운 적만 고른다.
                    float bd = seekR;
                    foreach (var e in GunnerTargets.Query(o, seekR + GunnerTargets.QueryMargin))
                    {
                        if (!InCone(e.center, o, baseAngle, maxAngle)) continue;
                        float dist = Vector2.Distance(e.center, o);
                        if (dist < bd) { bd = dist; bt = e.collider; }
                    }
                }
                Target = bt;

                if (bt != null && GunnerTargets.TryMake(bt, out var tg))
                {
                    Vector2 tv = tg.center - o;
                    float len = tv.magnitude;
                    if (len == 0f) len = 1f;
                    d = tv / len;
                    // 원본 `if (Math.abs(dx) > 0.15) p.facing = Math.sign(dx)` — 광선이 목표 쪽으로 캐릭터를 돌린다.
                    if (Mathf.Abs(d.x) > GunnerSpecConfig.BeamFacingTurnThreshold)
                    {
                        facing = d.x > 0f ? 1 : -1;
                        if (mover != null) mover.SetFacing(facing);
                    }
                    if (GunnerSpecConfig.BeamCurved(tier))
                    {
                        // 총구 방향으로 일정 거리 나간 뒤 목표로 꺾인다 — 목표를 살짝 지나치게 해 관통하는 느낌.
                        float ahead = GunnerSpecConfig.BeamCurveAhead(len);
                        Vector2 rawC = new Vector2(o.x + facing * ahead, o.y);
                        Vector2 rawE = tg.center + tv / len * GunnerSpecConfig.BeamCurveOvershoot;
                        float ease = Mathf.Min(1f, dt * GunnerSpecConfig.BeamCurveEaseRate);
                        control = hasPrevCurve ? Vector2.Lerp(prevControl, rawC, ease) : rawC;
                        end = hasPrevCurve ? Vector2.Lerp(prevEnd, rawE, ease) : rawE;
                        curved = true;
                    }
                }
                else
                {
                    // 콘 안에 적이 없다 — 짧게, 살짝 흔들리며 '탐색'한다.
                    range *= GunnerSpecConfig.BeamIdleRangeMult;
                    float sway = Mathf.Sin(Time.time * GunnerSpecConfig.BeamIdleSwayFreq) * GunnerSpecConfig.BeamIdleSwayAmp;
                    float ca = Mathf.Cos(sway), sa = Mathf.Sin(sway);
                    d = new Vector2(d.x * ca - d.y * sa, d.x * sa + d.y * ca);
                }
            }

            hasPrevCurve = curved;
            prevControl = control;
            prevEnd = end;

            float width = GunnerSpecConfig.BeamWidth(tier);
            if (curved)
            {
                float endLen = Vector2.Distance(end, o);
                if (endLen == 0f) endLen = range;
                d = (end - o) / endLen;
            }

            Active = true;
            Origin = o;
            Direction = d;
            Range = range;
            Width = width;
            Curved = curved;
            CurveControl = control;
            CurveEnd = end;

            tickTimer += dt;
            float tick = GunnerSpecConfig.BeamBaseTick(tier)
                         / PlayerStatCalculator.ComputeAttackSpeedMultiplier(ProfileService.Current);
            if (tickTimer >= tick)
            {
                tickTimer -= tick;
                DamageTick(tier);
            }

            UpdateVisual();
            return true;
        }

        /// <summary>캐릭터 전환 등으로 즉시 끈다(원본엔 해당 경로가 없고, 표시물만 정리하는 용도).</summary>
        public void Stop()
        {
            Active = false;
            Target = null;
            hasPrevCurve = false;
            HideVisual();
        }

        static bool InCone(Vector2 p, Vector2 o, float baseAngle, float maxAngle)
        {
            float ea = Mathf.Atan2(p.y - o.y, p.x - o.x);
            float diff = Mathf.Atan2(Mathf.Sin(ea - baseAngle), Mathf.Cos(ea - baseAngle));
            return Mathf.Abs(diff) <= maxAngle;
        }

        static Vector2 Bezier(Vector2 o, Vector2 c, Vector2 e, float t) =>
            (1f - t) * (1f - t) * o + 2f * (1f - t) * t * c + t * t * e;

        /// <summary>원본 :2310~:2330 — 광선 위의 적 전부에게 피해, 적중마다 충전 스택 +1.</summary>
        void DamageTick(int tier)
        {
            var profile = ProfileService.Current;
            float atk = PlayerStatCalculator.ComputeAtk(profile);
            float crit = PlayerStatCalculator.ComputeCritChance(profile);
            float mult = GunnerSpecConfig.BeamDamageMult(tier);

            float reach = Curved
                ? Mathf.Max(Vector2.Distance(CurveControl, Origin), Vector2.Distance(CurveEnd, Origin))
                : Range;

            foreach (var e in GunnerTargets.Query(Origin, reach + Width + GunnerTargets.QueryMargin))
            {
                float dist;
                if (Curved)
                {
                    // 원본: 곡선을 14조각 선분으로 나눠 가장 가까운 거리.
                    dist = float.MaxValue;
                    Vector2 prev = Origin;
                    for (int si = 1; si <= GunnerSpecConfig.BeamCurveSamples; si++)
                    {
                        Vector2 q = Bezier(Origin, CurveControl, CurveEnd, si / (float)GunnerSpecConfig.BeamCurveSamples);
                        dist = Mathf.Min(dist, GunnerField.DistToSegment(e.center, prev, q));
                        prev = q;
                    }
                }
                else
                {
                    float along = Vector2.Dot(e.center - Origin, Direction);
                    if (along < 0f || along > Range) continue;
                    dist = Vector2.Distance(e.center, Origin + Direction * along);
                }
                if (dist >= Width + e.width * GunnerSpecConfig.BeamEnemyWidthFactor) continue;

                int dmg = DamageCalculator.Roll(atk * mult, crit, out _);
                e.damageable.TakeTickDamage(dmg);
                kit.AddStack(1);
            }
        }

        // ---- 표시(원본 drawGunnerBeam :5372의 최소한만 — 판정과 같은 선을 따라 얇은 막대를 이어 붙인다) ----

        void EnsureVisual()
        {
            if (visualRoot != null) return;
            visualRoot = new GameObject("GunnerBeam");
            // 런 오브젝트와 같이 치워지게(다시 필요하면 여기서 새로 만든다).
            visualRoot.AddComponent<RunTransient>();
            segments = new SpriteRenderer[GunnerSpecConfig.BeamCurveSamples];
            for (int i = 0; i < segments.Length; i++)
            {
                var seg = new GameObject("Segment");
                seg.transform.SetParent(visualRoot.transform, false);
                var sr = seg.AddComponent<SpriteRenderer>();
                sr.sprite = kit.bulletSprite;
                sr.color = new Color(GunnerAttack.LaserColor.r, GunnerAttack.LaserColor.g, GunnerAttack.LaserColor.b, 0.85f);
                sr.sortingOrder = 3;
                segments[i] = sr;
            }
        }

        void HideVisual()
        {
            if (visualRoot != null) visualRoot.SetActive(false);
        }

        void UpdateVisual()
        {
            EnsureVisual();
            visualRoot.SetActive(true);
            float thickness = Width * 2f;
            if (Curved)
            {
                Vector2 prev = Origin;
                for (int i = 0; i < segments.Length; i++)
                {
                    Vector2 q = Bezier(Origin, CurveControl, CurveEnd, (i + 1) / (float)segments.Length);
                    PlaceSegment(segments[i], prev, q, thickness);
                    segments[i].enabled = true;
                    prev = q;
                }
            }
            else
            {
                PlaceSegment(segments[0], Origin, Origin + Direction * Range, thickness);
                segments[0].enabled = true;
                for (int i = 1; i < segments.Length; i++) segments[i].enabled = false;
            }
        }

        static void PlaceSegment(SpriteRenderer sr, Vector2 a, Vector2 b, float thickness)
        {
            Vector2 v = b - a;
            var t = sr.transform;
            t.position = (a + b) * 0.5f;
            t.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
            t.localScale = new Vector3(Mathf.Max(0.01f, v.magnitude), thickness, 1f);
        }
    }
}
