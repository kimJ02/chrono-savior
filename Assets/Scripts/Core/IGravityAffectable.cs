using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 중력구슬 빌드의 중력점(gravityWell)에 끌려갈 수 있는 대상. 원본 `updateZones()`의
    /// `gravityWell` 분기(project_test.html:3806-3827)에서 대상 쪽 처리에 해당한다 —
    /// 원본은 `e.x += dx/d*force*dt` 식으로 위치를 직접 밀어 넣지, 물리 힘이 아니다.
    ///
    /// `Combat`(1층)이 `Enemies`(2층)를 직접 참조할 수 없어(asmdef 계층 규칙) 이 Core 인터페이스로만
    /// 위치를 밀 수 있다 — `IDamageable`/`IElementAfflictable`과 같은 이유.
    /// </summary>
    public interface IGravityAffectable
    {
        Vector2 WorldPosition { get; }

        /// <summary>
        /// 이번 프레임 중력점이 끌어당기는 만큼 위치를 직접 더한다(속도가 아니라 위치 델타 —
        /// 원본이 `e.x +=`로 즉시 밀어 넣는 것과 동일). 동시에 기존 넉백을 감쇠시킨다
        /// (원본 `e.kbx = (e.kbx||0)*0.85`, project_test.html:3825 — 매 프레임 자연 감쇠와는 별개의 추가 감쇠).
        /// </summary>
        void ApplyGravityWellPull(Vector2 positionDelta);
    }
}
