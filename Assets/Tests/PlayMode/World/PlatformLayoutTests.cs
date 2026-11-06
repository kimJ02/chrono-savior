using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// **실제 맵 발판 배치**로 발판이 제대로 동작하는지(2026-10-24 사용자 "플랫폼 제대로 잘 작동하는지 확인").
/// 다른 발판 테스트(`PhysicsAndMageTests` 등)는 테스트용 발판 하나로 잰다 — 여기는 `20_Combat` 씬에 놓인 것과
/// 똑같이 땅 + `FieldLayout.NormalPlatforms` 15개 + `BossPlatforms` 3개를 만들고 Figma 타일(`StageVisuals`)까지 깐 뒤,
/// 진짜 `CharacterMover2D`로 착지·점프 통과·아래키 통과·가장자리 낙하를 잰다.
/// </summary>
public class PlatformLayoutTests
{
    const float Px = 1f / StageArtConfig.PixelsPerUnit;

    GameObject ground;
    GameObject normalSet, bossSet;

    [SetUp]
    public void Setup()
    {
        // 다른 테스트가 남긴 "Ground"가 있으면 `StageVisuals`가 그쪽을 집는다
        foreach (var stale in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (stale != null && stale.transform.parent == null && (stale.name == "Ground" || stale.name == "StageVisuals"))
                Object.DestroyImmediate(stale);
        FieldLayout.SetBossArena(false);
        int layer = LayerMask.NameToLayer("Ground");
        Assert.AreNotEqual(-1, layer, "Ground 레이어가 없다");

        // 씬 `20_Combat`의 Ground와 같은 구성
        ground = new GameObject("Ground") { layer = layer };
        var gcol = ground.AddComponent<BoxCollider2D>();
        gcol.size = new Vector2(FieldBounds.MaxX - FieldBounds.MinX, FieldLayout.PlatformThickness * 2f);
        ground.transform.position = new Vector3((FieldBounds.MinX + FieldBounds.MaxX) / 2f, FieldBounds.GroundY - FieldLayout.PlatformThickness, 0f);
        ground.AddComponent<LineRenderer>();

        normalSet = BuildSet("TestPlatforms_Normal", FieldLayout.NormalPlatforms, boss: false, layer);
        bossSet = BuildSet("TestPlatforms_Boss", FieldLayout.BossPlatforms, boss: true, layer);
        bossSet.SetActive(false); // 씬처럼 보스 무대는 꺼진 채 시작

        var visuals = new GameObject("StageVisuals").AddComponent<StageVisuals>();
        visuals.Art = TestPrefabs.StageArt; // 게임 씬과 같은 그림 묶음
        // 실제 씬에선 직렬화 참조(2026-11-05 2b — 예전엔 이름·타입으로 씬을 뒤졌다)
        visuals.Ground = ground.transform;
        visuals.PlatformSets = new[] { normalSet.GetComponent<PlatformSet>(), bossSet.GetComponent<PlatformSet>() };
        visuals.Build();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null &&
                (go.name.StartsWith("Test") || go.name == "Ground" || go.name == "StageVisuals"))
                Object.DestroyImmediate(go);
        FieldLayout.SetBossArena(false);
    }

    /// <summary>씬 `20_Combat`의 `PlatformSet`과 같은 구성.</summary>
    static GameObject BuildSet(string name, float[,] table, bool boss, int layer)
    {
        var parent = new GameObject(name);
        parent.AddComponent<PlatformSet>().bossArena = boss;
        for (int i = 0; i < table.GetLength(0); i++)
        {
            var go = new GameObject($"Platform_{i}") { layer = layer };
            go.transform.SetParent(parent.transform);
            go.transform.position = new Vector3(table[i, 0], table[i, 1], 0f);
            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(table[i, 2], FieldLayout.PlatformThickness);
            col.usedByEffector = true;
            go.AddComponent<PlatformEffector2D>().useOneWay = true;
            go.AddComponent<LineRenderer>();
        }
        return parent;
    }

    static (GameObject go, CharacterMover2D mover, Rigidbody2D rb) NewPlayer(Vector2 feet)
    {
        var go = new GameObject("TestPlayer") { tag = "Player" };
        go.AddComponent<CircleCollider2D>().radius = EntitySizeConfig.PlayerRadius;
        var rb = go.AddComponent<Rigidbody2D>();
        go.transform.position = new Vector3(feet.x, feet.y + EntitySizeConfig.PlayerRadius, 0f);
        var mover = go.AddComponent<CharacterMover2D>();
        return (go, mover, rb);
    }

    static float Feet(GameObject p) => p.transform.position.y - EntitySizeConfig.PlayerRadius;

    static IEnumerator Physics(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
    }

    // ---------------- 그림 = 판정 ----------------

    [Test]
    public void Tiles_MatchEveryRealPlatform_BothArenas()
    {
        foreach (var set in new[] { normalSet, bossSet })
        {
            foreach (Transform p in set.transform)
            {
                var col = p.GetComponent<BoxCollider2D>();
                var tiles = p.Find("Tiles");
                Assert.IsNotNull(tiles, $"{set.name}/{p.name} 타일이 없다");
                float l = float.MaxValue, r = float.MinValue, top = float.MinValue;
                foreach (Transform c in tiles)
                {
                    Vector3 s = c.GetComponent<SpriteRenderer>().sprite.bounds.size;
                    l = Mathf.Min(l, c.position.x - s.x / 2f);
                    r = Mathf.Max(r, c.position.x + s.x / 2f);
                    top = Mathf.Max(top, c.position.y + s.y / 2f);
                }
                Assert.AreEqual(p.position.x - col.size.x / 2f, l, Px, $"{p.name} 왼쪽 끝");
                Assert.AreEqual(p.position.x + col.size.x / 2f, r, Px, $"{p.name} 오른쪽 끝");
                Assert.AreEqual(p.position.y + col.size.y / 2f, top, Px, $"{p.name} 윗면(밟는 높이)");
                Assert.IsNull(tiles.GetComponentInChildren<Collider2D>(), "타일에 판정이 있으면 안 된다(그림만)");
            }
        }
    }

    // ---------------- 진짜 이동으로 ----------------

    [UnityTest]
    public IEnumerator Player_LandsOnEveryRealPlatform_FromAbove()
    {
        var (go, _, rb) = NewPlayer(new Vector2(5f, 3f));
        for (int i = 0; i < FieldLayout.NormalPlatforms.GetLength(0); i++)
        {
            float top = FieldLayout.PlatformTopY(i);
            rb.linearVelocity = Vector2.zero;
            go.transform.position = new Vector3(FieldLayout.NormalPlatforms[i, 0], top + 0.6f + EntitySizeConfig.PlayerRadius, 0f);
            rb.position = go.transform.position;
            yield return Physics(0.6f);
            Assert.AreEqual(top, Feet(go), 0.03f, $"{i}번 발판({FieldLayout.NormalPlatforms[i, 0]:F1}, 윗면 {top:F3})에 착지하지 않았다");
        }
    }

    [UnityTest]
    public IEnumerator Player_RealJump_PassesUpThroughLayer1_AndLandsOnIt()
    {
        float x = FieldLayout.NormalPlatforms[0, 0];
        var (go, mover, rb) = NewPlayer(new Vector2(x, FieldBounds.GroundY));
        yield return Physics(0.3f);
        Assert.AreEqual(FieldBounds.GroundY, Feet(go), 0.03f, "전제: 땅에 서 있다");

        rb.linearVelocity = new Vector2(0f, mover.jumpSpeed); // 실제 점프 속도
        float maxFeet = Feet(go);
        for (float t = 0f; t < 2f; t += Time.fixedDeltaTime)
        {
            yield return new WaitForFixedUpdate();
            maxFeet = Mathf.Max(maxFeet, Feet(go));
        }
        float top = FieldLayout.PlatformTopY(0);
        Assert.Greater(maxFeet, top, "실제 점프로 1층 발판 위까지 못 올라간다");
        Assert.AreEqual(top, Feet(go), 0.03f, "아래에서 뚫고 올라가 1층 발판 위에 서야 한다");
    }

    [UnityTest]
    public IEnumerator Player_DropThrough_FallsToGround_ThenPlatformIsSolidAgain()
    {
        float x = FieldLayout.NormalPlatforms[0, 0];
        float top = FieldLayout.PlatformTopY(0);
        var (go, mover, rb) = NewPlayer(new Vector2(x, top + 0.05f));
        yield return Physics(0.4f);
        Assert.AreEqual(top, Feet(go), 0.03f, "전제: 1층 발판에 서 있다");

        Assert.IsTrue(mover.TryStartDropThrough(), "서 있는 발판을 통과하지 못했다(아래키 동작)");
        yield return Physics(1.0f);
        Assert.AreEqual(FieldBounds.GroundY, Feet(go), 0.03f, "발판을 통과해 땅까지 떨어져야 한다");

        // 통과가 끝나면 그 발판을 다시 밟을 수 있어야 한다
        rb.linearVelocity = Vector2.zero;
        go.transform.position = new Vector3(x, top + 0.6f + EntitySizeConfig.PlayerRadius, 0f);
        rb.position = go.transform.position;
        yield return Physics(0.6f);
        Assert.AreEqual(top, Feet(go), 0.03f, "통과한 뒤 발판이 다시 단단해지지 않았다");
    }

    [UnityTest]
    public IEnumerator Player_OnGround_DropThroughDoesNothing()
    {
        var (go, mover, _) = NewPlayer(new Vector2(5f, FieldBounds.GroundY));
        yield return Physics(0.3f);
        Assert.IsFalse(mover.TryStartDropThrough(), "맨 바닥에선 통과할 발판이 없다(원본 `y < groundY - 2`)");
        yield return Physics(0.3f);
        Assert.AreEqual(FieldBounds.GroundY, Feet(go), 0.03f, "땅을 뚫고 빠졌다");
    }

    [UnityTest]
    public IEnumerator Player_JustPastPlatformEdge_Falls_NoInvisibleLedge()
    {
        float right = FieldLayout.PlatformRightX(0);
        float top = FieldLayout.PlatformTopY(0);
        // 판정 끝(= 그림 끝)에서 몸 반지름 + 조금 바깥 — 판정이 그림보다 길면 여기서 서 버린다
        var (go, _, _) = NewPlayer(new Vector2(right + EntitySizeConfig.PlayerRadius + 0.05f, top + 0.02f));
        yield return Physics(1.0f);
        Assert.AreEqual(FieldBounds.GroundY, Feet(go), 0.03f, "발판 끝 바깥 허공에 섰다");
    }

    // ---------------- 캐릭터가 발판 위에 서 보이는 높이 ----------------

    [Test]
    public void CharacterArt_FeetSink1PxIntoGrass_LikeFigma()
    {
        // 그림 아래 투명 2px + Figma처럼 발이 풀 속으로 1px = 피벗을 3px 위로(그림이 3px 아래에)
        foreach (var name in new[] { "char_mage", "char_gunner", "char_blade", "char_druid" })
        {
            string meta = File.ReadAllText(Path.Combine(Application.dataPath, "Sprites", "Prototype", name + ".png.meta"));
            StringAssert.Contains("alignment: 9", meta, name + " 피벗이 사용자 지정이 아니다");
            StringAssert.Contains("spritePivot: {x: 0.5, y: 0.546875}", meta, name + " 피벗 높이");
        }
    }
}

}
