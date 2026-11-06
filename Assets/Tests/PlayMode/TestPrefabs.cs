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

        /// <summary>섬영 베기 프레임 — 게임 씬 Player의 `BladeSlashVfx`가 쓰는 것과 같은 순서(이름순).</summary>
        public static Sprite[] BladeSlashFrames
        {
            get
            {
#if UNITY_EDITOR
                var guids = UnityEditor.AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Resources/Art/Figma/Vfx/BladeSlash" });
                var list = new System.Collections.Generic.List<Sprite>();
                foreach (var g in guids) list.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)));
                list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
                return list.ToArray();
#else
                throw new System.NotSupportedException("TestPrefabs는 에디터에서만 쓴다");
#endif
            }
        }

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

        public static MageChargeFlash Equipped(this MageChargeFlash flash) { flash.FlashMaterial = SpriteFlash; return flash; }
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
            return gunner;
        }
        public static EnemyShooterMotion Equipped(this EnemyShooterMotion shooter) { shooter.BoltPrefab = EnemyBolt; return shooter; }
        public static Boss Equipped(this Boss boss) { boss.BoltPrefab = EnemyBolt; return boss; }
    }
}
