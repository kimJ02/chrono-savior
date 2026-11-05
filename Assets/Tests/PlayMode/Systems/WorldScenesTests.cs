using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 실제 씬 로더(`Systems/SceneWorldScenes`, 2026-11-05 씬 분할 2차 3-3) — 진짜 씬 파일로 불러오기·내리기·멈추기·활성 씬을 본다.
/// 특히 **같은 이름의 씬을 둘 불러와도 핸들이 각자 자기 씬을 가리키는지**(재도전), 멈췄다 다시 켤 때 원래 꺼져 있던 루트는 그대로인지.
/// </summary>
public class WorldScenesTests
{
    const string Lobby = "10_Lobby";

    [SetUp]
    public void Setup()
    {
        ProfileService.Reset();
        WorldView.Reset();
        FieldLayout.SetBossArena(false);
    }

    [UnityTearDown]
    public IEnumerator Teardown()
    {
        for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s.name == Lobby && s.isLoaded) yield return SceneManager.UnloadSceneAsync(s);
        }
        WorldView.Reset();
    }

    static IEnumerator Wait(WorldSceneOp op)
    {
        float until = Time.realtimeSinceStartup + 10f;
        while (!op.IsDone && Time.realtimeSinceStartup < until) yield return null;
        Assert.IsTrue(op.IsDone, "10초 안에 안 끝났다");
    }

    [UnityTest]
    public IEnumerator SameSceneLoadedTwice_HandlesPointAtTheirOwnScene()
    {
        var loader = new SceneWorldScenes();
        var a = loader.Load(Lobby);
        yield return Wait(a);
        Assert.IsTrue(a.Succeeded, a.Error);
        loader.SetRunning(a.World, false); // 재도전처럼 — 옛 월드를 멈추고 같은 씬을 하나 더
        var b = loader.Load(Lobby);
        yield return Wait(b);
        Assert.IsTrue(b.Succeeded, b.Error);
        Assert.AreNotEqual(a.World.Id, b.World.Id, "같은 이름이어도 핸들이 다르다");
        Assert.IsTrue(loader.TryGetScene(a.World, out var sa));
        Assert.IsTrue(loader.TryGetScene(b.World, out var sb));
        Assert.AreNotEqual(sa, sb, "핸들이 각자 다른 씬을 가리킨다");

        loader.Activate(b.World);
        Assert.AreEqual(sb, SceneManager.GetActiveScene(), "새 월드가 활성 씬");

        var u = loader.Unload(a.World);
        yield return Wait(u);
        Assert.IsFalse(sa.isLoaded, "옛 핸들의 씬만 내려간다");
        Assert.IsTrue(sb.isLoaded, "새 월드는 그대로");
    }

    [UnityTest]
    public IEnumerator Pause_ThenResume_RestoresOnlyRootsThatWereOn()
    {
        var loader = new SceneWorldScenes();
        var op = loader.Load(Lobby);
        yield return Wait(op);
        loader.TryGetScene(op.World, out var scene);
        var roots = scene.GetRootGameObjects();
        var offOnPurpose = roots[0];
        offOnPurpose.SetActive(false); // 원래 꺼져 있던 루트

        loader.SetRunning(op.World, false);
        Assert.IsTrue(roots.All(r => !r.activeSelf), "멈추면 루트가 전부 꺼진다");
        Assert.IsNull(WorldView.Lobby, "꺼지면 로비 창구에서 빠진다(OnDisable)");

        loader.SetRunning(op.World, true);
        Assert.IsFalse(offOnPurpose.activeSelf, "원래 꺼져 있던 루트는 다시 켜지 않는다");
        Assert.IsTrue(roots.Skip(1).All(r => r.activeSelf), "켜져 있던 루트는 다시 켜진다");
    }

    [Test]
    public void LoadingAMissingScene_FailsWithoutThrowing()
    {
        var op = new SceneWorldScenes().Load("NoSuchScene");
        Assert.IsTrue(op.IsDone);
        Assert.IsFalse(op.Succeeded);
        StringAssert.Contains("NoSuchScene", op.Error);
    }
}

}
