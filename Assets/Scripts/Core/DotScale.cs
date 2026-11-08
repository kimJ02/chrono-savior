namespace ChronoSavior.Core
{
    /// <summary>
    /// **화면의 도트 크기는 하나다**(2026-11-09 사용자 "도트 크기를 맞춰야", Codex와 배율 2로 정함) — 모든 월드 그림의 1px이 월드 격자
    /// (PPU 128, 기준 960×540) **2칸**으로 보인다. 그림 크기를 판정에 맞춰 늘리지 않는다: 크기가 달라야 하면 작은/큰 그림을 고르고,
    /// 범위는 판정 고리(`Combat.RangeRing`)로 보인다. 예외는 길이만큼 이어 붙이는 것(레이저·중계선 — 도트는 그대로)과 그림 없는 임시 도형 몹.
    /// 기준표·보고서는 `docs/pixel-standard.md`·메뉴 `ChronoSavior/도트 배율 보고서`.
    /// </summary>
    public static class DotScale
    {
        /// <summary>그림 1px이 격자 몇 칸인가 — 모든 월드 그림 공통.</summary>
        public const int Value = 2;
        /// <summary>월드 격자 PPU(1유닛 = 128칸).</summary>
        public const float GridPixelsPerUnit = 128f;
        /// <summary>그림 px 수 → 화면 유닛.</summary>
        public static float Units(float artPixels) => artPixels * Value / GridPixelsPerUnit;
        /// <summary>그림(PPU p)을 도트 배율 2로 그리려면 곱할 배율 — PPU 128 그림이면 2.</summary>
        public static float ScaleFor(UnityEngine.Sprite sprite) => sprite == null ? Value : Value * sprite.pixelsPerUnit / GridPixelsPerUnit;
    }
}
