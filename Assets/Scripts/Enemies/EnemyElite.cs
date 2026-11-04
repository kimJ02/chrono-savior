using UnityEngine;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 엘리트 표식 — 원본 `spawnEnemyAt()`의 `elite` 플래그(project_test.html:3948)에 대응한다.
    ///
    /// 원본 엘리트는 **별도의 몹 종류가 아니라 아무 몹에게나 붙는 승격 상태**다(`CONFIG.elite` :696).
    /// 체력 ×4, 피해 ×1.5, 몸집 ×1.35, 보상 ×5이고 **골드는 확률을 무시하고 항상 떨군다**
    /// (`killEnemy`의 `if (Math.random() < goldDropChance || e.boss || e.elite)`, `:1818`).
    /// 행동(AI)은 일반 몹과 완전히 같다 — 그래서 이 컴포넌트엔 로직이 없고, 스포너가 수치를
    /// 곱한 뒤 "이 몹은 엘리트였다"는 사실만 남겨 처치 보상 계산이 읽어 간다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyElite : MonoBehaviour
    {
    }
}
