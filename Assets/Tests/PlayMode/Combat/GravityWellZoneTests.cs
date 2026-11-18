using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 중력점 목록(`GravityWellZone.Active`)은 밖에선 읽기만 하고(2026-11-05 씬 분할 2차 2b), 없앨 땐 `Vanish`가 **같은 프레임 안에** 목록에서 뺀다 —
/// `Destroy`는 프레임 끝이라, 그 전에 같은 프레임의 다음 검색(대붕괴·중첩)이 사라질 중력점을 또 찾지 않게.
/// </summary>
public class GravityWellZoneTests
{
    [SetUp]
    public void Setup() { GravityWellZone.ClearAllForTests(); ProfileService.Reset(); }

    [TearDown]
    public void Teardown() { GravityWellZone.ClearAllForTests(); ProfileService.Reset(); }

    [Test]
    public void Vanish_LeavesTheListImmediately()
    {
        var well = GravityWellZone.SpawnOrMerge(new Vector2(5f, 1f), tier: 1, chargeK: 0f);
        Assert.AreEqual(1, GravityWellZone.Active.Count);

        well.Vanish();
        Assert.AreEqual(0, GravityWellZone.Active.Count, "같은 프레임 안에 목록에서 빠져야 한다(Destroy는 프레임 끝)");
    }

    [Test]
    public void ActiveList_IsReadOnlyFromOutside()
    {
        Assert.IsFalse(typeof(System.Collections.Generic.List<GravityWellZone>).IsAssignableFrom(
            typeof(GravityWellZone).GetProperty(nameof(GravityWellZone.Active)).PropertyType), "밖에 가변 List로 열려 있다");
    }

    /// <summary>
    /// 2026-11-18 사용자 "보스들은 중력법사의 중력장 같은 끌어오기나 스턴 등 이동이 제한되는 효과는
    /// 안 받게 하고 싶다" — <see cref="ICrowdControlImmune"/>를 가진 대상은 끌어당김(위치 이동)만
    /// 걸러진다. 일반 몹은 그대로 끌려온다는 것도 같이 확인해 둔다(필터가 전부를 막아 버리는 실수 방지).
    /// </summary>
    [UnityTest]
    public IEnumerator Pull_SkipsCrowdControlImmuneTargets_ButStillPullsNormalEnemies()
    {
        var well = GravityWellZone.SpawnOrMerge(new Vector2(0f, 1f), tier: 1, chargeK: 1f);
        Assert.IsNotNull(well);

        var normal = NewPullable(new Vector2(1f, 1f), immune: false);
        var boss = NewPullable(new Vector2(-1f, 1f), immune: true);
        float normalStartX = normal.position.x;
        float bossStartX = boss.position.x;

        for (int i = 0; i < 6; i++) yield return null; // Update()가 몇 프레임 돌 시간

        Assert.AreNotEqual(normalStartX, normal.position.x, "일반 몹은 끌려와야 한다(필터가 전부를 막으면 안 된다)");
        Assert.AreEqual(bossStartX, boss.position.x, 0.0001f, "ICrowdControlImmune 대상은 끌려오면 안 된다");
    }

    static Transform NewPullable(Vector2 pos, bool immune)
    {
        var go = new GameObject(immune ? "TestBoss" : "TestEnemy");
        go.SetActive(false);
        go.tag = "Enemy";
        var move = go.AddComponent<EnemyMove>();
        move.moveSpeed = 0f; // 기본 배회 AI가 X를 같이 움직이면 "끌려서 움직였나"를 못 가린다 — 끌어당김만 보려고 0으로.
        if (immune) go.AddComponent<ImmuneMarker>();
        go.transform.position = pos;
        go.SetActive(true);
        move.SetSpawnProtection(0f); // Awake 이후에 걸어야 한다 — 그 전에 걸면 Awake가 기본값(2초)으로 다시 덮어쓴다.
        return go.transform;
    }

    class ImmuneMarker : MonoBehaviour, ICrowdControlImmune { }
}

}
