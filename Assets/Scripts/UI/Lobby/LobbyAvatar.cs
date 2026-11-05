using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.UI
{
    /// <summary>
    /// 로비에서 걷는 캐릭터 — **그림만** 있다(2026-11-05 씬 분할 2차 3-1). 키트·체력·물리·Player 태그가 없다: 로비에선 싸우지 않고, 걷기는 `LobbyWorld`가
    /// 위치를 옮긴다(시간이 멈춘 로비라 물리 이동이 아니다). 예전엔 전투 씬의 진짜 Player를 로비에서 걷게 했다 — 로비가 따로 서면 Player가 없다.
    ///
    /// 그림은 세이브의 지금 캐릭터(`PlayerProfile.character`)를 따라간다 — 로비 캐릭터 선택은 그 값만 바꾸므로, 바뀐 다음 프레임에 갈아입는다.
    /// 그림·색은 전투의 플레이어와 같은 에셋(`Core/CharacterArtSet`), 높이도 같은 계산(`EntitySizeConfig.ScaleToHeight`).
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class LobbyAvatar : MonoBehaviour
    {
        [Tooltip("캐릭터 그림 에셋(Assets/Data/Characters/CharacterArtSet.asset) — 전투의 Player와 같은 것.")]
        [SerializeField] CharacterArtSet art;

        /// <summary>씬 검증(`Editor/SceneSetupValidator`)과 테스트가 읽고 넣는다.</summary>
        public CharacterArtSet Art { get => art; set => art = value; }

        SpriteRenderer sr;
        CharacterId? shown;
        CharacterArtSet shownArt;

        /// <summary>지금 입고 있는 캐릭터 — 아직 안 입었으면 null.</summary>
        public CharacterId? Shown => shown;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        void OnEnable() => Refresh();

        void Update() => Refresh();

        /// <summary>프로필 캐릭터(또는 에셋)가 바뀌었을 때만 갈아입는다.</summary>
        void Refresh()
        {
            var id = ProfileService.Current.character;
            if (shown == id && shownArt == art) return;
            shown = id;
            shownArt = art;
            var sprite = art != null ? art.SpriteOf(id) : null;
            if (sprite == null) return; // 그림이 없으면 지금 그림 그대로(전투의 Player와 같은 규칙)
            sr.sprite = sprite;
            sr.color = art.TintOf(id);
            float scale = EntitySizeConfig.ScaleToHeight(sprite, EntitySizeConfig.PlayerHeight);
            if (scale > 0f) transform.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
