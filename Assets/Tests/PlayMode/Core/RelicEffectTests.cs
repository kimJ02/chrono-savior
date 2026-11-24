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

    /// <summary>
    /// 메카닉 범위 유물(2026-11-24 사용자 A안): 따로 띄울 그림이 없으면 맞은 적마다 하얀 번개가 가운데부터 퍼지며 친다 —
    /// 가까운 적이 먼저, 먼 적은 거리 ÷ 14초 뒤에 맞는다(그림과 피해가 같이).
    /// </summary>
    [UnityTest]
    public IEnumerator Circle_Gunner_HitsSpreadOutwardWithZaps()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner;
        p.gunnerBranch = GunnerBranch.Installer;
        var near = NewEnemy(new Vector3(500.3f, 0.5f, 0f));
        var far = NewEnemy(new Vector3(504.2f, 0.5f, 0f)); // 4.2 → 0.3초 뒤
        yield return new WaitForFixedUpdate();

        RelicFx.Circle(new Vector2(500f, 0.5f), 6f, 1f, Color.white);
        yield return new WaitForSeconds(0.1f);
        Assert.Less(near.CurrentHp, near.MaxHp, "가까운 적이 곧바로 안 맞았다");
        Assert.AreEqual(far.MaxHp, far.CurrentHp, "먼 적이 번개가 닿기 전에 맞았다");
        Assert.IsNotNull(GameObject.Find("Vfx_" + VfxKeys.MechanicRelayAura.Replace('/', '_')), "맞은 적에 하얀 번개가 없다");
        yield return new WaitForSeconds(0.35f);
        Assert.Less(far.CurrentHp, far.MaxHp, "먼 적이 끝내 안 맞았다");
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

    /// <summary>메카닉 재설계 2차(2026-11-22) — 잔탄 포켓은 Z 적중으로 잔탄을 모으고, 설치 유물은 그 갈래에서만(설치 한도·추가 중계기).</summary>
    [Test]
    public void GunnerRelics_ZChargesForX_AndInstallerOnlyOnItsBranch()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner;
        p.gunnerBranch = GunnerBranch.Laser;
        p.items.Add("spareRounds"); // 1랭크 — 8번 적중에 잔탄 1
        for (int i = 0; i < 7; i++) GunnerRelics.OnZHit();
        Assert.AreEqual(0, GunnerRelics.SpareCharges, "8번 전에 잔탄이 찼다");
        GunnerRelics.OnZHit();
        Assert.AreEqual(1, GunnerRelics.SpareCharges);
        for (int i = 0; i < 100; i++) GunnerRelics.OnZHit();
        Assert.AreEqual(3, GunnerRelics.SpareCharges, "잔탄은 최대 3");

        p.items.Add("triangleDischarge"); p.items.Add("triangleDischarge");
        Assert.AreEqual(0, GunnerRelics.TurretBonus, "레이저 갈래에 설치 유물이 켜졌다");
        p.gunnerBranch = GunnerBranch.Installer;
        Assert.AreEqual(5 + 2, GunnerSpecConfig.TurretMax(5), "삼각 방전 2랭크 — 설치 한도 +2");
    }

    /// <summary>메카닉 레이저 갈래(2026-11-22 사용자 "레이저가 닿기 전에 죽는 적은 없게") — 유물 공격은 레이저가 닿은 적만 고른다.</summary>
    [UnityTest]
    public IEnumerator LaserBranch_RelicsHitOnlyLaserTouchedEnemies()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner;
        p.gunnerBranch = GunnerBranch.Laser;
        var e = NewEnemy(new Vector3(300f, 0.5f, 0f));
        yield return new WaitForFixedUpdate();
        var col = e.GetComponent<Collider2D>();
        Assert.IsFalse(RelicFx.IsTarget(col, out _), "레이저가 안 닿았는데 유물 대상이 됐다");
        Relics.MarkLaserTouched(col);
        Assert.IsTrue(RelicFx.IsTarget(col, out _), "레이저가 닿은 뒤엔 대상");

        p.gunnerBranch = GunnerBranch.Installer;
        Relics.ClearLaserTouched();
        Assert.IsTrue(RelicFx.IsTarget(col, out _), "설치 갈래엔 이 제한이 없다");
    }

    /// <summary>
    /// 편지 — 메카닉 레이저 갈래는 레이저가 배달한다(2026-11-23 사용자 후보 B): 레이저가 안 닿은 적에게도 광선이 가서 닿은 적이 되고,
    /// 광선 자체는 피해가 없고 1초 뒤 폭발로만 피해(예전엔 닿은 적만 골라 5층 레이저면 편지가 거의 안 붙었다).
    /// </summary>
    [UnityTest]
    public IEnumerator Letter_LaserBranch_BeamDeliversThenExplodes()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner;
        p.gunnerBranch = GunnerBranch.Laser;
        p.items.Add("letter");
        var target = NewEnemy(new Vector3(400.8f, 0.5f, 0f));
        yield return new WaitForFixedUpdate();
        var col = target.GetComponent<Collider2D>();
        Assert.IsFalse(RelicFx.IsTarget(col, out _), "시작부터 레이저가 닿은 적이었다");

        using (Relics.Signature()) RelicDirector.OnChainKill(new Vector2(400f, 0.5f)); // 메카닉은 레이저 처치만 공용 처치 유물이 센다
        yield return new WaitForSeconds(0.4f);
        Assert.IsTrue(RelicFx.IsTarget(col, out _), "편지 광선이 닿았는데 레이저가 닿은 적이 안 됐다");
        Assert.AreEqual(target.MaxHp, target.CurrentHp, "광선이 피해를 줬다(폭발만 피해)");

        yield return new WaitForSeconds(1.2f);
        Assert.Less(target.CurrentHp, target.MaxHp, "1초 뒤 폭발이 없다");
    }

    /// <summary>
    /// 편지(2026-11-24 사용자 "적이 이미 죽었는데도 발동") — 표식을 단 적이 폭발 전에 죽으면 전기 기둥·피해가 없다(옆 적도 안 맞는다).
    /// </summary>
    [UnityTest]
    public IEnumerator Letter_TargetDiesBeforeFuse_NoExplosion()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Gunner;
        p.gunnerBranch = GunnerBranch.Laser;
        p.items.Add("letter"); // 1랭크 = 편지 1통 — 가장 가까운 적에게
        var marked = NewEnemy(new Vector3(600.5f, 0.5f, 0f), 50f);
        var bystander = NewEnemy(new Vector3(601.8f, 0.5f, 0f)); // 표식 적 옆(폭발 반경 1.3 안), 편지 대상은 아님(더 멀다)
        yield return new WaitForFixedUpdate();

        using (Relics.Signature()) RelicDirector.OnChainKill(new Vector2(600f, 0.5f));
        yield return new WaitForSeconds(0.3f);
        Assert.IsTrue(RelicFx.IsTarget(marked.GetComponent<Collider2D>(), out _), "편지가 가까운 적에게 안 갔다");
        marked.TakeDamage(1000f, null); // 폭발 전에 죽는다
        yield return new WaitForSeconds(1.2f);
        Assert.AreEqual(bystander.MaxHp, bystander.CurrentHp, "표식 적이 죽었는데 그 자리에서 폭발했다");
    }

    /// <summary>메카닉은 시그니처 처치만 공용 처치 유물이 센다(2026-11-22) — 마법사·섬영은 모든 처치.</summary>
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
        Assert.IsTrue(Relics.KillCountsForCommon(p), "섬영은 Z가 주력이라 모든 처치를 센다(2026-11-22 2차)");
    }

    /// <summary>섬영 재설계 2차(2026-11-22) — 전부 Z 회전베기: 무한 검무는 폭풍 갈래 Z 적중으로 검령, 폭풍 칼집은 처치 n번마다 초승달.</summary>
    [Test]
    public void BladeRelics_ZSpinDrivesSpiritsAndStormSheath()
    {
        var p = ProfileService.Current;
        p.character = CharacterId.Blade;
        p.bladeBranch = BladeBranch.Storm;
        p.items.Add("endlessDance"); // 1랭크 — Z 3번 적중에 검령 1
        for (int i = 0; i < 6; i++) BladeRelics.OnSpinHits(Vector2.zero, 1, 1, true, false, true);
        Assert.AreEqual(2, BladeRelics.Spirits, "Z 6번 적중 = 검령 2");
        BladeRelics.OnSpinHits(Vector2.zero, 1, 1, false, false, true); // 칼날폭풍 유지 베기는 검령을 안 모은다
        Assert.AreEqual(2, BladeRelics.Spirits);

        p.items.Add("stormSheath"); // 1랭크 — 4마리마다 초승달 2발
        for (int i = 0; i < 3; i++) BladeRelics.OnStormKill(Vector2.zero, 1);
        Assert.AreEqual(0, CountProjectiles(), "4마리 전에 나갔다");
        BladeRelics.OnStormKill(Vector2.zero, 1);
        Assert.AreEqual(2, CountProjectiles(), "4번째 처치에 초승달 2발");

        p.bladeBranch = BladeBranch.Focus;
        Assert.AreEqual(0, Relics.Rank("stormSheath"), "집중 갈래에 폭풍 유물이 켜졌다");
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
