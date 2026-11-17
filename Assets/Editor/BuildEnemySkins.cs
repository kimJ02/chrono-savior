using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// **몬스터 지역 그림 연결**(2026-11-17, 사용자 "2스테이지 몬스터 적용" — 팀원 그림 8종, 32×32 움직임 4장 + 피격 1장).
    /// 메뉴 한 번으로: ① `Assets/Sprites/Enemies/&lt;이름&gt;/` 그림의 가져오기 설정(Point·무압축·PPU 128 — 도트 배율 2는 크기 계산이 맞춘다),
    /// ② 이름마다 움직임 <see cref="SpriteFlipbook"/>(`Assets/Data/Enemies/Skins/Flipbook_&lt;이름&gt;_Move`)을 **제자리 갱신**(GUID 유지 — 지우고 새로 만들지 않는다),
    /// ③ 아래 <see cref="Assignments"/> 대로 `EnemyData.regionSkins`에 연결, ④ 몹 공용 프리팹 `Enemy_Base`에 <see cref="EnemySkinAnimator"/>가 없으면 붙인다.
    /// **이 목록이 "어느 지역에서 어느 몹이 어떤 그림인지"의 원본이다** — 에셋을 손으로 고치지 말고 여기를 고쳐 다시 돌린다.
    /// 그림 파일 이름 규칙: `Enemy_&lt;이름&gt;_Move_01..`, `Enemy_&lt;이름&gt;_Hit_01`.
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
        /// 오크통만 도트 4: 이 프로젝트 관례는 그림 높이 = 판정 지름(오우거 0.87)인데 32px × 도트 2 = 0.5라 0.19 떠 보였다 —
        /// 가장 가까운 정수 배율 4(1.0유닛, 바닥 0.065 묻힘)로(GPT 상의). 그림 전용 자식 렌더러로 발을 맞추는 정리는 엘리트 뜸과 묶어 따로.
        /// </summary>
        static readonly (EnemyType type, string art, int fromRegion, int dot)[] Assignments =
        {
            (EnemyType.Flyer, "Crow", 1, DotScale.Value),
            (EnemyType.Grunt, "Rat", 1, DotScale.Value),
            (EnemyType.Charger, "Boar", 1, DotScale.Value),
            (EnemyType.Brute, "Barrel", 1, 4),
        };

        [MenuItem("ChronoSavior/몬스터 지역 그림 연결")]
        public static void Build()
        {
            var problems = new List<string>();
            var books = new Dictionary<string, (SpriteFlipbook move, Sprite hit)>();
            foreach (var dir in Directory.GetDirectories(ArtRoot).Select(d => d.Replace('\\', '/')).OrderBy(d => d))
            {
                string name = Path.GetFileName(dir);
                var move = LoadFrames(dir, $"Enemy_{name}_Move_", problems);
                var hits = LoadFrames(dir, $"Enemy_{name}_Hit_", problems);
                if (move.Count == 0) { problems.Add($"{name}: 움직임 그림이 없다(Enemy_{name}_Move_01..)"); continue; }
                books[name] = (UpsertFlipbook(name, move), hits.FirstOrDefault());
            }

            var catalog = AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
            foreach (var (type, art, from, dot) in Assignments)
            {
                var data = catalog != null ? catalog.types.FirstOrDefault(d => d != null && d.type == type) : null;
                if (data == null) { problems.Add($"{type}: EnemyData가 없다"); continue; }
                if (!books.TryGetValue(art, out var b)) { problems.Add($"{type}: 그림 '{art}'이 없다({ArtRoot}/{art})"); continue; }
                // 이 목록이 원본이다 — 예전 칸(예: 처음 만든 2지역 칸)은 지우고 목록대로 다시 쓴다.
                var list = new List<RegionSkin> { new RegionSkin { fromRegion = from, move = b.move, hit = b.hit, facesLeft = true, dotScale = dot } };
                data.regionSkins = list.OrderBy(s => s.fromRegion).ToArray();
                EditorUtility.SetDirty(data);
            }

            EnsureAnimatorOnBasePrefab(problems);
            AssetDatabase.SaveAssets();
            foreach (var p in problems) Debug.LogError("[BuildEnemySkins] " + p);
            Debug.Log($"[BuildEnemySkins] 그림 {books.Count}종, 배정 {Assignments.Length}개" + (problems.Count > 0 ? $", 문제 {problems.Count}개" : ""));
        }

        public static void RunBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static List<Sprite> LoadFrames(string dir, string prefix, List<string> problems)
        {
            var paths = Directory.GetFiles(dir, prefix + "*.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p, System.StringComparer.Ordinal).ToList();
            var sprites = new List<Sprite>();
            foreach (var path in paths)
            {
                ApplyImport(path);
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (s == null) problems.Add($"그림을 못 읽었다: {path}");
                else sprites.Add(s);
            }
            return sprites;
        }

        /// <summary>도트 그림 설정 — 고블린·박쥐(`Sprites/Prototype`)와 같은 가운데 기준점, 대신 Point·무압축(도트가 뭉개지지 않게).</summary>
        static void ApplyImport(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (ti == null) return;
            bool changed = ti.textureType != TextureImporterType.Sprite || ti.spriteImportMode != SpriteImportMode.Single
                || !Mathf.Approximately(ti.spritePixelsPerUnit, DotScale.GridPixelsPerUnit) || ti.filterMode != FilterMode.Point
                || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled;
            if (!changed) return;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = DotScale.GridPixelsPerUnit;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
        }

        static SpriteFlipbook UpsertFlipbook(string name, List<Sprite> frames)
        {
            if (!AssetDatabase.IsValidFolder(OutRoot)) AssetDatabase.CreateFolder("Assets/Data/Enemies", "Skins");
            string path = $"{OutRoot}/Flipbook_{name}_Move.asset";
            var book = AssetDatabase.LoadAssetAtPath<SpriteFlipbook>(path);
            bool isNew = book == null;
            if (isNew) book = ScriptableObject.CreateInstance<SpriteFlipbook>();
            book.frames = frames.ToArray();
            book.fps = Fps.TryGetValue(name, out var f) ? f : DefaultFps;
            book.loop = true;
            if (isNew) AssetDatabase.CreateAsset(book, path);
            else EditorUtility.SetDirty(book);
            return book;
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
