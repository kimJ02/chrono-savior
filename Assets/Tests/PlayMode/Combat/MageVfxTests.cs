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
            if (go != null && go.transform.parent == null && (go.name.StartsWith("Test") || go.name.StartsWith("Vfx_") || go.name == "FireTrailZone" || go.name == "GravityBurst"))
                Object.Destroy(go);
        GravityWellZone.ClearAllForTests();
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

    [Test]
    public void WellOrb_ScaleIsTheStack_AndFadesAtTheEnd()
    {
        Assert.AreEqual(1, GravityVortexVfx.ScaleFor(1));
        Assert.AreEqual(3, GravityVortexVfx.ScaleFor(3));
        Assert.AreEqual(5, GravityVortexVfx.ScaleFor(9), "중첩 상한 5");
        Assert.AreEqual(1f, GravityVortexVfx.AlphaAt(2f));
        Assert.AreEqual(0f, GravityVortexVfx.AlphaAt(0f));
        // 다 커진 구체는 늘 끌어당기는 판정 지름보다 작다(그림 = 핵, 판정과 따로)
        for (int tier = 1; tier <= 5; tier++)
            for (int stack = 1; stack <= 5; stack++)
                Assert.Less(GravityVortexVfx.ScaleFor(stack) * 0.75f, MageSpecConfig.GravityWellRadius(tier, stack, 0f) * 2f);
    }

    /// <summary>중력탄이 터지면: 착탄 폭발(정수 배율)이 한 번, 중력장엔 구체가 1배로 나타나 중첩만큼 부드럽게 커지고, 폭발이 끝나도 남는다.</summary>
    [UnityTest]
    public IEnumerator GravityImpact_ShowsBurst_AndAnOrbThatGrowsWithStacks()
    {
        AssumeInstalled(VfxKeys.MageGravityOrbLarge);
        var pos = new Vector2(5f, 1.5f);
        MageSkillEffects.DetonateGravityOrb(pos, 2, 0f, 1);
        var burst = GameObject.Find("Vfx_mage_gravity_explosion");
        Assert.IsNotNull(burst, "착탄 폭발 그림");
        Assert.IsTrue(IsWhole(burst.transform.localScale.x), "정수 배율");

        var well = GravityWellZone.Active[0];
        var orb = well.GetComponentInChildren<GravityVortexVfx>();
        Assert.IsNotNull(orb, "중력장에 구체 그림");
        Assert.AreEqual(1f, orb.Scale, 1e-4f, "착탄 땐 투사체 크기(1배)로 나타난다");
        Assert.AreEqual(GravityVortexVfx.SortingOrder, orb.GetComponent<SpriteRenderer>().sortingOrder);

        MageSkillEffects.DetonateGravityOrb(pos, 2, 0f, 1); // 같은 자리 — 중첩
        Assert.Greater(well.Stack, 1);
        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(GravityVortexVfx.ScaleFor(well.Stack), orb.Scale, 1e-4f, "중첩만큼 커졌다(정수 배율에서 멈춘다)");
        Assert.IsTrue(orb != null && orb.GetComponent<SpriteRenderer>().color.a > 0.9f, "폭발이 끝나도 남아 보인다");
    }

    /// <summary>대붕괴: 중력점마다 붕괴 폭발 그림(가시 충격 고리, 정수 배율) — 원본 고리(`GravityBurstRing`)는 그대로 그 위에.</summary>
    [Test]
    public void Collapse_ShowsBurstPerWell()
    {
        AssumeInstalled(VfxKeys.MageCollapseExplosion);
        GravityWellZone.SpawnOrMerge(new Vector2(3f, 1f), 3, 0f); // 3차부터 중력점 3개까지
        GravityWellZone.SpawnOrMerge(new Vector2(6f, 1f), 3, 0f);
        Assert.AreEqual(2, GravityWellZone.Active.Count);
        MageSkillEffects.GravityCollapse(3, new Vector2(4.5f, 1f), 1, null);
        var bursts = System.Array.FindAll(Object.FindObjectsByType<VfxPlayer>(FindObjectsSortMode.None), p => p.name == "Vfx_mage_collapse_explosion");
        Assert.AreEqual(2, bursts.Length, "중력점마다 하나");
        foreach (var b in bursts) Assert.IsTrue(IsWhole(b.transform.localScale.x), "정수 배율");
    }
}

}
