using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 스테이지 선택 화면의 몬스터 카드(2026-11-05 씬 분할 2차 2a). 화면은 스포너(전투 씬)를 찾지 않고 같은 몬스터 목록 에셋(`MonsterCatalog`)을 읽는다.
/// 카드에 보여 줄 종류(`StageSelectScreen.MonstersOf`)는 스폰 확률표(`Systems/EnemySpawnTable`)의 복사본이 아니라 **표시 정책**(칸 5개)이라,
/// 둘이 어긋나지 않게 여기서 묶는다 — 보여 주는 건 그 지역에서 실제로 나오고, 칸이 남으면 나오는 건 다 보여 준다.
/// </summary>
public class StageSelectShowcaseTests
{
    const int Slots = 5;

    /// <summary>그 지역에서 실제로 스폰될 수 있는 종류 — 기본 확률표를 촘촘히 훑고, 2지역부터 오우거(10% 예외, 원본 :3944).</summary>
    static HashSet<EnemyType> Spawnable(int region)
    {
        var set = new HashSet<EnemyType>();
        for (int i = 0; i < 1000; i++) set.Add(EnemySpawnTable.RollBase(region, i / 1000f));
        if (region >= EnemySpawnTable.BigOniMinRegion) set.Add(EnemyType.Brute);
        return set;
    }

    [Test]
    public void ShownMonsters_ActuallySpawnInThatRegion()
    {
        for (int r = 1; r <= RegionConfig.Count; r++)
        {
            var shown = StageSelectScreen.MonstersOf(r);
            var spawnable = Spawnable(r);
            Assert.LessOrEqual(shown.Length, Slots, $"{r}지역 카드가 칸({Slots})보다 많다");
            Assert.AreEqual(shown.Length, shown.Distinct().Count(), $"{r}지역 카드에 같은 몹이 두 번");
            foreach (var t in shown)
                Assert.IsTrue(spawnable.Contains(t), $"{r}지역 카드에 {t}가 있는데 그 지역에선 안 나온다(스폰표와 어긋남)");
            if (spawnable.Count <= Slots)
                CollectionAssert.AreEquivalent(spawnable, shown, $"{r}지역 — 칸이 남는데 나오는 몹을 다 안 보여 준다");
        }
    }

    /// <summary>카드 순서는 지금 화면 그대로 — 원본 `rollSpawnType` 순서, 4지역부터는 칸이 모자라 오우거를 뺀다.</summary>
    [Test]
    public void ShownMonsters_KeepTheirOrder()
    {
        CollectionAssert.AreEqual(new[] { EnemyType.Flyer, EnemyType.Grunt }, StageSelectScreen.MonstersOf(1));
        CollectionAssert.AreEqual(new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Brute }, StageSelectScreen.MonstersOf(2));
        CollectionAssert.AreEqual(new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Shooter, EnemyType.Brute }, StageSelectScreen.MonstersOf(3));
        var late = new[] { EnemyType.Flyer, EnemyType.Grunt, EnemyType.Charger, EnemyType.Shooter, EnemyType.Splitter };
        for (int r = 4; r <= RegionConfig.Count; r++) CollectionAssert.AreEqual(late, StageSelectScreen.MonstersOf(r), $"{r}지역");
    }

    /// <summary>화면이 몬스터 목록 에셋에서 그림을 읽는다 — 전투 씬(스포너)이 없어도.</summary>
    [UnityTest]
    public IEnumerator Cards_ShowSpritesFromTheCatalog_WithoutTheCombatScene()
    {
        Assert.IsNull(Object.FindFirstObjectByType<EnemySpawner>(), "이 테스트는 스포너 없이 돌아야 한다");
        ProfileService.Reset();
        UiModal.Reset();
        GameState.Set(GameScene.Lobby);
        var pc = PixelCanvas.Create();
        try
        {
            var flyer = NewData(EnemyType.Flyer);
            var grunt = NewData(EnemyType.Grunt);
            var catalog = ScriptableObject.CreateInstance<MonsterCatalog>();
            catalog.types = new[] { flyer, grunt };
            catalog.boss = NewSprite();
            var screen = pc.GetComponent<StageSelectScreen>();
            screen.Catalog = catalog;

            StageSelectScreen.Open();
            yield return null;
            yield return null;

            var mobs = (Image[])typeof(StageSelectScreen).GetField("mobs", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screen);
            var boss = (Image)typeof(StageSelectScreen).GetField("boss", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(screen);
            Assert.AreSame(flyer.sprite, mobs[0].sprite, "1지역 첫 칸 = 박쥐");
            Assert.AreSame(grunt.sprite, mobs[1].sprite, "1지역 둘째 칸 = 고블린");
            Assert.IsFalse(mobs[2].enabled, "1지역은 두 종류뿐 — 나머지 칸은 숨긴다");
            Assert.AreSame(catalog.boss, boss.sprite);
            Assert.IsTrue(boss.enabled);
        }
        finally
        {
            StageSelectScreen.Close();
            Object.DestroyImmediate(pc.gameObject);
            foreach (var es in Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
                Object.DestroyImmediate(es.gameObject);
            UiModal.Reset();
            GameState.Reset();
        }
    }

    static EnemyData NewData(EnemyType type)
    {
        var d = ScriptableObject.CreateInstance<EnemyData>();
        d.type = type;
        d.sprite = NewSprite();
        return d;
    }

    static Sprite NewSprite() => Sprite.Create(new Texture2D(4, 4), new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
}

}
