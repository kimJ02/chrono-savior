using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>섬영 기본 공격 베기 효과(Slash 1 · color4, 2026-10-25) — 휘두르면 나오고, 0.2초 뒤 사라진다.</summary>
public class BladeSlashVfxTests
{
    [SetUp]
    public void Setup()
    {
        ProfileService.Reset();
        GameState.Set(GameScene.Run);
        ProfileService.Current.character = CharacterId.Blade;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null && (go.name.StartsWith("Test") || go.name == "BladeSlash"))
                Object.DestroyImmediate(go);
        ProfileService.Reset();
    }

    [Test]
    public void FrameAt_WalksThroughAllFrames()
    {
        Assert.AreEqual(0, BladeSlashVfx.FrameAt(0f, 9));
        Assert.AreEqual(4, BladeSlashVfx.FrameAt(BladeSlashVfx.Life * 0.5f, 9));
        Assert.AreEqual(8, BladeSlashVfx.FrameAt(BladeSlashVfx.Life, 9), "끝을 넘어도 마지막 프레임");
    }

    [UnityTest]
    public IEnumerator Swing_SpawnsSlash_ThenItEnds()
    {
        var go = new GameObject("TestBlade");
        go.AddComponent<CircleCollider2D>();
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        go.AddComponent<PlayerHealth>();
        var blade = go.AddComponent<BladeCombat>();
        go.AddComponent<SpriteRenderer>();
        var vfx = go.AddComponent<BladeSlashVfx>().Equipped();
        yield return null;
        Assert.AreEqual(0, vfx.Count, "휘두르기 전엔 없다");

        typeof(BladeCombat).GetField("cdTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(blade, 0.13f);
        yield return null;
        Assert.AreEqual(1, vfx.Count, "휘두르면(쿨다운이 올라가면) 하나 나온다");

        yield return new WaitForSeconds(BladeSlashVfx.Life + 0.1f);
        Assert.AreEqual(0, vfx.Count, "끝나면 사라진다");
    }
}

}
