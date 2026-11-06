using System.IO;
using UnityEditor;
using UnityEngine;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 동그라미·네모·세모 스프라이트를 만든다 — **정식 아트가 들어오기 전의 임시 도형**이다.
    ///
    /// ## 왜 프로토타입 그림을 안 쓰나
    /// 사용자 지시(2026-09-16): **"캐릭터나 몬스터에 일단 스킨 씌우지 말고 네모 세모 동그라미로만."**
    /// 구워둔 프로토타입 그림(`Assets/Sprites/Prototype/`, 절차는 `docs/prototype-sprites.md`)은
    /// **지우지 않았다** — 파일은 그대로 있고 코드가 참조만 안 한다. 다시 쓰려면
    /// 몹은 `BuildEnemyData`의 `sprite` 대입을, 캐릭터는 그림 묶음(`Assets/Data/Characters/CharacterArtSet`)을 되돌리면 된다.
    ///
    /// ## 도형에 뜻을 담는다
    /// 세 도형을 아무렇게나 배정하면 색만 다른 도형 더미가 된다. **플레이어가 다르게 대응해야 하는
    /// 것끼리 도형을 갈랐다** — 자세한 배정은 <see cref="ChronoSavior.Core.PrimitiveShape"/> 주석에 있다.
    ///
    /// ## 픽셀 크기와 PPU
    /// 128px에 PPU 128로 굽는다 → **한 장이 정확히 1 월드 유닛**이다. 크기 계산(`ApplySize`·
    /// `FitToWorldSize`)이 스프라이트 실측 높이를 쓰므로 PPU가 어긋나면 덩치가 통째로 틀어진다.
    /// </summary>
    public static class BuildPrimitiveSprites
    {
        public const string Folder = "Assets/Sprites/Primitive";
        const int Size = 128;

        /// <summary>한 변이 이 유닛 수만큼 된다(PPU를 픽셀 수와 같게 두므로 1.0).</summary>
        public const float WorldUnitsPerSprite = 1f;

        [MenuItem("ChronoSavior/원시 도형 스프라이트 생성")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);
            Write("Circle", Circle);
            Write("Square", Square);
            Write("Triangle", Triangle);
            AssetDatabase.Refresh();
            Debug.Log($"[BuildPrimitiveSprites] 동그라미·네모·세모 생성 완료 → {Folder}");
        }

        public static Sprite Load(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{name}.png");

        // ── 도형별 알파 함수 (0 = 투명, 1 = 불투명) ──
        // 경계에서 0↔1로 딱 끊지 않고 1픽셀 걸쳐 섞는다 — 안 그러면 확대했을 때 계단이 보인다.

        static float Circle(float x, float y)
        {
            const float r = Size / 2f;
            float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r));
            return Mathf.Clamp01(r - d);
        }

        /// <summary>네모는 테두리만 1px 부드럽게 — 가장자리를 꽉 채운다.</summary>
        static float Square(float x, float y)
        {
            float edge = Mathf.Min(Mathf.Min(x, Size - x), Mathf.Min(y, Size - y));
            return Mathf.Clamp01(edge);
        }

        /// <summary>
        /// 위를 향한 정삼각형. 밑변은 아래 끝에 딱 붙이고 꼭대기는 위 가운데다 —
        /// 좌우 대칭이라 스프라이트를 X로 뒤집어도(적이 방향을 바꿀 때) 모양이 그대로다.
        /// </summary>
        static float Triangle(float x, float y)
        {
            // y=0(아래)에서 폭이 최대, y=Size(위)에서 0이 되는 이등변삼각형.
            float halfWidth = (Size / 2f) * (1f - y / Size);
            float dx = Mathf.Abs(x - Size / 2f);
            return Mathf.Clamp01(halfWidth - dx);
        }

        static void Write(string name, System.Func<float, float, float> alpha)
        {
            string path = $"{Folder}/{name}.png";

            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            var px = new Color[Size * Size];
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    px[y * Size + x] = new Color(1f, 1f, 1f, alpha(x + 0.5f, y + 0.5f));
            tex.SetPixels(px);
            tex.Apply();

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = Size; // 한 장 = 1 유닛 (클래스 주석 참고)
            importer.filterMode = FilterMode.Bilinear;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
    }
}
