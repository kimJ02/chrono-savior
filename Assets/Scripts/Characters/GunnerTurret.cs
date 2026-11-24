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
            AttachRelayHead(t.transform);
            active.Add(t);
            // 2026-11-10 정정 — "가동" 소리는 더 이상 설치 순간 한 번이 아니다. 설치기가 2개 이상
            // 링크되어 연결선·장판이 떠 있는 동안 GunnerAttack.UpdateField가 몇 초마다 반복해서 올린다.
            // 놓는 순간의 소리는 호출부(GunnerAttack.TryUseSkill)의 RaiseMechanicTurretSet 그대로.
            return t;
        }

        /// <summary>중계기 머리 높이(발에서 — 우리가 그린 안테나 몸체(13px × 도트 2 = 0.2유닛) 위, 링크 꼭짓점 0.24 근처)·재생 속도·정렬.</summary>
        public const float RelayHeadHeight = 0.27f, RelayHeadFps = 15f;
        public const int RelayHeadSortingOrder = 4;

        /// <summary>
        /// 중계기 머리(2026-11-08 VFX, 팀원 문서 "몸체 위에 달아 놓는 느낌") — Projectiles Pack 4 `plasma_ball` 파랑(키 <see cref="VfxKeys.MechanicRelayHead"/>, 공통 도트 배율).
        /// 몸체(안테나) 그림은 팩에 없어 지금 몸체 그대로. 그림이 없으면 머리 없이.
        /// </summary>
        static void AttachRelayHead(Transform turret)
        {
            var frames = VfxFrames.Get(VfxKeys.MechanicRelayHead);
            if (frames.Length == 0) return;
            var go = new GameObject("RelayHead", typeof(SpriteRenderer));
            go.transform.SetParent(turret, false);
            go.transform.localPosition = new Vector3(0f, RelayHeadHeight, 0f); // 머리(24px × 도트 2 = 0.375유닛)가 몸체 위에 앉게
            go.transform.localScale = new Vector3(DotScale.Value, DotScale.Value, 1f);
            go.GetComponent<SpriteRenderer>().sortingOrder = RelayHeadSortingOrder;
            go.AddComponent<VfxPlayer>().Play(frames, RelayHeadFps, true, 0f, Color.white);
            // 머리 둘레 지직거리는 전기(2026-11-10 사용자 I-B '전기 아크형') — 켜져 있다는 표시
            var aura = VfxFrames.Get(VfxKeys.MechanicRelayAura);
            if (aura.Length == 0) return;
            var ago = new GameObject("RelayAura", typeof(SpriteRenderer));
            ago.transform.SetParent(turret, false);
            ago.transform.localPosition = new Vector3(0f, RelayHeadHeight, 0f);
            ago.transform.localScale = new Vector3(DotScale.Value, DotScale.Value, 1f);
            ago.GetComponent<SpriteRenderer>().sortingOrder = RelayHeadSortingOrder - 1;
            var ap = ago.AddComponent<VfxPlayer>();
            ap.Play(aura, RelayAuraFps, true, 0f, Color.white);
        }

        /// <summary>머리 전기 오라 재생 속도.</summary>
        public const float RelayAuraFps = 15f;

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

        // ── 설치 순간(2026-11-24 사용자 D안 — "설치 방식은 그대로, 설치하는 순간을 꼬아서 화려·타격감", GPT와 정함) ──
        /// <summary>
        /// 낙하 그림(편지 전기 기둥, 물결 번개 지운 것) 재생 속도와 바닥에 닿는 장 — 0.11초 뒤 꽂힌다. 판정은 X 순간부터 켜져 있고 그림만 꽂힐 때 보인다.
        /// 낙하 그림 가운데~바닥(그림 px).
        /// </summary>
        public const float DropFps = 36f, DropBottomPx = 58f;
        public const int DropLandFrame = 4;
        public static float DropLandDelay => DropLandFrame / DropFps;
        /// <summary>전력 인입 광선 — 기존 설치기에서 새 설치기로, 지나가는 적에게 피해(사용자 "광선이 공격처럼 보이면 공격으로 만들어").</summary>
        public const int PowerFlashSortingOrder = 6;

        float landIn = -1f;
        Vector3 shownScale;
        bool hitSparks;

        /// <summary>
        /// X로 놓은 직후 부른다(<see cref="GunnerAttack"/>). 위에서 에너지가 떨어져 꽂히고(그때까지 그림을 숨김), 꽂히는 순간 첫 공격 틱을 바로 쏘고
        /// (맞은 적마다 적중 불꽃) 화면을 흔든다. 이어서 이어지는 기존 설치기들에서 새 설치기로 광선이 흘러 들어오며 지나가는 적을 친다.
        /// 낙하 그림이 없으면 바로 꽂힌다. <paramref name="shake"/>: X 한 번에 하나만 흔든다(삼각 방전의 추가 중계기는 false).
        /// </summary>
        public void PlayArrival(bool shake)
        {
            shakeOnLand = shake;
            var drop = VfxPlayer.Spawn(VfxKeys.MechanicWarp, (Vector2)transform.position + new Vector2(0f, DotScale.Units(DropBottomPx)),
                                       DotScale.Value, DropFps, RelicFx.SortingOrder);
            if (drop == null) { Land(); return; }
            shownScale = transform.localScale;
            transform.localScale = Vector3.zero; // 꽂히기 전엔 안 보인다(판정은 켜져 있다)
            landIn = DropLandDelay;
        }
        bool shakeOnLand;

        void Land()
        {
            landIn = -1f;
            if (shownScale != Vector3.zero) transform.localScale = shownScale;
            if (shakeOnLand) HitFeel.Shake(2f + Tier * 0.4f, 0.1f); // 원본 :2416(X 순간) — 꽂히는 순간으로 옮겼다
            hitSparks = true;
            tickTimer = 0f;
            DamageTick(); // 꽂히는 순간 첫 공격
            hitSparks = false;
            PowerIn();
        }

        /// <summary>이어지는 기존 설치기(링크 거리 안)마다 머리에서 새 설치기 머리로 광선 — 닿는 순간 선 위 적에게 피해, 새 머리 번쩍.</summary>
        void PowerIn()
        {
            Vector2 head = (Vector2)transform.position + new Vector2(0f, RelayHeadHeight);
            float linkMax = GunnerSpecConfig.LinkMax(Tier);
            float arrive = -1f;
            int k = 0;
            foreach (var o in active)
            {
                if (o == this || o == null) continue;
                Vector2 from = (Vector2)o.transform.position + new Vector2(0f, RelayHeadHeight);
                if (Vector2.Distance(from, head) > linkMax) continue;
                var fx = GunnerChainBeamVfx.Play(GunnerRelics.ChainMaterial, from, head, 0f, k++ % 2 == 0 ? 1f : -1f, head, hitSpark: false);
                float a = fx != null ? fx.ArriveTime : 0f;
                arrive = Mathf.Max(arrive, a);
                Vector2 f0 = from;
                int tier = Tier;
                RelicFx.After(a, () => PowerSurge(f0, head, tier));
            }
            if (arrive < 0f) return;
            var self = this;
            RelicFx.After(arrive, () =>
            {
                if (self != null) VfxPlayer.Spawn(VfxKeys.MechanicDroneHit, head, DotScale.Value, 30f, PowerFlashSortingOrder); // 전력이 다 들어왔다
            });
        }

        /// <summary>전력 인입 광선 한 줄의 피해 — 선분에서 링크 판정 폭 안의 적 전부, <see cref="GunnerSpecConfig.PowerSurgeDamageMult"/>배.</summary>
        static void PowerSurge(Vector2 a, Vector2 b, int tier)
        {
            float atk = PlayerStatCalculator.ComputeAtk(ProfileService.Current);
            using var signature = Relics.Signature(); // 설치 시그니처
            float mult = GunnerSpecConfig.PowerSurgeDamageMult(tier) * GunnerRelics.OverloadDamageMult;
            Vector2 c = (a + b) * 0.5f;
            float r = Vector2.Distance(a, b) * 0.5f + GunnerSpecConfig.LinkHitWidth(tier) + GunnerTargets.QueryMargin;
            foreach (var e in GunnerTargets.Query(c, r))
            {
                if (GunnerField.DistToSegment(e.center, a, b) >= GunnerSpecConfig.LinkHitWidth(tier) + e.width * GunnerSpecConfig.LinkEnemyWidthFactor) continue;
                float dmg = DamageCalculator.Roll(atk * mult, 0f, out _);
                e.damageable.TakeTickDamage(dmg);
                VfxPlayer.Spawn(VfxKeys.MechanicDroneHit, e.center, DotScale.Value, 30f, PowerFlashSortingOrder);
            }
        }

        void Update()
        {
            if (removed) return;
            if (landIn >= 0f) { landIn -= Time.deltaTime; if (landIn <= 0f) Land(); }
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
            using var signature = Relics.Signature(); // 설치기 공격도 설치 시그니처(2026-11-22)
            float sig = GunnerRelics.OverloadDamageMult; // 세트 5 과부하
            bool armedTaken = false;
            foreach (var e in GunnerTargets.Query(c, Range + GunnerTargets.QueryMargin))
            {
                if (Vector2.Distance(e.center, c) >= Range + e.width / 2f) continue;
                if (!armedTaken) { armedTaken = true; sig *= GunnerRelics.ConsumeArmedMult(); } // 세트 3 — 맞힌 틱에서만
                float dmg = DamageCalculator.Roll(atk * DamageMult * sig, 0f, out _); // 원본 noCrit
                e.damageable.TakeTickDamage(dmg);                            // 원본 kb:0 + noHitstop
                if (hitSparks) VfxPlayer.Spawn(VfxKeys.MechanicDroneHit, e.center, DotScale.Value, 30f, PowerFlashSortingOrder); // 꽂히는 순간 적중 불꽃
                // 그림만 — 적 자리 전기 + 중계기 머리에서 그 적까지 아크(적마다 0.25초에 한 번, 2026-11-10)
                GunnerZap.At(e.collider, e.center, (Vector2)transform.position + new Vector2(0f, RelayHeadHeight), e.width * 0.5f);
            }
        }
    }
}
