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

        /// <summary>
        /// 로비 애니메이션(2026-11-16) — 전투의 `PlayerRig`와 같은 에셋(`CharacterArtSet.AnimatorOf`)을 쓰되, 로비엔
        /// 전투가 없으니 Idle/Move/JumpFall만 쓴다(Attack/Hit 트리거는 안 걸음). 로비는 물리가 아니라 `LobbyWorld`가
        /// `transform.position`을 직접 옮기므로, Moving은 프레임 사이 위치 변화량으로 판정한다(전투처럼 Rigidbody2D
        /// 속도를 읽을 수 없다). Grounded는 기본 true — **2026-11-18, 꾸밈용 점프가 생기면서** `LobbyWorld`가
        /// 점프 중에만 <see cref="SetGrounded"/>로 꺼 준다(발판이 없어 실제로 바닥이 바뀌는 건 아니다).
        /// </summary>
        Animator anim;
        Vector3 lastPos;

        static readonly int AnimMoving = Animator.StringToHash("Moving");
        static readonly int AnimGrounded = Animator.StringToHash("Grounded");

        /// <summary>지금 입고 있는 캐릭터 — 아직 안 입었으면 null.</summary>
        public CharacterId? Shown => shown;

        /// <summary>`LobbyWorld`가 점프 시작/종료 때 부른다 — Animator가 있고 켜져 있을 때만 반영(정지그림 캐릭터는 그냥 무시).</summary>
        public void SetGrounded(bool grounded)
        {
            if (anim != null && anim.enabled) anim.SetBool(AnimGrounded, grounded);
        }

        void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            anim = GetComponent<Animator>();
            if (anim == null) anim = gameObject.AddComponent<Animator>();
            // ⚠️ 로비는 시간이 멈춘 채(timeScale 0) 캐릭터만 실제 시간으로 걷는다(LobbyWorld가 이미
            // Time.unscaledDeltaTime을 쓰는 것과 같은 이유) — Animator 기본값(Normal)은 **timeScale을 그대로
            // 따라가서 timeScale 0에선 애니메이션 재생 자체가 멈춘다**(2026-11-16 사용자 발견: "전투에서는
            // 잘 나오는데 왜 로비에선 안 나오지" — 전투는 timeScale 1이라 안 드러났다). UnscaledTime으로
            // 바꿔서 로비에서도 Idle/Move가 실제 시간 기준으로 돈다.
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
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
            ApplyAnimator(id);
            var sprite = art != null ? art.SpriteOf(id) : null;
            if (sprite == null) return; // 그림이 없으면 지금 그림 그대로(전투의 Player와 같은 규칙)
            sr.sprite = sprite;
            sr.color = art.TintOf(id);
            float scale = EntitySizeConfig.ScaleToHeight(sprite, art.HeightOf(id)); // 전투의 플레이어와 같은 그림 높이(CharacterArtSet)
            if (scale > 0f) transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// 캐릭터가 바뀔 때만 호출 — `CharacterArtSet.AnimatorOf`가 null이면(아직 애니메이션 없는 캐릭터) Animator를
        /// 꺼서 <see cref="Refresh"/>가 넣은 정지그림이 그대로 유지되게 한다(전투 `PlayerRig.ApplyAnimator`와 같은 규칙).
        /// </summary>
        void ApplyAnimator(CharacterId id)
        {
            if (anim == null) return;
            var controller = art != null ? art.AnimatorOf(id) : null;
            if (controller == null)
            {
                anim.enabled = false;
                return;
            }
            anim.runtimeAnimatorController = controller;
            anim.enabled = true;
            anim.SetBool(AnimGrounded, true); // 로비엔 점프가 없다 — 항상 땅 위
        }

        /// <summary>
        /// 매 프레임 — 위치 변화량으로 Moving을 판정한다(로비는 물리가 아니라 `LobbyWorld`가 직접 옮긴다).
        /// 같은 변화량으로 좌우 반전도 같이 한다(2026-11-16 — 전투의 `PlayerRig.UpdateFacing`과 같은 이유,
        /// 반입된 그림 기본값이 왼쪽을 보고 있어서 오른쪽으로 걸을 때만 뒤집는다). Animator 유무와 무관하게
        /// 적용(정지그림 캐릭터도 좌우는 맞아야 한다).
        /// </summary>
        void UpdateMoving()
        {
            float dx = transform.position.x - lastPos.x;
            if (dx > 0.0001f) sr.flipX = true;
            else if (dx < -0.0001f) sr.flipX = false;

            if (anim != null && anim.enabled) anim.SetBool(AnimMoving, Mathf.Abs(dx) > 0.0001f);
            lastPos = transform.position;
        }
    }
}
