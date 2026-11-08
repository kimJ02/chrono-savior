using UnityEditor;
using UnityEngine;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// Figma에서 받은 도트 그림(`Assets/Resources/Art/Figma/`)의 가져오기 설정 — 2026-10-24 C안(1유닛 = 128px).
    ///
    /// Point 필터·무압축·밉맵 없음 — 도트가 도트로 보이는 최소 조건이다.
    /// 픽셀/유닛은 파일 이름으로 정한다 — `ground_*`는 64(Figma가 32px 원본을 2배로 깔았다), 나머지는 128.
    /// 타일로 늘려 그리는 그림이 있어서 메시는 `FullRect`여야 한다(아니면 `SpriteRenderer` 타일 모드가 경고만 내고 늘린다).
    /// 모양(`textureShape`)은 반드시 2D로 박는다 — 메타가 비어 있는 새 그림은 큐브맵으로 들어가서 `Resources.Load`가 못 찾았다
    /// (2026-10-24). 설정을 바꾸면 <see cref="GetVersion"/>을 올려야 이미 들어온 그림도 다시 가져온다.
    /// </summary>
    public class FigmaArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Resources/Art/Figma/";
        /// <summary>Figma UI 그림(640×360 화면 부품) — 2026-10-24.</summary>
        public const string UiFolder = Folder + "UI/";

        /// <summary>도트가 아닌 큰 그림(타이틀 배경·로고 1672×941, 스킬 아이콘 314px) — 화면 크기에 맞춰 부드럽게 줄여 그린다.</summary>
        public static bool IsIllustration(string path) =>
            path.StartsWith(UiFolder + "Title/") || path.StartsWith(UiFolder + "Skills/");

        public override uint GetVersion() => 6;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.filterMode = FilterMode.Point;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.mipmapEnabled = false;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            bool ui = assetPath.StartsWith(UiFolder);
            // UI는 캔버스 1단위 = 그림 1도트(캔버스 referencePixelsPerUnit 100) — 9-슬라이스 테두리도 도트 그대로 잡힌다.
            // 월드 타일(땅·발판)은 PPU 64 — 그대로 깔면 도트 배율 2(Core.DotScale, 2026-11-09 발판도 땅과 같게). 배경·포탈은 128에 배율 2를 곱해 깐다.
            string file = System.IO.Path.GetFileName(assetPath);
            ti.spritePixelsPerUnit = ui ? 100f : file.StartsWith("ground_") || file.StartsWith("platform_") ? 64f : 128f;
            // UI 버튼의 마우스 올림·눌림·비활성 그림은 원본 그림의 밝기만 바꿔 실행 중에 만든다(`UI/Pixel/PxArt`) — 픽셀을 읽어야 한다.
            ti.isReadable = ui;
            if (IsIllustration(assetPath))
            {
                // 1672px 그림을 640판 329px 로고·1920px 배경으로 줄이고 늘린다 — 도트 필터면 들쭉날쭉해진다
                ti.filterMode = FilterMode.Bilinear;
                ti.mipmapEnabled = true;
                ti.isReadable = false;
            }

            var settings = new TextureImporterSettings();
            ti.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.textureShape = TextureImporterShape.Texture2D;
            ti.SetTextureSettings(settings);
            ti.textureShape = TextureImporterShape.Texture2D;
        }
    }
}
