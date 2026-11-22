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

    /// <summary>캐릭터 그림은 도트 1 — 2026-11-18 사용자 "배경·포탈만 도트 2, 나머지는 1"(그림 높이 0.75 → 0.375). 몹·이펙트는 아직 <see cref="DotScale.Value"/>.</summary>
    const float CharacterDot = 1f;

    /// <summary>그림 높이(유닛)가 그 그림을 도트 배율로 그린 높이인가(<paramref name="dot"/>을 안 주면 화면 기본 배율).</summary>
    static void AssertDot(string what, Sprite sprite, float worldHeight, float dot = -1f)
    {
        float d = dot < 0f ? DotScale.Value : dot;
        Assert.AreEqual(sprite.rect.height * d / DotScale.GridPixelsPerUnit, worldHeight, 1e-3f, $"{what}: 도트 배율 {d}이 아니다");
    }

    [Test]
    public void Characters_AreDrawnAtDotScale()
    {
        var art = Load<CharacterArtSet>("Assets/Data/Characters/CharacterArtSet.asset");
        foreach (CharacterId id in System.Enum.GetValues(typeof(CharacterId)))
            if (art.SpriteOf(id) != null)
                // 애니메이션 그림이 있는 정식 캐릭터(마법사·메카닉·살수)는 도트 1, 임시 그림(드루이드 — 기획에서 빠짐)은 화면 기본 배율 그대로
                AssertDot("캐릭터 " + id, art.SpriteOf(id), art.HeightOf(id), art.AnimSetOf(id) != null ? CharacterDot : -1f);
    }

    [Test]
    public void EnemiesWithRealArt_AreDrawnAtDotScale()
    {
        var catalog = Load<MonsterCatalog>("Assets/Data/Enemies/MonsterCatalog.asset");
        int checkedCount = 0;
        foreach (var data in catalog.types)
        {
            if (data == null) continue;
            // 지역 그림(2026-11-17 팀원 몬스터 8종) — 칸마다 모든 프레임이 PPU 128이고 높이 = 픽셀 × 그 칸의 도트 배율(보통 2, 오크통 4 — 덩치 큰 종류 예외).
            if (data.regionSkins != null && data.regionSkins.Length > 0)
            {
                foreach (var skin in data.regionSkins)
                {
                    Assert.IsTrue(skin.dotScale == DotScale.Value || data.type == EnemyType.Brute, $"몹 {data.name}: 도트 {skin.dotScale} — 2가 아닌 건 오우거 자리만");
                    var frames = new System.Collections.Generic.List<Sprite>(skin.move.frames) { skin.hit };
                    foreach (var f in frames)
                    {
                        Assert.IsNotNull(f, $"몹 {data.name}: 빈 프레임");
                        Assert.AreEqual(DotScale.GridPixelsPerUnit, f.pixelsPerUnit, 1e-4f, $"몹 {data.name} {f.name}: PPU 128");
                        Assert.AreEqual(skin.move.frames[0].rect.size, f.rect.size, $"몹 {data.name} {f.name}: 프레임 크기가 같아야 움직일 때 안 흔들린다");
                    }
                    Assert.AreEqual(skin.move.frames[0].rect.height * skin.dotScale / DotScale.GridPixelsPerUnit, skin.VisualHeight, 1e-4f);
                    checkedCount++;
                }
                continue; // 지역 그림이 모든 지역을 덮으면 옛 sprite는 안 쓰인다
            }
            if (data.sprite == null) continue; // 임시 도형 몹은 그림이 들어올 때
            AssertDot("몹 " + data.name, data.sprite, data.visualHeight);
            checkedCount++;
        }
        Assert.Greater(checkedCount, 0, "그림 있는 몹이 하나는 있어야 한다");
    }

    [Test]
    public void Stage_BackgroundPortalAndTiles_AreAtDotScale()
    {
        var art = Load<StageArtSet>("Assets/Data/Stages/StageArt_Forest.asset");
        Assert.AreEqual(DotScale.Value * art.background.pixelsPerUnit / DotScale.GridPixelsPerUnit, art.backgroundScale, 1e-4f, "배경");
        Assert.AreEqual(DotScale.Value * art.portal.pixelsPerUnit / DotScale.GridPixelsPerUnit, art.portalScale, 1e-4f, "포탈");
        foreach (var tile in new[] { art.groundTop, art.groundDirt, art.platformTile })
            Assert.AreEqual(DotScale.GridPixelsPerUnit, tile.pixelsPerUnit, 1e-4f, tile.name + ": 타일은 PPU 128(그대로 깔면 도트 1 — 2026-11-18 사용자, 배경·포탈만 도트 2)");
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
