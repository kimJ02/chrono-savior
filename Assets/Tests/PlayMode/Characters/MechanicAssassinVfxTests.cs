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

    /// <summary>레이저 몸체 두께(2026-11-10 사용자 "두껍다" → 팩 B형 가운데 줄 잘라 쓰기): 층마다 커지고 5층이 그림 원래 굵기(16줄), 선 폭(몸체+빛)을 넘지 않는다.</summary>
    [Test]
    public void LaserArt_ThicknessGrowsWithTier()
    {
        int prev = 0;
        for (int tier = 1; tier <= 5; tier++)
        {
            int rows = GunnerLaserBeam.ArtBodyRows(tier);
            Assert.GreaterOrEqual(rows, prev, $"{tier}층은 아래층보다 얇지 않다");
            Assert.AreEqual(0, rows % 2, "위아래 대칭(짝수 줄)");
            Assert.LessOrEqual(rows + 4, GunnerBeamVfx.WindowRows, "몸체 + 빛 2줄씩이 선 폭 안");
            prev = rows;
        }
        Assert.AreEqual(10, GunnerLaserBeam.ArtBodyRows(1), "1층 10줄 — 예전 A형 26줄의 절반 아래");
        Assert.AreEqual(GunnerBeamVfx.SourceRows, GunnerLaserBeam.ArtBodyRows(5), "5층은 그림 원래 굵기");
    }

    /// <summary>
    /// 레이저 몸통 머티리얼(게임 씬에 꽂히는 `Assets/Materials/LaserBeam`)의 합의값 — 위아래 3줄 1:1, 가장 바깥 줄 0.6, 빛 2줄(Codex 리뷰 2026-11-10:
    /// 머티리얼 값이 바뀌면 테두리·빛이 조용히 깨진다). 셰이더는 텍스처 전체를 한 프레임으로 읽으므로 빔 프레임이 아틀라스에 묶이면 안 된다 — 각 프레임이 단독 텍스처인지도 본다.
    /// </summary>
    [Test]
    public void LaserBeamMaterial_KeepsAgreedSliceSettings()
    {
        var m = TestPrefabs.LaserBeam;
        Assert.AreEqual("ChronoSavior/LaserBeamSlice", m.shader.name);
        Assert.AreEqual(3f, m.GetFloat("_EdgeRows"), "위아래 3줄(밝은 테두리·진한 선·줄무늬)은 그대로");
        Assert.AreEqual(0.6f, m.GetFloat("_EdgeAlpha"), 1e-4f, "가장 바깥 줄은 조금 투명");
        Assert.Greater(m.GetFloat("_Glow1"), m.GetFloat("_Glow2"), "빛은 바깥으로 갈수록 옅다");
        Assert.Greater(m.GetFloat("_Glow2"), 0f, "빛 2줄");
        AssumeInstalled(VfxKeys.MechanicLaser("center", "loop"));
        foreach (var f in VfxFrames.Get(VfxKeys.MechanicLaser("center", "loop")))
        {
            Assert.AreEqual(f.texture.width, (int)f.rect.width, "프레임이 텍스처 전체(아틀라스에 묶이면 셰이더가 엉뚱한 곳을 읽는다)");
            Assert.AreEqual(f.texture.height, (int)f.rect.height);
            Assert.GreaterOrEqual(f.texture.height, GunnerBeamVfx.SourceRows, "띠 16줄이 들어가는 높이");
        }
    }

    /// <summary>
    /// 레이저(2026-11-09 다시 짬): 맞을 적이 없으면 빔 대신 총구에서 차징하는 모습만 — 적이 광선에 들어오면 선 하나로 뻗고(길이 = 판정 사거리, 프레임마다),
    /// 끝 그림은 목표를 잡았을 때만. 떼면 사그라들었다 숨는다.
    /// </summary>
    [UnityTest]
    public IEnumerator LaserBeam_ChargesUntilSomethingIsOnTheLine_ThenOneUnbrokenLine()
    {
        AssumeInstalled(VfxKeys.MechanicLaser("center", "loop"));
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
            Assert.AreEqual(beam.Range, beam.Vfx.VisibleLength, 1e-3f, "몸통 길이 = 판정 사거리(프레임마다)");
            Assert.AreEqual(beam.Range, Vector3.Distance(line.GetPosition(0), line.GetPosition(line.positionCount - 1)), 1e-3f, "직선 — 첫 점에서 끝 점까지가 사거리");
            yield return null;
        }
        Assert.IsFalse(beam.Vfx.ImpactVisible, "1층은 목표를 잡지 않으니 끝 그림 없음");
        Assert.AreEqual(DotScale.Units(GunnerBeamVfx.WindowRows), beam.Vfx.Line.widthMultiplier, 1e-4f, "선 폭 = (몸체 + 빛) 줄 수 × 도트 배율");
        Assert.AreEqual(10, beam.Vfx.BodyRows, "1층 몸체 10줄");
        var m = beam.Vfx.BodyMaterial;
        Assert.AreEqual("ChronoSavior/LaserBeamSlice", m.shader.name, "몸통은 가운데 줄을 잘라 그리는 셰이더");
        Assert.AreEqual(10f, m.GetFloat("_BodyRows"));

        // 끝이 뚝 끊기지 않는다 — 셰이더가 끝 0.75유닛을 점점 투명하게, 끝 빛 알갱이(Particle System)가 흩어진다
        Assert.AreEqual(GunnerBeamVfx.FadeLength, m.GetFloat("_FadeLength"), 1e-4f, "허공에서 끝나면 끝 흐림");
        Assert.AreEqual(beam.Range, m.GetFloat("_BeamLength"), 1e-3f, "흐림 기준 길이 = 보이는 길이");
        Assert.IsNotNull(beam.Vfx.TipSparks, "끝 빛 알갱이 프리팹");
        Assert.IsTrue(beam.Vfx.TipSparks.emission.enabled, "빔이 켜져 있으면 알갱이가 나온다");

        float before = beam.Vfx.VisibleLength, endDur = beam.Vfx.EndDuration;
        Assert.Greater(endDur, 0.25f, "사그라들기는 팩 권장 15fps — 약 0.33초");
        beam.Update(Time.deltaTime, false, 1); // 뗐다
        Assert.IsTrue(beam.Vfx.Visible, "바로 사라지지 않고 사그라든다");
        yield return new WaitForSeconds(endDur * 0.5f);
        Assert.IsTrue(beam.Vfx.Visible, "아직 사그라드는 중");
        Assert.Less(beam.Vfx.VisibleLength, before * 0.8f, "앞끝부터 총구로 빨려 들어간다");
        Assert.IsFalse(beam.Vfx.TipSparks.emission.enabled, "빨려 들어갈 땐 알갱이가 새로 안 나온다");
        yield return new WaitForSeconds(endDur * 0.5f + 0.15f);
        Assert.IsFalse(beam.Vfx.Visible, "사그라든 뒤 숨는다");
        Object.Destroy(enemy);
    }

    /// <summary>적을 잃으면(0.35초 유지 뒤) 빔이 툭 꺼지지 않고 총구로 빨려 들어간 다음 차징 모습으로 이어진다(2026-11-09).</summary>
    [UnityTest]
    public IEnumerator LaserBeam_LosingTarget_RetractsThenCharges()
    {
        AssumeInstalled(VfxKeys.MechanicLaser("center", "loop"));
        var go = NewBody("TestGunner");
        go.transform.position = new Vector3(5f, 0.5f, 0f);
        var kit = go.AddComponent<GunnerAttack>().Equipped();
        var beam = new GunnerLaserBeam(kit, go.GetComponent<CharacterMover2D>());
        beam.Update(Time.deltaTime, true, 1);
        var enemy = new GameObject("TestEnemy");
        enemy.tag = "Enemy";
        enemy.layer = LayerMask.NameToLayer("Enemy");
        enemy.transform.position = beam.Origin + beam.Direction * 1.5f;
        enemy.AddComponent<CircleCollider2D>().radius = 0.25f;
        enemy.AddComponent<ChronoSavior.Enemies.EnemyHealth>().SetMaxHp(100000f);
        Physics2D.SyncTransforms();
        beam.Update(Time.deltaTime, true, 1);
        yield return null;
        Assert.IsFalse(beam.Vfx.Charging, "빔");

        Object.Destroy(enemy);
        beam.Update(Time.deltaTime, true, 1); // 매 프레임 그려야 키트가 꺼진 걸로 안 본다
        yield return null;
        float t = 0f, full = beam.Range;
        bool sawRetract = false;
        while (t < GunnerLaserBeam.BeamHoldTime + beam.Vfx.EndDuration + 0.3f)
        {
            beam.Update(Time.deltaTime, true, 1);
            if (!beam.Vfx.Charging && beam.Vfx.Visible && beam.Vfx.VisibleLength < full * 0.7f && beam.Vfx.VisibleLength > 0.01f) sawRetract = true;
            t += Time.deltaTime;
            yield return null;
        }
        Assert.IsTrue(sawRetract, "빔이 줄어드는 단계가 있다(툭 꺼지지 않는다)");
        Assert.IsTrue(beam.Vfx.Charging, "그 뒤 차징 모습");
        beam.Update(Time.deltaTime, false, 1);
        Assert.IsFalse(beam.Vfx.Visible, "떼면 끈다");
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

    /// <summary>
    /// 드론 D-B '발사 강조형'(2026-11-10 사용자): 드론 프리팹엔 추진 배기(Particle System), 탄 프리팹엔 꼬리(TrailRenderer — 기본 꺼짐, 드론 탄만 켬).
    /// 몸 둘레 빛 원·호는 없다.
    /// </summary>
    [Test]
    public void Drone_HasExhaust_NoRing_AndOnlyDroneBulletsTrail()
    {
        var dronePrefab = TestPrefabs.GunnerDrone;
        Assert.IsNotNull(dronePrefab.Exhaust, "추진 배기 Particle System");
        Assert.IsNull(dronePrefab.transform.Find("Glow"), "둘레 빛 원 없음");
        Assert.IsNull(dronePrefab.transform.Find("Arc"), "둘레 호 없음");
        Assert.IsNotNull(TestPrefabs.GunnerBullet.Trail, "탄 프리팹에 꼬리");
        Assert.IsFalse(TestPrefabs.GunnerBullet.Trail.enabled, "꼬리는 기본 꺼짐");
        var droneBullet = GunnerBullet.Spawn(TestPrefabs.GunnerBullet, new Vector3(3f, 3f, 0f), Vector2.right, 1f, 1, 5f, 1f, Color.cyan, withTrail: true);
        var plain = GunnerBullet.Spawn(TestPrefabs.GunnerBullet, new Vector3(3f, 4f, 0f), Vector2.right, 1f, 1, 5f, 1f, Color.cyan);
        Assert.IsTrue(droneBullet.Trail.enabled && droneBullet.Trail.emitting, "드론 탄은 꼬리");
        Assert.IsFalse(plain.Trail.enabled, "메카닉 Z 탄은 꼬리 없음");
        Object.Destroy(droneBullet.gameObject);
        Object.Destroy(plain.gameObject);
    }

    /// <summary>설치기 피해 순간 전기(I-B): 같은 적엔 0.25초에 한 번만(중계기·링크·장판이 같은 틱에 때려도), 다른 적은 따로.</summary>
    [Test]
    public void Zap_ThrottledPerEnemy()
    {
        GunnerZap.ResetForTests();
        var a = new GameObject("ZapA").AddComponent<CircleCollider2D>();
        var b = new GameObject("ZapB").AddComponent<CircleCollider2D>();
        Assert.IsTrue(GunnerZap.At(a, Vector2.zero), "처음은 띄운다");
        Assert.IsFalse(GunnerZap.At(a, Vector2.zero), "같은 적 바로 또는 안 띄운다");
        Assert.IsTrue(GunnerZap.At(b, Vector2.one), "다른 적은 따로");
        Object.Destroy(a.gameObject);
        Object.Destroy(b.gameObject);
        GunnerZap.ResetForTests();
    }

    /// <summary>
    /// 적중 분기 아크(2026-11-10 사용자 'A'): 전기가 뜰 때 때린 곳에서 적 몸 가장자리까지 짧은 전기 — 출발점 고정, 끝은 적 가장자리, 0.13초 뒤 꺼짐.
    /// 한 프레임 4개까지(넘으면 아크만 생략).
    /// </summary>
    [UnityTest]
    public IEnumerator ZapArc_RunsFromSourceToEnemyEdge_AndIsCapped()
    {
        GunnerZap.ResetForTests();
        GunnerZap.LineMaterial = TestMaterial();
        Assume.That(GunnerZap.LineMaterial != null, "머티리얼 에셋이 없다");
        var cols = new List<CircleCollider2D>();
        for (int i = 0; i < 6; i++) cols.Add(new GameObject("ZapEnemy" + i).AddComponent<CircleCollider2D>());
        int before = GunnerZap.ArcsAlive;
        Assert.AreEqual(0, before, "초기화 뒤엔 보이는 아크 없음");
        Vector2 from = new Vector2(0f, 0f), enemy = new Vector2(1f, 0f);
        GunnerZap.At(cols[0], enemy, from, 0.25f);
        Assert.AreEqual(before + 1, GunnerZap.ArcsAlive, "아크 하나");
        var arc = Object.FindObjectsByType<GunnerZapArc>(FindObjectsSortMode.None);
        LineRenderer line = null;
        foreach (var a in arc) if (a.Busy) line = a.Line;
        Assert.IsNotNull(line);
        float px = DotScale.Units(1f);
        Assert.Less(Vector2.Distance(line.GetPosition(0), from), px, "출발점 = 때린 곳");
        Assert.Less(Mathf.Abs(Vector2.Distance(line.GetPosition(line.positionCount - 1), enemy) - 0.25f * 0.9f), px * 1.5f, "끝 = 적 몸 가장자리");
        for (int i = 1; i < 6; i++) GunnerZap.At(cols[i], enemy + Vector2.up * i, from, 0.25f);
        Assert.AreEqual(before + GunnerZap.MaxArcsPerFrame, GunnerZap.ArcsAlive, "한 프레임 4개까지");
        yield return new WaitForSeconds(GunnerZapArc.Life + 0.1f);
        Assert.AreEqual(0, GunnerZap.ArcsAlive, "잠깐 번쩍이고 꺼진다");
        foreach (var c in cols) Object.Destroy(c.gameObject);
        GunnerZap.ResetForTests();
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
        // I-B(2026-11-10 사용자): 늘 깔린 범위 원·점선 원은 없다(맞는 순간 전기로 알린다)
        Assert.IsNull(t.transform.Find("RangeFill"));
        Assert.IsNull(t.transform.Find("RangeEdge"));
        Assert.IsNull(t.transform.Find("LinkRange"));
        if (VfxFrames.Get(VfxKeys.MechanicRelayAura).Length > 0) Assert.IsNotNull(t.transform.Find("RelayAura"), "머리 둘레 전기 오라");
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
        vfx.Show(tri, segs, 3, null);
        Assert.AreEqual(3, vfx.VisibleLinks);
        // I-B 전기 아크(2026-11-10): 링크마다 [판정 경로 그대로인 기준선, 주 아크, 보조 아크] — 아크 양 끝은 중계기에 고정, 가운데는 ±3px 안
        var lines = vfx.LinkLines(0);
        Assert.AreEqual(3, lines.Length);
        Assert.AreEqual(2, lines[0].positionCount, "기준선은 곧은 두 점");
        float px = DotScale.Units(1f);
        Assert.Less(Vector3.Distance(lines[0].GetPosition(0), tri[0]), px, "기준선 시작 = 링크 끝점");
        Assert.Less(Vector3.Distance(lines[0].GetPosition(1), tri[1]), px, "기준선 끝 = 링크 끝점");
        var arc = lines[1];
        Assert.Greater(arc.positionCount, 2, "아크는 여러 점");
        Assert.Less(Vector3.Distance(arc.GetPosition(0), tri[0]), px, "아크 시작 고정");
        Assert.Less(Vector3.Distance(arc.GetPosition(arc.positionCount - 1), tri[1]), px, "아크 끝 고정");
        for (int k = 0; k < arc.positionCount; k++)
            Assert.LessOrEqual(Mathf.Abs(arc.GetPosition(k).y - tri[0].y), DotScale.Units(GunnerFieldVfx.ArcJitterPx) + px, "가운데 점은 ±3px 안(판정선 둘레)");
        Assert.IsTrue(vfx.FillVisible, "닫힌 도형은 칠한다");
        vfx.Show(tri, new List<GunnerField.Segment> { segs[0], segs[1] }, 3, null);
        Assert.IsFalse(vfx.FillVisible, "꼭짓점은 셋이어도 한 변이 안 이어지면(판정상 안쪽 피해 없음) 칠하지 않는다");
        var line = new List<Vector2> { tri[0], tri[1] };
        vfx.Show(line, new List<GunnerField.Segment> { segs[0] }, 3, null);
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
