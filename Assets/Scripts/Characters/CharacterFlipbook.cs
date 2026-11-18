using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 캐릭터 몸 그림 재생(2026-11-17 애니메이션 플립북 전환 — `docs/animation-flipbook-proposal.md`). Animator 대신 이것 하나가 그림만 넘긴다:
    /// **어떤 자세인지는 게임 코드가 정하고**(루프 자세는 <c>PlayerRig</c>·<c>LobbyAvatar</c>가 매 프레임, 공격·피격은 그 일이 일어난 곳에서),
    /// 여기선 그 자세의 프레임 묶음(<see cref="CharacterAnimSet"/>)을 시간으로 넘긴다. 씬의 Player·로비 아바타에 **배치**돼 있다(코드로 붙이지 않는다).
    ///
    /// 우선순위(위가 이김): 피격 &gt; 공격(한 번 재생) &gt; 유지 자세(레이저처럼 누르는 동안 공격 마지막 프레임) &gt; 루프 자세(차지·상승·하강·이동·서기).
    /// - 공격 중 또 공격하면 처음부터 다시 틀지 않는다(연사 때 첫 프레임만 반복되던 문제 — 팀원 2026-11-16 결정 유지). 피격 중 공격은 무시.
    /// - 피격은 공격을 끊는다. 피격이 끝났는데 유지 자세가 아직 켜져 있으면 그리로 돌아간다.
    /// 시간: 전투는 게임 시간(히트스톱·일시정지 땐 같이 멈춤), 로비는 <see cref="useUnscaledTime"/>(시간이 멈춘 로비에서도 걷는다).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CharacterFlipbook : MonoBehaviour
    {
        [Tooltip("로비처럼 시간이 멈춘 화면에서도 돌게(실제 시간). 전투는 끈다 — 히트스톱·일시정지 때 같이 멈춘다.")]
        public bool useUnscaledTime;

        SpriteRenderer sr;
        CharacterAnimSet set;

        CharacterPose loopPose = CharacterPose.Idle;
        float loopT;

        bool oneShotOn;
        CharacterPose oneShotPose;
        SpriteFlipbook oneShotBook;
        float oneShotT;

        bool heldOn;
        SpriteFlipbook heldBook;
        float heldT;

        public CharacterAnimSet Set => set;

        /// <summary>지금 보이는 자세(테스트·디버그용).</summary>
        public CharacterPose Current => oneShotOn ? oneShotPose : heldOn ? CharacterPose.Attack : loopPose;

        void Awake() => sr = GetComponent<SpriteRenderer>();

        /// <summary>캐릭터를 바꿀 때 — 재생 중이던 건 다 지운다.</summary>
        public void SetAnimSet(CharacterAnimSet animSet)
        {
            set = animSet;
            loopPose = CharacterPose.Idle;
            loopT = 0f;
            oneShotOn = false;
            heldOn = false;
            Draw();
        }

        /// <summary>매 프레임 — 서기·이동·상승·하강·차지 중 하나. 같은 자세면 이어서 돈다.</summary>
        public void SetLoopPose(CharacterPose pose)
        {
            if (pose == loopPose) return;
            loopPose = pose;
            loopT = 0f;
        }

        /// <summary>공격·피격을 한 번 재생한다. 끝나면 유지 자세 → 루프 자세로 돌아간다.</summary>
        public void PlayOnce(CharacterPose pose, int attackIndex = 0)
        {
            if (set == null) return;
            if (oneShotOn && oneShotT < oneShotBook.Duration)
            {
                if (pose == CharacterPose.Attack) return; // 공격 중 공격은 끝까지(재시작 안 함), 피격 중 공격도 무시
            }
            var book = set.Get(pose, attackIndex);
            if (book == null || book.Count == 0) return;
            oneShotOn = true;
            oneShotPose = pose;
            oneShotBook = book;
            oneShotT = 0f;
        }

        /// <summary>누르는 동안 공격 자세를 붙잡는다(메카닉 레이저) — 한 번 돌고 마지막 프레임에서 멈춘다. <see cref="ReleaseHeldPose"/>까지.</summary>
        public void PlayAndHold(CharacterPose pose, int attackIndex = 0)
        {
            if (set == null || heldOn) return;
            var book = set.Get(pose, attackIndex);
            if (book == null || book.Count == 0) return;
            heldOn = true;
            heldBook = book;
            heldT = 0f;
        }

        public void ReleaseHeldPose() => heldOn = false;

        public bool IsHolding => heldOn;

        void LateUpdate() => Refresh(useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime);

        /// <summary>시간을 넘기고 그린다(테스트가 직접 부른다).</summary>
        public void Refresh(float dt)
        {
            loopT += dt;
            if (heldOn) heldT += dt;
            if (oneShotOn)
            {
                oneShotT += dt;
                if (oneShotT >= oneShotBook.Duration) oneShotOn = false; // 되풀이 묶음이 와도 한 바퀴로 끝
            }
            Draw();
        }

        void Draw()
        {
            if (set == null || sr == null) return;
            Sprite frame;
            if (oneShotOn) frame = oneShotBook.FrameAtHeld(oneShotT);
            else if (heldOn) frame = heldBook.FrameAtHeld(heldT);
            else frame = set.Get(loopPose).FrameAt(loopT);
            if (frame != null) sr.sprite = frame;
        }
    }
}
