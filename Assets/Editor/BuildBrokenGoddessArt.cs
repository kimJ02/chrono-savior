using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// **2지역 보스 '부서진 여신상' 그림 연결**(2026-11-18, 사용자 "여신상 완성" — 실제 스프라이트 수령).
    /// 이 세션(클로드)은 디바이스 브릿지로만 파일을 옮길 수 있고 Unity 에디터를 직접 열 수 없어서,
    /// PNG는 이미 `Assets/Resources/Art/Enemies/BrokenGoddess/`에 옮겨 놓았지만(파일명 규칙은 아래 참고)
    /// **가져오기 설정(Sprite·PPU·Point 필터)·<see cref="SpriteFlipbook"/> 생성·프리팹 연결은 여기서
    /// 메뉴로 한다** — `Editor/BuildEnemySkins.BuildBossArt`(1지역 덩어리 지배자)와 정확히 같은 패턴.
    ///
    /// 파일명 규칙: `Enemy_BrokenGoddess_&lt;동작&gt;_NN.png`(대기 Idle 4장·이동 Move 4장·잡기 Grab 7장·
    /// 휘두르기 Sweep 5장·피격 Hit 1장·사망 Death 8장 — `BrokenGoddessBoss.cs` 헤더 주석 참고).
    /// 대기·이동·피격·사망은 80×64 캔버스, 잡기·휘두르기는 100×64 캔버스(높이는 전부 64 공통이라
    /// <see cref="BrokenGoddessBoss.VisualSize"/> 하나로 일관되게 늘어난다 — PPU 값 자체는 결과 크기에
    /// 영향이 없다, `EnemySetup.FitToWorldSize`가 그림 크기와 무관하게 항상 같은 월드 크기로 맞춘다).
    ///
    /// **"확인 필요"**(이 메뉴를 실행한 사람이 눈으로 봐야 하는 것 — 이 세션은 에디터가 없어 확인 불가):
    /// ① 여섯 종류 그림이 전부 "같은 기준선"(발 위치)에서 그려졌는지 — 보스는 중심 기준점(0.5,0.5)을
    /// 그대로 쓰므로, 포즈마다 캐릭터가 캔버스 안에서 세로로 다른 위치에 있으면 자세가 바뀔 때마다
    /// 몸이 위아래로 튀어 보인다. ② <see cref="BrokenGoddessBoss.VisualSize"/>(1.875×1.50, 세로는 공통
    /// 보스 기준 유지·가로만 80:64 비율로 보정)가 실제로 봤을 때 자연스러운 크기인지 — 아니면 팀장과
    /// 상의해 숫자만 조정(그림을 다시 가져올 필요 없음).
    /// </summary>
    public static class BuildBrokenGoddessArt
    {
        const string ArtRoot = "Assets/Resources/Art/Enemies/BrokenGoddess";
        const string OutRoot = "Assets/Data/Enemies/Skins";
        const string BossPrefab = "Assets/Prefabs/Enemies/Enemy_BrokenGoddess.prefab";

        /// <summary>(동작 이름, 장수(확인용, 안 맞아도 진행은 한다), fps(대기·이동·사망만 실제로 쓰인다 — 잡기·휘두르기는
        /// BrokenGoddessBoss가 상태 진행도로 직접 고르므로 fps는 재생에 안 쓰인다, BlobLord의 튀어오르기·투척과 같음),
        /// 되풀이 여부, 꽂을 필드).</summary>
        static readonly (string action, int expectedCount, float fps, bool loop, System.Action<BrokenGoddessBoss, SpriteFlipbook> set)[] Books =
        {
            ("Idle",  4, 4f, true,  (b, f) => b.IdleArt = f),
            ("Move",  4, 6f, true,  (b, f) => b.MoveArt = f),
            ("Grab",  7, 4f, false, (b, f) => b.GrabArt = f),
            ("Sweep", 5, 4f, false, (b, f) => b.SweepArt = f),
            ("Hit",   1, 8f, false, (b, f) => b.HitArt = f),
            ("Death", 8, 8f, false, (b, f) => b.DeathArt = f),
        };

        [MenuItem("ChronoSavior/보스 그림 연결/부서진 여신상")]
        public static void Build()
        {
            var problems = new System.Collections.Generic.List<string>();
            var root = PrefabUtility.LoadPrefabContents(BossPrefab);
            if (root == null) { Debug.LogError("[BuildBrokenGoddessArt] Enemy_BrokenGoddess 프리팹을 못 열었다"); return; }

            try
            {
                var boss = root.GetComponent<BrokenGoddessBoss>();
                if (boss == null) { Debug.LogError("[BuildBrokenGoddessArt] Enemy_BrokenGoddess에 BrokenGoddessBoss가 없다"); return; }

                Sprite idleFirst = null;
                foreach (var (action, expectedCount, fps, loop, set) in Books)
                {
                    var paths = Paths(ArtRoot, $"Enemy_BrokenGoddess_{action}_");
                    if (paths.Count == 0) { problems.Add($"{action}: 그림이 없다({ArtRoot}/Enemy_BrokenGoddess_{action}_NN.png)"); continue; }
                    if (paths.Count != expectedCount)
                        problems.Add($"{action}: 장수가 예상({expectedCount})과 다르다({paths.Count}) — 계속 진행은 한다, 확인할 것");

                    var frames = paths.Select(p => ApplyImport(p, problems)).Where(s => s != null).ToArray();
                    if (frames.Length == 0) continue;

                    var book = UpsertFlipbook($"Flipbook_BrokenGoddess_{action}", frames, fps, loop);
                    set(boss, book);
                    if (action == "Idle") idleFirst = frames[0];
                }

                // 프리팹의 기본(틀) 그림 — BlobLord와 같은 방식(스폰 직후 EnemySetup.FitToWorldSize가 이 스프라이트
                // 크기를 기준으로 localScale을 계산하므로, 반드시 이 시점에 이미 실제 그림이어야 한다).
                var sr = root.GetComponent<SpriteRenderer>();
                if (sr != null && idleFirst != null)
                {
                    sr.sprite = idleFirst;
                    sr.color = Color.white; // 그림에 이미 색이 칠해져 있다 — 도형 임시색을 남겨두면 또 곱해진다
                }
                else if (idleFirst == null) problems.Add("대기 그림을 못 찾아 프리팹 기본 그림을 못 바꿨다");

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }

            AssetDatabase.SaveAssets();
            foreach (var p in problems) Debug.LogWarning("[BuildBrokenGoddessArt] " + p);
            Debug.Log($"[BuildBrokenGoddessArt] 부서진 여신상 그림 연결 끝" + (problems.Count > 0 ? $" — 확인할 것 {problems.Count}건(위 경고)" : ""));
        }

        public static void RunBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static System.Collections.Generic.List<string> Paths(string dir, string prefix)
        {
            if (!Directory.Exists(dir)) return new System.Collections.Generic.List<string>();
            return Directory.GetFiles(dir, prefix + "*.png")
                             .Select(p => p.Replace(Path.DirectorySeparatorChar, '/'))
                             .OrderBy(p => p, System.StringComparer.Ordinal)
                             .ToList();
        }

        /// <summary>
        /// 그림 가져오기 설정 — Sprite(Single)·PPU <see cref="DotScale.GridPixelsPerUnit"/>·Point 필터·무압축.
        /// 보스는 몹 지역 그림(<see cref="ChronoSavior.Editor.BuildEnemySkins"/>)과 달리 발 기준점 보정이 없다
        /// (<see cref="Systems.EnemySetup.SetupBoss"/>가 그림 크기만 보고 균등 스케일을 잡는다) — 그래서 기준점은
        /// Unity 기본값(가운데) 그대로 두고, 포즈마다 캐릭터가 캔버스 안에서 같은 세로 위치에 그려졌는지는
        /// 그림을 그린 쪽이 맞춰야 한다(위 클래스 주석 "확인 필요" ①).
        /// </summary>
        static Sprite ApplyImport(string path, System.Collections.Generic.List<string> problems)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { AssetDatabase.ImportAsset(path); ti = AssetImporter.GetAtPath(path) as TextureImporter; }
            if (ti == null) { problems.Add($"그림을 못 읽었다: {path}"); return null; }

            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = DotScale.GridPixelsPerUnit;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null) problems.Add($"Sprite를 못 만들었다: {path}");
            return sprite;
        }

        /// <summary>제자리 갱신(GUID 유지) — BuildEnemySkins.UpsertFlipbook과 같은 이유(다시 돌려도 연결이 안 끊김).</summary>
        static SpriteFlipbook UpsertFlipbook(string assetName, Sprite[] frames, float fps, bool loop)
        {
            if (!AssetDatabase.IsValidFolder(OutRoot))
            {
                if (!AssetDatabase.IsValidFolder("Assets/Data/Enemies")) AssetDatabase.CreateFolder("Assets/Data", "Enemies");
                AssetDatabase.CreateFolder("Assets/Data/Enemies", "Skins");
            }
            string path = $"{OutRoot}/{assetName}.asset";
            var book = AssetDatabase.LoadAssetAtPath<SpriteFlipbook>(path);
            bool isNew = book == null;
            if (isNew) book = ScriptableObject.CreateInstance<SpriteFlipbook>();
            book.frames = frames;
            book.fps = fps;
            book.loop = loop;
            if (isNew) AssetDatabase.CreateAsset(book, path);
            else EditorUtility.SetDirty(book);
            return book;
        }
    }
}
