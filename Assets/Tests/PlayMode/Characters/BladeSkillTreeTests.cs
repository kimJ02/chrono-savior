using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using ChronoSavior.Enemies;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 섬영 전문화(스킬트리) 1~2층 — 집중(move 갈래)·칼날폭풍(conv 갈래) 둘 다. 원본
/// `SPEC.blade`(project_test.html:933-949)·`bladeFocusStart`/`bladeTrailUpdate`/`bladeRipTrail`
/// (:2521-2592)·`bladeStormStart`/`bladeStormUpdate`/`bladeStormLand`(:2612-2738)의 1~2층
/// 범위를 검증한다. 2층 테스트는 "2단계로 넘어가자" 지시에 맞춰 이 파일에 이어 붙였다(§== 2층 ==
/// 구획 참고) — 1층 테스트는 그대로 두고 건드리지 않았다.
///
/// 3층 이상 분기는 `BladeCombat.cs`에 TODO(tier3+)로만 표시돼 있고 아직 없다 — 3층에 들어갈 때
/// 이 파일에도 해당 테스트를 추가한다(사용자 지시 "섬영 차례대로": 한 번에 다 만들지 않는다).
///
/// `PlayerProfile.TryLearnBladeTier`/`bladeBranch`/`bladeTier`는 팀원이 `Core/PlayerProfile.cs`에
/// 추가한 것(파일 소유권 예외 — 그 파일 주석과 PROGRESS.md 참고, 팀장 확인 필요). 2층 동작
/// 테스트는 SP 소비 경로(`TryLearnBladeTier`)를 거치지 않고 `ProfileService.Current.bladeBranch`/
/// `bladeTier`를 직접 대입한다 — SP 경제 자체는 위 세 `[Test]`에서 이미 따로 검증했으므로, 여기서는
/// "그 티어에 있을 때 스킬 동작이 맞는지"만 격리해서 본다(레벨/SP 세팅을 매번 반복할 필요가 없다).
/// </summary>
public class BladeSkillTreeTests
{
    [SetUp]
    public void Reset()
    {
        ProfileService.Reset();
        CombatModifiers.Reset();
    }

    [TearDown]
    public void Cleanup()
    {
        ProfileService.Reset();
        CombatModifiers.Reset();
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
        {
            if (go != null && (go.name.StartsWith("Test"))) Object.DestroyImmediate(go);
        }
    }

    // ==================== SP 경제 (PlayerProfile.TryLearnBladeTier) ====================

    [Test]
    public void TryLearnBladeTier_LocksBranchAndRequiresSequentialOrder()
    {
        var p = new PlayerProfile();
        p.ProgressOf(CharacterId.Blade).level = 20; // SP 넉넉히 — 섬영 칸(캐릭터별 성장, 2026-10-14 병합)
        Assert.IsTrue(p.TryLearnBladeTier(BladeBranch.Focus), "1층(SP1)을 못 배웠다");
        Assert.AreEqual(BladeBranch.Focus, p.bladeBranch);
        Assert.AreEqual(1, p.bladeTier);

        Assert.IsFalse(p.TryLearnBladeTier(BladeBranch.Storm), "이미 집중 갈래를 골랐는데 칼날폭풍도 배워짐 — 갈래는 고정이어야 한다");
        Assert.AreEqual(1, p.bladeTier, "다른 갈래 시도가 실패해도 되는데 티어가 바뀜");

        Assert.IsTrue(p.TryLearnBladeTier(BladeBranch.Focus));
        Assert.AreEqual(2, p.bladeTier);
        Assert.AreEqual(1 + 2, p.bladeSpUsed, "1+2층 비용 합이 안 맞음");
    }

    [Test]
    public void TryLearnBladeTier_FailsWithoutEnoughSp()
    {
        var p = new PlayerProfile();
        p.ProgressOf(CharacterId.Blade).level = 2; // 섬영 SP = 1
        Assert.IsTrue(p.TryLearnBladeTier(BladeBranch.Storm), "SP1로 1층(비용1)을 못 배웠다");
        Assert.IsFalse(p.TryLearnBladeTier(BladeBranch.Storm), "SP가 부족한데 2층(비용2)이 배워졌다");
        Assert.AreEqual(1, p.bladeTier);
    }

    [Test]
    public void TryLearnBladeTier_SpPoolIsIndependentFromMage()
    {
        var p = new PlayerProfile { level = 3 }; // 마법사 SP = 2
        p.ProgressOf(CharacterId.Blade).level = 3; // 섬영 SP = 2 — 캐릭터마다 따로(2026-10-14 병합)
        Assert.IsTrue(p.TryLearnMageTier(MageBranch.Explosion), "마법사 1층을 못 배웠다");
        Assert.AreEqual(1, p.spUsed, "마법사 SP 소비가 안 됐다");

        // 섬영은 마법사와 별도 SP 풀을 쓴다 — 마법사가 SP를 먼저 썼어도 섬영 SP는 그대로 남아 있어야 한다.
        Assert.IsTrue(p.TryLearnBladeTier(BladeBranch.Focus), "마법사가 SP를 먼저 써서 섬영 1층을 못 배웠다 — SP 풀이 공유되면 안 된다");
        Assert.AreEqual(1, p.bladeSpUsed);
        Assert.AreEqual(1, p.spUsed, "섬영 습득이 마법사 spUsed를 건드렸다");
    }

    // ==================== 집중(move 갈래) ====================

    static GameObject NewBlade(Vector3 pos)
    {
        var go = new GameObject("TestBlade");
        go.transform.position = pos;
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        go.AddComponent<PlayerHealth>();
        go.AddComponent<BladeCombat>();
        return go;
    }

    static GameObject NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy");
        go.tag = "Enemy";
        go.transform.position = pos;
        go.AddComponent<SpriteRenderer>();
        go.AddComponent<CircleCollider2D>().radius = 0.5f;
        var move = go.AddComponent<EnemyMove>();
        go.AddComponent<EnemyHealth>();
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(move, 0f);
        return go;
    }

    static MethodInfo Method(string name) =>
        typeof(BladeCombat).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance);

    static FieldInfo Field(string name) =>
        typeof(BladeCombat).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);

    static void InvokeTryFocusStart(BladeCombat blade, int tier)
    {
        var m = Method("TryFocusStart");
        Assert.IsNotNull(m, "TryFocusStart(int)가 없다");
        m.Invoke(blade, new object[] { tier });
    }

    static void InvokeUpdateFocus(BladeCombat blade, float dt)
    {
        var m = Method("UpdateFocus");
        Assert.IsNotNull(m, "UpdateFocus(float)가 없다");
        m.Invoke(blade, new object[] { dt });
    }

    static float GetFocusT(BladeCombat blade) => (float)Field("focusT").GetValue(blade);
    static float GetTrailT(BladeCombat blade) => (float)Field("trailT").GetValue(blade);
    static void SetFocusT(BladeCombat blade, float value) => Field("focusT").SetValue(blade, value);
    static float GetFocusCd(BladeCombat blade) => (float)Field("focusCd").GetValue(blade);
    static void SetFocusCd(BladeCombat blade, float value) => Field("focusCd").SetValue(blade, value);
    static int GetTrailPtsCount(BladeCombat blade) =>
        (Field("trailPts").GetValue(blade) as System.Collections.ICollection)?.Count ?? 0;

    static void InvokeUpdatePassTick(BladeCombat blade, bool atTop)
    {
        var m = Method("UpdatePassTick");
        Assert.IsNotNull(m, "UpdatePassTick(bool)이 없다");
        m.Invoke(blade, new object[] { atTop });
    }

    [UnityTest]
    public IEnumerator Focus_WithTierZero_DoesNothing()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        yield return null;

        InvokeTryFocusStart(blade, 0); // 갈래를 아직 안 골랐을 때와 같은 상태(tier==0)
        InvokeUpdateFocus(blade, 0.02f);

        Assert.AreEqual(0f, GetFocusT(blade), 0.001f, "1층 미만인데 집중이 시작됐다");
        Assert.AreEqual(1f, mover.AccelMultiplier, 0.001f, "1층 미만인데 가속 배율이 올라갔다");
    }

    [UnityTest]
    public IEnumerator Focus_Start_AppliesAccelMultiplier_ThenEndsAfterDuration()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        yield return null;

        InvokeTryFocusStart(blade, 1);
        InvokeUpdateFocus(blade, 0.05f);
        // 원본 F.accelMul = 1.7 (CONFIG.blade.focus, project_test.html:627~635).
        Assert.AreEqual(1.7f, mover.AccelMultiplier, 0.001f, "집중 중 가속 배율이 원본(1.7)과 다르다");

        // 지속시간(F.dur=2.0)을 다 채우면 가속 배율이 원래대로 돌아와야 한다.
        InvokeUpdateFocus(blade, 2.0f);
        Assert.LessOrEqual(GetFocusT(blade), 0f, "집중 지속시간이 끝났는데 focusT가 안 줄었다");
        InvokeUpdateFocus(blade, 0.001f); // 다음 프레임에 배율이 반영된다(AccelMultiplier는 매 프레임 재계산).
        Assert.AreEqual(1f, mover.AccelMultiplier, 0.001f, "집중이 끝났는데 가속 배율이 그대로다");
    }

    [UnityTest]
    public IEnumerator Focus_ToggleCancel_StopsAccelButTrailSurvives()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        yield return null;

        InvokeTryFocusStart(blade, 1);
        InvokeUpdateFocus(blade, 0.05f);
        float trailBefore = GetTrailT(blade);
        Assert.Greater(trailBefore, 0f);

        // 재사용 = 즉시 중단(원본 :2524) — focusT만 0이 되고 trailT/trailPts는 안 건드린다.
        InvokeTryFocusStart(blade, 1);
        Assert.AreEqual(0f, GetFocusT(blade), 0.001f, "재사용으로 집중이 즉시 중단되지 않았다");
        Assert.AreEqual(trailBefore, GetTrailT(blade), 0.01f, "집중 취소가 궤적 지속시간까지 건드렸다(원본은 안 건드림)");

        InvokeUpdateFocus(blade, 0.001f);
        Assert.AreEqual(1f, mover.AccelMultiplier, 0.001f, "집중을 취소했는데 가속 배율이 남아 있다");
    }

    [UnityTest]
    public IEnumerator Focus_Cooldown_BlocksImmediateRestart()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        yield return null;

        InvokeTryFocusStart(blade, 1); // focusCd = 11 (F.cd)
        InvokeTryFocusStart(blade, 1); // 즉시 취소(focusT=0)
        Assert.AreEqual(0f, GetFocusT(blade), 0.001f);

        InvokeTryFocusStart(blade, 1); // 쿨다운(11초) 중이라 다시 시작되면 안 된다.
        Assert.AreEqual(0f, GetFocusT(blade), 0.001f, "쿨다운 중인데 집중이 다시 시작됐다");
    }

    [UnityTest]
    public IEnumerator Focus_Trail_DamagesEnemyNearRecordedPath()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        yield return null;

        InvokeTryFocusStart(blade, 1);
        InvokeUpdateFocus(blade, 0.01f); // 시작 지점(2,0.5)에 첫 궤적 점을 남긴다.

        go.transform.position = new Vector3(3f, 0.5f, 0f); // 궤적 간격(0.24) 이상 이동.
        var enemy = NewEnemy(new Vector3(3.1f, 0.5f, 0f)); // 새 궤적 점 반경(0.34) 안.
        yield return null;

        // 두 번째 점을 남기고, 틱 간격(0.22)을 넘겨 피해 판정까지 한 번에 처리.
        InvokeUpdateFocus(blade, 0.25f);
        yield return null;

        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.Less(hp.CurrentHp, hp.MaxHp, "궤적 위의 적이 지속피해를 안 받았다(원본 bladeTrailUpdate)");
    }

    // ==================== 칼날폭풍(conv 갈래) ====================

    static void InvokeTryStormStart(BladeCombat blade, int tier)
    {
        var m = Method("TryStormStart");
        Assert.IsNotNull(m, "TryStormStart(int)가 없다");
        m.Invoke(blade, new object[] { tier });
    }

    static void InvokeUpdateStorm(BladeCombat blade, float dt)
    {
        var m = Method("UpdateStorm");
        Assert.IsNotNull(m, "UpdateStorm(float)가 없다");
        m.Invoke(blade, new object[] { dt });
    }

    static bool GetStorming(BladeCombat blade) => (bool)Field("storming").GetValue(blade);
    static int GetStormDir(BladeCombat blade) => (int)Field("stormDir").GetValue(blade);

    static void InvokeRipTrail(BladeCombat blade)
    {
        var m = Method("RipTrail");
        Assert.IsNotNull(m, "RipTrail()가 없다");
        m.Invoke(blade, null);
    }

    static void InvokeUpdateFalling(BladeCombat blade, float dt)
    {
        var m = Method("UpdateFalling");
        Assert.IsNotNull(m, "UpdateFalling(float)가 없다");
        m.Invoke(blade, new object[] { dt });
    }

    static void InvokeEndStorm(BladeCombat blade)
    {
        var m = Method("EndStorm");
        Assert.IsNotNull(m, "EndStorm()가 없다");
        m.Invoke(blade, null);
    }

    static bool InvokeTryStormHop(BladeCombat blade, bool jumpPressed)
    {
        var m = Method("TryStormHop");
        Assert.IsNotNull(m, "TryStormHop(bool)이 없다");
        return (bool)m.Invoke(blade, new object[] { jumpPressed });
    }

    static float GetStormSpd(BladeCombat blade) => (float)Field("stormSpd").GetValue(blade);
    static void SetStormSpd(BladeCombat blade, float value) => Field("stormSpd").SetValue(blade, value);
    static float GetStormCd(BladeCombat blade) => (float)Field("stormCd").GetValue(blade);

    // GrabRecord/FallRecord는 BladeCombat의 private 중첩 struct라 테스트에서 타입을 직접 못 쓴다 —
    // List<T>도 비제네릭 ICollection을 구현하므로 Count만 필요하면 그걸로 충분하다.
    static int GetStormGrabCount(BladeCombat blade) =>
        (Field("stormGrab").GetValue(blade) as System.Collections.ICollection)?.Count ?? 0;
    static int GetStormFallingCount(BladeCombat blade) =>
        (Field("stormFalling").GetValue(blade) as System.Collections.ICollection)?.Count ?? 0;

    /// <summary>실제 Boss/Shrine 대신 <see cref="IGrabExempt"/>만 표시하는 최소 테스트 대역 —
    /// 4층 강제 연행이 "보스·성소 제외"(원본 e.boss||e.shrine, project_test.html:2694) 규칙을
    /// 실제로 보는지는 마커 인터페이스 하나면 충분히 검증된다(Boss/Shrine 자체 로직은 팀장 담당).</summary>
    class GrabExemptMarker : MonoBehaviour, IGrabExempt { }

    static GameObject NewGrabExemptEnemy(Vector3 pos)
    {
        var go = NewEnemy(pos);
        go.AddComponent<GrabExemptMarker>();
        return go;
    }

    [UnityTest]
    public IEnumerator Storm_WithTierZero_DoesNothing()
    {
        var go = NewBlade(new Vector3(2f, 30f, 0f)); // 허공 — 접지 여부가 결과를 가리지 않게.
        var blade = go.GetComponent<BladeCombat>();
        yield return null;

        InvokeTryStormStart(blade, 0);
        Assert.IsFalse(GetStorming(blade), "1층 미만인데 칼날폭풍이 시작됐다");
    }

    [UnityTest]
    public IEnumerator Storm_FailsWhileGrounded()
    {
        var go = NewBlade(new Vector3(2.2f, FieldBounds.GroundY + 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();

        int groundLayer = LayerMask.NameToLayer("Ground");
        var ground = new GameObject("TestGround");
        ground.layer = groundLayer;
        ground.AddComponent<BoxCollider2D>().size = new Vector2(4f, 1f);
        ground.transform.position = new Vector3(2.2f, FieldBounds.GroundY - 0.05f, 0f);
        // `yield return null;` 한 번(=한 프레임)만으로는 물리 씬에 콜라이더가 확실히 등록된다는
        // 보장이 없다 — 실제로 235개 중 이 테스트 하나만 간헐적으로 실패하는 게 확인됐다(원본 로직
        // 변경 없이 이 테스트만 재현되는 것으로 봐서 물리 등록 타이밍 문제로 판단). `PlayerAttackTests.
        // Jump_RisesThenReturnsToGround`(:123-124)가 이미 같은 이유로 `WaitForFixedUpdate()`를
        // 두 번 기다리는 관례를 쓰고 있어 그대로 따른다.
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        InvokeTryStormStart(blade, 1);
        Assert.IsFalse(GetStorming(blade), "지상에 있는데 칼날폭풍이 시작됐다(원본은 공중 전용)");
    }

    [UnityTest]
    public IEnumerator Storm_StartsAndGlidesDiagonallyDownward_WhileAirborne()
    {
        var go = NewBlade(new Vector3(2.2f, 10f, 0f)); // 근처에 바닥/발판이 전혀 없는 허공.
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        yield return null;

        InvokeTryStormStart(blade, 1);
        Assert.IsTrue(GetStorming(blade), "공중인데 칼날폭풍이 안 시작됐다");

        float yBefore = go.transform.position.y;
        InvokeUpdateStorm(blade, 0.05f);
        // Y+가 위인 이 포팅에서 "하강"은 Y가 줄어드는 것이다(원본 좌표계와 반대 — 클래스 주석 참고).
        Assert.Less(go.transform.position.y, yBefore, "칼날폭풍이 활공하는데 아래로 안 내려간다");
        Assert.IsTrue(GetStorming(blade), "허공에서 한 스텝 만에 칼날폭풍이 끝나 버렸다");
    }

    [UnityTest]
    public IEnumerator Storm_EndsWhenReachingGroundY()
    {
        var go = NewBlade(new Vector3(2.2f, FieldBounds.GroundY + 0.05f, 0f)); // 바닥 바로 위 허공.
        var blade = go.GetComponent<BladeCombat>();
        yield return null;

        InvokeTryStormStart(blade, 1);
        Assert.IsTrue(GetStorming(blade));

        // 큰 dt로 한 번에 바닥까지 내려가게 한다.
        InvokeUpdateStorm(blade, 1.0f);

        Assert.IsFalse(GetStorming(blade), "바닥에 닿았는데 칼날폭풍이 안 끝났다");
        Assert.AreEqual(FieldBounds.GroundY, go.transform.position.y, 0.01f, "착지 위치가 바닥(GroundY)이 아니다");
    }

    // ==================== 2층 (집중: 예리한 감각 / 칼날폭풍: 낙하 충격) ====================

    [UnityTest]
    public IEnumerator Focus_Tier2_DoublesAccelMultiplier()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 2;
        yield return null;

        InvokeTryFocusStart(blade, 2);
        InvokeUpdateFocus(blade, 0.05f);
        // 원본 2층부터 accelMul(1.7) * accelMul2(1.4) = 2.38 (project_test.html:2462).
        Assert.AreEqual(1.7f * 1.4f, mover.AccelMultiplier, 0.001f,
            "2층 집중 가속 배율이 원본(accelMul×accelMul2)과 다르다");
    }

    [UnityTest]
    public IEnumerator Focus_Tier1_DoesNotGetTier2AccelBonus()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 1;
        yield return null;

        InvokeTryFocusStart(blade, 1);
        InvokeUpdateFocus(blade, 0.05f);
        Assert.AreEqual(1.7f, mover.AccelMultiplier, 0.001f, "1층인데 2층 가속 보너스가 들어갔다(회귀 확인)");
    }

    static void InvokeApplyCrashImmunity(BladeCombat blade, bool active)
    {
        var m = Method("ApplyCrashImmunity");
        Assert.IsNotNull(m, "ApplyCrashImmunity(bool)이 없다");
        m.Invoke(blade, new object[] { active });
    }

    [UnityTest]
    public IEnumerator CrashImmunity_GrantsInvulnWhenActive()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var health = go.GetComponent<PlayerHealth>();
        yield return null;

        Assert.AreEqual(0f, health.InvulnRemaining, 0.001f, "시작부터 무적이 걸려 있으면 안 된다");

        InvokeApplyCrashImmunity(blade, true);
        Assert.Greater(health.InvulnRemaining, 0f,
            "최고 속도 충돌 무적 조건이 활성인데 무적이 안 걸렸다(원본 bladeCrashImmune)");
    }

    [UnityTest]
    public IEnumerator CrashImmunity_DoesNothingWhenInactive()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var health = go.GetComponent<PlayerHealth>();
        yield return null;

        InvokeApplyCrashImmunity(blade, false);
        Assert.AreEqual(0f, health.InvulnRemaining, 0.001f, "조건이 꺼져 있는데 무적이 걸렸다");
    }

    [UnityTest]
    public IEnumerator Storm_Tier1_LandingDealsNoAreaDamage()
    {
        var go = NewBlade(new Vector3(2.2f, FieldBounds.GroundY + 0.05f, 0f)); // 바닥 바로 위.
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 1;
        var enemy = NewEnemy(new Vector3(2.2f, FieldBounds.GroundY + 0.05f, 0f)); // 착지 지점 바로 옆.
        yield return null;

        InvokeTryStormStart(blade, 1);
        Assert.IsTrue(GetStorming(blade));

        // 회전베기 유지 틱 간격(0.11)보다 짧은 dt로 착지시켜, 이번 프레임엔 회전 틱이 안 끼게 한다 —
        // 적이 착지 지점 바로 옆에 있어서 회전 틱까지 같이 터지면 "착지 충격만" 격리해서 볼 수 없다.
        InvokeUpdateStorm(blade, 0.05f);
        Assert.IsFalse(GetStorming(blade), "이번 dt로는 바닥에 닿았어야 한다(테스트 전제 확인)");

        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.AreEqual(hp.MaxHp, hp.CurrentHp, 0.001f, "1층인데 착지 충격 피해가 들어갔다(2층 전용이어야 함)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier2_LandingDealsAreaDamage()
    {
        var go = NewBlade(new Vector3(2.2f, FieldBounds.GroundY + 0.05f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 2;
        var enemy = NewEnemy(new Vector3(2.2f, FieldBounds.GroundY + 0.05f, 0f));
        yield return null;

        InvokeTryStormStart(blade, 2);
        InvokeUpdateStorm(blade, 0.05f);
        Assert.IsFalse(GetStorming(blade), "이번 dt로는 바닥에 닿았어야 한다(테스트 전제 확인)");

        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.Less(hp.CurrentHp, hp.MaxHp, "2층인데 착지 충격 피해가 안 들어갔다(원본 bladeStormLand)");
    }

    // ==================== 3층 (집중: 짙은 잔영 / 칼날폭풍: 벽 반사) ====================

    [UnityTest]
    public IEnumerator Focus_Tier3_TrailLifeIsLonger()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 3;
        yield return null;

        InvokeTryFocusStart(blade, 3);
        // 원본 bladeTrailLife(): F.trailLife(3.2) × t3Life(1.45) = 4.64 (project_test.html:2537,
        // 3층 "짙은 잔영"부터 — 캐스팅 순간 티어로 고정된다).
        Assert.AreEqual(3.2f * 1.45f, GetTrailT(blade), 0.001f,
            "3층 궤적 지속시간이 원본(trailLife×t3Life)과 다르다");
    }

    [UnityTest]
    public IEnumerator Focus_Tier1_DoesNotGetTier3TrailLifeBonus()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 1;
        yield return null;

        InvokeTryFocusStart(blade, 1);
        Assert.AreEqual(3.2f, GetTrailT(blade), 0.001f, "1층인데 3층 궤적 지속시간 보너스가 들어갔다(회귀 확인)");
    }

    [UnityTest]
    public IEnumerator Focus_Tier1_TrailMisses_EnemyOutsideBaseRadius()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 1;
        // 1층 유효 반경(궤적 0.34 + 적 콜라이더 0.5 = 0.84) 밖, 3층 유효 반경(0.34×1.3+0.5=0.942) 안 —
        // 반경 보너스 자체를 격리해서 보려고 딱 그 사이 거리(0.89)에 둔다.
        var enemy = NewEnemy(new Vector3(2.89f, 0.5f, 0f));
        yield return null;

        InvokeTryFocusStart(blade, 1);
        InvokeUpdateFocus(blade, 0.01f); // 시작 지점에 궤적 점 기록.
        InvokeUpdateFocus(blade, 0.25f); // 틱 간격(0.22) 통과.
        yield return null;

        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.AreEqual(hp.MaxHp, hp.CurrentHp, 0.001f, "1층 궤적 반경 밖의 적이 맞았다(3층 반경 보너스가 새고 있음)");
    }

    [UnityTest]
    public IEnumerator Focus_Tier3_TrailHits_EnemyWithinBoostedRadius()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 3;
        var enemy = NewEnemy(new Vector3(2.89f, 0.5f, 0f)); // 위 테스트와 같은 거리 — 3층 반경 안.
        yield return null;

        InvokeTryFocusStart(blade, 3);
        InvokeUpdateFocus(blade, 0.01f);
        InvokeUpdateFocus(blade, 0.25f);
        yield return null;

        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.Less(hp.CurrentHp, hp.MaxHp, "3층 궤적 반경 안의 적이 안 맞았다(원본 bladeTrailR의 t3R 배율)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier3_BouncesOffWall_InsteadOfEnding()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f)); // 임시 위치 — mover.edgeMargin 확인 뒤 벽 앞으로 옮긴다.
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 3;

        float maxX = FieldBounds.MaxX - mover.edgeMargin; // UpdateStorm이 쓰는 것과 같은 식.
        go.transform.position = new Vector3(maxX - 0.05f, 10f, 0f); // 오른쪽 벽 바로 앞, 공중(착지 방지).
        yield return null;

        InvokeTryStormStart(blade, 3);
        Assert.IsTrue(GetStorming(blade));
        int dirBefore = GetStormDir(blade);

        InvokeUpdateStorm(blade, 0.05f); // 이 한 스텝의 가속만으로 벽까지 닿는다(아래 1층 회귀 테스트와 동일 전제).

        Assert.IsTrue(GetStorming(blade), "3층인데 벽에 닿자 칼날폭풍이 끝나 버렸다(반사돼야 함, 원본 :2683~2686)");
        Assert.AreEqual(-dirBefore, GetStormDir(blade), "3층 벽 반사인데 진행 방향이 안 뒤집혔다(원본 st.dir *= -1)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier1_StillEndsAtWall_Regression()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 1;

        float maxX = FieldBounds.MaxX - mover.edgeMargin;
        go.transform.position = new Vector3(maxX - 0.05f, 10f, 0f);
        yield return null;

        InvokeTryStormStart(blade, 1);
        InvokeUpdateStorm(blade, 0.05f);

        Assert.IsFalse(GetStorming(blade), "1층인데 벽에서 반사돼 안 끝났다(반사는 3층 전용이어야 함)");
    }

    [UnityTest]
    public IEnumerator CrashImmunity_DuringStorm_Tier3_AtTopSpeed_GrantsInvuln()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f)); // 허공 — 검증 전에 착지로 칼날폭풍이 끝나면 안 된다.
        var blade = go.GetComponent<BladeCombat>();
        var health = go.GetComponent<PlayerHealth>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 3;
        yield return null;

        InvokeTryStormStart(blade, 3);
        // 두 스텝으로 stormSpd를 끌어올려 stormSpeedRatio가 topAt(0.92) 이상이 되게 한다.
        InvokeUpdateStorm(blade, 0.3f);
        InvokeUpdateStorm(blade, 0.3f);
        Assert.IsTrue(GetStorming(blade), "테스트 전제: 아직 착지하면 안 된다");
        Assert.AreEqual(0f, health.InvulnRemaining, 0.001f, "아직 Update()가 한 번도 안 돌았는데 무적이 걸려 있다");

        // 리플렉션 호출은 BladeCombat.Update()를 안 거치므로, 실제 컴포넌트의 Update()가 이번에 계산해
        // 둔 stormSpeedRatio를 읽어 ApplyCrashImmunity를 부르는지는 진짜 프레임을 한 번 돌려야 확인된다.
        yield return null;

        Assert.Greater(health.InvulnRemaining, 0f,
            "칼날폭풍 3층 최고 속도인데 충돌 무적이 안 걸렸다(원본 bladeCrashImmune — 이전엔 storming 중 " +
            "ComputeSpeedRatio가 항상 0이라 이 절반이 죽어 있던 버그, 이번에 stormSpeedRatio로 고침)");
    }

    // ==================== 4층 (집중: 궤적 파열·즉시 가속 / 칼날폭풍: 강제 연행) ====================

    [UnityTest]
    public IEnumerator Focus_Tier4_SetsInstantAccelMultiplier_OnCastFrame()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 4;
        yield return null;

        InvokeTryFocusStart(blade, 4);
        InvokeUpdateFocus(blade, 0.02f);
        // "즉시 최고 속도" 근사(TryFocusStart/UpdateFocus 주석) — 캐스팅 직후 한 번은 초강력 배율이어야 한다.
        Assert.AreEqual(2000f, mover.AccelMultiplier, 0.001f, "4층 집중 캐스팅 직후 즉시가속 근사 배율이 안 걸렸다");
    }

    [UnityTest]
    public IEnumerator Focus_Tier4_InstantAccelMultiplier_OnlyLastsOneUpdate()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 4;
        yield return null;

        InvokeTryFocusStart(blade, 4);
        InvokeUpdateFocus(blade, 0.02f); // 이번 호출에서 초강력 배율을 소모한다.
        InvokeUpdateFocus(blade, 0.02f); // 다음 호출부터는 평소 4층(=2층과 동일) 배율로 돌아가야 한다.

        Assert.AreEqual(1.7f * 1.4f, mover.AccelMultiplier, 0.001f,
            "4층 즉시가속 스파이크가 한 번으로 안 끝나고 계속 남아 있다");
    }

    [UnityTest]
    public IEnumerator Focus_Tier3_DoesNotGetTier4InstantAccel()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var mover = go.GetComponent<CharacterMover2D>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 3;
        yield return null;

        InvokeTryFocusStart(blade, 3);
        InvokeUpdateFocus(blade, 0.02f);
        // 3층은 이미 2층 보너스(accelMul2)를 포함한다(1.7×1.4=2.38) — 여기서 확인할 회귀는
        // "4층 즉시가속(2000배)이 안 섞여 들어왔는가"이지 "2층 보너스가 없는가"가 아니다.
        Assert.AreEqual(1.7f * 1.4f, mover.AccelMultiplier, 0.001f, "3층인데 4층 즉시가속 배율이 들어갔다(회귀 확인)");
    }

    [UnityTest]
    public IEnumerator Focus_Tier4_RipTrail_DamagesEnemyWithinBoostedRadius()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 4;
        // 3층 궤적 반경(0.34×1.3+0.5=0.942) 밖, 4층 파열 반경(0.34×1.3×1.7+0.5≈1.251) 안 —
        // 파열 전용 피해인지 격리해서 보려고 딱 그 사이 거리(1.1)에 둔다.
        var enemy = NewEnemy(new Vector3(3.1f, 0.5f, 0f));
        yield return null;

        InvokeTryFocusStart(blade, 4);
        InvokeUpdateFocus(blade, 0.01f); // 시작 지점(2,0.5)에 궤적 점 하나만 남긴다(틱 간격 0.22 미도달).
        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.AreEqual(hp.MaxHp, hp.CurrentHp, 0.001f, "테스트 전제: 파열 전에는 아직 안 맞아야 한다");

        InvokeRipTrail(blade);
        Assert.Less(hp.CurrentHp, hp.MaxHp, "4층 궤적 파열 반경 안의 적이 안 맞았다(원본 bladeRipTrail :2570~2589)");
    }

    [UnityTest]
    public IEnumerator Focus_Tier3_RipTrail_DoesNotExplode()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 3;
        var enemy = NewEnemy(new Vector3(3.1f, 0.5f, 0f)); // 위 테스트와 같은 거리 — 4층 파열 반경 안.
        yield return null;

        InvokeTryFocusStart(blade, 3);
        InvokeUpdateFocus(blade, 0.01f);
        InvokeRipTrail(blade);

        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.AreEqual(hp.MaxHp, hp.CurrentHp, 0.001f, "3층인데 궤적 파열 폭발이 터졌다(4층 전용이어야 함)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier4_GrabsNearbyEnemy()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 4;
        var enemy = NewEnemy(new Vector3(2.2f, 10f, 0f)); // 붙잡기 반경(0.74) 안.
        yield return null;

        InvokeTryStormStart(blade, 4);
        Assert.IsTrue(GetStorming(blade));
        InvokeUpdateStorm(blade, 0.02f);

        Assert.AreEqual(1, GetStormGrabCount(blade), "가까운 적이 안 붙잡혔다(원본 :2691~2699)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier1_DoesNotGrabNearbyEnemy_Regression()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 1;
        var enemy = NewEnemy(new Vector3(2.2f, 10f, 0f));
        yield return null;

        InvokeTryStormStart(blade, 1);
        InvokeUpdateStorm(blade, 0.02f);

        Assert.AreEqual(0, GetStormGrabCount(blade), "1층인데 붙잡기가 됐다(4층 전용이어야 함)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier4_DoesNotGrabExemptEnemy()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 4;
        var boss = NewGrabExemptEnemy(new Vector3(2.2f, 10f, 0f));
        yield return null;

        InvokeTryStormStart(blade, 4);
        InvokeUpdateStorm(blade, 0.02f);

        Assert.AreEqual(0, GetStormGrabCount(blade),
            "IGrabExempt(보스·성소 대응, 원본 e.boss||e.shrine :2694) 적이 붙잡혔다");
    }

    [UnityTest]
    public IEnumerator Storm_Tier4_GrabCapRespected()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 4;
        for (int i = 0; i < 8; i++)
        {
            NewEnemy(new Vector3(2f + 0.01f * i, 10f, 0f)); // 전부 붙잡기 반경 안, 살짝씩만 흩어 겹침을 피한다.
        }
        yield return null;

        InvokeTryStormStart(blade, 4);
        InvokeUpdateStorm(blade, 0.02f);

        Assert.AreEqual(6, GetStormGrabCount(blade), "동시 최대 연행 수(S.grabMax=6)를 안 지켰다");
    }

    [UnityTest]
    public IEnumerator Storm_Tier4_GrabbedEnemy_SkipsSpinTick()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 4;
        var enemy = NewEnemy(new Vector3(2.2f, 10f, 0f)); // 붙잡기 반경 안 = 회전베기 반경 안이기도 하다.
        yield return null;

        InvokeTryStormStart(blade, 4);
        var hp = enemy.GetComponent<EnemyHealth>();

        InvokeUpdateStorm(blade, 0.02f); // 붙잡는다.
        Assert.AreEqual(1, GetStormGrabCount(blade), "테스트 전제: 붙잡히지 않았다");
        InvokeUpdateStorm(blade, 0.12f); // 누적 dt가 회전베기 유지 간격(0.11)을 넘겨 틱이 돈다.

        Assert.AreEqual(hp.MaxHp, hp.CurrentHp, 0.001f,
            "붙잡힌 적이 칼날폭풍 회전베기 유지 틱에 맞았다(원본 skipGrabbed :2486 미적용)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier4_EndReleasesGrabbedEnemyToFalling()
    {
        // 허공에 둔다 — `EndStorm()`을 직접 불러 해제 로직만 격리해서 본다. 실제 칼날폭풍이 바닥에
        // 닿아 끝나는 경로로 유도하면, 그 프레임에 같이 도는 2층 착지 광역 피해(`StormLand`, 붙잡힌
        // 적이 착지 지점 바로 옆이라 거의 항상 맞는다)가 적을 먼저 죽여 버려서 "죽지 않은 적만 낙하로
        // 넘어간다"(원본 `!e.dead`)는 규칙과 뒤섞인다 — 원본도 같은 프레임에 `bladeStormLand()`를
        // `bladeStormEnd()`보다 먼저 부르므로(project_test.html:2649~2650) 이 얽힘 자체는 정상 동작이다.
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 4;
        var enemy = NewEnemy(new Vector3(2.2f, 10f, 0f)); // 붙잡기 반경(0.74) 안.
        yield return null;

        InvokeTryStormStart(blade, 4);
        InvokeUpdateStorm(blade, 0.02f); // 붙잡는다.
        Assert.AreEqual(1, GetStormGrabCount(blade), "테스트 전제: 붙잡히지 않았다");

        InvokeEndStorm(blade);

        Assert.IsFalse(GetStorming(blade));
        Assert.AreEqual(0, GetStormGrabCount(blade), "칼날폭풍이 끝났는데 붙잡기 목록이 안 비었다");
        Assert.AreEqual(1, GetStormFallingCount(blade), "붙잡혔던 적이 낙하 목록으로 안 넘어갔다(원본 bladeStormEnd :2630~2637)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier4_FallingEnemy_DealsDamageOnLanding()
    {
        // 위 테스트와 같은 이유로 `EndStorm()`을 직접 불러 낙하 목록에 넣는다(착지 광역 피해와
        // 안 얽히게) — 이 테스트가 보려는 건 "낙하 자체가 착지 시 피해를 주는가"뿐이다.
        var go = NewBlade(new Vector3(2.2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 4;
        var enemy = NewEnemy(new Vector3(2.2f + 0.05f, 10f, 0f));
        yield return null;

        InvokeTryStormStart(blade, 4);
        InvokeUpdateStorm(blade, 0.02f); // 붙잡는다.
        Assert.AreEqual(1, GetStormGrabCount(blade), "테스트 전제: 붙잡히지 않았다");

        InvokeEndStorm(blade); // 낙하 목록으로 전환.
        Assert.AreEqual(1, GetStormFallingCount(blade), "테스트 전제: 낙하 목록에 들어가 있어야 한다");

        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.AreEqual(hp.MaxHp, hp.CurrentHp, 0.001f, "테스트 전제: 아직 낙하 충격을 받으면 안 된다");

        InvokeUpdateFalling(blade, 1.0f); // 큰 dt 한 번으로 허공(y≈10)에서 바닥까지 보낸다.

        Assert.AreEqual(0, GetStormFallingCount(blade), "착지했는데 낙하 목록에서 안 빠졌다");
        Assert.Less(hp.CurrentHp, hp.MaxHp, "낙하한 적이 착지 충격을 안 받았다(원본 bladeFallingUpdate :2724~2737)");
    }

    // ===== 5층: 집중 재시전 궤적 파열 · 패스 틱 쿨타임 환급 · 칼날폭풍 곡예비행 =====

    [UnityTest]
    public IEnumerator Focus_Tier5_RecastRipsRemainingTrail()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 5;
        var enemy = NewEnemy(new Vector3(2.1f, 0.5f, 0f)); // 궤적 파열 반경 안.
        yield return null;

        InvokeTryFocusStart(blade, 5);
        InvokeUpdateFocus(blade, 0.01f); // 궤적 점 하나.
        go.transform.position = new Vector3(2.3f, 0.5f, 0f);
        InvokeUpdateFocus(blade, 0.01f); // 두 번째 점 → trailPts.Count>1.
        Assert.Greater(GetTrailPtsCount(blade), 1, "테스트 전제: 궤적 점이 2개 이상이어야 한다");

        // focusT를 0으로 만들어 지속시간이 자연 만료된 상태를 흉내 낸다(원본도 지속시간이 궤적
        // 수명보다 짧아 focusT만 먼저 꺼지고 궤적은 남아 있는 상황이 실제로 생긴다) — 쿨다운도
        // 0으로 만들어 바로 재시전할 수 있게 한다.
        SetFocusT(blade, 0f);
        SetFocusCd(blade, 0f);

        var hp = enemy.GetComponent<EnemyHealth>();
        Assert.AreEqual(hp.MaxHp, hp.CurrentHp, 0.001f, "테스트 전제: 재시전 전에는 아직 안 맞아야 한다");

        InvokeTryFocusStart(blade, 5); // 새 시전 — 남은 궤적부터 먼저 찢는다(:2526).

        Assert.Less(hp.CurrentHp, hp.MaxHp, "5층 재시전인데 남은 궤적이 안 찢어졌다(원본 bladeFocusStart :2526)");
    }

    [UnityTest]
    public IEnumerator Focus_Tier4_RecastDoesNotRipTrail_Regression()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 4;
        var enemy = NewEnemy(new Vector3(2.1f, 0.5f, 0f));
        yield return null;

        InvokeTryFocusStart(blade, 4);
        InvokeUpdateFocus(blade, 0.01f);
        go.transform.position = new Vector3(2.3f, 0.5f, 0f);
        InvokeUpdateFocus(blade, 0.01f);
        Assert.Greater(GetTrailPtsCount(blade), 1, "테스트 전제: 궤적 점이 2개 이상이어야 한다");

        SetFocusT(blade, 0f);
        SetFocusCd(blade, 0f);

        var hp = enemy.GetComponent<EnemyHealth>();
        InvokeTryFocusStart(blade, 4); // 4층은 재시전으로 안 찢어야 한다(5층 전용).

        Assert.AreEqual(hp.MaxHp, hp.CurrentHp, 0.001f,
            "4층인데 재시전으로 궤적 파열이 터졌다(5층 전용이어야 함, 회귀 확인)");
    }

    [UnityTest]
    public IEnumerator Focus_Tier5_PassTick_ReducesFocusCooldown()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 5;
        NewEnemy(new Vector3(2f, 0.5f, 0f)); // 완전히 겹치는 위치 — 확실히 스친다.
        yield return null;

        SetFocusCd(blade, 5f);
        InvokeUpdatePassTick(blade, true); // 최고 속도로 취급.

        Assert.AreEqual(5f - 0.55f, GetFocusCd(blade), 0.001f,
            "5층 최고 속도로 적을 스쳤는데 집중 쿨타임이 안 깎였다(원본 bladePassTick :2594~2608, cdRedPerPass=0.55)");
    }

    [UnityTest]
    public IEnumerator Focus_Tier5_PassTick_DoesNotDoubleApplyWithinInterval()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 5;
        NewEnemy(new Vector3(2f, 0.5f, 0f));
        yield return null;

        SetFocusCd(blade, 5f);
        InvokeUpdatePassTick(blade, true);
        float afterFirst = GetFocusCd(blade);
        InvokeUpdatePassTick(blade, true); // 같은 프레임 — 0.5초(PassTickInterval) 이내라 또 깎이면 안 된다.

        Assert.AreEqual(afterFirst, GetFocusCd(blade), 0.001f,
            "패스 간격(0.5초) 안인데 쿨타임이 또 깎였다(원본이 적별 마지막 적용 시각을 기억하는 이유)");
    }

    [UnityTest]
    public IEnumerator Focus_Tier3_PassTick_DoesNothing_Regression()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Focus;
        ProfileService.Current.bladeTier = 3;
        NewEnemy(new Vector3(2f, 0.5f, 0f));
        yield return null;

        SetFocusCd(blade, 5f);
        InvokeUpdatePassTick(blade, true);

        Assert.AreEqual(5f, GetFocusCd(blade), 0.001f,
            "3층인데 패스 틱 쿨타임 환급이 적용됐다(5층 전용이어야 함, 회귀 확인)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier5_PassTick_DoesNothing_Regression()
    {
        var go = NewBlade(new Vector3(2f, 0.5f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 5;
        NewEnemy(new Vector3(2f, 0.5f, 0f));
        yield return null;

        SetFocusCd(blade, 5f);
        InvokeUpdatePassTick(blade, true);

        Assert.AreEqual(5f, GetFocusCd(blade), 0.001f,
            "칼날폭풍 갈래인데 집중 쿨타임 환급이 적용됐다(패스 틱은 집중 전용이어야 함)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier5_HopEndsStormAndRefundsCooldown()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 5;
        yield return null;

        InvokeTryStormStart(blade, 5);
        Assert.IsTrue(GetStorming(blade), "테스트 전제: 칼날폭풍이 시작돼 있어야 한다");
        Assert.Greater(GetStormCd(blade), 0f, "테스트 전제: 쿨다운이 걸려 있어야 한다");

        bool hopped = InvokeTryStormHop(blade, true);

        Assert.IsTrue(hopped, "5층인데 도약이 안 일어났다");
        Assert.IsFalse(GetStorming(blade), "도약했는데 칼날폭풍이 안 끝났다(원본 :2664 bladeStormEnd())");
        Assert.AreEqual(0f, GetStormCd(blade), 0.001f, "도약했는데 쿨타임이 즉시 안 돌아왔다(원본 :2669 p.stormCd=0)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier5_HopSetsUpwardVelocityScaledByStormSpeed()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        var rb = go.GetComponent<Rigidbody2D>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 5;
        yield return null;

        InvokeTryStormStart(blade, 5);
        SetStormSpd(blade, 9f); // 임의의 활공 속도로 고정 — 공식 검증용.

        InvokeTryStormHop(blade, true);

        // 원본 hopVy = S.hopVy(-720px/s) + st.spd*S.hopSpdK(0.55), 원본은 Y+아래라 음수=위로 솟구침.
        // 이 포트는 Y+가 위라 부호를 반전: 7.2(=720/100) + stormSpd*0.55.
        float expected = 7.2f + 9f * 0.55f;
        Assert.AreEqual(expected, rb.linearVelocity.y, 0.01f,
            "도약 상승 속도 공식이 원본과 다르다(원본 :2662~2673)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier1_HopDoesNothing_Regression()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 1;
        yield return null;

        InvokeTryStormStart(blade, 1);
        Assert.IsTrue(GetStorming(blade), "테스트 전제: 칼날폭풍이 시작돼 있어야 한다");

        bool hopped = InvokeTryStormHop(blade, true);

        Assert.IsFalse(hopped, "1층인데 도약이 일어났다(5층 전용이어야 함, 회귀 확인)");
        Assert.IsTrue(GetStorming(blade), "1층에서 점프 입력만으로 칼날폭풍이 끊겼다(5층 전용 분기가 새고 있음)");
    }

    [UnityTest]
    public IEnumerator Storm_Tier5_HopReleasesGrabbedEnemiesToFalling()
    {
        var go = NewBlade(new Vector3(2f, 10f, 0f));
        var blade = go.GetComponent<BladeCombat>();
        ProfileService.Current.bladeBranch = BladeBranch.Storm;
        ProfileService.Current.bladeTier = 5;
        var enemy = NewEnemy(new Vector3(2.2f, 10f, 0f)); // 붙잡기 반경 안.
        yield return null;

        InvokeTryStormStart(blade, 5);
        InvokeUpdateStorm(blade, 0.02f); // 붙잡는다.
        Assert.AreEqual(1, GetStormGrabCount(blade), "테스트 전제: 붙잡히지 않았다");

        InvokeTryStormHop(blade, true);

        Assert.AreEqual(0, GetStormGrabCount(blade), "도약 후에도 붙잡기 목록이 안 비었다");
        Assert.AreEqual(1, GetStormFallingCount(blade),
            "도약(=bladeStormEnd 경유)인데 붙잡힌 적이 낙하 목록으로 안 넘어갔다");
        Assert.IsNotNull(enemy, "테스트 전제: 적이 유지돼야 한다");
    }
}

}
