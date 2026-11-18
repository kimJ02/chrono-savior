using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Core;
using Lock = ChronoSavior.Characters.CharacterMover2D.AttackMoveLock;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 공격 중 이동 잠금(2026-11-16 사용자 결정, 의도적 편차 — 원본은 마법사·메카닉도 공격 중 이동 자유 `attackRapid`).
/// 공격 그림이 제자리에 선 자세라, 땅에선 마법사 차지·발사 자세와 메카닉 레이저 동안 좌우 이동·점프를 막는다(뒤돌기는 된다).
/// 공중: 마법사는 원본대로 자유, 레이저는 새 좌우 입력만 무시하고 지금 가로 속도·중력 유지. 메카닉 일반 사격·섬영은 그대로.
/// 키 입력은 흉내 낼 수 없어 순수 함수 + 리플렉션으로 상태를 켜고 실제 Update/FixedUpdate를 돌린다.
/// </summary>
public class AttackMoveLockTests
{
    static GameObject NewPlayer(Vector3 pos)
    {
        var go = new GameObject("TestPlayer");
        go.tag = "Player";
        go.AddComponent<CircleCollider2D>().radius = 0.25f;
        go.AddComponent<Rigidbody2D>();
        go.transform.position = pos;
        go.AddComponent<CharacterMover2D>();
        return go;
    }

    static readonly FieldInfo Charging = typeof(MageAttack).GetField("charging", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo FirePose = typeof(MageAttack).GetField("firePoseTimer", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo Coyote = typeof(CharacterMover2D).GetField("coyoteTimer", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo Buffer = typeof(CharacterMover2D).GetField("jumpBufferTimer", BindingFlags.NonPublic | BindingFlags.Instance);

    [Test]
    public void ApplyAttackLock_Table()
    {
        // 잠금 없음 — 입력 그대로(원본)
        Assert.AreEqual(2.7f, CharacterMover2D.ApplyAttackLock(Lock.None, true, 2.7f, 5f));
        Assert.AreEqual(-2.7f, CharacterMover2D.ApplyAttackLock(Lock.None, false, -2.7f, 5f));
        // 땅 — 둘 다 제자리
        Assert.AreEqual(0f, CharacterMover2D.ApplyAttackLock(Lock.RootOnGround, true, 2.7f, 5f));
        Assert.AreEqual(0f, CharacterMover2D.ApplyAttackLock(Lock.RootOnGroundNoSteerInAir, true, 2.7f, 5f));
        // 공중 — 마법사는 입력대로(원본), 레이저는 지금 속도 유지(새 입력 무시)
        Assert.AreEqual(-1.35f, CharacterMover2D.ApplyAttackLock(Lock.RootOnGround, false, -1.35f, 5f));
        Assert.AreEqual(5f, CharacterMover2D.ApplyAttackLock(Lock.RootOnGroundNoSteerInAir, false, -2.7f, 5f));
    }

    [Test]
    public void JumpBlocked_OnlyWhenLockedOnGroundOrCoyote()
    {
        Assert.IsFalse(CharacterMover2D.JumpBlockedByAttack(Lock.None, true));
        Assert.IsTrue(CharacterMover2D.JumpBlockedByAttack(Lock.RootOnGround, true));
        Assert.IsTrue(CharacterMover2D.JumpBlockedByAttack(Lock.RootOnGroundNoSteerInAir, true));
        Assert.IsFalse(CharacterMover2D.JumpBlockedByAttack(Lock.RootOnGround, false), "공중엔 점프할 땅이 없다 — 막을 것도 없다");
    }

    /// <summary>잠금 중엔 코요테 구간이어도 점프가 안 나가고, 눌렀던 점프는 버린다(버튼을 뗀 뒤 저절로 뛰지 않게).</summary>
    [UnityTest]
    public IEnumerator Locked_JumpInputIsDropped_EvenInCoyoteWindow()
    {
        var go = NewPlayer(new Vector3(2.2f, 30f, 0f)); // 허공 — CheckGrounded가 늘 false
        var mover = go.GetComponent<CharacterMover2D>();
        mover.AttackLock = Lock.RootOnGround;
        Coyote.SetValue(mover, 0.05f);
        Buffer.SetValue(mover, 0.13f);
        yield return null;
        Assert.Less(go.GetComponent<Rigidbody2D>().linearVelocity.y, mover.jumpSpeed - 0.5f, "공격 잠금 중인데 점프가 나갔다");
        Assert.LessOrEqual((float)Buffer.GetValue(mover), 0f, "막힌 점프 입력이 남아 있다 — 잠금이 풀리는 순간 저절로 뛴다");
        Object.Destroy(go);
        yield return null;
    }

    /// <summary>공중 레이저: 새 좌우 입력이 없어도(즉시 이동 모드라면 0이 되는 자리) 지금 가로 속도를 그대로 가져간다.</summary>
    [UnityTest]
    public IEnumerator AirLaser_KeepsCurrentHorizontalSpeed_AirMageDoesNot()
    {
        var go = NewPlayer(new Vector3(8f, 30f, 0f));
        var mover = go.GetComponent<CharacterMover2D>();
        var rb = go.GetComponent<Rigidbody2D>();
        mover.AttackLock = Lock.RootOnGroundNoSteerInAir;
        rb.linearVelocity = new Vector2(2f, 0f);
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(2f, rb.linearVelocity.x, 0.01f, "공중 레이저인데 가로 속도가 사라졌다(관성 유지가 아니다)");
        Assert.Less(rb.linearVelocity.y, 0f, "공중 레이저 중에도 중력은 그대로 — 떠 있으면 안 된다");

        mover.AttackLock = Lock.RootOnGround; // 공중 마법사 차지 — 원본대로 입력이 정한다(입력 없음 = 0)
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(0f, rb.linearVelocity.x, 0.01f);
        Object.Destroy(go);
        yield return null;
    }

    /// <summary>마법사: 차지 중·발사 자세 동안 땅 제자리 잠금, 자세가 끝나면 풀린다. 캐릭터에서 빠지면 바로 풀린다.</summary>
    [UnityTest]
    public IEnumerator Mage_LocksWhileChargingAndFirePose_ThenReleases()
    {
        var go = NewPlayer(new Vector3(2.2f, FieldBounds.GroundY + 0.5f, 0f));
        var mage = go.AddComponent<MageAttack>().Equipped();
        var mover = go.GetComponent<CharacterMover2D>();
        yield return null;
        Assert.AreEqual(Lock.None, mover.AttackLock);

        Charging.SetValue(mage, true);
        yield return null;
        Assert.AreEqual(Lock.RootOnGround, mover.AttackLock, "차지 중인데 제자리 잠금이 아니다");
        Assert.AreEqual(mage.chargeMoveSlow, mover.SpeedMultiplier, 1e-4f, "공중 차지 감속(원본 0.5)은 그대로");

        Charging.SetValue(mage, false);
        FirePose.SetValue(mage, 0.1f); // 방금 쐈다
        yield return null;
        Assert.AreEqual(Lock.RootOnGround, mover.AttackLock, "발사 자세 동안은 아직 제자리");
        yield return new WaitForSeconds(0.15f);
        Assert.AreEqual(Lock.None, mover.AttackLock, "발사 자세가 끝났는데 아직 잠겨 있다");

        Charging.SetValue(mage, true);
        yield return null;
        mage.OnDeselected();
        Assert.AreEqual(Lock.None, mover.AttackLock, "다른 캐릭터로 바뀌었는데 잠금이 남았다");
        Object.Destroy(go);
        yield return null;
    }

    /// <summary>발사 자세 시간 = 원본 attackDur(0.2 ÷ 공속 배수) — 공속이 오르면 짧아진다.</summary>
    [Test]
    public void FirePose_IsOriginalAttackDuration()
    {
        Assert.AreEqual(0.2f, MageAttack.FirePoseBase, 1e-6f, "원본 bowFire `p.attackDur = 0.2 / statAs()`(:1939)");
    }

    /// <summary>메카닉: 사냥 밖이거나 캐릭터에서 빠지면 잠금이 없다(일반 사격·차징 없는 상태도 None).</summary>
    [UnityTest]
    public IEnumerator Gunner_NotLocked_OutsideLaserOrWhenDeselected()
    {
        var go = NewPlayer(new Vector3(2.2f, FieldBounds.GroundY + 0.5f, 0f));
        var gunner = go.AddComponent<GunnerAttack>();
        var mover = go.GetComponent<CharacterMover2D>();
        mover.AttackLock = Lock.RootOnGroundNoSteerInAir; // 남아 있던 잠금
        yield return null; // 테스트는 사냥 밖(GameState 기본 로비) — Update가 None으로 돌린다
        Assert.AreEqual(Lock.None, mover.AttackLock, "레이저를 안 쏘는데 잠겨 있다");
        mover.AttackLock = Lock.RootOnGroundNoSteerInAir;
        gunner.OnDeselected();
        Assert.AreEqual(Lock.None, mover.AttackLock);
        Object.Destroy(go);
        yield return null;
    }

    /// <summary>실제 바닥 위(CheckGrounded 경로): 레이저 잠금이면 남은 가로 속도가 0이 되고(같은 잠금이 공중에선 속도를 유지하는 것과 대조), 점프 입력이 버려진다. 잠금이 없으면 같은 입력으로 뛴다.</summary>
    [UnityTest]
    public IEnumerator OnRealGround_LockStopsSlideAndJump_UnlockedJumps()
    {
        var ground = new GameObject("TestGround");
        ground.layer = LayerMask.NameToLayer("Ground");
        ground.AddComponent<BoxCollider2D>().size = new Vector2(40f, 1f);
        ground.transform.position = new Vector3(8f, -0.5f, 0f);       // 윗면 y = 0
        var go = NewPlayer(new Vector3(8f, 0.26f, 0f));                   // 발이 윗면에 닿게(반지름 0.25)
        var mover = go.GetComponent<CharacterMover2D>();
        var rb = go.GetComponent<Rigidbody2D>();
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        mover.AttackLock = Lock.RootOnGroundNoSteerInAir;
        rb.linearVelocity = new Vector2(3f, 0f);                          // 넉백 같은 남은 속도
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(0f, rb.linearVelocity.x, 0.01f, "땅에서 레이저 잠금인데 미끄러진다(공중 규칙이 적용됐다 — 땅 판정 경로 확인)");

        Buffer.SetValue(mover, 0.13f);                                    // 점프를 눌렀다
        yield return null;
        yield return new WaitForFixedUpdate();
        Assert.Less(rb.linearVelocity.y, mover.jumpSpeed - 0.5f, "땅에서 공격 잠금인데 점프가 나갔다");

        mover.AttackLock = Lock.None;                                     // 버튼을 뗐다
        Buffer.SetValue(mover, 0.13f);
        yield return null;
        Assert.AreEqual(mover.jumpSpeed, rb.linearVelocity.y, 0.5f, "잠금이 풀렸는데 점프가 안 나간다(대조 — 이 바닥에서 원래는 뛴다)");
        Object.Destroy(go); Object.Destroy(ground);
        yield return null;
    }

    [Test]
    public void ResetMotion_ClearsLock()
    {
        var go = NewPlayer(Vector3.zero);
        var mover = go.GetComponent<CharacterMover2D>();
        mover.AttackLock = Lock.RootOnGround;
        mover.ResetMotion();
        Assert.AreEqual(Lock.None, mover.AttackLock, "캐릭터 전환·런 리셋 뒤에도 잠금이 남는다");
        Object.DestroyImmediate(go);
    }
}

}
