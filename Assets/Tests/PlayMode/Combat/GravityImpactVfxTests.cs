using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Combat;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>블랙홀 마법사 착탄 폭발(2026-10-25) — 10프레임 15fps, 지름 = 반경 × 2, 끝나면 사라진다.</summary>
public class GravityImpactVfxTests
{
    [TearDown]
    public void Teardown()
    {
        foreach (var v in Object.FindObjectsByType<GravityImpactVfx>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
    }

    [Test]
    public void FrameAt_Is15Fps_AndClamps()
    {
        Assert.AreEqual(0, GravityImpactVfx.FrameAt(0f, 10));
        Assert.AreEqual(3, GravityImpactVfx.FrameAt(0.21f, 10));
        Assert.AreEqual(9, GravityImpactVfx.FrameAt(5f, 10));
    }

    [Test]
    public void Vortex_LoopsSwirlFrames_PullsInward_FadesAtEnds()
    {
        Assert.AreEqual(4, GravityVortexVfx.LoopFrameAt(0f, 10));
        Assert.AreEqual(5, GravityVortexVfx.LoopFrameAt(1.1f / GravityVortexVfx.Fps, 10));
        Assert.AreEqual(6, GravityVortexVfx.LoopFrameAt(2.1f / GravityVortexVfx.Fps, 10));
        Assert.AreEqual(4, GravityVortexVfx.LoopFrameAt(4.1f / GravityVortexVfx.Fps, 10), "한 바퀴 돌아 처음으로");
        Assert.AreEqual(1f, GravityVortexVfx.PullScale(0f), 1e-4f);
        Assert.Less(GravityVortexVfx.PullScale(0.8f), GravityVortexVfx.PullScale(0.1f), "시간이 갈수록 안으로 줄어든다");
        Assert.AreEqual(0f, GravityVortexVfx.AlphaAt(0f, 3f), "착탄 폭발이 보이는 동안엔 안 보인다");
        Assert.Greater(GravityVortexVfx.AlphaAt(2f, 3f), 0.5f, "중력장이 남아 있으면 계속 보인다");
        Assert.AreEqual(0f, GravityVortexVfx.AlphaAt(2f, 0f), "중력장이 끝나면 사라진다");
    }

    [UnityTest]
    public IEnumerator Well_HasVortex_ThatSurvivesTheImpactBurst_AndSizesToRadius()
    {
        var well = GravityWellZone.SpawnOrMerge(new Vector2(3f, 1f), 2, 0.6f);
        var vortex = well.GetComponentInChildren<GravityVortexVfx>();
        Assert.IsNotNull(vortex, "중력장에 소용돌이 그림이 붙는다");
        yield return new WaitForSeconds(GravityImpactVfx.Life + 0.2f);
        Assert.IsTrue(vortex != null && well != null, "폭발이 끝나도 중력장과 소용돌이는 남아 있다");
        Assert.Greater(vortex.GetComponent<SpriteRenderer>().color.a, 0.4f, "그리고 보인다");
        GravityWellZone.ClearAllForTests();
    }

    [UnityTest]
    public IEnumerator Spawn_ScalesToRadius_AndEnds()
    {
        var v = GravityImpactVfx.Spawn(new Vector2(3f, 1f), 1.5f);
        Assert.IsNotNull(v, "프레임 10장이 Resources에 있어야 한다");
        float width = v.Current.bounds.size.x * v.transform.localScale.x;
        Assert.AreEqual(3f, width, 1e-3f, "그림 한 변 = 지름");
        yield return new WaitForSeconds(GravityImpactVfx.Life + 0.15f);
        Assert.IsTrue(v == null, "0.67초 뒤 사라진다");
    }
}

}
