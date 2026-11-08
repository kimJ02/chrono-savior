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

        struct Row { public string what, art, source; public float dotX, dotY; public string note; public bool placeholderShape; }

        [MenuItem("ChronoSavior/도트 배율 보고서")]
        public static void Run()
        {
            string report = Build(out int warnings, out int shapes);
            Debug.Log(report);
            Debug.Log($"[DotScaleReport] 정수가 아닌 도트 배율 {warnings}곳(그중 임시 도형 {shapes}곳) — 경고만, 지금은 임시 그림");
        }

        public static void RunBatch()
        {
            string report = Build(out int warnings, out int shapes);
            string path = ArgValue("-reportOut");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, report, new UTF8Encoding(false));
            Debug.Log(report);
            Debug.Log($"[DotScaleReport] 정수가 아닌 도트 배율 {warnings}곳(그중 임시 도형 {shapes}곳) — 경고만, 지금은 임시 그림");
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

        static string Build(out int warnings, out int shapes)
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

            // 몹 — EnemySetup.ApplySize: 데이터 그림(또는 임시 도형)을 visualHeight에 맞춰 늘린다(엘리트 ×EliteScale)
            var catalog = AssetDatabase.FindAssets("t:MonsterCatalog").Select(g => AssetDatabase.LoadAssetAtPath<MonsterCatalog>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
            if (catalog != null && catalog.types != null)
            {
                foreach (var data in catalog.types.Where(d => d != null))
                {
                    var s = data.sprite;
                    string note = s == null ? "임시 도형(그림 없음) — 128px 도형" : null;
                    float px = s != null ? s.rect.height : 128f;
                    float h = data.visualHeight > 0f ? data.visualHeight : data.colliderRadius * 2f;
                    float d = Dot(h, px);
                    rows.Add(new Row { what = "몹 " + data.displayName, art = s != null ? s.name : "(도형)", source = data.name + ".visualHeight", dotX = d, dotY = d, note = note, placeholderShape = s == null });
                    float de = Dot(h * DifficultyScalingConfig.EliteScale, px);
                    rows.Add(new Row { what = "몹 " + data.displayName + "(엘리트)", art = s != null ? s.name : "(도형)", source = "×EliteScale", dotX = de, dotY = de, note = note, placeholderShape = s == null });
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
            // 살수 베기 — 키로 불러온 라이선스 그림(git 밖, 없으면 이 줄이 빠진다)
            var slashFrames = VfxFrames.Get(VfxKeys.AssassinSlash1);
            foreach (var f in slashFrames.Where(f => f != null).GroupBy(f => (f.rect.width, f.rect.height, f.pixelsPerUnit)).Select(g => g.First()))
                rows.Add(new Row { what = "살수 베기 효과", art = VfxKeys.AssassinSlash1, source = "VfxKeys.AssassinSlash1~3 × BladeSlashVfx.Scale",
                                   dotX = GridPpu * BladeSlashVfx.Scale / f.pixelsPerUnit, dotY = GridPpu * BladeSlashVfx.Scale / f.pixelsPerUnit });
            // 마법사 — 키로 불러온 라이선스 그림(2026-11-07). 배율은 정수(VfxPlayer.WholeScale 등)라 도트 배율 = 배율. 가로=최소, 세로=최대.
            void Mage(string what, string key, float lo, float hi, string src)
            {
                var f = VfxFrames.Get(key).FirstOrDefault();
                if (f == null) return; // 팩을 안 가져온 클론
                float dlo = GridPpu * lo / f.pixelsPerUnit, dhi = GridPpu * hi / f.pixelsPerUnit;
                rows.Add(new Row { what = what, art = key, source = src, dotX = dlo, dotY = dhi, note = Mathf.Approximately(dlo, dhi) ? null : $"{dlo:0.##}~{dhi:0.##}(가로=최소, 세로=최대)" });
            }
            int Whole(string key, float width) => VfxPlayer.WholeScale(width, VfxFrames.Get(key).FirstOrDefault());
            Mage("마법사 차지 오라", VfxKeys.MageChargeLarge, 1f, 1f, "MageChargeVfx(크기 고정, 월드 배율 1)");
            Mage("마법사 화염탄", VfxKeys.MageFireProjectile, Whole(VfxKeys.MageFireProjectile, 0.4f), Whole(VfxKeys.MageFireProjectile, 0.4f * 2.25f), "판정 폭(0.4×sizeMul)에 가장 가까운 정수");
            Mage("마법사 중력탄", VfxKeys.MageGravityOrbSmall, Whole(VfxKeys.MageGravityOrbSmall, 0.4f), Whole(VfxKeys.MageGravityOrbSmall, 0.4f * 1.9f), "판정 폭(0.4×sizeMul)에 가장 가까운 정수");
            Mage("마법사 불길", VfxKeys.MageFireTrail, FireTrailZone.FlameScale, FireTrailZone.FlameScale, "FireTrailZone.FlameScale");
            Mage("마법사 화염 폭발", VfxKeys.MageFireExplosion, Whole(VfxKeys.MageFireExplosion, MageSpecConfig.FireExplosionRadius(1) * 2f), Whole(VfxKeys.MageFireExplosion, MageSpecConfig.FireExplosionRadius(5) * 2f), "판정 지름에 가장 가까운 정수");
            Mage("마법사 중력장 구체", VfxKeys.MageGravityOrbLarge, GravityVortexVfx.ScaleFor(1), GravityVortexVfx.ScaleFor(5), "중첩 수(GravityVortexVfx.ScaleFor)");
            Mage("마법사 중력 착탄", VfxKeys.MageGravityExplosion, Whole(VfxKeys.MageGravityExplosion, MageSpecConfig.GravityImpactRadius(1, 0f) * 2f), Whole(VfxKeys.MageGravityExplosion, MageSpecConfig.GravityImpactRadius(5, 1f) * 2f), "판정 지름에 가장 가까운 정수");
            Mage("마법사 대붕괴 폭발", VfxKeys.MageCollapseExplosion, Whole(VfxKeys.MageCollapseExplosion, MageSpecConfig.CollapseRadius(1, 0) * 2f), Whole(VfxKeys.MageCollapseExplosion, MageSpecConfig.CollapseRadius(5, 5) * 2f), "판정 지름에 가장 가까운 정수");
            // 메카닉·살수(2026-11-08)
            Mage("메카닉 레이저(1~2층 small·3~4층 large·5층 large)", VfxKeys.MechanicLaser("center", "loop", true), GunnerLaserBeam.ArtThickness(1), GunnerLaserBeam.ArtThickness(5), "굵기 정수 배율 — 길이는 이어 붙임(타일)");
            Mage("메카닉 드론 탄(펠렛)", VfxKeys.MechanicPellet, Whole(VfxKeys.MechanicPellet, 0.26f * GunnerSpecConfig.DroneBulletSize(1)), Whole(VfxKeys.MechanicPellet, 0.26f * GunnerSpecConfig.DroneBulletSize(5)), "판정 폭에 가장 가까운 정수");
            Mage("메카닉 중계기 머리", VfxKeys.MechanicRelayHead, 1f, 1f, "GunnerTurret(배율 1)");
            Mage("메카닉 중계선", VfxKeys.MechanicLaser("center", "loop", false), 1f, 1f, "GunnerFieldVfx(이어 붙임)");
            Mage("살수 칼날폭풍 회전베기", VfxKeys.AssassinSpin, Whole(VfxKeys.AssassinSpin, BladeCombat.StormSpinDiameter), Whole(VfxKeys.AssassinSpin, BladeCombat.StormSpinDiameter), "판정 지름에 가장 가까운 정수");
            Mage("살수 하트비트", VfxKeys.AssassinHeartbeat, 1f, 1f, "BladeVfx");
            Mage("살수 스플래터", VfxKeys.AssassinSplatter, BladeVfx.SplatterScale, BladeVfx.SplatterScale, "BladeVfx.SplatterScale");
            Mage("살수 연기", VfxKeys.AssassinSmoke, BladeVfx.SmokeScale, BladeVfx.SmokeScale, "BladeVfx.SmokeScale");
            Mage("마법사 임계 붕괴 충격", VfxKeys.MageCollapseCritical, Whole(VfxKeys.MageCollapseCritical, MageSpecConfig.CollapseRadius(2, 0) * MageSpecConfig.CollapseCriticalRingMult(2) * 2f), Whole(VfxKeys.MageCollapseCritical, MageSpecConfig.CollapseRadius(5, 5) * MageSpecConfig.CollapseCriticalRingMult(5) * 2f), "2차 고리 지름에 가장 가까운 정수");

            warnings = 0; shapes = 0;
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
                if (!whole) { warnings++; if (r.placeholderShape) shapes++; }
                string dots = Mathf.Approximately(r.dotX, r.dotY) ? $"{r.dotX:0.###}" : $"{r.dotX:0.###} × {r.dotY:0.###}";
                sb.AppendLine($"| {r.what} | {r.art} | {r.source} | {dots} | {(whole ? "" : "⚠")} {r.note} |");
            }
            return sb.ToString();
        }

        static bool IsWhole(float v) => Mathf.Abs(v - Mathf.Round(v)) < 0.01f && v >= 0.99f;
    }
}
