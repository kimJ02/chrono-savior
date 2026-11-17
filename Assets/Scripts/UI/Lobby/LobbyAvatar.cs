using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Characters;

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

        /// <summary>
        /// 로비 애니메이션(2026-11-17 플립북 전환 — 2026-11-16 Animator 대신). 전투의 Player와 같은 자세 그림 묶음(`CharacterArtSet.AnimSetOf`),
        /// 씬의 아바타에 배치된 `CharacterFlipbook`(실제 시간 — 시간이 멈춘 로비에서도 걷는다). 로비엔 전투가 없어 서기·걷기·상승·하강만.
        /// 로비는 물리가 아니라 `LobbyWorld`가 위치를 직접 옮기므로 걷기·상승/하강은 프레임 사이 위치 변화량으로, 공중 여부는
        /// `LobbyWorld`가 꾸밈용 점프 동안 <see cref="SetGrounded"/>로 알려 준다(2026-11-18 팀원).
        /// </summary>
        CharacterFlipbook flipbook;
        Vector3 lastPos;
        bool grounded = true;

        /// <summary>지금 입고 있는 캐릭터 — 아직 안 입었으면 null.</summary>
        public CharacterId? Shown => shown;

        /// <summary>`LobbyWorld`가 점프 시작/종료 때 부른다.</summary>
        public void SetGrounded(bool isGrounded) => grounded = isGrounded;

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            flipbook = GetComponent<CharacterFlipbook>();
        }

        void OnEnable()
        {
            lastPos = transform.position;
            Refresh();
        }

        void Update()
        {
            Refresh();
            UpdateMoving();
        }

        /// <summary>프로필 캐릭터(또는 에셋)가 바뀌었을 때만 갈아입는다.</summary>
        void Refresh()
        {
            var id = ProfileService.Current.character;
            if (shown == id && shownArt == art) return;
            shown = id;
            shownArt = art;
            var animSet = art != null ? art.AnimSetOf(id) : null;
            if (flipbook != null) flipbook.SetAnimSet(animSet);
            // 크기 기준은 전투의 Player와 같게 — 애니메이션이 있으면 서기 첫 장, 없으면 정지 그림.
            var sprite = animSet != null && animSet.idle != null && animSet.idle.Count > 0 ? animSet.idle.frames[0] : art != null ? art.SpriteOf(id) : null;
            if (sprite == null) return; // 그림이 없으면 지금 그림 그대로(전투의 Player와 같은 규칙)
            sr.sprite = sprite;
            sr.color = art.TintOf(id);
            float scale = EntitySizeConfig.ScaleToHeight(sprite, art.HeightOf(id)); // 전투의 플레이어와 같은 그림 높이(CharacterArtSet)
            if (scale > 0f) transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 매 프레임 — 위치 변화량으로 Moving을 판정한다(로비는 물리가 아니라 `LobbyWorld`가 직접 옮긴다).
        /// 같은 변화량으로 좌우 반전도 같이 한다(2026-11-16 — 전투의 `PlayerRig.UpdateFacing`과 같은 이유,
        /// 반입된 그림 기본값이 왼쪽을 보고 있어서 오른쪽으로 걸을 때만 뒤집는다). 애니메이션 유무와 무관하게
        /// 적용(정지그림 캐릭터도 좌우는 맞아야 한다).
        /// </summary>
        void UpdateMoving()
        {
            float dx = transform.position.x - lastPos.x;
            if (dx > 0.0001f) sr.flipX = true;
            else if (dx < -0.0001f) sr.flipX = false;

            float dy = transform.position.y - lastPos.y;
            if (flipbook != null)
                flipbook.SetLoopPose(!grounded ? (dy > 0f ? CharacterPose.Rise : CharacterPose.Fall)
                                   : Mathf.Abs(dx) > 0.0001f ? CharacterPose.Move : CharacterPose.Idle);
            lastPos = transform.position;
        }
    }
}
