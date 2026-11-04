using UnityEngine;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 몹 종류별 **수평 이동 결정**. 원본 `updateEnemies()`가 타입마다 `mvx`를 다르게 계산하는 분기
    /// (project_test.html:4059~4111)에 대응한다.
    ///
    /// 중력·발판 착지·가장자리 반전·경계 클램프·넉백·접촉 피해는 종류가 달라도 똑같아서
    /// <see cref="EnemyMove"/>가 계속 맡는다(원본도 그 부분은 타입 공통 코드다, `:4112`~`:4148`).
    /// 이 인터페이스를 구현한 컴포넌트가 같은 오브젝트에 붙어 있으면 `EnemyMove`가 기본 보행 대신
    /// 그쪽에 수평 속도를 물어본다 — CLAUDE.md "행동이 다르면 타입 전용 스크립트" 규칙 그대로.
    /// </summary>
    public interface IEnemyMotion
    {
        /// <param name="dt">이번 프레임 시간.</param>
        /// <param name="target">추적 대상(플레이어). 없으면 null.</param>
        /// <param name="baseSpeed">이 몹의 이동 속도(`EnemyData.moveSpeed`).</param>
        /// <returns>이번 프레임 수평 속도(원본 `mvx`).</returns>
        float GetHorizontalSpeed(float dt, Transform target, float baseSpeed);
    }

    /// <summary>
    /// **비행형**의 수직 이동. 구현체가 붙어 있으면 <see cref="EnemyMove"/>는 중력·발판 착지를
    /// 통째로 건너뛰고 여기서 받은 Y를 그대로 쓴다(원본 박쥐 분기 `:4042`~`:4057` — 지면을 무시하고
    /// 플레이어 Y를 일정 속도로 쫓는다).
    /// </summary>
    public interface IEnemyVerticalMotion
    {
        /// <returns>이번 프레임에 있어야 할 월드 Y 좌표.</returns>
        float GetVerticalPosition(float dt, Transform target, float currentY);
    }

    /// <summary>
    /// 상태에 따라 접촉 피해가 달라지는 몹. 원본은 멧돼지가 **질주 중일 때만** 접촉 피해 ×1.4다
    /// (`chargeMul`, project_test.html:4147). <see cref="EnemyMove"/>가 접촉 피해를 줄 때 곱한다.
    /// </summary>
    public interface IEnemyContactDamageModifier
    {
        float ContactDamageMultiplier { get; }
    }
}
