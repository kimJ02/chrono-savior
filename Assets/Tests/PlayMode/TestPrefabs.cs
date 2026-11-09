using UnityEngine;
using ChronoSavior.Characters;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{
    /// <summary>
    /// 테스트가 **실제 프리팹**을 쓰게 한다(2026-11-05 생성 책임 2-3) — 투사체·소환물 구성은 프리팹 파일이 원본이라, 테스트가 같은 구성을 코드로 다시 짜면
    /// 프리팹이 틀려도 모른다. 원본 에셋은 고치지 않고 넘겨주기만 한다(생성 함수가 인스턴스를 만든다).
    /// PlayMode 테스트는 이 프로젝트에서 에디터에서만 돈다 — 에셋은 `AssetDatabase`로 읽는다.
    /// </summary>
    static class TestPrefabs
    {
        public const string CombatFolder = "Assets/Prefabs/Combat";

        public static T Load<T>(string name) where T : Component
        {
#if UNITY_EDITOR
            var go = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"{CombatFolder}/{name}.prefab");
            var c = go != null ? go.GetComponent<T>() : null;
            if (c == null) throw new System.InvalidOperationException($"테스트 프리팹 {CombatFolder}/{name}.prefab({typeof(T).Name})이 없다");
            return c;
#else
            throw new System.NotSupportedException("TestPrefabs는 에디터에서만 쓴다");
#endif
        }

        public static GunnerBullet GunnerBullet => Load<GunnerBullet>("GunnerBullet");
        public static MageProjectile MageBolt => Load<MageProjectile>("MageBolt");
        public static EnemyBolt EnemyBolt => Load<EnemyBolt>("EnemyBolt");
        public static GunnerDrone GunnerDrone => Load<GunnerDrone>("GunnerDrone");
        public static GunnerTurret GunnerTurret => Load<GunnerTurret>("GunnerTurret");
        public static ChronoSavior.Systems.ExpOrb ExpOrb => Load<ChronoSavior.Systems.ExpOrb>("ExpOrb");

        /// <summary>1스테이지(숲) 그림 묶음 — 게임 씬의 `StageVisuals`가 쓰는 에셋.</summary>
        public static ChronoSavior.World.StageArtSet StageArt
        {
            get
            {
#if UNITY_EDITOR
                var a = UnityEditor.AssetDatabase.LoadAssetAtPath<ChronoSavior.World.StageArtSet>(StageArtPath);
                if (a == null) throw new System.InvalidOperationException("스테이지 그림 묶음이 없다: " + StageArtPath);
                return a;
#else
                throw new System.NotSupportedException("TestPrefabs는 에디터에서만 쓴다");
#endif
            }
        }

        public const string StageArtPath = "Assets/Data/Stages/StageArt_Forest.asset";

        /// <summary>효과 머티리얼(SpriteFlash) — 게임 씬·몹 프리팹이 쓰는 것.</summary>
        public static Material SpriteFlash => LoadAsset<Material>("Assets/Materials/SpriteFlash.mat");
        /// <summary>선·메시 효과 머티리얼(게임에선 Player의 BladeVfx·GunnerFieldVfx·GunnerAttack 칸에 꽂혀 있다).</summary>
        public static Material VfxUnlit => LoadAsset<Material>("Assets/Materials/VfxUnlit.mat");

        /// <summary>
        /// 섬영 베기 테스트용 프레임 — **특정 파일에 기대지 않는다**(효과 그림은 외주로 바꿀 예정이라 파일이 옮겨져도 테스트가 깨지지 않게, 타격음과 같은 방식).
        /// 지금 게임 프레임과 같은 규격(128×128·128 PPU, 9장)으로 만든다. 씬에 실제로 연결된 프레임은 `GameBootTests`가 본다.
        /// </summary>
        public static Sprite[] BladeSlashFrames
        {
            get
            {
                // 한 번만 만들어 공유한다 — 부를 때마다 만들면 텍스처·그림이 테스트 내내 쌓인다(Codex 지적). 씬을 바꿔도 남게 HideAndDontSave.
                if (slashFrames != null && System.Array.TrueForAll(slashFrames, f => f != null)) return slashFrames;
                slashFrames = new Sprite[9];
                for (int i = 0; i < slashFrames.Length; i++)
                {
                    var tex = new Texture2D(128, 128) { filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                    slashFrames[i] = Sprite.Create(tex, new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
                    slashFrames[i].name = $"TestSlash_{i:00}";
                    slashFrames[i].hideFlags = HideFlags.HideAndDontSave;
                }
                return slashFrames;
            }
        }

        static Sprite[] slashFrames;

        static T LoadAsset<T>(string path) where T : Object
        {
#if UNITY_EDITOR
            var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) throw new System.InvalidOperationException("테스트 에셋이 없다: " + path);
            return a;
#else
            throw new System.NotSupportedException("TestPrefabs는 에디터에서만 쓴다");
#endif
        }

        public static MageChargeVfx Equipped(this MageChargeVfx flash) { flash.FlashMaterial = SpriteFlash; return flash; }
        public static PlayerAfterimages Equipped(this PlayerAfterimages ai) { ai.GhostMaterial = SpriteFlash; return ai; }
        public static BladeSlashVfx Equipped(this BladeSlashVfx vfx) { vfx.Frames = BladeSlashFrames; return vfx; }
        public static EnemyHealth Equipped(this EnemyHealth health) { health.FlashMaterial = SpriteFlash; return health; }

        /// <summary>테스트가 만든 무기·몹에 게임과 같은 탄 프리팹을 끼운다(게임에선 20_Combat의 Player·몹 프리팹에 꽂혀 있다). `AddComponent&lt;MageAttack&gt;().Equipped()`.</summary>
        public static MageAttack Equipped(this MageAttack mage) { mage.BoltPrefab = MageBolt; return mage; }
        public static GunnerAttack Equipped(this GunnerAttack gunner)
        {
            gunner.BulletPrefab = GunnerBullet;
            gunner.DronePrefab = GunnerDrone;
            gunner.TurretPrefab = GunnerTurret;
            gunner.BeamMaterial = VfxUnlit;
            return gunner;
        }
        public static EnemyShooterMotion Equipped(this EnemyShooterMotion shooter) { shooter.BoltPrefab = EnemyBolt; return shooter; }
        public static Boss Equipped(this Boss boss) { boss.BoltPrefab = EnemyBolt; return boss; }
    }
}
