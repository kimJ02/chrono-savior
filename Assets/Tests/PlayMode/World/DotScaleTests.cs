using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// **화면의 도트 크기는 하나다**(2026-11-09 사용자, 배율 2 — `Core.DotScale`). 월드에 그려지는 실제 그림이 모두 같은 도트 배율인지 에셋 값으로 확인한다.
/// 그림 없는 임시 도형 몹(128px 매끈한 도형)은 그림이 들어올 때 맞춘다(제외). 이펙트는 `MageVfxTests`·`MechanicAssassinVfxTests`가 배율을 본다.
/// </summary>
public class DotScaleTests
{
#if UNITY_EDITOR
    static T Load<T>(string path) where T : Object
    {
        var a = UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
        Assert.IsNotNull(a, path);
        return a;
    }

    /// <summary>그림 높이(유닛)가 그 그림을 도트 배율로 그린 높이인가.</summary>
    static void AssertDot(string what, Sprite sprite, float worldHeight)
    {
        Assert.AreEqual(DotScale.Units(sprite.rect.height), worldHeight, 1e-3f, $"{what}: 도트 배율 {DotScale.Value}이 아니다");
    }

    [Test]
    public void Characters_AreDrawnAtDotScale()
    {
        var art = Load<CharacterArtSet>("Assets/Data/Characters/CharacterArtSet.asset");
        foreach (CharacterId id in System.Enum.GetValues(typeof(CharacterId)))
            if (art.SpriteOf(id) != null) AssertDot("캐릭터 " + id, art.SpriteOf(id), art.HeightOf(id));
    }

    [Test]
    public void EnemiesWithRealArt_AreDrawnAtDotScale()
    {
        var catalog = Load<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
        int checkedCount = 0;
        foreach (var data in catalog.types)
        {
            if (data == null || data.sprite == null) continue; // 임시 도형 몹은 그림이 들어올 때
            AssertDot("몹 " + data.name, data.sprite, data.visualHeight);
            checkedCount++;
        }
        Assert.Greater(checkedCount, 0, "그림 있는 몹이 하나는 있어야 한다(고블린·박쥐)");
    }

    [Test]
    public void Stage_BackgroundPortalAndTiles_AreAtDotScale()
    {
        var art = Load<StageArtSet>("Assets/Data/Stages/StageArt_Forest.asset");
        Assert.AreEqual(DotScale.Value * art.background.pixelsPerUnit / DotScale.GridPixelsPerUnit, art.backgroundScale, 1e-4f, "배경");
        Assert.AreEqual(DotScale.Value * art.portal.pixelsPerUnit / DotScale.GridPixelsPerUnit, art.portalScale, 1e-4f, "포탈");
        foreach (var tile in new[] { art.groundTop, art.groundDirt, art.platformTile })
            Assert.AreEqual(DotScale.GridPixelsPerUnit / DotScale.Value, tile.pixelsPerUnit, 1e-4f, tile.name + ": 타일은 PPU 64(그대로 깔면 도트 2)");
    }

    /// <summary>캐릭터 그림 발이 판정 원 바닥(발)에 맞는다 — 기준점이 아래에서 (판정 반지름 ÷ 그림 높이).</summary>
    [Test]
    public void CharacterSprite_FeetSitOnTheGround()
    {
        var art = Load<CharacterArtSet>("Assets/Data/Characters/CharacterArtSet.asset");
        var s = art.SpriteOf(CharacterId.Mage);
        float pivotFromBottom = s.pivot.y / s.rect.height * art.HeightOf(CharacterId.Mage);
        Assert.AreEqual(EntitySizeConfig.PlayerRadius, pivotFromBottom, 0.01f, "그림 발 = 판정 원 바닥");
    }
#endif
}

}
