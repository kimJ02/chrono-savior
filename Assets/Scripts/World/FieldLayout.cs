using UnityEngine;

namespace ChronoSavior.World
{
    /// <summary>
    /// 필드의 정적 공간 데이터(발판 배치, 원본 스폰 포인트) — 씬의 발판(`20_Combat`의 `PlatformSet`)과
    /// 몬스터 스폰·카메라·미니맵이 공유하는 단일 출처. 예전엔 이 발판 좌표가
    /// 씬을 코드로 만들던 도구(2026-11-05 삭제) 안에만 있어서 스폰 포인트 계산 때 값을 복붙해야 했는데,
    /// 그러면 나중에 발판 배치가 또 바뀔 때(층간 간격을 넓힌 것처럼) 두 군데를
    /// 따로 고쳐야 하는 위험이 있어 여기 하나로 합쳤다.
    ///
    /// **일반 사냥과 보스전은 무대가 다르다** — 원본도 `platforms = mode === 'normal' ?
    /// NORMAL_PLATFORMS : BOSS_PLATFORMS`(project_test.html:4316)로 통째로 갈아끼우고
    /// 맵 폭도 2600 → 1800으로 좁힌다(`:4313`). 좁은 무대에서 슬램·돌진을 피하는 게 보스전
    /// 설계라, 넓은 필드를 그대로 두면 그냥 도망다니면 되는 싸움이 된다.
    /// </summary>
    public static class FieldLayout
    {
        // 원본(project_test.html) NORMAL_PLATFORMS의 X 배치(centerX/width)는 그대로 옮김. pl.x는
        // 원본에서 "왼쪽 끝" 좌표였음이 충돌판정 코드(`p.x > pl.x - 6 && p.x < pl.x + pl.w + 6`)로
        // 확인됨 — 중심이 아니다. 100px=1유닛, groundY=620 기준 centerX=(x+w/2)/100 로 환산.
        //
        // Y(층 간격)는 원본 그대로(y=505/395/285/185, 층간 1.0~1.1유닛)가 아니라 사용자 요청으로
        // 층간 1.35유닛으로 넓혔다 — 의도적 편차(원본으로 되돌리지 말 것, docs/sprints/01-combat-core.md 1번 참고).
        public static readonly float[,] NormalPlatforms =
        {
            // centerX, centerY, width  (전부 유닛)
            {3.20f, 1.35f, 2.80f}, {9.00f, 1.35f, 3.20f}, {15.40f, 1.35f, 3.00f}, {21.50f, 1.35f, 3.20f}, // 1층
            {5.90f, 2.70f, 3.00f}, {12.30f, 2.70f, 3.20f}, {18.60f, 2.70f, 3.00f}, {24.00f, 2.70f, 2.60f}, // 2층
            {3.30f, 4.05f, 2.60f}, {9.60f, 4.05f, 3.00f}, {16.20f, 4.05f, 3.00f}, {22.00f, 4.05f, 2.60f}, // 3층
            {6.80f, 5.40f, 2.80f}, {13.40f, 5.40f, 3.00f}, {19.70f, 5.40f, 2.80f}, // 4층
        };

        /// <summary>
        /// 보스 무대 — 원본 `BOSS_PLATFORMS`(project_test.html:1547) 3개 그대로.
        /// `{x:280,y:480,w:280} {x:1240,y:480,w:280} {x:740,y:360,w:300}` → centerX=(x+w/2)/100.
        /// 층 높이는 일반 무대와 같은 간격(1.35/2.70)을 쓴다 — 원본 값(1.40/2.60)과 거의 같고,
        /// 우리 "층간 1.35" 의도적 편차와 어긋나면 같은 점프 감각이 안 나온다.
        /// </summary>
        public static readonly float[,] BossPlatforms =
        {
            {4.20f, 1.35f, 2.80f}, {13.80f, 1.35f, 2.80f}, // 아래층 좌우
            {8.90f, 2.70f, 3.00f},                          // 위층 가운데
        };

        /// <summary>일반 사냥 맵 폭. 원본 `CONFIG.world.mapW = 2600`(`:601`).</summary>
        public const float NormalMapWidth = 26f;
        /// <summary>보스전 맵 폭. 원본 `CONFIG.bossWorld.mapW = 1800`(`:602`) — 훨씬 좁다.</summary>
        public const float BossMapWidth = 18f;

        // ── 보스 포탈 (원본에 없음 — 사용자 지시 2026-09-16) ──
        /// <summary>
        /// 보스 포탈의 X 위치. 일반 맵 우측 끝 근처다(폭 26 중 24.2).
        /// **플레이어 시작 지점(x=2.2)의 반대쪽 끝**이라, 사냥하며 자연스럽게 발견하게 되고
        /// 100마리를 채운 뒤 계속 사냥하려는 사람이 실수로 근처에 밀려갈 일도 적다.
        /// </summary>
        public const float BossPortalX = 24.2f;
        /// <summary>포탈 크기(월드 유닛) — 문처럼 세로로 길다.</summary>
        public const float BossPortalWidth = 0.9f;
        public const float BossPortalHeight = 2.0f;

        /// <summary>지금 무대가 보스전인지. <see cref="SetBossArena"/>가 바꾼다.</summary>
        public static bool IsBossArena { get; private set; }

        /// <summary>
        /// 지금 무대의 발판들. **배열을 그대로 돌려주므로 호출부는 안 바뀐다**
        /// (`Platforms[i, 0]`, `Platforms.GetLength(0)` 그대로 쓸 수 있다).
        /// </summary>
        public static float[,] Platforms => IsBossArena ? BossPlatforms : NormalPlatforms;

        public const float PlatformThickness = 0.15f;

        /// <summary>
        /// 무대를 바꾼다. 발판 목록과 <see cref="Core.FieldBounds.MaxX"/>가 **같이** 바뀐다 —
        /// 둘이 따로 놀면 좁은 보스 무대에서 몹이 맵 밖에 스폰된다.
        /// </summary>
        public static void SetBossArena(bool boss)
        {
            IsBossArena = boss;
            Core.FieldBounds.SetMaxX(boss ? BossMapWidth : NormalMapWidth);
        }

        // 원본 buildSpawnPoints()의 바닥 그리드: `for (let x=260; x<mapW-160; x+=380) {x, y:groundY, w:300}`
        // → 100px=1유닛로 축척하면 2.6부터 3.8 간격.
        const float GroundGridStart = 2.6f;
        const float GroundGridStep = 3.8f;
        const float GroundGridEndMargin = 1.6f; // 원본 `x < mapW - 160`

        /// <summary>
        /// 바닥 스폰 지점. **맵 폭에 따라 개수가 달라진다** — 원본이 `mapW`를 보고 루프를 도는 것과 같다.
        /// 고정 배열로 두면 좁은 보스 무대에서 맵 밖 좌표가 섞여 들어간다.
        /// </summary>
        public static float[] GroundGridX
        {
            get
            {
                float end = Core.FieldBounds.MaxX - GroundGridEndMargin;
                int n = 0;
                for (float x = GroundGridStart; x < end; x += GroundGridStep) n++;
                if (n == 0) return new[] { GroundGridStart }; // 맵이 극단적으로 좁아도 한 곳은 있어야 한다

                var result = new float[n];
                int i = 0;
                for (float x = GroundGridStart; i < n; x += GroundGridStep) result[i++] = x;
                return result;
            }
        }

        public const float GroundGridPointWidth = 3.0f; // 원본 w:300px → 3.0유닛

        /// <summary>발판 i번 위에 반지름 radius인 원형 콜라이더가 안착했을 때의 중심 Y(발판 윗면+반지름).</summary>
        public static float PlatformLandingY(int index, float radius) => Platforms[index, 1] + PlatformThickness / 2f + radius;
        public static float PlatformLeftX(int index) => Platforms[index, 0] - Platforms[index, 2] / 2f;
        public static float PlatformRightX(int index) => Platforms[index, 0] + Platforms[index, 2] / 2f;
        /// <summary>발판 i번의 윗면 높이 — 발이 서는 곳.</summary>
        public static float PlatformTopY(int index) => Platforms[index, 1] + PlatformThickness / 2f;

        /// <summary>
        /// 원본 `groundYBelow(x, fromY)`(project_test.html:2181) — x 위치에서 **fromY 이하**(4px 여유)에 있는 가장 높은
        /// 발판 윗면, 없으면 바닥. 발판 좌우 판정은 원본처럼 6px씩 넉넉하다(`pl.x - 6 … pl.x + pl.w + 6`).
        /// 원본은 Y+가 아래라 부등호가 전부 뒤집혀 있다(`pl.y >= fromY - 4 && pl.y < gy`).
        /// 마법사 순간이동 착지와 폭발탄 착탄이 쓴다 — 콜라이더가 아니라 이 데이터로 재는 게 원본과 같다.
        /// </summary>
        public static float SurfaceBelow(float x, float fromY)
        {
            float best = Core.FieldBounds.GroundY;
            var p = Platforms;
            for (int i = 0; i < p.GetLength(0); i++)
            {
                float top = PlatformTopY(i);
                if (x > PlatformLeftX(i) - SurfaceEdgeSlack && x < PlatformRightX(i) + SurfaceEdgeSlack
                    && top <= fromY + SurfaceFromSlack && top > best)
                    best = top;
            }
            return best;
        }

        const float SurfaceEdgeSlack = 0.06f; // 원본 6px
        const float SurfaceFromSlack = 0.04f; // 원본 `fromY - 4`

        // ── 천장 — 원본 캔버스 위쪽 한계 ──
        // 원본은 마법탄·중력탄이 `y < 64px`, 순간이동 도착이 `toY >= 120px`에서 막힌다(:3628·:3640·:2143). 둘 다 원본
        // 맨 위 발판(y=185px)보다 각각 1.21·0.65 위다. 우리는 발판 층간을 1.35로 넓혀서(의도적 편차) 원본 절댓값
        // (바닥 위 5.56·5.00)을 그대로 쓰면 천장이 우리 맨 위 발판 윗면(5.475)보다 낮아진다 — 그래서 **맨 위 발판
        // 기준 거리**를 옮겼다. 보스 무대도 원본은 같은 캔버스라 같은 값을 쓴다.
        static readonly float NormalTopSurfaceY = NormalPlatforms[12, 1] + PlatformThickness / 2f; // 4층 = 5.475
        /// <summary>마법탄·중력탄 천장(원본 `y < 64`).</summary>
        public static readonly float ProjectileCeilingY = NormalTopSurfaceY + 1.21f;
        /// <summary>순간이동 도착 천장(원본 `toY >= 120`) — 발 높이 기준.</summary>
        public static readonly float TeleportCeilingY = NormalTopSurfaceY + 0.65f;
    }
}
