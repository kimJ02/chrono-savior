using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 메카닉(원본 무기 "gunner") 키트 — Z 기본공격(총알 / 레이저 빔)과 X 스킬(레이저 드론 / 설치기),
    /// 충전 스택·부품, 설치기 링크·내부 장판까지. 원본 `gunnerFire`(project_test.html:2188)·
    /// `updateGunnerBeam`(:2224)·`tryGunnerSkill`(:2390)·`addGunnerStack`(:1411)·`updateZones` 장판(:3766)을 옮겼다.
    /// 수치는 전부 <see cref="GunnerSpecConfig"/>.
    ///
    /// **빌드별 동작**
    /// - 0차(갈래 없음): Z를 누르고 있으면 쿨다운마다 정면으로 총알. X 없음. 스택도 안 쌓인다(`addGunnerStack`이
    ///   갈래가 없으면 즉시 빠진다, `:1414`).
    /// - 레이저 빌드(`move`): Z를 누르는 동안 총알 대신 **관통 광선**(<see cref="GunnerLaserBeam"/>) — 적중마다 충전 스택 +1.
    ///   X는 충전 스택을 전부 써서 **레이저 드론**(<see cref="GunnerDrone"/>) 소환(스택 1개당 지속 +0.32초).
    /// - 설치기 빌드(`conv`): Z는 0차와 같은 총알이고, 적중 10회마다 **부품** 1개. 런 시작 시 부품 1개.
    ///   X는 부품 1개로 **설치기**(<see cref="GunnerTurret"/>) 배치(한도 초과 시 가장 오래된 것 교체).
    ///   설치기 2개 이상이 링크되면 연결선 위·닫힌 도형 안에 장판 피해(<see cref="GunnerField"/>).
    ///
    /// 이동은 마법사와 같은 즉시-속도 방식(원본 `updatePlayerCommon`의 bow/gunner 공통 분기).
    ///
    /// 빌드는 원본 `meta.skills.gunner = {branch, tier}`처럼 세이브(<see cref="PlayerProfile.gunnerBranch"/>/
    /// <see cref="PlayerProfile.gunnerTier"/>)에 있고 로비 전문화 탭에서 SP로 배운다 — 이 키트는 매 프레임
    /// 읽기만 한다(원본 `gunnerFire`가 `meta.skills.gunner`를 매번 새로 읽는 것과 같다).
    /// </summary>
    public class GunnerAttack : MonoBehaviour, ICharacterKit, IRunResettable, ISkillSlotSource
    {
        /// <summary>레이저·드론·설치기 연출색 — 원본 `#8fd8ff`.</summary>
        public static readonly Color LaserColor = new Color(0x8f / 255f, 0xd8 / 255f, 1f);

        [Header("원본 CONFIG/gunnerFire 그대로 (거리·속도는 100px=1유닛 축척)")]
        [Tooltip("표시용 현재 쿨다운. 매 프레임 빌드별 기준값 ÷ 공격속도 배수 × 쿨감으로 다시 계산된다.")]
        public float cooldown = 0.24f;
        [Tooltip("표시용 현재 피해량. statAtk × 빌드별 배수로 매 프레임 재계산된다.")]
        public float baseDamage = 8.2f;

        const float BulletSpeed = 13.2f;         // 원본 spd 1320 ÷100(:2192) — 레이저 빌드도 같은 탄속
        const float MuzzleForward = 0.30f;       // 원본 x: p.x + facing*30(:2199)
        /// <summary>원본 y: `p.y - 38`(:2199) — **발**에서 0.38 위(<see cref="FeetPosition"/>, <see cref="PlayerBody"/>).</summary>
        const float MuzzleHeight = 0.38f;

        [Tooltip("메카닉 탄 프리팹(Assets/Prefabs/Combat/GunnerBullet) — 이 무기와 드론이 쏜다.")]
        [SerializeField] GunnerBullet bulletPrefab;

        /// <summary>씬 검증과 테스트가 읽고 넣는다. 드론도 이 프리팹으로 쏜다.</summary>
        public GunnerBullet BulletPrefab { get => bulletPrefab; set => bulletPrefab = value; }

        [Tooltip("레이저 드론 프리팹(Assets/Prefabs/Combat/GunnerDrone) — X(충전 갈래).")]
        [SerializeField] GunnerDrone dronePrefab;
        [Tooltip("설치 포탑 프리팹(Assets/Prefabs/Combat/GunnerTurret) — X(설치 갈래).")]
        [SerializeField] GunnerTurret turretPrefab;

        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public GunnerDrone DronePrefab { get => dronePrefab; set => dronePrefab = value; }
        /// <summary>씬 검증과 테스트가 읽고 넣는다.</summary>
        public GunnerTurret TurretPrefab { get => turretPrefab; set => turretPrefab = value; }

        [Tooltip("레이저 구간·설치물 연결선 그림(그림만 있는 효과 — 코드가 깐다).")]
        public Sprite bulletSprite;
        public Color bulletColor = new Color(1f, 0.92f, 0.63f);

        CharacterMover2D mover;
        CircleCollider2D bodyCol;
        GunnerLaserBeam beam;
        float cdTimer;
        float ultCdTimer;
        // 쿨다운을 **건 순간**의 최대치 — HUD 슬롯 덮개 비율의 분모. 원본 `p.gunnerCdMax`(:2196)·`p.ultCdMax`
        // (:2399·:2414)도 발사·시전 때 정해진다.
        float attackCdMax;
        float ultCdMax;
        float fieldTickTimer;                    // 원본 player.mechFieldTick

        GameObject fieldVisualRoot;
        readonly List<SpriteRenderer> linkVisuals = new List<SpriteRenderer>();

        public CharacterId Character => CharacterId.Gunner;
        /// <summary>메카닉은 마법사와 같은 즉시-속도 이동(관성은 섬영 전용).</summary>
        public CharacterMover2D.MoveMode RequiredMoveMode => CharacterMover2D.MoveMode.Instant;

        /// <summary>지금 빌드 층수 — 원본 `meta.skills.gunner.tier`.</summary>
        public int Tier => ProfileService.Current.gunnerTier;
        /// <summary>
        /// 실제로 적용되는 갈래 — 층수가 1 미만이면 없음. 원본은 갈래가 정해지는 순간 1층을 같이 배우므로
        /// (`learnSkill`) "갈래는 있는데 0층"인 상태가 없다 — 세이브가 그런 값이어도 0차로 본다.
        /// </summary>
        public GunnerBranch Branch => Tier >= 1 ? ProfileService.Current.gunnerBranch : GunnerBranch.None;
        public int LaserTier => GunnerSpecConfig.LaserTier(Branch, Tier);
        public int InstallerTier => GunnerSpecConfig.InstallerTier(Branch, Tier);

        /// <summary>충전 스택(레이저) 또는 부품(설치기) — 원본 `player.gunnerStacks`.</summary>
        public int Stacks { get; private set; }
        /// <summary>설치기 빌드의 부품 충전용 적중 수(10이면 부품 1개) — 원본 `player.gunnerPartHits`.</summary>
        public int PartHits { get; private set; }
        /// <summary>원본 `gunnerStackMax()`.</summary>
        public int StackMax => GunnerSpecConfig.StackMax(Branch, Tier);
        /// <summary>X 스킬 남은 쿨다운(원본 `p.ultCd`).</summary>
        public float UltCooldownRemaining => Mathf.Max(0f, ultCdTimer);
        /// <summary>Z 레이저 광선 상태(레이저 빌드 전용).</summary>
        public GunnerLaserBeam Beam => beam;

        /// <summary>캐릭터가 보는 방향(원본 `p.facing`).</summary>
        public int Facing => mover != null ? mover.Facing : 1;

        // ---- HUD 스킬 슬롯 (원본 syncHUD 슬롯 부분 :6319~:6366, 버프 줄 :6312~:6317) ----

        /// <summary>
        /// Z 칸 — 원본 `clamp(p.atkCds.gunner / p.gunnerCdMax, 0, 1)`(:6322)과 남은 초. 레이저 빌드가 광선을 쏘는
        /// 동안에는 총알 쿨다운이 안 걸려서 비어 있다(원본도 광선은 `atkCds.gunner`를 건드리지 않는다).
        /// </summary>
        public SkillSlotState AttackSlot => SkillSlotState.Cooldown(cdTimer, attackCdMax);

        /// <summary>
        /// X 칸 — 원본 `clamp(p.ultCd / p.ultCdMax, 0, 1)`(:6352). 갈래가 있으면 오른쪽 위에 충전 스택(레이저)
        /// 또는 부품(설치기) `n/최대`(:6358), 설치기인데 부품이 0개면 `noStack`(:6364).
        /// </summary>
        public SkillSlotState SkillSlot
        {
            get
            {
                var b = Branch;
                bool stackSkill = b != GunnerBranch.None;
                return SkillSlotState.Cooldown(ultCdTimer, ultCdMax,
                    stackSkill ? Stacks : 0, stackSkill ? StackMax : 0,
                    noStack: b == GunnerBranch.Installer && Stacks <= 0);
            }
        }

        /// <summary>
        /// 원본 버프 줄의 메카닉 몫(:6312~:6317) — 레이저는 `충전 n/최대`, 설치기는
        /// `부품 n/최대 · 적중 k/10 · 설치기 a/b · 링크 L`. 링크 거리는 원본이 보여주는 숫자 그대로(px, 우리 단위×100).
        /// </summary>
        public string BuffText
        {
            get
            {
                var b = Branch;
                if (b == GunnerBranch.Laser) return $"충전 {Stacks}/{StackMax}";
                if (b != GunnerBranch.Installer) return "";
                int t = InstallerTier;
                return $"부품 {Stacks}/{StackMax} · 적중 {PartHits}/{GunnerSpecConfig.PartHitsPerPart}" +
                       $" · 설치기 {GunnerTurret.Active.Count}/{GunnerSpecConfig.TurretMax(t)}" +
                       $" · 링크 {Mathf.RoundToInt(GunnerSpecConfig.LinkMax(t) * 100f)}";
            }
        }

        /// <summary>
        /// 발 위치 — 원본 `(p.x, p.y)`(<see cref="PlayerBody.Feet"/>). 원본의 `p.y - N` 꼴 높이
        /// (총구·빔·설치기·드론)는 전부 여기서 잰다.
        /// </summary>
        public Vector2 FeetPosition => PlayerBody.Feet(transform, bodyCol);

        public void OnSelected()
        {
            cdTimer = 0f;
            ultCdTimer = 0f;
        }

        public void OnDeselected()
        {
            beam?.Stop();
            if (fieldVisualRoot != null) fieldVisualRoot.SetActive(false);
        }

        /// <summary>
        /// 새 사냥 시작 — 원본 `resetPlayerForRun()`(:1516~:1537): 쿨다운 초기화, 충전 스택 0,
        /// **설치기 빌드는 부품 1개를 쥐고 시작**, 부품 적중 수 0.
        /// </summary>
        public void ResetForRun()
        {
            OnSelected();
            Stacks = Branch == GunnerBranch.Installer ? 1 : 0;
            PartHits = 0;
        }

        void Awake()
        {
            mover = GetComponent<CharacterMover2D>();
            bodyCol = GetComponent<CircleCollider2D>();
            beam = new GunnerLaserBeam(this, mover);
        }

        /// <summary>
        /// Run을 벗어나는 순간 광선·장판 표시를 꺼 둔다 — 아래 <see cref="Update"/>의 IsRunning 가드만으로는
        /// "광선을 쏘는 중에 죽어서 Run을 벗어난" 경우 그 표시가 로비까지 그대로 남는 걸 못 막는다(Update
        /// 자체가 멈추니 꺼 줄 코드도 같이 멈춘다). <see cref="OnDeselected"/>와 같은 정리를 재사용한다.
        /// </summary>
        void OnEnable() => GameState.Changed += HandleGameStateChanged;
        void OnDisable() => GameState.Changed -= HandleGameStateChanged;
        void HandleGameStateChanged(GameScene scene)
        {
            if (scene != GameScene.Run) OnDeselected();
        }

        void Update()
        {
            // 원본 statAtk()/statAs()가 호출마다 다시 계산되는 것과 같은 방식 — 필드 자기 자신이 아니라
            // 항상 프로필+상수에서 새로 굴리므로 매 프레임 곱해져 누적되는 버그가 없다.
            var profile = ProfileService.Current;
            int laserTier = LaserTier;
            baseDamage = PlayerStatCalculator.ComputeAtk(profile) * GunnerSpecConfig.BulletDamageMult(laserTier);
            // 원본 `p.gunnerCdMax = ... / statAs() * cdMult()`(:2196) — '시간의 조각'이 여기에도 붙는다.
            cooldown = GunnerSpecConfig.BulletBaseCooldown(laserTier)
                       / PlayerStatCalculator.ComputeAttackSpeedMultiplier(profile)
                       * PlayerStatCalculator.ComputeCooldownMultiplier(profile);

            float dt = Time.deltaTime;
            cdTimer -= dt;
            ultCdTimer -= dt;

            // 로비 등 Run 밖에서는 "새 입력을 받아 액션을 거는" 부분만 막는다 — 위 쿨다운·피해량
            // 재계산과 아래 UpdateField(dt)는 그대로 둬야 한다(PlayMode 테스트가 Update()를 직접 돌려서
            // 확인하는데, 테스트의 GameState는 기본 Lobby라 여기를 통째로 막으면 Field_* 테스트들이
            // 깨진다 — 실제로 한 번 그렇게 했다가 깨뜨렸다). 막아야 하는 건 GameInput.AttackHeld/
            // AttackDown/UltDown을 "새로" 읽어 공격·스킬을 거는 지점뿐이다 — GameInput.MenuConfirmDown이
            // 같은 Z를 메뉴 확인에도 쓰기 때문에(10-24), 로비 진입 직후 메뉴를 확인하려는 Z가 그대로
            // 총알 발사로 읽히는 게 이번 버그였다.
            if (GameState.IsRunning)
            {
                if (GameInput.UltDown) TryUseSkill();

                // 원본(:3538): `if (!updateGunnerBeam(dt, held) && !p.attacking && p.atkCds.gunner <= 0 && (held || justAtk)) gunnerFire();`
                // 레이저 빌드는 누르고 있는 동안 광선이 켜져서 총알이 안 나간다.
                bool held = GameInput.AttackHeld;
                bool beamOn = UpdateBeam(dt, held);
                if (!beamOn && cdTimer <= 0f && (held || GameInput.AttackDown))
                {
                    Fire();
                    cdTimer = attackCdMax = cooldown;
                }
            }

            UpdateField(dt);
        }

        /// <summary>원본 `updateGunnerBeam(dt, held)` — 광선이 켜져 있으면 true.</summary>
        bool UpdateBeam(float dt, bool held) => beam.Update(dt, held, LaserTier);

        /// <summary>
        /// 원본 `gunnerFire()` — 정면으로 총알 하나. 레이저 빌드 티어(`laserTier`)에 따라 피해·관통·수명·크기가
        /// 바뀌고 5층은 유도탄이 된다. 적중마다 스택 +1(`stackOnHit: 1`, 갈래가 없으면 무시).
        /// </summary>
        void Fire()
        {
            int laserTier = LaserTier;
            int facing = Facing;
            Vector2 feet = FeetPosition;
            Vector3 spawnPos = new Vector3(feet.x + facing * MuzzleForward, feet.y + MuzzleHeight, 0f);
            Vector2 velocity = new Vector2(facing * BulletSpeed, 0f);

            GunnerBullet.Spawn(bulletPrefab, spawnPos, velocity, baseDamage,
                GunnerSpecConfig.BulletPierce(laserTier), GunnerSpecConfig.BulletLife(laserTier),
                GunnerSpecConfig.BulletSize(laserTier), laserTier > 0 ? LaserColor : bulletColor,
                homing: GunnerSpecConfig.BulletHoming(laserTier),
                seekRange: GunnerSpecConfig.BulletSeekRange(laserTier),
                turnRate: GunnerSpecConfig.BulletTurnRate(laserTier),
                stackOwner: this, stackOnHit: GunnerSpecConfig.BulletStackOnHit);
        }

        /// <summary>
        /// 원본 `addGunnerStack(n)` — 갈래가 없으면 아무것도 안 한다.
        /// - 레이저: 스택 += n (최대치에서 자른다).
        /// - 설치기: 부품이 가득 차 있으면 세지 않는다. 적중 수를 더해 10마다 부품 +1, 가득 차면 적중 수를 0으로.
        /// </summary>
        /// <returns>갱신 후 스택(부품) 수.</returns>
        public int AddStack(int n)
        {
            var b = Branch;
            if (b == GunnerBranch.None) return 0;
            int max = StackMax;
            if (b == GunnerBranch.Installer)
            {
                if (Stacks >= max) return Stacks;
                PartHits += n;
                while (PartHits >= GunnerSpecConfig.PartHitsPerPart && Stacks < max)
                {
                    PartHits -= GunnerSpecConfig.PartHitsPerPart;
                    Stacks = Mathf.Clamp(Stacks + 1, 0, max);
                }
                if (Stacks >= max) PartHits = 0;
                return Stacks;
            }
            Stacks = Mathf.Clamp(Stacks + n, 0, max);
            return Stacks;
        }

        /// <summary>
        /// 원본 `tryGunnerSkill()` — X 스킬. 쿨다운 중이거나 빌드가 없으면 아무 일도 없다(쿨다운도 안 돈다).
        /// </summary>
        void TryUseSkill()
        {
            if (ultCdTimer > 0f) return;
            var b = Branch;
            if (b == GunnerBranch.None) return; // 원본: "메카닉 빌드를 먼저 습득하자"
            int tier = Tier;
            float cdMult = PlayerStatCalculator.ComputeCooldownMultiplier(ProfileService.Current);

            if (b == GunnerBranch.Laser)
            {
                // 충전 스택은 소환하는 순간 전부 드론 지속시간으로 바뀐다(`consumeGunnerStacks`).
                int stackSpend = Stacks;
                Stacks = 0;
                GunnerDrone.Summon(dronePrefab, this, tier, stackSpend);
                HitFeel.Shake(3f + tier * 0.5f, 0.14f); // 원본 :2401
                ultCdTimer = ultCdMax = GunnerSpecConfig.DroneUltCooldown(tier) * cdMult;
                return;
            }

            // 설치기 — 부품이 없으면 못 놓고, 쿨다운도 안 돈다(`consumeGunnerStackOne`이 0이면 return).
            if (Stacks <= 0) return; // 원본: "부품 필요 · 공격 적중 10회로 충전"
            Stacks--;
            var turrets = GunnerTurret.Active;
            if (turrets.Count >= GunnerSpecConfig.TurretMax(tier))
            {
                // 가장 오래된 것(`z.t`가 가장 큰 것)을 치운다.
                GunnerTurret oldest = null;
                foreach (var t in turrets)
                    if (oldest == null || t.Age > oldest.Age) oldest = t;
                oldest?.Remove();
            }
            // 원본 `placeGunnerTurret`는 `y: p.y` — 발 높이(바닥)에 놓는다.
            GunnerTurret.Place(turretPrefab, FeetPosition, Facing, tier);
            HitFeel.Shake(2f + tier * 0.4f, 0.1f); // 원본 :2416
            ultCdTimer = ultCdMax = GunnerSpecConfig.TurretUltCooldown(tier) * cdMult;
        }

        /// <summary>
        /// 원본 `updateZones()` 앞부분(:3766~:3797) — 설치기 빌드에서 장판을 이루는 설치기가 2개 이상이면
        /// 틱마다 링크·내부 장판 피해. 2개 미만이면 틱 누적을 0으로 되돌린다.
        /// (원본은 무기가 메카닉일 때만 계산한다 — 이 키트가 꺼지면 Update 자체가 안 돌아서 같은 결과다.)
        /// </summary>
        void UpdateField(float dt)
        {
            int t = InstallerTier;
            var fieldTurrets = GunnerField.FieldTurrets(t);
            if (fieldTurrets.Count >= 2)
            {
                fieldTickTimer += dt;
                float tick = GunnerSpecConfig.FieldTick(t);
                var pts = GunnerField.FieldPoints(fieldTurrets);
                var segs = GunnerField.LinkSegments(pts, GunnerSpecConfig.LinkMax(t));
                if (fieldTickTimer >= tick)
                {
                    fieldTickTimer -= tick;
                    GunnerField.DamageTick(t, pts, segs);
                }
                UpdateFieldVisual(segs);
            }
            else
            {
                fieldTickTimer = 0f;
                if (fieldVisualRoot != null) fieldVisualRoot.SetActive(false);
            }
        }

        // ---- 링크 표시(원본 drawMechanicField :5194의 최소한 — 연결선만 얇은 막대로) ----
        void UpdateFieldVisual(List<GunnerField.Segment> segs)
        {
            if (fieldVisualRoot == null)
            {
                fieldVisualRoot = new GameObject("GunnerFieldLinks");
                fieldVisualRoot.AddComponent<RunTransient>(); // 설치기와 같이 런이 끝나면 치워진다
                linkVisuals.Clear();
            }
            fieldVisualRoot.SetActive(true);
            while (linkVisuals.Count < segs.Count)
            {
                var go = new GameObject("Link");
                go.transform.SetParent(fieldVisualRoot.transform, false);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = bulletSprite;
                sr.color = new Color(LaserColor.r, LaserColor.g, LaserColor.b, 0.6f);
                sr.sortingOrder = 3;
                linkVisuals.Add(sr);
            }
            for (int i = 0; i < linkVisuals.Count; i++)
            {
                bool on = i < segs.Count;
                linkVisuals[i].enabled = on;
                if (!on) continue;
                Vector2 a = segs[i].a, b = segs[i].b, v = b - a;
                var tf = linkVisuals[i].transform;
                tf.position = (a + b) * 0.5f;
                tf.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg);
                tf.localScale = new Vector3(Mathf.Max(0.01f, v.magnitude), 0.06f, 1f);
            }
        }
    }
}
