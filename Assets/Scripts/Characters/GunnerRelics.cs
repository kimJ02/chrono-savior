using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 전용 유물 6종 — **2026-11-22 재설계 2차**(사용자 "레이저는 Z·X에 골고루 특별한 효과, 설치는 X에 특별한 효과" — GPT와 정함).
    /// 수치만 올리지 않고 그 버튼의 플레이를 바꾼다.
    /// - 잔탄 포켓(기본공격, 두 갈래): Z 적중으로 잔탄을 모아 다음 X 때 전기 연쇄로 터뜨린다.
    /// - 프리즘 구경(레이저, Z): 광선 처치 n번마다 광선이 갈라져 주변 적에게 연쇄.
    /// - 자율 포좌(레이저, X): 드론이 n번 쏠 때마다 부채꼴 추가 탄.
    /// - 삼각 방전(설치, X): 설치 한도 +(X 한 번에 더 놓던 건 2026-11-24 사용자 "하나만"으로 뺐다).
    /// - 교차 전류(설치, X): 설치하는 순간 그 자리에서 전류 방출.
    /// - 공성 연결망(설치, X): 장판이 맞힌 틱마다 적에서 적으로 전류가 이어진다.
    /// 세트 3: X 뒤 첫 시그니처 적중 틱 ×2. 세트 5: X 뒤 5초 과부하(광선 굵기 ×1.35·피해 ×1.5 / 장판 틱 ×0.6·피해 ×1.5).
    /// </summary>
    public static class GunnerRelics
    {
        const int MaxSpareCharges = 3;
        static int zHits, spareCharges, beamKills, droneShots;
        static bool setArmed;
        static float overloadUntil = -1f;

        public static void ResetForRun()
        {
            zHits = spareCharges = beamKills = droneShots = 0; setArmed = false; overloadUntil = -1f; Relics.ClearLaserTouched();
            Relics.LaserLetter = LetterByLaser; // 편지 레이저 배달(RelicDirector가 부른다)
        }

        /// <summary>광선 연쇄 그림의 머티리얼(본 레이저와 같은 것 — 메카닉 키트가 런 시작 때 넣는다). 없으면 그림 없이 바로 맞힌다(테스트).</summary>
        public static Material ChainMaterial;

        // ── 킷·설정이 읽는 값 ──
        public static int TurretBonus => Relics.VI("triangleDischarge", 0);
        public static bool Overloaded => Time.time < overloadUntil;
        public static float BeamWidthMult => Overloaded ? 1.35f : 1f;
        public static float FieldTickMult => Overloaded ? 0.6f : 1f;
        public static float OverloadDamageMult => Overloaded ? 1.5f : 1f;

        /// <summary>세트 3 장전을 쓴다 — 시그니처가 실제로 적을 맞힌 틱에서 한 번만(빗나가면 남는다). 장전이면 2, 아니면 1.</summary>
        public static float ConsumeArmedMult()
        {
            if (!setArmed) return 1f;
            setArmed = false;
            return 2f;
        }

        /// <summary>
        /// X를 썼다(<paramref name="at"/> = 드론이 뜨는·설치기를 놓은 자리) — 세트, 잔탄 포켓(모은 잔탄을 전기 연쇄로), 교차 전류(설치 순간 방출).
        /// </summary>
        public static void OnSkillUsed(Vector2 at, bool installer)
        {
            if (Relics.CharacterSet3) setArmed = true;
            if (Relics.CharacterSet5)
            {
                overloadUntil = Time.time + 5f;
                VfxPlayer.Spawn(VfxKeys.MechanicRelayAura, at + Vector2.up * 0.5f, DotScale.Value, 15f, RelicFx.SortingOrder);
            }

            // 잔탄 포켓 — 잔탄마다 0.1초 간격으로 전기 연쇄
            if (spareCharges > 0 && Relics.Rank("spareRounds") > 0)
            {
                Relics.Proc("spareRounds");
                int n = spareCharges, targets = Relics.VI("spareRounds", 1);
                float mult = Relics.V("spareRounds", 2);
                spareCharges = 0;
                Vector2 o = at + Vector2.up * 0.6f;
                for (int i = 0; i < n; i++)
                {
                    if (installer) RelicFx.After(0.1f * i, () => ChainFrom(o, targets, mult, false));
                    else RelicFx.After(0.12f * i, () => LaserChain(o, targets, mult)); // 레이저 갈래 — 광선 연쇄(2026-11-23)
                }
            }

            // 교차 전류 — 설치하는 순간 그 자리에서 방출
            if (installer && Relics.Rank("crossCurrent") > 0)
            {
                Relics.Proc("crossCurrent");
                bool gold = Relics.Awakened("crossCurrent");
                int targets = Relics.VI("crossCurrent", 0);
                float mult = Relics.V("crossCurrent", 1);
                Vector2 o = at + new Vector2(0f, GunnerSpecConfig.TurretCenterHeight);
                VfxPlayer.Spawn(VfxKeys.MechanicRelayAura, o, DotScale.Value, 15f, RelicFx.SortingOrder);
                ChainFrom(o, targets, mult, gold);
                if (gold) RelicFx.After(0.2f, () => ChainFrom(o, targets, mult * 0.35f, true));
            }
        }

        /// <summary>Z(총알·광선)가 적 하나를 맞혔다 — 잔탄 포켓: n번마다 잔탄 1(최대 3).</summary>
        public static void OnZHit()
        {
            if (Relics.InRelicDamage || Relics.Rank("spareRounds") <= 0 || spareCharges >= MaxSpareCharges) return;
            if (++zHits < Relics.VI("spareRounds", 0)) return;
            zHits = 0;
            spareCharges++;
        }

        /// <summary>지금 모인 잔탄(HUD·테스트용).</summary>
        public static int SpareCharges => spareCharges;

        /// <summary>레이저 광선이 적을 쓰러뜨렸다 — 프리즘 구경: n번마다 광선이 갈라져 주변 적에게 연쇄.</summary>
        public static void OnBeamKill(Vector2 at)
        {
            if (Relics.InRelicDamage || Relics.Rank("prismBore") <= 0) return;
            if (++beamKills < Relics.VI("prismBore", 0)) return;
            beamKills = 0;
            Relics.Proc("prismBore");
            bool gold = Relics.Awakened("prismBore");
            int n = Relics.VI("prismBore", 1);
            float mult = Relics.V("prismBore", 2);
            LaserChain(at, n, mult * (gold ? 1.35f : 1f)); // 각성 — 위력 +35%(광선이 두 번 튀면 어지러워 메아리 대신)
        }

        /// <summary>
        /// 레이저 광선 연쇄(2026-11-23 사용자 승인 그림) — at에서 가장 가까운 적으로, 거기서 다음 적으로 **한 마리씩 차례로** 튄다(최대 n번, 한 번 튄 적은 다시 안 감).
        /// 그림은 <see cref="GunnerChainBeamVfx"/>(본 레이저와 같은 셰이더·그림, 닿으면 하얀 번개), 피해는 광선이 **닿는 순간**. 레이저 갈래라 레이저가 닿은 적만 고른다(<see cref="RelicFx.IsTarget"/>).
        /// </summary>
        static void LaserChain(Vector2 at, int n, float mult, Collider2D exclude = null)
        {
            var visited = new List<Collider2D>();
            if (exclude != null) visited.Add(exclude);
            Collider2D cur = null;
            Vector2 from = at;
            float start = 0f, bend = 1f;
            for (int i = 0; i < n; i++)
            {
                Collider2D next;
                using (Relics.AsLaser()) next = RelicFx.NearestOne(cur != null ? RelicFx.Center(cur) : from, 4.5f, visited); // 가장 가까운 적(레이저 자신이 튄다)
                if (next == null) break;
                Relics.MarkLaserTouched(next);
                Vector2 center = RelicFx.Center(next);
                if (cur != null) from = cur.ClosestPoint(center);           // 앞 적 몸 가장자리에서
                Vector2 to = next.ClosestPoint(from);                         // 다음 적 몸 가장자리까지
                if ((to - from).sqrMagnitude < 1e-4f) to = center;
                var fx = GunnerChainBeamVfx.Play(ChainMaterial, from, to, start, bend, center);
                float arrive = fx != null ? fx.ArriveTime : 0f;
                var target = next;
                float dir = Mathf.Sign(center.x - from.x);
                if (arrive <= 0.001f) { using (Relics.AsLaser()) RelicFx.Hit(target, mult, dir, 0.5f); }
                else RelicFx.After(arrive, () => { if (target != null) using (Relics.AsLaser()) RelicFx.Hit(target, mult, dir, 0.5f); });
                visited.Add(next);
                cur = next;
                start = arrive + 0.02f; // 앞 광선이 닿고 0.02초 뒤 다음 적으로(0.04 → 느리다, 2026-11-23 사용자)
                bend = -bend;           // 지그재그로 읽히게 번갈아 휜다
            }
        }

        // ── 편지(공용 유물) — 메카닉 레이저 갈래 ──
        /// <summary>표식이 붙어 있는 시간(초)·그중 마지막에 빨라지는 시간·적마다 폭발 시차·폭발 반경(원래 편지와 같은 1.3).</summary>
        public const float LetterFuse = 1f, LetterRush = 0.3f, LetterStagger = 0.06f, LetterRadius = 1.3f, LetterRange = 6f;
        const float LetterMarkFps = 10f, LetterRushMult = 2.5f, LetterBoomFps = 24f;
        /// <summary>전기 기둥 그림은 위에서 에너지가 떨어져 4번째 장에 바닥에 닿는다 — 그만큼 먼저 시작해 닿는 장이 피해 순간에 오게.</summary>
        const int LetterBoomLeadFrames = 4;
        /// <summary>전기 기둥 그림(128px) 가운데에서 바닥까지(그림 px) — 발밑에 세운다. 표식 그림 크기(px).</summary>
        const float LetterBoomBottomPx = 58f, LetterMarkPx = 16f;

        /// <summary>
        /// 편지 — 메카닉 레이저 갈래(2026-11-23 사용자 후보 B, GPT 합의). 5층 레이저는 목표 하나만 따라가 "레이저가 닿은 적"이 거의 지금 목표뿐이라,
        /// 처치 순간 근처에 닿은 적이 없어 편지가 아무에게도 안 붙었다. 그래서 **레이저가 편지를 배달한다**:
        /// 처치 자리에서 가까운 적 n마리로 광선 연쇄(승인된 그림, 닿을 때 번쩍임 없이)가 동시에 뻗고 → 닿는 순간 레이저가 닿은 적이 되고 머리 위에
        /// 파란 에너지 구슬 표식(마지막 0.3초 빠르게) → 1초 뒤 위에서 에너지가 떨어져 발밑에서 전기 기둥, 닿는 순간 반경 1.3 피해(적마다 0.06초 시차).
        /// 광선은 피해가 없다. 닿기 전에 죽은 적은 표식도 폭발도 없다. 범위 고리는 그리지 않는다(받은 VFX만). 도트 배율 2 하나.
        /// </summary>
        public static void LetterByLaser(Vector2 from, int n, float mult, bool gold, float awakenDelay, float awakenPower)
        {
            List<Collider2D> targets;
            using (Relics.AsLaser()) targets = RelicFx.Nearest(from, n, LetterRange); // 레이저가 배달하니 아직 안 닿은 적도 고른다
            for (int k = 0; k < targets.Count; k++)
            {
                var target = targets[k];
                Vector2 center = RelicFx.Center(target);
                Vector2 to = target.ClosestPoint(from);
                if ((to - from).sqrMagnitude < 1e-4f) to = center;
                var fx = GunnerChainBeamVfx.Play(ChainMaterial, from, to, 0f, k % 2 == 0 ? 1f : -1f, center, hitSpark: false);
                float arrive = fx != null ? fx.ArriveTime : 0f;
                float stagger = LetterStagger * k;
                RelicFx.After(arrive, () => Deliver(target, stagger, mult, gold, awakenDelay, awakenPower));
            }
        }

        static void Deliver(Collider2D target, float stagger, float mult, bool gold, float awakenDelay, float awakenPower)
        {
            if (target == null || !target.gameObject.activeInHierarchy) return; // 광선이 닿기 전에 죽었다
            Relics.MarkLaserTouched(target);
            var b = target.bounds;
            Vector2 center = b.center;
            float feetY = b.min.y;
            var mark = VfxPlayer.Spawn(VfxKeys.MechanicPellet, new Vector2(b.center.x, b.max.y + DotScale.Units(LetterMarkPx * 0.5f + 1f)),
                                       DotScale.Value, LetterMarkFps, RelicFx.SortingOrder, loop: true);
            if (mark != null) mark.transform.SetParent(target.transform, true); // 머리 위에 붙어 따라간다(적이 사라지면 같이)
            RelicFx.After(LetterFuse - LetterRush, () =>
            {
                if (mark != null) mark.Play(VfxFrames.Get(VfxKeys.MechanicPellet), LetterMarkFps * LetterRushMult, true, 0f, Color.white); // 곧 터진다
            });
            RelicFx.After(LetterFuse, () => { if (mark != null) Object.Destroy(mark.gameObject); });

            float lead = LetterBoomLeadFrames / LetterBoomFps;
            RelicFx.After(LetterFuse + stagger - lead, () =>
            {
                if (target != null && target.gameObject.activeInHierarchy) { center = target.bounds.center; feetY = target.bounds.min.y; }
                Vector2 boomAt = new Vector2(center.x, feetY + DotScale.Units(LetterBoomBottomPx));
                VfxPlayer.Spawn(VfxKeys.MechanicWarp, boomAt, DotScale.Value, LetterBoomFps, RelicFx.SortingOrder);
                RelicFx.After(lead, () =>
                {
                    RelicFx.Circle(center, LetterRadius, mult, RelicFx.Cyan, null, showRing: false);
                    if (!gold) return;
                    // 각성 — 같은 자리에 한 번 더(옅게)
                    RelicFx.After(awakenDelay - lead, () =>
                    {
                        VfxPlayer.Spawn(VfxKeys.MechanicWarp, boomAt, DotScale.Value, LetterBoomFps, RelicFx.SortingOrder, tint: new Color(1f, 1f, 1f, 0.6f));
                        RelicFx.After(lead, () => RelicFx.Circle(center, LetterRadius, mult * awakenPower, RelicFx.Cyan, null, showRing: false));
                    });
                });
            });
        }

        /// <summary>드론이 한 발 쐈다 — 자율 포좌: n발마다 부채꼴 추가 탄.</summary>
        public static void OnDroneShot(Vector2 pos, Vector2 dir)
        {
            if (Relics.Rank("autoTurret") <= 0) return;
            if (++droneShots < Relics.VI("autoTurret", 0)) return;
            droneShots = 0;
            Relics.Proc("autoTurret");
            int n = Relics.VI("autoTurret", 1);
            float mult = Relics.V("autoTurret", 2);
            bool gold = Relics.Awakened("autoTurret");
            for (int i = 0; i < n; i++)
            {
                float deg = n == 1 ? 14f : (i == 0 ? 16f : -16f);
                Vector2 d = Quaternion.Euler(0f, 0f, deg) * dir;
                RelicProjectile.Spawn(pos, d, 12f, 0.8f, mult, RelicProjectile.Mode.Homing, VfxKeys.MechanicPellet,
                                      gold ? RelicFx.Gold : GunnerAttack.LaserColor, 0.3f, 0);
            }
        }

        /// <summary>설치 장 한 틱이 적들을 맞혔다 — 공성 연결망: 맞은 적에서 다른 적으로 전류가 이어진다.</summary>
        public static void OnFieldTick(List<Collider2D> hits, List<Vector2> pts)
        {
            if (hits.Count == 0 || Relics.InRelicDamage || Relics.Rank("siegeNetwork") <= 0) return;
            Relics.Proc("siegeNetwork");
            var first = hits[0];
            if (first == null) return;
            ChainFrom(RelicFx.Center(first), Relics.VI("siegeNetwork", 0), Relics.V("siegeNetwork", 1), Relics.Awakened("siegeNetwork"), first); // 맞은 적은 빼고 다음 적부터
        }

        /// <summary>at에서 가까운 적으로 n번 튀는 전류·광선.</summary>
        static void ChainFrom(Vector2 at, int n, float mult, bool gold, Collider2D exclude = null)
        {
            var visited = new List<Collider2D>();
            if (exclude != null) visited.Add(exclude);
            Vector2 from = at;
            for (int i = 0; i < n; i++)
            {
                var next = RelicFx.NearestOne(from, 4.5f, visited);
                if (next == null) break;
                Vector2 to = RelicFx.Center(next);
                RelicLine.Show(from, to, 0.2f, gold ? RelicFx.Gold : RelicFx.Cyan, 0.22f);
                RelicFx.Hit(next, mult, Mathf.Sign(to.x - from.x), 0.5f);
                VfxPlayer.Spawn(VfxKeys.MechanicZap, to, DotScale.Value, 15f, RelicFx.SortingOrder);
                visited.Add(next);
                from = to;
            }
        }
    }
}
