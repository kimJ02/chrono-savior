using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 캐릭터 전용 유물(2026-11-19 테스트 유물)이 실제로 발동하는지 — 범위 피해 도구, 처치 폭발(불씨 장부),
/// n번마다 발동(잔탄 포켓·잔영 칼끝·폭풍 칼집), 유물 피해가 유물을 다시 발동시키지 않는 규칙.
/// </summary>
public class RelicEffectTests
{
    [SetUp]
    public void Setup() => ResetStatics();

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && (go.name == "TestEnemy" || go.name.StartsWith("Relic") || go.name.StartsWith("Vfx_") || go.name == "RangeRing"))
                Object.DestroyImmediate(go);
        ResetStatics();
    }

    static void ResetStatics()
    {
        ProfileService.Reset();
        CombatModifiers.Reset();
        CombatEvents.Reset(); RunEvents.Reset();
        RunTransient.Reset();
        GunnerRelics.ResetForRun();
        BladeRelics.ResetForRun();
    }

    static EnemyHealth NewEnemy(Vector3 pos, float hp = 100000f)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = go.AddComponent<EnemyMove>();
        var health = go.AddComponent<EnemyHealth>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(move, 0f);
        var rb = go.GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        move.enabled = false;
        health.SetMaxHp(hp);
        return health;
    }

    static int CountProjectiles() => Object.FindObjectsByType<RelicProjectile>(FindObjectsSortMode.None).Length;

    [UnityTest]
    public IEnumerator Circle_HitsInsideRadius_NotOutside()
    {
        var near = NewEnemy(new Vector3(100f, 0.5f, 0f));
        var far = NewEnemy(new Vector3(104f, 0.5f, 0f));
        yield return new WaitForFixedUpdate();

        int n = RelicFx.Circle(new Vector2(100f, 0.5f), 1.5f, 1f, Color.white);

        Assert.AreEqual(1, n);
        Assert.Less(near.CurrentHp, near.MaxHp, "범위 안 적이 안 맞았다");
        Assert.AreEqual(far.MaxHp, far.CurrentHp, "범위 밖 적이 맞았다");
    }

    [UnityTest]
    public IEnumerator EmberLedger_BurningEnemyDeathExplodes()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        p.mageBranch = MageBranch.Explosion;
        p.items.Add("emberLedger");

        var dying = NewEnemy(new Vector3(200f, 0.5f, 0f), 10f);
        var neighbour = NewEnemy(new Vector3(200.8f, 0.5f, 0f));
        yield return new WaitForFixedUpdate();

        dying.ApplyBurn(1);
        RelicDirector.OnEnemyKilled(dying.gameObject);
        yield return new WaitForSeconds(0.15f);

        Assert.Less(neighbour.CurrentHp, neighbour.MaxHp, "불탄 적이 쓰러졌는데 시체 폭발이 없다");
    }

    [UnityTest]
    public IEnumerator EmberLedger_NeedsExplosionBranch()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        p.mageBranch = MageBranch.Gravity;
        p.items.Add("emberLedger");

        var dying = NewEnemy(new Vector3(300f, 0.5f, 0f), 10f);
        var neighbour = NewEnemy(new Vector3(300.8f, 0.5f, 0f));
        yield return new WaitForFixedUpdate();

        dying.ApplyBurn(1);
        RelicDirector.OnEnemyKilled(dying.gameObject);
        yield return new WaitForSeconds(0.15f);

        Assert.AreEqual(neighbour.MaxHp, neighbour.CurrentHp, "중력 갈래인데 폭발 갈래 유물이 켜졌다");
    }

    [Test]
    public void SpareRounds_FiresEveryNthZHit_NotFromRelicDamage()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner;
        p.items.Add("spareRounds"); // 1랭크 = 5번마다

        for (int i = 0; i < 4; i++) GunnerRelics.OnZHit(Vector2.zero, Vector2.right);
        Assert.AreEqual(0, CountProjectiles(), "5번 전에 나갔다");
        GunnerRelics.OnZHit(Vector2.zero, Vector2.right);
        Assert.AreEqual(1, CountProjectiles(), "5번째 적중에 유도탄이 안 나갔다");

        using (Relics.Damaging())
            for (int i = 0; i < 10; i++) GunnerRelics.OnZHit(Vector2.zero, Vector2.right);
        Assert.AreEqual(1, CountProjectiles(), "유물 피해가 유물을 다시 발동시켰다");
    }

    [Test]
    public void AfterEdge_EveryNthSpin_AndNeedsBlade()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        p.items.Add("afterEdge");
        for (int i = 0; i < 10; i++) BladeRelics.OnSpin(Vector2.zero, 1);
        Assert.AreEqual(0, CountProjectiles(), "섬영 전용이 마법사에게 켜졌다");

        p.character = CharacterId.Blade;
        BladeRelics.ResetForRun();
        for (int i = 0; i < 5; i++) BladeRelics.OnSpin(Vector2.zero, 1);
        Assert.AreEqual(1, CountProjectiles(), "회전 베기 5번째에 칼날이 안 나갔다");
    }

    [Test]
    public void StormSheath_ThreeChargesFireFan()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Blade;
        p.bladeBranch = BladeBranch.Storm;
        p.items.Add("stormSheath"); // 5처치 = 1충전, 3충전이면 칼날 3발

        for (int i = 0; i < 14; i++) BladeRelics.OnSpinKill(Vector2.zero, 1);
        Assert.AreEqual(0, CountProjectiles(), "3충전 전에 나갔다");
        BladeRelics.OnSpinKill(Vector2.zero, 1);
        Assert.AreEqual(3, CountProjectiles(), "15처치(3충전)에 칼날 3발이 아니다");
    }

    [UnityTest]
    public IEnumerator HomingProjectile_HitsEnemy()
    {
        var e = NewEnemy(new Vector3(400f, 1.5f, 0f));
        yield return new WaitForFixedUpdate();
        RelicProjectile.Spawn(new Vector2(397f, 0.5f), Vector2.right, 9f, 2f, 1f, RelicProjectile.Mode.Homing, null, Color.white);
        float t = 0f;
        while (t < 1.5f && e.CurrentHp >= e.MaxHp) { t += Time.deltaTime; yield return null; }
        Assert.Less(e.CurrentHp, e.MaxHp, "유도탄이 적에게 안 닿았다");
    }
}

}
