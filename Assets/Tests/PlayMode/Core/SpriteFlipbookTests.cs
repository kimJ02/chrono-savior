using NUnit.Framework;
using UnityEngine;
using ChronoSavior.Core;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>프레임 묶음(2026-11-17 애니메이션 플립북 전환) — t초에 몇 번째 그림인지, 되풀이·멈춤·빈 묶음.</summary>
public class SpriteFlipbookTests
{
    static Sprite[] Sprites(int n)
    {
        var tex = new Texture2D(2, 2);
        var s = new Sprite[n];
        for (int i = 0; i < n; i++) { s[i] = Sprite.Create(tex, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f)); s[i].name = "f" + i; }
        return s;
    }

    static SpriteFlipbook Book(int n, float fps, bool loop)
    {
        var b = ScriptableObject.CreateInstance<SpriteFlipbook>();
        b.frames = Sprites(n); b.fps = fps; b.loop = loop;
        return b;
    }

    [Test]
    public void Loop_WrapsAround()
    {
        var b = Book(4, 10f, true);
        Assert.AreEqual(0.4f, b.Duration, 1e-5f);
        Assert.AreEqual(0, b.IndexAt(0f));
        Assert.AreEqual(1, b.IndexAt(0.1f));
        Assert.AreEqual(3, b.IndexAt(0.39f));
        Assert.AreEqual(0, b.IndexAt(0.4f), "한 바퀴 돌면 처음으로");
        Assert.AreEqual(1, b.IndexAt(1.35f), "13번째 칸 = 13 % 4");
        Assert.IsFalse(b.IsFinished(10f), "되풀이는 끝나지 않는다");
    }

    [Test]
    public void Once_HoldsLastFrame_AndFinishes()
    {
        var b = Book(3, 12f, false);
        Assert.AreEqual(2, b.IndexAt(5f), "끝나면 마지막 프레임에 멈춘다");
        Assert.AreSame(b.frames[2], b.FrameAt(5f));
        Assert.IsFalse(b.IsFinished(0.2f));
        Assert.IsTrue(b.IsFinished(0.25f), "3장 ÷ 12fps = 0.25초");
    }

    [Test]
    public void EmptyOrZeroFps_IsSafe()
    {
        var empty = Book(0, 12f, true);
        Assert.AreEqual(-1, empty.IndexAt(1f));
        Assert.IsNull(empty.FrameAt(1f));
        Assert.AreEqual(0f, empty.Duration);
        Assert.IsFalse(empty.IsFinished(1f));

        var still = Book(3, 0f, false);
        Assert.AreEqual(0, still.IndexAt(9f), "fps 0이면 첫 그림 그대로");
        Assert.AreEqual(0, still.IndexAt(-1f), "음수 시간도 첫 그림");
    }

    [Test]
    public void AnimSet_FallsBackToIdle_AndWrapsAttacks()
    {
        var set = ScriptableObject.CreateInstance<CharacterAnimSet>();
        set.idle = Book(2, 8f, true);
        set.move = Book(3, 12f, true);
        set.attacks = new[] { Book(2, 12f, false), Book(2, 12f, false) };
        Assert.AreSame(set.move, set.Get(CharacterPose.Move));
        Assert.AreSame(set.idle, set.Get(CharacterPose.Rise), "없는 자세는 서기로");
        Assert.AreSame(set.idle, set.Get(CharacterPose.Hit));
        Assert.AreSame(set.attacks[1], set.Get(CharacterPose.Attack, 1));
        Assert.AreSame(set.attacks[0], set.Get(CharacterPose.Attack, 2), "공격 번호는 감는다");
        Assert.AreSame(set.attacks[1], set.Get(CharacterPose.Attack, -1));

        set.charge = Book(0, 6f, true);
        Assert.AreSame(set.idle, set.Get(CharacterPose.Charge), "빈 묶음도 서기로");
    }
}
}
