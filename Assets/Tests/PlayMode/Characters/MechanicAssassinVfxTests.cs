using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Combat;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 메카닉·살수 이펙트 그림(2026-11-08 VFX 스프린트) — 그림만 얹고 판정은 그대로인지, 그림 없는 클론에서 안전한지.
/// 그림을 보는 검사는 팩을 안 가져온 클론에선 '미설치로 생략'(Inconclusive).
/// </summary>
public class MechanicAssassinVfxTests
{
    static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void Setup()
    {
        ProfileService.Reset();
        GameState.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null && (go.GetComponent<RunTransient>() != null || go.name.StartsWith("Test"))) Object.Destroy(go);
        RunTransient.Reset();
        ProfileService.Reset();
        GameState.Reset();
    }

    static void AssumeInstalled(string key) =>
        Assume.That(VfxFrames.Has(key), "이펙트 팩을 안 가져온 클론 — 미설치로 생략(메뉴 ChronoSavior/VFX 팩 가져오기)");

    static Material TestMaterial()
    {
#if UNITY_EDITOR
        return UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/VfxUnlit.mat");
#else
        return null;
#endif
    }

    static GameObject NewBody(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterMover2D>();
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        return go;
    }

    // ── 메카닉 ──

    [Test]
    public void LaserArt_ThicknessGrowsWithTier()
    {
        Assert.IsFalse(GunnerLaserBeam.LargeArt(4), "1~4층 small");
        Assert.IsTrue(GunnerLaserBeam.LargeArt(5), "5층 large");
    }

    /// <summary>
    /// 레이저(2026-11-09 다시 짬): 맞을 적이 없으면 빔 대신 총구에서 차징하는 모습만 — 적이 광선에 들어오면 선 하나로 뻗고(길이 = 판정 사거리, 프레임마다),
    /// 끝 그림은 목표를 잡았을 때만. 떼면 사그라들었다 숨는다.
    /// </summary>
    [UnityTest]
    public IEnumerator LaserBeam_ChargesUntilSomethingIsOnTheLine_ThenOneUnbrokenLine()
    {
        AssumeInstalled(VfxKeys.MechanicLaser("center", "loop", false));
        var go = NewBody("TestGunner");
        go.transform.position = new Vector3(5f, 0.5f, 0f);
        var kit = go.AddComponent<GunnerAttack>().Equipped();
        var beam = new GunnerLaserBeam(kit, go.GetComponent<CharacterMover2D>());
        beam.Update(Time.deltaTime, true, 1);
        Assert.IsTrue(beam.Vfx.Charging, "맞을 적이 없으면 차징 모습");
        Assert.IsFalse(beam.Vfx.Line.enabled, "빔은 안 보인다");

        // 광선 위에 적
        var enemy = new GameObject("TestEnemy");
        enemy.tag = "Enemy";
        enemy.layer = LayerMask.NameToLayer("Enemy");
        enemy.transform.position = beam.Origin + beam.Direction * 1.5f;
        enemy.AddComponent<CircleCollider2D>().radius = 0.25f;
        enemy.AddComponent<ChronoSavior.Enemies.EnemyHealth>().SetMaxHp(100000f);
        Physics2D.SyncTransforms();
        for (int f = 0; f < 4; f++)
        {
            beam.Update(Time.deltaTime, true, 1);
            Assert.IsFalse(beam.Vfx.Charging, "적이 광선에 들어오면 빔");
            var line = beam.Vfx.Line;
            Assert.IsTrue(line.enabled);
            Assert.AreEqual(2, line.positionCount, "직선은 두 점");
            Assert.AreEqual(beam.Range, Vector3.Distance(line.GetPosition(0), line.GetPosition(1)), 1e-3f, "몸통 길이 = 판정 사거리(프레임마다)");
            yield return null;
        }
        Assert.IsFalse(beam.Vfx.ImpactVisible, "1층은 목표를 잡지 않으니 끝 그림 없음");
        Assert.AreEqual(DotScale.Units(64f), beam.Vfx.Line.widthMultiplier, 1e-4f, "선 굵기 = 텍스처 높이 × 도트 배율");

        beam.Update(Time.deltaTime, false, 1); // 뗐다
        Assert.IsTrue(beam.Vfx.Visible, "바로 사라지지 않고 사그라든다");
        yield return new WaitForSeconds(0.4f);
        Assert.IsFalse(beam.Vfx.Visible, "사그라든 뒤 숨는다");
    }

    /// <summary>드론 탄은 적 하나를 맞히면 사라진다(사용자 2026-11-09 — 원본은 2마리(5층 3마리) 관통).</summary>
    [Test]
    public void DroneBullet_VanishesAfterOneHit()
    {
        for (int tier = 1; tier <= 5; tier++) Assert.AreEqual(1, GunnerSpecConfig.DroneBulletPierce(tier), $"{tier}층");
    }

    /// <summary>드론 탄은 에너지 펠렛(정수 배율), 다른 탄은 예전 그림. 판정 상자는 그대로.</summary>
    [Test]
    public void DroneBullet_UsesPellet_OthersUnchanged()
    {
        AssumeInstalled(VfxKeys.MechanicPellet);
        var pellet = GunnerBullet.Spawn(TestPrefabs.GunnerBullet, new Vector3(3f, 3f, 0f), Vector2.right, 1f, 1, 5f, 1.5f, Color.cyan, artKey: VfxKeys.MechanicPellet);
        var plain = GunnerBullet.Spawn(TestPrefabs.GunnerBullet, new Vector3(3f, 4f, 0f), Vector2.right, 1f, 1, 5f, 1.5f, Color.cyan);
        Assert.AreEqual(pellet.GetComponent<BoxCollider2D>().size, plain.GetComponent<BoxCollider2D>().size, "판정 상자는 같다");
        var p = pellet.GetComponentInChildren<VfxPlayer>();
        Assert.IsNotNull(p, "드론 탄은 펠렛 그림");
        Assert.AreEqual(Mathf.Round(p.transform.localScale.x), p.transform.localScale.x, 1e-4f, "정수 배율");
        Assert.IsNull(plain.GetComponentInChildren<VfxPlayer>(), "그 밖의 탄은 예전 그림");
        Object.Destroy(pellet.gameObject);
        Object.Destroy(plain.gameObject);
    }

    [Test]
    public void Turret_HasRelayHead_AboveTheBody()
    {
        AssumeInstalled(VfxKeys.MechanicRelayHead);
        var t = GunnerTurret.Spawn(TestPrefabs.GunnerTurret, new Vector2(4f, 0f), 1);
        var head = t.transform.Find("RelayHead");
        Assert.IsNotNull(head, "중계기 머리");
        Assert.Greater(head.localPosition.y, GunnerTurret.RelayHeadHeight - 1e-4f, "몸체 위");
        Assert.AreEqual(DotScale.Value, head.lossyScale.x, 1e-4f, "공통 도트 배율");
        t.Remove();
    }

    /// <summary>장판: 연결선마다 그림, 닫힌 도형(3점 이상)이면 안을 칠한다. 2점이면 선만.</summary>
    [Test]
    public void Field_DrawsLinks_AndFillsOnlyClosedShapes()
    {
        var go = new GameObject("TestFieldVfx");
        var vfx = go.AddComponent<GunnerFieldVfx>();
        vfx.FillMaterial = TestMaterial();
        Assume.That(vfx.FillMaterial != null, "머티리얼 에셋이 없다");
        var tri = new List<Vector2> { new Vector2(0f, 0f), new Vector2(2f, 0f), new Vector2(1f, 1.5f) };
        var segs = new List<GunnerField.Segment> { new GunnerField.Segment(tri[0], tri[1]), new GunnerField.Segment(tri[1], tri[2]), new GunnerField.Segment(tri[2], tri[0]) };
        vfx.Show(tri, segs, null);
        Assert.AreEqual(3, vfx.VisibleLinks);
        Assert.IsTrue(vfx.FillVisible, "닫힌 도형은 칠한다");
        var line = new List<Vector2> { tri[0], tri[1] };
        vfx.Show(line, new List<GunnerField.Segment> { segs[0] }, null);
        Assert.AreEqual(1, vfx.VisibleLinks);
        Assert.IsFalse(vfx.FillVisible, "두 점이면 선만");
        vfx.Hide();
    }

    // ── 살수 ──

    static (BladeCombat blade, BladeVfx vfx) NewAssassin()
    {
        var go = NewBody("TestBlade");
        var blade = go.AddComponent<BladeCombat>();
        var vfx = go.AddComponent<BladeVfx>();
        vfx.LineMaterial = TestMaterial();
        GameState.Set(GameScene.Run);
        return (blade, vfx);
    }

    [Test]
    public void Focus_DarkensBackground_AndShowsHeartbeat_ThenClears()
    {
        AssumeInstalled(VfxKeys.AssassinHeartbeat);
        var (blade, vfx) = NewAssassin();
        typeof(BladeCombat).GetField("focusT", Private).SetValue(blade, 2f);
        for (int i = 0; i < 10; i++) vfx.Refresh(0.05f);
        Assert.AreEqual(BladeVfx.ShadeAlpha, vfx.ShadeAmount, 1e-4f, "집중 중엔 배경이 어둡다");
        Assert.IsTrue(vfx.HeartbeatVisible, "하트비트");
        typeof(BladeCombat).GetField("focusT", Private).SetValue(blade, 0f);
        for (int i = 0; i < 10; i++) vfx.Refresh(0.05f);
        Assert.AreEqual(0f, vfx.ShadeAmount, 1e-4f, "끝나면 다시 밝다");
        Assert.IsFalse(vfx.HeartbeatVisible);
    }

    /// <summary>궤적 선은 판정과 같은 점을 따라가고, 4층 파열은 길을 따라 스플래터를 뿌린다.</summary>
    [Test]
    public void Trail_FollowsTrailPoints_AndRipScattersSplatter()
    {
        AssumeInstalled(VfxKeys.AssassinSplatter);
        var (blade, vfx) = NewAssassin();
        Assume.That(vfx.LineMaterial != null, "머티리얼 에셋이 없다");
        ProfileService.Current.bladeTier = 4;
        var pts = (List<Vector2>)typeof(BladeCombat).GetField("trailPts", Private).GetValue(blade);
        for (int i = 0; i < 12; i++) pts.Add(new Vector2(i * 0.3f, 1f));
        typeof(BladeCombat).GetField("trailT", Private).SetValue(blade, 1f);
        vfx.Refresh(0.016f);
        Assert.AreEqual(12, vfx.TrailPointCount, "궤적 선 = 판정 점");

        typeof(BladeCombat).GetMethod("RipTrail", Private).Invoke(blade, null);
        int splatters = 0;
        foreach (var p in Object.FindObjectsByType<VfxPlayer>(FindObjectsSortMode.None)) if (p.name == "Vfx_assassin_splatter") splatters++;
        Assert.GreaterOrEqual(splatters, 6, "길(3.3유닛)을 따라 0.5유닛마다");
    }

    [Test]
    public void Storm_LoopsSpinSlash_AndLeavesSmoke()
    {
        AssumeInstalled(VfxKeys.AssassinSpin);
        var (blade, vfx) = NewAssassin();
        typeof(BladeCombat).GetField("storming", Private).SetValue(blade, true);
        vfx.Refresh(0.016f);
        Assert.IsTrue(vfx.SpinVisible, "칼날폭풍 중 회전베기");
        blade.transform.position += new Vector3(1f, 0f, 0f);
        vfx.Refresh(0.016f);
        bool smoke = false;
        foreach (var p in Object.FindObjectsByType<VfxPlayer>(FindObjectsSortMode.None)) if (p.name == "Vfx_assassin_smoke") smoke = true;
        Assert.IsTrue(smoke, "지나간 길에 연기");
        typeof(BladeCombat).GetField("storming", Private).SetValue(blade, false);
        vfx.Refresh(0.016f);
        Assert.IsFalse(vfx.SpinVisible, "끝나면 회전베기도 끝");
    }
}

}
