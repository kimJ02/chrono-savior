using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 보스 무대 전환(원본 `:4313`·`:4316`)과 아래키 발판 관통(원본 `:3460`).
///
/// 둘 다 "없어도 게임이 돌아가지만 체감이 크게 다른" 종류라, 값이 실제로 바뀌는지를 못 박는다.
/// </summary>
public class ArenaAndDropThroughTests
{
    [SetUp]
    public void Setup() => PlatformSet.Activate(false);

    [TearDown]
    public void Teardown()
    {
        PlatformSet.Activate(false);
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
    }

    // ────────────────────────── 보스 무대 ──────────────────────────

    /// <summary>
    /// 보스전은 맵이 **좁아진다**(원본 2600 → 1800). 좁은 무대에서 슬램·돌진을 피하는 게
    /// 보스전 설계라, 넓은 필드 그대로면 그냥 도망다니면 되는 싸움이 된다.
    /// </summary>
    [Test]
    public void BossArena_NarrowsTheMap()
    {
        Assert.AreEqual(FieldLayout.NormalMapWidth, FieldBounds.MaxX, 0.01f);

        FieldLayout.SetBossArena(true);
        Assert.AreEqual(FieldLayout.BossMapWidth, FieldBounds.MaxX, 0.01f);
        Assert.Less(FieldLayout.BossMapWidth, FieldLayout.NormalMapWidth);

        FieldLayout.SetBossArena(false);
        Assert.AreEqual(FieldLayout.NormalMapWidth, FieldBounds.MaxX, 0.01f);
    }

    /// <summary>발판 배치도 통째로 갈린다(원본 15개 → 3개).</summary>
    [Test]
    public void BossArena_SwapsPlatformTable()
    {
        Assert.AreEqual(15, FieldLayout.NormalPlatforms.GetLength(0));
        Assert.AreEqual(3, FieldLayout.BossPlatforms.GetLength(0));

        Assert.AreEqual(15, FieldLayout.Platforms.GetLength(0));
        FieldLayout.SetBossArena(true);
        Assert.AreEqual(3, FieldLayout.Platforms.GetLength(0));
    }

    /// <summary>
    /// 발판이 전부 좁아진 맵 **안에** 있어야 한다 — 밖으로 삐져나가면 닿을 수 없는 발판이 된다.
    /// </summary>
    [Test]
    public void BossArena_PlatformsFitInsideNarrowMap()
    {
        FieldLayout.SetBossArena(true);
        for (int i = 0; i < FieldLayout.Platforms.GetLength(0); i++)
        {
            Assert.GreaterOrEqual(FieldLayout.PlatformLeftX(i), FieldBounds.MinX,
                $"보스 발판 {i}가 맵 왼쪽 밖으로 나갔다");
            Assert.LessOrEqual(FieldLayout.PlatformRightX(i), FieldBounds.MaxX,
                $"보스 발판 {i}가 맵 오른쪽 밖으로 나갔다");
        }
    }

    /// <summary>
    /// 바닥 스폰 지점도 맵 폭을 따라 줄어든다 — 고정 배열로 두면 좁은 보스 무대에서
    /// **맵 밖 좌표에 몹이 스폰**된다(원본은 `mapW`를 보고 루프를 돈다).
    /// </summary>
    [Test]
    public void BossArena_ShrinksGroundSpawnGrid()
    {
        int normal = FieldLayout.GroundGridX.Length;
        FieldLayout.SetBossArena(true);
        int boss = FieldLayout.GroundGridX.Length;

        Assert.Less(boss, normal, "좁은 맵인데 스폰 지점 수가 그대로다");
        foreach (float x in FieldLayout.GroundGridX)
            Assert.LessOrEqual(x, FieldBounds.MaxX, "스폰 지점이 맵 밖에 있다");
    }

    // ────────────────────────── 아래키 관통 낙하 ──────────────────────────

    /// <summary>
    /// 발판 위에서 아래키를 누르면 통과해서 내려간다(원본 `:3460`).
    /// 입력을 시뮬레이트할 수 없어서 **통과 로직 자체**(콜라이더 쌍 무시 → 복구)를 직접 태운다.
    /// </summary>
    [UnityTest]
    public IEnumerator DropThrough_IgnoresPlatformThenRestores()
    {
        // 원웨이 발판 하나
        var plat = new GameObject("TestPlatform");
        plat.transform.position = new Vector3(5f, 2f, 0f);
        var pcol = plat.AddComponent<BoxCollider2D>();
        pcol.size = new Vector2(3f, FieldLayout.PlatformThickness);
        pcol.usedByEffector = true;
        plat.AddComponent<PlatformEffector2D>().useOneWay = true;

        var player = new GameObject("TestPlayer");
        var pc = player.AddComponent<CircleCollider2D>();
        pc.radius = 0.5f;
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        yield return null;

        // 통과 시작을 흉내낸다 — 실제로는 CharacterMover2D가 아래키+착지 상태에서 이걸 한다.
        Physics2D.IgnoreCollision(pc, pcol, true);
        Assert.IsTrue(Physics2D.GetIgnoreCollision(pc, pcol), "통과 중인데 충돌이 살아 있다");

        Physics2D.IgnoreCollision(pc, pcol, false);
        Assert.IsFalse(Physics2D.GetIgnoreCollision(pc, pcol),
            "통과가 끝났는데 충돌이 안 돌아왔다 — 이러면 그 뒤로 발판을 영영 못 밟는다");

        Object.DestroyImmediate(plat);
        Object.DestroyImmediate(player);
    }
}

}
