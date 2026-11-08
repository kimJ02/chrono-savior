using System.Collections;
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
/// 잔상(2026-10-24 사용자 "섬영 깜빡임 말고 다른 방식" → 원본 `player.afterimages`) — 섬영이 빠를 때·칼날폭풍 중·마법사
/// 순간이동 출발 자리에 푸른 반투명 그림이 0.25초 남는다(project_test.html:2147·:2815·:2877·:3502·:6192).
/// </summary>
public class AfterimageTests
{
    [SetUp]
    public void Setup()
    {
        ProfileService.Reset();
        GameState.Set(GameScene.Run);
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null && (go.name.StartsWith("Test") || go.name == "Afterimage"))
                Object.DestroyImmediate(go);
        foreach (var f in Object.FindObjectsByType<FireTrailZone>(FindObjectsSortMode.None))
            if (f != null) Object.DestroyImmediate(f.gameObject);
        ProfileService.Reset();
    }

    static Sprite TestSprite()
    {
        var tex = new Texture2D(8, 8);
        return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 16f);
    }

    static PlayerAfterimages NewBody(Vector3 pos)
    {
        var go = new GameObject("TestBody");
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>().sprite = TestSprite();
        go.AddComponent<CircleCollider2D>().radius = 0.25f;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll; // 위치는 테스트가 직접 옮긴다(칼날폭풍처럼)
        go.AddComponent<CharacterMover2D>();
        return go.AddComponent<PlayerAfterimages>().Equipped();
    }

    [UnityTest]
    public IEnumerator Ghosts_AreCleared_WhenGoingToLobby()
    {
        var ai = NewBody(new Vector3(2f, 1f, 0f));
        ai.Spawn(ai.transform.position);
        ai.Spawn(ai.transform.position + Vector3.right);
        Assert.AreEqual(2, ai.Count);
        GameState.Set(GameScene.Lobby); // 시간이 멈춰도(dt 0) 남지 않아야 한다
        yield return null;
        Assert.AreEqual(0, ai.Count, "로비 하늘에 잔상이 굳어 남던 버그(2026-10-25)");
    }

    [Test]
    public void Chance_MatchesOriginal_At60fps_AndKeepsRatePerSecond()
    {
        const float f60 = 1f / 60f;
        Assert.AreEqual(0f, PlayerAfterimages.ChancePerFrame(0.55f, false, f60), "비율 0.55 이하면 안 남긴다(원본 `> 0.55`)");
        Assert.AreEqual(0.8f * 0.7f, PlayerAfterimages.ChancePerFrame(0.8f, false, f60), 1e-5f, "원본 비율 × 0.7(:2876)");
        Assert.AreEqual(0.8f, PlayerAfterimages.ChancePerFrame(0f, true, f60), 1e-5f, "칼날폭풍은 80%(원본 :2815)");
        float p120 = PlayerAfterimages.ChancePerFrame(0.8f, false, 1f / 120f);
        Assert.AreEqual(0.56f, 1f - (1f - p120) * (1f - p120), 1e-4f, "120fps 두 프레임 = 60fps 한 프레임");
    }

    [UnityTest]
    public IEnumerator Ghost_StaysWhereLeft_BlueTranslucent_GoneAfter025s()
    {
        var ai = NewBody(new Vector3(2f, 1f, 0f));
        var body = ai.GetComponent<SpriteRenderer>();
        body.flipX = true;
        ai.Spawn(ai.transform.position);

        var g = ai.GhostRenderer(0);
        Assert.AreEqual(body.sprite, g.sprite, "지금 그림 그대로");
        Assert.IsTrue(g.flipX, "보는 방향 그대로");
        Assert.AreEqual(PlayerAfterimages.StartAlpha, g.color.a, 1e-5f, "원본 처음 투명도 0.3");
        Assert.AreEqual(PlayerAfterimages.GhostColor.b, g.color.b, 1e-5f, "원본 유령 색(푸른빛)");
        Assert.Less(g.sortingOrder, body.sortingOrder, "캐릭터 뒤에 그린다");

        ai.transform.position += new Vector3(1f, 0f, 0f);
        yield return null;
        yield return null;
        Assert.AreEqual(2f, g.transform.position.x, 1e-4f, "잔상은 캐릭터를 따라가지 않는다");
        Assert.Less(g.color.a, PlayerAfterimages.StartAlpha, "점점 옅어진다");

        yield return new WaitForSeconds(0.35f);
        Assert.AreEqual(0, ai.Count, "0.25초 뒤엔 사라진다(원본 :3504)");
    }

    [UnityTest]
    public IEnumerator Blade_GlidingFast_LeavesGhosts_Mage_DoesNot()
    {
        foreach (var who in new[] { CharacterId.Mage, CharacterId.Blade })
        {
            ProfileService.Current.character = who;
            var ai = NewBody(new Vector3(0f, 1f, 0f));
            int most = 0;
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                yield return null;
                ai.transform.position += new Vector3(8f * Time.deltaTime, 0f, 0f); // 물리 속도 없이 위치만(칼날폭풍 활공)
                most = Mathf.Max(most, ai.Count);
            }
            if (who == CharacterId.Blade) Assert.Greater(most, 0, "섬영 활공 중인데 잔상이 없다(원본 :2815)");
            else Assert.AreEqual(0, most, "섬영이 아닌데 잔상이 생겼다");
            Object.DestroyImmediate(ai.gameObject);
        }
    }

    [Test]
    public void MageTeleport_LeavesOneGhost_AtDeparture()
    {
        var go = new GameObject("TestMage");
        go.transform.position = new Vector3(5f, 0.25f, 0f);
        go.AddComponent<CircleCollider2D>().radius = 0.25f;
        var rb = go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterMover2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        go.AddComponent<SpriteRenderer>().sprite = TestSprite();
        var ai = go.AddComponent<PlayerAfterimages>().Equipped();
        var mage = go.AddComponent<MageAttack>().Equipped();
        float startX = go.transform.position.x;

        typeof(MageAttack).GetMethod("TeleportTowards", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(mage, new object[] { 1f, 0f, 1, 1 });
        typeof(MageAttack).GetMethod("UpdateDash", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(mage, new object[] { 1f }); // 화염돌진을 끝까지(2026-11-09)

        Assert.AreNotEqual(startX, go.transform.position.x, "전제: 돌진해 옮겨 갔다");
        Assert.AreEqual(1 + MageAttack.DashGhosts, ai.Count, "출발 자리 잔상(원본 :2147) + 돌진 중 잔상");
        Assert.AreEqual(startX, ai.GhostRenderer(0).transform.position.x, 1e-4f, "잔상은 출발 자리에");
    }
}

}
