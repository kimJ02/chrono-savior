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
        public static float MinX = 0f;
        public static float MaxX = 26f;
        public static float GroundY = 0f;

        public static float ClampX(float x) => Mathf.Clamp(x, MinX, MaxX);

        public static float RandomX() => Random.Range(MinX, MaxX);
    }
}
