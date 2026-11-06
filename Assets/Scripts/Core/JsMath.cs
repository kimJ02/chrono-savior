using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// 원본(JavaScript)과 같은 결과를 내야 하는 수학 — 지금은 반올림 하나(2026-11-07 최종 점검).
    /// 원본 `Math.round`는 .5를 **항상 위로** 올리지만 Unity `Mathf.Round`·`RoundToInt`는 짝수 쪽으로 보낸다(2.5 → 2).
    /// 몹 체력·공격과 피해처럼 원본이 `Math.round`한 값은 이걸 쓴다.
    /// </summary>
    public static class JsMath
    {
        /// <summary>원본 `Math.round(x)` — `floor(x + 0.5)`.</summary>
        public static float Round(float x) => Mathf.Floor(x + 0.5f);
    }
}
