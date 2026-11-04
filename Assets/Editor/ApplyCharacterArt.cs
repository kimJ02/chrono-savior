using UnityEditor;
using UnityEngine;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 캐릭터 그림 에셋(`Assets/Data/Characters/CharacterArtSet.asset`)을 `Assets/Sprites/Prototype/` 캐릭터 그림 4종으로 한 번에 채운다.
    /// 메뉴 `ChronoSavior/Apply Character Art (Player)` — 씬을 열 필요 없고, 에셋을 바로 저장한다(2026-11-05 — 예전엔 열린 씬의
    /// PlayerRig 배열을 고친 뒤 씬을 저장해야 했다. 그림이 씬에서 에셋으로 옮겨 갔다, <see cref="CharacterArtAsset"/>).
    /// 정식 그림은 이미 색이 칠해져 있어 덧칠 색은 흰색(덧칠 없음)으로 둔다.
    /// </summary>
    public static class ApplyCharacterArt
    {
        // Core.CharacterId 순서(마법사·메카닉·섬영·드루이드).
        static readonly string[] SpriteNames = { "char_mage", "char_gunner", "char_blade", "char_druid" };

        [MenuItem("ChronoSavior/Apply Character Art (Player)")]
        public static void Apply()
        {
            var art = CharacterArtAsset.Load();
            if (art == null) return;

            var sprites = new Sprite[SpriteNames.Length];
            var tints = new Color[SpriteNames.Length];
            for (int i = 0; i < SpriteNames.Length; i++)
            {
                sprites[i] = BuildPrototypeSprites.Load(SpriteNames[i]);
                tints[i] = Color.white;
                if (sprites[i] == null)
                {
                    Debug.LogWarning($"[ApplyCharacterArt] {SpriteNames[i]} 스프라이트를 못 찾았다 — " +
                        "먼저 메뉴 ChronoSavior/Import Prototype Sprites(또는 Build Enemy Data)를 실행했는지 확인할 것.");
                    return; // 일부가 비면 네 칸을 섞어 반쪽만 바뀐 상태로 두지 않는다.
                }
            }

            CharacterArtAsset.Write(art, sprites, tints, "Apply Character Art");
            Debug.Log("[ApplyCharacterArt] 캐릭터 그림 4칸(마법사·메카닉·섬영·드루이드)을 Prototype 그림으로 채우고 에셋을 저장했다.");
        }
    }
}
