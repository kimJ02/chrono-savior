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
        RelicDirector.ResetForRun();
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

    /// <summary>메카닉 재설계(2026-11-22) — 레이저·설치 유물은 시그니처 수치를 키운다(갈래가 맞을 때만).</summary>
    [Test]
    public void GunnerRelics_GrowSignatureNumbers_OnlyOnTheirBranch()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner;
        p.gunnerBranch = GunnerBranch.Laser;
        float range0 = GunnerSpecConfig.BeamRange(5);
        p.items.Add("prismBore"); p.items.Add("prismBore"); p.items.Add("prismBore");
        Assert.AreEqual(1.2f, GunnerRelics.BeamRangeBonus, 1e-4f, "프리즘 구경 3랭크 사거리");
        Assert.AreEqual(0.22f, GunnerRelics.BeamWidthBonus, 1e-4f);
        Assert.AreEqual(0, GunnerRelics.TurretBonus, "레이저 갈래에 설치 유물이 켜지면 안 된다");

        p.gunnerBranch = GunnerBranch.Installer;
        p.items.Add("triangleDischarge"); p.items.Add("triangleDischarge");
        Assert.AreEqual(0f, GunnerRelics.BeamRangeBonus, "설치 갈래에 레이저 유물이 켜졌다");
        Assert.AreEqual(5 + 2, GunnerSpecConfig.TurretMax(5), "삼각 방전 2랭크 — 설치 한도 +2");
        float link0 = GunnerSpecConfig.LinkMax(5);
        p.items.Add("crossCurrent");
        Assert.AreEqual(link0 * 1.25f, GunnerSpecConfig.LinkMax(5), 1e-3f, "교차 전류 1랭크 — 연결 거리 +25%");
        Assert.Less(GunnerSpecConfig.FieldTick(5), 0.28f, "장판 틱이 빨라진다");
        Assert.Greater(range0, 0f);
    }

    /// <summary>메카닉·섬영은 시그니처 처치만 공용 처치 유물이 센다(2026-11-22) — 마법사는 그대로.</summary>
    [Test]
    public void CommonKillRelics_CountOnlySignatureKills_ForGunnerAndBlade()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        Assert.IsTrue(Relics.KillCountsForCommon(p), "마법사는 모든 처치");
        p.character = CharacterId.Gunner; p.gunnerBranch = GunnerBranch.Laser;
        Assert.IsFalse(Relics.KillCountsForCommon(p), "메카닉 Z 처치는 안 센다");
        using (Relics.Signature()) Assert.IsTrue(Relics.KillCountsForCommon(p), "레이저 처치는 센다");
        p.character = CharacterId.Blade; p.bladeBranch = BladeBranch.Storm;
        Assert.IsFalse(Relics.KillCountsForCommon(p));
    }

    /// <summary>섬영 재설계(2026-11-22) — 쿨감·궤적 강화는 섬영·그 갈래에서만, 폭풍 칼집은 칼날폭풍 처치만 센다.</summary>
    [Test]
    public void BladeRelics_GrowSignature_AndStormSheathCountsStormKills()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        p.items.Add("afterEdge");
        Assert.AreEqual(1f, BladeRelics.SkillCdMult, 1e-4f, "섬영 전용이 마법사에게 켜졌다");
        p.character = CharacterId.Blade;
        Assert.AreEqual(0.9f, BladeRelics.SkillCdMult, 1e-4f, "잔영 칼끝 1랭크 — X 쿨 -10%");

        p.bladeBranch = BladeBranch.Focus;
        p.items.Add("focusHeart");
        Assert.AreEqual(1.15f, BladeRelics.TrailDamageMult(false), 1e-4f, "집중의 심장 1랭크 — 궤적 ×1.15");
        p.items.Add("stormSheath");
        Assert.AreEqual(0, Relics.Rank("stormSheath"), "집중 갈래에 폭풍 유물이 켜졌다");

        p.bladeBranch = BladeBranch.Storm;
        for (int i = 0; i < 3; i++) BladeRelics.OnStormKill(Vector2.zero, 1);
        Assert.AreEqual(0, CountProjectiles(), "4마리 전에 나갔다");
        BladeRelics.OnStormKill(Vector2.zero, 1);
        Assert.AreEqual(3, CountProjectiles(), "4번째 칼날폭풍 처치에 초승달 3발");
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
    // ───────────────────────── 공용 유물·세트 ─────────────────────────

    static GameObject Dummy() => new GameObject("TestEnemy"); // 처치 이벤트만 받는 빈 몹(콜라이더 없음)

    [UnityTest]
    public IEnumerator Lantern_EveryEightKills_PulsesNearestPack()
    {
        ProfileService.Current.items.Add("lantern"); // 1랭크 = 8처치
        var target = NewEnemy(new Vector3(0.6f, 0.5f, 0f)); // 플레이어 자리(전투 화면 없으면 0,0) 근처
        yield return new WaitForFixedUpdate();

        for (int i = 0; i < 7; i++) RelicDirector.OnEnemyKilled(Dummy());
        Assert.AreEqual(target.MaxHp, target.CurrentHp, "8번 전에 터졌다");
        RelicDirector.OnEnemyKilled(Dummy());
        Assert.Less(target.CurrentHp, target.MaxHp, "8번째 처치에 등불 파동이 없다");
    }

    [Test]
    public void KillsFromRelicDamage_DoNotCount()
    {
        ProfileService.Current.items.Add("lantern");
        using (Relics.Damaging())
            for (int i = 0; i < 20; i++) RelicDirector.OnEnemyKilled(Dummy());
        RelicDirector.OnEnemyKilled(Dummy());
        // 유물 처치를 셌다면 이미 두 번 터지고 카운터가 5였을 것 — 정상이면 1
        var counters = (System.Collections.Generic.Dictionary<string, int>)typeof(RelicDirector)
            .GetField("killCounters", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        Assert.AreEqual(1, counters["lantern"], "유물 피해로 난 처치까지 셌다");
    }

    [Test]
    public void GlassOath_KillBuffsAttack_AndRaisesDamageTaken()
    {
        var p = ProfileService.Current;
        float atk0 = PlayerStatCalculator.ComputeAtk(p);
        p.items.Add("glassOath");
        RelicDirector.OnEnemyKilled(Dummy());
        Assert.Greater(PlayerStatCalculator.ComputeAtk(p), atk0 * 1.2f, "유리 서약 처치 강화가 공격력에 안 붙는다");
        Assert.Greater(CombatModifiers.RelicDamageTakenMultiplier, 1f);
        Assert.IsTrue(CombatModifiers.BreakGlass(), "맞으면 깨져야 한다");
        Assert.AreEqual(atk0, PlayerStatCalculator.ComputeAtk(p), 0.001f, "깨진 뒤에도 강화가 남았다");
    }

    [Test]
    public void DoomBell_RaisesEnemyDamage()
    {
        float before = CombatModifiers.EnemyDamageMultiplier;
        ProfileService.Current.items.Add("doomBell");
        Assert.AreEqual(before * 1.10f, CombatModifiers.EnemyDamageMultiplier, 0.001f, "종언의 종 저주(적 공격력 +10%)");
    }

    [Test]
    public void SeventhCrown_StoresSpentDaysAsSeals_OnRegression()
    {
        var p = ProfileService.Current;
        p.items.Add("seventhCrown"); // 1랭크 = 봉인 최대 3
        p.doomDays = PlayerProfile.DoomDaysStart - 5; // 5일 씀
        p.MarkBossCleared(1);
        p.DoRegression();
        Assert.AreEqual(3, p.relicSeals, "쓴 날짜 5일이 상한 3으로 잘려 봉인이 되어야 한다");
    }

    [Test]
    public void CharacterSet_CountsDistinctOwnRelics()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Mage;
        p.items.Add("mageEcho"); p.items.Add("mageEcho"); // 중복은 1종
        p.items.Add("emberLedger");
        p.items.Add("spareRounds");                       // 메카닉 것은 안 센다
        Assert.AreEqual(2, Relics.CharacterSetCount());
        Assert.IsFalse(Relics.CharacterSet3);
        p.items.Add("gravitySpring");
        Assert.IsTrue(Relics.CharacterSet3, "마법사 유물 3종이면 세트 3");
        Assert.AreEqual(0, Relics.TagCount("연쇄"));
        p.items.Add("lantern"); p.items.Add("letter");
        Assert.AreEqual(2, Relics.TagCount("연쇄"));
    }
}

}
