using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Enemies
{
    /// <summary>
    /// 박쥐(wisp)의 비행 이동 — 원본 `updateEnemies()`의 wisp 분기
    /// (project_test.html:4042~4057)와 `CONFIG.wispYSpeed`(`:700`) 그대로.
    ///
    /// **지면·발판을 완전히 무시하고 날아다닌다.** <see cref="IEnemyVerticalMotion"/>을 구현했으므로
    /// <see cref="EnemyMove"/>가 중력과 착지 처리를 통째로 건너뛴다.
    ///
    /// 핵심 두 가지(원본 주석 그대로):
    /// - 감지 범위(350px) **밖이면 제자리에서 사인파로 떠 있는다** — 쫓아오지 않는다.
    /// - Y축 추적은 **거리와 무관하게 일정한 저속**(46px/s)이다. 가까워도 안 느려지고 멀어도 안 빨라진다.
    /// </summary>
    [RequireComponent(typeof(EnemyMove))]
    public class EnemyFlyerMotion : MonoBehaviour, IEnemyMotion, IEnemyVerticalMotion
    {
        [Header("원본 wisp 분기 그대로 (거리는 100px=1유닛)")]
        [Tooltip("이 X거리 밖이면 추적하지 않고 제자리 부유. 원본 350.")]
        public float detectRangeX = 3.5f;
        [Tooltip("이 거리 안으로 붙으면 수평 속도가 절반이 된다. 원본 40.")]
        public float slowRangeX = 0.4f;
        [Tooltip("Y축 추적 속도(일정). 원본 CONFIG.wispYSpeed 46 ÷100.")]
        public float verticalSpeed = 0.46f;

        [Header("부유 흔들림 (원본 sin 항)")]
        [Tooltip("제자리 부유 수평 진폭. 원본 22 ÷100.")]
        public float idleSwayX = 0.22f;
        [Tooltip("제자리 부유 수직 진폭. 원본 14 ÷100.")]
        public float idleSwayY = 0.14f;
        [Tooltip("추적 시 목표 높이의 흔들림 진폭. 원본 20 ÷100.")]
        public float chaseBobY = 0.20f;
        [Tooltip("플레이어보다 이만큼 위를 노린다. 원본 y-42 → 우리 좌표계는 위가 +.")]
        public float chaseHeightOffset = 0.42f;

        [Header("고도 제한 (원본 clamp(e.y, 90, groundY-16))")]
        [Tooltip("지면에서 이만큼 위가 최저 고도. 원본 groundY-16.")]
        public float minHeightAboveGround = 0.16f;
        [Tooltip("지면에서 이만큼 위가 최고 고도. 원본 y=90 → (620-90)/100.")]
        public float maxHeightAboveGround = 5.3f;

        float seed;

        void Awake() => seed = Random.Range(0f, 100f); // 원본 `seed: Math.random()*100`(:3963)

        public float GetHorizontalSpeed(float dt, Transform target, float baseSpeed)
        {
            if (target == null) return IdleSwayX();

            float dx = target.position.x - transform.position.x;
            if (Mathf.Abs(dx) > detectRangeX) return IdleSwayX();

            // 원본 `sign(dx) * spd * (|dx| > 40 ? 1 : 0.5)` — 바짝 붙으면 절반 속도.
            int towards = dx > 0f ? 1 : -1;
            return towards * baseSpeed * (Mathf.Abs(dx) > slowRangeX ? 1f : 0.5f);
        }

        public float GetVerticalPosition(float dt, Transform target, float currentY)
        {
            float y = currentY;

            if (target == null || Mathf.Abs(target.position.x - transform.position.x) > detectRangeX)
            {
                // 감지 밖: 제자리 부유. 원본 `e.y += sin(time*1.6 + seed) * 14 * dt`
                y += Mathf.Sin(Time.time * 1.6f + seed) * idleSwayY * dt;
            }
            else
            {
                // 추적: 목표 높이로 **일정 속도**로만 다가간다(거리 비례 아님 — 원본 주석 그대로).
                float targetY = target.position.y + chaseHeightOffset
                                + Mathf.Sin(Time.time * 2.2f + seed) * chaseBobY;
                float dy = targetY - y;
                float step = verticalSpeed * dt;
                y += Mathf.Abs(dy) <= step ? dy : Mathf.Sign(dy) * step;
            }

            return Mathf.Clamp(y,
                FieldBounds.GroundY + minHeightAboveGround,
                FieldBounds.GroundY + maxHeightAboveGround);
        }

        /// <summary>원본 `e.vx = sin(time*0.8 + seed) * 22` — 감지 범위 밖 제자리 흔들림.</summary>
        float IdleSwayX() => Mathf.Sin(Time.time * 0.8f + seed) * idleSwayX;
    }
}
