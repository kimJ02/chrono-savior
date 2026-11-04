using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 캐릭터 키트(<see cref="PlayerRig"/>)가 **세이브의 선택 캐릭터를 항상 따라가는지** — 2026-09-28 사용자 발견:
/// 메카닉을 골랐는데 전문화 탭엔 마법사 트리가 뜨고, 사냥에선 메카닉 공격이 나가고, 로비로 돌아오니 마법사로
/// 선택돼 있었다. 원인은 프로필이 통째로 바뀔 때(자동 불러오기·"새 원정"·"전체 초기화") 키트를 다시 맞추지
/// 않은 것과, 이미 선택된 캐릭터를 다시 누르면 프로필을 안 고치고 끝나던 것.
/// </summary>
public class PlayerRigProfileSyncTests
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

    static (PlayerRig rig, MageAttack mage, GunnerAttack gunner) NewPlayer()
    {
        var go = new GameObject("TestPlayer");
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = EntitySizeConfig.PlayerRadius;
        var rb = go.AddComponent<Rigidbody2D>();
        go.AddComponent<CharacterMover2D>();
        rb.gravityScale = 0f;
        // 키트를 전부 붙인 뒤에 PlayerRig를 붙여야 Awake에서 찾는다(BuildPartAScene과 같은 순서).
        var mage = go.AddComponent<MageAttack>();
        var gunner = go.AddComponent<GunnerAttack>();
        var rig = go.AddComponent<PlayerRig>();
        return (rig, mage, gunner);
    }

    [UnityTest]
    public IEnumerator Rig_FollowsProfile_WhenProfileIsReplaced()
    {
        var (rig, mage, gunner) = NewPlayer();
        yield return null;
        Assert.AreEqual(CharacterId.Mage, rig.Current, "기본 프로필은 마법사다");

        // 세이브를 불러와 프로필이 통째로 바뀐 상황(SaveService.Load / 새 원정).
        ProfileService.Replace(new PlayerProfile { character = CharacterId.Gunner });
        yield return null;

        Assert.AreEqual(CharacterId.Gunner, rig.Current, "프로필이 메카닉으로 바뀌었는데 키트가 안 따라갔다");
        Assert.IsTrue(gunner.enabled, "메카닉 키트가 안 켜졌다");
        Assert.IsFalse(mage.enabled, "마법사 키트가 그대로 켜져 있다");
    }

    [UnityTest]
    public IEnumerator SelectingCurrentKitAgain_RewritesProfile()
    {
        var (rig, _, _) = NewPlayer();
        yield return null;
        Assert.IsTrue(rig.Select(CharacterId.Gunner));
        yield return null;

        // 같은 프레임 안에서 프로필만 어긋난 상태를 만든 뒤 지금 키트를 다시 고른다.
        ProfileService.Current.character = CharacterId.Mage;
        Assert.IsTrue(rig.Select(CharacterId.Gunner));
        Assert.AreEqual(CharacterId.Gunner, ProfileService.Current.character,
            "이미 켜진 키트를 다시 골랐을 때 프로필이 안 고쳐졌다 — 로비는 마법사, 사냥은 메카닉이 된다");
    }

    [UnityTest]
    public IEnumerator ProfilePointingAtMissingKit_FallsBackToCurrentKit()
    {
        var (rig, _, _) = NewPlayer();
        yield return null;

        ProfileService.Current.character = CharacterId.Druid; // 이 오브젝트엔 드루이드 키트가 없다(기획 제외)
        yield return null;

        Assert.AreEqual(CharacterId.Mage, rig.Current);
        Assert.AreEqual(CharacterId.Mage, ProfileService.Current.character, "키트가 없는 캐릭터를 가리킨 채로 남았다");
    }
}
}
