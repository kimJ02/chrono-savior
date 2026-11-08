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
            if (go != null && go.transform.parent == null && (go.name.StartsWith("Test") || go.name.StartsWith("Vfx_") || go.name == "FireTrailZone" || go.name == "PathFlame" || go.name == "RangeRing"))
                Object.Destroy(go);
        GravityWellZone.ClearAllForTests();
        RunTransient.Reset();
        ProfileService.Reset();
    }

    static void AssumeInstalled(string key) =>
        Assume.That(VfxFrames.Has(key), "이펙트 팩을 안 가져온 클론 — 미설치로 생략(메뉴 ChronoSavior/VFX 팩 가져오기)");


    /// <summary>바닥 불꽃: 늦게 점화(처음엔 안 보임) → 밑동은 바닥에 붙은 채 솟고 → 끝에 사그라들어 사라진다.</summary>
    [UnityTest]
    public IEnumerator PathFlame_IgnitesFromTheGround_ThenDiesDown()
    {
        AssumeInstalled(VfxKeys.MageFireTrail);
        var f = PathFlame.Ignite(new Vector2(4f, 0f), 0.1f, 0.8f);
        Assert.IsNotNull(f);
        var sr = f.GetComponent<SpriteRenderer>();
        Assert.IsFalse(sr.enabled, "점화 전엔 안 보인다");
        yield return new WaitForSeconds(0.1f + PathFlame.IgniteTime + 0.05f);
        Assert.IsTrue(sr.enabled);
        Assert.AreEqual(1f, f.Grow, 1e-3f, "다 솟았다");
        Assert.AreEqual(0f, sr.bounds.min.y, 0.03f, "밑동은 바닥에");
        yield return new WaitForSeconds(0.8f);
        Assert.IsTrue(f == null, "사그라들어 사라진다");
    }

    /// <summary>바닥 불꽃 모양은 5종을 섞는다(그림이 모두 들어와 있어야).</summary>
    [Test]
    public void PathFlame_ShapesAreAllInstalled()
    {
        AssumeInstalled(VfxKeys.MageFireTrail);
        foreach (var k in PathFlame.Shapes) Assert.IsTrue(VfxFrames.Has(k), k);
    }

    /// <summary>화염 폭발: 그림은 공통 도트 배율로 한 번, 판정 범위는 고리로 보인다, 끝나면 사라진다.</summary>
    [UnityTest]
    public IEnumerator FireExplosion_PlaysOnceAtDotScale_WithRangeRing()
    {
        AssumeInstalled(VfxKeys.MageFireExplosion);
        MageSkillEffects.SpawnFireExplosion(new Vector2(6f, 1f), 3, 1f, Vector2.zero, 1);
        var vfx = GameObject.Find("Vfx_mage_fire_explosion");
        Assert.IsNotNull(vfx, "폭발 그림");
        Assert.AreEqual(DotScale.Value, vfx.transform.localScale.x, 1e-4f, "공통 도트 배율");
        var ring = GameObject.Find("RangeRing");
        Assert.IsNotNull(ring, "판정 고리");
        Assert.AreEqual(MageSpecConfig.FireExplosionRadius(3) * 2f, ring.GetComponent<SpriteRenderer>().bounds.size.x, 0.1f, "고리 지름 = 판정 지름");
        yield return new WaitForSeconds(VfxFrames.Get(VfxKeys.MageFireExplosion).Length / MageSkillEffects.ExplosionFps + 0.2f);
        Assert.IsTrue(vfx == null && ring == null, "한 번 재생하고 고리와 함께 사라진다");
    }

    [Test]
    public void WellOrb_SmallThenLarge_AndFadesAtTheEnd()
    {
        Assert.AreEqual(VfxKeys.MageGravityOrbSmall, GravityVortexVfx.KeyFor(1));
        Assert.AreEqual(VfxKeys.MageGravityOrbSmall, GravityVortexVfx.KeyFor(2));
        Assert.AreEqual(VfxKeys.MageGravityOrbLarge, GravityVortexVfx.KeyFor(3));
        Assert.Less(GravityVortexVfx.DiameterFor(1), GravityVortexVfx.DiameterFor(5));
        Assert.AreEqual(1f, GravityVortexVfx.AlphaAt(2f));
        Assert.AreEqual(0f, GravityVortexVfx.AlphaAt(0f));
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
        Assert.AreEqual(DotScale.Value, burst.transform.localScale.x, 1e-4f, "공통 도트 배율");

        var well = GravityWellZone.Active[0];
        var orb = well.GetComponentInChildren<GravityVortexVfx>();
        Assert.IsNotNull(orb, "중력장에 구체 그림");
        Assert.AreEqual(VfxKeys.MageGravityOrbSmall, orb.ShownKey, "착탄 땐 작은 구체(중력탄과 같은 크기)");
        Assert.AreEqual(DotScale.Value, orb.transform.lossyScale.x, 1e-4f, "공통 도트 배율");
        Assert.AreEqual(GravityVortexVfx.SortingOrder, orb.GetComponent<SpriteRenderer>().sortingOrder);

        MageSkillEffects.DetonateGravityOrb(pos, 2, 0f, 1); // 같은 자리 — 중첩
        Assert.Greater(well.Stack, 1);
        yield return new WaitForSeconds(0.8f);
        Assert.AreEqual(GravityVortexVfx.KeyFor(well.Stack), orb.ShownKey, "중첩이 늘면 큰 구체로");
        Assert.IsTrue(orb != null && orb.GetComponent<SpriteRenderer>().color.a > 0.9f, "폭발이 끝나도 남아 보인다");
    }

    /// <summary>대붕괴: 중력점마다 붕괴 폭발 그림(가시 충격 고리, 정수 배율). 원본의 보라 원판·선 고리는 2026-11-09 사용자 결정으로 그리지 않는다.</summary>
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
        foreach (var b in bursts) Assert.AreEqual(DotScale.Value, b.transform.localScale.x, 1e-4f, "공통 도트 배율");
    }

    /// <summary>중첩되면: 구체가 한 번 부풀고(펄스), "중첩 xN" 글씨를 띄워 달라고 알린다(전투 HUD가 그린다).</summary>
    [Test]
    public void Merge_PulsesOrb_AndAsksForStackPopup()
    {
        AssumeInstalled(VfxKeys.MageGravityOrbLarge);
        string popup = null;
        CombatEvents.PopupRequested += (pos, text, color) => popup = text;
        try
        {
            var well = GravityWellZone.SpawnOrMerge(new Vector2(5f, 1.5f), 2, 0f);
            var orb = well.GetComponentInChildren<GravityVortexVfx>();
            Assert.IsNull(popup, "처음 생길 땐 글씨 없음");
            GravityWellZone.SpawnOrMerge(new Vector2(5.2f, 1.5f), 2, 1f); // 완충탄 → +3
            Assert.AreEqual("중첩 x" + well.Stack, popup);
            Assert.IsNotNull(GameObject.Find("RangeRing"), "커진 범위가 고리로 한 번 퍼진다");
        }
        finally { CombatEvents.Reset(); }
    }

    /// <summary>중력탄이 적을 맞히면 그 적 자리에 보라 충격이 튄다.</summary>
    [Test]
    public void GravityImpact_SparksOnEachHitEnemy()
    {
        AssumeInstalled(VfxKeys.MageGravitySpark);
        NewEnemy(new Vector3(5.3f, 1.5f, 0f));
        Physics2D.SyncTransforms();
        int hits = MageSkillEffects.DetonateGravityOrb(new Vector2(5f, 1.5f), 2, 0f, 1);
        Assert.AreEqual(1, hits);
        Assert.IsNotNull(GameObject.Find("Vfx_mage_gravity_spark"), "맞은 적 자리에 보라 충격");
    }

    /// <summary>대붕괴 그림은 중첩 1~2는 가시 고리, 3 이상·임계는 큰 번개 충격(도트 크기는 같게 — 그림을 바꾼다).</summary>
    [Test]
    public void CollapseArt_BiggerPictureForBiggerStacks()
    {
        Assert.AreEqual(VfxKeys.MageCollapseExplosion, MageSkillEffects.CollapseArtKey(1, false));
        Assert.AreEqual(VfxKeys.MageCollapseExplosion, MageSkillEffects.CollapseArtKey(2, false));
        Assert.AreEqual(VfxKeys.MageCollapseCritical, MageSkillEffects.CollapseArtKey(3, false));
        Assert.AreEqual(VfxKeys.MageCollapseCritical, MageSkillEffects.CollapseArtKey(1, true), "임계면 큰 것");
    }

    /// <summary>임계 붕괴(5마리 이상)면 큰 충격 그림을 2차 고리 크기로 겹친다. 임계가 아니면 없다.</summary>
    [Test]
    public void CriticalCollapse_AddsBigShock()
    {
        AssumeInstalled(VfxKeys.MageCollapseCritical);
        GravityWellZone.SpawnOrMerge(new Vector2(5f, 0.36f), 5, 0.2f);
        MageSkillEffects.GravityCollapse(5, new Vector2(3f, 0.36f), 1, null);
        Assert.IsNull(GameObject.Find("Vfx_mage_collapse_critical"), "적이 없고 중첩도 작으면 임계 그림이 아니다");

        GravityWellZone.SpawnOrMerge(new Vector2(5f, 0.36f), 5, 0.2f);
        for (int i = 0; i < 5; i++) NewEnemy(new Vector3(5f + (i - 2) * 0.2f, 0.36f, 0f)); // 5마리 = 임계(원본 `count >= 5`)
        Physics2D.SyncTransforms();
        var (_, critical, _) = MageSkillEffects.GravityCollapse(5, new Vector2(3f, 0.36f), 1, null);
        Assert.IsTrue(critical, "전제: 5마리면 임계 붕괴");
        var shock = GameObject.Find("Vfx_mage_collapse_critical");
        Assert.IsNotNull(shock, "임계면 큰 충격");
        Assert.AreEqual(DotScale.Value, shock.transform.localScale.x, 1e-4f, "공통 도트 배율");
    }

    /// <summary>화상 — 불타는 동안 몸에 불꽃이 붙고(정수 월드 배율), 틱마다 주황으로 깜빡이며 피해를 받고, 끝나면 불꽃이 꺼진다(2026-11-09 사용자).</summary>
    [UnityTest]
    public IEnumerator Burn_ShowsFlame_TicksDamage_ThenGoesOut()
    {
        AssumeInstalled(VfxKeys.MageBurn);
        NewEnemy(new Vector3(6f, 0.36f, 0f));
        var enemy = GameObject.Find("TestEnemy").GetComponent<ChronoSavior.Enemies.EnemyHealth>();
        enemy.ApplyBurn(2);
        var flame = enemy.transform.Find("BurnFlame");
        Assert.IsNotNull(flame, "불타는 그림");
        Assert.IsTrue(flame.gameObject.activeSelf);
        Assert.AreEqual(ChronoSavior.Enemies.EnemyHealth.BurnFlameScale, flame.lossyScale.x, 1e-3f, "몹 배율과 무관한 정수 배율");
        float hp = enemy.CurrentHp;
        yield return new WaitForSeconds(enemy.burnTickInterval + 0.1f);
        Assert.Less(enemy.CurrentHp, hp, "화상 틱 피해");
        typeof(ChronoSavior.Enemies.EnemyHealth).GetField("burnRemaining", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(enemy, 0f); // 화상 시간이 다 됐다
        yield return null;
        yield return null;
        Assert.AreEqual(0, enemy.BurnStacks);
        Assert.IsFalse(flame.gameObject.activeSelf, "화상이 끝나면 불꽃도 꺼진다");
    }

    static void NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = go.AddComponent<ChronoSavior.Enemies.EnemyMove>();
        var health = go.AddComponent<ChronoSavior.Enemies.EnemyHealth>();
        typeof(ChronoSavior.Enemies.EnemyMove).GetField("spawnProtectTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        move.moveSpeed = 0f;
        health.SetMaxHp(100000f); // 죽으면 대상이 사라져 임계 판정이 흔들린다
    }
}

}
