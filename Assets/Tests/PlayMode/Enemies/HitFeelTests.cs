using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ChronoSavior.Characters;
using ChronoSavior.Combat;
using ChronoSavior.Core;
using ChronoSavior.Enemies;
using ChronoSavior.Systems;
using ChronoSavior.World;

namespace ChronoSavior.Tests.PlayMode
{

/// <summary>
/// 타격감(2026-10-24, 사용자 지시) — 원본 기준 히트스톱 길이·피격 플래시·화면 흔들림·화면 붉어짐·무적 깜빡임.
/// 화면은 배치 테스트로 못 보니, 연출이 "걸렸는지·원본 값인지·끝나면 원래대로인지"를 잰다.
/// </summary>
public class HitFeelTests
{
    readonly List<float> requested = new List<float>();
    void Collect(float s) => requested.Add(s);

    [SetUp]
    public void Setup()
    {
        CombatEvents.Reset();
        RunEvents.Reset();
        HitFeel.Reset();
        ProfileService.Reset();
        CombatModifiers.Reset();
        GravityWellZone.ClearAllForTests();
        GameState.Set(GameScene.Run);
        requested.Clear();
        CombatEvents.HitstopRequested += Collect;
    }

    [TearDown]
    public void Teardown()
    {
        foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            if (go != null && go.transform.parent == null &&
                (go.name.StartsWith("Test") || go.name == "GravityWellZone" || go.name == "GravityBurst"))
                Object.DestroyImmediate(go);
        CombatEvents.Reset();
        RunEvents.Reset();
        HitFeel.Reset();
        ProfileService.Reset();
        CombatModifiers.Reset();
        GravityWellZone.ClearAllForTests();
        GameState.Reset();
        Time.timeScale = 1f;
    }

    static Sprite TestSprite()
    {
        var tex = new Texture2D(8, 8);
        var px = new Color[64];
        for (int i = 0; i < px.Length; i++) px[i] = Color.white;
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 32f);
    }

    /// <summary>스포너가 하는 순서 그대로 — 프리팹 색(빨강)으로 생긴 뒤 스포너가 흰색(그림 원래 색)을 칠한다.</summary>
    static EnemyHealth NewEnemy(Vector3 pos)
    {
        var go = new GameObject("TestEnemy") { tag = "Enemy" };
        go.transform.position = pos;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = TestSprite();
        sr.color = new Color(0.85f, 0.2f, 0.2f); // Enemy 프리팹 색
        go.AddComponent<CircleCollider2D>().radius = 0.3f;
        var move = go.AddComponent<EnemyMove>();
        var health = go.AddComponent<EnemyHealth>();
        typeof(EnemyMove).GetField("spawnProtectTimer", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(move, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        move.moveSpeed = 0f;
        health.SetMaxHp(100000f);
        sr.color = Color.white; // 스포너: 정식 그림이면 흰색
        return health;
    }

    // ---------------- 히트스톱 ----------------

    [Test]
    public void Hit_RequestsHitstopByAttack_LikeOriginal()
    {
        var e = NewEnemy(Vector3.zero);
        e.TakeDamageWithKnockback(1f, null, 1f, 0f);
        Assert.AreEqual(new[] { HitFeel.HitstopHit }, requested.ToArray(), "기본 직접 타격은 0.045");

        requested.Clear();
        e.TakeDamageWithKnockback(1f, null, 1f, 0f, HitFeel.HitstopSkill);
        Assert.AreEqual(new[] { 0.07f }, requested.ToArray(), "스킬 타격은 원본 CONFIG.hitstop.skill");

        requested.Clear();
        e.TakeDamageWithKnockback(1f, null, 1f, 0f, HitFeel.HitstopNone);
        e.TakeTickDamage(1f);
        Assert.IsEmpty(requested, "noHitstop(메카닉 총알 등)·틱 피해는 멈추면 안 된다");
    }

    [Test]
    public void KillFeel_MatchesOriginal()
    {
        Assert.AreEqual((0.085f, 0f, 0f), CombatDirector.KillFeel(boss: false, shrine: false, elite: false));
        Assert.AreEqual((0.085f, 6f, 0.22f), CombatDirector.KillFeel(false, false, elite: true));
        Assert.AreEqual((0.25f, 14f, 0.5f), CombatDirector.KillFeel(boss: true, false, false), "보스 처치는 0.25(예전엔 0.085)");
        Assert.AreEqual((0.12f, 9f, 0.35f), CombatDirector.KillFeel(false, shrine: true, false), "성소 파괴는 0.12");
    }

    [Test]
    public void FireExplosion_UsesSkillHitstop_AndShakes()
    {
        NewEnemy(new Vector3(5f, 1f, 0f));
        bool hit = MageSkillEffects.SpawnFireExplosion(new Vector2(5f, 1f), 1, 1f, new Vector2(3f, 1f), 1);
        Assert.IsTrue(hit);
        CollectionAssert.Contains(requested, HitFeel.HitstopSkill);
        Assert.AreEqual(7f, HitFeel.ShakeMag, "적중한 화염 폭발은 shake(7, 0.18)(:1992)");
    }

    [Test]
    public void GravityImpact_HasNoHitstop()
    {
        NewEnemy(new Vector3(5f, 1f, 0f));
        MageSkillEffects.DetonateGravityOrb(new Vector2(5f, 1f), 1, 0.5f, 1);
        Assert.IsEmpty(requested, "중력탄 충돌은 원본 noHitstop(:2036)");
    }

    [Test]
    public void GravityCollapse_NotCritical_UsesLightHitstop()
    {
        NewEnemy(new Vector3(5f, 1f, 0f));
        GravityWellZone.SpawnOrMerge(new Vector2(5f, 1f), tier: 1, chargeK: 0.2f);
        var (count, critical, _) = MageSkillEffects.GravityCollapse(1, new Vector2(3f, 1f), 1, null);
        Assert.AreEqual(1, count);
        Assert.IsFalse(critical, "전제: 1층·1마리는 임계가 아니다");
        CollectionAssert.Contains(requested, HitFeel.HitstopLight);
        Assert.AreEqual(MageSkillEffects.CollapseShakePx, HitFeel.ShakeMag, "대붕괴 흔들림 — 원본 5에서 12로 키움(사용자 2026-10-28)");
    }

    // ---------------- 피격 플래시 ----------------

    [UnityTest]
    public IEnumerator Flash_LeavesEnemyColorAlone()
    {
        var e = NewEnemy(Vector3.zero);
        var sr = e.GetComponent<SpriteRenderer>();
        e.TakeDamage(1f, null);
        Assert.AreEqual(Color.white, sr.color, "플래시가 몹 색을 바꾸면 안 된다");
        yield return new WaitForSeconds(0.35f);
        Assert.AreEqual(Color.white, sr.color, "한 번 맞은 뒤 프리팹 색(빨강)으로 물들었다");
    }

    [UnityTest]
    public IEnumerator Flash_IsWhiteSilhouetteOnTop_ThenHides()
    {
        var e = NewEnemy(Vector3.zero);
        var sr = e.GetComponent<SpriteRenderer>();
        e.TakeDamage(1f, null);

        var f = e.FlashRenderer;
        Assert.IsNotNull(f, "플래시 렌더러가 안 생겼다(셰이더 Resources/Shaders/SpriteFlash 확인)");
        Assert.IsTrue(f.enabled);
        Assert.AreSame(sr.sprite, f.sprite, "몸 모양(같은 그림)으로 덮어야 한다");
        Assert.AreEqual(sr.sortingOrder + 1, f.sortingOrder, "몸 위에 그려야 한다");
        Assert.AreEqual(0.75f, f.color.a, 0.01f, "원본 flash × 0.75(:4799)");
        Assert.AreEqual(Color.white.r, f.color.r);

        yield return new WaitForSeconds(0.35f); // 원본 초당 6씩 줄어 약 0.17초
        Assert.AreEqual(0f, e.FlashAmount, 1e-4f);
        Assert.IsFalse(f.enabled, "플래시가 끝났는데 흰빛이 남아 있다");
    }

    // ---------------- 화면 흔들림 ----------------

    [Test]
    public void Shake_TakesLargerValues_AndDecaysLikeOriginal()
    {
        HitFeel.Shake(5f, 0.1f);
        HitFeel.Shake(9f, 0.35f);
        HitFeel.Shake(3f, 0.2f); // 더 작은 요청은 덮어쓰지 않는다
        Assert.AreEqual(9f, HitFeel.ShakeMag);
        Assert.AreEqual(0.35f, HitFeel.ShakeTime, 1e-5f);

        Assert.AreEqual(9f * 0.25f / 0.18f, HitFeel.TickShake(0.1f), 1e-3f, "원본 m = mag × t / 0.18");
        Assert.AreEqual(0f, HitFeel.TickShake(0.3f), 1e-5f);
        Assert.AreEqual(0f, HitFeel.ShakeMag, "끝나면 세기도 0으로");
    }

    [UnityTest]
    public IEnumerator Camera_ShakeOffset_StaysWithinOriginalAmplitude()
    {
        var target = new GameObject("TestTarget").transform;
        var cam = new GameObject("TestCamera");
        cam.AddComponent<Camera>().orthographic = true;
        var follow = cam.AddComponent<CameraFollow2D>();
        CameraFollow2D.Track(target);
        yield return null;

        HitFeel.Shake(10f, 0.18f);
        yield return null;
        float maxM = 10f * CameraFollow2D.ShakePxToUnits * HitFeel.ShakeStrength; // 처음 프레임 크기 이하(세기 배율 포함)
        Assert.LessOrEqual(Mathf.Abs(follow.LastShakeOffset.x), maxM + 1e-5f);
        Assert.LessOrEqual(Mathf.Abs(follow.LastShakeOffset.y), maxM + 1e-5f);

        yield return new WaitForSecondsRealtime(0.3f);
        yield return null;
        Assert.AreEqual(Vector3.zero, follow.LastShakeOffset, "흔들림이 끝났는데 카메라가 제자리로 안 돌아왔다");
    }

    /// <summary>
    /// 시간이 멈춘 화면(타이틀·일시정지·결과·히트스톱 — timeScale 0)에서 카메라가 목표에 정확히 서 있으면 `Mathf.SmoothDamp`가
    /// 속도를 0/0으로 계산해 NaN을 만들고, 그 뒤 카메라 위치가 영영 NaN이 됐다(2026-11-04 씬 부팅 테스트가 잡음).
    /// </summary>
    [UnityTest]
    public IEnumerator Camera_WhileTimeIsFrozen_StaysPut_NoNaN()
    {
        var target = new GameObject("TestTarget").transform;
        target.position = new Vector3((FieldBounds.MinX + FieldBounds.MaxX) * 0.5f, FieldBounds.GroundY, 0f); // 필드 가운데(가장자리 고정에 안 걸리게)
        var cam = new GameObject("TestCamera");
        cam.AddComponent<Camera>().orthographic = true;
        cam.AddComponent<CameraFollow2D>();
        CameraFollow2D.Track(target);
        yield return null; // 첫 프레임에 목표 위치로 붙는다
        var settled = cam.transform.position;

        Time.timeScale = 0f;
        try
        {
            for (int i = 0; i < 5; i++) yield return null;
            Assert.IsFalse(float.IsNaN(cam.transform.position.x) || float.IsNaN(cam.transform.position.y), "멈춘 시간에 카메라가 NaN이 됐다");
            Assert.AreEqual(settled, cam.transform.position, "멈춘 동안엔 따라가지 않는다");
        }
        finally { Time.timeScale = 1f; }

        target.position += Vector3.right * 2f;
        yield return new WaitForSeconds(0.3f);
        Assert.Greater(cam.transform.position.x, settled.x, "시간이 다시 흐르면 정상적으로 따라간다");
    }

    /// <summary>
    /// "바로 붙기" 요청(`Track(t, snap: true)`, 로비 기준점)은 **그 대상에만** 걸린다 — 다음 프레임 전에 다른 대상으로 바꾸면 취소돼서, 새 대상(사냥의 플레이어)엔
    /// 예전처럼 부드럽게 간다(2026-11-05 Codex 2b 리뷰: 요청이 남아 있으면 사냥 시작 때 순간 이동했다).
    /// </summary>
    [UnityTest]
    public IEnumerator Camera_SnapRequest_AppliesOnlyToItsOwnTarget()
    {
        float y = FieldBounds.GroundY + EntitySizeConfig.PlayerRadius;
        var a = new GameObject("TestTarget").transform;
        a.position = new Vector3(6f, y, 0f);
        var b = new GameObject("TestTargetB").transform;
        b.position = new Vector3(18f, y, 0f);
        var cam = new GameObject("TestCamera");
        cam.AddComponent<Camera>().orthographic = true;
        cam.AddComponent<CameraFollow2D>();
        CameraFollow2D.Track(a);
        yield return null; // 첫 프레임엔 대상에 붙는다

        CameraFollow2D.Track(a, snap: true);
        CameraFollow2D.Track(b); // 프레임이 돌기 전에 다른 대상으로 — 바로 붙기 요청은 취소돼야 한다
        yield return null;
        Assert.Less(cam.transform.position.x, 17f, "다른 대상으로 바꿨는데 남은 바로 붙기 요청으로 순간 이동했다");

        CameraFollow2D.Track(b, snap: true);
        yield return null;
        Assert.AreEqual(18f, cam.transform.position.x, 0.01f, "자기 대상의 바로 붙기 요청은 그 프레임에 바로 붙는다");
        CameraFollow2D.Untrack(b);
    }

    // ---------------- 플레이어가 맞을 때 ----------------

    static PlayerHealth NewPlayer()
    {
        var go = new GameObject("TestPlayer") { tag = "Player" };
        go.AddComponent<SpriteRenderer>().sprite = TestSprite();
        go.AddComponent<Rigidbody2D>().gravityScale = 0f;
        go.AddComponent<CircleCollider2D>();
        return go.AddComponent<PlayerHealth>();
    }

    [Test]
    public void PlayerHurt_FlashesScreenRed_AndShakes()
    {
        var p = NewPlayer();
        p.TakeDamage(1f, null);
        Assert.AreEqual(0.35f, HitFeel.RedFlash, 1e-5f, "원본 state.redFlash = 0.35(:1912)");
        Assert.AreEqual(7f, HitFeel.ShakeMag, "원본 shake(7, 0.22)(:1913)");
        HitFeel.TickRedFlash(0.1f);
        Assert.AreEqual(0.35f - 0.22f, HitFeel.RedFlash, 1e-5f, "초당 2.2씩 줄어든다(:4426)");
        HitFeel.TickRedFlash(5f);
        Assert.AreEqual(0f, HitFeel.RedFlash);
    }

    [UnityTest]
    public IEnumerator Invulnerable_PlayerBlinks_DuringRun_ThenShowsAgain()
    {
        var p = NewPlayer();
        var sr = p.GetComponent<SpriteRenderer>();
        p.GrantInvuln(0.6f);
        bool sawHidden = false, sawShown = false;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            yield return null;
            if (sr.enabled) sawShown = true; else sawHidden = true;
        }
        Assert.IsTrue(sawHidden && sawShown, "무적 동안 1/14초마다 깜빡여야 한다(원본 :6195)");
        yield return new WaitForSeconds(0.4f);
        Assert.IsTrue(sr.enabled, "무적이 끝났는데 캐릭터가 안 보인다");
    }

    [Test]
    public void Blink_OnlyForOneShotInvuln_NotPerFrameImmunity()
    {
        var hit = NewPlayer();
        hit.TakeDamage(1f, null);
        Assert.AreEqual(hit.invulnTime, hit.BlinkRemaining, 1e-4f, "맞은 뒤 무적은 깜빡인다(원본 :1907)");

        var tele = NewPlayer();
        tele.GrantInvuln(MageSpecConfig.TeleportInvuln);
        Assert.AreEqual(MageSpecConfig.TeleportInvuln, tele.BlinkRemaining, 1e-4f, "순간이동 무적은 깜빡인다(원본 :2149)");

        var blade = NewPlayer();
        blade.GrantInvuln(2f / 60f); // BladeCombat이 방향키를 누르는 동안 매 프레임 주는 양
        Assert.Greater(blade.InvulnRemaining, 0f, "상시 무적도 피해는 막는다");
        Assert.AreEqual(0f, blade.BlinkRemaining, "매 프레임 갱신하는 상시 무적(섬영)은 깜빡이지 않는다(원본 :1889)");
    }

    [UnityTest]
    public IEnumerator BladeStyleSustainedImmunity_BlocksHits_WithoutBlinking()
    {
        var p = NewPlayer();
        var sr = p.GetComponent<SpriteRenderer>();
        yield return null; // Start(최대 체력 계산) 뒤에 잰다
        float hp = p.CurrentHp;
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            p.GrantInvuln(2f / 60f);
            p.TakeDamage(5f, null);
            yield return null;
            Assert.IsTrue(sr.enabled, "섬영 상시 무적 중에 캐릭터가 깜빡였다");
        }
        Assert.AreEqual(hp, p.CurrentHp, "상시 무적인데 맞았다");
    }
}

}
