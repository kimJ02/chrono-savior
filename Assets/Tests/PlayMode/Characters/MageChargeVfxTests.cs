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

    /// <summary>손 오브(2026-11-16): 차지하는 동안만 보이고, 차지율을 11장에 그대로 매핑한다(0%=1번째·100%=11번째). 그림이 없는 클론은 생략.</summary>
    [Test]
    public void Orb_ShowsOnlyWhileCharging_MapsChargeRatioToFrame()
    {
        var mage = NewMage();
        var vfx = mage.GetComponent<MageChargeVfx>();
        var frames = vfx.OrbArt.frames;
        Assert.AreEqual(11, frames.Length, "오브 11장");
        vfx.Refresh(0.016f);
        Assert.IsFalse(vfx.OrbVisible, "차지 전엔 안 보인다");

        Charging.SetValue(mage, true);
        ChargeT.SetValue(mage, 0f);
        vfx.Refresh(0.016f);
        Assert.IsTrue(vfx.OrbVisible, "차지하는 동안 보인다");
        var orb = mage.transform.Find("ChargeOrb").GetComponent<SpriteRenderer>();
        Assert.AreSame(frames[0], orb.sprite, "0%는 1번 프레임");

        ChargeT.SetValue(mage, 1f); // chargeMax 기본 1초라 ChargeRatio=1
        vfx.Refresh(0.016f);
        Assert.AreSame(frames[10], orb.sprite, "100%는 11번 프레임");

        Charging.SetValue(mage, false);
        vfx.Refresh(0.016f);
        Assert.IsFalse(vfx.OrbVisible, "차지가 끝나면 사라진다");
    }

    /// <summary>오브 일렁임(2026-11-16, 사용자 "KiEffect는 발사순간이 아니라 차지 9장쯤부터 오브 근처에 일렁이듯"):
    /// 오브가 9번째 프레임(0-based 인덱스 8)에 들어서야 비로소 같이 보인다.</summary>
    [Test]
    public void Ki_ShowsOnlyOnceOrbReachesFrame9()
    {
        var mage = NewMage();
        var vfx = mage.GetComponent<MageChargeVfx>();
        Charging.SetValue(mage, true);

        ChargeT.SetValue(mage, 0.7f); // 오브 인덱스 round(0.7*10)=7 — 아직 9번째(인덱스 8) 전
        vfx.Refresh(0.016f);
        Assert.IsFalse(vfx.KiVisible, "오브가 9번째에 닿기 전엔 일렁임이 안 보인다");

        ChargeT.SetValue(mage, 0.8f); // round(0.8*10)=8 — 9번째(인덱스 8)
        vfx.Refresh(0.016f);
        Assert.IsTrue(vfx.KiVisible, "9번째부터 같이 보인다");

        ChargeT.SetValue(mage, 0.5f); // 다시 내려가면(이론상 안 그러지만) 꺼진다
        vfx.Refresh(0.016f);
        Assert.IsFalse(vfx.KiVisible);
    }

    /// <summary>블랙홀(중력) 갈래는 보라 그림, 폭발·0차는 빨강 원본(2026-11-17 사용자 "블랙홀 법사 손에서 불이 차징되는 게 문제" → 후보 A).</summary>
    [Test]
    public void OrbAndKi_GravityBranchUsesVioletFrames()
    {
        var probe = NewMage().GetComponent<MageChargeVfx>();
        Assert.AreEqual(11, probe.OrbArtGravity.Count, "보라 오브 11장");
        Assert.AreEqual(6, probe.KiArtGravity.Count, "보라 일렁임 6장");
        foreach (var f in probe.OrbArtGravity.frames) StringAssert.Contains("Gravity", f.name);

        var mage = NewMage();
        var vfx = mage.GetComponent<MageChargeVfx>();
        Charging.SetValue(mage, true);
        ChargeT.SetValue(mage, 1f);

        foreach (var (branch, violet) in new[] { (MageBranch.Gravity, true), (MageBranch.Explosion, false), (MageBranch.None, false) })
        {
            ProfileService.Current.mageBranch = branch;
            vfx.Refresh(0.016f);
            var orb = mage.transform.Find("ChargeOrb").GetComponent<SpriteRenderer>();
            var ki = mage.transform.Find("ChargeKi").GetComponent<SpriteRenderer>();
            Assert.AreSame(vfx.OrbFrames(branch)[10], orb.sprite, $"{branch} 오브");
            Assert.AreEqual(violet, orb.sprite.name.Contains("Gravity"), $"{branch} 오브 색");
            Assert.Greater(ki.sortingOrder, orb.sortingOrder, "일렁임은 오브 앞");
            Assert.AreEqual(violet, ki.sprite.name.Contains("Gravity"), $"{branch} 일렁임");
        }
    }

    /// <summary>갈래·완충별 탄 그림 — 그림은 정수 배율로 갈아 끼우고, 판정 상자는 그대로다. 0차 탄도 2026-11-16부터 그림(파랑). 그림이 없는 클론은 생략.</summary>
    [Test]
    public void Bolt_ShowsBranchArt_HitboxUnchanged()
    {
        Assume.That(ChronoSavior.Combat.VfxFrames.Has(ChronoSavior.Combat.VfxKeys.MageBasicProjectileLarge), "이펙트 팩을 안 가져온(또는 2026-11-16 이전에 가져온) 클론 — 미설치로 생략");
        MageProjectile Bolt(bool explosive, bool gravity, bool charged) => MageProjectile.Spawn(TestPrefabs.MageBolt, new Vector3(5f, 3f, 0f), Vector2.zero, 1f, 1, 5f, 1.9f,
            Color.cyan, false, charged, explosive, 1f, gravity, 0f, 1, Vector2.zero, 1);

        // 2026-11-16(팀원 로그 116·117): 갈래마다 완충이면 큰 그림, 아니면 작은 그림. 0차(갈래 없음)도 이제 그림이 있다(energy_blast B 파랑).
        foreach (var (explosive, gravity, charged, key) in new[]
        {
            (true, false, false, ChronoSavior.Combat.VfxKeys.MageFireProjectile), (true, false, true, ChronoSavior.Combat.VfxKeys.MageFireProjectileLarge),
            (false, true, false, ChronoSavior.Combat.VfxKeys.MageGravityOrbSmall), (false, true, true, ChronoSavior.Combat.VfxKeys.MageGravityOrbLarge),
            (false, false, false, ChronoSavior.Combat.VfxKeys.MageBasicProjectile), (false, false, true, ChronoSavior.Combat.VfxKeys.MageBasicProjectileLarge),
        })
        {
            var bolt = Bolt(explosive, gravity, charged);
            Assert.AreEqual(new Vector2(0.4f * 1.9f, 0.3f * 1.9f), bolt.GetComponent<BoxCollider2D>().size, "판정 상자는 그대로");
            var player = bolt.GetComponentInChildren<ChronoSavior.Combat.VfxPlayer>();
            Assert.IsNotNull(player, $"{key} 그림이 돈다");
            float s = player.transform.localScale.x;
            Assert.AreEqual(Mathf.Round(s), s, 1e-4f, "정수 배율");
            CollectionAssert.Contains(ChronoSavior.Combat.VfxFrames.Get(key), player.GetComponent<SpriteRenderer>().sprite, $"{key} — 그 갈래·완충 여부의 그림");
        }
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
        Assert.IsNull(GameObject.Find("Vfx_mage_collapse_explosion"), "아무 데서도 안 터진다");

        ChronoSavior.Combat.GravityWellZone.SpawnOrMerge(mage.transform.position + Vector3.right, 3, 0.5f);
        tryUse.Invoke(mage, new object[] { ProfileService.Current, MageBranch.Gravity, 3 });
        Assert.Greater((float)ultCd.GetValue(mage), 0f, "깔아 둔 블랙홀이 있으면 터뜨리고 쿨다운이 돈다");
        ChronoSavior.Combat.GravityWellZone.ClearAllForTests();
        foreach (var v in Object.FindObjectsByType<ChronoSavior.Combat.VfxPlayer>(FindObjectsSortMode.None)) Object.DestroyImmediate(v.gameObject);
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
