using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Systems
{
    /// <summary>
    /// 지역별 몬스터 해금표 — 원본 `rollSpawnType(region)`(project_test.html:3933~3939)과
    /// 그 바로 뒤의 대오니 예외(`:3944`)를 그대로 옮긴 것이다.
    ///
    /// **지역이 오를수록 강한 종이 확률표에 "추가"되는 구조**다. 새 종이 들어오면서 기존 종의
    /// 비중이 같이 줄어들기 때문에, 1지역에서 45%였던 도깨비불이 4지역 이상에선 24%가 된다.
    /// 확률을 임의로 반올림하거나 균등분포로 바꾸면 체감 난이도 곡선이 통째로 달라진다.
    ///
    /// 새끼(<see cref="EnemyType.Splitlet"/>)는 **이 표에 절대 들어가지 않는다** — 원본에서도
    /// 분열귀가 죽을 때만 나온다(`spawnEnemyAt(..., 'splitlet')`, `:1842`).
    /// </summary>
    public static class EnemySpawnTable
    {
        /// <summary>원본 `if (run.region >= 2 && lv >= 4 && Math.random() < 0.10) type = 'bigOni'`(:3944).</summary>
        public const float BigOniChance = 0.10f;
        /// <summary>대오니가 나오기 시작하는 지역. 원본은 여기에 `lv >= 4` 조건이 하나 더 붙는데,
        /// 몹 레벨이 `regionBaseLv(r) = (r-1)*3+1`(:972)라 2지역의 최소 레벨이 이미 4다 —
        /// 즉 지역 조건을 만족하면 레벨 조건은 항상 참이라 실질적으로 같은 조건이다.
        /// (몹 레벨은 원본에서 "페널티 전용"이고 우리 포트엔 아직 없어서, 없는 개념을
        /// 새로 들여오는 대신 등가인 지역 조건만 남겼다.)</summary>
        public const int BigOniMinRegion = 2;

        /// <summary>
        /// 이번에 스폰할 몹 종류를 굴린다. <paramref name="region"/>은 1부터.
        /// </summary>
        public static EnemyType Roll(int region)
        {
            if (region >= BigOniMinRegion && Random.value < BigOniChance) return EnemyType.BigOni;
            return RollBase(region, Random.value);
        }

        /// <summary>
        /// 대오니 예외를 뺀 기본 확률표. 난수를 인자로 받아서 테스트가 경계값을 직접 찍을 수 있게 했다.
        /// </summary>
        /// <param name="roll">0 이상 1 미만.</param>
        public static EnemyType RollBase(int region, float roll)
        {
            if (region <= 1)
                return roll < 0.45f ? EnemyType.Wisp : EnemyType.Oni;
            if (region == 2)
                return roll < 0.34f ? EnemyType.Wisp
                     : roll < 0.70f ? EnemyType.Oni
                     : EnemyType.Charger;
            if (region == 3)
                return roll < 0.28f ? EnemyType.Wisp
                     : roll < 0.56f ? EnemyType.Oni
                     : roll < 0.80f ? EnemyType.Charger
                     : EnemyType.Shooter;
            return roll < 0.24f ? EnemyType.Wisp
                 : roll < 0.48f ? EnemyType.Oni
                 : roll < 0.68f ? EnemyType.Charger
                 : roll < 0.85f ? EnemyType.Shooter
                 : EnemyType.Splitter;
        }
    }
}
