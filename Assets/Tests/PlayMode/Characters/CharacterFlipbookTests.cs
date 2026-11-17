using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Characters;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 캐릭터 몸 그림 재생(2026-11-17 애니메이션 플립북 전환 — `docs/animation-flipbook-proposal.md`).
/// 우선순위 피격 &gt; 공격 &gt; 유지(레이저) &gt; 루프, 공격 중 공격은 재시작 안 함, 피격은 공격을 끊고 끝나면 유지 자세로 돌아간다.
/// </summary>
public class CharacterFlipbookTests
{
    [SetUp]
    public void Setup() { ProfileService.Reset(); RunState.Reset(); }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go.name.StartsWith("TestFlip")) Object.DestroyImmediate(go);
        ProfileService.Reset(); RunState.Reset();
    }

    static SpriteFlipbook Book(string name, int n, float fps, bool loop)
    {
        var b = ScriptableObject.CreateInstance<SpriteFlipbook>();
        var tex = new Texture2D(2, 2);
        b.frames = new Sprite[n];
        for (int i = 0; i < n; i++) { b.frames[i] = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f)); b.frames[i].name = $"{name}{i}"; }
        b.fps = fps; b.loop = loop;
        return b;
    }

    static CharacterAnimSet Set()
    {
        var s = ScriptableObject.CreateInstance<CharacterAnimSet>();
        s.idle = Book("idle", 2, 10f, true);
        s.move = Book("move", 3, 10f, true);
        s.rise = Book("rise", 2, 10f, false);
        s.fall = Book("fall", 2, 10f, false);
        s.charge = Book("charge", 2, 10f, true);
        s.hit = Book("hit", 2, 10f, false);       // 0.2초
        s.attacks = new[] { Book("atk", 4, 10f, false) }; // 0.4초
        return s;
    }

    static (CharacterFlipbook fb, SpriteRenderer sr) NewFlip()
    {
        var go = new GameObject("TestFlip");
        var sr = go.AddComponent<SpriteRenderer>();
        var fb = go.AddComponent<CharacterFlipbook>();
        fb.SetAnimSet(Set());
        return (fb, sr);
    }

    [Test]
    public void LoopPose_Plays_AndKeepsTimeWhileSamePose()
    {
        var (fb, sr) = NewFlip();
        StringAssert.StartsWith("idle", sr.sprite.name, "처음엔 서기");
        fb.SetLoopPose(CharacterPose.Move);
        fb.Refresh(0.15f);
        Assert.AreEqual("move1", sr.sprite.name);
        fb.SetLoopPose(CharacterPose.Move); // 같은 자세를 매 프레임 넣어도 처음으로 안 돌아간다
        fb.Refresh(0.1f);
        Assert.AreEqual("move2", sr.sprite.name);
    }

    [Test]
    public void Attack_PlaysOnce_NoRestart_ThenBackToLoop()
    {
        var (fb, sr) = NewFlip();
        fb.SetLoopPose(CharacterPose.Move);
        fb.PlayOnce(CharacterPose.Attack);
        fb.Refresh(0.25f);
        Assert.AreEqual("atk2", sr.sprite.name);
        fb.PlayOnce(CharacterPose.Attack); // 연사 — 처음부터 다시 틀지 않는다
        fb.Refresh(0.1f);
        Assert.AreEqual("atk3", sr.sprite.name, "재시작 안 함");
        fb.Refresh(0.1f);
        Assert.AreEqual(CharacterPose.Move, fb.Current, "다 돌면 루프 자세로");
        StringAssert.StartsWith("move", sr.sprite.name);
    }

    [Test]
    public void Hit_InterruptsAttack_AttackDuringHitIgnored()
    {
        var (fb, sr) = NewFlip();
        fb.PlayOnce(CharacterPose.Attack);
        fb.Refresh(0.1f);
        fb.PlayOnce(CharacterPose.Hit);
        fb.Refresh(0.01f);
        Assert.AreEqual(CharacterPose.Hit, fb.Current, "피격이 공격을 끊는다");
        fb.PlayOnce(CharacterPose.Attack);
        fb.Refresh(0.01f);
        Assert.AreEqual(CharacterPose.Hit, fb.Current, "피격 중 공격은 무시");
        StringAssert.StartsWith("hit", sr.sprite.name);
    }

    [Test]
    public void Hold_KeepsLastAttackFrame_HitReturnsToHold_ReleaseToLoop()
    {
        var (fb, sr) = NewFlip();
        fb.PlayAndHold(CharacterPose.Attack);
        fb.Refresh(1f);
        Assert.AreEqual("atk3", sr.sprite.name, "레이저 — 공격 마지막 프레임을 붙잡는다");
        fb.PlayOnce(CharacterPose.Hit);
        fb.Refresh(0.05f);
        StringAssert.StartsWith("hit", sr.sprite.name);
        fb.Refresh(0.3f);
        Assert.AreEqual("atk3", sr.sprite.name, "피격이 끝나도 레이저가 켜져 있으면 붙잡은 자세로");
        fb.ReleaseHeldPose();
        fb.Refresh(0.01f);
        StringAssert.StartsWith("idle", sr.sprite.name, "놓으면 루프 자세");
    }

    [Test]
    public void LoopPose_Choice_ChargeOverAirOverMove()
    {
        Assert.AreEqual(CharacterPose.Charge, PlayerRig.LoopPose(true, false, new Vector2(3f, 5f)), "차지가 제일 먼저");
        Assert.AreEqual(CharacterPose.Rise, PlayerRig.LoopPose(false, false, new Vector2(0f, 2f)));
        Assert.AreEqual(CharacterPose.Fall, PlayerRig.LoopPose(false, false, new Vector2(0f, -2f)));
        Assert.AreEqual(CharacterPose.Move, PlayerRig.LoopPose(false, true, new Vector2(1f, 0f)));
        Assert.AreEqual(CharacterPose.Idle, PlayerRig.LoopPose(false, true, new Vector2(0.01f, 0f)), "멈출 때 남는 미세 속도는 서기");
    }

    /// <summary>
    /// 2026-11-17 회귀: 예전엔 공격 모션을 효과음 이벤트(기본 공격 것만)에 걸어서 불·블랙홀 법사는 공격 모션이 안 나왔다.
    /// 이제 발사 지점이 직접 알린다 — 갈래와 상관없이 공격 자세.
    /// </summary>
    [Test]
    public void MageFire_PlaysAttack_ForEveryBranch()
    {
        foreach (var branch in new[] { MageBranch.None, MageBranch.Explosion, MageBranch.Gravity })
        {
            var go = new GameObject("TestFlipMage");
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<CircleCollider2D>().radius = 0.25f;
            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            go.AddComponent<CharacterMover2D>();
            var fb = go.AddComponent<CharacterFlipbook>();
            fb.SetAnimSet(Set());
            var mage = go.AddComponent<MageAttack>().Equipped();
            ProfileService.Current.mageBranch = branch;
            typeof(MageAttack).GetMethod("FireAimed", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(mage, new object[] { 0f, branch, 0, 1f, 0f });
            fb.Refresh(0.01f);
            Assert.AreEqual(CharacterPose.Attack, fb.Current, $"{branch}: 발사하면 공격 자세");
            Object.DestroyImmediate(go);
        }
    }
}
}
