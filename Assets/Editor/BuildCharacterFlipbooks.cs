using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.UI;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// **캐릭터 애니메이션 연결**(2026-11-17 플립북 전환 — `docs/animation-flipbook-proposal.md`, 2026-11-16 `BuildCharacterAnimations`(Animator)를 대신한다).
    /// 메뉴 한 번으로:
    /// ① `Assets/Resources/Art/Characters/{Shared,Mage,Gunner,Blade}/`의 프레임 가져오기 설정(Point·무압축·PPU 128)과 **묶음마다 기준점** —
    ///    가로는 머리 가운데(공격 그림 70px 캔버스는 몸이 오른쪽에 있어 가운데 기준이면 공격할 때 몸이 10px 옆으로 튀었다), 세로는 "발(묶음에서 가장 낮은
    ///    불투명 줄) + 판정 반지름" — 발이 판정 원 바닥에 닿는다(몬스터와 같은 규칙). 좌우 반전(flipX)은 기준점 둘레로 뒤집혀 머리 자리가 그대로다.
    ///    마법사 오브·일렁임(보라 사본 포함)은 몸 공격 그림과 같은 캔버스라 그 묶음의 기준점을 그대로 쓴다.
    /// ② 묶음 <see cref="SpriteFlipbook"/>(`Assets/Data/Characters/Anim/Flipbook_*`)과 캐릭터별 <see cref="CharacterAnimSet"/>(`AnimSet_*`)을 **제자리 갱신**(GUID 유지),
    /// ③ `CharacterArtSet.animSets` 연결, ④ 씬 `20_Combat`의 Player와 `10_Lobby`의 아바타에 <see cref="CharacterFlipbook"/>을 배치하고 Player의
    ///    `MageChargeVfx` 오브·일렁임 칸을 채운다.
    /// **아래 <see cref="Books"/> 표가 원본이다** — 에셋을 손으로 고치지 말고 표를 고쳐 다시 돌린다(2026-11-16 Animator 컨트롤러를 손으로 고쳤다가 생성 메뉴가 지운 사고 재발 방지).
    ///
    /// 배치: `-executeMethod ChronoSavior.Editor.BuildCharacterFlipbooks.RunBatch`.
    /// </summary>
    public static class BuildCharacterFlipbooks
    {
        const string ArtRoot = "Assets/Resources/Art/Characters";
        const string OutRoot = "Assets/Data/Characters/Anim";
        const string ArtSetPath = "Assets/Data/Characters/CharacterArtSet.asset";

        /// <summary>
        /// (에셋 이름, 폴더, 파일 앞부분, 몇 번째부터, 몇 장(0 = 끝까지), fps, 되풀이, 기준점 묶음 — 같은 캔버스·같은 몸이면 같은 이름).
        /// 프레임 순서·fps는 팀원 2026-11-16 클립 그대로(서기 8 · 이동 12 · 점프 10 · 피격 12 · 공격 12 · 마법사 차지 6). 점프/하강 8장은
        /// 1~2 웅크림(땅) · 3~5 오름 · 6~8 내림이라 상승 = 3~5, 하강 = 6~8로 나눴다(제안서 7절 결정 1).
        /// </summary>
        static readonly (string name, string folder, string prefix, int from, int count, float fps, bool loop, string pivotGroup)[] Books =
        {
            ("Idle", "Shared", "Character_Idle_", 1, 0, 8f, true, "Idle"),
            ("Move", "Shared", "Character_Move_", 1, 0, 12f, true, "Move"),
            ("Rise", "Shared", "Character_JumpFall_", 3, 3, 10f, false, "JumpFall"),
            ("Fall", "Shared", "Character_JumpFall_", 6, 3, 10f, false, "JumpFall"),
            ("Hit", "Shared", "Character_Hit_", 1, 0, 12f, false, "Hit"),
            ("Mage_Attack", "Mage", "Character_Mage_Attack_Body_", 1, 0, 12f, false, "MageBody"),
            ("Mage_Charge", "Mage", "Character_Mage_Attack_Body_", 1, 3, 6f, true, "MageBody"),
            ("Mage_Orb", "Mage", "Character_Mage_Attack_OrbEffect_", 1, 0, 10f, false, "MageBody"),
            ("Mage_Ki", "Mage", "Character_Mage_Attack_KiEffect_", 1, 0, 10f, true, "MageBody"),
            ("Mage_OrbGravity", "Mage", "Character_Mage_Attack_OrbEffectGravity_", 1, 0, 10f, false, "MageBody"),
            ("Mage_KiGravity", "Mage", "Character_Mage_Attack_KiEffectGravity_", 1, 0, 10f, true, "MageBody"),
            ("Gunner_Attack", "Gunner", "Character_Gunner_Attack_", 1, 0, 12f, false, "GunnerBody"),
            ("Blade_Attack", "Blade", "Character_Blade_Attack_", 1, 0, 12f, false, "BladeBody"),
        };

        /// <summary>기준점 묶음마다 기준점을 재는 그림(머리·발이 보이는 몸 그림).</summary>
        static readonly Dictionary<string, (string folder, string prefix)> PivotSource = new Dictionary<string, (string, string)>
        {
            ["Idle"] = ("Shared", "Character_Idle_"), ["Move"] = ("Shared", "Character_Move_"), ["JumpFall"] = ("Shared", "Character_JumpFall_"),
            ["Hit"] = ("Shared", "Character_Hit_"), ["MageBody"] = ("Mage", "Character_Mage_Attack_Body_"),
            ["GunnerBody"] = ("Gunner", "Character_Gunner_Attack_"), ["BladeBody"] = ("Blade", "Character_Blade_Attack_"),
            ["Static"] = ("", "chara_temp_"), // 애니메이션 없는 캐릭터용 정지 그림(CharacterArtSet.sprites) — 같은 규칙으로 발을 맞춘다
        };

        [MenuItem("ChronoSavior/캐릭터 애니메이션 연결")]
        public static void Build()
        {
            var problems = new List<string>();
            var art = AssetDatabase.LoadAssetAtPath<CharacterArtSet>(ArtSetPath);
            if (art == null) { Debug.LogError("[BuildCharacterFlipbooks] CharacterArtSet이 없다: " + ArtSetPath); return; }
            if (!AssetDatabase.IsValidFolder(OutRoot)) AssetDatabase.CreateFolder("Assets/Data/Characters", "Anim");

            // ① 기준점(묶음마다) — 판정 반지름을 그림 픽셀로: 반지름 ÷ (그림 높이 ÷ 픽셀 높이)
            var pivots = new Dictionary<string, Vector2>();
            foreach (var kv in PivotSource)
            {
                var paths = Paths(kv.Value.folder, kv.Value.prefix);
                if (paths.Count == 0) { problems.Add($"기준점 그림이 없다: {kv.Value.folder}/{kv.Value.prefix}*"); continue; }
                var (w, h, headX) = HeadCenter(paths[0]);
                int feet = paths.Min(BuildEnemySkins.BottomGap);
                float radiusPx = EntitySizeConfig.PlayerRadius / (art.HeightOf(CharacterId.Mage) / h);
                pivots[kv.Key] = new Vector2(headX / w, (feet + radiusPx) / h);
            }

            // ② 묶음
            var books = new Dictionary<string, SpriteFlipbook>();
            foreach (var b in Books)
            {
                var all = Paths(b.folder, b.prefix);
                var picked = all.Skip(b.from - 1).Take(b.count > 0 ? b.count : int.MaxValue).ToList();
                if (picked.Count == 0) { problems.Add($"{b.name}: 프레임이 없다({b.folder}/{b.prefix}*)"); continue; }
                if (!pivots.TryGetValue(b.pivotGroup, out var pivot)) continue;
                var sprites = picked.Select(p => ApplyImport(p, pivot, problems)).Where(s => s != null).ToList();
                if (sprites.Count != picked.Count) continue;
                books[b.name] = Upsert<SpriteFlipbook>($"{OutRoot}/Flipbook_{b.name}.asset", fb => { fb.frames = sprites.ToArray(); fb.fps = b.fps; fb.loop = b.loop; });
            }
            if (pivots.TryGetValue("Static", out var staticPivot))
                foreach (var p in Paths("", "chara_temp_")) ApplyImport(p, staticPivot, problems);
            SpriteFlipbook B(string n) => books.TryGetValue(n, out var f) ? f : null;

            // ③ 캐릭터별 묶음 + CharacterArtSet
            CharacterAnimSet SetOf(string who, string attack, string charge) => Upsert<CharacterAnimSet>($"{OutRoot}/AnimSet_{who}.asset", s =>
            {
                s.idle = B("Idle"); s.move = B("Move"); s.rise = B("Rise"); s.fall = B("Fall"); s.hit = B("Hit");
                s.charge = charge != null ? B(charge) : null;
                s.attacks = new[] { B(attack) };
            });
            var sets = new CharacterAnimSet[CharacterArtSet.Count];
            sets[(int)CharacterId.Mage] = SetOf("Mage", "Mage_Attack", "Mage_Charge");
            sets[(int)CharacterId.Gunner] = SetOf("Gunner", "Gunner_Attack", null);
            sets[(int)CharacterId.Blade] = SetOf("Blade", "Blade_Attack", null);
            art.animSets = sets;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssets();

            // ④ 씬 배치
            PlaceInScenes(B, problems);

            foreach (var p in problems) Debug.LogError("[BuildCharacterFlipbooks] " + p);
            Debug.Log($"[BuildCharacterFlipbooks] 묶음 {books.Count}개, 캐릭터 3명" + (problems.Count > 0 ? $", 문제 {problems.Count}개" : ""));
        }

        public static void RunBatch()
        {
            Build();
            EditorApplication.Exit(0);
        }

        static List<string> Paths(string folder, string prefix) =>
            Directory.GetFiles(folder.Length > 0 ? $"{ArtRoot}/{folder}" : ArtRoot, prefix + "*.png").Select(p => p.Replace(Path.DirectorySeparatorChar, '/'))
                // "OrbEffect_"가 "OrbEffectGravity_"를 안 잡게 — 앞부분 뒤가 바로 숫자인 것만
                .Where(p => char.IsDigit(Path.GetFileNameWithoutExtension(p)[prefix.Length]))
                .OrderBy(p => p, System.StringComparer.Ordinal).ToList();

        /// <summary>그림 크기와 머리 가운데 x(픽셀, 왼쪽부터) — 맨 위 불투명 줄부터 8줄의 가로 범위 가운데.</summary>
        static (int w, int h, float headX) HeadCenter(string path)
        {
            var tex = new Texture2D(2, 2);
            try
            {
                tex.LoadImage(File.ReadAllBytes(path));
                int w = tex.width, h = tex.height, top = -1;
                for (int y = h - 1; y >= 0 && top < 0; y--)
                    for (int x = 0; x < w; x++) if (tex.GetPixel(x, y).a > 0.01f) { top = y; break; }
                int minX = w, maxX = -1;
                for (int y = top; y > top - 8 && y >= 0; y--)
                    for (int x = 0; x < w; x++)
                        if (tex.GetPixel(x, y).a > 0.01f) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); }
                return (w, h, maxX >= minX ? (minX + maxX + 1) / 2f : w / 2f);
            }
            finally { Object.DestroyImmediate(tex); }
        }

        static Sprite ApplyImport(string path, Vector2 pivot, List<string> problems)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) { problems.Add("그림을 못 읽었다: " + path); return null; }
            var st = new TextureImporterSettings();
            ti.ReadTextureSettings(st);
            bool changed = ti.textureType != TextureImporterType.Sprite || st.spriteMode != (int)SpriteImportMode.Single
                || st.spriteAlignment != (int)SpriteAlignment.Custom || (st.spritePivot - pivot).sqrMagnitude > 1e-8f
                || !Mathf.Approximately(ti.spritePixelsPerUnit, DotScale.GridPixelsPerUnit) || ti.filterMode != FilterMode.Point
                || ti.textureCompression != TextureImporterCompression.Uncompressed || ti.mipmapEnabled;
            if (changed)
            {
                ti.textureType = TextureImporterType.Sprite;
                st.spriteMode = (int)SpriteImportMode.Single;
                st.spriteAlignment = (int)SpriteAlignment.Custom;
                st.spritePivot = pivot;
                ti.SetTextureSettings(st);
                ti.spritePixelsPerUnit = DotScale.GridPixelsPerUnit;
                ti.filterMode = FilterMode.Point;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.mipmapEnabled = false;
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (s == null) problems.Add("그림을 못 읽었다: " + path);
            return s;
        }

        static T Upsert<T>(string path, System.Action<T> fill) where T : ScriptableObject
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            bool isNew = a == null;
            if (isNew) a = ScriptableObject.CreateInstance<T>();
            fill(a);
            if (isNew) AssetDatabase.CreateAsset(a, path);
            else EditorUtility.SetDirty(a);
            return a;
        }

        static void PlaceInScenes(System.Func<string, SpriteFlipbook> book, List<string> problems)
        {
            var combat = EditorSceneManager.OpenScene(SceneSetupValidator.CombatPath, OpenSceneMode.Single);
            var rig = Object.FindFirstObjectByType<PlayerRig>(FindObjectsInactive.Include);
            if (rig == null) problems.Add("20_Combat에 Player(PlayerRig)가 없다");
            else
            {
                var fb = rig.GetComponent<CharacterFlipbook>();
                if (fb == null) fb = rig.gameObject.AddComponent<CharacterFlipbook>(); // Unity 객체엔 ?? 금지(가짜 null)
                fb.useUnscaledTime = false;
                var charge = rig.GetComponent<MageChargeVfx>();
                if (charge != null)
                {
                    charge.OrbArt = book("Mage_Orb"); charge.KiArt = book("Mage_Ki");
                    charge.OrbArtGravity = book("Mage_OrbGravity"); charge.KiArtGravity = book("Mage_KiGravity");
                    EditorUtility.SetDirty(charge);
                }
                EditorUtility.SetDirty(fb);
                EditorSceneManager.SaveScene(combat);
            }

            var lobby = EditorSceneManager.OpenScene(SceneSetupValidator.LobbyPath, OpenSceneMode.Single);
            var avatar = Object.FindFirstObjectByType<LobbyAvatar>(FindObjectsInactive.Include);
            if (avatar == null) problems.Add("10_Lobby에 LobbyAvatar가 없다");
            else
            {
                var fb = avatar.GetComponent<CharacterFlipbook>();
                if (fb == null) fb = avatar.gameObject.AddComponent<CharacterFlipbook>(); // Unity 객체엔 ?? 금지(가짜 null)
                fb.useUnscaledTime = true; // 시간이 멈춘 로비에서도 걷는다
                EditorUtility.SetDirty(fb);
                EditorSceneManager.SaveScene(lobby);
            }
        }
    }
}
