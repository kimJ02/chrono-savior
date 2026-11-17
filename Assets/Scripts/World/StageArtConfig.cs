using UnityEngine;

namespace ChronoSavior.World
{
    /// <summary>
    /// Figma 도트 그림을 선명하게 보이기 위한 화면 기준 — **2026-10-24 사용자 결정 "C안(절충)"**
    /// (미리보기 `claude작업물/chrono_preview/비교_한장.png`). 의도적 편차: `docs/original-parity.md` 9절.
    ///
    /// - **1유닛 = 128px.** Figma 캐릭터 64px가 판정 0.5유닛(`Core/EntitySizeConfig.PlayerHeight`)과 딱 맞는다.
    /// - **월드는 960×540px를 그대로 보이고 정수배로 키운다**(1080p에서 2배, `PixelPerfectCamera`).
    ///   한 화면에 7.5×4.22유닛 — 원본 카메라(13.65×7.68, 필드 4층 전부)의 55%라 카메라가 위아래로도 따라간다.
    /// - HUD는 이것과 따로 Figma 640×360 기준(1080p 3배)이다.
    /// </summary>
    public static class StageArtConfig
    {
        public const int PixelsPerUnit = 128;
        public const int RefWidth = 960;
        public const int RefHeight = 540;
        public const float ViewHalfWidth = RefWidth / (2f * PixelsPerUnit);   // 3.75
        public const float ViewHalfHeight = RefHeight / (2f * PixelsPerUnit); // 2.109375

        /// <summary>
        /// 땅(더 내려갈 데 없음)에 서 있을 때 지면이 화면 아래에서 이 비율 높이에 온다 — 아래는 버리는 공간이라
        /// 작게, 위를 넉넉히 보여준다. (2026-11-17, 사용자가 실제로 플레이해보고 지적: "1층에 있을 때는 위로밖에
        /// 못 가는데 화면은 바닥이 2~30%를 차지한다" — 옛 `GroundScreenFraction`(0.275, 위아래 어디서나 똑같았다)을
        /// 이 값과 <see cref="GroundScreenFractionAtTop"/> 둘로 나눠 바닥~맨 위 발판 사이를 보간한다(<see cref="ScreenFractionAt"/>).)
        /// </summary>
        public const float GroundScreenFractionAtFloor = 0.15f;

        /// <summary>
        /// 맨 위 발판(더 올라갈 데 없음)에 서 있을 때 그 발판이 화면 아래에서 이 비율 높이에 온다 — 위는 버리는
        /// 공간이라 작게, 아래를 넉넉히 보여준다. (2026-11-17, 같은 지적의 반대쪽: "맨 위 플랫폼일 때는 위에 뭐가
        /// 없고 밑을 봐야 하는데 화면은 위에 있어서 밑이 안 보인다.")
        /// </summary>
        public const float GroundScreenFractionAtTop = 0.65f;

        /// <summary>t(0 = 땅, 1 = 맨 위 발판, 그 사이는 보간)에서의 화면 비율.</summary>
        public static float ScreenFractionAt(float t) => Mathf.Lerp(GroundScreenFractionAtFloor, GroundScreenFractionAtTop, t);

        /// <summary>그 t에서의 카메라 오프셋(서 있는 발 높이 → 카메라 중심 높이 차이) — 비율이 <see cref="ScreenFractionAt"/>에 오게.
        /// 옛 고정 상수 `CameraYOffset`을 대신한다(2026-11-17 — t=0일 때 값이 옛 상수와 같은 공식이다).</summary>
        public static float CameraOffsetAt(float t) => ViewHalfHeight * 2f * (0.5f - ScreenFractionAt(t));

        /// <summary>공중에서는 머리가 화면 위 이 거리 안으로 들어올 때만 카메라가 따라 올라간다.</summary>
        public const float CameraAirTopMargin = 0.75f;

        /// <summary>공중에서 발이 화면 아래 이 거리 밖으로 떨어지면 따라 내려간다.</summary>
        public const float CameraAirBottomMargin = 0.35f;

        public const float CameraSmoothTimeY = 0.2f;

        // 배경 배율·보여 줄 범위는 그 그림에 딸린 값이라 스테이지 그림 묶음(`StageArtSet`)에 있다(2026-11-06 — 예전엔 여기 BackgroundScale = 3).
    }
}
