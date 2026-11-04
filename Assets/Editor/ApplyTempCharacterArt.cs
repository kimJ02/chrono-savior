using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using YokaiFront.Characters;

namespace YokaiFront.Editor
{
    /// <summary>
    /// 2026-11-04 사용자가 올린 임시 캐릭터 그림(<c>Assets/Resources/Art/Characters/chara_temp_01.png</c>)을
    /// 열린 씬의 <see cref="PlayerRig"/>에 마법사·건너·섬영 세 칸(드루이드는 키트가 아직 없어 제외) 전부
    /// 같은 그림으로 채운다. <see cref="ApplyCharacterArt"/>와 같은 패턴(씬을 직접 찾아 필드를 채우고
    /// dirty 표시) — characterSprites는 BuildPartAScene이 씬에 박아 넣는 직렬화 필드라 코드만 고쳐서는
    /// 이미 저장된 씬에 반영되지 않기 때문에 에디터 메뉴로 둔다.
    ///
    /// 실행: `CombatCore.unity`를 열어둔 상태에서 메뉴 `YokaiFront/Apply Temp Character Art (chara_temp_01)`.
    /// 끝나면 **씬을 저장**해야 한다(Ctrl+S) — 안 하면 에디터를 닫을 때 날아간다.
    ///
    /// 이 그림은 이미 색이 칠해진 그림이라 `characterColors`는 흰색(=덧칠 없음)으로 둔다
    /// (<see cref="PlayerRig.ApplySprite"/> 주석 참고). 세 캐릭터가 전부 같은 그림을 쓰게 되므로
    /// 화면에서 서로 구분이 안 된다 — 의도된 동작이다(사용자 확인, 2026-11-04).
    /// </summary>
    public static class ApplyTempCharacterArt
    {
        const string SpriteResourcePath = "Art/Characters/chara_temp_01";

        [MenuItem("YokaiFront/Apply Temp Character Art (chara_temp_01)")]
        public static void Apply()
        {
            var rig = Object.FindAnyObjectByType<PlayerRig>(FindObjectsInactive.Include);
            if (rig == null)
            {
                Debug.LogWarning("[ApplyTempCharacterArt] 열린 씬에서 PlayerRig를 못 찾았다. CombatCore.unity를 열고 다시 실행할 것.");
                return;
            }

            var sprite = Resources.Load<Sprite>(SpriteResourcePath);
            if (sprite == null)
            {
                Debug.LogWarning($"[ApplyTempCharacterArt] {SpriteResourcePath} 스프라이트를 못 찾았다 — " +
                    "Assets/Resources/Art/Characters/chara_temp_01.png가 있는지, import가 Sprite로 됐는지 확인할 것.");
                return;
            }

            var sprites = rig.characterSprites != null && rig.characterSprites.Length == 4
                ? (Sprite[])rig.characterSprites.Clone() : new Sprite[4];
            var colors = rig.characterColors != null && rig.characterColors.Length == 4
                ? (Color[])rig.characterColors.Clone() : new Color[4];

            // CharacterId 순서: 0=마법사, 1=건너, 2=섬영, 3=드루이드(키트 없음 — 안 건드린다).
            for (int i = 0; i < 3; i++)
            {
                sprites[i] = sprite;
                colors[i] = Color.white; // 이미 색칠된 그림이라 덧칠 없음(흰색).
            }

            rig.characterSprites = sprites;
            rig.characterColors = colors;
            EditorUtility.SetDirty(rig);
            EditorSceneManager.MarkSceneDirty(rig.gameObject.scene);
            Debug.Log("[ApplyTempCharacterArt] 마법사·건너·섬영 세 칸을 chara_temp_01로 채웠다. 씬을 저장할 것(Ctrl+S).");
        }
    }
}
