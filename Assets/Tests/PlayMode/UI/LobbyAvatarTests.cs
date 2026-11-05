using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.UI;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 로비 아바타(2026-11-05 씬 분할 2차 3-1) — 그림만 있는 로비 캐릭터. 세이브의 지금 캐릭터를 따라 같은 그림 에셋에서 그림·색을 입고,
/// 전투의 플레이어와 같은 높이로 맞춘다. 키트·체력·물리·Player 태그는 없다.
/// </summary>
public class LobbyAvatarTests
{
    [SetUp]
    public void Setup() => ProfileService.Reset();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        ProfileService.Reset();
    }

    static Sprite NewSprite(int heightPx) => Sprite.Create(new Texture2D(8, heightPx), new Rect(0, 0, 8, heightPx), new Vector2(0.5f, 0.5f), 100f);

    [UnityTest]
    public IEnumerator Avatar_WearsTheProfileCharacter_AndFollowsAChange()
    {
        var art = ScriptableObject.CreateInstance<CharacterArtSet>();
        art.sprites[(int)CharacterId.Mage] = NewSprite(32);
        art.sprites[(int)CharacterId.Gunner] = NewSprite(64);
        art.tints[(int)CharacterId.Gunner] = new Color(0.2f, 0.8f, 1f);

        var go = new GameObject("TestAvatar");
        var sr = go.AddComponent<SpriteRenderer>();
        var avatar = go.AddComponent<LobbyAvatar>();
        avatar.Art = art;
        yield return null;
        Assert.AreSame(art.SpriteOf(CharacterId.Mage), sr.sprite, "기본 캐릭터(마법사) 그림");
        Assert.AreEqual(EntitySizeConfig.PlayerHeight, sr.sprite.bounds.size.y * go.transform.localScale.y, 1e-4f, "전투의 플레이어와 같은 높이");

        ProfileService.Current.character = CharacterId.Gunner; // 로비 캐릭터 선택은 이 값만 바꾼다
        yield return null;
        Assert.AreSame(art.SpriteOf(CharacterId.Gunner), sr.sprite, "다음 프레임에 메카닉 그림으로 갈아입는다");
        Assert.AreEqual(art.TintOf(CharacterId.Gunner), sr.color);
        Assert.AreEqual(EntitySizeConfig.PlayerHeight, sr.sprite.bounds.size.y * go.transform.localScale.y, 1e-4f, "그림 크기가 달라도 같은 높이");

        Assert.IsNull(go.GetComponent<Collider2D>(), "로비 아바타엔 물리가 없다");
        Assert.AreNotEqual("Player", go.tag, "Player 태그가 아니다(전투의 플레이어와 헷갈리지 않게)");
        Object.DestroyImmediate(art);
    }

    [Test]
    public void ScaleToHeight_IsSharedWithThePlayer()
    {
        Assert.AreEqual(0.5f / 0.64f, EntitySizeConfig.ScaleToHeight(NewSprite(64), 0.5f), 1e-5f);
        Assert.AreEqual(0f, EntitySizeConfig.ScaleToHeight(null, 0.5f), "그림이 없으면 0(건드리지 않는다)");
    }
}

}
