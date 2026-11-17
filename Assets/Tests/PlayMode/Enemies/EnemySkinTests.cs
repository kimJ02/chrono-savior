using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 몹 지역 그림(2026-11-17, 사용자 "2스테이지 몬스터 적용" → "4종 배정" → "1스테이지도 바꿔라").
/// 1지역부터 박쥐 = 까마귀, 고블린 = 쥐, 멧돼지 = 멧돼지, 오우거 = 오크통(도트 4).
/// </summary>
public class EnemySkinTests
{
    [SetUp]
    public void Setup() { RunState.Reset(); ProfileService.Reset(); CombatModifiers.Reset(); }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<EnemyMove>(FindObjectsSortMode.None))
            if (go.name.StartsWith("TestSkin")) Object.DestroyImmediate(go.gameObject);
        RunState.Reset(); ProfileService.Reset(); CombatModifiers.Reset();
    }

#if UNITY_EDITOR
    static MonsterCatalog Catalog() => UnityEditor.AssetDatabase.LoadAssetAtPath<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
    static EnemyData Data(EnemyType t) { foreach (var d in Catalog().types) if (d != null && d.type == t) return d; return null; }

    static GameObject Spawn(EnemyData data, int region)
    {
        RunState.Begin(region, RunMode.Normal);
        var go = Object.Instantiate(data.prefab);
        go.name = "TestSkin_" + data.type;
        EnemySetup.Apply(go, data, allowElite: false, eliteChance: 0f, shapeSprite: null);
        return go;
    }

    [Test]
    public void AllRegions_UseAssignedArt()
    {
        foreach (var (type, art, dot) in new[] { (EnemyType.Flyer, "Crow", 2), (EnemyType.Grunt, "Rat", 2), (EnemyType.Charger, "Boar", 2), (EnemyType.Brute, "Barrel", 4) })
        {
            var data = Data(type);
            Assert.IsNotNull(data, type.ToString());
            foreach (int region in new[] { 1, 2, 5 })
            {
                var skin = data.SkinFor(region);
                Assert.IsNotNull(skin, $"{type}: {region}지역 그림이 연결돼 있다(메뉴 ChronoSavior/몬스터 지역 그림 연결)");
                StringAssert.StartsWith($"Enemy_{art}_Move_", skin.move.frames[0].name, $"{type} = {art}");
            }
            var s = data.SkinFor(1);
            Assert.AreEqual(4, s.move.Count, $"{art}: 움직임 4장");
            Assert.IsNotNull(s.hit, $"{art}: 피격 그림");
            Assert.AreEqual(dot, s.dotScale, $"{art}: 도트 배율");
        }
    }

    [Test]
    public void Spawn_AnimatesAtDotScale_BarrelSitsOnItsHitbox()
    {
        var grunt = Data(EnemyType.Grunt);
        foreach (int region in new[] { 1, 2 })
        {
            var rat = Spawn(grunt, region);
            var sr = rat.GetComponent<SpriteRenderer>();
            var anim = rat.GetComponent<EnemySkinAnimator>();
            Assert.IsNotNull(anim, "몹 공용 프리팹에 그림 부품이 배치돼 있다");
            Assert.IsNotNull(anim.Skin, $"{region}지역에서 켜진다");
            CollectionAssert.Contains(grunt.SkinFor(region).move.frames, sr.sprite);
            Assert.AreEqual(Color.white, sr.color, "그림 색 그대로(종류색 안 칠함)");
            Assert.AreEqual(DotScale.Units(32f), sr.bounds.size.y, 1e-3f, "32px 그림 → 도트 배율 2(0.5유닛)");
            anim.Refresh(0.5f);
            CollectionAssert.Contains(grunt.SkinFor(region).move.frames, sr.sprite, "움직임은 되풀이");
        }

        var barrel = Spawn(Data(EnemyType.Brute), 1);
        var bsr = barrel.GetComponent<SpriteRenderer>();
        var col = barrel.GetComponent<CircleCollider2D>();
        Assert.AreEqual(1f, bsr.bounds.size.y, 1e-3f, "오크통은 도트 4(32px → 1.0유닛)");
        float colBottom = barrel.transform.position.y + col.offset.y * barrel.transform.localScale.y - col.radius * barrel.transform.localScale.y; // 물리 동기화 전이라 bounds 대신 계산
        Assert.AreEqual(colBottom, bsr.bounds.min.y, 0.07f, "그림 바닥이 판정 바닥과 거의 같다(묻힘 0.065 이내)");
        Assert.LessOrEqual(bsr.bounds.min.y, colBottom + 1e-3f, "뜨지 않는다");
    }

    [Test]
    public void Hit_ShowsHitFrameBriefly_AndFacesWalkDirection()
    {
        var data = Data(EnemyType.Grunt);
        var go = Spawn(data, 2);
        var anim = go.GetComponent<EnemySkinAnimator>();
        var sr = go.GetComponent<SpriteRenderer>();

        CombatEvents.RaiseEnemyDamaged(go);
        anim.Refresh(0.01f);
        Assert.AreSame(data.SkinFor(2).hit, sr.sprite, "맞으면 피격 그림");
        anim.Refresh(EnemySkinAnimator.HitDuration);
        Assert.IsFalse(anim.ShowingHit);
        CollectionAssert.Contains(data.SkinFor(2).move.frames, sr.sprite, "잠깐 뒤 움직임으로");

        var other = Spawn(data, 2);
        CombatEvents.RaiseEnemyDamaged(other);
        anim.Refresh(0.01f);
        Assert.IsFalse(anim.ShowingHit, "다른 몹이 맞은 건 상관없다");

        var dirField = typeof(EnemyMove).GetField("dir", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        dirField.SetValue(go.GetComponent<EnemyMove>(), 1);
        anim.Refresh(0f);
        Assert.IsTrue(sr.flipX, "왼쪽을 본 그림 — 오른쪽으로 갈 땐 뒤집는다");
        dirField.SetValue(go.GetComponent<EnemyMove>(), -1);
        anim.Refresh(0f);
        Assert.IsFalse(sr.flipX);
    }
#endif
}
}
