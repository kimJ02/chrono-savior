using System.IO;
using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 세이브 슬롯 3개(2026-11-19, 교수님 지시 — 플레이테스트 시나리오): 시나리오 내용, 슬롯마다 따로 저장, 초기화, 깨진 파일 복구.
/// 실제 세이브 폴더를 안 건드리게 임시 폴더를 쓴다.
/// </summary>
public class SaveSlotTests
{
    string dir;

    [SetUp]
    public void Setup()
    {
        dir = Path.Combine(Path.GetTempPath(), "chrono_slot_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        SaveService.SlotDirOverride = dir;
        SaveService.ClearSlotForTests();
        ProfileService.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        SaveService.SlotDirOverride = TestSaveSandbox.SlotDir; // 테스트 전체 임시 폴더로 되돌린다
        SaveService.ClearSlotForTests();
        ProfileService.Reset();
        try { Directory.Delete(dir, true); } catch { }
    }

    [Test]
    public void Presets_MatchScenarios()
    {
        var fresh = SavePresets.Build(1);
        Assert.IsTrue(fresh.needsCharacterSelect, "새 출발은 캐릭터부터 고른다");
        Assert.AreEqual(1, fresh.level);
        Assert.AreEqual(0, fresh.items.stacks.Count);

        var mid = SavePresets.Build(2);
        Assert.AreEqual(CharacterId.Mage, mid.character);
        Assert.AreEqual(MageBranch.Gravity, mid.mageBranch);
        Assert.AreEqual(5, mid.mageTier, "성장 — 스킬만 5층(2026-11-19 사용자)");
        Assert.AreEqual(16, mid.level);
        Assert.AreEqual(0, mid.upgrades.atk + mid.upgrades.hp + mid.upgrades.ms + mid.upgrades.atkSpeed + mid.upgrades.crit, "성장 슬롯은 강화 없음");
        Assert.AreEqual(0, mid.items.stacks.Count, "성장 슬롯엔 유물이 없다");

        var full = SavePresets.Build(3);
        Assert.AreEqual(MageBranch.Gravity, full.mageBranch);
        Assert.AreEqual(5, full.mageTier, "풀세팅 — 중력 5층");
        foreach (var d in ItemDatabase.All)
            Assert.AreEqual(ItemDatabase.Cap(d.grade), full.items.Count(d.id), $"{d.id}가 각성(상한)이 아니다");
        Assert.AreEqual(1f, Relics.Value(full, "spawnCap", 0), 0.001f, "요기 응집석 각성 = 몬스터 상한 2배");
        Assert.AreEqual(1f, Relics.Value(full, "spawnRate", 0), 0.001f, "황천의 문 각성 = 나오는 빈도 2배");
        Assert.AreEqual(2f, full.spawnScale, 0.001f, "슬롯 3 전용 몬스터 2배 더(2026-11-19)");
        Assert.AreEqual(1f, mid.spawnScale, 0.001f, "다른 슬롯은 그대로");
    }

    [Test]
    public void EnsureSlots_CreatesThree_AndSlotsSaveSeparately()
    {
        SaveService.EnsureSlots();
        for (int i = 1; i <= SaveService.SlotCount; i++) Assert.IsTrue(SaveService.SlotExists(i));

        Assert.IsTrue(SaveService.SelectSlot(2));
        Assert.AreEqual(16, ProfileService.Current.level);
        ProfileService.Current.gold = 777;
        SaveService.Save();

        Assert.IsTrue(SaveService.SelectSlot(3));
        Assert.AreEqual(25, ProfileService.Current.level, "슬롯 3을 골랐는데 다른 슬롯이 열렸다");
        Assert.AreEqual(777, SaveService.Peek(2).gold, "슬롯 2 진행이 따로 남아야 한다");
        Assert.AreNotEqual(777, SaveService.Peek(3).gold);
    }

    [Test]
    public void ResetSlot_RestoresScenario()
    {
        SaveService.EnsureSlots();
        SaveService.SelectSlot(2);
        ProfileService.Current.gold = 999999;
        SaveService.Save();

        SaveService.ResetSlot(2);
        Assert.AreEqual(SavePresets.Build(2).gold, ProfileService.Current.gold, "초기화하면 지금 프로필도 시나리오 처음으로");
        Assert.AreEqual(SavePresets.Build(2).gold, SaveService.Peek(2).gold);
    }

    [Test]
    public void CorruptSlot_FallsBackToScenario()
    {
        File.WriteAllText(SaveService.SlotPath(3), "{ 깨진 파일");
        Assert.IsTrue(SaveService.SelectSlot(3));
        Assert.AreEqual(5, ProfileService.Current.mageTier, "깨진 슬롯은 시나리오 처음 상태로 되살아나야 한다");
    }
}

}
