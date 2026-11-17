using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// **몬스터 지역 그림 연결**(2026-11-17, 사용자 "2스테이지 몬스터 적용" — 팀원 그림 8종, 32×32 움직임 4장 + 피격 1장).
    /// 메뉴 한 번으로: ① `Assets/Sprites/Enemies/&lt;이름&gt;/` 그림의 가져오기 설정(Point·무압축·PPU 128 — 도트 배율은 크기 계산이 맞춘다)과
    /// **발 기준점**(아래 참고), ② 이름마다 움직임 <see cref="SpriteFlipbook"/>(`Assets/Data/Enemies/Skins/Flipbook_&lt;이름&gt;_Move`·`_Move_Elite`)을
    /// **제자리 갱신**(GUID 유지 — 지우고 새로 만들지 않는다), ③ 아래 <see cref="Assignments"/> 대로 `EnemyData.regionSkins`에 연결,
    /// ④ 몹 공용 프리팹 `Enemy_Base`에 <see cref="EnemySkinAnimator"/>가 없으면 붙인다.
    /// **이 목록이 "어느 지역에서 어느 몹이 어떤 그림인지"의 원본이다** — 에셋을 손으로 고치지 말고 여기를 고쳐 다시 돌린다.
    /// 그림 파일 이름 규칙: `Enemy_&lt;이름&gt;_Move_01..`, `Enemy_&lt;이름&gt;_Hit_01`.
    ///
    /// **발 기준점**(2026-11-17 사용자 "몬스터들 바닥에서 뜨는 거", GPT 상의 P안): 그림 아래에 투명 줄이 있고(쥐 3px·멧돼지 5px 등) 판정 반지름과
    /// 그림 높이가 달라 떠 보였다. 땅에 서는 종류는 기준점을 "발(움직임 프레임들 중 가장 낮은 불투명 줄) + 판정 반지름" 높이에 둔다 —
    /// 루트(= 판정 중심)는 그대로라 `EnemyMove`의 Y 로직(지면·발판 착지)은 안 바뀌고 그림만 발이 판정 바닥에 닿게 내려온다.
    /// 엘리트는 판정만 ×1.35라 같은 텍스처에 엘리트 기준점 그림(`_Elite`)을 하나 더 둔다. 피격 그림도 같은 기준점. 비행(까마귀)·미배정 그림은 가운데.
    ///
    /// 배치: `-executeMethod ChronoSavior.Editor.BuildEnemySkins.RunBatch`.
    /// </summary>
    public static class BuildEnemySkins
    {
        const string ArtRoot = "Assets/Sprites/Enemies";
        const string OutRoot = "Assets/Data/Enemies/Skins";
        const string BasePrefab = "Assets/Prefabs/Enemies/Enemy_Base.prefab";

        /// <summary>그림 이름별 움직임 속도(fps). 목록에 없으면 <see cref="DefaultFps"/>.</summary>
        static readonly Dictionary<string, float> Fps = new Dictionary<string, float> { ["Crow"] = 10f };
        const float DefaultFps = 8f;

        /// <summary>
        /// (몹 종류, 그림 이름, 몇 지역부터, 도트 배율). 2026-11-17 사용자 선택 "4종 배정": 박쥐 자리 = 까마귀, 고블린 = 쥐, 멧돼지 = 멧돼지, 오우거 = 오크통.
        /// 처음엔 2지역부터였는데 사용자 "1스테이지도 바꿔라"로 1지역부터. 민들레·상자·새싹·슬라임은 그림·움직임 에셋만 만들어 둔다(배정 미정).
        /// 오크통만 도트 4: 이 프로젝트 관례는 그림 높이 = 판정 지름(오우거 0.87)인데 32px × 도트 2 = 0.5라 너무 작아 보였다 — 가장 가까운 정수 배율(GPT 상의).
        /// </summary>
        static readonly (EnemyType type, string art, int fromRegion, int dot)[] Assignments =
        {
            (EnemyType.Flyer, "Crow", 1, DotScale.Value),
            (EnemyType.Grunt, "Rat", 1, DotScale.Value),
            (EnemyType.Charger, "Boar", 1, DotScale.Value),
            (EnemyType.Brute, "Barrel", 1, 4),
        };

        sealed class Art { public SpriteFlipbook move, eliteMove; public Sprite hit, eliteHit; }

        [MenuItem("ChronoSavior/몬스터 지역 그림 연결")]
        public static void Build()
        {
            var problems = new List<string>();
            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
            EnemyData DataOf(EnemyType t) => catalog != null ? catalog.types.FirstOrDefault(d => d != null && d.type == t) : null;

            var books = new Dictionary<string, Art>();
            foreach (var dir in Directory.GetDirectories(ArtRoot).Select(Slash).OrderBy(d => d, System.StringComparer.Ordinal))
            {
                string name = Path.GetFileName(dir);
                var movePaths = Paths(dir, $"Enemy_{name}_Move_");
                var hitPaths = Paths(dir, $"Enemy_{name}_Hit_");
                if (movePaths.Count == 0) { problems.Add($"{name}: 움직임 그림이 없다(Enemy_{name}_Move_01..)"); continue; }

                int idx = System.Array.FindIndex(Assignments, a => a.art == name);
                var data = idx >= 0 ? DataOf(Assignments[idx].type) : null;
                bool grounded = data != null && Assignments[idx].type != EnemyType.Flyer;
                int dot = idx >= 0 ? Assignments[idx].dot : DotScale.Value;
                float pxPerUnit = DotScale.GridPixelsPerUnit / dot; // 그림 1픽셀 = 1/pxPerUnit 유닛
                int feet = movePaths.Min(BottomGap);
                float PivotPx(float radius) => grounded ? feet + radius * pxPerUnit : -1f; // 아래에서 픽셀, −1 = 가운데
                float normalPx = PivotPx(data != null ? data.colliderRadius : 0f);
                float elitePx = PivotPx(data != null ? data.colliderRadius * DifficultyScalingConfig.EliteScale : 0f);

                var move = movePaths.Select(p => ApplyImport(p, normalPx, elitePx, problems)).ToList();
                var hit = hitPaths.Count > 0 ? ApplyImport(hitPaths[0], normalPx, elitePx, problems) : (null, null);
                if (move.Any(m => m.normal == null || m.elite == null)) continue;
                books[name] = new Art
                {
                    move = UpsertFlipbook($"Flipbook_{name}_Move", move.Select(m => m.normal).ToList(), name),
                    eliteMove = UpsertFlipbook($"Flipbook_{name}_Move_Elite", move.Select(m => m.elite).ToList(), name),
                    hit = hit.normal,
                    eliteHit = hit.elite,
                };
            }

            foreach (var (type, art, from, dot) in Assignments)
            {
                var data = DataOf(type);
                if (data == null) { problems.Add($"{type}: EnemyData가 없다"); continue; }
                if (!books.TryGetValue(art, out var b)) { problems.Add($"{type}: 그림 '{art}'이 없다({ArtRoot}/{art})"); continue; }
                // 이 목록이 원본이다 — 예전 칸은 지우고 목록대로 다시 쓴다.
                data.regionSkins = new[]
                {
                    new RegionSkin { fromRegion = from, move = b.move, hit = b.hit, eliteMove = b.eliteMove, eliteHit = b.eliteHit, facesLeft = true, dotScale = dot },
                };
                EditorUtility.SetDirty(data);
            }

            EnsureAnimatorOnBasePrefab(problems);
            BuildBossArt(problems);
            AssetDatabase.SaveAssets();
            foreach (var p in problems) Debug.LogError("[BuildEnemySkins] " + p);
            Debug.Log($"[BuildEnemySkins] 그림 {books.Count}종, 배정 {Assignments.Length}개" + (problems.Count > 0 ? $", 문제 {problems.Count}개" : ""));
        }

        public static void RunBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static string Slash(string p) => p.Replace(Path.DirectorySeparatorChar, '/');

        static List<string> Paths(string dir, string prefix) =>
            Directory.GetFiles(dir, prefix + "*.png").Select(Slash).OrderBy(p => p, System.StringComparer.Ordinal).ToList();

        /// <summary>그림 아래쪽 투명 줄 수 — 파일을 직접 읽는다(텍스처 읽기 권한과 무관). 테스트도 쓴다.</summary>
        public static int BottomGap(string path)
        {
            var tex = new Texture2D(2, 2);
            try
            {
                tex.LoadImage(File.ReadAllBytes(path));
                for (int y = 0; y < tex.height; y++) // LoadImage는 아래 줄이 y = 0
                    for (int x = 0; x < tex.width; x++)
                        if (tex.GetPixel(x, y).a > 0.01f) return y;
                return 0;
            }
            finally { Object.DestroyImmediate(tex); }
        }

        /// <summary>
        /// 도트 그림 설정(Point·무압축·PPU 128) + 그림 두 장: `&lt;파일&gt;`(일반 기준점)·`&lt;파일&gt;_Elite`(엘리트 기준점). 기준점은 아래에서 픽셀(−1이면 가운데).
        /// 다시 돌려도 그림 ID는 그대로라 연결이 안 끊긴다.
        /// </summary>
        static (Sprite normal, Sprite elite) ApplyImport(string path, float pivotPx, float elitePivotPx, List<string> problems)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (ti == null) { problems.Add($"그림을 못 읽었다: {path}"); return (null, null); }
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Multiple;
            ti.spritePixelsPerUnit = DotScale.GridPixelsPerUnit;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.GetSourceTextureWidthAndHeight(out int w, out int h);
            string baseName = Path.GetFileNameWithoutExtension(path);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var dp = factory.GetSpriteEditorDataProviderFromObject(ti);
            dp.InitSpriteEditorDataProvider();
            var old = new Dictionary<string, GUID>();
            foreach (var r in dp.GetSpriteRects()) old[r.name] = r.spriteID;
            SpriteRect Rect(string n, float px) => new SpriteRect
            {
                name = n,
                rect = new Rect(0, 0, w, h),
                alignment = SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, px < 0f ? 0.5f : px / h),
                spriteID = old.TryGetValue(n, out var id) ? id : GUID.Generate(),
            };
            var rects = new[] { Rect(baseName, pivotPx), Rect(baseName + "_Elite", elitePivotPx) };
            dp.SetSpriteRects(rects);
            var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
            if (names != null) names.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
            dp.Apply();
            ti.SaveAndReimport();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToList();
            var normal = sprites.FirstOrDefault(s => s.name == baseName);
            var elite = sprites.FirstOrDefault(s => s.name == baseName + "_Elite");
            if (normal == null || elite == null) problems.Add($"그림 두 장을 못 만들었다: {path}");
            return (normal, elite);
        }

        static SpriteFlipbook UpsertFlipbook(string assetName, List<Sprite> frames, string artName)
        {
            if (!AssetDatabase.IsValidFolder(OutRoot)) AssetDatabase.CreateFolder("Assets/Data/Enemies", "Skins");
            string path = $"{OutRoot}/{assetName}.asset";
            var book = AssetDatabase.LoadAssetAtPath<SpriteFlipbook>(path);
            bool isNew = book == null;
            if (isNew) book = ScriptableObject.CreateInstance<SpriteFlipbook>();
            book.frames = frames.ToArray();
            book.fps = Fps.TryGetValue(artName, out var f) ? f : DefaultFps;
            book.loop = true;
            if (isNew) AssetDatabase.CreateAsset(book, path);
            else EditorUtility.SetDirty(book);
            return book;
        }

        const string BossArtRoot = "Assets/Resources/Art/Enemies/BlobLord";
        const string BossPrefab = "Assets/Prefabs/Enemies/Enemy_BlobLord.prefab";

        /// <summary>
        /// 1지역 보스 '덩어리 지배자' 몸 그림(2026-11-17 — 팀원 2026-11-16 그림, 예전엔 `BlobLordBoss`가 경로 문자열로 불러왔다): 동작마다 `SpriteFlipbook`
        /// (`Flipbook_BlobLord_&lt;동작&gt;`)을 만들어 보스 프리팹 칸에 꽂는다. 가져오기 설정(80×64 캔버스·기준점)은 팀원 것 그대로 둔다.
        /// 튀어오르기·투척은 상태 진행도로 몇 번째 장인지 고르고(fps는 안 쓴다), 이동(넉백 슬라이딩)·사망은 fps로 돈다.
        /// </summary>
        static readonly (string action, float fps, bool loop, System.Action<BlobLordBoss, SpriteFlipbook> set)[] BossBooks =
        {
            ("Bounce", 10f, false, (b, f) => b.BounceArt = f),
            ("Throw", 10f, false, (b, f) => b.ThrowArt = f),
            ("Move", 10f, true, (b, f) => b.MoveArt = f),
            ("Hit", 10f, false, (b, f) => b.HitArt = f),
            ("Death", 8f, false, (b, f) => b.DeathArt = f),
        };

        static void BuildBossArt(List<string> problems)
        {
            var root = PrefabUtility.LoadPrefabContents(BossPrefab);
            if (root == null) { problems.Add("Enemy_BlobLord 프리팹을 못 열었다"); return; }
            try
            {
                var boss = root.GetComponent<BlobLordBoss>();
                if (boss == null) { problems.Add("Enemy_BlobLord에 BlobLordBoss가 없다"); return; }
                foreach (var (action, fps, loop, set) in BossBooks)
                {
                    var frames = Paths(BossArtRoot, $"Enemy_BlobLord_{action}_").Select(AssetDatabase.LoadAssetAtPath<Sprite>).ToList();
                    if (frames.Count == 0 || frames.Any(f => f == null)) { problems.Add($"보스 그림이 없다: {BossArtRoot}/Enemy_BlobLord_{action}_NN"); continue; }
                    string path = $"{OutRoot}/Flipbook_BlobLord_{action}.asset";
                    var book = AssetDatabase.LoadAssetAtPath<SpriteFlipbook>(path);
                    bool isNew = book == null;
                    if (isNew) book = ScriptableObject.CreateInstance<SpriteFlipbook>();
                    book.frames = frames.ToArray(); book.fps = fps; book.loop = loop;
                    if (isNew) AssetDatabase.CreateAsset(book, path); else EditorUtility.SetDirty(book);
                    set(boss, book);
                }
                PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        static void EnsureAnimatorOnBasePrefab(List<string> problems)
        {
            var root = PrefabUtility.LoadPrefabContents(BasePrefab);
            if (root == null) { problems.Add("Enemy_Base 프리팹을 못 열었다"); return; }
            try
            {
                if (root.GetComponent<EnemySkinAnimator>() != null) return;
                root.AddComponent<EnemySkinAnimator>();
                PrefabUtility.SaveAsPrefabAsset(root, BasePrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
}
