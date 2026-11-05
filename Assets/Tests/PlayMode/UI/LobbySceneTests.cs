using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 로비 씬(`10_Lobby`, 2026-11-05 씬 분할 2차 3-2) — **씬 파일 자체**를 불러와 구성을 본다(흐름 없이 씬만). 게임 흐름이 이 씬을 불러오고 내리는 건 `GameBootTests`가 본다(3-4).
/// 로비 월드·그림만 있는 아바타·땅 그림이 있고, 전투 것(플레이어·스포너·전투 담당)은 없다. 내리면 로비 창구가 빈다.
/// </summary>
public class LobbySceneTests
{
    const string LobbyScene = "10_Lobby";

    [UnitySetUp]
    public IEnumerator Setup()
    {
        ProfileService.Reset();
        WorldView.Reset();
        FieldLayout.SetBossArena(false);
        GameState.Set(GameScene.Lobby);
        yield return SceneManager.LoadSceneAsync(LobbyScene, LoadSceneMode.Additive);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Teardown()
    {
        var scene = SceneManager.GetSceneByName(LobbyScene);
        if (scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
        GameState.Reset();
        WorldView.Reset();
        ProfileService.Reset();
    }

    static T[] InLobby<T>() where T : Component =>
        SceneManager.GetSceneByName(LobbyScene).GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();

    [Test]
    public void LobbyScene_HasTheLobbyWorld_AndNoCombatObjects()
    {
        var world = InLobby<LobbyWorld>().Single();
        var avatar = InLobby<LobbyAvatar>().Single();
        Assert.AreSame(avatar.transform, world.Player, "로비 월드가 걷게 하는 건 그림만 있는 아바타");
        Assert.AreSame(world, WorldView.Lobby, "로비 월드가 화면 창구에 꽂힌다");
        Assert.IsEmpty(InLobby<PlayerRig>(), "로비엔 전투의 플레이어가 없다");
        Assert.IsEmpty(InLobby<EnemySpawner>());
        Assert.IsEmpty(InLobby<CombatDirector>());
        Assert.IsEmpty(InLobby<PlatformSet>(), "로비엔 발판이 없다");
        Assert.IsEmpty(InLobby<Camera>(), "카메라는 상주 씬 하나뿐");
    }

    [Test]
    public void LobbyScene_DrawsItsGround_FromAnInertCollider()
    {
        var visuals = InLobby<StageVisuals>().Single();
        Assert.IsNotNull(visuals.transform.Find("GroundTiles"), "땅 그림을 깐다");
        var ground = visuals.Ground.GetComponent<BoxCollider2D>();
        Assert.IsFalse(ground.enabled, "땅 판정은 꺼 둔다 — 그림 크기 입력용(로비엔 물리가 없다)");
    }

    [Test]
    public void Avatar_WearsTheProfileCharacter()
    {
        var avatar = InLobby<LobbyAvatar>().Single();
        Assert.AreEqual(ProfileService.Current.character, avatar.Shown);
        Assert.AreSame(avatar.Art.SpriteOf(ProfileService.Current.character), avatar.GetComponent<SpriteRenderer>().sprite);
    }

    [UnityTest]
    public IEnumerator UnloadingTheLobby_EmptiesTheLobbyView()
    {
        yield return SceneManager.UnloadSceneAsync(SceneManager.GetSceneByName(LobbyScene));
        Assert.IsNull(WorldView.Lobby, "로비 씬을 내리면 로비 창구가 빈다");
    }
}

}
