using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// Part B(몬스터 스폰/이동) 관련 PlayMode 테스트. 필드 경계 API 전환 + 이동축 수정(X만 이동) +
/// 원본과 비교해 다시 이식한 것들(스폰 포인트를 균등 랜덤이 아니라 발판/바닥그리드 기반으로,
/// 스폰 직후 2초 무적)이 실제로 맞물려 동작하는지 검증한다.
/// </summary>
public class EnemySpawnAndMoveTests
{
    /// <summary>
    /// 웨이브 하나를 강제로 발생시켜, 스폰된 몹이 전부 필드 X 경계 안 + "알려진 착지 Y" 중
    /// 하나(바닥 또는 15개 발판 중 하나의 착지 높이)에 있는지 확인한다. 전부 GroundY였던 이전
    /// 가정과 달리 이제 발판 스폰 포인트는 그 발판 실제 높이를 쓴다(사용자 지적으로 수정됨).
    /// waveTimer를 기다리지 않고 private SpawnWave()를 리플렉션으로 직접 호출해 테스트를 빠르게 한다.
    /// </summary>
    [UnityTest]
    public IEnumerator SpawnWave_PlacesMonstersWithinFieldBounds_OnKnownLandingHeights()
    {
        var prefab = new GameObject("TestMonsterPrefab");
        prefab.tag = "Enemy";
        prefab.AddComponent<CircleCollider2D>().radius = 0.3f;

        var spawnerGO = new GameObject("TestSpawner");
        var spawner = spawnerGO.AddComponent<EnemySpawner>();
        spawner.monsterPrefab = prefab;
        spawner.maxSpawnPerWave = 5;
        // 엘리트는 몸집이 1.35배라 착지 높이가 그만큼 올라간다 — 이 테스트가 보려는 건
        // "스폰 포인트가 발판/바닥 높이에 정확히 맞는가"라 그 변수를 뺀다(엘리트는 전용 테스트에서 확인).
        spawner.eliteChanceOverride = 0f;

        var spawnWave = typeof(EnemySpawner).GetMethod("SpawnWave", BindingFlags.NonPublic | BindingFlags.Instance);
        var aliveField = typeof(EnemySpawner).GetField("aliveMonsters", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(spawnWave);
        Assert.IsNotNull(aliveField);

        // 21개 스폰 포인트 중 일부만 걸리는 걸 방지하려고 여러 웨이브를 돌려 발판 높이도 확실히 포함시킨다.
        for (int wave = 0; wave < 6; wave++) spawnWave.Invoke(spawner, null);
        yield return null;

        // 스포너가 직접 관리하는 목록으로 확인한다 — 태그 전역 검색은 프리팹 템플릿(비활성)
        // 자체와 실제 스폰된 인스턴스를 혼동하기 쉬워서(Instantiate는 활성 상태를 그대로 복사) 피한다.
        var alive = (System.Collections.IList)aliveField.GetValue(spawner);
        Assert.Greater(alive.Count, 0, "웨이브를 발생시켰는데 스폰된 몹이 없다");

        const float radius = 0.3f;
        var knownY = new System.Collections.Generic.List<float> { FieldBounds.GroundY + radius };
        for (int i = 0; i < FieldLayout.Platforms.GetLength(0); i++)
            knownY.Add(FieldLayout.Platforms[i, 1] + FieldLayout.PlatformThickness / 2f + radius);

        bool sawPlatformHeight = false;
        foreach (Transform t in alive)
        {
            Assert.GreaterOrEqual(t.position.x, FieldBounds.MinX - 0.01f, "필드 왼쪽 경계 밖에 스폰됨");
            Assert.LessOrEqual(t.position.x, FieldBounds.MaxX + 0.01f, "필드 오른쪽 경계 밖에 스폰됨");

            bool matchesKnownHeight = knownY.Exists(y => Mathf.Abs(y - t.position.y) < 0.01f);
            Assert.IsTrue(matchesKnownHeight, $"y={t.position.y}가 바닥/발판 착지 높이 어디와도 안 맞음");
            if (Mathf.Abs(t.position.y - (FieldBounds.GroundY + radius)) > 0.01f) sawPlatformHeight = true;

            Object.Destroy(t.gameObject);
        }
        Assert.IsTrue(sawPlatformHeight, "6웨이브를 돌렸는데 발판 높이에 스폰된 몹이 하나도 없다 — 발판 스폰이 실제로 동작하는지 확인 필요");

        Object.Destroy(prefab);
        Object.Destroy(spawnerGO);
        yield return null;
    }

    /// <summary>
    /// 스포너가 만든 몹은 쫓을 플레이어를 받아서 나온다(2026-11-05 2b — 예전엔 몹이 Player 태그로 스스로 찾았다). 잡몹 웨이브로 확인한다
    /// (꼬마 슬라임·성소·보스도 같은 `SpawnMonsterAt`을 지난다).
    /// </summary>
    [UnityTest]
    public IEnumerator SpawnedMonsters_AreGivenThePlayerToChase()
    {
        var player = new GameObject("TestPlayer").transform;
        var prefab = new GameObject("TestMonsterPrefab");
        prefab.tag = "Enemy";
        prefab.AddComponent<CircleCollider2D>().radius = 0.3f;
        prefab.AddComponent<EnemyMove>();
        prefab.GetComponent<Rigidbody2D>().gravityScale = 0f;

        var spawner = new GameObject("TestSpawner").AddComponent<EnemySpawner>();
        spawner.monsterPrefab = prefab;
        spawner.Player = player;
        spawner.eliteChanceOverride = 0f;
        typeof(EnemySpawner).GetMethod("SpawnWave", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(spawner, null);
        yield return null;

        var alive = (System.Collections.IList)typeof(EnemySpawner).GetField("aliveMonsters", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(spawner);
        Assert.Greater(alive.Count, 0);
        foreach (Transform t in alive)
        {
            Assert.AreSame(player, t.GetComponent<EnemyMove>().Target, "스포너가 만든 몹이 쫓을 플레이어를 못 받았다");
            Object.Destroy(t.gameObject);
        }
        Object.Destroy(prefab);
        Object.Destroy(spawner.gameObject);
        Object.Destroy(player.gameObject);
        yield return null;
    }

    /// <summary>
    /// 원본 고블린 AI: 플레이어가 추적범위(3유닛) 안이면 그 방향으로 dir을 맞춘다. 몹의 기본
    /// dir은 1(오른쪽)인데, 플레이어를 일부러 "왼쪽"에 둬서(추적범위 안) 실제로 방향이 반전되는지
    /// 확인한다 — 그냥 기본값이 우연히 맞아떨어지는 걸 통과로 착각하지 않기 위한 설계.
    /// Y는 건드리지 않는지도 같이 본다(중력/착지는 별도 테스트가 검증하므로 여기선
    /// gravityScale=0으로 물리 낙하를 배제하고 추적 판단 로직만 격리한다). 플레이어를 몹보다
    /// 높은 위치(발판 위 시뮬레이션)에 둬서, 예전 버그(X·Y 둘 다 추적)라면 몹이 위로 떠오르는 걸
    /// 바로 잡아낼 수 있게 했다.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyMove_ChasesTowardPlayerWithinAggroRange_XOnly()
    {
        var playerGO = new GameObject("TestPlayer");
        playerGO.tag = "Player";
        playerGO.transform.position = new Vector3(0f, 5f, 0f); // 몹(x=2)보다 왼쪽 + 훨씬 위(발판 위 상황)

        var monsterGO = new GameObject("TestMover");
        monsterGO.transform.position = new Vector3(2f, FieldBounds.GroundY, 0f); // 플레이어까지 거리 2 < 기본 추적범위(3)
        var move = monsterGO.AddComponent<EnemyMove>(); // RequireComponent로 Rigidbody2D도 같이 붙음
        move.moveSpeed = 5f; // 테스트를 빨리 끝내려고 크게
        move.Target = playerGO.transform; // 실제 게임에선 스포너가 넣는다(2026-11-05 2b). 안 넣으면 배회 방향(무작위)이 우연히 맞아 통과할 수 있다
        monsterGO.GetComponent<Rigidbody2D>().gravityScale = 0f; // 이 테스트는 추적 판단만 봄, 낙하/착지는 별도 테스트
        // 스폰 보호(2초) 중엔 원본처럼 아예 안 움직이므로, 추적 판단을 보려면 무적을 먼저 풀어야 한다.
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(move, 0f);

        float startY = monsterGO.transform.position.y;
        float startX = monsterGO.transform.position.x;

        yield return new WaitForSeconds(0.3f);

        Assert.AreEqual(startY, monsterGO.transform.position.y, 0.001f, "몹이 Y축으로 움직였다(발판 위 플레이어를 공중에서 쫓아감)");
        Assert.Less(monsterGO.transform.position.x, startX, "몹의 기본 방향(오른쪽)을 그대로 유지함 — 왼쪽에 있는 플레이어를 추적하지 않음");

        Object.Destroy(playerGO);
        Object.Destroy(monsterGO);
        yield return null;
    }

    /// <summary>
    /// 몹을 발판 위에서 살짝 위쪽에 놓고 떨어뜨렸을 때 실제 Physics2D로 그 발판 위에 착지해서
    /// 멈추는지 확인한다 — "발판 위에도 스폰된다"는 게 실제로 물리적으로 성립하는지의 핵심 검증.
    /// 예전엔 EnemyMove가 순수 transform 이동이라 발판 콜라이더를 그냥 통과했다.
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyMove_LandsOnPlatform_ViaRealPhysics()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.AreNotEqual(-1, groundLayer, "Ground 레이어가 없음 — BuildPartAScene.Build()를 한 번도 안 돌린 프로젝트인가?");

        var platformGO = new GameObject("TestPlatform");
        platformGO.layer = groundLayer;
        platformGO.transform.position = new Vector3(5f, 3f, 0f);
        var col = platformGO.AddComponent<BoxCollider2D>();
        col.size = new Vector2(3f, 0.15f);
        col.usedByEffector = true;
        platformGO.AddComponent<PlatformEffector2D>().useOneWay = true;

        var monsterGO = new GameObject("TestLandingMonster");
        monsterGO.tag = "Enemy";
        monsterGO.transform.position = new Vector3(5f, 4f, 0f); // 발판 한참 위에서 떨어뜨림
        monsterGO.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = monsterGO.AddComponent<EnemyMove>(); // RequireComponent로 Rigidbody2D도 같이 붙음(중력 적용)
        // 이 발판은 FieldLayout 좌표와 안 맞아서 가장자리 반전이 안 걸린다 — 순수 착지 물리만
        // 보려고 배회 이동(수평 드리프트)을 꺼서 대기 시간 동안 발판 밖으로 안 나가게 한다.
        move.moveSpeed = 0f;

        float t = 0f;
        while (t < 2f)
        {
            yield return new WaitForFixedUpdate();
            t += Time.fixedDeltaTime;
        }

        float expectedY = 3f + 0.075f + 0.3f; // 발판 윗면(중심+두께/2) + 몹 반지름
        Assert.AreEqual(expectedY, monsterGO.transform.position.y, 0.05f, "발판 위에 착지하지 않았다(뚫고 떨어졌거나 공중에 떠 있음)");

        Object.Destroy(monsterGO);
        Object.Destroy(platformGO);
        yield return null;
    }

    /// <summary>
    /// 원본: "발판 위 몹은 가장자리에서 되돌아간다 — 내려오지 않는다." 실제 FieldLayout 발판(0번)
    /// 오른쪽 끝 바로 안쪽에서 시작해 기본 방향(오른쪽)으로 걷게 두면, 가장자리에서 멈추고
    /// 반전해서 계속 그 발판 위에 남아있어야 한다(걸어서 떨어지면 안 됨).
    /// </summary>
    [UnityTest]
    public IEnumerator EnemyMove_TurnsBackAtPlatformEdge_DoesNotWalkOff()
    {
        int groundLayer = LayerMask.NameToLayer("Ground");
        Assert.AreNotEqual(-1, groundLayer, "Ground 레이어가 없음");

        float cx = FieldLayout.Platforms[0, 0];
        float cy = FieldLayout.Platforms[0, 1];
        float w = FieldLayout.Platforms[0, 2];

        var platformGO = new GameObject("TestPlatform0");
        platformGO.layer = groundLayer;
        platformGO.transform.position = new Vector3(cx, cy, 0f);
        var pcol = platformGO.AddComponent<BoxCollider2D>();
        pcol.size = new Vector2(w, FieldLayout.PlatformThickness);
        pcol.usedByEffector = true;
        platformGO.AddComponent<PlatformEffector2D>().useOneWay = true;

        const float radius = 0.3f;
        float landingY = FieldLayout.PlatformLandingY(0, radius);
        float rightEdgeX = FieldLayout.PlatformRightX(0);

        var monsterGO = new GameObject("TestEdgeMonster");
        monsterGO.tag = "Enemy";
        monsterGO.transform.position = new Vector3(rightEdgeX - 0.2f, landingY, 0f); // 이미 발판 위, 오른쪽 끝 근처
        monsterGO.AddComponent<CircleCollider2D>().radius = radius;
        var edgeMove = monsterGO.AddComponent<EnemyMove>(); // 플레이어 없음 → 배회, 기본 dir=1(오른쪽)이라 곧 가장자리에 닿음
        // 스폰 보호 중엔 움직이지 않으므로(원본 동일) 가장자리 반전을 보려면 무적을 풀어야 한다.
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(edgeMove, 0f);

        float minY = monsterGO.transform.position.y;
        float t = 0f;
        while (t < 1.5f)
        {
            yield return new WaitForFixedUpdate();
            minY = Mathf.Min(minY, monsterGO.transform.position.y);
            t += Time.fixedDeltaTime;
        }

        Assert.AreEqual(landingY, monsterGO.transform.position.y, 0.05f, "발판 아래로 떨어졌다(가장자리 반전이 안 먹힘)");
        Assert.Greater(minY, landingY - 0.1f, "도중에 발판 아래로 떨어졌었다");
        Assert.LessOrEqual(monsterGO.transform.position.x, rightEdgeX, "발판 오른쪽 끝을 넘어갔다");
        Assert.GreaterOrEqual(monsterGO.transform.position.x, FieldLayout.PlatformLeftX(0), "발판 왼쪽 끝을 넘어갔다");

        Object.Destroy(monsterGO);
        Object.Destroy(platformGO);
        yield return null;
    }

    /// <summary>
    /// 스폰 X좌표가 필드 전체 균등 랜덤이 아니라, 원본처럼 발판 중심/바닥그리드 지점(FieldLayout)
    /// 중 하나 근처(그 지점의 폭 안)에만 찍히는지 확인한다. 여러 번 스폰해서 전부 어떤 스폰
    /// 포인트의 창 안에 들어가는지 검사 — 하나라도 창 밖이면 "아무 데서나" 스폰되는 셈이라 실패.
    /// </summary>
    [UnityTest]
    public IEnumerator SpawnWave_PicksXFromKnownSpawnPoints_NotUniformRandom()
    {
        var prefab = new GameObject("TestMonsterPrefab");
        prefab.tag = "Enemy";
        prefab.AddComponent<CircleCollider2D>().radius = 0.3f;

        var spawnerGO = new GameObject("TestSpawner");
        var spawner = spawnerGO.AddComponent<EnemySpawner>();
        spawner.monsterPrefab = prefab;
        spawner.maxSpawnPerWave = 7;
        spawner.minSpacing = 0f; // 이 테스트는 분포만 보는 거라 간격 재시도로 흔들리지 않게

        var spawnWave = typeof(EnemySpawner).GetMethod("SpawnWave", BindingFlags.NonPublic | BindingFlags.Instance);
        var aliveField = typeof(EnemySpawner).GetField("aliveMonsters", BindingFlags.NonPublic | BindingFlags.Instance);

        // 여러 웨이브를 돌려서 스폰 포인트 21개 중 일부만 우연히 걸리는 걸 방지
        for (int wave = 0; wave < 5; wave++)
        {
            spawnWave.Invoke(spawner, null);
        }
        yield return null;

        var alive = (System.Collections.IList)aliveField.GetValue(spawner);
        Assert.Greater(alive.Count, 0, "스폰된 몹이 없다");

        const float margin = 0.26f;
        foreach (Transform t in alive)
        {
            bool inWindow = false;
            for (int i = 0; i < FieldLayout.Platforms.GetLength(0) && !inWindow; i++)
            {
                float cx = FieldLayout.Platforms[i, 0];
                float halfSpan = Mathf.Max(0f, FieldLayout.Platforms[i, 2] / 2f - margin) + 0.02f;
                if (Mathf.Abs(t.position.x - cx) <= halfSpan) inWindow = true;
            }
            for (int i = 0; i < FieldLayout.GroundGridX.Length && !inWindow; i++)
            {
                float halfSpan = Mathf.Max(0f, FieldLayout.GroundGridPointWidth / 2f - margin) + 0.02f;
                if (Mathf.Abs(t.position.x - FieldLayout.GroundGridX[i]) <= halfSpan) inWindow = true;
            }
            Assert.IsTrue(inWindow, $"x={t.position.x}가 어떤 스폰 포인트 창에도 안 들어감 — 균등 랜덤으로 회귀한 것 아닌지 확인");
            Object.Destroy(t.gameObject);
        }

        Object.Destroy(prefab);
        Object.Destroy(spawnerGO);
        yield return null;
    }

    /// <summary>
    /// 원본 spawnInvuln(스폰 직후 2초 무적) — 몹이 무적 상태를 내보이고(`ISpawnProtectable`), 시간이 지나면 풀린다.
    /// 공격마다 이 상태를 보고 건너뛰는 것은 각 공격의 테스트가 본다(예전엔 쓰지 않는 범용 근접 공격으로 봤다 — 2026-11-05 삭제).
    /// </summary>
    [UnityTest]
    public IEnumerator SpawnProtection_IsExposed_ThenExpires()
    {
        var enemyGO = new GameObject("TestProtectedEnemy");
        enemyGO.tag = "Enemy";
        enemyGO.AddComponent<CircleCollider2D>().radius = 0.3f;
        enemyGO.AddComponent<SpriteRenderer>();
        enemyGO.AddComponent<ChronoSavior.Enemies.EnemyHealth>();
        var move = enemyGO.AddComponent<EnemyMove>();
        move.SetSpawnProtection(0.1f); // 원본 값은 2초 — 테스트를 빨리 끝내려고 짧게(스포너가 쓰는 공개 경로)

        ISpawnProtectable protectable = enemyGO.GetComponent<ISpawnProtectable>();
        Assert.IsNotNull(protectable, "공격 스크립트는 이 인터페이스로 무적을 묻는다");
        Assert.IsTrue(protectable.IsSpawnProtected, "스폰 직후엔 무적");

        yield return new WaitForSeconds(0.2f); // 무적(0.1초) 만료 대기
        Assert.IsFalse(protectable.IsSpawnProtected, "시간이 지나면 풀린다");

        Object.Destroy(enemyGO);
        yield return null;
    }
}

}
