using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉 설치기(설치기 빌드 X 스킬) 하나. 원본 `placeGunnerTurret`(project_test.html:2349)로 생기고
    /// `updateZones()`의 `turret` 분기(:3858~:3871)로 갱신된다 — 일정 간격마다 반경 안의 적 전부에게
    /// 피해(치명타·넉백·히트스톱 없음)를 주고, 수명이 다하면 사라진다(5층은 사실상 영구).
    ///
    /// 살아 있는 설치기는 <see cref="Active"/>에 **설치한 순서대로** 모인다 — 원본 `zones` 배열 순서와 같아서
    /// 장판(<see cref="GunnerField"/>)의 "앞에서부터 최대 N개"와 "가장 오래된 것 교체"가 이 순서를 쓴다.
    /// </summary>
    public class GunnerTurret : MonoBehaviour
    {
        static readonly List<GunnerTurret> active = new List<GunnerTurret>();

        /// <summary>살아 있는 설치기(설치한 순서, 오래된 것이 앞). 원본 `zones.filter(z => z.kind === 'turret' && !z.dead)`.</summary>
        public static IReadOnlyList<GunnerTurret> Active
        {
            get { active.RemoveAll(t => t == null); return active; }
        }

        public int Tier { get; private set; }
        /// <summary>설치 후 지난 시간 — 원본 `z.t`.</summary>
        public float Age { get; private set; }
        public float Life { get; private set; }
        public float Range { get; private set; }
        public float DamageMult { get; private set; }
        public float TickInterval { get; private set; }

        float tickTimer;
        bool removed;

        /// <summary>피해 판정 중심 — 원본 `(z.x, z.y - 18)`.</summary>
        public Vector2 DamageCenter => (Vector2)transform.position + new Vector2(0f, GunnerSpecConfig.TurretCenterHeight);
        /// <summary>장판 꼭짓점 — 원본 `(z.x, z.y - 24)`.</summary>
        public Vector2 FieldPoint => (Vector2)transform.position + new Vector2(0f, GunnerSpecConfig.FieldPointHeight);

        /// <summary>
        /// 원본 `placeGunnerTurret(tier)` — 캐릭터 앞 0.34에 놓되 맵 양 끝 0.36 안쪽으로 자른다. 높이는 캐릭터 **발**
        /// (원본 `y: p.y`, <see cref="GunnerAttack.FeetPosition"/>)이라 <paramref name="playerFeet"/>로 받는다.
        /// 개수 한도 초과 시 가장 오래된 것을 치우는 건 호출하는 쪽(<see cref="GunnerAttack"/>, 원본 `tryGunnerSkill`) 몫이다.
        /// </summary>
        public static GunnerTurret Place(GunnerTurret prefab, Vector2 playerFeet, int facing, int tier)
        {
            float x = Mathf.Clamp(playerFeet.x + facing * GunnerSpecConfig.TurretPlaceForward,
                FieldBounds.MinX + GunnerSpecConfig.TurretEdgeMargin, FieldBounds.MaxX - GunnerSpecConfig.TurretEdgeMargin);
            return Spawn(prefab, new Vector2(x, playerFeet.y), tier);
        }

        /// <summary>
        /// 정해진 자리에 바로 세운다(<see cref="Place"/>가 위치 계산 뒤 부른다 — 테스트도 직접 쓴다). 세우는 쪽(<see cref="GunnerAttack"/>)이 들고 있는 프리팹
        /// (Assets/Prefabs/Combat/GunnerTurret — 그림 자식·RunTransient, 2026-11-06 생성 책임 2-3)을 세우고 이번 층의 값을 넣는다. 프리팹이 없으면 오류.
        /// </summary>
        public static GunnerTurret Spawn(GunnerTurret prefab, Vector2 pos, int tier)
        {
            if (prefab == null) { Debug.LogError("[GunnerTurret] 포탑 프리팹이 없다 — GunnerAttack의 turretPrefab을 확인"); return null; }
            var t = Instantiate(prefab, pos, Quaternion.identity); // RunTransient가 붙어 있어 런이 끝나면 사라진다(원본 `zones = []`, :4318)
            t.name = prefab.name;
            t.Tier = tier;
            t.Life = GunnerSpecConfig.TurretLife(tier);
            t.TickInterval = GunnerSpecConfig.TurretTick(tier);
            t.Range = GunnerSpecConfig.TurretRange(tier);
            t.DamageMult = GunnerSpecConfig.TurretDamageMult(tier);
            active.Add(t);
            return t;
        }

        /// <summary>
        /// 치운다(원본 `z.dead = true` / `zones.splice`). 목록에서 **즉시** 빠진다 — `Destroy`는 프레임 끝에야
        /// 반영돼서, 같은 프레임에 새 설치기를 놓을 때 개수 계산이 틀리지 않게 하려는 것.
        /// </summary>
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
            tickTimer += dt;
            if (tickTimer >= TickInterval)
            {
                tickTimer -= TickInterval;
                DamageTick();
            }
            if (Age >= Life) Remove();
        }

        /// <summary>원본 :3860~:3868 — `hypot(e.x - z.x, 몸통중심 - (z.y-18)) < z.range + e.w/2`인 적 전부.</summary>
        void DamageTick()
        {
            float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
            Vector2 c = DamageCenter;
            foreach (var e in GunnerTargets.Query(c, Range + GunnerTargets.QueryMargin))
            {
                if (Vector2.Distance(e.center, c) >= Range + e.width / 2f) continue;
                int dmg = DamageCalculator.Roll(atk * DamageMult, 0f, out _); // 원본 noCrit
                e.damageable.TakeTickDamage(dmg);                            // 원본 kb:0 + noHitstop
            }
        }
    }
}
