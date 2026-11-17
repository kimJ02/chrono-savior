using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// "붙잡혀서 끌려가는" 대상(지금은 플레이어뿐) — 2지역 보스 "부서진 여신상"의 잡기 패턴
    /// (사용자 2026-11-18 요청서 2절 "잡기에 성공하면 플레이어의 일반 이동을 잠시 제한한다")에서 쓴다.
    ///
    /// 이 인터페이스가 Core에 있는 이유는 <see cref="IDamageable"/>·<see cref="IGrabExempt"/>와 같다 —
    /// `Enemies`(보스가 있는 도메인)는 asmdef 계층 규칙상 `Characters`(플레이어 이동 스크립트)를 직접
    /// 참조할 수 없다. 그래서 보스는 플레이어의 구체 타입을 몰라도 "지금 붙잡아도 되는가 / 붙잡는다 /
    /// 끌어당긴다 / 놓는다"만 이 계약으로 묻고 시킨다.
    ///
    /// **이름이 비슷한 다른 기능과는 무관하다** — 섬영 칼날폭풍의 "강제 연행"(플레이어가 적을 붙잡아
    /// 끄는 것, <see cref="IGrabExempt"/>가 막는 대상)과는 방향이 반대다(여기는 적이 플레이어를
    /// 붙잡는다). 서로 참조하지 않고, 한쪽을 구현해도 다른 쪽과 아무 관계가 없다.
    /// </summary>
    public interface IGrabbable
    {
        /// <summary>지금 붙잡혀 있는지 — 중복으로 붙잡히지 않게, 잡으려는 쪽이 먼저 확인한다.</summary>
        bool IsGrabbed { get; }

        /// <summary>
        /// 붙잡기 시작 — 구현체는 이 시점부터 자기 쪽 일반 이동 입력·물리를 멈춘다(위치는 이후
        /// <see cref="MoveGrabbedTo"/>로 붙잡은 쪽이 직접 넣어 준다). 이미 붙잡혀 있으면 아무 일도
        /// 안 한다(중복 호출 안전).
        /// </summary>
        /// <param name="grabber">붙잡은 쪽의 오브젝트.</param>
        void BeginGrab(GameObject grabber);

        /// <summary>
        /// 붙잡은 쪽이 매 프레임(또는 여러 프레임에 걸쳐) 끌어당기는 중간 위치로 직접 옮긴다 —
        /// 한 번에 순간이동시키지 말고 보간해서 여러 번 불러야 "부드럽게 끌려온다"가 된다.
        /// 붙잡힌 상태가 아니면 아무 일도 안 한다.
        /// </summary>
        void MoveGrabbedTo(Vector2 position);

        /// <summary>
        /// 붙잡기 종료 — 일반 이동 입력·물리 상태를 원래대로 되돌린다. 이미 풀려 있으면 아무 일도
        /// 안 한다(보스 사망·비활성화 경로에서 여러 번 불러도 안전해야 한다).
        /// </summary>
        void EndGrab();
    }
}
