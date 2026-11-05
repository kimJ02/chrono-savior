using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;
using ChronoSavior.UI;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 화면이 월드를 읽는 창구(`Systems/WorldView`, 2026-11-05 씬 분할 2차 2b). 월드 쪽 담당이 꽂고(`CombatDirector`·`LobbyWorld`) 화면은 계약으로 읽는다 —
/// 예전 화면이 씬을 뒤져 얻던 값(플레이어 체력·키트·위치, 포탈, 보스 체력, 살아 있는 적 위치)과 같은지, 월드가 없을 때 화면이 안전한지 본다.
/// </summary>
public class WorldViewTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        Time.timeScale = 1f;
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && (go.name.StartsWith("Test") || go.GetComponent<UnityEngine.EventSystems.EventSystem>() != null)) Object.DestroyImmediate(go);
        ResetStatics();
    }

    static void ResetStatics()
    {
        GameState.Reset();
        RunState.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunTransient.Reset();
        CombatModifiers.Reset();
        ProfileService.Reset();
        UiModal.Reset();
        WorldView.Reset();
    }

    /// <summary>계약만 구현한 가짜 담당 — 창구 규칙(중복 거부·자기만 빼기)만 본다.</summary>
    class FakeCombat : ICombatView
    {
        public float PlayerHp => 0f;
        public float PlayerMaxHp => 0f;
        public Vector2 PlayerPosition => Vector2.zero;
        public ISkillSlotSource PlayerKit => null;
        public PortalView Portal => default;
        public float? BossHpRatio => null;
        public void CopyEnemyPositions(List<Vector2> into) => into.Clear();
    }

    [Test]
    public void Attach_RefusesASecondView_AndDetachOnlyRemovesItself()
    {
        var a = new FakeCombat();
        var b = new FakeCombat();
        WorldView.Attach(a);
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("이미 다른 담당"));
        WorldView.Attach(b);
        Assert.AreSame(a, WorldView.Combat, "먼저 꽂힌 담당이 그대로다");

        WorldView.Detach(b);
        Assert.AreSame(a, WorldView.Combat, "다른 담당을 빼려 해도 지금 담당은 그대로");
        WorldView.Detach(a);
        Assert.IsNull(WorldView.Combat);
    }

    static GameObject NewPlayer()
    {
        var go = new GameObject("TestPlayer");
        go.transform.position = new Vector3(3f, 1f, 0f);
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>();
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        go.AddComponent<PlayerHealth>();
        go.AddComponent<MageAttack>();
        go.AddComponent<PlayerRig>();
        return go;
    }

    static EnemyHealth NewEnemy(Vector3 pos, bool boss = false)
    {
        var go = new GameObject(boss ? "TestBoss" : "TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>();
        go.AddComponent<EnemyMove>();
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        var hp = go.AddComponent<EnemyHealth>();
        hp.isBoss = boss;
        return hp;
    }

    /// <summary>전투 담당이 내주는 값 = 예전 화면이 씬을 뒤져 얻던 값.</summary>
    [UnityTest]
    public IEnumerator Director_ReportsPlayerPortalBossAndEnemies()
    {
        var player = NewPlayer();
        var flow = TestFlow.Create(player);
        var director = CombatDirector.Current;
        var portalGo = new GameObject("TestPortal");
        portalGo.transform.position = new Vector3(9f, 1f, 0f);
        portalGo.AddComponent<SpriteRenderer>();
        var portal = portalGo.AddComponent<BossPortal>();
        portal.Player = player.transform;
        director.BossPortal = portal;
        yield return null;
        flow.StartRun(1, RunMode.Normal);

        var view = WorldView.Combat;
        Assert.AreSame(director, view, "전투 담당이 창구에 꽂힌다");
        var health = player.GetComponent<PlayerHealth>();
        Assert.AreEqual(health.CurrentHp, view.PlayerHp);
        Assert.AreEqual(health.MaxHp, view.PlayerMaxHp);
        Assert.AreEqual((Vector2)player.transform.position, view.PlayerPosition);
        Assert.AreSame(player.GetComponent<MageAttack>(), view.PlayerKit, "지금 캐릭터(마법사) 키트");
        Assert.IsTrue(view.Portal.Visible, "일반 필드에선 포탈이 보인다");
        Assert.AreEqual((Vector2)portal.transform.position, view.Portal.Position);
        Assert.IsNull(view.BossHpRatio, "보스가 없으면 체력바도 없다");

        var a = NewEnemy(new Vector3(5f, 1f, 0f));
        var b = NewEnemy(new Vector3(6f, 1f, 0f));
        var boss = NewEnemy(new Vector3(7f, 1f, 0f), boss: true);
        boss.SetMaxHp(100f);
        boss.TakeTickDamage(25);
        var positions = new List<Vector2> { new Vector2(-99f, -99f) }; // 먼저 비우는지 보려고
        view.CopyEnemyPositions(positions);
        CollectionAssert.AreEquivalent(new[] { (Vector2)a.transform.position, (Vector2)b.transform.position, (Vector2)boss.transform.position }, positions,
            "살아 있는 적 전부(보스 포함), 이전 내용은 비운다");
        Assert.AreEqual(boss.CurrentHp / boss.MaxHp, view.BossHpRatio.Value, 1e-4f);

        b.gameObject.SetActive(false);
        view.CopyEnemyPositions(positions);
        Assert.AreEqual(2, positions.Count, "꺼진 적은 빠진다");

        director.enabled = false;
        Assert.IsNull(WorldView.Combat, "전투 씬이 내려가면(담당이 꺼지면) 창구가 빈다");
    }

    /// <summary>전투 씬이 없을 때(타이틀·전환 중) 전투 화면은 오류 없이 비어 있다 — 다시 찾는 예비 경로가 없다.</summary>
    [UnityTest]
    public IEnumerator CombatHud_WithoutCombatScene_IsSafe()
    {
        Assert.IsNull(WorldView.Combat);
        GameState.Set(GameScene.Run);
        var pc = PixelCanvas.Create();
        try
        {
            for (int i = 0; i < 5; i++) yield return null; // 미니맵 갱신 주기(0.15초)와 상관없이 몇 프레임 — 예외가 나면 테스트가 실패한다
            Assert.IsNotNull(pc.GetComponent<CombatHud>());
        }
        finally
        {
            Object.DestroyImmediate(pc.gameObject);
        }
    }
}

}
