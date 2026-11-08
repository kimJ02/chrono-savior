using UnityEngine;

namespace ChronoSavior.World
{
    /// <summary>
    /// 한 스테이지의 그림 묶음 — 땅·발판·배경·보스 포탈(2026-11-06 에셋 교체 경계 3.5-b). `StageVisuals`가 직렬화로 받는다(예전엔 `Resources` 경로 문자열로 읽었다).
    /// **그림을 바꿀 땐 이 에셋만 바꾼다** — 그림 파일·그 그림에 딸린 숫자(배경 배율·보여 줄 범위·풀밭 높이, 포탈 배율)를 같이 둔다(배경을 바꾸면 이 숫자도 바뀌므로).
    /// 지역별 배경이 생기면 에셋을 하나 더 만든다. 지금 칸은 지금 쓰는 것만 — 미리 늘리지 않는다.
    /// 에셋: `Assets/Data/Stages/`. 도트 배율 기준은 `docs/pixel-standard.md`.
    /// </summary>
    [CreateAssetMenu(menuName = "ChronoSavior/Stage Art Set", fileName = "StageArt")]
    public class StageArtSet : ScriptableObject
    {
        [Header("땅·발판 (판정 상자 위에 타일로 깐다)")]
        [Tooltip("땅 맨 윗줄 타일(풀).")]
        public Sprite groundTop;
        [Tooltip("땅 아랫줄 타일(흙).")]
        public Sprite groundDirt;
        [Tooltip("발판 타일.")]
        public Sprite platformTile;

        [Header("배경 (카메라를 따라 천천히 움직인다)")]
        public Sprite background;
        [Tooltip("배경 배율 = 공통 도트 배율(Core.DotScale, 2026-11-09 — 예전 3). 640×360이 10×5.6유닛으로 화면(7.5×4.2)을 덮는다.")]
        [Min(1)] public int backgroundScale = 2;
        [Tooltip("카메라가 필드 왼쪽 끝에 있을 때 화면 가운데가 가리킬 배경 그림의 x(원본 px, 왼쪽 기준) — 그림 가장자리가 안 보이게.")]
        public float backgroundViewLeftPx = 240f;
        [Tooltip("카메라가 필드 오른쪽 끝에 있을 때 화면 가운데가 가리킬 배경 그림의 x(원본 px).")]
        public float backgroundViewRightPx = 400f;
        [Tooltip("배경 그림 속 땅(풀밭)의 높이(원본 px, 위 기준) — 땅에 섰을 때 이 높이가 우리 지면에 온다.")]
        public float backgroundGrassYPx = 181.8f;
        [Tooltip("배경 그림에서 보여도 되는 맨 윗줄(원본 px, 위 기준) — 그 위는 테두리라 화면에 안 걸리게(1스테이지 숲: 위 19px가 어두운 바위 테두리, 2026-11-09 배율 2로 줄이며 드러났다).")]
        public float backgroundTopPx = 0f;

        [Header("보스 포탈")]
        public Sprite portal;
        [Tooltip("포탈 그림 배율(땅 위에 세운다) — 1스테이지: 1.5배(48×96px, 사용자 결정 2026-10-24 — Figma는 1배(캐릭터와 키가 같다)인데 작아서 '좀만 키우자'. " +
                 "정수배가 아니라 확대하면 픽셀 크기가 들쭉날쭉하다 — 1·1.5·2배 비교 캡처를 보고 고름, 2배는 선명하지만 크다).")]
        public float portalScale = 2f;
    }
}
