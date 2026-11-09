using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using ChronoSavior.Combat;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// **라이선스 이펙트 팩 → `Assets/Resources/Vfx/<키>/frame_000…`**(git 밖, `HANDOFF.md` 0-3). 원본 zip 6개가 있는 폴더를 고르면 아래 목록의 프레임만 풀고
    /// 가져오기 설정(Point·무압축·밉맵 없음·PPU 128)을 맞춘다. 팀원은 itch에서 팩을 각자 받아 메뉴 한 번이면 같은 폴더가 된다(약관·출처 `docs/vfx-credits.md`).
    /// **이 목록이 '무엇을 쓰는지'의 원본이다** — 키는 실행 코드의 `Combat/VfxKeys` 상수라 어긋나면 컴파일이 안 된다.
    /// 원본이 없거나 비면 조용히 넘어가지 않고 오류로 알리고, 그 키는 바꾸지 않는다.
    ///
    /// 배치: `-executeMethod ChronoSavior.Editor.VfxPackImporter.RunBatch -vfxPacks &lt;zip 폴더&gt;`.
    /// </summary>
    public static class VfxPackImporter
    {
        public const string OutRoot = "Assets/Resources/Vfx";
        const string PrefKey = "ChronoSavior.VfxPackFolder";

        // 팩 zip 이름의 앞부분(받은 파일 이름 뒤에 " (1)"·버전이 붙어도 되게)
        const string Gigapack = "Super Pixel Effects Gigapack";
        const string Projectiles = "Super Pixel Projectiles Pack 4";
        const string Slash = "vfx_slash_vol_2";
        const string SlashUpdate = "update_1"; // Slash Vol 2 업데이트(회전베기 등) — 시트에 불투명 갈색 바탕 칸이 있어 그 색을 빼고 쓴다
        const string Fire = "Pixel fire asset pack";

        /// <summary>한 키를 어디서 가져오나 — 프레임 폴더(파일마다 한 장) 또는 스프라이트 시트의 한 줄.</summary>
        sealed class Source
        {
            public string Key, Pack, Path;
            /// <summary>시트일 때만(Cell &gt; 0): 칸 크기, **위에서 0부터 센** 줄 번호, 그 줄의 프레임 수(뒤쪽 빈 칸 제외 — 다르면 오류).</summary>
            public int Cell, Row, Frames;
            public bool IsSheet => Cell > 0;
            /// <summary>재색(0이면 그대로): 픽셀마다 색상(hue)을 이만큼(도) 돌린 파생 그림을 만든다 — 밝기·채도·투명도는 그대로라
            /// 런타임 틴트(곱하기)로는 탁해지는 색 바꿈(노랑 → 보라)에 쓴다(`HANDOFF.md` 0-3 재색 규칙). 팩 원본은 안 건드린다.</summary>
            public float HueShift;
            /// <summary>시트일 때: 이 색(불투명 바탕)을 투명으로 뺀다(null이면 그대로).</summary>
            public Color32? Matte;
        }

        static Source Frames(string key, string pack, string folder) => new Source { Key = key, Pack = pack, Path = folder };
        static Source Sheet(string key, string pack, string sheet, int cell, int row, int frames) =>
            new Source { Key = key, Pack = pack, Path = sheet, Cell = cell, Row = row, Frames = frames };
        static Source Recolored(Source src, float hueShiftDegrees) { src.HueShift = hueShiftDegrees; return src; }
        static Source Matted(Source src, Color32 background) { src.Matte = background; return src; }

        /// <summary>쓰는 효과 목록(2026-11-07 초안 — 효과를 입히며 고른다, `HANDOFF.md` 2절).</summary>
        static IEnumerable<Source> Manifest()
        {
            // 마법사
            yield return Frames(VfxKeys.MageChargeLarge, Gigapack, "PNG/Sci-fi/scifi_charge_up_001/scifi_charge_up_001_large_yellow");
            yield return Frames(VfxKeys.MageChargeSmall, Gigapack, "PNG/Sci-fi/scifi_charge_up_001/scifi_charge_up_001_small_yellow");
            // 중력 갈래 차지는 보라 — 팩엔 노랑뿐이고 보라 틴트를 곱하면 노란 픽셀이 탁한 주황이 돼서(캡처·Codex 리뷰) 색상을 돌린 파생 그림
            yield return Recolored(Frames(VfxKeys.MageChargeLargeViolet, Gigapack, "PNG/Sci-fi/scifi_charge_up_001/scifi_charge_up_001_large_yellow"), 225f);
            yield return Recolored(Frames(VfxKeys.MageChargeSmallViolet, Gigapack, "PNG/Sci-fi/scifi_charge_up_001/scifi_charge_up_001_small_yellow"), 225f);
            // 화염탄 — 문서 예시(혜성 모양 energy_blast)의 빨강. 오른쪽을 향한 그림이라 쏠 때 속도 방향으로 돌린다(2026-11-08, 예전 plasma_ball에서 교체)
            yield return Frames(VfxKeys.MageFireProjectile, Projectiles, "PNG/pj4_energy_blast_A_small_red");
            yield return Frames(VfxKeys.MageFireTrail, Fire, "Pixel Fire Asset Pack Colored/fire asset red/Group 4 - 1");
            yield return Frames(VfxKeys.MageFireTrailSmall, Fire, "Pixel Fire Asset Pack Colored/fire asset red/Group 4 - 3");
            yield return Frames(VfxKeys.MageBurn, Fire, "Pixel Fire Asset Pack Colored/fire asset red/Group 4 - 2"); // 화상 걸린 적에 붙는 불꽃
            // 화염돌진 바닥 불꽃 — 모양을 섞어 줄지어 선 기둥처럼 안 보이게(2026-11-09 사용자 "인위적")
            yield return Frames(VfxKeys.MageFireTrailWide, Fire, "Pixel Fire Asset Pack Colored/fire asset red/Group 4 - 4");
            yield return Frames(VfxKeys.MageFireTrailLow, Fire, "Pixel Fire Asset Pack Colored/fire asset red/Group 4 - 5");
            yield return Frames(VfxKeys.MageFireTrailBig, Fire, "Pixel Fire Asset Pack Colored/fire asset red/Group 6 - 2");
            yield return Frames(VfxKeys.MageFireSmoke, Gigapack, "PNG/Smoke Bursts/looping_smoke_burst_002/looping_smoke_burst_002_small_gray");
            yield return Frames(VfxKeys.MageDashPuff, Gigapack, "PNG/Explosions/symmetrical_explosion_002/symmetrical_explosion_002_small_orange");
            yield return Frames(VfxKeys.MageDashLand, Gigapack, "PNG/Explosions/symmetrical_explosion_001/symmetrical_explosion_001_small_orange");
            // 중력 공격 적중 — 보라 충격(팩에 보라가 있다)
            yield return Frames(VfxKeys.MageGravitySpark, Gigapack, "PNG/Impacts/directional_impact_003/directional_impact_003_small_violet");
            yield return Frames(VfxKeys.MageGravitySparkLarge, Gigapack, "PNG/Impacts/directional_impact_003/directional_impact_003_large_violet");
            yield return Frames(VfxKeys.MageGravityOrbSmall, Projectiles, "PNG/pj4_dark_matter_small_violet");
            yield return Frames(VfxKeys.MageGravityOrbLarge, Projectiles, "PNG/pj4_dark_matter_large_violet");
            // 폭발 — 기가팩 폭발 17종을 게임 화면 배율로 비교해 골랐다(2026-11-08, 사용자 "다양하게 적용해 보고 어울리는 것", Codex 합의):
            // 화염 = 둥근 불꽃+연기(공중·지면 어디서 터져도 어울림), 중력 착탄 = 보라 구체, 대붕괴 = 가시 충격 고리(보라 파생), 임계 붕괴엔 큰 번개 충격을 겹친다
            yield return Frames(VfxKeys.MageFireExplosion, Gigapack, "PNG/Explosions/stylized_explosion_001/stylized_explosion_001_large_yellow");
            // 중력 착탄 — 2026-11-09 사용자 "블랙홀은 기존에 쓰던 그림(10/25부터 쓰던 stylized_explosion_002 보라 구체)은 빼자" → 알갱이 고리 폭발을 보라로(Codex와 고름)
            yield return Recolored(Frames(VfxKeys.MageGravityExplosion, Gigapack, "PNG/Explosions/symmetrical_explosion_004/symmetrical_explosion_004_large_orange"), 250f);
            yield return Recolored(Frames(VfxKeys.MageCollapseExplosion, Gigapack, "PNG/Impacts/symmetrical_impact_002/symmetrical_impact_002_large_blue"), 75f);
            yield return Recolored(Frames(VfxKeys.MageCollapseCritical, Gigapack, "PNG/Impacts/symmetrical_impact_006/symmetrical_impact_006_large_yellow"), 225f);
            // 메카닉
            foreach (var part in VfxKeys.LaserParts)
                foreach (var phase in VfxKeys.LaserPhases)
                {
                    // B형 small — 가운데 줄을 셰이더가 잘라 층별 두께로(2026-11-10 사용자 "레이저가 두껍다", A형 26px → B형 16px 기준)
                    yield return Frames(VfxKeys.MechanicLaser(part, phase), Projectiles, $"PNG/pj4_laser_beam_B_small_blue/{part}_{phase}");
                }
            yield return Frames(VfxKeys.MechanicPellet, Projectiles, "PNG/pj4_energy_pellet_small_yellow");
            // 레이저 차징 — 맞을 적이 없을 때 총구에(2026-11-09 사용자). 노랑 차지를 레이저 파랑으로
            // 레이저 적중 — 작고 짧은 파란 충격(2026-11-09 사용자 "맞았을 때 효과가 이게 뭐야" — 예전엔 빔 끝 물보라를 적마다 틱마다 띄워 겹쳤다)
            yield return Frames(VfxKeys.MechanicHitSpark, Gigapack, "PNG/Impacts/directional_impact_001/directional_impact_001_small_blue");
            yield return Recolored(Frames(VfxKeys.MechanicCharge, Gigapack, "PNG/Sci-fi/scifi_charge_up_001/scifi_charge_up_001_small_yellow"), 150f);
            // 드론 — 팀원 문서 "별도 에셋이 없으면 이걸로"의 그림(회색 껍데기·초록 바이저·노란 줄무늬)이 이 팩의 scrap_metal 회색이다(2026-11-09 사용자)
            yield return Frames(VfxKeys.MechanicDrone, Projectiles, "PNG/pj4_scrap_metal_small_gray");
            yield return Frames(VfxKeys.MechanicRelayHead, Projectiles, "PNG/pj4_plasma_ball_small_blue");
            // 살수 — Slash vol2 시트는 96칸 7열, 효과가 한 줄 건너 하나씩 있다(위에서 0부터 세어 1·3·5줄이 무속성, 프레임 5·6·7장)
            yield return Sheet(VfxKeys.AssassinSlash1, Slash, "spritesheet_96x96/sprite_sheet_96x96.png", 96, 1, 5);
            yield return Sheet(VfxKeys.AssassinSlash2, Slash, "spritesheet_96x96/sprite_sheet_96x96.png", 96, 3, 6);
            yield return Sheet(VfxKeys.AssassinSlash3, Slash, "spritesheet_96x96/sprite_sheet_96x96.png", 96, 5, 7);
            // 칼날폭풍 회전베기 — 업데이트 시트 17줄(위에서 0부터, 하양·하늘 타원 회전베기 7장)
            yield return Matted(Sheet(VfxKeys.AssassinSpin, SlashUpdate, "spritesheet_96x96/sprite_sheet_96x96.png", 96, 17, 7), new Color32(157, 137, 108, 255));
            yield return Frames(VfxKeys.AssassinHeartbeat, Gigapack, "PNG/Sci-fi/scifi_heartbeat_fast_001/scifi_heartbeat_fast_001_large_yellow");
            yield return Frames(VfxKeys.AssassinSplatter, Gigapack, "PNG/Splatters/directional_splatter_001/directional_splatter_001_small_red");
            yield return Frames(VfxKeys.AssassinSmoke, Gigapack, "PNG/Smoke Bursts/looping_smoke_burst_002/looping_smoke_burst_002_small_gray");
        }

        /// <summary>목록이 모든 키를 한 번씩 덮는지(검사·테스트용).</summary>
        public static IEnumerable<string> ManifestKeys => Manifest().Select(s => s.Key);

        [MenuItem("ChronoSavior/VFX 팩 가져오기")]
        static void RunMenu()
        {
            string folder = EditorUtility.OpenFolderPanel("이펙트 팩 zip이 있는 폴더", EditorPrefs.GetString(PrefKey, ""), "");
            if (string.IsNullOrEmpty(folder)) return;
            EditorPrefs.SetString(PrefKey, folder);
            var problems = Import(folder);
            EditorUtility.DisplayDialog("VFX 팩 가져오기", problems.Count == 0 ? "다 가져왔다." : "문제:\n" + string.Join("\n", problems.Take(20)), "확인");
        }

        public static void RunBatch()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-vfxPacks");
            if (i < 0 || i + 1 >= args.Length) { Debug.LogError("[VfxPackImporter] -vfxPacks <폴더>가 필요하다"); EditorApplication.Exit(1); return; }
            var problems = Import(args[i + 1]);
            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }

        /// <summary>
        /// 가져온다. 돌려주는 건 문제 목록(비면 성공). **목록 전부를 먼저 읽고 검증한 뒤에만 쓴다** — 하나라도 문제가 있으면 아무 키도 바꾸지 않는다
        /// (옛 그림과 새 그림이 섞이지 않게, 그때까지 있던 `Resources/Vfx`는 그대로 남는다). 쓰는 도중 디스크 오류가 나면 일부 키만 바뀔 수 있다 —
        /// 그땐 다시 가져오면 된다(원본 zip이 원본이라 잃는 건 없다).
        /// </summary>
        public static List<string> Import(string packFolder)
        {
            var problems = new List<string>();
            var zips = Directory.Exists(packFolder) ? Directory.GetFiles(packFolder, "*.zip") : new string[0];
            var opened = new Dictionary<string, ZipArchive>();
            var ready = new List<(string key, List<byte[]> frames)>();
            try
            {
                foreach (var src in Manifest())
                {
                    if (!opened.TryGetValue(src.Pack, out var zip))
                    {
                        zip = OpenPack(zips, src.Pack, problems);
                        opened[src.Pack] = zip;
                    }
                    if (zip == null) { problems.Add($"{src.Key}: 팩을 못 열었다 — '{src.Pack}…'.zip"); continue; }
                    var frames = src.IsSheet ? SliceSheet(zip, src, problems) : ReadFrames(zip, src, problems);
                    if (frames != null && src.HueShift != 0f) frames = frames.Select(f => ShiftHue(f, src.HueShift)).ToList();
                    if (frames != null && frames.Count > 0) ready.Add((src.Key, frames));
                }
            }
            finally
            {
                foreach (var z in opened.Values) z?.Dispose();
            }

            if (problems.Count == 0)
            {
                foreach (var (key, frames) in ready) Write(key, frames);
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                VfxFrames.ClearCache();
            }
            foreach (var p in problems) Debug.LogError("[VfxPackImporter] " + p);
            Debug.Log(problems.Count == 0 ? $"[VfxPackImporter] {ready.Count}개 키를 가져왔다" : $"[VfxPackImporter] 문제 {problems.Count}건 — 아무것도 바꾸지 않았다");
            return problems;
        }

        /// <summary>이름이 그 팩으로 시작하는 zip이 **정확히 하나**여야 연다(둘이면 어느 쪽인지 모른다). 깨진 zip도 문제로 알린다.</summary>
        static ZipArchive OpenPack(string[] zips, string pack, List<string> problems)
        {
            var matches = zips.Where(z => Path.GetFileName(z).StartsWith(pack, System.StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length != 1)
            {
                problems.Add(matches.Length == 0 ? $"'{pack}…'.zip이 없다" : $"'{pack}…'.zip이 {matches.Length}개다 — 하나만 두세요: {string.Join(", ", matches.Select(Path.GetFileName))}");
                return null;
            }
            try { return ZipFile.OpenRead(matches[0]); }
            catch (System.Exception e) { problems.Add($"{Path.GetFileName(matches[0])}: zip을 못 읽었다 — {e.Message}"); return null; }
        }

        /// <summary>zip 안 경로를 비교용으로 — 구분자 `/`, 앞에 `/`를 붙이고 소문자로(팩 최상위 폴더가 있든 없든 같은 방식으로 찾게).</summary>
        static string Norm(string path) => "/" + path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

        static readonly Regex FrameName = new Regex(@"(?:frame|_)(\d+)\.png$", RegexOptions.IgnoreCase);

        /// <summary>프레임 폴더의 PNG를 번호순으로. 팩 최상위 폴더 이름은 건너뛰고 그 아래 경로로 찾는다.</summary>
        static List<byte[]> ReadFrames(ZipArchive zip, Source src, List<string> problems)
        {
            string dir = Norm(src.Path).TrimEnd('/') + "/";
            var hits = new List<(int n, ZipArchiveEntry e)>();
            foreach (var e in zip.Entries)
            {
                string full = Norm(e.FullName);
                int at = full.IndexOf(dir, System.StringComparison.Ordinal);
                if (at < 0) continue;
                string rest = full.Substring(at + dir.Length);
                if (rest.Contains("/")) continue;
                var m = FrameName.Match(rest);
                if (m.Success) hits.Add((int.Parse(m.Groups[1].Value), e));
            }
            if (hits.Count == 0) { problems.Add($"{src.Key}: 프레임이 없다 — {src.Pack}/{src.Path}"); return null; }
            hits.Sort((x, y) => x.n.CompareTo(y.n));
            // 번호가 이어져야 한다(0000부터든 _1부터든) — 중간 프레임이 빠진 zip을 짧은 애니메이션으로 받아들이지 않게
            for (int k = 1; k < hits.Count; k++)
                if (hits[k].n != hits[k - 1].n + 1) { problems.Add($"{src.Key}: 프레임 번호가 끊겼다({hits[k - 1].n} 다음 {hits[k].n}) — {src.Pack}/{src.Path}"); return null; }
            return hits.Select(h => ReadAll(h.e)).ToList();
        }

        /// <summary>시트의 한 줄을 칸 크기로 잘라 앞에서부터(뒤쪽 빈 칸은 버린다).</summary>
        static List<byte[]> SliceSheet(ZipArchive zip, Source src, List<string> problems)
        {
            var entry = zip.Entries.FirstOrDefault(e => Norm(e.FullName).EndsWith(Norm(src.Path), System.StringComparison.Ordinal));
            if (entry == null) { problems.Add($"{src.Key}: 시트가 없다 — {src.Pack}/{src.Path}"); return null; }
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!sheet.LoadImage(ReadAll(entry))) { problems.Add($"{src.Key}: 시트를 못 읽었다"); return null; }
                int y = sheet.height - (src.Row + 1) * src.Cell; // 텍스처는 아래가 0
                if (y < 0) { problems.Add($"{src.Key}: 시트 {src.Row}줄이 없다"); return null; }
                var cells = new List<byte[]>();
                var px = sheet.GetPixels32();
                for (int c = 0; (c + 1) * src.Cell <= sheet.width; c++)
                {
                    var cell = new Texture2D(src.Cell, src.Cell, TextureFormat.RGBA32, false);
                    var cellPx = new Color32[src.Cell * src.Cell];
                    for (int yy = 0; yy < src.Cell; yy++)
                        for (int xx = 0; xx < src.Cell; xx++)
                        {
                            var p = px[(y + yy) * sheet.width + c * src.Cell + xx];
                            if (src.Matte.HasValue && p.r == src.Matte.Value.r && p.g == src.Matte.Value.g && p.b == src.Matte.Value.b) p = new Color32(0, 0, 0, 0);
                            cellPx[yy * src.Cell + xx] = p;
                        }
                    cell.SetPixels32(cellPx);
                    cell.Apply();
                    bool empty = cell.GetPixels32().All(p => p.a == 0);
                    cells.Add(empty ? null : cell.EncodeToPNG());
                    Object.DestroyImmediate(cell);
                }
                while (cells.Count > 0 && cells[cells.Count - 1] == null) cells.RemoveAt(cells.Count - 1);
                if (cells.Any(c => c == null)) { problems.Add($"{src.Key}: 중간에 빈 칸이 있다(시트 줄을 확인)"); return null; }
                if (cells.Count != src.Frames) { problems.Add($"{src.Key}: 시트 {src.Row}줄(위에서 0부터)의 프레임이 {cells.Count}장 — {src.Frames}장이어야 한다"); return null; }
                return cells;
            }
            finally { Object.DestroyImmediate(sheet); }
        }

        /// <summary>PNG 한 장의 색상(hue)을 돌린다 — 밝기(V)·채도(S)·투명도는 그대로(흰색·회색은 그대로 남는다).</summary>
        public static byte[] ShiftHue(byte[] png, float degrees)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                tex.LoadImage(png);
                var px = tex.GetPixels();
                for (int i = 0; i < px.Length; i++)
                {
                    Color.RGBToHSV(px[i], out float h, out float s, out float v);
                    var c = Color.HSVToRGB(Mathf.Repeat(h + degrees / 360f, 1f), s, v);
                    c.a = px[i].a;
                    px[i] = c;
                }
                tex.SetPixels(px);
                tex.Apply();
                return tex.EncodeToPNG();
            }
            finally { Object.DestroyImmediate(tex); }
        }

        static byte[] ReadAll(ZipArchiveEntry e)
        {
            using var s = e.Open();
            using var m = new MemoryStream();
            s.CopyTo(m);
            return m.ToArray();
        }

        /// <summary>그 키 폴더를 비우고 frame_000…으로 쓴다(그 키의 옛 그림이 섞이지 않게).</summary>
        static void Write(string key, List<byte[]> frames)
        {
            string dir = Path.Combine(OutRoot, key).Replace('\\', '/');
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            if (File.Exists(dir + ".meta")) File.Delete(dir + ".meta");
            Directory.CreateDirectory(dir);
            for (int i = 0; i < frames.Count; i++) File.WriteAllBytes($"{dir}/frame_{i:000}.png", frames[i]);
        }
    }

    /// <summary>`Assets/Resources/Vfx/` 그림의 가져오기 설정 — 도트가 도트로 보이게(Point·무압축·밉맵 없음), 월드와 같은 PPU 128(배율 1 = 그림 한 픽셀이 격자 한 칸).</summary>
    sealed class VfxImportSettings : AssetPostprocessor
    {
        public const float PixelsPerUnit = 128f;
        public override uint GetVersion() => 2; // 설정이 바뀌면 올린다 — 이미 들어온 그림도 다시 가져온다

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(VfxPackImporter.OutRoot + "/")) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = PixelsPerUnit;
            t.filterMode = FilterMode.Point;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            // 레이저 몸통은 선(LineRenderer)이 길이 방향으로 반복해 그린다 — 텍스처가 반복돼야 한다(2026-11-09)
            t.wrapMode = assetPath.Contains("/laser_center_") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            // 레이저 몸통·중계선은 길이만큼 이어 붙인다(SpriteRenderer 타일) — 타일은 사각형 메시(FullRect)여야 한다
            var settings = new TextureImporterSettings();
            t.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            t.SetTextureSettings(settings);
        }
    }
}
