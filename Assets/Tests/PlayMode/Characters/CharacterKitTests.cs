using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 단계 1 착수 전제(캐릭터 뼈대) 검증 — `docs/sprints/05-all-characters-tier0.md` 1번, 계약은 `docs/worksplit.md` 3절.
/// 팀원 세션이 섬영·드루이드 키트를 여기에 얹으므로, 계약이 바뀌면 여기서 먼저 깨져야 한다.
/// </summary>
public class CharacterKitTests
{
    [SetUp]
    public void Setup() => ProfileService.Reset();

    [TearDown]
    public void Teardown()
    {
        ProfileService.Reset();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && go.name.StartsWith("Test")) Object.DestroyImmediate(go);
        }
    }

    static GameObject NewPlayer()
    {
        var go = new GameObject("TestPlayer");
        go.tag = "Player";
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        go.AddComponent<MageAttack>().Equipped();
        go.AddComponent<PlayerRig>();   // 키트를 전부 붙인 뒤에 추가해야 Awake가 전부 찾는다
        return go;
    }

    // ---- 캐릭터 스탯 배수 (원본 CHAR_STAT_MULT, project_test.html:1282) ----

    [Test]
    public void BladeStatMultiplier_AppliesToAtkAndMaxHp_ButNotToMsAsCrit()
    {
        var p = new PlayerProfile();

        p.character = CharacterId.Mage;
        float mageAtk = PlayerStatCalculator.ComputeAtk(p);
        float mageHp = PlayerStatCalculator.ComputeMaxHp(p);
        float mageMs = PlayerStatCalculator.ComputeMoveSpeedMultiplier(p);
        float mageAs = PlayerStatCalculator.ComputeAttackSpeedMultiplier(p);
        float mageCrit = PlayerStatCalculator.ComputeCritChance(p);

        p.character = CharacterId.Blade;
        Assert.AreEqual(mageAtk * 1.5f, PlayerStatCalculator.ComputeAtk(p), 0.001f, "섬영 공격력에 1.5배가 안 붙음");
        Assert.AreEqual(Mathf.Round(mageHp * 1.5f), PlayerStatCalculator.ComputeMaxHp(p), 0.001f, "섬영 최대체력에 1.5배가 안 붙음");

        // 원본은 statMs/statAs/statCrit(:1286~1288)엔 charStatMult를 곱하지 않는다
        Assert.AreEqual(mageMs, PlayerStatCalculator.ComputeMoveSpeedMultiplier(p), 0.001f, "이동속도에 캐릭터 배수가 붙었다 — 원본은 안 붙는다");
        Assert.AreEqual(mageAs, PlayerStatCalculator.ComputeAttackSpeedMultiplier(p), 0.001f, "공격속도에 캐릭터 배수가 붙었다 — 원본은 안 붙는다");
        Assert.AreEqual(mageCrit, PlayerStatCalculator.ComputeCritChance(p), 0.001f, "치명타에 캐릭터 배수가 붙었다 — 원본은 안 붙는다");
    }

    [Test]
    public void OnlyBladeHasStatMultiplier()
    {
        Assert.AreEqual(1f, CharacterStats.StatMultiplier(CharacterId.Mage), 0.001f);
        Assert.AreEqual(1f, CharacterStats.StatMultiplier(CharacterId.Gunner), 0.001f);
        Assert.AreEqual(1.5f, CharacterStats.StatMultiplier(CharacterId.Blade), 0.001f);
        Assert.AreEqual(1f, CharacterStats.StatMultiplier(CharacterId.Druid), 0.001f);
    }

    // ---- PlayerRig ----

    [UnityTest]
    public IEnumerator PlayerRig_StartsAsMage_AndKeepsMageKitEnabled()
    {
        var go = NewPlayer();
        yield return null;

        var rig = go.GetComponent<PlayerRig>();
        Assert.AreEqual(CharacterId.Mage, rig.Current);
        Assert.IsTrue(go.GetComponent<MageAttack>().enabled, "마법사 키트가 꺼져 있다");
        Assert.AreEqual(CharacterId.Mage, ProfileService.Current.character, "프로필에 선택 캐릭터가 반영 안 됨");
        Assert.AreEqual(CharacterMover2D.MoveMode.Instant, go.GetComponent<CharacterMover2D>().moveMode,
            "마법사인데 이동 방식이 즉시 이동이 아니다");
    }

    /// <summary>키트가 없는 캐릭터를 가리키면 전환이 안 되고 지금 키트가 그대로 켜져 있다(캐릭터를 바꾸는 길은 프로필 하나 — 2026-11-05 2a).</summary>
    [UnityTest]
    public IEnumerator PlayerRig_KeepsCurrentKit_WhenProfilePointsAtMissingKit()
    {
        var go = NewPlayer();
        yield return null;
        var rig = go.GetComponent<PlayerRig>();

        ProfileService.Current.character = CharacterId.Blade; // 이 테스트 오브젝트엔 섬영 키트가 없다
        yield return null;
        Assert.AreEqual(CharacterId.Mage, rig.Current);
        Assert.IsTrue(go.GetComponent<MageAttack>().enabled, "실패한 전환이 기존 키트를 꺼버렸다");
    }

    // ---- 관성 이동 (원본 bladeMove, project_test.html:2454) ----

    static CharacterMover2D NewInertialMover()
    {
        var go = new GameObject("TestInertialMover");
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        var mover = go.AddComponent<CharacterMover2D>();
        mover.moveMode = CharacterMover2D.MoveMode.Inertial;
        return mover;
    }

    /// <summary>
    /// `ComputeInertialVx`는 입력을 인자로 받는 private 메서드라 키 입력 없이 직접 호출해서
    /// 공식만 검증한다(이 프로젝트에서 Input 시뮬레이션이 안 될 때 쓰는 패턴 — `PhysicsAndMageTests` 참고).
    /// </summary>
    static float Step(CharacterMover2D mover, float h, float dt)
    {
        var m = typeof(CharacterMover2D).GetMethod("ComputeInertialVx", BindingFlags.NonPublic | BindingFlags.Instance);
        Assert.IsNotNull(m, "ComputeInertialVx가 없다 — 관성 이동 계약이 바뀌었나?");
        return (float)m.Invoke(mover, new object[] { h, dt });
    }

    [UnityTest]
    public IEnumerator Inertial_AcceleratesFromBaseSpeedTowardMaxSpeed()
    {
        var mover = NewInertialMover();
        yield return null;

        // 첫 입력은 baseSpeed로 즉시 붙는다(원본 `max(|vx|, baseS)`)
        float v1 = Step(mover, 1f, 0.02f);
        Assert.GreaterOrEqual(v1, mover.inertialBaseSpeed, "첫 입력에 baseSpeed가 즉시 안 붙었다");

        // 같은 방향을 유지하면 계속 빨라지고 maxSpeed를 넘지 않는다
        float v = v1;
        for (int i = 0; i < 300; i++) v = Step(mover, 1f, 0.02f);
        Assert.Greater(v, v1, "같은 방향을 유지하는데 가속이 안 된다");
        Assert.LessOrEqual(v, mover.inertialMaxSpeed + 0.001f, "maxSpeed 상한을 넘었다");
        Assert.AreEqual(mover.inertialMaxSpeed, v, 0.05f, "충분히 달렸는데 최고 속도에 도달하지 못했다");
    }

    [UnityTest]
    public IEnumerator Inertial_ReversesInstantly_KeepingSpeed()
    {
        var mover = NewInertialMover();
        yield return null;

        float v = 0f;
        for (int i = 0; i < 300; i++) v = Step(mover, 1f, 0.02f); // 오른쪽 최고속
        float topSpeed = Mathf.Abs(v);

        float reversed = Step(mover, -1f, 0.02f);

        // 원본(:2469~2474): 방향 전환은 감속이 아니라 "갖고 있던 속도를 그대로 반대로 돌린다"
        Assert.Less(reversed, 0f, "방향을 바꿨는데 속도 부호가 안 바뀌었다");
        Assert.AreEqual(topSpeed, Mathf.Abs(reversed), 0.001f,
            "방향 전환에서 속도가 깎였다 — 원본은 감속 없이 즉시 반전이다(CONFIG.blade.brake는 쓰이지 않는 죽은 값)");
        Assert.AreEqual(-1, mover.RunDir, "RunDir이 새 방향으로 안 바뀌었다");
    }

    [UnityTest]
    public IEnumerator Inertial_DeceleratesToZeroWithoutInput()
    {
        var mover = NewInertialMover();
        yield return null;

        for (int i = 0; i < 300; i++) Step(mover, 1f, 0.02f);

        float v = 0f;
        for (int i = 0; i < 300; i++) v = Step(mover, 0f, 0.02f);
        Assert.AreEqual(0f, v, 0.001f, "무입력인데 완전히 멈추지 않았다");
    }

    [UnityTest]
    public IEnumerator Inertial_MoveScaleCapsSpeed_AndScalesAccelByItsSquare()
    {
        var fast = NewInertialMover();
        var slow = NewInertialMover();
        slow.MoveScale = 0.5f;
        yield return null;

        float vFast = 0f, vSlow = 0f;
        for (int i = 0; i < 400; i++) { vFast = Step(fast, 1f, 0.02f); vSlow = Step(slow, 1f, 0.02f); }

        // 원본 bladeMaxSpeed() = maxSpeed × bladeMsK() — 속도 상한이 배율만큼 줄어든다
        Assert.AreEqual(fast.inertialMaxSpeed * 0.5f, vSlow, 0.05f, "MoveScale이 최고 속도에 반영되지 않았다");
        Assert.Greater(vFast, vSlow, "MoveScale이 낮은 쪽이 더 빠르다");
    }

    [UnityTest]
    public IEnumerator Inertial_ResetMotion_ClearsCarriedSpeed()
    {
        var mover = NewInertialMover();
        yield return null;

        for (int i = 0; i < 100; i++) Step(mover, 1f, 0.02f);
        mover.ResetMotion();

        // 리셋 직후 첫 입력은 다시 baseSpeed에서 시작해야 한다(쌓인 속도가 남아 있으면 안 됨)
        float v = Step(mover, 1f, 0.02f);
        Assert.AreEqual(mover.inertialBaseSpeed, v, 0.05f, "ResetMotion 후에도 관성 속도가 남아 있다");
    }
}
}
