using System;
using UnityEngine;

namespace ChronoSavior.Core
{
    /// <summary>
    /// "몹이 몹을 낳는" 요청을 전달하는 이벤트 버스. 원본 분열귀가 죽을 때 `spawnEnemyAt()`을 직접
    /// 부르는 것(project_test.html:1840)에 대응한다.
    ///
    /// **왜 이벤트인가**: 스포너는 `Systems`(3층)이고 몹은 `Enemies`(2층)다. `Systems.asmdef`가 이미
    /// `Enemies`를 참조하므로, 몹이 스포너를 직접 부르면 두 asmdef가 서로를 참조해 Unity가
    /// "circular assembly definition reference"로 **컴파일 자체를 거부한다**(스타일 문제가 아니라 빌드가 깨짐).
    /// 그래서 방향을 뒤집어 `Core`의 이벤트만 아래층이 부르게 한다 — `ISpawnProtectable`과 같은 구조다.
    ///
    /// 구독자(`Systems.EnemySpawner`)는 **반드시 `OnDisable`에서 구독을 해제**할 것. 정적 이벤트라
    /// 해제를 빠뜨리면 파괴된 오브젝트를 계속 참조해 `MissingReferenceException`이 난다.
    ///
    /// > 시그니처는 CLAUDE.md "낮은 층이 높은 층의 기능을 요청" 항목에서 미리 확정해둔 것을 따랐다.
    /// > 다만 식별자는 확정 당시의 `string` 대신 <see cref="EnemyType"/> enum을 쓴다 — 그 사이
    /// > `Core/EnemyData`가 생기면서 두 도메인이 같은 enum을 이미 공유하게 됐고, 문자열 키보다
    /// > 오타에 안전하다. 의존 방향을 뒤집는다는 원래 의도는 그대로다.
    /// </summary>
    public static class EnemySpawnRequestBus
    {
        /// <summary>
        /// (스폰 위치, 몹 종류, 스폰 보호 시간). 스포너가 구독해서 실제 `Instantiate`를 담당한다.
        ///
        /// 보호 시간이 인자에 있는 이유: 원본 `spawnEnemyAt`의 `opts.protect`가 호출자마다 다르다 —
        /// 분열귀 새끼는 0.35초(`:1842`, 죽은 자리에서 바로 나오는데 2초면 손을 못 댄다),
        /// 보스 부하는 지정이 없어 **기본 2초**(`:4278` → `:3965`)다. 한 값으로 고정하면 둘 중
        /// 하나는 반드시 원본과 달라진다.
        /// </summary>
        public static event Action<Vector2, EnemyType, float> Requested;

        /// <summary>원본 `CONFIG.run.spawnProtect`(project_test.html:695) — `opts.protect` 미지정 시의 기본값.</summary>
        public const float DefaultSpawnProtect = 2f;

        public static void Request(Vector2 position, EnemyType enemyType, float spawnProtectSeconds) =>
            Requested?.Invoke(position, enemyType, spawnProtectSeconds);

        /// <summary>테스트 격리용 — 정적 이벤트라 구독이 남으면 테스트끼리 오염된다.</summary>
        public static void Reset() => Requested = null;
    }
}
