using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 캐릭터 그림 — 캐릭터마다 그림 하나와 덧칠 색 하나(<see cref="CharacterId"/> 순서: 마법사·메카닉·섬영·드루이드).
    /// 전투의 플레이어(`Characters/PlayerRig`), 에디터 그림 적용 도구, 씬 분할 2차 3단계의 로비 아바타가 **같은 에셋**을 읽는다
    /// (2026-11-05 2a — 예전엔 PlayerRig 컴포넌트(전투 씬)의 배열이라 로비가 전투 씬과 떨어지면 못 읽었다).
    ///
    /// **그림·색만 둔다** — 이름·설명·해금 조건은 넣지 않고(지금은 캐릭터 선택 화면의 표), 고른 캐릭터·레벨 같은 보유 상태는 세이브(`PlayerProfile`)다.
    /// 에셋: `Assets/Data/Characters/CharacterArtSet.asset`.
    /// </summary>
    [CreateAssetMenu(menuName = "ChronoSavior/Character Art Set", fileName = "CharacterArtSet")]
    public class CharacterArtSet : ScriptableObject
    {
        /// <summary>캐릭터 수 = <see cref="CharacterId"/> 개수. 배열 길이가 이와 다르면 씬 검증이 알린다.</summary>
        public static readonly int Count = System.Enum.GetValues(typeof(CharacterId)).Length;

        [Tooltip("CharacterId 순서(마법사·메카닉·섬영·드루이드). 비어 있으면 플레이어는 기본 스프라이트를 그대로 쓴다.")]
        public Sprite[] sprites = new Sprite[Count];

        [Tooltip("캐릭터별 덧칠 색. 임시 도형(흰 네모)은 색이 유일한 구분 수단이고, 이미 색칠된 정식 그림은 흰색(덧칠 없음)으로 둔다. " +
                 "알파 0(비어 있음)도 흰색으로 본다.")]
        public Color[] tints = new Color[Count];

        /// <summary>그 캐릭터 그림 — 없으면 null.</summary>
        public Sprite SpriteOf(CharacterId id)
        {
            int i = (int)id;
            return sprites != null && i >= 0 && i < sprites.Length ? sprites[i] : null;
        }

        /// <summary>그 캐릭터 덧칠 색 — 비어 있으면(알파 0) 흰색.</summary>
        public Color TintOf(CharacterId id)
        {
            int i = (int)id;
            return tints != null && i >= 0 && i < tints.Length && tints[i].a > 0f ? tints[i] : Color.white;
        }
    }
}
