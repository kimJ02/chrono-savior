using UnityEditor;
using UnityEngine;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 우리가 직접 그린 월드 도트 그림(`Assets/Sprites/Pixel/` — 메카닉 드론·중계기 몸체 등, 2026-11-09)의 가져오기 설정:
    /// 도트가 도트로 보이게(Point·무압축·밉맵 없음), PPU 128 — 그리는 쪽이 공통 도트 배율(`Core.DotScale`, 2)로 키운다.
    /// </summary>
    public class PixelArtImporter : AssetPostprocessor
    {
        public const string Folder = "Assets/Sprites/Pixel/";

        void OnPreprocessTexture()
        {
            if (!assetPath.Replace(System.IO.Path.DirectorySeparatorChar, '/').StartsWith(Folder)) return;
            var t = (TextureImporter)assetImporter;
            t.textureType = TextureImporterType.Sprite;
            t.spriteImportMode = SpriteImportMode.Single;
            t.spritePixelsPerUnit = 128f;
            t.filterMode = FilterMode.Point;
            t.textureCompression = TextureImporterCompression.Uncompressed;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.wrapMode = TextureWrapMode.Clamp;
        }
    }
}
