using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Combat;
using ChronoSavior.World;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 도트 배율 보고서(2026-11-06 에셋 교체 경계 3.5-f) — 그림마다 '그림 한 픽셀이 월드 격자 몇 칸인지'를 계산해 표로 낸다.
    /// 도트 배율 = `128 × 화면에서 차지하는 크기(유닛) ÷ 그림 픽셀 수`(`docs/pixel-standard.md` 1절). 정수가 아니면 도트 폭이 고르지 않다.
    /// **지금은 보고만 한다**(임시 그림이라 막지 않는다) — 외주 그림이 오고 규격이 정해지면 '정수가 아니면 실패'로 강화한다(`HANDOFF.md` 순서 5).
    /// 값은 데이터·프리팹·씬 설정에서 읽는다(게임을 돌리지 않는다 — 실행 중에 크기를 정하는 곳은 그 계산을 그대로 따라 한다).
    /// 실행: 메뉴 `ChronoSavior/도트 배율 보고서`, 배치 `-executeMethod ChronoSavior.Editor.DotScaleReport.RunBatch [-reportOut 경로]`.
    /// </summary>
    public static class DotScaleReport
    {
        const float GridPpu = StageArtConfig.PixelsPerUnit; // 월드 격자: 1유닛 = 128칸

        struct Row { public string what, art, source; public float dotX, dotY; public string note; }

        [MenuItem("ChronoSavior/도트 배율 보고서")]
        public static void Run()
        {
            string report = Build(out int warnings);
            Debug.Log(report);
            Debug.Log($"[DotScaleReport] 정수가 아닌 도트 배율 {warnings}곳(경고만 — 지금은 임시 그림)");
        }

        public static void RunBatch()
        {
            string report = Build(out int warnings);
            string path = ArgValue("-reportOut");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, report, new UTF8Encoding(false));
            Debug.Log(report);
            Debug.Log($"[DotScaleReport] 정수가 아닌 도트 배율 {warnings}곳(경고만 — 지금은 임시 그림)");
            EditorApplication.Exit(0);
        }

        static string ArgValue(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        /// <summary>그림 한 장이 화면에서 차지하는 크기(유닛)로 도트 배율 — 픽셀 수 기준.</summary>
        static float Dot(float worldSize, float pixels) => pixels > 0f ? GridPpu * worldSize / pixels : 0f;

        static string Build(out int warnings)
        {
            var rows = new List<Row>();

            // 스테이지 그림 묶음
            foreach (var guid in AssetDatabase.FindAssets("t:StageArtSet"))
            {
                var set = AssetDatabase.LoadAssetAtPath<StageArtSet>(AssetDatabase.GUIDToAssetPath(guid));
                void Tile(string what, Sprite s) { if (s != null) rows.Add(new Row { what = what, art = s.name, source = set.name, dotX = GridPpu / s.pixelsPerUnit, dotY = GridPpu / s.pixelsPerUnit }); }
                Tile("땅 윗면", set.groundTop); Tile("땅 흙", set.groundDirt); Tile("발판", set.platformTile);
                if (set.background != null) rows.Add(new Row { what = "배경", art = set.background.name, source = set.name + ".backgroundScale", dotX = GridPpu * set.backgroundScale / set.background.pixelsPerUnit, dotY = GridPpu * set.backgroundScale / set.background.pixelsPerUnit });
                if (set.portal != null) rows.Add(new Row { what = "보스 포탈", art = set.portal.name, source = set.name + ".portalScale", dotX = GridPpu * set.portalScale / set.portal.pixelsPerUnit, dotY = GridPpu * set.portalScale / set.portal.pixelsPerUnit });
            }

            // 캐릭터 — PlayerRig·LobbyAvatar: 그림 높이(heights)에 맞춰 늘린다
            foreach (var guid in AssetDatabase.FindAssets("t:CharacterArtSet"))
            {
                var art = AssetDatabase.LoadAssetAtPath<CharacterArtSet>(AssetDatabase.GUIDToAssetPath(guid));
                foreach (CharacterId id in System.Enum.GetValues(typeof(CharacterId)))
                {
                    var s = art.SpriteOf(id);
                    if (s == null) continue;
                    float d = Dot(art.HeightOf(id), s.rect.height);
                    rows.Add(new Row { what = "캐릭터 " + id, art = s.name, source = art.name + ".heights", dotX = d, dotY = d });
                }
            }

            // 몹 — EnemySpawner.ApplySize: 데이터 그림(또는 임시 도형)을 visualHeight에 맞춰 늘린다(엘리트 ×EliteScale)
            var catalog = AssetDatabase.FindAssets("t:MonsterCatalog").Select(g => AssetDatabase.LoadAssetAtPath<MonsterCatalog>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (catalog != null)
            {
                foreach (var data in catalog.types.Where(d => d != null))
                {
                    var s = data.sprite;
                    string note = s == null ? "임시 도형(그림 없음) — 128px 도형" : null;
                    float px = s != null ? s.rect.height : 128f;
                    float h = data.visualHeight > 0f ? data.visualHeight : data.colliderRadius * 2f;
                    float d = Dot(h, px);
                    rows.Add(new Row { what = "몹 " + data.displayName, art = s != null ? s.name : "(도형)", source = data.name + ".visualHeight", dotX = d, dotY = d, note = note });
                    float de = Dot(h * DifficultyScalingConfig.EliteScale, px);
                    rows.Add(new Row { what = "몹 " + data.displayName + "(엘리트)", art = s != null ? s.name : "(도형)", source = "×EliteScale", dotX = de, dotY = de, note = note });
                }
                void Big(string what, Sprite s, Vector2 size, string src)
                {
                    float pxW = s != null ? s.rect.width : 128f, pxH = s != null ? s.rect.height : 128f;
                    rows.Add(new Row { what = what, art = s != null ? s.name : "(프리팹 그림)", source = src, dotX = Dot(size.x, pxW), dotY = Dot(size.y, pxH) });
                }
                Big("보스", catalog.boss, catalog.bossSize, catalog.name + ".bossSize");
                Big("성소", catalog.shrine, catalog.shrineSize, catalog.name + ".shrineSize");
            }

            // 효과 — 코드 상수로 크기를 정하는 것
            var slashFrame = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Art/Figma/Vfx/BladeSlash/slash1_color4_01.png");
            if (slashFrame != null) rows.Add(new Row { what = "섬영 베기 효과", art = slashFrame.name, source = "BladeSlashVfx.Scale", dotX = GridPpu * BladeSlashVfx.Scale / slashFrame.pixelsPerUnit, dotY = GridPpu * BladeSlashVfx.Scale / slashFrame.pixelsPerUnit });
            var impact = AssetDatabase.FindAssets("t:Sprite", new[] { "Assets/Resources/Art/Figma/Vfx/GravityImpact" }).Select(g => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (impact != null)
            {
                float lo = Dot(MageSpecConfig.GravityImpactRadius(0, 0f) * 2f, impact.rect.width);
                float hi = Dot(MageSpecConfig.GravityImpactRadius(5, 1f) * 2f, impact.rect.width);
                rows.Add(new Row { what = "중력 착탄 효과", art = impact.name, source = "스킬 반경×2(MageSpecConfig)", dotX = lo, dotY = hi, note = $"층·차지에 따라 {lo:0.##}~{hi:0.##}(가로=최소, 세로=최대)" });
            }

            warnings = 0;
            var sb = new StringBuilder();
            sb.AppendLine("# 도트 배율 보고서 (" + System.DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            sb.AppendLine();
            sb.AppendLine("도트 배율 = 128 × 화면 크기(유닛) ÷ 그림 픽셀 수. 1이 가장 또렷, 정수가 아니면 도트 폭이 고르지 않다(⚠). 지금은 경고만.");
            sb.AppendLine();
            sb.AppendLine("| 그림 | 파일 | 크기를 정하는 곳 | 도트 배율(가로×세로) | |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var r in rows)
            {
                bool whole = IsWhole(r.dotX) && IsWhole(r.dotY);
                if (!whole && r.note == null) warnings++;
                string dots = Mathf.Approximately(r.dotX, r.dotY) ? $"{r.dotX:0.###}" : $"{r.dotX:0.###} × {r.dotY:0.###}";
                sb.AppendLine($"| {r.what} | {r.art} | {r.source} | {dots} | {(whole ? "" : "⚠")} {r.note} |");
            }
            return sb.ToString();
        }

        static bool IsWhole(float v) => Mathf.Abs(v - Mathf.Round(v)) < 0.01f && v >= 0.99f;
    }
}
