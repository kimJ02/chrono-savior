using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 1스테이지 Figma 그림 + C안 카메라(2026-10-24, `StageArtConfig`). 화면은 배치 테스트로 못 보니
/// "선명하게 보이려면 반드시 맞아야 하는 숫자"(픽셀/유닛·정수배·크기)와 "가장자리가 안 보이는 범위"를 잰다.
/// </summary>
public class StageVisualsTests
{
    [SetUp]
    public void Setup()
    {
        // 다른 테스트가 남긴 "Ground"가 있으면 `GameObject.Find`가 그쪽을 집는다
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null && (go.name == "Ground" || go.name == "StageVisuals"))
                Object.DestroyImmediate(go);
        FieldLayout.SetBossArena(false);
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (go != null && go.transform.parent == null &&
                (go.name.StartsWith("Test") || go.name == "Ground" || go.name == "StageVisuals"))
                Object.DestroyImmediate(go);
        }
        FieldLayout.SetBossArena(false);
    }

    // ---------------- 카메라 ----------------

    // 카메라의 PixelPerfectCamera 값(128PPU·960×540)은 씬에 저장된 값이라 실제 씬을 부팅하는
    // `Systems/GameBootTests.SharedCamera_IsPixelPerfect960x540_AsSavedInTheScene`이 본다(2026-11-05).

    [Test]
    public void ViewSize_Is960x540px_At128PixelsPerUnit()
    {
        Assert.AreEqual(2.109375f, StageArtConfig.ViewHalfHeight, 1e-6f);
        Assert.AreEqual(3.75f, StageArtConfig.ViewHalfWidth, 1e-6f);
    }

    [Test]
    public void CameraY_StandingOnGround_PutsGroundAt27Percent()
    {
        float y = CameraFollow2D.NextFollowY(5f, FieldBounds.GroundY, standing: true);
        float viewBottom = y - StageArtConfig.ViewHalfHeight;
        float fraction = (FieldBounds.GroundY - viewBottom) / (StageArtConfig.ViewHalfHeight * 2f);
        Assert.AreEqual(0.275f, fraction, 1e-4f, "땅에 섰을 때 지면이 화면 아래 27.5%가 아니다(미리보기 구도)");
    }

    [Test]
    public void CameraY_StandingOnPlatform_FollowsUp_AndClampsAtTop()
    {
        float layer1 = FieldLayout.PlatformTopY(0); // 1층 발판 윗면
        Assert.AreEqual(layer1 + StageArtConfig.CameraYOffset, CameraFollow2D.NextFollowY(0f, layer1, true), 1e-4f);
        Assert.AreEqual(CameraFollow2D.MaxY, CameraFollow2D.NextFollowY(0f, 50f, true), 1e-4f, "맨 위 발판보다 위로는 안 간다");
        Assert.AreEqual(CameraFollow2D.MinY, CameraFollow2D.NextFollowY(9f, -5f, true), 1e-4f, "땅보다 아래로는 안 간다");
    }

    [Test]
    public void CameraY_InAir_MovesOnlyWhenLeavingTheScreen()
    {
        float follow = CameraFollow2D.MinY;
        // 땅에서 1.0 점프 — 머리(1.5)는 화면 위 여유 안이라 그대로
        Assert.AreEqual(follow, CameraFollow2D.NextFollowY(follow, 1.0f, standing: false), 1e-4f, "작은 점프마다 화면이 출렁인다");
        // 높이 날아오름 — 머리가 위 여유를 넘은 만큼만 올라간다
        float feet = 3.0f;
        float head = feet + EntitySizeConfig.PlayerHeight;
        float top = follow + StageArtConfig.ViewHalfHeight - StageArtConfig.CameraAirTopMargin;
        Assert.AreEqual(follow + (head - top), CameraFollow2D.NextFollowY(follow, feet, false), 1e-4f);
        // 위에서 떨어짐 — 발이 아래 여유 밖으로 나간 만큼만 내려간다
        float high = 4.0f;
        float bottom = high - StageArtConfig.ViewHalfHeight + StageArtConfig.CameraAirBottomMargin;
        Assert.AreEqual(high - (bottom - 1.0f), CameraFollow2D.NextFollowY(high, 1.0f, false), 1e-4f);
    }

    // ---------------- 배경 ----------------

    [Test]
    public void Background_NeverShowsItsEdges_AnywhereTheCameraCanGo()
    {
        foreach (bool boss in new[] { false, true })
        {
            FieldLayout.SetBossArena(boss);
            float minX = FieldBounds.MinX + StageArtConfig.ViewHalfWidth, maxX = FieldBounds.MaxX - StageArtConfig.ViewHalfWidth;
            float halfWRaw = StageArtConfig.ViewHalfWidth * StageArtConfig.PixelsPerUnit / (float)TestPrefabs.StageArt.backgroundScale;
            float halfHRaw = StageArtConfig.ViewHalfHeight * StageArtConfig.PixelsPerUnit / (float)TestPrefabs.StageArt.backgroundScale;
            for (int i = 0; i <= 10; i++)
            for (int j = 0; j <= 10; j++)
            {
                float x = Mathf.Lerp(minX, maxX, i / 10f);
                float y = Mathf.Lerp(CameraFollow2D.MinY, CameraFollow2D.MaxY, j / 10f);
                Vector2 c = StageVisuals.BackgroundRawCenter(TestPrefabs.StageArt, x, y);
                Assert.GreaterOrEqual(c.x - halfWRaw, -0.01f, $"왼쪽 가장자리가 보인다 (카메라 {x:F2},{y:F2})");
                Assert.LessOrEqual(c.x + halfWRaw, 640.01f, $"오른쪽 가장자리가 보인다 (카메라 {x:F2},{y:F2})");
                Assert.GreaterOrEqual(c.y - halfHRaw, -0.01f, $"위 가장자리가 보인다 (카메라 {x:F2},{y:F2})");
                Assert.LessOrEqual(c.y + halfHRaw, 360.01f, $"아래 가장자리가 보인다 (카메라 {x:F2},{y:F2})");
            }
        }
    }

    [Test]
    public void Background_GrassLineNearGround_AndNoEdgeEverShows()
    {
        // 2026-11-09 도트 통일로 배경 3배 → 2배: 그림 위·아래 가장자리를 숨기는 게 먼저라, 땅에 섰을 때 그림 속 풀밭은 지면에서 조금(그림 14px 이내) 어긋날 수 있다.
        var art = TestPrefabs.StageArt;
        float halfHRaw = StageArtConfig.ViewHalfHeight * StageArtConfig.PixelsPerUnit / (float)art.backgroundScale;
        float halfWRaw = StageArtConfig.ViewHalfWidth * StageArtConfig.PixelsPerUnit / (float)art.backgroundScale;
        Vector2 c = StageVisuals.BackgroundRawCenter(art, StageArtConfig.ViewHalfWidth, CameraFollow2D.MinY);
        float groundRawY = (c.y - halfHRaw) + (1f - StageArtConfig.GroundScreenFraction) * 2f * halfHRaw;
        Assert.AreEqual(art.backgroundGrassYPx, groundRawY, 36f, "그림 속 풀밭이 지면 근처에 와야 한다(테두리를 숨기느라 그림 36px 이내로 어긋날 수 있다)");
        foreach (float camX in new[] { FieldBounds.MinX, FieldBounds.MaxX })
            foreach (float camY in new[] { CameraFollow2D.MinY, CameraFollow2D.MaxY })
            {
                Vector2 v = StageVisuals.BackgroundRawCenter(art, camX, camY);
                Assert.GreaterOrEqual(v.x - halfWRaw, -0.01f, "왼쪽 가장자리가 보인다");
                Assert.LessOrEqual(v.x + halfWRaw, art.background.rect.width + 0.01f, "오른쪽 가장자리가 보인다");
                Assert.GreaterOrEqual(v.y - halfHRaw, art.backgroundTopPx - 0.01f, "위 테두리가 보인다");
                Assert.LessOrEqual(v.y + halfHRaw, art.background.rect.height + 0.01f, "아래 가장자리가 보인다");
            }
    }

    // ---------------- 그림 깔기 ----------------

    [Test]
    public void Build_TilesGroundAndBothPlatformSets_AndHidesOldLines()
    {
        var ground = new GameObject("Ground");
        var gcol = ground.AddComponent<BoxCollider2D>();
        gcol.size = new Vector2(26f, FieldLayout.PlatformThickness * 2f);
        ground.transform.position = new Vector3(13f, FieldBounds.GroundY - FieldLayout.PlatformThickness, 0f);
        var glr = ground.AddComponent<LineRenderer>();

        var normal = NewPlatformSet("TestPlatforms_Normal", boss: false, width: 2.8f, pos: new Vector2(3.2f, 1.35f));
        var boss = NewPlatformSet("TestPlatforms_Boss", boss: true, width: 3.0f, pos: new Vector2(8.9f, 2.7f));
        boss.SetActive(false); // 실제 씬처럼 보스 무대는 꺼진 채 시작

        var visuals = new GameObject("StageVisuals").AddComponent<StageVisuals>();
        visuals.Art = TestPrefabs.StageArt; // 게임 씬과 같은 그림 묶음
        // 실제 씬에선 직렬화 참조(2026-11-05 2b — 예전엔 이름·타입으로 씬을 뒤졌다)
        visuals.Ground = ground.transform;
        visuals.PlatformSets = new[] { normal.GetComponent<PlatformSet>(), boss.GetComponent<PlatformSet>() };
        visuals.Build();
        visuals.Build(); // 두 번 불러도 한 번만 깔린다

        Assert.IsFalse(glr.enabled, "땅의 옛 선이 그대로 보인다");
        var rows = visuals.transform.Find("GroundTiles");
        Assert.IsNotNull(rows);
        Assert.AreEqual(StageVisuals.GroundRows, rows.childCount);
        var (gl, gr, gt) = Extent(rows.GetChild(0));
        Assert.AreEqual(0f, gl, Px, "땅 타일 왼쪽 끝");
        Assert.AreEqual(26f, gr, Px, "땅 타일이 필드 폭을 다 안 덮는다");
        Assert.AreEqual(FieldBounds.GroundY, gt, Px, "땅 타일 윗면이 지면이 아니다");
        Assert.AreEqual(52, rows.GetChild(0).childCount, "땅 한 줄 = 0.5유닛 타일 52장");

        foreach (var set in new[] { normal, boss })
        {
            var platform = set.transform.GetChild(0);
            Assert.IsFalse(platform.GetComponent<LineRenderer>().enabled, $"{set.name} 발판의 옛 선이 그대로다");
            var tiles = platform.Find("Tiles");
            Assert.IsNotNull(tiles, $"{set.name} 발판에 타일이 없다(꺼진 보스 무대도 깔아 둬야 한다)");
            Assert.AreEqual(1, platform.childCount, "두 번 깔렸다");
            var col = platform.GetComponent<BoxCollider2D>();
            var (l, r, t) = Extent(tiles);
            Assert.AreEqual(platform.position.x - col.size.x / 2f, l, Px, "발판 그림 왼쪽 끝이 판정과 다르다");
            Assert.AreEqual(platform.position.x + col.size.x / 2f, r, Px, "발판 그림 오른쪽 끝이 판정과 다르다(마지막 장 자르기)");
            Assert.AreEqual(platform.position.y + col.size.y / 2f, t, Px, "발판 그림 윗면이 밟는 높이와 다르다");
        }

        var bg = visuals.transform.Find("Background");
        Assert.IsNotNull(bg);
        Assert.AreEqual(StageVisuals.BackgroundOrder, bg.GetComponent<SpriteRenderer>().sortingOrder);
        Assert.AreEqual((float)TestPrefabs.StageArt.backgroundScale, bg.localScale.x, 1e-6f, "배경이 정수배가 아니다");
    }

    const float Px = 1f / StageArtConfig.PixelsPerUnit; // 1픽셀

    /// <summary>한 줄로 놓인 타일들의 왼쪽·오른쪽 끝과 윗면(그림 크기로 계산 — 꺼진 오브젝트도 잴 수 있게).</summary>
    static (float left, float right, float top) Extent(Transform strip)
    {
        float l = float.MaxValue, r = float.MinValue, t = float.MinValue;
        foreach (Transform c in strip)
        {
            Vector3 s = c.GetComponent<SpriteRenderer>().sprite.bounds.size;
            l = Mathf.Min(l, c.position.x - s.x / 2f);
            r = Mathf.Max(r, c.position.x + s.x / 2f);
            t = Mathf.Max(t, c.position.y + s.y / 2f);
        }
        return (l, r, t);
    }

    static GameObject NewPlatformSet(string name, bool boss, float width, Vector2 pos)
    {
        var parent = new GameObject(name);
        parent.AddComponent<PlatformSet>().bossArena = boss;
        var p = new GameObject("Platform_0");
        p.transform.SetParent(parent.transform, false);
        p.transform.position = pos;
        p.AddComponent<BoxCollider2D>().size = new Vector2(width, FieldLayout.PlatformThickness);
        p.AddComponent<LineRenderer>();
        return parent;
    }

    // ---------------- 그림 파일 ----------------

    [Test]
    public void FigmaArt_IsImportedPixelExact()
    {
        var set = TestPrefabs.StageArt; // 게임 씬의 StageVisuals가 쓰는 그림 묶음
        AssertSprite(set.groundTop, "ground_top", 64f, new Vector2(0.5f, 0.5f));
        AssertSprite(set.groundDirt, "ground_dirt", 64f, new Vector2(0.5f, 0.5f));
        AssertSprite(set.platformTile, "platform_tile", 64f, new Vector2(0.25f, 0.25f)); // 2026-11-09 도트 통일 — 땅 타일과 같은 PPU 64(도트 2)
        AssertSprite(set.background, "bg_forest", 128f, new Vector2(5f, 2.8125f));
        AssertSprite(set.portal, "portal", 128f, new Vector2(0.25f, 0.5f));
    }

    [Test]
    public void Portal_GetsFigmaArt_AtDotScale_StandingOnGround_KeepsItsArtColors()
    {
        var go = new GameObject("TestPortal");
        go.transform.position = new Vector3(FieldLayout.BossPortalX, FieldBounds.GroundY + FieldLayout.BossPortalHeight / 2f, 0f);
        go.transform.localScale = new Vector3(0.7f, 1.56f, 1f); // 예전 흰 원을 문 모양으로 늘린 배율
        go.AddComponent<SpriteRenderer>();
        var portal = go.AddComponent<BossPortal>();
        var set = TestPrefabs.StageArt;
        var art = set.portal;

        portal.ApplyArt(art, set.portalScale);

        var sr = go.GetComponent<SpriteRenderer>();
        Assert.AreSame(art, sr.sprite);
        Assert.AreEqual(new Vector3(DotScale.Value, DotScale.Value, 1f), go.transform.localScale, "포탈은 공통 도트 배율(2026-11-09 — 예전 1.5배는 사용자 2026-10-24 결정, 도트 통일로 2배)");
        Assert.AreEqual(FieldBounds.GroundY, go.transform.position.y - art.bounds.size.y * set.portalScale / 2f, 1e-4f,
            "포탈이 땅 위에 안 서 있다");
        Assert.AreEqual(FieldLayout.BossPortalX, go.transform.position.x, 1e-4f);
        Assert.AreEqual(Color.white, portal.unlockedColor, "열린 포탈은 그림 색 그대로(밝기만 깜빡) — 보라를 곱하면 탁해진다");
        Assert.AreEqual(BossPortal.LockedArtColor, portal.lockedColor);
    }

    static void AssertSprite(Sprite s, string name, float ppu, Vector2 size)
    {
        Assert.IsNotNull(s, name + " 그림이 없다");
        Assert.AreEqual(name, s.name, "그림 묶음의 칸에 다른 그림이 들어갔다");
        Assert.AreEqual(ppu, s.pixelsPerUnit, 1e-4f, name + " 픽셀/유닛");
        Assert.AreEqual(FilterMode.Point, s.texture.filterMode, name + "이 흐리게(Bilinear) 들어왔다");
        Assert.AreEqual(size.x, s.bounds.size.x, 1e-4f, name + " 가로 크기");
        Assert.AreEqual(size.y, s.bounds.size.y, 1e-4f, name + " 세로 크기");
    }

    [Test]
    public void CharacterArt_IsDrawnOneToOne()
    {
        // 64px 캐릭터가 판정 0.5유닛과 딱 맞으려면 128px/유닛이어야 한다(예전 100이라 0.78배로 줄여 그려 흐렸다).
        foreach (var name in new[] { "char_mage", "char_gunner", "char_blade", "char_druid" })
        {
            string meta = File.ReadAllText(Path.Combine(Application.dataPath, "Sprites", "Prototype", name + ".png.meta"));
            StringAssert.Contains("spritePixelsToUnits: 128", meta, name);
            StringAssert.Contains("filterMode: 0", meta, name + " — 흐림 필터(Bilinear)면 도트가 번진다");
            StringAssert.DoesNotContain("textureCompression: 1", meta, name + " — 압축하면 도트에 얼룩이 생긴다");
        }
        Assert.AreEqual(0.5f, EntitySizeConfig.PlayerHeight, 1e-6f, "판정 높이가 바뀌면 캐릭터 그림 배율도 같이 봐야 한다");
    }
}

}
