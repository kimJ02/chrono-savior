using System.Collections.Generic;
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

        int currentTier;
        // 원본 `p.gunnerBeamTick` — 광선이 켜져 있을 때만 쌓이고, 뗐다고 초기화되진 않는다(원본 그대로).
        float tickTimer;
        // 원본 `p.gunnerBeamCurve` — 직전 프레임 곡선(부드럽게 따라가기용).
        bool hasPrevCurve;
        Vector2 prevControl, prevEnd;

        // 그림은 따로(2026-11-09 — 선 하나로 끊김 없이, <see cref="GunnerBeamVfx"/>). 처음 켤 때 만든다.
        GunnerBeamVfx vfx;
        readonly List<Vector2> path = new List<Vector2>();

        /// <summary>
        /// 층별 레이저 그림 굵기 — 팀원 문서 "파란색, 각성 차수에 따라 굵기 차이": 도트 크기를 하나로 맞추면서(2026-11-09) 1~4층 small(띠 26px → 0.4유닛),
        /// 5층 large(50px → 0.78유닛). 판정 굵기(2W 0.31~0.72)와는 따로다.
        /// </summary>
        public static int ArtBodyRows(int tier) => tier >= 5 ? 16 : tier == 4 ? 14 : tier == 3 ? 12 : 10;

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
            currentTier = tier;
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
            if (vfx != null) vfx.HideNow();
        }

        static bool InCone(Vector2 p, Vector2 o, float baseAngle, float maxAngle)
        {
            float ea = Mathf.Atan2(p.y - o.y, p.x - o.x);
            float diff = Mathf.Atan2(Mathf.Sin(ea - baseAngle), Mathf.Cos(ea - baseAngle));
            return Mathf.Abs(diff) <= maxAngle;
        }

        /// <summary>그 적이 광선 위에 있나 — 원본 :2310~:2330(곡선은 14조각 선분까지 가장 가까운 거리, 직선은 사거리 안 수직 거리).</summary>
        bool OnBeam(GunnerTargets.Target e)
        {
            float dist;
            if (Curved)
            {
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
                if (along < 0f || along > Range) return false;
                dist = Vector2.Distance(e.center, Origin + Direction * along);
            }
            return dist < Width + e.width * GunnerSpecConfig.BeamEnemyWidthFactor;
        }

        /// <summary>
        /// 지금 광선에 맞을 적이 있나 — 없으면 그림은 빔 대신 총구에서 **차징하는 모습**만 보인다(2026-11-09 사용자 "Z를 누르고 있을 때 주변에 맞을 적이 없으면
        /// 레이저 차징 중인 것처럼"). 판정(틱 피해)은 그대로 돈다 — 맞을 적이 없으니 결과도 같다.
        /// </summary>
        public bool HasSomethingToHit
        {
            get
            {
                if (!Active) return false;
                float reach = Curved ? Mathf.Max(Vector2.Distance(CurveControl, Origin), Vector2.Distance(CurveEnd, Origin)) : Range;
                foreach (var e in GunnerTargets.Query(Origin, reach + Width + GunnerTargets.QueryMargin))
                    if (OnBeam(e)) return true;
                return false;
            }
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
                if (!OnBeam(e)) continue;

                float dmg = DamageCalculator.Roll(atk * mult, crit, out _);
                e.damageable.TakeTickDamage(dmg);
                if (vfx != null) vfx.Spark(e.collider, e.center, Direction); // 맞은 자리에 작은 충격(그림만, 적마다 0.25초에 한 번)
                kit.AddStack(1);
            }
        }

        // ---- 표시(그림은 GunnerBeamVfx — 경로·층·끝 그림 여부만 넘긴다) ----

        void HideVisual()
        {
            beamHold = 0f; // 다음에 켤 땐 맞을 적이 있어야 빔
            if (vfx != null) vfx.Release(); // 사그라든다(end 프레임)
        }

        /// <summary>빔이 켜진 뒤 맞을 적이 없어져도 이만큼(초)은 빔을 유지한다 — 적이 들락날락·죽을 때 빔↔차징이 깜빡이지 않게(2026-11-09 Codex 합의).</summary>
        public const float BeamHoldTime = 0.35f;
        float beamHold;

        void UpdateVisual()
        {
            if (vfx == null) vfx = GunnerBeamVfx.Create(kit.BeamMaterial, kit.LaserTipPrefab);
            if (vfx == null) return;
            if (HasSomethingToHit) beamHold = BeamHoldTime;
            else beamHold -= Time.deltaTime;
            if (beamHold <= 0f)
            {
                vfx.ShowCharging(Origin, Direction);
                return;
            }
            path.Clear();
            path.Add(Origin);
            if (Curved)
            {
                // 5층 곡선은 판정상 목표를 0.5 지나 끝나지만, 그림은 **목표 가장자리에서** 끝낸다(목표를 뚫고 나간 끝이 '끊긴' 것처럼 보였다)
                bool hasTarget = GunnerTargets.TryMake(Target, out var tg);
                for (int i = 1; i <= GunnerSpecConfig.BeamCurveSamples; i++)
                {
                    Vector2 q = Bezier(Origin, CurveControl, CurveEnd, i / (float)GunnerSpecConfig.BeamCurveSamples);
                    path.Add(q);
                    if (hasTarget && Vector2.Distance(q, tg.center) <= tg.width * 0.5f) break;
                }
            }
            else
                path.Add(Origin + Direction * Range);
            vfx.Show(path, currentTier, Curved && Target != null, Width); // 끝 그림은 빔이 목표에서 끝날 때(5층 곡선)만 — 3·4층 직선은 목표를 지나 사거리 끝까지 간다(빈 곳에 물보라가 뜨지 않게)
        }

        /// <summary>그림 담당(테스트용 — 아직 안 켰으면 null).</summary>
        public GunnerBeamVfx Vfx => vfx;
    }
}
