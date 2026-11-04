using UnityEditor;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Editor
{
    /// <summary>
    /// 캐릭터 그림 에셋(<see cref="CharacterArtSet"/>)을 에디터 도구가 찾고 저장하는 곳 — 그림 적용 메뉴 두 개가 같이 쓴다(2026-11-05 2a).
    /// 예전 도구는 **열린 씬의 PlayerRig**를 고친 뒤 씬 저장을 요구했다. 이제 그림은 씬이 아니라 이 에셋에 있어서, 씬을 열 필요 없이 에셋만 고치고 저장한다.
    /// </summary>
    static class CharacterArtAsset
    {
        public const string Path = "Assets/Data/Characters/CharacterArtSet.asset";

        public static CharacterArtSet Load()
        {
            var art = AssetDatabase.LoadAssetAtPath<CharacterArtSet>(Path);
            if (art == null) Debug.LogWarning($"[CharacterArtAsset] {Path}가 없다 — 캐릭터 그림 에셋을 만들고 20_Combat의 Player(PlayerRig.Art)에 연결할 것.");
            return art;
        }

        /// <summary>그림·색을 통째로 바꾸고 에셋을 저장한다(길이는 캐릭터 수에 맞춘다).</summary>
        public static void Write(CharacterArtSet art, Sprite[] sprites, Color[] tints, string who)
        {
            Undo.RecordObject(art, who);
            art.sprites = sprites;
            art.tints = tints;
            EditorUtility.SetDirty(art);
            AssetDatabase.SaveAssetIfDirty(art);
        }

        /// <summary>지금 값을 복사해 캐릭터 수 길이로 — 일부 칸만 바꾸는 도구용.</summary>
        public static (Sprite[] sprites, Color[] tints) Copy(CharacterArtSet art)
        {
            var sprites = new Sprite[CharacterArtSet.Count];
            var tints = new Color[CharacterArtSet.Count];
            for (int i = 0; i < CharacterArtSet.Count; i++)
            {
                sprites[i] = art.SpriteOf((CharacterId)i);
                tints[i] = art.tints != null && i < art.tints.Length ? art.tints[i] : Color.white;
            }
            return (sprites, tints);
        }
    }
}
