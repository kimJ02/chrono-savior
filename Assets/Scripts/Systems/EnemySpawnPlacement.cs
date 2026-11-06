using System.Collections.Generic;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Systems
{
/// <summary>
/// **웨이브의 몹 한 마리를 어디에 세울지** — 원본 `spawnWave()`의 자리 고르기(2026-11-06 정리 순서 5-3, 예전엔 `EnemySpawner` 안에 있었다).
///
/// 스폰 지점은 원본 `buildSpawnPoints()` 그대로다 — 필드 전체에서 균등 랜덤이 아니라 "발판 15개의 중심 X + 바닥 위 380px 간격 지점(6곳)"
/// 중 하나를 골라 그 지점 폭 안에서 살짝 흔든다(`rand(-pt.w/2+26, pt.w/2-26)`). **발판 지점은 실제로 그 발판 높이에 세운다**
/// (Y를 항상 GroundY로 고정했던 이전 버전은 "발판 위에도 나온다"는 말과 실제 동작이 달랐음 — 사용자가 직접 확인하고 지적).
/// 좌표의 단일 출처는 <see cref="FieldLayout"/>.
///
/// 같은 웨이브 안에서 이미 고른 자리와 너무 가까우면 다시 뽑는다 — **간격 체크는 이번 웨이브 안에서만** 한다
/// (원본 `const placed=[]`가 호출마다 새로 시작. 살아 있는 몹 전체와 비교하면 필드가 찰수록 원본보다 실패가 잦아진다).
/// 상태를 갖지 않는다 — 간격·재시도 횟수·몸 반지름은 스포너(씬 설정)가 넘긴다.
/// </summary>
public static class EnemySpawnPlacement
{
    /// <summary>
    /// 자리를 하나 고른다. <paramref name="retries"/>번 다시 뽑아도 간격을 못 맞추면 false(그 마리는 건너뛴다 — 원본과 같다).
    /// </summary>
    /// <param name="bodyRadius">기본 몹의 몸 반지름 — 발판 지점의 착지 높이(발판 윗면 + 반지름)를 계산한다.</param>
    public static bool TryPick(List<Vector2> placedThisWave, float bodyRadius, float minSpacing, int retries, out Vector2 result)
    {
        for (int attempt = 0; attempt < retries; attempt++)
        {
            Vector2 candidate = PickSpawnPoint(bodyRadius);
            candidate.x = FieldBounds.ClampX(candidate.x);

            if (IsFarEnoughFromPlaced(candidate, placedThisWave, minSpacing))
            {
                result = candidate;
                return true;
            }
        }

        result = default;
        return false;
    }

    /// <summary>
    /// 원본 spawnWave()의 `spawnPoints[randInt(...)]` + `rand(-pt.w/2+26, pt.w/2-26)`를 그대로 옮김.
    /// 발판 중심(15개)과 바닥 그리드(6개) 중 하나를 균등 랜덤으로 고른 뒤 그 지점 폭 안에서 흔든다.
    /// 발판 포인트는 **그 발판의 실제 착지 Y**(발판 윗면 + 몹 반지름)를 쓴다 — 몹이 물리로
    /// 그 위에 서 있게 된다. 바닥그리드 포인트는 FieldBounds.GroundY.
    /// </summary>
    static Vector2 PickSpawnPoint(float radius)
    {
        int platformCount = FieldLayout.Platforms.GetLength(0);
        int groundCount = FieldLayout.GroundGridX.Length;
        int idx = Random.Range(0, platformCount + groundCount);

        float centerX, centerY, width;
        if (idx < platformCount)
        {
            centerX = FieldLayout.Platforms[idx, 0];
            centerY = FieldLayout.PlatformLandingY(idx, radius);
            width = FieldLayout.Platforms[idx, 2];
        }
        else
        {
            centerX = FieldLayout.GroundGridX[idx - platformCount];
            centerY = FieldBounds.GroundY + radius;
            width = FieldLayout.GroundGridPointWidth;
        }

        const float margin = 0.26f; // 원본 26px 여백 ÷100
        float halfSpan = Mathf.Max(0f, width / 2f - margin);
        return new Vector2(centerX + Random.Range(-halfSpan, halfSpan), centerY);
    }

    static bool IsFarEnoughFromPlaced(Vector2 candidate, List<Vector2> placedThisWave, float minSpacing)
    {
        // 원본: `Math.abs(q.x-x)<52 && Math.abs(q.y-y)<12` — 둘 다 만족해야(AND) "너무 가깝다".
        // 발판 층마다 Y가 다르므로(이전엔 전부 GroundY라 Y조건이 항상 참이었음) 이제 Y차이가
        // 크면(다른 층) 겹쳐도 통과시킨다 — 원본과 동일.
        const float yThreshold = 0.12f; // 원본 12px ÷100
        foreach (var p in placedThisWave)
        {
            if (Mathf.Abs(p.x - candidate.x) < minSpacing && Mathf.Abs(p.y - candidate.y) < yThreshold)
                return false;
        }
        return true;
    }
}
}
