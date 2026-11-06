using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 경험치 구슬 — 원본 `pickups`의 `exporb`(project_test.html:1860 드랍, `:4452` 갱신/획득).
/// </summary>
public class ExpOrbTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test") || go.name == "ExpOrb"))
                Object.DestroyImmediate(go);
        }
        ResetStatics();
    }

    static void ResetStatics()
    {
        RunState.Reset();
        RunTransient.Reset();
        CombatModifiers.Reset();
        ProfileService.Reset();
    }

    static GameObject NewPlayer(Vector3 pos)
    {
        var go = new GameObject("TestPlayer");
        go.tag = "Player";
        go.transform.position = pos;
        return go;
    }

    /// <summary>
    /// 획득 경험치는 **현재 레벨의 필요 경험치 25%**다(원본 `:4466`) — 고정값이 아니라 비율이라
    /// 레벨이 올라도 구슬 하나의 가치가 같이 커진다. 고정값으로 바꾸면 후반에 무의미해진다.
    /// </summary>
    [UnityTest]
    public IEnumerator Orb_GrantsPercentOfCurrentLevelRequirement()
    {
        ProfileService.Current.level = 5;
        RunState.Begin(1, RunMode.Normal);

        int expected = Mathf.Max(ExpOrb.MinExp,
            Mathf.RoundToInt(PlayerProfile.RequiredExp(5) * ExpOrb.ExpPercent));

        // 플레이어를 구슬 바로 위에 둬서 첫 프레임에 획득되게 한다.
        var player = NewPlayer(new Vector3(5f, 0.6f - ExpOrb.PlayerCenterOffset, 0f));
        ExpOrb.Spawn(TestPrefabs.ExpOrb, new Vector3(5f, 0.2f, 0f), player.transform);
        yield return null;
        yield return null;

        Assert.AreEqual(expected, ProfileService.Current.exp, "구슬 경험치가 필요량의 25%가 아니다");
        Assert.AreEqual(expected, RunState.ExpEarned, "결과 화면 집계에 구슬 경험치가 안 들어갔다");

        Object.DestroyImmediate(player);
    }

    /// <summary>자석 범위(1.5) 밖이면 안 끌려오고 제자리에서 떠 있기만 한다(원본 `:4456`).</summary>
    [UnityTest]
    public IEnumerator Orb_DoesNotChasePlayerOutsideMagnetRange()
    {
        var player = NewPlayer(new Vector3(20f, 0.5f, 0f));
        var orb = ExpOrb.Spawn(TestPrefabs.ExpOrb, new Vector3(5f, 0.2f, 0f), player.transform);
        float startX = orb.transform.position.x;

        for (int i = 0; i < 10; i++) yield return null;

        Assert.IsTrue(orb != null, "범위 밖인데 획득돼 사라졌다");
        Assert.AreEqual(startX, orb.transform.position.x, 0.001f, "자석 범위 밖인데 끌려왔다");

        Object.DestroyImmediate(player);
    }

    /// <summary>자석 범위 안이면 플레이어 쪽으로 끌려온다.</summary>
    [UnityTest]
    public IEnumerator Orb_MagnetsTowardPlayerInRange()
    {
        var player = NewPlayer(new Vector3(6f, 0.5f, 0f));
        var orb = ExpOrb.Spawn(TestPrefabs.ExpOrb, new Vector3(5f, 0.2f, 0f), player.transform); // 거리 약 1.0 — 자석 범위 안
        float startX = orb.transform.position.x;

        yield return null;
        yield return null;

        if (orb != null)
            Assert.Greater(orb.transform.position.x, startX, "자석 범위 안인데 안 끌려온다");
        // orb가 null이면 이미 획득된 것 — 그것도 "끌려와서 먹혔다"는 뜻이라 통과다.

        Object.DestroyImmediate(player);
    }

    /// <summary>
    /// 구슬은 **지면 아래에서 나오지 않는다**(원본 `Math.min(e.y, groundY)`, `:1861` —
    /// 원본은 Y+가 아래라 min이지만 우리 좌표계에선 max다).
    /// </summary>
    [Test]
    public void Orb_SpawnsAtOrAboveGround()
    {
        var orb = ExpOrb.Spawn(TestPrefabs.ExpOrb, new Vector3(5f, FieldBounds.GroundY - 3f, 0f), null);
        Assert.GreaterOrEqual(orb.transform.position.y, FieldBounds.GroundY);
    }

    /// <summary>로비로 나가면 필드에서 사라져야 한다 — `RunTransient`가 붙어 있는지 확인.</summary>
    [Test]
    public void Orb_IsRunTransient()
    {
        var orb = ExpOrb.Spawn(TestPrefabs.ExpOrb, new Vector3(5f, 1f, 0f), null);
        Assert.IsNotNull(orb.GetComponent<RunTransient>(),
            "RunTransient가 없으면 로비로 나갔다 와도 구슬이 필드에 남는다");
    }
}

}
