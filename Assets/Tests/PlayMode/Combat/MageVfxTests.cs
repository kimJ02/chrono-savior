using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 마법사 이펙트 그림(2026-11-07 VFX 스프린트 3단계) — 그림만 얹고 판정은 그대로인지, 크기는 정수 배율인지, 수명이 효과를 따라가는지.
/// 그림을 보는 검사는 팩을 안 가져온 클론에선 '미설치로 생략'(Inconclusive). 차지 오라·탄 그림은 `MageChargeVfxTests`.
/// </summary>
public class MageVfxTests
{
    [SetUp]
    public void Setup() => ProfileService.Reset();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null && (go.name.StartsWith("Test") || go.name.StartsWith("Vfx_") || go.name == "FireTrailZone"))
                Object.Destroy(go);
        RunTransient.Reset();
        ProfileService.Reset();
    }

    static void AssumeInstalled(string key) =>
        Assume.That(VfxFrames.Has(key), "이펙트 팩을 안 가져온 클론 — 미설치로 생략(메뉴 ChronoSavior/VFX 팩 가져오기)");

    static bool IsWhole(float x) => Mathf.Abs(x - Mathf.Round(x)) < 1e-4f;

    [Test]
    public void FlameAlpha_FadesOnlyAtTheEnd()
    {
        Assert.AreEqual(1f, FireTrailZone.FlameAlpha(2f));
        Assert.AreEqual(0.5f, FireTrailZone.FlameAlpha(FireTrailZone.FadeTime * 0.5f), 1e-4f);
        Assert.AreEqual(0f, FireTrailZone.FlameAlpha(0f));
    }

    /// <summary>불길 장판마다 불꽃이 돌고(정수 배율, 밑동은 발 높이), 장판이 끝나 갈 때 흐려지며 장판과 같이 사라진다.</summary>
    [UnityTest]
    public IEnumerator FireTrail_ShowsFlame_ThatFadesWithTheZone()
    {
        AssumeInstalled(VfxKeys.MageFireTrail);
        var zone = FireTrailZone.Spawn(new Vector2(4f, 0.28f), tier: 1, power: 1f);
        var flame = zone.Flame;
        Assert.IsNotNull(flame);
        Assert.AreEqual(FireTrailZone.FlameScale, flame.transform.localScale.x);
        var sr = flame.GetComponent<SpriteRenderer>();
        Assert.AreEqual(0f, sr.bounds.min.y, 0.05f, "불꽃 밑동이 발 높이(장판 중심 − 0.28)");
        yield return null;
        Assert.AreEqual(1f, sr.color.a, 1e-4f, "처음엔 또렷하다");

        float life = MageSpecConfig.FlameTrailLife(1);
        yield return new WaitForSeconds(life - FireTrailZone.FadeTime * 0.4f);
        if (zone != null) Assert.Less(sr.color.a, 1f, "끝나 갈 때 흐려진다");
        yield return new WaitForSeconds(FireTrailZone.FadeTime);
        Assert.IsTrue(zone == null && flame == null, "장판과 같이 사라진다");
    }

    /// <summary>화염 폭발은 그림을 한 번 띄운다 — 판정 지름에 가장 가까운 정수 배율, 끝나면 사라진다.</summary>
    [UnityTest]
    public IEnumerator FireExplosion_PlaysOnce_AtWholeScale()
    {
        AssumeInstalled(VfxKeys.MageFireExplosion);
        MageSkillEffects.SpawnFireExplosion(new Vector2(6f, 1f), 3, 1f, Vector2.zero, 1);
        var vfx = GameObject.Find("Vfx_mage_fire_explosion");
        Assert.IsNotNull(vfx, "폭발 그림");
        float scale = vfx.transform.localScale.x;
        Assert.IsTrue(IsWhole(scale), $"정수 배율이어야 한다: {scale}");
        float diameter = MageSpecConfig.FireExplosionRadius(3) * 2f;
        Assert.AreEqual(diameter, vfx.GetComponent<SpriteRenderer>().bounds.size.x, 0.75f * 0.5f + 1e-3f, "판정 지름에 가장 가까운 크기");
        yield return new WaitForSeconds(VfxFrames.Get(VfxKeys.MageFireExplosion).Length / MageSkillEffects.ExplosionFps + 0.2f);
        Assert.IsTrue(vfx == null, "한 번 재생하고 사라진다");
    }
}

}
