using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 전투 HUD 미니맵(`CombatMinimapView`, 2026-11-06 정리 순서 5-4에서 HUD에서 떼어 냄)의 움직이는 부분 —
/// 포탈 점은 자리가 있을 때만, 몹 점은 넘긴 수만큼, 무대(일반·보스)가 바뀌면 발판 선을 다시 그린다, 전투 씬이 없으면 지난 몹 점을 지운다.
/// (그려진 모양 자체는 UI 캡처가 기준 그림과 비교한다.)
/// </summary>
public class CombatMinimapViewTests
{
    GameObject parent;
    bool arenaBefore;

    [SetUp]
    public void Setup()
    {
        arenaBefore = FieldLayout.IsBossArena;
        FieldLayout.SetBossArena(false);
        parent = new GameObject("TestMinimapParent", typeof(RectTransform));
    }

    [TearDown]
    public void Teardown()
    {
        if (parent != null) Object.DestroyImmediate(parent);
        FieldLayout.SetBossArena(arenaBefore);
    }

    int Shown(string name) =>
        parent.GetComponentsInChildren<Image>(true).Count(i => i.name == name && i.enabled);

    [Test]
    public void Minimap_DrawsWhatItIsGiven_AndRedrawsPlatformsWhenTheArenaChanges()
    {
        var map = new CombatMinimapView(parent.transform, new Rect(0f, 0f, 128f, 64f));

        map.Refresh(new Vector2(3f, 0f), new[] { new Vector2(5f, 0f), new Vector2(9f, 1f) }, new Vector2(20f, 0f));
        Assert.AreEqual(2, Shown("Enemy"), "넘긴 몹 수만큼 점");
        Assert.AreEqual(1, Shown("Portal"), "포탈 자리가 있으면 점");
        Assert.AreEqual(FieldLayout.Platforms.GetLength(0) + 1, map.PlatformLineCount, "발판마다 한 줄 + 땅");

        map.Refresh(new Vector2(3f, 0f), new[] { new Vector2(5f, 0f) }, null);
        Assert.AreEqual(1, Shown("Enemy"), "몹이 줄면 남는 점은 끈다");
        Assert.AreEqual(0, Shown("Portal"), "포탈 자리가 없으면 점도 없다");

        FieldLayout.SetBossArena(true);
        map.Refresh(Vector2.zero, new Vector2[0], null);
        Assert.AreEqual(FieldLayout.Platforms.GetLength(0) + 1, map.PlatformLineCount, "보스 무대로 바뀌면 그 발판으로 다시 그린다");
    }

    [Test]
    public void Minimap_WithoutACombatScene_ShowsNoEnemies()
    {
        var map = new CombatMinimapView(parent.transform, new Rect(0f, 0f, 128f, 64f));
        map.Refresh(Vector2.zero, new[] { new Vector2(5f, 0f) }, null);
        Assert.AreEqual(1, Shown("Enemy"));

        // 전역 등록(WorldView)은 건드리지 않는다 — 지우면 다른 테스트의 월드 상태가 깨진다. 전투 씬이 떠 있으면 이 경우를 볼 수 없어 건너뛴다.
        Assume.That(WorldView.Combat, Is.Null, "전투 씬이 없을 때의 동작이라 전투 창구가 비어 있어야 한다");
        map.ForgetEnemies();
        map.Refresh(Vector2.zero, null, null); // null = 전투 창구에서 읽기 — 전투 씬이 없다
        Assert.AreEqual(0, Shown("Enemy"), "전투 씬이 없으면 지난 몹 점을 남기지 않는다");
    }
}

}
