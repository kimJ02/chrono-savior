using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Combat;
using ChronoSavior.Core;

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
}

}
