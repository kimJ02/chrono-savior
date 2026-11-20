using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChronoSavior.Core;
using ChronoSavior.Combat;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 2026-11-20 최적화 점검(GPT와 같이)에서 넣은 캐시가 **값을 바꾸지 않는지** — 보유·캐릭터·갈래·목록 교체가 바뀌면 바로 따라가야 한다.
/// 함께 고친 버그: 세이브 프리셋이 이번 생 보스 처치를 남겨 회귀 파편이 나오던 것.
/// </summary>
public class LookupCacheTests
{
    [SetUp] public void Setup() { ProfileService.Reset(); RunState.Reset(); }
    [TearDown] public void Teardown() { ProfileService.Reset(); RunState.Reset(); }

    static float ManualAdd(ItemInventory inv, ItemStat stat)
    {
        float sum = 0f;
        foreach (var s in inv.stacks)
        {
            var d = ItemDatabase.Get(s.id);
            if (d != null && d.kind == ItemKind.Stat && d.stat == stat && !d.multiplicative) sum += s.count * d.per;
        }
        return sum;
    }

    static int ManualTag(PlayerProfile p, string tag) =>
        ItemDatabase.All.Count(d => p.items.stacks.Any(s => s.id == d.id && s.count > 0) && d.tags.Contains(tag));

    [Test]
    public void ItemCounts_FollowAdds_AndListReplacement()
    {
        var inv = new ItemInventory();
        Assert.AreEqual(0, inv.Count("sevenSands"));
        inv.Add("sevenSands");
        Assert.AreEqual(1, inv.Count("sevenSands"), "더한 뒤 바로 1");
        inv.Add("sevenSands");
        Assert.AreEqual(2, inv.Count("sevenSands"));

        // 목록을 통째로 바꿔도(불러오기 등) 낡은 값이 남으면 안 된다
        inv.stacks = new System.Collections.Generic.List<ItemStack> { new ItemStack { id = "rewindCore", count = 3 } };
        Assert.AreEqual(0, inv.Count("sevenSands"));
        Assert.AreEqual(3, inv.Count("rewindCore"));

        foreach (ItemStat st in System.Enum.GetValues(typeof(ItemStat)))
            Assert.AreEqual(ManualAdd(inv, st), inv.Add(st), 1e-5f, $"{st} 합이 캐시와 다르다");
    }

    [Test]
    public void RelicRank_FollowsBranchAndCharacter_AndSetsCount()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        p.mageBranch = MageBranch.None;
        p.items.Add("rewindCore");
        Assert.AreEqual(0, Relics.Rank("rewindCore"), "중력 갈래가 아니면 꺼짐");
        int before = Relics.StateVersion;

        p.mageBranch = MageBranch.Gravity; // 필드를 바로 바꿔도 캐시가 따라간다
        Assert.AreEqual(1, Relics.Rank("rewindCore"));
        Assert.AreNotEqual(before, Relics.StateVersion, "상태가 바뀌면 번호가 오른다(전투 화면 유물 칸이 다시 그린다)");

        p.items.Add("rewindCore");
        Assert.AreEqual(2, Relics.Rank("rewindCore"), "하나 더 얻으면 바로 랭크 2");
        Assert.AreEqual(1, Relics.CharacterSetCount(), "법사 전용 1종");

        p.character = CharacterId.Gunner;
        Assert.AreEqual(0, Relics.Rank("rewindCore"), "캐릭터가 바뀌면 법사 전용은 꺼짐");
        Assert.AreEqual(0, Relics.CharacterSetCount());

        p.items.Add("sevenSands");
        Assert.AreEqual(ManualTag(p, "시간"), Relics.TagCount("시간"));
        Assert.AreEqual(ManualTag(p, "연쇄"), Relics.TagCount("연쇄"));

        // 프로필이 바뀌면(슬롯 불러오기) 새 프로필 기준
        ProfileService.Replace(new PlayerProfile());
        Assert.AreEqual(0, Relics.Rank("sevenSands"));
        Assert.AreEqual(0, Relics.TagCount("시간"));
    }

    [Test]
    public void Presets_DoNotCountAsThisLifeBossKills()
    {
        // 슬롯 2·3은 "이미 깬 진행"이다 — 보스를 안 잡고 회귀하면 파편 0(예전엔 1·3이 나왔다)
        Assert.AreEqual(0, SavePresets.Build(2).ShardPreview());
        Assert.AreEqual(0, SavePresets.Build(3).ShardPreview());
        Assert.IsTrue(SavePresets.Build(3).IsBossCleared(2), "정복 표시는 그대로");
    }

    /// <summary>유물 지연 대기열 — 정리(런 시작) 직후 같은 프레임에 건 예약도 사라지지 않는다.</summary>
    [UnityTest]
    public IEnumerator DelayedRelicAction_SurvivesCleanupInSameFrame()
    {
        bool early = false, late = false;
        RelicFx.After(10f, () => early = true);
        RunTransient.DestroyAllIn(SceneManager.GetActiveScene()); // 런 시작 정리 — 옛 대기열은 프레임 끝에 지워진다
        RelicFx.After(0f, () => late = true);                     // 같은 프레임에 건 예약(입장 유물)
        for (int i = 0; i < 4 && !late; i++) yield return null;
        Assert.IsTrue(late, "정리 직후 건 예약이 사라졌다");
        Assert.IsFalse(early, "정리 전 예약은 런과 같이 사라진다");
    }
}

}
