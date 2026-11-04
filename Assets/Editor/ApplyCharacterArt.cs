using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ChronoSavior.Characters;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 지금 열려 있는 씬의 <see cref="PlayerRig"/>에 `Assets/Sprites/Prototype/` 캐릭터 그림 4종을
    /// 한 번에 채운다.
    ///
    /// ## 왜 따로 메뉴를 만들었나
    /// `PlayerRig.characterSprites`는 `BuildPartAScene.BuildPlayer()`가 씬을 지을 때 값을 박아 넣는
    /// 직렬화 필드라, 이 스크립트(C#)만 고쳐서는 **이미 저장된 씬**(20_Combat.unity)엔 반영되지 않는다.
    /// `BuildPartAScene.Build()`를 다시 돌리면 씬 전체(바닥·발판·카메라·스포너·GameFlow까지)를
    /// 통째로 새로 짜서 덮어쓰므로, 캐릭터 그림만 바꾸려고 그걸 쓰는 건 과하다 — 그래서 열려 있는 씬의
    /// Player만 건드리는 작은 메뉴를 따로 둔다.
    ///
    /// 실행: `20_Combat.unity`를 열어둔 상태에서 메뉴 `ChronoSavior/Apply Character Art (Player)`.
    /// 끝나면 **씬을 저장**해야 한다(Ctrl+S) — 안 하면 에디터를 닫을 때 날아간다.
    /// </summary>
    public static class ApplyCharacterArt
    {
        // characterSprites 인덱스 순서 = Core.CharacterId 순서(마법사·메카닉·섬영·드루이드).
        static readonly string[] SpriteNames = { "char_mage", "char_gunner", "char_blade", "char_druid" };

        [MenuItem("ChronoSavior/Apply Character Art (Player)")]
        public static void Apply()
        {
            var rig = Object.FindAnyObjectByType<PlayerRig>(FindObjectsInactive.Include);
            if (rig == null)
            {
                Debug.LogWarning("[ApplyCharacterArt] 열린 씬에서 PlayerRig를 못 찾았다. 20_Combat.unity를 열고 다시 실행할 것.");
                return;
            }

            var sprites = new Sprite[SpriteNames.Length];
            var colors = new Color[SpriteNames.Length];
            bool missing = false;
            for (int i = 0; i < SpriteNames.Length; i++)
            {
                sprites[i] = BuildPrototypeSprites.Load(SpriteNames[i]);
                // 정식 아트는 이미 색이 칠해져 있으니 흰색으로 둔다(덧칠하면 원래 색이 가려진다) —
                // PlayerRig.ApplySprite 주석 참고.
                colors[i] = Color.white;
                if (sprites[i] == null)
                {
                    missing = true;
                    Debug.LogWarning($"[ApplyCharacterArt] {SpriteNames[i]} 스프라이트를 못 찾았다 — " +
                        "먼저 메뉴 ChronoSavior/Import Prototype Sprites(또는 Build Enemy Data)를 실행했는지 확인할 것.");
                }
            }
            if (missing) return; // 일부가 비면 네 칸을 섞어 반쪽만 바뀐 상태로 두지 않는다.

            rig.characterSprites = sprites;
            rig.characterColors = colors;
            EditorUtility.SetDirty(rig);
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            Debug.Log("[ApplyCharacterArt] characterSprites 4칸(마법사·메카닉·섬영·드루이드)을 Prototype 그림으로 채웠다. " +
                "씬을 저장할 것(Ctrl+S).");
        }
    }
}
