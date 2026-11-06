using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 레이저 드론(레이저 빌드 X 스킬) 하나. 원본 `summonLaserDrone`(project_test.html:2363)으로 생기고
    /// `updateZones()`의 `laserDrone` 분기(:3872~:3916)로 갱신된다.
    ///
    /// - 0.18초마다(또는 목표가 죽거나 스폰 무적이 되면) 사거리 안에서 **가장 가까운 적**을 다시 고른다.
    /// - 캐릭터 뒤쪽 위를 부드럽게 따라다니며 위아래로 살짝 흔들린다.
    /// - 정해진 간격마다 목표에게 레이저 탄(<see cref="GunnerBullet"/>)을 쏜다 — 4층부터 탄이 적을 추적하고,
    ///   5층은 관통 3·탄 크기 1.45. 드론 탄은 충전 스택을 쌓지 않는다(`stackOnHit: 0`).
    /// 다시 소환하면 기존 드론은 전부 교체된다.
    /// </summary>
    public class GunnerDrone : MonoBehaviour
    {
        static readonly List<GunnerDrone> active = new List<GunnerDrone>();

        /// <summary>살아 있는 드론. 원본 `zones.filter(z => z.kind === 'laserDrone' && !z.dead)`.</summary>
        public static IReadOnlyList<GunnerDrone> Active
        {
            get { active.RemoveAll(d => d == null); return active; }
        }

        public int Tier { get; private set; }
        public int Side { get; private set; }
        public float Life { get; private set; }
        public float Age { get; private set; }
        public float FireInterval { get; private set; }
        public float Range { get; private set; }
        public float DamageMult { get; private set; }
        /// <summary>지금 조준 중인 적(원본 `z.target`).</summary>
        public Collider2D Target { get; private set; }

        GunnerAttack owner;
        float fireTimer;
        float scanTimer;
        bool removed;

        /// <summary>
        /// 원본 `summonLaserDrone(tier, stackSpend)` — 기존 드론을 전부 치우고 새로 소환한다(5층은 2기).
        /// 소모한 충전 스택 1개당 지속시간 +0.32초. 높이는 원본처럼 캐릭터 **발**에서 잰다(`p.y - 78`).
        /// </summary>
        /// <remarks>드론 구성(그림 자식·RunTransient)은 프리팹(Assets/Prefabs/Combat/GunnerDrone, 2026-11-06 생성 책임 2-3)에 있다 — 소환하는 쪽(<see cref="GunnerAttack"/>)이 넘긴다. 없으면 오류.</remarks>
        public static void Summon(GunnerDrone prefab, GunnerAttack owner, int tier, int stackSpend)
        {
            if (prefab == null) { Debug.LogError("[GunnerDrone] 드론 프리팹이 없다 — GunnerAttack의 dronePrefab을 확인"); return; }
            foreach (var d in new List<GunnerDrone>(Active)) d.Remove();

            int facing = owner.Facing;
            Vector2 feet = owner.FeetPosition;
            int count = GunnerSpecConfig.DroneCount(tier);
            for (int i = 0; i < count; i++)
            {
                int side = i == 0 ? -1 : 1;
                Vector2 pos = feet + new Vector2(
                    -facing * GunnerSpecConfig.DroneSpawnBack + side * GunnerSpecConfig.DroneSpawnSide,
                    GunnerSpecConfig.DroneSpawnHeight + i * GunnerSpecConfig.DroneSpawnHeightStep);
                var d = Instantiate(prefab, pos, Quaternion.identity); // RunTransient가 붙어 있어 런이 끝나면 사라진다(원본 `zones = []`, :4318)
                d.name = prefab.name;
                d.owner = owner;
                d.Tier = tier;
                d.Side = side;
                d.Life = GunnerSpecConfig.DroneLife(tier, stackSpend);
                d.fireTimer = i * GunnerSpecConfig.DroneFirePhaseStep; // 원본 tickT: i*0.12
                d.FireInterval = GunnerSpecConfig.DroneFireInterval(tier);
                d.Range = GunnerSpecConfig.DroneRange(tier);
                d.DamageMult = GunnerSpecConfig.DroneDamageMult(tier);
                d.scanTimer = 0f;
                active.Add(d);
            }
        }

        /// <summary>치운다(원본 `z.dead = true`). 목록에서 즉시 빠진다 — 이유는 <see cref="GunnerTurret.Remove"/>와 같다.</summary>
        public void Remove()
        {
            if (removed) return;
            removed = true;
            active.Remove(this);
            Destroy(gameObject);
        }

        void OnDestroy() => active.Remove(this);

        void Update()
        {
            if (removed) return;
            float dt = Time.deltaTime;
            Age += dt;

            // 원본 :3873~:3883 — 목표가 없거나 죽었거나 스폰 무적이거나, 재탐색 시간이 되면 다시 고른다.
            scanTimer -= dt;
            if (!GunnerTargets.TryMake(Target, out _) || scanTimer <= 0f)
            {
                Target = null;
                scanTimer = GunnerSpecConfig.DroneRescanInterval;
                Target = FindNearest();
            }

            // 원본 :3884~:3887 — 캐릭터 뒤쪽 위를 따라다닌다(높이는 발 기준 `player.y - 86`).
            if (owner != null)
            {
                int facing = owner.Facing;
                Vector2 follow = owner.FeetPosition + new Vector2(
                    -facing * GunnerSpecConfig.DroneFollowBack + Side * GunnerSpecConfig.DroneFollowSide,
                    GunnerSpecConfig.DroneFollowHeight
                    + Mathf.Sin(Time.time * GunnerSpecConfig.DroneBobFreq + Side) * GunnerSpecConfig.DroneBobAmp);
                float k = Mathf.Min(1f, dt * GunnerSpecConfig.DroneFollowRate);
                transform.position = Vector2.Lerp(transform.position, follow, k);
            }

            // 원본 :3888~:3913 — 사격 간격마다 유효한 목표에게 쏜다(목표가 없으면 그 틱은 그냥 넘어간다).
            fireTimer += dt;
            if (fireTimer >= FireInterval)
            {
                fireTimer -= FireInterval;
                if (GunnerTargets.TryMake(Target, out var t)) Shoot(t);
            }

            if (Age >= Life) Remove();
        }

        /// <summary>원본 — 드론 위치에서 몸통 중심까지 거리가 사거리 미만인 적 중 가장 가까운 것.</summary>
        Collider2D FindNearest()
        {
            Collider2D best = null;
            float bestD = Range;
            Vector2 pos = transform.position;
            foreach (var e in GunnerTargets.Query(pos, Range + GunnerTargets.QueryMargin))
            {
                float d = Vector2.Distance(e.center, pos);
                if (d < bestD) { bestD = d; best = e.collider; }
            }
            return best;
        }

        void Shoot(GunnerTargets.Target t)
        {
            Vector2 pos = transform.position;
            Vector2 dir = t.center - pos;
            float len = dir.magnitude;
            if (len == 0f) len = 1f;
            dir /= len;

            float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
            GunnerBullet.Spawn(owner != null ? owner.BulletPrefab : null, pos, dir * GunnerSpecConfig.DroneBulletSpeed(Tier), atk * DamageMult,
                GunnerSpecConfig.DroneBulletPierce(Tier), GunnerSpecConfig.DroneBulletLife,
                GunnerSpecConfig.DroneBulletSize(Tier), GunnerAttack.LaserColor,
                homing: GunnerSpecConfig.DroneBulletHoming(Tier), seekRange: Range,
                turnRate: GunnerSpecConfig.DroneBulletTurnRate(Tier), stackOwner: null, stackOnHit: 0);
        }
    }
}
