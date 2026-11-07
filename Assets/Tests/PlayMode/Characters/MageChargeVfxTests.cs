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
/// 마법사 차지 — 2026-10-25 사용자: "차지 단계마다 플레이어가 깜빡이게, 마법진은 없애"와 "로비 들어갔을 때 스킬이 한 번 써진다".
/// 단계는 원본 강화탄(50%)·완충(99%) 문턱. 사냥 중이 아니면 차지를 쏘지 않는다(메뉴의 Z "확인"이 로비에서 탄으로 나가던 버그).
/// </summary>
public class MageChargeVfxTests
{
    static readonly FieldInfo Charging = typeof(MageAttack).GetField("charging", BindingFlags.NonPublic | BindingFlags.Instance);
    static readonly FieldInfo ChargeT = typeof(MageAttack).GetField("chargeT", BindingFlags.NonPublic | BindingFlags.Instance);

    [SetUp]
    public void Setup()
    {
        ProfileService.Reset();
        GameState.Reset();
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null && (go.name.StartsWith("Test") || go.GetComponent<MageProjectile>() != null))
                Object.DestroyImmediate(go);
        ProfileService.Reset();
        GameState.Reset();
    }

    static MageAttack NewMage()
    {
        var go = new GameObject("TestMage");
        var tex = new Texture2D(8, 8);
        go.AddComponent<SpriteRenderer>().sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 16f);
        go.AddComponent<CircleCollider2D>().radius = 0.25f;
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CharacterMover2D>();
        go.AddComponent<PlayerHealth>();
        var mage = go.AddComponent<MageAttack>().Equipped();
        go.AddComponent<MageChargeVfx>().Equipped(); // 게임에선 씬의 Player에 같이 놓여 있다(2026-11-05 생성 책임 2-1)
        return mage;
    }

    /// <summary>차지 오라(2026-11-07 VFX): 차지하는 동안만 보이고, 크기는 월드 배율 1로 고정, 색은 갈래마다. 그림이 없는 클론은 생략.</summary>
    [Test]
    public void ChargeAura_ShowsOnlyWhileCharging_AtFixedWorldScale()
    {
        Assume.That(ChronoSavior.Combat.VfxFrames.Has(ChronoSavior.Combat.VfxKeys.MageChargeLarge), "이펙트 팩을 안 가져온 클론 — 미설치로 생략");
        ProfileService.Current.mageBranch = MageBranch.Gravity;
        var mage = NewMage();
        mage.transform.localScale = new Vector3(0.8f, 0.8f, 1f); // 씬의 Player처럼 1이 아닌 배율
        var vfx = mage.GetComponent<MageChargeVfx>();
        vfx.Refresh(0.016f);
        Assert.IsFalse(vfx.AuraVisible, "차지 전엔 안 보인다");

        Charging.SetValue(mage, true);
        ChargeT.SetValue(mage, 0.1f);
        vfx.Refresh(0.016f);
        Assert.IsTrue(vfx.AuraVisible, "차지하는 동안 보인다");
        var aura = mage.transform.Find("ChargeAura");
        Assert.AreEqual(1f, aura.lossyScale.x, 1e-4f, "크기 고정 — 월드 배율 1(도트 배율 1)");
        ChargeT.SetValue(mage, 1f);
        vfx.Refresh(0.016f);
        Assert.AreEqual(1f, aura.lossyScale.x, 1e-4f, "차지율과 상관없이 같은 크기");
        Assert.AreEqual(MageChargeVfx.GravityTint, aura.GetComponent<SpriteRenderer>().color, "중력 갈래는 보라");

        Charging.SetValue(mage, false);
        vfx.Refresh(0.016f);
        Assert.IsFalse(vfx.AuraVisible, "차지가 끝나면 사라진다");
    }

    [Test]
    public void ChargeAura_TintPerBranch()
    {
        Assert.AreEqual(Color.white, MageChargeVfx.AuraTint(MageBranch.None), "빌드 없음은 그림 그대로");
        Assert.AreEqual(MageChargeVfx.FireTint, MageChargeVfx.AuraTint(MageBranch.Explosion));
        Assert.AreEqual(MageChargeVfx.GravityTint, MageChargeVfx.AuraTint(MageBranch.Gravity));
    }

    /// <summary>탄 그림 배율은 판정 폭에 가장 가까운 정수(최소 1).</summary>
    [Test]
    public void BoltArtScale_IsNearestWholeNumber()
    {
        Assert.AreEqual(2, MageProjectile.ArtScale(0.4f, 0.1875f));
        Assert.AreEqual(5, MageProjectile.ArtScale(0.9f, 0.1875f));
        Assert.AreEqual(1, MageProjectile.ArtScale(0.1f, 0.375f), "최소 1");
    }

    /// <summary>갈래별 탄 그림 — 그림은 정수 배율로 갈아 끼우고, 판정 상자는 그대로다. 0차 탄은 예전 원 그림. 그림이 없는 클론은 생략.</summary>
    [Test]
    public void Bolt_ShowsBranchArt_HitboxUnchanged()
    {
        Assume.That(ChronoSavior.Combat.VfxFrames.Has(ChronoSavior.Combat.VfxKeys.MageFireProjectile), "이펙트 팩을 안 가져온 클론 — 미설치로 생략");
        MageProjectile Bolt(bool explosive, bool gravity) => MageProjectile.Spawn(TestPrefabs.MageBolt, new Vector3(5f, 3f, 0f), Vector2.zero, 1f, 1, 5f, 1.9f,
            Color.cyan, false, false, explosive, 1f, gravity, 0f, 1, Vector2.zero, 1);

        foreach (var (explosive, gravity, key) in new[] { (true, false, ChronoSavior.Combat.VfxKeys.MageFireProjectile), (false, true, ChronoSavior.Combat.VfxKeys.MageGravityOrbSmall) })
        {
            var bolt = Bolt(explosive, gravity);
            Assert.AreEqual(new Vector2(0.4f * 1.9f, 0.3f * 1.9f), bolt.GetComponent<BoxCollider2D>().size, "판정 상자는 그대로");
            var player = bolt.GetComponentInChildren<ChronoSavior.Combat.VfxPlayer>();
            Assert.IsNotNull(player, $"{key} 그림이 돈다");
            float s = player.transform.localScale.x;
            Assert.AreEqual(Mathf.Round(s), s, 1e-4f, "정수 배율");
            CollectionAssert.Contains(ChronoSavior.Combat.VfxFrames.Get(key), player.GetComponent<SpriteRenderer>().sprite, "그 갈래의 그림");
        }

        var plain = Bolt(false, false);
        Assert.IsNull(plain.GetComponentInChildren<ChronoSavior.Combat.VfxPlayer>(), "0차 탄은 예전 원 그림");
        Assert.AreEqual(Color.cyan, plain.GetComponentInChildren<SpriteRenderer>().color);
    }

    [Test]
    public void OnlyFullCharge_Flashes()
    {
        Assert.AreEqual(0, MageChargeVfx.StageAt(0.5f), "강화탄(50%)에선 안 번쩍인다(사용자 \"풀차징 되면 한 번만\")");
        Assert.AreEqual(0, MageChargeVfx.StageAt(0.98f));
        Assert.AreEqual(1, MageChargeVfx.StageAt(0.99f), "원본 완충 `chargeK >= 0.99`");
    }

    [Test]
    public void FlashesOnce_AtFullCharge_NotEveryFrame()
    {
        var mage = NewMage();
        var flash = mage.GetComponent<MageChargeVfx>();
        Assert.IsNotNull(flash, "MageAttack이 깜빡임을 붙인다");
        Charging.SetValue(mage, true);

        ChargeT.SetValue(mage, 0.55f);
        flash.Refresh(0.016f);
        Assert.AreEqual(0f, flash.FlashAmount, "50%에선 안 번쩍인다");

        ChargeT.SetValue(mage, 1f);
        flash.Refresh(0.016f);
        Assert.AreEqual(1f, flash.FlashAmount, "다 차는 순간 번쩍인다");
        var sr = mage.transform.Find("ChargeFlash").GetComponent<SpriteRenderer>();
        Assert.IsTrue(sr.enabled);
        Assert.AreSame(mage.GetComponent<SpriteRenderer>().sprite, sr.sprite, "몸 모양 그대로 하얗게");

        for (int i = 0; i < 20; i++) flash.Refresh(0.016f);
        Assert.AreEqual(0f, flash.FlashAmount, "한 번 번쩍이고 사라진다(완충을 계속 들고 있어도 다시 안 번쩍)");
        Assert.IsFalse(sr.enabled);
    }

    [Test]
    public void GravityX_WithoutBlackHole_DoesNothing_AndKeepsCooldown()
    {
        ChronoSavior.Combat.GravityWellZone.ClearAllForTests();
        ProfileService.Current.mageBranch = MageBranch.Gravity;
        ProfileService.Current.mageTier = 3;
        var mage = NewMage();
        var tryUse = typeof(MageAttack).GetMethod("TryUseSkill", BindingFlags.NonPublic | BindingFlags.Instance);
        var ultCd = typeof(MageAttack).GetField("ultCdTimer", BindingFlags.NonPublic | BindingFlags.Instance);

        tryUse.Invoke(mage, new object[] { ProfileService.Current, MageBranch.Gravity, 3 });
        Assert.LessOrEqual((float)ultCd.GetValue(mage), 0f, "블랙홀이 없으면 X가 안 나가고 쿨다운도 안 쓴다");
        Assert.AreEqual(0, Object.FindObjectsByType<ChronoSavior.Combat.GravityBurstRing>(FindObjectsSortMode.None).Length, "아무 데서도 안 터진다");

        ChronoSavior.Combat.GravityWellZone.SpawnOrMerge(mage.transform.position + Vector3.right, 3, 0.5f);
        tryUse.Invoke(mage, new object[] { ProfileService.Current, MageBranch.Gravity, 3 });
        Assert.Greater((float)ultCd.GetValue(mage), 0f, "깔아 둔 블랙홀이 있으면 터뜨리고 쿨다운이 돈다");
        ChronoSavior.Combat.GravityWellZone.ClearAllForTests();
        foreach (var b in Object.FindObjectsByType<ChronoSavior.Combat.GravityBurstRing>(FindObjectsSortMode.None)) Object.DestroyImmediate(b.gameObject);
    }

    [Test]
    public void ChargeCircle_IsGone()
    {
        var mage = NewMage();
        Assert.IsNull(System.Type.GetType("ChronoSavior.Characters.MageChargeCircle, ChronoSavior.Characters"), "마법진은 뺐다");
        Assert.IsNull(mage.transform.Find("MageChargeCircle"));
        Assert.IsNull(mage.transform.Find("ChargeIndicator"), "예전 하늘색 구체도 없다");
    }

    [UnityTest]
    public IEnumerator OutsideRun_HeldCharge_DoesNotFire_InsideRun_ItDoes()
    {
        var mage = NewMage();
        yield return null;
        Charging.SetValue(mage, true);
        ChargeT.SetValue(mage, 0.6f);

        // 로비(시간 멈춤) — Z를 안 누른 상태로 읽히지만 쏘면 안 된다
        yield return null;
        yield return null;
        Assert.AreEqual(0, Object.FindObjectsByType<MageProjectile>(FindObjectsSortMode.None).Length, "로비에서 탄이 나가면 안 된다");
        Assert.IsTrue(mage.IsCharging, "차지는 그대로 멈춰 둔다");

        GameState.Set(GameScene.Run);
        yield return null;
        Assert.IsFalse(mage.IsCharging, "사냥 중엔 Z를 뗀 것이라 쏜다");
        Assert.Greater(Object.FindObjectsByType<MageProjectile>(FindObjectsSortMode.None).Length, 0);
    }

    [Test]
    public void GameState_CountsChanges_AndRemembersPrevious()
    {
        int v = GameState.Version;
        GameState.Set(GameScene.Run);
        GameState.Set(GameScene.Pause);
        Assert.AreEqual(v + 2, GameState.Version);
        Assert.AreEqual(GameScene.Run, GameState.Previous, "일시정지에서 돌아올 땐 키를 안 잠근다 — 전 화면을 기억해야 안다");
        GameState.Set(GameScene.Pause);
        Assert.AreEqual(v + 2, GameState.Version, "같은 화면이면 안 바뀐다");
    }
}

}
