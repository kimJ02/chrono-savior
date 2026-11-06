using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 전투 필드의 경계. 원본(project_test.html) mapW=2600px 기준, 100px=1유닛로 축척했다.
    /// 카메라가 필드 폭 전체를 한 번에 못 보여줄 만큼 넓어져서 CameraFollow2D가 플레이어를 따라간다
    /// (이전엔 카메라 고정이었는데, 실제 플랫폼 레이아웃을 넣으면서 원본처럼 스크롤로 바꿨다).
    /// Part A(플레이어/카메라)와 Part B(몬스터 스폰 X좌표)가 공통 참조 — 임의로 바꾸지 말 것.
    /// </summary>
    public static class FieldBounds
    {
        public const float MinX = 0f;
        public const float GroundY = 0f;

        /// <summary>
        /// 필드 오른쪽 끝. 무대(일반·보스)마다 다르다 — 바꾸는 곳은 `World.FieldLayout.SetBossArena` 한 곳뿐이다
        /// (발판 목록과 같이 바뀌어야 해서. 2026-11-06 public 필드였던 것을 읽기 전용으로).
        /// </summary>
        public static float MaxX { get; private set; } = 26f;

        /// <summary>`FieldLayout.SetBossArena`만 부른다 — 무대를 바꾸려면 그쪽을 쓸 것.</summary>
        public static void SetMaxX(float maxX) => MaxX = maxX;

        public static float ClampX(float x) => Mathf.Clamp(x, MinX, MaxX);

        public static float RandomX() => Random.Range(MinX, MaxX);
    }
}
