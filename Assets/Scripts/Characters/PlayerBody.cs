using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Characters
{
    /// <summary>
    /// 원본 좌표 → Unity 좌표 변환에서 **높이 기준점**을 맞추는 헬퍼.
    ///
    /// 원본은 모든 개체의 `y`가 **발**이다(`entRect = [x - w/2, y - h, w, h]`, project_test.html:1003) —
    /// 그래서 `p.y - 36`(마법탄)·`p.y - 38`(메카닉 총구)은 "발에서 36·38px 위"다.
    /// Unity 플레이어는 기준점이 **몸 중심**(원형 콜라이더 중심)이라, 중심에서 재면 반지름만큼 높아져
    /// 키 0.5짜리 고블린 머리 위로 공격이 지나간다(2026-09-28 사용자가 캐릭터 크기를 줄이며 발견).
    /// 원본의 `p.y - N` 꼴 높이는 전부 <see cref="Feet"/>에서 잰다.
    /// </summary>
    public static class PlayerBody
    {
        /// <summary>
        /// 발 위치(원본 `(p.x, p.y)`) — 몸 중심에서 콜라이더 월드 반지름만큼 내린다.
        /// 콜라이더가 없으면 설정값(<see cref="EntitySizeConfig.PlayerRadius"/>)을 쓴다.
        /// </summary>
        public static Vector2 Feet(Transform t, CircleCollider2D body)
        {
            float r = body != null ? body.radius * Mathf.Abs(t.lossyScale.y) : EntitySizeConfig.PlayerRadius;
            return new Vector2(t.position.x, t.position.y - r);
        }
    }
}
