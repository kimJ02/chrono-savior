using UnityEditor;
using UnityEngine;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 2026-11-04 사용자가 올린 임시 캐릭터 그림(<c>Assets/Resources/Art/Characters/chara_temp_01.png</c>)으로 캐릭터 그림 에셋의
    /// 마법사·메카닉·섬영 세 칸(드루이드는 키트가 없어 그대로)을 채운다. 메뉴 `ChronoSavior/Apply Temp Character Art (chara_temp_01)` —
    /// 씬을 열 필요 없고 에셋을 바로 저장한다(2026-11-05, <see cref="CharacterArtAsset"/>).
    ///
    /// 이 그림은 이미 색이 칠해진 그림이라 덧칠 색은 흰색(덧칠 없음)으로 둔다. 세 캐릭터가 전부 같은 그림을 쓰게 되므로
    /// 화면에서 서로 구분이 안 된다 — 의도된 동작이다(사용자 확인, 2026-11-04).
    /// </summary>
    public static class ApplyTempCharacterArt
    {
        const string SpriteResourcePath = "Art/Characters/chara_temp_01";

        [MenuItem("ChronoSavior/Apply Temp Character Art (chara_temp_01)")]
        public static void Apply()
        {
            var art = CharacterArtAsset.Load();
            if (art == null) return;

            var sprite = Resources.Load<Sprite>(SpriteResourcePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[ApplyTempCharacterArt] {SpriteResourcePath} 스프라이트를 못 찾았다 — " +
                    "Assets/Resources/Art/Characters/chara_temp_01.png가 있는지, import가 Sprite로 됐는지 확인할 것.");
                return;
            }

            var (sprites, tints) = CharacterArtAsset.Copy(art);
            // CharacterId 순서: 0=마법사, 1=메카닉, 2=섬영, 3=드루이드(키트 없음 — 안 건드린다).
            for (int i = 0; i < 3; i++)
            {
                sprites[i] = sprite;
                tints[i] = Color.white; // 이미 색칠된 그림이라 덧칠 없음(흰색).
            }

            CharacterArtAsset.Write(art, sprites, tints, "Apply Temp Character Art");
            Debug.Log("[ApplyTempCharacterArt] 마법사·메카닉·섬영 세 칸을 chara_temp_01로 채우고 에셋을 저장했다.");
        }
    }
}
