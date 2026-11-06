using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 캐릭터 그림 에셋(`Core/CharacterArtSet`, 2026-11-05 2a) — 예전엔 `PlayerRig`(전투 씬)의 배열이었다. 플레이어는 고른 캐릭터의 그림·색을
/// 에셋에서 읽고, 그림 높이를 콜라이더 지름에 맞춘다(전과 같다). 실제 씬의 연결은 `GameBootTests`가 본다.
/// </summary>
public class CharacterArtTests
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

    static Sprite NewSprite(int heightPx) => Sprite.Create(new Texture2D(8, heightPx), new Rect(0, 0, 8, heightPx), new Vector2(0.5f, 0f), 100f);

    [Test]
    public void ArtSet_ReadsByCharacter_AndTreatsEmptyTintAsWhite()
    {
        var art = ScriptableObject.CreateInstance<CharacterArtSet>();
        Assert.AreEqual(System.Enum.GetValues(typeof(CharacterId)).Length, art.sprites.Length, "배열은 캐릭터 수만큼");
        var gunner = NewSprite(16);
        art.sprites[(int)CharacterId.Gunner] = gunner;
        art.tints[(int)CharacterId.Gunner] = Color.red;

        Assert.AreSame(gunner, art.SpriteOf(CharacterId.Gunner));
        Assert.AreEqual(Color.red, art.TintOf(CharacterId.Gunner));
        Assert.IsNull(art.SpriteOf(CharacterId.Mage), "비어 있으면 null — 플레이어는 기본 스프라이트를 그대로 쓴다");
        Assert.AreEqual(Color.white, art.TintOf(CharacterId.Mage), "알파 0(비어 있음)은 흰색(덧칠 없음)");

        art.sprites = new Sprite[1]; // 길이가 모자라도 안전
        Assert.IsNull(art.SpriteOf(CharacterId.Blade));
        Object.DestroyImmediate(art);
    }

    /// <summary>
    /// 그림의 화면 높이(그림 에셋)와 판정 크기(`EntitySizeConfig`)는 따로다(2026-11-06 3.5-d) — 그림 높이를 바꿔도, 픽셀 수·PPU가 다른 그림으로 바꿔도
    /// 판정 반지름은 그대로다(외주 그림으로 갈아끼울 때 게임이 바뀌지 않게).
    /// </summary>
    [UnityTest]
    public IEnumerator Player_ArtHeight_AndHitbox_AreIndependent()
    {
        var go = new GameObject("TestPlayer");
        var sr = go.AddComponent<SpriteRenderer>();
        var col = go.AddComponent<CircleCollider2D>();
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        go.AddComponent<MageAttack>().Equipped();
        go.AddComponent<GunnerAttack>().Equipped();
        var rig = go.AddComponent<PlayerRig>();

        var art = ScriptableObject.CreateInstance<CharacterArtSet>();
        art.sprites[(int)CharacterId.Gunner] = Sprite.Create(new Texture2D(16, 96), new Rect(0, 0, 16, 96), new Vector2(0.5f, 0f), 128f); // 다른 크기·PPU
        art.heights[(int)CharacterId.Gunner] = 0.8f; // 그림만 키운다
        rig.Art = art;
        ProfileService.Current.character = CharacterId.Gunner;
        yield return null;
        yield return null;

        Assert.AreEqual(0.8f, sr.bounds.size.y, 1e-4f, "그림은 그림 에셋의 높이로 보인다");
        Assert.AreEqual(EntitySizeConfig.PlayerRadius, col.radius * go.transform.lossyScale.x, 1e-4f, "판정 반지름은 그림과 상관없이 그대로");
        Object.DestroyImmediate(art);
    }

    /// <summary>캐릭터가 바뀌면 그 캐릭터의 그림·색으로 갈아입고, (그림 높이가 비어 있으면 판정 높이로) 그림 높이가 콜라이더 지름이 되게 맞춘다.</summary>
    [UnityTest]
    public IEnumerator Player_WearsTheSelectedCharactersArt()
    {
        var go = new GameObject("TestPlayer");
        var sr = go.AddComponent<SpriteRenderer>();
        var col = go.AddComponent<CircleCollider2D>();
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        go.AddComponent<MageAttack>().Equipped();
        go.AddComponent<GunnerAttack>().Equipped();
        var rig = go.AddComponent<PlayerRig>(); // 키트를 다 붙인 뒤에(Awake에서 찾는다)

        var art = ScriptableObject.CreateInstance<CharacterArtSet>();
        art.sprites[(int)CharacterId.Mage] = NewSprite(32);
        art.sprites[(int)CharacterId.Gunner] = NewSprite(64);
        art.tints[(int)CharacterId.Gunner] = new Color(0.2f, 0.8f, 1f);
        rig.Art = art;
        yield return null;

        ProfileService.Current.character = CharacterId.Gunner;
        yield return null;
        Assert.AreSame(art.SpriteOf(CharacterId.Gunner), sr.sprite, "메카닉 그림으로 갈아입는다");
        Assert.AreEqual(art.TintOf(CharacterId.Gunner), sr.color);
        float worldDiameter = col.radius * 2f * go.transform.localScale.y;
        Assert.AreEqual(EntitySizeConfig.PlayerHeight, worldDiameter, 1e-4f, "그림 높이 = 콜라이더 지름(보이는 곳 = 맞는 곳)");

        ProfileService.Current.character = CharacterId.Mage;
        yield return null;
        Assert.AreSame(art.SpriteOf(CharacterId.Mage), sr.sprite);
        Assert.AreEqual(Color.white, sr.color, "마법사 색은 비어 있어 흰색");
        Object.DestroyImmediate(art);
    }
}

}
