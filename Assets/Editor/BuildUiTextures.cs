using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// Figma에서 받은 UI 이미지의 임포트 설정을 잡는다(`Assets/Sprites/UI/`).
    ///
    /// ## 왜 따로 필요한가 — 픽셀아트는 기본 설정으로 들어오면 망가진다
    /// Unity 2D 기본 임포트는 **Bilinear 필터 + 압축**이다. 32×32 도트 아이콘을 64×64로 늘리면
    /// 보간이 끼어들어 흐릿해지고, 압축은 몇 픽셀짜리 도트에 블록 노이즈를 남긴다.
    /// 그래서 **Point 필터 + 무압축**으로 강제한다 — 도트가 도트로 보이는 최소 조건이다.
    ///
    /// `npotScale = None`도 필수다. 창 프레임이 512×288인데(288은 2의 거듭제곱이 아니다) 기본
    /// 설정은 이걸 가까운 거듭제곱으로 늘려버려서, 디자인 좌표와 1픽셀도 안 맞게 된다.
    ///
    /// ## Sprite가 아니라 Texture로 넣는 이유
    /// UI는 IMGUI(`GUI.DrawTexture`)로 그리므로 `Texture2D`가 필요하다. Sprite로 임포트하면
    /// 쓰지도 않는 Sprite 서브에셋이 같이 생긴다.
    /// </summary>
    public static class BuildUiTextures
    {
        public const string Folder = "Assets/Sprites/UI";

        /// <summary>Figma 노드 → 파일명. 주석의 크기는 원본 픽셀 크기다.</summary>
        public static readonly string[] Names =
        {
            "icon_flag",       //  32×32  깃발      — 최고 스테이지 (Figma 163:16)
            "icon_star",       //  32×32  별        — 최고 레벨      (165:22)
            "icon_skull",      //  32×32  해골      — 총 처치 수     (164:21)
            "icon_shard",      //  32×32  시간의 파편 — 획득 파편     (163:20)
            "window_frame",    // 512×288 회귀창 프레임(제목 구분선 포함) (162:2)
            "menu_plate",      // 256×64  타이틀 메뉴 버튼 플레이트 (63:3 · 61:54~56)
            "skill_slot_attack", // 32×32 전투 HUD Z 칸(틀+칼)      (보스 HUD 181:2)
            "skill_slot_skill",  // 32×32 전투 HUD X 칸(틀+마법진)  (181:3)
            "skill_slot_jump",   // 32×32 전투 HUD C 칸(틀+화살표)  (181:4)
        };

        [MenuItem("ChronoSavior/UI 텍스처 임포트 설정")]
        public static void Apply()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogWarning($"[BuildUiTextures] {Folder} 가 없다 — Figma 에셋을 먼저 내려받을 것.");
                return;
            }

            int n = 0;
            foreach (var name in Names)
            {
                string path = $"{Folder}/{name}.png";
                if (!File.Exists(path))
                {
                    Debug.LogWarning($"[BuildUiTextures] 없음: {path}");
                    continue;
                }

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                importer.textureType = TextureImporterType.Default;
                importer.filterMode = FilterMode.Point;          // 도트가 흐려지지 않게 (클래스 주석 참고)
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None; // 512×288을 그대로 유지
                importer.maxTextureSize = 1024;
                importer.SaveAndReimport();
                n++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[BuildUiTextures] {n}개 텍스처 설정 완료 → {Folder}");
        }

        public static Texture2D Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/{name}.png");
    }
}
